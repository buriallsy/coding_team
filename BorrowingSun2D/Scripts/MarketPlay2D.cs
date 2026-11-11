using System;
using System.Collections.Generic;
using System.Linq;
using BorrowingSun2D.Core;
using Godot;

namespace BorrowingSun2D;

internal sealed class MarketPlay2D
{
    internal const int ShuttleWaterCost = 1;
    private sealed record Point(Marker2D Marker, ForageKind Kind, string? LightTarget);
    private readonly TowerSlice scene;
    private readonly Random random;
    private readonly Point[] points;
    private readonly Marker2D store;
    private readonly MarketPanel panel;
    private ForageSpot[] spots = [];
    private MarketPhase previousPhase;
    private int previousDay;
    private float shuttleProgress;
    private bool towardFlowerCourt;
    private string feedback = "";
    private float feedbackTime;

    internal MarketRun Run { get; }
    internal bool Raining { get; set; }
    internal bool ShuttleMoving { get; private set; }
    internal bool OnShuttle => scene.Player.IsOnFloor() && scene.CargoTrack.IsOnDeck(scene.Player.Feet);
    internal Vector2 Store => store.GlobalPosition;
    internal string Hint => feedbackTime > 0f ? feedback : ShuttleMoving
        ? "摆渡中，等车停稳"
        : OnShuttle ? $"G 驶向{(shuttleProgress == 0f ? "花庭" : "东岸")} · 耗水 {ShuttleWaterCost}"
        : Run.IsNight ? "夜间没有阳光 · 按 C 采集，或点击返店" : "靠近采集点按 C · 用原有机关连通各岸";

    internal MarketPlay2D(TowerSlice scene, Random? seed = null)
    {
        this.scene = scene;
        random = seed ?? new Random();
        Run = new MarketRun(random);
        store = scene.GetNode<Marker2D>("Lift/MarketStore");
        points =
        [
            new(scene.GetNode<Marker2D>("Lift/MushroomPatch"), ForageKind.MushroomPatch, null),
            new(scene.GetNode<Marker2D>("Entry/MushroomPatch"), ForageKind.MushroomPatch, null),
            new(scene.GetNode<Marker2D>("Entry/WildPatch"), ForageKind.WildPatch, null),
            new(scene.GetNode<Marker2D>("SunCourt/FruitTree"), ForageKind.Tree, "collector"),
            new(scene.GetNode<Marker2D>("SunCourt/WildPatch"), ForageKind.WildPatch, null),
            new(scene.GetNode<Marker2D>("East/FruitTree"), ForageKind.Tree, "receiver"),
            new(scene.GetNode<Marker2D>("East/WildPatch"), ForageKind.WildPatch, null),
            new(scene.GetNode<Marker2D>("Delivery/WildPatch"), ForageKind.WildPatch, null),
        ];
        panel = new MarketPanel(scene, this);
    }

    internal IEnumerable<(Vector2 Position, bool Tree, FoodKind? Food)> ResourceViews()
    {
        for (int i = 0; i < spots.Length; i++)
            yield return (points[i].Marker.GlobalPosition, points[i].Kind == ForageKind.Tree, spots[i].Produce);
    }

    internal void BeginRun()
    {
        if (!Run.Start()) return;
        spots = points.Select(point => new ForageSpot(point.Kind, random.Next(2, 7))).ToArray();
        previousDay = 0;
        SynchronizePhase();
    }

    internal void TickExploration(float delta)
    {
        feedbackTime = Math.Max(0f, feedbackTime - delta);
        AdvanceShuttle(delta);
        for (int i = 0; i < points.Length; i++)
        {
            bool lit = points[i].LightTarget switch
            {
                "collector" => scene.Optics.Path.Incoming?.Hit == LightHit2D.Collector,
                "receiver" => scene.Optics.ReceiverLit,
                _ => false,
            };
            spots[i].Tick(delta, new ForageEnvironment(Run.IsNight, Raining, lit), random);
        }
        if (Input.IsActionJustPressed("harvest")) TryHarvest();
    }

    internal void TickClock(float delta)
    {
        Run.Tick(delta);
        SynchronizePhase();
    }

    internal void SynchronizePhase()
    {
        if (previousDay != Run.Day)
        {
            previousDay = Run.Day;
            Raining = false;
            ShuttleMoving = false;
            shuttleProgress = 0f;
            feedbackTime = 0f;
            scene.ResetForMarket();
            if (Run.Day > 1) Say($"昨晚实收 +{Run.LastNightRevenue} 金币 · 钱箱 {Run.Balance} 金币");
        }
        else if (previousPhase != Run.Phase)
        {
            if (Run.IsNight) scene.Optics.Tick(scene.State.Heading, sunlightAvailable: false);
            if (Run.Phase == MarketPhase.Menu) scene.ReturnToStore();
        }
        previousPhase = Run.Phase;
        panel.Refresh();
    }

    private void TryHarvest()
    {
        if (!scene.Player.IsOnFloor()) return;

        Vector2 feet = scene.Player.Feet;
        int nearest = -1;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 offset = feet - points[i].Marker.GlobalPosition;
            if (spots[i].Produce == null || Mathf.Abs(offset.X) > 30f || Mathf.Abs(offset.Y) > 10f
                || offset.LengthSquared() >= nearestDistance) continue;
            nearest = i;
            nearestDistance = offset.LengthSquared();
        }

        if (nearest >= 0 && spots[nearest].Harvest(random) is FoodKind food)
        {
            Run.Bag.Add(food);
            Say($"采到 {MarketText.FoodName(food)} ×1");
        }
        else Say("脚边还没有成熟的食材");
    }

    private void AdvanceShuttle(float delta)
    {
        if (!ShuttleMoving && Input.IsActionJustPressed("cargo_action", exactMatch: true))
        {
            if (!OnShuttle)
            {
                Say("走上摆渡车后按 G");
                return;
            }
            towardFlowerCourt = shuttleProgress == 0f;
            // Keep a return fare in the tower when leaving the east shore.
            if (towardFlowerCourt && scene.State.Water < ShuttleWaterCost * 2)
            {
                Say($"往返至少需要 {ShuttleWaterCost * 2} 单位水 · 先回塔台降雨");
                return;
            }
            if (!scene.TrySpendWater(ShuttleWaterCost))
            {
                Say($"返航需要 {ShuttleWaterCost} 单位水");
                return;
            }

            // At a dock the static shore and the carrier overlap. Board the
            // moving floor itself so the shore cannot retain platform contact.
            scene.Player.Respawn(scene.CargoTrack.SafeFeet);
            ShuttleMoving = true;
            Say($"已用 {ShuttleWaterCost} 单位水 · 驶向{(towardFlowerCourt ? "花庭" : "东岸")}");
            return;
        }
        if (!ShuttleMoving) return;
        float destination = towardFlowerCourt ? 1f : 0f;
        shuttleProgress = Mathf.MoveToward(shuttleProgress, destination, delta / CargoJourney.TravelSeconds);
        scene.CargoTrack.Synchronize(shuttleProgress);
        if (shuttleProgress == destination) ShuttleMoving = false;
    }

    internal void Say(string message)
    {
        feedback = message;
        feedbackTime = 2f;
    }
}

internal static class MarketText
{
    internal static string FoodName(FoodKind food) => food switch
    {
        FoodKind.Mushroom => "蘑菇",
        FoodKind.GameMeat => "野味",
        FoodKind.SunFruit => "晴果",
        FoodKind.RainFruit => "雨露果",
        FoodKind.MoonFruit => "月果",
        _ => "雨夜果",
    };

    internal static string Stock(Inventory bag) =>
        string.Join("  ·  ", Enum.GetValues<FoodKind>().Select(f => $"{FoodName(f)} {bag.Count(f)}"));

    internal static string Ingredients(Recipe recipe) =>
        string.Join(" + ", recipe.Ingredients.Select(i => $"{FoodName(i.Food)} ×{i.Quantity}"));
}
