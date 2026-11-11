using System;
using BorrowingSun.Market;

namespace BorrowingSun.Tests
{
    // Shared by NUnit and the offline runner. Every assertion exercises production domain code.
    public static class MarketBehaviorScenarios
    {
        public static int AssertionCount { get; private set; }

        public static (string Name, Action Run)[] Cases => new (string, Action)[]
        {
            (nameof(StartAndRepeatedCommandsPreserveSession), StartAndRepeatedCommandsPreserveSession),
            (nameof(OrdersAppearOnlyAfterMenuAndReserveWithoutOverbooking), OrdersAppearOnlyAfterMenuAndReserveWithoutOverbooking),
            (nameof(TwoBurnersAdvanceTogetherAndOnlyPaidWindowRefills), TwoBurnersAdvanceTogetherAndOnlyPaidWindowRefills),
            (nameof(ClosingReturnsCookingStockAndReleasesWaitingReservations), ClosingReturnsCookingStockAndReleasesWaitingReservations),
            (nameof(ClosingReturnsReadyMealsWithoutPaying), ClosingReturnsReadyMealsWithoutPaying),
            (nameof(BusinessTimeoutStopsAtBoundaryAndRefunds), BusinessTimeoutStopsAtBoundaryAndRefunds),
            (nameof(DirectEveningUsesExplicitReturnAndFullBusinessTime), DirectEveningUsesExplicitReturnAndFullBusinessTime),
            (nameof(NightForageUsesHalfDayAndHalfBusinessTime), NightForageUsesHalfDayAndHalfBusinessTime),
            (nameof(EarlyNightReturnDoesNotRestoreFullBusinessTime), EarlyNightReturnDoesNotRestoreFullBusinessTime),
            (nameof(EmptyInventoryCanRestAndContinueWithoutDeadline), EmptyInventoryCanRestAndContinueWithoutDeadline),
            (nameof(EarningsBuyOnePermanentUpgradeWithoutReducingGrossRevenue), EarningsBuyOnePermanentUpgradeWithoutReducingGrossRevenue),
            (nameof(ResetClearsMarketAndAllowsOneFreshStart), ResetClearsMarketAndAllowsOneFreshStart),
            (nameof(InvalidInputsLeaveTimeStockAndOrdersUnchanged), InvalidInputsLeaveTimeStockAndOrdersUnchanged),
            (nameof(InvalidMenusAreRejectedAtomically), InvalidMenusAreRejectedAtomically),
            (nameof(MixedMenuOnlyCreatesOrdersThatStockCanFulfil), MixedMenuOnlyCreatesOrdersThatStockCanFulfil),
            (nameof(EnvironmentChangesMatureFruitButNotHarvestedInventory), EnvironmentChangesMatureFruitButNotHarvestedInventory),
            (nameof(SunnyFruitCanBeCookedAndPaidBeforeNextDay), SunnyFruitCanBeCookedAndPaidBeforeNextDay),
            (nameof(ForageCooldownPreventsDuplicateHarvests), ForageCooldownPreventsDuplicateHarvests),
            (nameof(HarvestRejectedOutsideExplorationDoesNotConsumePoint), HarvestRejectedOutsideExplorationDoesNotConsumePoint),
            (nameof(InvalidConfigurationIsRejected), InvalidConfigurationIsRejected)
        };

        public static void StartAndRepeatedCommandsPreserveSession()
        {
            var run = new MarketSession();
            Rejected(run.TryHarvest(IngredientId.Mushroom), "locked harvest");
            Rejected(run.TryChooseEvening(false), "locked dusk choice");
            Rejected(run.TryCook(0), "locked cooking");
            Rejected(run.TryServe(0), "locked serving");
            Rejected(run.TryNextDay(), "locked next day");
            Ok(run.TryStart());
            Need(run.Day == 1 && run.Phase == MarketPhase.Day, "start enters first day");
            Ok(run.TryHarvest(IngredientId.Mushroom, 3));
            run.Tick(10f);
            Rejected(run.TryStart(), "repeat start");
            Need(run.Stock(IngredientId.Mushroom) == 3 && run.Day == 1, "repeat start preserves inventory and day");
            Near(run.RemainingSeconds, 170f, "repeat start preserves elapsed time");
        }

        public static void OrdersAppearOnlyAfterMenuAndReserveWithoutOverbooking()
        {
            var run = Started();
            Ok(run.TryHarvest(IngredientId.Mushroom, 2));
            Need(run.GetOrder(0) == null && run.GetOrder(1) == null, "no customers during exploration");
            ToMenu(run);
            Need(run.GetOrder(0) == null && run.GetOrder(1) == null, "no customers before opening menu");
            Ok(run.TryOpenBusiness(new[] { "mushroom" }));
            Need(run.GetOrder(0) != null && run.GetOrder(1) == null, "stock for one meal cannot produce two orders");
            Need(run.Stock(IngredientId.Mushroom) == 2 && run.Reserved(IngredientId.Mushroom) == 2,
                "reservation does not prematurely consume stock");
            Rejected(run.TryCook(1), "empty window cooking");
            Ok(run.TryCook(0));
            Need(run.Stock(IngredientId.Mushroom) == 0 && run.Reserved(IngredientId.Mushroom) == 0,
                "cooking consumes reserved ingredients once");
            Rejected(run.TryCook(0), "repeat load");
            Need(run.Stock(IngredientId.Mushroom) == 0, "repeat load does not debit twice");
        }

        public static void TwoBurnersAdvanceTogetherAndOnlyPaidWindowRefills()
        {
            var run = Started();
            Ok(run.TryHarvest(IngredientId.Mushroom, 10));
            Ok(run.TryHarvest(IngredientId.RainFruit, 2));
            ToMenu(run);
            Ok(run.TryOpenBusiness(new[] { "mushroom", "rain" }));
            var first = run.GetOrder(0);
            var second = run.GetOrder(1);
            Need(first != null && second != null, "two affordable customers are present");
            Ok(run.TryCook(0));
            Ok(run.TryCook(1));
            run.Tick(3f);
            Need(run.GetOrder(0).Stage == OrderStage.Ready, "three-second dish is ready");
            Need(run.GetOrder(1).Stage == OrderStage.Cooking, "four-second dish remains cooking");
            Near(run.GetOrder(1).RemainingSeconds, 1f, "second burner advanced concurrently");
            Rejected(run.TryServe(1), "serve unfinished dish");
            Need(run.Cash == 0, "unfinished serving gives no money");
            Ok(run.TryServe(0));
            Need(run.Cash == 12 && run.TotalRevenue == 12 && run.TonightRevenue == 12,
                "one payment updates all revenue views");
            Need(run.GetOrder(0).Id != first.Id && run.GetOrder(0).Stage == OrderStage.Waiting,
                "only paid window receives a new waiting customer");
            Need(run.GetOrder(1).Id == second.Id, "other customer retains identity");
            Near(run.GetOrder(1).RemainingSeconds, 1f, "refill does not reset other cooking clock");
            Rejected(run.TryServe(0), "repeat receipt on refilled window");
            Need(run.Cash == 12, "payment cannot be duplicated");
            run.Tick(1f);
            Ok(run.TryServe(1));
            Need(run.Cash == 34 && run.TotalRevenue == 34, "different dishes pay their own prices");
        }

        public static void ClosingReturnsCookingStockAndReleasesWaitingReservations()
        {
            var run = MushroomBusiness(6);
            Ok(run.TryCook(0));
            run.Tick(1f);
            Need(run.Stock(IngredientId.Mushroom) == 4 && run.Reserved(IngredientId.Mushroom) == 2,
                "one cooking and one waiting order share one inventory");
            Ok(run.TryClose());
            Need(run.Phase == MarketPhase.Summary, "manual closing opens summary");
            Need(run.Stock(IngredientId.Mushroom) == 6 && run.Reserved(IngredientId.Mushroom) == 0,
                "only spent ingredients are returned; waiting reservation is released");
            Need(run.GetOrder(0) == null && run.GetOrder(1) == null, "closing clears customer windows");
            Rejected(run.TryClose(), "repeat close");
            Need(run.Stock(IngredientId.Mushroom) == 6, "repeat closing cannot duplicate refunds");
            Ok(run.TryNextDay());
            Need(run.Day == 2 && run.Stock(IngredientId.Mushroom) == 6, "next day retains refunded inventory");
        }

        public static void ClosingReturnsReadyMealsWithoutPaying()
        {
            var run = MushroomBusiness(4);
            Ok(run.TryCook(0));
            Ok(run.TryCook(1));
            run.Tick(3f);
            Need(run.GetOrder(0).Stage == OrderStage.Ready && run.GetOrder(1).Stage == OrderStage.Ready,
                "both meals ready before closing");
            Ok(run.TryClose());
            Need(run.Stock(IngredientId.Mushroom) == 4, "ready but unserved meals return ingredients");
            Need(run.Cash == 0 && run.TotalRevenue == 0, "closing does not automatically sell meals");
            Rejected(run.TryServe(0), "serving after close");
        }

        public static void BusinessTimeoutStopsAtBoundaryAndRefunds()
        {
            var run = Started(new MarketSettings(daySeconds: 1f, nightSeconds: 1f,
                businessSeconds: 2f, shortBusinessSeconds: 1f));
            Ok(run.TryHarvest(IngredientId.Mushroom, 4));
            ToMenu(run);
            Ok(run.TryOpenBusiness(new[] { "mushroom" }));
            Ok(run.TryCook(0));
            Ok(run.TryCook(1));
            run.Tick(100f);
            Need(run.Phase == MarketPhase.Summary && run.Day == 1,
                "oversized frame closes once without skipping confirmation or days");
            Need(run.Stock(IngredientId.Mushroom) == 4 && run.Cash == 0,
                "timeout refunds both unfinished meals");
            run.Tick(100f);
            Need(run.Phase == MarketPhase.Summary && run.Day == 1, "summary waits for explicit next day");
        }

        public static void DirectEveningUsesExplicitReturnAndFullBusinessTime()
        {
            var run = Started();
            Ok(run.TryHarvest(IngredientId.Mushroom, 2));
            run.Tick(179f);
            Need(run.Phase == MarketPhase.Day, "day remains active before boundary");
            Near(run.RemainingSeconds, 1f, "day has one second remaining");
            run.Tick(1f);
            Need(run.Phase == MarketPhase.Dusk, "180 seconds reaches dusk");
            run.Tick(500f);
            Need(run.Phase == MarketPhase.Dusk, "dusk decision is untimed");
            Ok(run.TryChooseEvening(false));
            Need(run.Phase == MarketPhase.Returning && run.IsNight, "direct choice enters night returning");
            Rejected(run.TryHarvest(IngredientId.Mushroom), "returning harvest");
            Rejected(run.TryOpenBusiness(new[] { "mushroom" }), "open before arrival");
            run.Tick(500f);
            Need(run.Phase == MarketPhase.Returning, "return journey has no business countdown");
            Ok(run.TryArriveAtShop());
            run.Tick(500f);
            Need(run.Phase == MarketPhase.Menu && run.GetOrder(0) == null, "menu waits without generating orders");
            Ok(run.TryOpenBusiness(new[] { "mushroom" }));
            Near(run.RemainingSeconds, 120f, "full business time starts only after menu confirmation");
            run.Tick(120f);
            Need(run.Phase == MarketPhase.Summary, "full evening reaches summary");
        }

        public static void NightForageUsesHalfDayAndHalfBusinessTime()
        {
            var run = Started();
            Ok(run.TryHarvest(IngredientId.Mushroom, 2));
            run.Tick(180f);
            Ok(run.TryChooseEvening(true));
            Need(run.Phase == MarketPhase.NightForage && run.IsNight && run.CanHarvest, "night exploration permits gathering");
            Near(run.RemainingSeconds, 90f, "night exploration begins at ninety seconds");
            run.Tick(89f);
            Need(run.Phase == MarketPhase.NightForage, "night remains active until its boundary");
            run.Tick(1f);
            Need(run.Phase == MarketPhase.Returning && !run.CanHarvest, "night timeout stops harvesting, permits return");
            run.Tick(1000f);
            Need(run.Phase == MarketPhase.Returning, "long safe return cannot skip into service");
            Ok(run.TryArriveAtShop());
            Ok(run.TryOpenBusiness(new[] { "mushroom" }));
            Near(run.RemainingSeconds, 60f, "night route receives half business shift");
        }

        public static void EarlyNightReturnDoesNotRestoreFullBusinessTime()
        {
            var run = Started();
            Ok(run.TryHarvest(IngredientId.Mushroom, 2));
            run.Tick(180f);
            Ok(run.TryChooseEvening(true));
            run.Tick(10f);
            Ok(run.TryReturnToShop());
            Rejected(run.TryReturnToShop(), "repeated return");
            Ok(run.TryArriveAtShop());
            Ok(run.TryOpenBusiness(new[] { "mushroom" }));
            Near(run.RemainingSeconds, 60f, "early return retains selected short shift");
        }

        public static void EmptyInventoryCanRestAndContinueWithoutDeadline()
        {
            var run = Started();
            for (int day = 1; day <= 31; day++)
            {
                ToMenu(run);
                Rejected(run.TryOpenBusiness(new[] { "mushroom" }), "open with empty stock");
                Need(run.Phase == MarketPhase.Menu, "failed opening leaves rest available");
                Ok(run.TryNextDay());
                Need(run.Day == day + 1 && run.Phase == MarketPhase.Day, "empty-stock run can continue next day");
            }
            Need(run.Cash == 0 && run.TotalRevenue == 0, "resting cannot create income");
        }

        public static void EarningsBuyOnePermanentUpgradeWithoutReducingGrossRevenue()
        {
            var run = EarnUpgradeAndReachMenu();
            Need(run.Cash == 36 && run.TotalRevenue == 36, "three actual payments fund upgrade");
            Ok(run.TryUpgradeKitchen());
            Need(run.Cash == 0 && run.TotalRevenue == 36 && run.KitchenUpgraded,
                "upgrade spends available cash but retains earned revenue");
            Rejected(run.TryUpgradeKitchen(), "repeat upgrade");
            Need(run.Cash == 0, "repeat upgrade does not debit again");
            Ok(run.TryOpenBusiness(new[] { "mushroom" }));
            Ok(run.TryCook(0));
            Near(run.GetOrder(0).RemainingSeconds, 2.1f, "upgrade reduces three seconds by thirty percent");
            run.Tick(2f);
            Rejected(run.TryServe(0), "upgraded meal still requires full duration");
            run.Tick(.11f);
            Ok(run.TryServe(0));
            Need(run.Cash == 12 && run.TotalRevenue == 48, "earning resumes after spending");
            Ok(run.TryClose());
            Need(run.LastNightRevenue == 12, "summary records actual new-night sales");
            Ok(run.TryNextDay());
            Need(run.KitchenUpgraded && run.Cash == 12 && run.TotalRevenue == 48, "upgrade and money persist next day");
        }

        public static void ResetClearsMarketAndAllowsOneFreshStart()
        {
            var run = EarnUpgradeAndReachMenu();
            Ok(run.TryUpgradeKitchen());
            Ok(run.TryOpenBusiness(new[] { "mushroom" }));
            Ok(run.TryCook(0));
            run.Reset();
            Need(run.Phase == MarketPhase.Locked && run.Day == 0 && !run.IsNight, "reset returns to locked daylight state");
            Need(run.Cash == 0 && run.TotalRevenue == 0 && run.TonightRevenue == 0 && run.LastNightRevenue == 0,
                "reset clears all accounting");
            Need(!run.KitchenUpgraded && run.Stock(IngredientId.Mushroom) == 0 && run.Reserved(IngredientId.Mushroom) == 0,
                "reset clears inventory, reservations and upgrade");
            Need(run.GetOrder(0) == null && run.GetOrder(1) == null, "reset removes in-flight orders");
            Ok(run.TryStart());
            Need(run.Day == 1, "after reset a new run begins on day one");
        }

        public static void InvalidInputsLeaveTimeStockAndOrdersUnchanged()
        {
            var run = Started();
            Ok(run.TryHarvest(IngredientId.Mushroom, 4));
            Rejected(run.TryHarvest((IngredientId)12345), "invalid ingredient");
            Rejected(run.TryHarvest(IngredientId.Mushroom, 0), "zero harvest");
            Rejected(run.TryHarvest(IngredientId.Mushroom, -1), "negative harvest");
            foreach (float delta in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1f, 0f })
                run.Tick(delta);
            Need(run.Stock(IngredientId.Mushroom) == 4, "invalid harvest does not change stock");
            Near(run.RemainingSeconds, 180f, "invalid time cannot advance exploration");
            ToMenu(run);
            Ok(run.TryOpenBusiness(new[] { "mushroom" }));
            Rejected(run.TryCook(-1), "negative slot");
            Rejected(run.TryServe(2), "out-of-range slot");
            Rejected(run.TryUpgradeKitchen(), "upgrade outside menu");
            Need(run.Stock(IngredientId.Mushroom) == 4 && run.Cash == 0, "invalid commands cannot debit or pay");
            Ok(run.TryCook(0));
            foreach (float delta in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1f })
                run.Tick(delta);
            Near(run.GetOrder(0).RemainingSeconds, 3f, "invalid time cannot finish a meal");
            Near(run.RemainingSeconds, 120f, "invalid time cannot finish business");
        }

        public static void InvalidMenusAreRejectedAtomically()
        {
            var run = Started();
            Ok(run.TryHarvest(IngredientId.Mushroom, 4));
            ToMenu(run);
            Rejected(run.TryOpenBusiness(new[] { "mushroom", "not-a-recipe" }), "menu containing unknown recipe");
            Rejected(run.TryOpenBusiness(new[] { "mushroom", "mushroom" }), "menu containing duplicate recipe");
            Rejected(run.TryOpenBusiness(Array.Empty<string>()), "empty menu");
            Rejected(run.TryOpenBusiness(null), "missing menu");
            Need(run.Phase == MarketPhase.Menu && run.Stock(IngredientId.Mushroom) == 4,
                "invalid menu leaves phase and stock unchanged");
            Need(run.Reserved(IngredientId.Mushroom) == 0 && run.GetOrder(0) == null && run.GetOrder(1) == null,
                "invalid menu creates no partial reservation or customer");
            Ok(run.TryOpenBusiness(new[] { "mushroom" }));
        }

        public static void MixedMenuOnlyCreatesOrdersThatStockCanFulfil()
        {
            var run = Started();
            Ok(run.TryHarvest(IngredientId.Mushroom, 4));
            ToMenu(run);
            Rejected(run.TryOpenBusiness(new[] { "rain", "sun" }), "menu with no affordable dish");
            Need(run.Phase == MarketPhase.Menu && run.Stock(IngredientId.Mushroom) == 4,
                "unaffordable menu preserves phase and raw stock");
            Need(run.Reserved(IngredientId.Mushroom) == 0 && run.GetOrder(0) == null && run.GetOrder(1) == null,
                "unaffordable menu has no partial customers or reservations");
            Ok(run.TryOpenBusiness(new[] { "rain", "mushroom", "sun" }));
            Need(run.GetOrder(0).Recipe.Id == "mushroom" && run.GetOrder(1).Recipe.Id == "mushroom",
                "unavailable dishes may stay on the menu, but customers order only affordable dishes");
            Need(run.Reserved(IngredientId.Mushroom) == 4 && run.Reserved(IngredientId.RainFruit) == 0 &&
                run.Reserved(IngredientId.SunFruit) == 0, "only affordable ingredients are reserved");
        }

        public static void EnvironmentChangesMatureFruitButNotHarvestedInventory()
        {
            var run = Started();
            var point = new ForageState(ForageKind.WeatherTree, regrowSeconds: 8f);
            point.Tick(.1f, new ForageEnvironment(false, false, true));
            Need(point.CurrentIngredient == IngredientId.SunFruit, "real daytime light enables sun fruit");
            Ok(run.TryHarvest(point));
            Need(run.Stock(IngredientId.SunFruit) == 1, "harvest records concrete ingredient kind");
            point.Tick(8f, new ForageEnvironment(false, true, true));
            Need(point.CurrentIngredient == IngredientId.RainFruit, "rain has priority over daytime light");
            point.Tick(.1f, new ForageEnvironment(false, false, false));
            Need(point.IsMature && point.CurrentIngredient == null, "unlit dry mature tree waits without old fruit");
            point.Tick(.1f, new ForageEnvironment(true, false, true));
            Need(point.CurrentIngredient == IngredientId.MoonFruit, "night ignores a stale sunlight input");
            point.Tick(.1f, new ForageEnvironment(true, true, true));
            Need(point.CurrentIngredient == IngredientId.StormFruit, "rainy night gives its own fruit");
            Need(run.Stock(IngredientId.SunFruit) == 1 && run.Stock(IngredientId.StormFruit) == 0,
                "environment changes do not transmute previously harvested stock");
        }

        public static void SunnyFruitCanBeCookedAndPaidBeforeNextDay()
        {
            var run = Started();
            var point = new ForageState(ForageKind.WeatherTree);
            point.Tick(0f, new ForageEnvironment(false, false, true));
            Ok(run.TryHarvest(point));
            Ok(run.TryHarvest(IngredientId.Mushroom));
            Need(run.Stock(IngredientId.SunFruit) == 1, "sunlit tree provides a usable daytime ingredient");
            ToMenu(run);
            Need(run.CanMake("sun"), "daytime fruit has an affordable menu recipe");
            Ok(run.TryOpenBusiness(new[] { "sun" }));
            Need(run.GetOrder(0).Recipe.Id == "sun" && run.GetOrder(1) == null,
                "one sunny-fruit serving creates exactly one affordable order");
            Ok(run.TryCook(0));
            Need(run.Stock(IngredientId.SunFruit) == 0 && run.Stock(IngredientId.Mushroom) == 0,
                "sunny-fruit recipe consumes one fruit and one mushroom");
            run.Tick(4f);
            Need(run.GetOrder(0).Stage == OrderStage.Ready, "sunny-fruit dish finishes after four seconds");
            Ok(run.TryServe(0));
            Need(run.Cash == 20 && run.TotalRevenue == 20, "sunny-fruit dish converts daylight gathering into income");
            Ok(run.TryClose());
            Ok(run.TryNextDay());
            Need(run.Cash == 20 && run.Day == 2, "paid daytime gathering remains valuable on the next day");
            Need(run.Stock(IngredientId.SunFruit) == 0, "paid dish ingredients cannot be refunded at closing");
        }

        public static void ForageCooldownPreventsDuplicateHarvests()
        {
            var run = Started();
            var point = new ForageState(ForageKind.MushroomPatch, regrowSeconds: 8f, initialSeconds: 2f);
            var environment = new ForageEnvironment(false, false, false);
            point.Tick(1f, environment);
            Need(!point.IsMature, "initial maturity delay is respected");
            Rejected(run.TryHarvest(point), "premature harvest");
            foreach (float delta in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1f })
                point.Tick(delta, environment);
            Near(point.RemainingSeconds, 1f, "invalid time leaves point maturity unchanged");
            point.Tick(1f, environment);
            Ok(run.TryHarvest(point));
            Need(run.Stock(IngredientId.Mushroom) == 1 && !point.IsMature, "one mature point yields one ingredient");
            Rejected(run.TryHarvest(point), "same point harvested twice");
            Near(point.RemainingSeconds, 8f, "harvest starts regrowth once");
            point.Tick(8f, environment);
            Ok(run.TryHarvest(point));
            Need(run.Stock(IngredientId.Mushroom) == 2, "regrown point can be harvested again");
        }

        public static void HarvestRejectedOutsideExplorationDoesNotConsumePoint()
        {
            var run = Started();
            var point = new ForageState(ForageKind.MushroomPatch);
            point.Tick(.1f, new ForageEnvironment(false, false, false));
            run.Tick(180f);
            Rejected(run.TryHarvest(point), "harvest during dusk");
            Need(point.IsMature && point.CurrentIngredient == IngredientId.Mushroom,
                "failed phase check does not consume scene point");
            Need(run.Stock(IngredientId.Mushroom) == 0, "failed phase check does not add inventory");
            Ok(run.TryChooseEvening(true));
            Ok(run.TryHarvest(point));
            Need(run.Stock(IngredientId.Mushroom) == 1, "same point remains usable after valid phase resumes");
        }

        public static void InvalidConfigurationIsRejected()
        {
            ThrowsArgument(() => new MarketSettings(daySeconds: 0f), "zero day duration");
            ThrowsArgument(() => new MarketSettings(nightSeconds: float.NaN), "NaN night duration");
            ThrowsArgument(() => new MarketSettings(businessSeconds: float.PositiveInfinity), "infinite business duration");
            ThrowsArgument(() => new MarketSettings(shortBusinessSeconds: -1f), "negative short duration");
            ThrowsArgument(() => new MarketSettings(upgradeCost: -1), "negative upgrade cost");
            ThrowsArgument(() => new MarketSettings(upgradeTimeMultiplier: float.NaN), "NaN upgrade multiplier");
        }

        private static MarketSession EarnUpgradeAndReachMenu()
        {
            var run = MushroomBusiness(6);
            Ok(run.TryCook(0));
            Ok(run.TryCook(1));
            run.Tick(3f);
            Ok(run.TryServe(0));
            Ok(run.TryServe(1));
            Ok(run.TryCook(0));
            run.Tick(3f);
            Ok(run.TryServe(0));
            Ok(run.TryClose());
            Ok(run.TryNextDay());
            Ok(run.TryHarvest(IngredientId.Mushroom, 4));
            ToMenu(run);
            return run;
        }

        private static MarketSession MushroomBusiness(int mushrooms)
        {
            var run = Started();
            Ok(run.TryHarvest(IngredientId.Mushroom, mushrooms));
            ToMenu(run);
            Ok(run.TryOpenBusiness(new[] { "mushroom" }));
            return run;
        }

        private static MarketSession Started(MarketSettings settings = null)
        {
            var run = new MarketSession(settings);
            Ok(run.TryStart());
            return run;
        }

        private static void ToMenu(MarketSession run)
        {
            Need(run.Phase == MarketPhase.Day, "fixture starts on an active day");
            run.Tick(run.RemainingSeconds);
            Need(run.Phase == MarketPhase.Dusk, "day ends at dusk");
            Ok(run.TryChooseEvening(false));
            Need(run.Phase == MarketPhase.Returning, "direct evening requires physical return");
            Ok(run.TryArriveAtShop());
            Need(run.Phase == MarketPhase.Menu, "arrival unlocks menu");
        }

        private static void Ok(MarketActionResult result) =>
            Need(result == MarketActionResult.Success, "command succeeds, actual: " + result);

        private static void Rejected(MarketActionResult result, string reason) =>
            Need(result != MarketActionResult.Success, reason + " is rejected");

        private static void Near(float actual, float expected, string reason) =>
            Need(!float.IsNaN(actual) && Math.Abs(actual - expected) < .0001f,
                reason + ": expected " + expected + ", actual " + actual);

        private static void Need(bool condition, string reason)
        {
            AssertionCount++;
            if (!condition) throw new InvalidOperationException(reason);
        }

        private static void ThrowsArgument(Action action, string reason)
        {
            try { action(); }
            catch (ArgumentException) { Need(true, reason); return; }
            Need(false, reason + " must reject invalid settings");
        }
    }
}
