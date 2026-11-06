using System;
using BorrowingSun2D.Core;
using Godot;

namespace BorrowingSun2D;

public partial class TowerSlice : Node2D
{
    private const float SurfaceTolerance = 8f;
    private const float StationRange = 82f;
    private const float FallDistance = 64f;

    private readonly TowerMechanism mechanism = new();
    private AnimatableBody2D lift = null!;
    private CollisionShape2D entry = null!;
    private CollisionShape2D sunCourt = null!;
    private CollisionShape2D deck = null!;
    private CollisionShape2D bridgeHinge = null!;
    private CollisionShape2D bridgeExtension = null!;
    private Marker2D station = null!;
    private Marker2D spawn = null!;
    private Marker2D goal = null!;
    private float lowTop;
    private float highTop;
    private float lastGroundY;
    private bool bridgeConnected;
    private bool deckCheckpoint;
    private float time;
    private float messageUntil;
    private string message = "";

    internal SlicePlayer Player { get; private set; } = null!;
    internal TowerState State => mechanism.State;
    internal Weather SelectedWeather { get; private set; } = Weather.Rain;
    internal bool Completed { get; private set; }
    internal int Recoveries { get; private set; }
    internal bool BridgeConnected => bridgeConnected;
    internal bool BridgeCollisionEnabled => !bridgeExtension.Disabled;
    internal MechanismResult? LastResult { get; private set; }
    internal Rect2 EntryFloor => Surface(entry);
    internal Rect2 CourtFloor => Surface(sunCourt);
    internal Rect2 Deck => Surface(deck);
    internal Rect2 BridgeHinge => Surface(bridgeHinge);
    internal Rect2 BridgeExtension => Surface(bridgeExtension);
    internal Vector2 Goal => goal.Position + goal.GetParent<Node2D>().Position;
    internal bool AtStation => Player.IsOnFloor() && OnLiftHeight
        && Mathf.Abs(Player.Feet.X - station.GlobalPosition.X) <= StationRange;
    internal bool DoorwayOccupied => Player.IsOnFloor() && OnLiftHeight
        && Player.Feet.X >= BridgeExtension.Position.X - 8f && Player.Feet.X <= Deck.Position.X + 40f;

    private bool OnLiftHeight => Mathf.Abs(Player.Feet.Y - Deck.Position.Y) <= SurfaceTolerance;

    internal string Status
    {
        get
        {
            if (time < messageUntil) return message;
            if (Completed) return "已抵达采光庭 · R 重新开始";
            if (mechanism.IsTurning) return "风正在转动桥舌 · 等待停稳";
            if (mechanism.Draining) return "排水下降 · F 停止排水";
            if (mechanism.IsLifting) return "集雨升塔 · Q 收回雨令";
            if (mechanism.DockedPort == Port.SunCourt) return "高位已接桥 · 向左走到采光庭";
            if (mechanism.Latched) return "机械锁已扣合 · 选风，向塔台施令";
            if (AtStation) return "对塔台施雨，升至采光庭的高度";
            if (mechanism.Height > 0f) return "塔台停在当前水位 · 可继续施雨";
            return "向右走过低位接桥，靠近塔台设备";
        }
    }

    public override void _Ready()
    {
        Player = GetNode<SlicePlayer>("Player");
        lift = GetNode<AnimatableBody2D>("Lift");
        entry = GetNode<CollisionShape2D>("Entry/Surface");
        sunCourt = GetNode<CollisionShape2D>("SunCourt/Surface");
        deck = GetNode<CollisionShape2D>("Lift/Deck");
        bridgeHinge = GetNode<CollisionShape2D>("Lift/BridgeHinge");
        bridgeExtension = GetNode<CollisionShape2D>("Lift/BridgeExtension");
        station = GetNode<Marker2D>("Lift/WeatherStation");
        spawn = GetNode<Marker2D>("Entry/Spawn");
        goal = GetNode<Marker2D>("SunCourt/Goal");
        lowTop = EntryFloor.Position.Y;
        highTop = CourtFloor.Position.Y;
        lastGroundY = lowTop;
        bridgeConnected = true;

        if (Array.IndexOf(OS.GetCmdlineUserArgs(), "--verify") >= 0)
            new TowerRouteCheck(this).Run();
    }

    public override void _PhysicsProcess(double delta)
    {
        float step = (float)delta;
        time += step;
        if (Input.IsActionJustPressed("reset_slice"))
        {
            ResetSlice();
            return;
        }

        ReadCommands();
        mechanism.Tick(step);
        UpdateLiftGeometry();
        Player.Step(step);
        TrackPlayer();
    }

    private void ReadCommands()
    {
        if (Input.IsActionJustPressed("select_rain")) SelectedWeather = Weather.Rain;
        if (Input.IsActionJustPressed("select_wind")) SelectedWeather = Weather.Wind;
        if (Input.IsActionJustPressed("recall_weather") && mechanism.ActiveWeather != Weather.None)
        {
            mechanism.Recall();
            Say("天气令已收回 · 已存水和当前朝向保留");
        }

        bool cast = Input.IsActionJustPressed("cast_weather");
        bool drain = Input.IsActionJustPressed("toggle_drain");
        if (!cast && !drain) return;
        if (!AtStation && !DoorwayOccupied)
        {
            LastResult = null;
            Say("先站上塔台，靠近中央的天气设备");
            return;
        }

        LastResult = cast ? mechanism.Cast(SelectedWeather, DoorwayOccupied)
            : mechanism.ToggleDrain(DoorwayOccupied);
        if (LastResult == MechanismResult.Applied)
        {
            messageUntil = 0f;
            return;
        }

        Say(LastResult switch
        {
            MechanismResult.DoorwayOccupied => "先离开接桥门槛，再操作塔台",
            MechanismResult.Moving => "塔台正在运动，停稳后再操作",
            MechanismResult.NeedHigh => "先用雨把塔台升至高位",
            MechanismResult.NeedEntryHeading => "先用风转回入口朝向，再放水",
            MechanismResult.Empty => "水箱已空",
            _ => "水箱已满 · 可以换风",
        });
    }

    private void UpdateLiftGeometry()
    {
        float top = Mathf.Lerp(lowTop, highTop, mechanism.Height / TowerMechanism.TravelMetres);
        if (!Mathf.IsEqualApprox(lift.Position.Y, top))
            lift.Position = new Vector2(lift.Position.X, top);

        bool connected = mechanism.DockedPort != Port.None;
        if (connected == bridgeConnected) return;
        bridgeConnected = connected;
        bridgeExtension.SetDeferred(CollisionShape2D.PropertyName.Disabled, !connected);
    }

    private void TrackPlayer()
    {
        if (Player.IsOnFloor())
        {
            lastGroundY = Player.Feet.Y;
            if (AtStation && !DoorwayOccupied) deckCheckpoint = true;
            if (!Completed && mechanism.DockedPort == Port.SunCourt && Player.Feet.X <= Goal.X
                && Mathf.Abs(Player.Feet.Y - highTop) <= SurfaceTolerance)
            {
                Completed = true;
                messageUntil = 0f;
            }
            return;
        }

        if (Player.Feet.Y - lastGroundY <= FallDistance && Player.Feet.Y <= 690f) return;
        Player.Respawn(deckCheckpoint ? station.GlobalPosition + Vector2.Up : spawn.GlobalPosition);
        lastGroundY = Player.Feet.Y;
        Recoveries++;
        Say("失足 · 已回到最近到达的安全平台");
    }

    private void ResetSlice()
    {
        mechanism.Reset();
        // Keep the animatable body's physics synchronization active through the reset.
        UpdateLiftGeometry();
        Player.Respawn(spawn.GlobalPosition);
        lastGroundY = lowTop;
        deckCheckpoint = Completed = false;
        Recoveries = 0;
        SelectedWeather = Weather.Rain;
        LastResult = null;
        messageUntil = 0f;
    }

    private void Say(string text)
    {
        message = text;
        messageUntil = time + 2f;
    }

    private static Rect2 Surface(CollisionShape2D collider)
    {
        Vector2 size = ((RectangleShape2D)collider.Shape).Size;
        return new Rect2(collider.GlobalPosition - size * .5f, size);
    }
}
