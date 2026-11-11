using System;
using BorrowingSun2D.Core;

internal static class MarketChecks
{
    internal static void Run(Action<bool, string> check)
    {
        CheckForage(check);
        CheckNightChoice(check);
        CheckShuttleWater(check);
        CheckOrders(check);
    }

    private static void CheckForage(Action<bool, string> check)
    {
        var random = new Random(27);
        var tree = new ForageSpot(ForageKind.Tree, 2);
        tree.Tick(1, new(false, false, true), random);
        check(tree.Produce == null, "fruit has a growth delay");
        tree.Tick(1, new(false, false, true), random);
        check(tree.Produce == FoodKind.SunFruit, "lit tree produces sunny fruit");
        tree.Tick(0, new(false, true, true), random);
        check(tree.Produce == FoodKind.RainFruit, "existing fruit changes in daylight rain");
        tree.Tick(0, new(true, true, true), random);
        check(tree.Produce == FoodKind.StormFruit, "night rain takes precedence over light");
        var bag = new Inventory();
        bag.Add(tree.Harvest(random)!.Value);
        check(tree.Harvest(random) == null, "no duplicate harvest");
        tree.Tick(7, new(true, false, false), random);
        check(tree.Produce == null, "empty slot waits at least eight seconds");
        tree.Tick(7, new(true, false, false), random);
        check(tree.Produce == FoodKind.MoonFruit, "empty slot refills within fourteen seconds");
        tree.Tick(0, new(false, false, false), random);
        check(tree.Produce == null, "unlit dry tree has no harvestable fruit");
        tree.Tick(0, new(false, false, true), random);
        check(tree.Produce == FoodKind.SunFruit && bag.Count(FoodKind.StormFruit) == 1,
            "mature slot adapts while harvested inventory stays unchanged");
    }

    private static void CheckNightChoice(Action<bool, string> check)
    {
        var run = new MarketRun(new Random(31));
        check(run.Start() && run.Day == 1 && run.Remaining == 180, "first day starts after tutorial");
        run.Bag.Add(FoodKind.Mushroom, 4);
        run.Tick(180);
        check(run.Phase == MarketPhase.Dusk && run.IsNight, "day ends at dusk");
        run.Tick(999);
        check(run.Phase == MarketPhase.Dusk && run.Shop.OrderAt(0) == null,
            "dusk pauses the clock and has no customers");
        check(run.ChooseNight(true) && run.Remaining == 90, "night exploration is half the day");
        run.Tick(90);
        check(run.Phase == MarketPhase.Menu && run.Shop.OrderAt(0) == null,
            "night exploration ends at menu selection, with no generated orders");
        check(run.OpenShop(["mushroom"]) && run.Remaining == 60, "night exploration halves business time");
        run.Tick(60);
        check(run.Day == 2 && run.Bag.Count(FoodKind.Mushroom) == 4,
            "closing releases reservations and carries stock to day two");
        run.Tick(180);
        check(run.ChooseNight(false) && !run.OpenShop([]), "direct return requires a chosen menu");
        check(run.OpenShop(["mushroom"]) && run.Remaining == 120, "direct return gives full business time");
    }

    private static void CheckShuttleWater(Action<bool, string> check)
    {
        var tower = new TowerMechanism();
        check(!tower.TrySpendWater(1), "an empty tank cannot pay a ferry fare");
        tower.Cast(Weather.Rain, false, TurnDirection.Clockwise);
        tower.Tick(5);
        tower.Cast(Weather.Wind, false, TurnDirection.Clockwise);
        tower.Tick(2);
        tower.Cast(Weather.Wind, false, TurnDirection.Clockwise);
        tower.Tick(2);
        check(tower.TrySpendWater(1) && tower.Water == 5 && tower.Latched && tower.DockedPort == Port.East,
            "ferry fare draws real water without releasing the lift lock");
        tower.TransferToCounterweight(4, 4);
        check(!tower.TrySpendWater(2) && tower.TrySpendWater(1) && tower.Counterweight == 4 && tower.Water == 0,
            "fares cannot spend water committed to counterweight");
    }

    private static void CheckOrders(Action<bool, string> check)
    {
        var run = new MarketRun(new Random(49));
        run.Start();
        run.Bag.Add(FoodKind.Mushroom, 2);
        run.Bag.Add(FoodKind.SunFruit, 20);
        run.Bag.Add(FoodKind.MoonFruit, 20);
        run.Tick(180);
        run.ChooseNight(false);
        check(!run.TryUpgradeKitchen(), "upgrade requires earned coins");
        run.OpenShop(["mushroom"]);
        check(run.Shop.OrderAt(0)?.Recipe.Id == "mushroom" && run.Shop.OrderAt(1) == null,
            "one affordable menu order reserves its ingredients before the second queue");
        check(run.TryCook(0) && run.Bag.Count(FoodKind.Mushroom) == 0 && !run.TryServe(0),
            "loading consumes ingredients and serving must wait");
        check(run.EndEvening() && run.Bag.Count(FoodKind.Mushroom) == 2 && run.Balance == 0,
            "closing refunds an unfinished dish without payment");

        run.Bag.Add(FoodKind.Mushroom, 18);
        run.Tick(180);
        run.ChooseNight(false);
        check(run.OpenShop(["mushroom"]), "stocked menu opens");
        CustomerOrder? other = run.Shop.OrderAt(1);
        check(other != null && run.Shop.OrderAt(2) == null, "two slots maximum");
        check(run.TryCook(0) && run.TryCook(1), "both burners can cook at once");
        run.Tick(1);
        check(run.Shop.OrderAt(0)?.State == OrderState.Cooking && other?.State == OrderState.Cooking,
            "both orders advance independently before they are ready");
        check(run.EndEvening() && run.Bag.Count(FoodKind.Mushroom) == 20,
            "closing refunds both unfinished burners");

        run.Tick(180);
        run.ChooseNight(false);
        run.OpenShop(["mushroom"]);
        run.TryCook(0);
        run.Tick(1);
        run.TryCook(1);
        other = run.Shop.OrderAt(1);
        run.Tick(2);
        check(run.TryServe(0) && run.Balance == 12 && !run.TryServe(0),
            "ready order pays once");
        check(ReferenceEquals(run.Shop.OrderAt(1), other) && other?.State == OrderState.Cooking &&
            run.Shop.OrderAt(0) != null, "only the paid window refills while the second burner keeps cooking");
        run.TryCook(0);
        run.Tick(3);
        check(run.TryServe(0) && run.TryServe(1) && run.TonightRevenue == 36, "three sales make 36 coins");
        check(run.EndEvening() && run.Day == 4 && run.Balance == 36 && run.LastNightRevenue == 36 &&
            run.TonightRevenue == 0, "earned funds and last night's receipts carry into the next day");
        run.Bag.Add(FoodKind.Mushroom, 20);
        run.Tick(180);
        run.ChooseNight(false);
        check(run.TryUpgradeKitchen() && !run.TryUpgradeKitchen() && run.Balance == 0 && run.TotalRevenue == 36,
            "buying the upgrade once spends balance but keeps gross revenue");
        run.OpenShop(["mushroom"]);
        check(run.TryCook(0) && run.TryCook(1), "both upgraded burners start together");
        run.Tick(2);
        check(run.Shop.OrderAt(0)?.State == OrderState.Cooking && run.Shop.OrderAt(1)?.State == OrderState.Cooking,
            "upgraded cooking still needs time");
        run.Tick(.11f);
        check(run.TryServe(0) && run.TryServe(1), "both upgraded burners finish in 2.1 seconds instead of 3");
        for (int i = 0; i < 7; i++)
        {
            int slot = run.Shop.OrderAt(0) == null ? 1 : 0;
            check(run.TryCook(slot), "remaining stock can cook a queued order");
            run.Tick(3);
            check(run.TryServe(slot), "serving completes the queued order");
        }
        check(run.Phase == MarketPhase.Business && run.TotalRevenue == 144 && run.Balance == 108,
            "business continues past the previous revenue threshold");
        run.EndEvening();

        for (int i = 0; i < 370; i++)
        {
            run.Tick(180);
            run.ChooseNight(false);
            run.EndEvening();
        }
        check(run.Day == 375 && run.Phase == MarketPhase.Day && run.Shop.KitchenUpgraded, "no calendar deadline and upgrade persists");
    }
}
