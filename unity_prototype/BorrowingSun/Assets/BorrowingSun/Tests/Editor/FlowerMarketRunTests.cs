using System;
using NUnit.Framework;

namespace BorrowingSun.Tests
{
    public sealed class FlowerMarketRunTests
    {
        [Test]
        public void CentralMarketMovesStraightIntoNightAndCanServeBothStoves()
        {
            var run = new FlowerMarketRun(dayGatherSeconds: 180f, nightGatherSeconds: 45f,
                nightTradingSeconds: 60f, skipDuskChoice: true);

            Assert.IsTrue(run.TryStart());
            run.Tick(179f);
            Assert.That(run.RemainingGatherSeconds, Is.EqualTo(1f));
            run.Tick(1f);
            Assert.That(run.Phase, Is.EqualTo(FlowerMarketPhase.NightExploration));
            Assert.That(run.RemainingGatherSeconds, Is.EqualTo(45f));
            Assert.That(run.GetOrchardProduce(false, false), Is.Null);
            Assert.That(run.GetOrchardProduce(true, false), Is.EqualTo(FlowerIngredient.RainNightFruit));
            Assert.IsTrue(run.TryHarvest(FlowerIngredient.RainNightFruit));
            Assert.IsTrue(run.TryReturnToShop());
            Assert.IsTrue(run.TryOpenShop());
            Assert.IsTrue(run.TryAcceptOrders());
            Assert.That(run.RemainingTradingSeconds, Is.EqualTo(60f));
            Assert.IsTrue(run.TryCook(0));
            Assert.IsTrue(run.TryCook(1));
            run.Tick(4f);
            Assert.IsTrue(run.TryServe(0));
            Assert.IsTrue(run.TryServe(1));
            Assert.That(run.EveningRevenue, Is.EqualTo(34));
            run.Tick(56f);
            Assert.That(run.Phase, Is.EqualTo(FlowerMarketPhase.DaySummary));
            Assert.IsTrue(run.TryStartNextDay());
            Assert.That(run.RemainingGatherSeconds, Is.EqualTo(180f));
            Assert.That(run.GetStock(FlowerIngredient.Mushroom), Is.EqualTo(1));
        }

        [Test]
        public void CommandsRequireStartAndRepeatedStartKeepsStock()
        {
            var run = new FlowerMarketRun();

            Assert.IsFalse(run.TryHarvest(FlowerIngredient.Mushroom));
            Assert.IsFalse(run.TryAcceptOrders());
            Assert.IsFalse(run.TryCook(0));
            Assert.IsFalse(run.TryServe(0));
            Assert.IsFalse(run.TryUpgradeKitchen());
            Assert.IsFalse(run.TryEndDay());
            Assert.IsFalse(run.TryChooseDusk(true));
            Assert.IsFalse(run.TryReturnToShop());
            Assert.IsFalse(run.TryOpenShop());
            Assert.IsFalse(run.TryCloseShop());
            Assert.IsFalse(run.TryStartNextDay());
            Assert.IsTrue(run.TryStart());
            Assert.That(run.Day, Is.EqualTo(1));
            Assert.That(run.Phase, Is.EqualTo(FlowerMarketPhase.DayGathering));
            Assert.IsTrue(run.TryHarvest(FlowerIngredient.Mushroom));
            Assert.IsFalse(run.TryStart());
            Assert.That(run.GetStock(FlowerIngredient.Mushroom), Is.EqualTo(1));
        }

        [Test]
        public void GatheringTimersStopAtChoiceAndAtReturnWithoutOpeningTheShop()
        {
            var run = new FlowerMarketRun(dayGatherSeconds: 2f, nightGatherSeconds: 3f);
            run.TryStart();
            Assert.That(run.RemainingGatherSeconds, Is.EqualTo(2f));
            run.Tick(1.5f);
            Assert.That(run.Phase, Is.EqualTo(FlowerMarketPhase.DayGathering));
            Assert.That(run.RemainingGatherSeconds, Is.EqualTo(.5f));
            run.Tick(.5f);
            run.Tick(10f);
            Assert.That(run.Phase, Is.EqualTo(FlowerMarketPhase.DuskChoice));
            Assert.That(run.RemainingGatherSeconds, Is.Zero);
            Assert.IsFalse(run.IsNight);
            Assert.IsFalse(run.TryOpenShop());

            Assert.IsTrue(run.TryChooseDusk(true));
            Assert.That(run.Phase, Is.EqualTo(FlowerMarketPhase.NightExploration));
            Assert.That(run.RemainingGatherSeconds, Is.EqualTo(3f));
            Assert.IsTrue(run.IsNight);
            Assert.IsFalse(run.TryOpenShop());
            run.Tick(4f);
            Assert.That(run.Phase, Is.EqualTo(FlowerMarketPhase.ReturningToShop));
            Assert.That(run.RemainingGatherSeconds, Is.Zero);
            Assert.IsTrue(run.IsNight);
            Assert.IsFalse(run.TryHarvest(FlowerIngredient.Mushroom));
            Assert.IsTrue(run.TryOpenShop());
            Assert.That(run.Phase, Is.EqualTo(FlowerMarketPhase.Trading));
        }

        [Test]
        public void OrchardDistinguishesDaylightDryDarkRainAndRainAtNight()
        {
            var run = new FlowerMarketRun();
            Assert.That(run.GetOrchardProduce(raining: true, directlySunlit: true), Is.Null);
            run.TryStart();
            Assert.That(run.GetOrchardProduce(raining: false, directlySunlit: false), Is.Null);
            Assert.That(run.GetOrchardProduce(raining: false, directlySunlit: true), Is.EqualTo(FlowerIngredient.SunnyFruit));
            Assert.That(run.GetOrchardProduce(raining: true, directlySunlit: true), Is.EqualTo(FlowerIngredient.WeatherFruit));
            Assert.IsFalse(run.TryHarvest(FlowerIngredient.RainNightFruit));

            Assert.IsTrue(run.TryEndDay());
            Assert.That(run.GetOrchardProduce(raining: true, directlySunlit: true), Is.Null);
            Assert.IsTrue(run.TryChooseDusk(true));
            Assert.That(run.GetOrchardProduce(raining: false, directlySunlit: true), Is.Null);
            Assert.That(run.GetOrchardProduce(raining: true, directlySunlit: false), Is.EqualTo(FlowerIngredient.RainNightFruit));
            Assert.IsFalse(run.TryHarvest(FlowerIngredient.SunnyFruit));
            Assert.IsFalse(run.TryHarvest(FlowerIngredient.WeatherFruit));
            Assert.IsTrue(run.TryHarvest(FlowerIngredient.RainNightFruit));
            Assert.IsTrue(run.TryReturnToShop());
            Assert.That(run.GetOrchardProduce(raining: true, directlySunlit: true), Is.Null);
            Assert.That(run.RemainingGatherSeconds, Is.Zero);
        }

        [Test]
        public void MissingIngredientDoesNotDebitAvailableStock()
        {
            var run = new FlowerMarketRun();
            run.TryStart();
            run.TryHarvest(FlowerIngredient.Mushroom);
            OpenShopDirectly(run);

            Assert.IsFalse(run.TryAcceptOrders());
            Assert.IsFalse(run.OrdersAccepted);
            Assert.IsFalse(run.TryCook(0));
            Assert.That(run.GetStock(FlowerIngredient.Mushroom), Is.EqualTo(1));
            Assert.That(run.GetStove(0).Stage, Is.EqualTo(FlowerOrderStage.Idle));
        }

        [TestCase(FlowerRecipe.MushroomBento, 2, FlowerIngredient.WeatherFruit, 0, 3f, 12)]
        [TestCase(FlowerRecipe.FruitBento, 1, FlowerIngredient.WeatherFruit, 1, 4f, 22)]
        [TestCase(FlowerRecipe.FruitBento, 1, FlowerIngredient.SunnyFruit, 1, 4f, 22)]
        [TestCase(FlowerRecipe.FruitBento, 1, FlowerIngredient.RainNightFruit, 1, 4f, 22)]
        public void EachRecipeConsumesItsIngredientsAndPaysExactlyOnce(
            FlowerRecipe recipe, int mushrooms, FlowerIngredient fruit, int fruitCount, float seconds, int price)
        {
            var run = new FlowerMarketRun();
            run.TryStart();
            for (var i = 0; i < mushrooms; i++) run.TryHarvest(FlowerIngredient.Mushroom);
            if (fruit == FlowerIngredient.RainNightFruit)
            {
                Assert.IsTrue(run.TryEndDay());
                Assert.IsTrue(run.TryChooseDusk(true));
                for (var i = 0; i < fruitCount; i++) Assert.IsTrue(run.TryHarvest(fruit));
                Assert.IsTrue(run.TryReturnToShop());
                Assert.IsTrue(run.TryOpenShop());
            }
            else
            {
                for (var i = 0; i < fruitCount; i++) Assert.IsTrue(run.TryHarvest(fruit));
                Assert.IsFalse(run.TryAcceptOrders());
                OpenShopDirectly(run);
            }

            Assert.IsFalse(run.TryCook(0));
            Assert.IsTrue(run.TryAcceptOrders());
            Assert.That(run.GetStove(0).Stage, Is.EqualTo(FlowerOrderStage.Waiting));
            Assert.That(run.GetStove(0).Recipe, Is.EqualTo(recipe));
            Assert.That(run.GetStock(FlowerIngredient.Mushroom), Is.EqualTo(mushrooms));
            Assert.That(run.GetReserved(FlowerIngredient.Mushroom), Is.EqualTo(mushrooms));
            Assert.That(run.GetStock(fruit), Is.EqualTo(fruitCount));
            Assert.That(run.GetReserved(fruit), Is.EqualTo(fruitCount));
            Assert.IsTrue(run.TryCook(0));
            Assert.That(run.GetStock(FlowerIngredient.Mushroom), Is.Zero);
            Assert.That(run.GetStock(fruit), Is.Zero);
            Assert.That(run.GetReserved(FlowerIngredient.Mushroom), Is.Zero);
            Assert.That(run.GetStove(0).Stage, Is.EqualTo(FlowerOrderStage.Cooking));
            Assert.That(run.GetStove(0).RemainingSeconds, Is.EqualTo(seconds));
            Assert.IsFalse(run.TryCook(0));

            run.Tick(seconds - .5f);
            Assert.IsFalse(run.TryServe(0));
            Assert.That(run.Cash, Is.Zero);
            run.Tick(.5f);
            Assert.That(run.GetStove(0).Stage, Is.EqualTo(FlowerOrderStage.Ready));
            Assert.IsTrue(run.TryServe(0));
            Assert.IsFalse(run.TryServe(0));
            Assert.That(run.Cash, Is.EqualTo(price));
            Assert.That(run.TotalRevenue, Is.EqualTo(price));
        }

        [Test]
        public void NextDayKeepsStockAndCashAndResetClearsRun()
        {
            var run = new FlowerMarketRun();
            run.TryStart();
            run.TryHarvest(FlowerIngredient.Mushroom);
            run.TryHarvest(FlowerIngredient.Mushroom);
            run.TryHarvest(FlowerIngredient.Mushroom);
            run.TryHarvest(FlowerIngredient.WeatherFruit);
            OpenShopDirectly(run);
            Assert.IsTrue(run.TryAcceptOrders());
            Assert.IsTrue(run.TryCook(0));
            run.Tick(3f);
            Assert.IsTrue(run.TryServe(0));

            Assert.IsTrue(run.TryCloseShop());
            Assert.That(run.Day, Is.EqualTo(1));
            Assert.That(run.Phase, Is.EqualTo(FlowerMarketPhase.DaySummary));
            Assert.That(run.EveningRevenue, Is.EqualTo(12));
            Assert.IsFalse(run.ClosedByTimeLimit);
            Assert.IsTrue(run.IsNight);
            Assert.IsTrue(run.TryStartNextDay());
            Assert.IsFalse(run.TryStartNextDay());
            Assert.That(run.Day, Is.EqualTo(2));
            Assert.That(run.Phase, Is.EqualTo(FlowerMarketPhase.DayGathering));
            Assert.That(run.RemainingGatherSeconds, Is.EqualTo(75f));
            Assert.IsFalse(run.IsNight);
            Assert.That(run.Cash, Is.EqualTo(12));
            Assert.That(run.EveningRevenue, Is.Zero);
            Assert.That(run.GetStock(FlowerIngredient.Mushroom), Is.EqualTo(1));
            Assert.That(run.GetStock(FlowerIngredient.WeatherFruit), Is.EqualTo(1));
            Assert.IsFalse(run.OrdersAccepted);
            Assert.IsFalse(run.HasPendingOrders);
            Assert.That(run.GetStove(0).Stage, Is.EqualTo(FlowerOrderStage.Idle));
            Assert.That(run.GetStove(1).Recipe, Is.Null);
            OpenShopDirectly(run);
            Assert.IsTrue(run.TryAcceptOrders());
            Assert.That(run.GetStove(0).Recipe, Is.EqualTo(FlowerRecipe.FruitBento));
            Assert.IsTrue(run.TryCook(0));

            run.Reset();
            Assert.IsFalse(run.IsStarted);
            Assert.That(run.Phase, Is.EqualTo(FlowerMarketPhase.Locked));
            Assert.That(run.RemainingGatherSeconds, Is.Zero);
            Assert.That(run.Day, Is.Zero);
            Assert.That(run.Cash, Is.Zero);
            Assert.That(run.TotalRevenue, Is.Zero);
            Assert.That(run.GetStock(FlowerIngredient.Mushroom), Is.Zero);
            Assert.That(run.GetStock(FlowerIngredient.WeatherFruit), Is.Zero);
            Assert.That(run.GetStove(0).Stage, Is.EqualTo(FlowerOrderStage.Idle));
            Assert.That(run.GetStove(0).Recipe, Is.Null);
            Assert.That(run.GetStove(0).RemainingSeconds, Is.Zero);
            Assert.IsFalse(run.OrdersAccepted);
            Assert.IsTrue(run.TryStart());
            Assert.That(run.Day, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CloseRefundsExactReservedFruitOnlyOnce(bool cooked)
        {
            var run = new FlowerMarketRun();
            run.TryStart();
            run.TryHarvest(FlowerIngredient.Mushroom);
            run.TryHarvest(FlowerIngredient.WeatherFruit);
            run.TryHarvest(FlowerIngredient.SunnyFruit);
            run.TryEndDay();
            run.TryChooseDusk(true);
            run.TryHarvest(FlowerIngredient.RainNightFruit);
            run.TryReturnToShop();
            run.TryOpenShop();

            Assert.IsTrue(run.TryAcceptOrders());
            Assert.That(run.GetReserved(FlowerIngredient.RainNightFruit), Is.EqualTo(1));
            Assert.IsTrue(run.TryCook(0));
            Assert.That(run.GetStock(FlowerIngredient.RainNightFruit), Is.Zero);
            Assert.That(run.GetStock(FlowerIngredient.WeatherFruit), Is.EqualTo(1));
            Assert.That(run.GetStock(FlowerIngredient.SunnyFruit), Is.EqualTo(1));
            if (cooked) run.Tick(4f);
            Assert.IsTrue(run.TryCloseShop());
            Assert.IsFalse(run.TryCloseShop());
            Assert.That(run.Cash, Is.Zero);
            Assert.That(run.GetStock(FlowerIngredient.Mushroom), Is.EqualTo(1));
            Assert.That(run.GetStock(FlowerIngredient.WeatherFruit), Is.EqualTo(1));
            Assert.That(run.GetStock(FlowerIngredient.SunnyFruit), Is.EqualTo(1));
            Assert.That(run.GetStock(FlowerIngredient.RainNightFruit), Is.EqualTo(1));
            Assert.That(run.GetRefundedStock(FlowerIngredient.Mushroom), Is.EqualTo(1));
            Assert.That(run.GetRefundedStock(FlowerIngredient.RainNightFruit), Is.EqualTo(1));
            Assert.That(run.GetRefundedStock(FlowerIngredient.WeatherFruit), Is.Zero);
            Assert.That(run.GetRefundedStock(FlowerIngredient.SunnyFruit), Is.Zero);
        }

        [Test]
        public void WaitingStovesDoNotDoubleReserveOneBatchOfStock()
        {
            var run = new FlowerMarketRun();
            run.TryStart();
            GatherMushrooms(run, 2);
            Assert.IsTrue(run.TryHarvest(FlowerIngredient.WeatherFruit));
            OpenShopDirectly(run);

            Assert.IsTrue(run.TryAcceptOrders());
            Assert.IsFalse(run.TryAcceptOrders());
            Assert.That(run.GetStove(0).Recipe, Is.EqualTo(FlowerRecipe.MushroomBento));
            Assert.That(run.GetStove(1).Stage, Is.EqualTo(FlowerOrderStage.Idle));
            Assert.That(run.GetStock(FlowerIngredient.Mushroom), Is.EqualTo(2));
            Assert.That(run.GetReserved(FlowerIngredient.Mushroom), Is.EqualTo(2));
            Assert.That(run.GetReserved(FlowerIngredient.WeatherFruit), Is.Zero);
            Assert.IsFalse(run.TryCook(-1));
            Assert.IsFalse(run.TryServe(FlowerMarketRun.StoveCount));
            Assert.IsFalse(run.TryServe(0));
            Assert.IsTrue(run.TryCloseShop());
            Assert.That(run.GetStock(FlowerIngredient.Mushroom), Is.EqualTo(2));
            Assert.That(run.GetReserved(FlowerIngredient.Mushroom), Is.Zero);
            Assert.That(run.GetRefundedStock(FlowerIngredient.Mushroom), Is.Zero);
        }

        [Test]
        public void TwoStovesCookAndServeIndependentlyAndOnlyTheServedStoveRefills()
        {
            var run = new FlowerMarketRun();
            run.TryStart();
            GatherMushrooms(run, 5);
            Assert.IsTrue(run.TryHarvest(FlowerIngredient.WeatherFruit));
            Assert.IsTrue(run.TryHarvest(FlowerIngredient.SunnyFruit));
            OpenShopDirectly(run);
            Assert.IsTrue(run.TryAcceptOrders());
            Assert.That(run.GetStove(0).Recipe, Is.EqualTo(FlowerRecipe.MushroomBento));
            Assert.That(run.GetStove(1).Recipe, Is.EqualTo(FlowerRecipe.FruitBento));
            Assert.That(run.GetReserved(FlowerIngredient.Mushroom), Is.EqualTo(3));
            Assert.That(run.GetReserved(FlowerIngredient.WeatherFruit), Is.EqualTo(1));
            Assert.That(run.GetReserved(FlowerIngredient.SunnyFruit), Is.Zero);

            Assert.IsTrue(run.TryCook(0));
            run.Tick(1f);
            Assert.IsTrue(run.TryCook(1));
            run.Tick(2f);
            Assert.That(run.GetStove(0).Stage, Is.EqualTo(FlowerOrderStage.Ready));
            Assert.That(run.GetStove(1).Stage, Is.EqualTo(FlowerOrderStage.Cooking));
            Assert.That(run.GetStove(1).RemainingSeconds, Is.EqualTo(2f));
            Assert.IsFalse(run.TryServe(1));
            Assert.IsTrue(run.TryServe(0));
            Assert.IsFalse(run.TryServe(0));
            Assert.That(run.GetStove(0).Stage, Is.EqualTo(FlowerOrderStage.Waiting));
            Assert.That(run.GetStove(0).Recipe, Is.EqualTo(FlowerRecipe.MushroomBento));
            Assert.That(run.GetStove(1).RemainingSeconds, Is.EqualTo(2f));
            Assert.That(run.Cash, Is.EqualTo(12));

            run.Tick(2f);
            Assert.IsTrue(run.TryServe(1));
            Assert.That(run.GetStove(1).Stage, Is.EqualTo(FlowerOrderStage.Idle));
            Assert.That(run.GetStove(0).Stage, Is.EqualTo(FlowerOrderStage.Waiting));
            Assert.That(run.GetStock(FlowerIngredient.SunnyFruit), Is.EqualTo(1));
            Assert.IsTrue(run.TryCook(0));
            run.Tick(3f);
            Assert.IsTrue(run.TryServe(0));
            Assert.That(run.Cash, Is.EqualTo(46));
            Assert.That(run.TotalRevenue, Is.EqualTo(46));
            Assert.IsFalse(run.HasPendingOrders);
            Assert.IsFalse(run.TryAcceptOrders());
        }

        [Test]
        public void CloseRefundsBothReadyAndCookingStovesWithoutCreditingRevenue()
        {
            var run = new FlowerMarketRun();
            run.TryStart();
            GatherMushrooms(run, 3);
            Assert.IsTrue(run.TryHarvest(FlowerIngredient.WeatherFruit));
            OpenShopDirectly(run);
            Assert.IsTrue(run.TryAcceptOrders());
            Assert.IsTrue(run.TryCook(0));
            Assert.IsTrue(run.TryCook(1));
            run.Tick(3f);
            Assert.That(run.GetStove(0).Stage, Is.EqualTo(FlowerOrderStage.Ready));
            Assert.That(run.GetStove(1).Stage, Is.EqualTo(FlowerOrderStage.Cooking));

            Assert.IsTrue(run.TryCloseShop());
            Assert.IsFalse(run.TryCloseShop());
            Assert.That(run.GetStock(FlowerIngredient.Mushroom), Is.EqualTo(3));
            Assert.That(run.GetStock(FlowerIngredient.WeatherFruit), Is.EqualTo(1));
            Assert.That(run.GetReserved(FlowerIngredient.Mushroom), Is.Zero);
            Assert.That(run.GetRefundedStock(FlowerIngredient.Mushroom), Is.EqualTo(3));
            Assert.That(run.GetRefundedStock(FlowerIngredient.WeatherFruit), Is.EqualTo(1));
            Assert.That(run.Cash, Is.Zero);
            Assert.That(run.TotalRevenue, Is.Zero);
            Assert.IsFalse(run.HasPendingOrders);
        }

        [Test]
        public void OneTimeUpgradeDebitsCashAndKeepsTunedDurationsAcrossDaysUntilReset()
        {
            var run = new FlowerMarketRun(mushroomCookSeconds: 5.5f, fruitCookSeconds: 7f,
                upgradedMushroomCookSeconds: 1.25f, upgradedFruitCookSeconds: 2.75f, kitchenUpgradeCost: 30);
            run.TryStart();
            GatherMushrooms(run, 6);
            OpenShopDirectly(run);
            Assert.IsFalse(run.CanUpgradeKitchen);
            Assert.IsFalse(run.TryUpgradeKitchen());
            Assert.IsTrue(run.TryGetRecipe(FlowerRecipe.FruitBento, out var baseFruit));
            Assert.That(baseFruit.CookSeconds, Is.EqualTo(7f));
            Assert.That(baseFruit.Price, Is.EqualTo(22));
            Assert.IsTrue(run.TryAcceptOrders());
            Assert.IsTrue(run.TryCook(0));
            Assert.IsTrue(run.TryCook(1));
            Assert.That(run.GetStove(0).RemainingSeconds, Is.EqualTo(5.5f));
            run.Tick(5.5f);
            Assert.IsTrue(run.TryServe(0));
            Assert.IsTrue(run.TryServe(1));
            Assert.IsTrue(run.TryCook(0));
            run.Tick(5.5f);
            Assert.IsTrue(run.TryServe(0));
            Assert.That(run.Cash, Is.EqualTo(36));
            Assert.IsFalse(run.TryUpgradeKitchen());
            Assert.IsTrue(run.TryCloseShop());
            Assert.IsFalse(run.TryUpgradeKitchen());
            Assert.IsTrue(run.TryStartNextDay());

            GatherMushrooms(run, 3);
            Assert.IsTrue(run.TryHarvest(FlowerIngredient.WeatherFruit));
            OpenShopDirectly(run);
            Assert.That(run.KitchenUpgradeCost, Is.EqualTo(30));
            Assert.IsTrue(run.CanUpgradeKitchen);
            Assert.IsTrue(run.TryUpgradeKitchen());
            Assert.IsFalse(run.TryUpgradeKitchen());
            Assert.That(run.Cash, Is.EqualTo(6));
            Assert.That(run.TotalRevenue, Is.EqualTo(36));
            Assert.IsTrue(run.TryAcceptOrders());
            Assert.IsTrue(run.TryCook(0));
            Assert.IsTrue(run.TryCook(1));
            Assert.That(run.GetStove(0).RemainingSeconds, Is.EqualTo(1.25f));
            Assert.That(run.GetStove(1).RemainingSeconds, Is.EqualTo(2.75f));
            Assert.IsTrue(run.TryCloseShop());
            Assert.IsTrue(run.KitchenUpgraded);
            Assert.That(run.Day, Is.EqualTo(2));
            Assert.That(run.GetStock(FlowerIngredient.Mushroom), Is.EqualTo(3));
            Assert.That(run.GetStock(FlowerIngredient.WeatherFruit), Is.EqualTo(1));
            Assert.IsTrue(run.TryStartNextDay());
            Assert.That(run.Day, Is.EqualTo(3));
            Assert.IsTrue(run.KitchenUpgraded);

            run.Reset();
            Assert.IsFalse(run.KitchenUpgraded);
            Assert.That(run.Cash, Is.Zero);
            Assert.That(run.TotalRevenue, Is.Zero);
            Assert.That(run.KitchenUpgradeCost, Is.EqualTo(30));
            Assert.IsTrue(run.TryGetRecipe(FlowerRecipe.FruitBento, out var resetFruit));
            Assert.That(resetFruit.CookSeconds, Is.EqualTo(7f));
        }

        [TestCase(false, 120f)]
        [TestCase(true, 60f)]
        public void EveningChoiceSetsDurationButOnlyAcceptingOrdersStartsTheClock(bool explore, float duration)
        {
            var run = new FlowerMarketRun();
            Assert.IsTrue(run.TryStart());
            GatherMushrooms(run, 2);
            Assert.IsTrue(run.TryEndDay());
            Assert.IsTrue(run.TryChooseDusk(explore));
            if (explore) Assert.IsTrue(run.TryReturnToShop());
            Assert.That(run.TradingDurationSeconds, Is.EqualTo(duration));
            run.Tick(duration + 1f);
            Assert.That(run.Phase, Is.EqualTo(FlowerMarketPhase.ReturningToShop));
            Assert.That(run.RemainingTradingSeconds, Is.Zero);

            Assert.IsTrue(run.TryOpenShop());
            run.Tick(duration + 1f);
            Assert.That(run.Phase, Is.EqualTo(FlowerMarketPhase.Trading));
            Assert.That(run.RemainingTradingSeconds, Is.Zero);
            Assert.IsFalse(run.TryStartNextDay());
            Assert.IsTrue(run.TryAcceptOrders());
            Assert.That(run.RemainingTradingSeconds, Is.EqualTo(duration));
            run.Tick(1f);
            Assert.IsFalse(run.TryAcceptOrders());
            Assert.That(run.RemainingTradingSeconds, Is.EqualTo(duration - 1f));
        }

        [Test]
        public void DeadlineWinsOverCookingCompletionAndRefundsTheExactFruit()
        {
            var run = new FlowerMarketRun(nightTradingSeconds: 4f);
            Assert.IsTrue(run.TryStart());
            GatherMushrooms(run, 3);
            Assert.IsTrue(run.TryHarvest(FlowerIngredient.WeatherFruit));
            Assert.IsTrue(run.TryHarvest(FlowerIngredient.SunnyFruit));
            Assert.IsTrue(run.TryEndDay());
            Assert.IsTrue(run.TryChooseDusk(true));
            Assert.IsTrue(run.TryHarvest(FlowerIngredient.RainNightFruit));
            Assert.IsTrue(run.TryReturnToShop());
            Assert.IsTrue(run.TryOpenShop());
            Assert.IsTrue(run.TryAcceptOrders());
            Assert.IsTrue(run.TryCook(0));
            Assert.IsTrue(run.TryCook(1));

            run.Tick(3f);
            Assert.That(run.GetStove(0).Stage, Is.EqualTo(FlowerOrderStage.Ready));
            Assert.That(run.GetStove(1).RemainingSeconds, Is.EqualTo(1f));
            Assert.That(run.RemainingTradingSeconds, Is.EqualTo(1f));
            Assert.IsTrue(run.TryServe(0));
            Assert.That(run.GetStock(FlowerIngredient.RainNightFruit), Is.Zero);

            run.Tick(1f);
            Assert.That(run.Phase, Is.EqualTo(FlowerMarketPhase.DaySummary));
            Assert.That(run.Day, Is.EqualTo(1));
            Assert.IsTrue(run.ClosedByTimeLimit);
            Assert.IsFalse(run.OrdersAccepted);
            Assert.IsFalse(run.HasPendingOrders);
            Assert.That(run.RemainingTradingSeconds, Is.Zero);
            Assert.That(run.GetStove(1).Stage, Is.EqualTo(FlowerOrderStage.Idle));
            Assert.IsFalse(run.TryServe(1));
            Assert.IsFalse(run.TryCook(0));
            Assert.IsFalse(run.TryCloseShop());
            run.Tick(100f);
            Assert.That(run.Day, Is.EqualTo(1));
            Assert.That(run.EveningRevenue, Is.EqualTo(12));
            Assert.That(run.TotalRevenue, Is.EqualTo(12));
            Assert.That(run.Cash, Is.EqualTo(12));
            Assert.That(run.GetRefundedStock(FlowerIngredient.Mushroom), Is.EqualTo(1));
            Assert.That(run.GetRefundedStock(FlowerIngredient.RainNightFruit), Is.EqualTo(1));
            Assert.That(run.GetRefundedStock(FlowerIngredient.WeatherFruit), Is.Zero);
            Assert.That(run.GetRefundedStock(FlowerIngredient.SunnyFruit), Is.Zero);
            Assert.That(run.GetStock(FlowerIngredient.Mushroom), Is.EqualTo(1));
            Assert.That(run.GetStock(FlowerIngredient.RainNightFruit), Is.EqualTo(1));
            Assert.That(run.GetStock(FlowerIngredient.WeatherFruit), Is.EqualTo(1));
            Assert.That(run.GetStock(FlowerIngredient.SunnyFruit), Is.EqualTo(1));

            Assert.IsTrue(run.TryStartNextDay());
            Assert.That(run.Day, Is.EqualTo(2));
            Assert.That(run.EveningRevenue, Is.Zero);
            Assert.That(run.GetRefundedStock(FlowerIngredient.RainNightFruit), Is.Zero);
            Assert.IsFalse(run.ClosedByTimeLimit);
            Assert.That(run.GetStock(FlowerIngredient.RainNightFruit), Is.EqualTo(1));
            Assert.That(run.Cash, Is.EqualTo(12));
            Assert.That(run.TotalRevenue, Is.EqualTo(12));
        }

        [Test]
        public void DeadlineRefundsReadyOrderAndOnlyReleasesTheWaitingReservation()
        {
            var run = new FlowerMarketRun(directTradingSeconds: 5f);
            Assert.IsTrue(run.TryStart());
            GatherMushrooms(run, 3);
            Assert.IsTrue(run.TryHarvest(FlowerIngredient.WeatherFruit));
            OpenShopDirectly(run);
            Assert.IsTrue(run.TryAcceptOrders());
            Assert.IsTrue(run.TryCook(0));
            run.Tick(3f);
            Assert.That(run.GetStove(0).Stage, Is.EqualTo(FlowerOrderStage.Ready));
            Assert.That(run.GetStove(1).Stage, Is.EqualTo(FlowerOrderStage.Waiting));
            Assert.That(run.GetReserved(FlowerIngredient.WeatherFruit), Is.EqualTo(1));

            run.Tick(2f);
            Assert.That(run.Phase, Is.EqualTo(FlowerMarketPhase.DaySummary));
            Assert.That(run.GetRefundedStock(FlowerIngredient.Mushroom), Is.EqualTo(2));
            Assert.That(run.GetRefundedStock(FlowerIngredient.WeatherFruit), Is.Zero);
            Assert.That(run.GetStock(FlowerIngredient.Mushroom), Is.EqualTo(3));
            Assert.That(run.GetStock(FlowerIngredient.WeatherFruit), Is.EqualTo(1));
            Assert.That(run.GetReserved(FlowerIngredient.WeatherFruit), Is.Zero);
            Assert.That(run.Cash, Is.Zero);

            run.Reset();
            Assert.That(run.Phase, Is.EqualTo(FlowerMarketPhase.Locked));
            Assert.That(run.RemainingTradingSeconds, Is.Zero);
            Assert.That(run.GetRefundedStock(FlowerIngredient.Mushroom), Is.Zero);
            Assert.IsFalse(run.ClosedByTimeLimit);
        }

        [Test]
        public void ShopCanCloseBeforeOrdersAndWaitInSummaryUntilNextDay()
        {
            var run = new FlowerMarketRun();
            Assert.IsTrue(run.TryStart());
            OpenShopDirectly(run);
            Assert.IsFalse(run.TryAcceptOrders());
            Assert.That(run.RemainingTradingSeconds, Is.Zero);
            Assert.IsTrue(run.TryCloseShop());
            Assert.That(run.Phase, Is.EqualTo(FlowerMarketPhase.DaySummary));
            Assert.That(run.EveningRevenue, Is.Zero);
            Assert.IsFalse(run.ClosedByTimeLimit);
            Assert.IsTrue(run.IsNight);
            run.Tick(500f);
            Assert.That(run.Day, Is.EqualTo(1));
            Assert.IsFalse(run.TryAcceptOrders());
            Assert.IsTrue(run.TryStartNextDay());
            Assert.That(run.Day, Is.EqualTo(2));
            Assert.That(run.Phase, Is.EqualTo(FlowerMarketPhase.DayGathering));
        }

        [Test]
        public void InvalidCookingDurationsAndUpgradeFeeAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FlowerMarketRun(mushroomCookSeconds: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FlowerMarketRun(fruitCookSeconds: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FlowerMarketRun(upgradedFruitCookSeconds: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FlowerMarketRun(kitchenUpgradeCost: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FlowerMarketRun(directTradingSeconds: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FlowerMarketRun(nightTradingSeconds: 0f));
        }

        private static void GatherMushrooms(FlowerMarketRun run, int count)
        {
            for (int i = 0; i < count; i++) Assert.IsTrue(run.TryHarvest(FlowerIngredient.Mushroom));
        }

        private static void OpenShopDirectly(FlowerMarketRun run)
        {
            Assert.IsTrue(run.TryEndDay());
            Assert.IsTrue(run.TryChooseDusk(false));
            Assert.That(run.Phase, Is.EqualTo(FlowerMarketPhase.ReturningToShop));
            Assert.IsTrue(run.TryOpenShop());
        }
    }
}
