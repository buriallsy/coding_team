using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BorrowingSun2D.Core;
using Godot;

namespace BorrowingSun2D;

internal sealed class MarketRouteCheck(TowerSlice scene)
{
    private MarketPlay2D Play => scene.Market!;
    private MarketRun Run => Play.Run;

    internal async void RunCheck()
    {
        try
        {
            await Frames(4);
            await Pulse("ui_accept");
            await Walk("walk_right", () => scene.Player.Feet.X >= 655f);
            await RaiseAndLight();
            await Walk("walk_right", () => scene.IsAboardCargo);
            await Until(() => scene.CargoReadyToLaunch, 600, "tutorial loads and fills ballast");
            await Pulse("cargo_action");
            await Until(() => scene.CargoPhase == CargoPhase.Arrived, 250, "tutorial cargo arrives");
            await Walk("walk_right", () => scene.AtDeliveryStation);
            await Pulse("cargo_action");
            Require(scene.Screen == TowerScreen.Finished && scene.DeliveredCount == 1, "tutorial receipt");
            await Pulse("ui_accept");
            Require(Run.Phase == MarketPhase.Day && Run.Day == 1 &&
                scene.Player.Feet.DistanceTo(Play.Store) < 5f,
                $"day one starts at the central shop ({Run.Phase}, {scene.Player.Feet}, {Play.Store})");

            await HarvestMushrooms(7);
            Require(Run.Bag.Count(FoodKind.Mushroom) == 7, "C harvests seven actual spawned mushrooms");
            await Capture("market-day.png");

            await Walk("walk_left", () => scene.Player.Feet.X <= 660f);
            await RaiseAndLight();
            Require(Play.Raining, "foraging rain persists through wind and mirror commands");
            Require(Play.ResourceViews().Any(p => p.Tree && p.Food == FoodKind.RainFruit), "mature trees produce rain fruit");
            await Pulse("recall_weather");
            Require(!Play.Raining && Play.ResourceViews().All(p => !p.Tree || p.Food == FoodKind.SunFruit),
                "same mature trees change to sunny fruit under the actual light path");

            await Walk("walk_right", () => Play.OnShuttle);
            for (int round = 0; round < 3; round++)
            {
                float water = scene.State.Water;
                await Pulse("cargo_action");
                await Until(() => !Play.ShuttleMoving && scene.Player.Feet.X > 1160f, 250, "shuttle crosses to Flower Court");
                Require(scene.State.Water == water - MarketPlay2D.ShuttleWaterCost, "outbound fare spends tower water");
                await Walk("walk_right", () => scene.AtDeliveryStation);
                Require(!Play.OnShuttle, "player can disembark at Flower Court");
                await Walk("walk_left", () => Play.OnShuttle);
                Require(Play.OnShuttle && !scene.IsAboardCargo, "the deck edge is enough to board for return");
                await Pulse("cargo_action");
                await Until(() => !Play.ShuttleMoving && scene.Player.Feet.X < 1140f, 250, "shuttle returns to east after reboarding");
                Require(scene.State.Water == water - 2 * MarketPlay2D.ShuttleWaterCost, "return fare also spends water");
            }
            await Pulse("cargo_action");
            Require(!Play.ShuttleMoving && scene.State.Water == 0, "empty tank cannot depart again");

            await Until(() => Run.Phase == MarketPhase.Dusk, 11000, "day ends at the choice screen");
            Require(!scene.Optics.ReceiverLit && scene.Optics.Path.Incoming == null, "night extinguishes the previous beam");
            Click("NightForage");
            Require(Run.Phase == MarketPhase.NightForage && Run.Remaining == 90, "UI starts half-day night exploration");
            await Frames(3);
            Require(Play.ResourceViews().All(p => !p.Tree || p.Food == FoodKind.MoonFruit), "same trees change to moon fruit");
            await Walk("walk_left", () => scene.Player.Feet.X <= 660f);
            await Pulse("select_rain");
            await Pulse("cast_weather");
            Require(Play.ResourceViews().All(p => !p.Tree || p.Food == FoodKind.StormFruit) &&
                !scene.Optics.ReceiverLit, "rain gives night-rain fruit without sunlight");
            Click("ReturnToShop");
            Require(Run.Phase == MarketPhase.Menu && Run.Shop.OrderAt(0) == null &&
                scene.Player.Feet.DistanceTo(Play.Store) < 5f, "return to shop before orders generate");
            var dish = scene.FindChild("Recipe_mushroom", true, false) as CheckBox;
            Require(dish != null && !dish.Disabled, "the harvested stock enables the mushroom menu option");
            dish!.ButtonPressed = true;
            await Frames(2);
            await Capture("market-menu.png");
            Click("OpenShop");
            Require(Run.Phase == MarketPhase.Business && Run.Remaining == 60 &&
                Run.Shop.OrderAt(0) != null && Run.Shop.OrderAt(1) != null, "open selected menu with two customers and half shift");
            await Capture("market-business.png");
            Click("Order0");
            Click("Order1");
            Require(Run.Shop.OrderAt(0)?.State == OrderState.Cooking && Run.Shop.OrderAt(1)?.State == OrderState.Cooking &&
                Run.Bag.Count(FoodKind.Mushroom) == 3, "both customer buttons load and cook concurrently");
            await Capture("market-dual-cooking.png");
            await Until(() => Run.Shop.OrderAt(0)?.State == OrderState.Ready && Run.Shop.OrderAt(1)?.State == OrderState.Ready,
                200, "both meals are ready to serve");
            Click("Order0");
            Require(Run.Balance == 12 && Run.TonightRevenue == 12 && Run.Shop.OrderAt(0)?.State == OrderState.Waiting &&
                Run.Shop.OrderAt(1)?.State == OrderState.Ready, "payment credits the till and refills only its window");
            await Capture("market-receipt.png");
            Click("Order1");
            Click("Order0");
            await Until(() => Run.Shop.OrderAt(0)?.State == OrderState.Ready, 200, "next order is cooked");
            Click("Order0");
            Require(Run.Balance == 36 && Run.TonightRevenue == 36, "three receipts add up to the upgrade price");
            Click("CloseShop");
            await Frames(3);
            Require(Run.Day == 2 && Run.Balance == 36 && Run.LastNightRevenue == 36 &&
                Run.Bag.Count(FoodKind.Mushroom) == 1 &&
                scene.State.DockedPort == Port.Entry && scene.Player.IsOnFloor() &&
                scene.Player.Feet.DistanceTo(Play.Store) < 5f,
                "close carries stock and payment into a fresh central day");
            await HarvestMushrooms(1);
            await Until(() => Run.Phase == MarketPhase.Dusk, 11000, "second dusk");
            Click("DirectShop");
            Click("UpgradeKitchen");
            Require(Run.Shop.KitchenUpgraded && Run.Balance == 0 && Run.TotalRevenue == 36,
                "UI purchase spends yesterday's earnings and leaves gross sales unchanged");
            await Capture("market-kitchen-upgrade.png");
            dish = scene.FindChild("Recipe_mushroom", true, false) as CheckBox;
            Require(dish != null && !dish.Disabled && dish.Text.Contains("2.1"), "menu shows the improved cook time");
            dish!.ButtonPressed = true;
            await Frames(2);
            Click("OpenShop");
            Click("Order0");
            Require(Run.Shop.OrderAt(0) is { State: OrderState.Cooking, CookRemaining: < 2.2f and > 2f },
                "next shift uses the upgraded burner");
            await Until(() => Run.Shop.OrderAt(0)?.State == OrderState.Ready, 132, "upgraded mushroom dish finishes sooner");
            Click("Order0");
            Require(Run.Balance == 12 && Run.TotalRevenue == 48, "earnings resume after the purchase");
            GD.Print("MARKET_ROUTE PASSED");
            scene.GetTree().Quit();
        }
        catch (Exception e)
        {
            GD.PushError(e.ToString());
            scene.GetTree().Quit(1);
        }
    }

    private async Task HarvestMushrooms(int count)
    {
        await Walk("walk_right", () => scene.Player.Feet.X >= 706f);
        for (int i = 0; i < count; i++)
        {
            await Until(() => Play.ResourceViews().First().Food == FoodKind.Mushroom, 900, "central patch refreshes");
            await Pulse("harvest");
        }
    }

    private async Task RaiseAndLight()
    {
        await Pulse("select_rain");
        await Pulse("cast_weather");
        await Until(() => scene.State.Latched, 500, "rain raises the existing lift");
        await Pulse("select_wind");
        await Pulse("cast_weather");
        await Until(() => scene.State.DockedPort == Port.SunCourt, 160, "north dock");
        await Walk("walk_left", () => scene.AtSolarStation);
        await Pulse("cast_weather");
        await Pulse("select_sunny");
        await Pulse("cast_weather");
        await Walk("walk_right", () => scene.Player.Feet.X >= 655f);
        await Pulse("select_wind");
        await Pulse("cast_weather");
        await Until(() => scene.State.DockedPort == Port.East && scene.Optics.ReceiverLit, 170, "east dock and physical light");
    }

    private void Click(string name)
    {
        if (scene.FindChild(name, true, false) is not Button button || button.Disabled || !button.IsVisibleInTree())
            throw new InvalidOperationException($"Button not available: {name}");
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }

    private async Task Walk(string action, Func<bool> done)
    {
        Input.ActionPress(action);
        try { await Until(done, 360, $"walk {action} to target"); }
        finally { Input.ActionRelease(action); }
        await Frames(2);
    }

    private async Task Pulse(string action)
    {
        Input.ActionPress(action);
        await Frames(1);
        Input.ActionRelease(action);
        await Frames(2);
    }

    private async Task Until(Func<bool> predicate, int limit, string name)
    {
        for (int i = 0; i < limit && !predicate(); i++) await Frames(1);
        Require(predicate(), name);
    }

    private async Task Frames(int count)
    {
        for (int i = 0; i < count; i++)
            await scene.ToSignal(scene.GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private async Task Capture(string name)
    {
        string output = OS.GetEnvironment("BORROWING_SUN_TEST_CAPTURE_DIR");
        if (output.Length == 0 || DisplayServer.GetName() == "headless") return;
        await scene.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Error result = scene.GetViewport().GetTexture().GetImage().SavePng(Path.Combine(output, name));
        Require(result == Error.Ok, $"capture {name}");
    }

    private static void Require(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
        GD.Print($"PASS {name}");
    }
}
