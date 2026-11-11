using NUnit.Framework;

namespace BorrowingSun.Tests
{
    public sealed class FlowerMarketRunTests
    {
        [Test]
        public void CommandsRequireStartAndRepeatedStartKeepsStock()
        {
            var run = new FlowerMarketRun();

            Assert.IsFalse(run.TryHarvest(FlowerIngredient.Mushroom));
            Assert.IsFalse(run.TryCook(FlowerRecipe.MushroomBento));
            Assert.IsFalse(run.TryServe());
            Assert.IsFalse(run.TryEndDay());
            Assert.IsFalse(run.TryChooseDusk(true));
            Assert.IsFalse(run.TryReturnToShop());
            Assert.IsFalse(run.TryOpenShop());
            Assert.IsFalse(run.TryCloseShop());
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

            Assert.IsFalse(run.CanCook(FlowerRecipe.FruitBento));
            Assert.IsFalse(run.TryCook(FlowerRecipe.FruitBento));
            Assert.That(run.GetStock(FlowerIngredient.Mushroom), Is.EqualTo(1));
            Assert.That(run.OrderStage, Is.EqualTo(FlowerOrderStage.Idle));
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
                Assert.IsFalse(run.CanCook(recipe));
                OpenShopDirectly(run);
            }

            Assert.IsTrue(run.CanCook(recipe));
            Assert.IsTrue(run.TryCook(recipe));
            Assert.That(run.GetStock(FlowerIngredient.Mushroom), Is.Zero);
            Assert.That(run.GetStock(fruit), Is.Zero);
            Assert.That(run.OrderStage, Is.EqualTo(FlowerOrderStage.Cooking));
            Assert.That(run.CookTimeRemaining, Is.EqualTo(seconds));
            Assert.IsFalse(run.TryCook(recipe));

            run.Tick(seconds - .5f);
            Assert.IsFalse(run.TryServe());
            Assert.That(run.Cash, Is.Zero);
            run.Tick(.5f);
            Assert.That(run.OrderStage, Is.EqualTo(FlowerOrderStage.Ready));
            Assert.IsTrue(run.TryServe());
            Assert.IsFalse(run.TryServe());
            Assert.That(run.Cash, Is.EqualTo(price));
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
            run.TryCook(FlowerRecipe.MushroomBento);
            run.Tick(3f);
            run.TryServe();

            Assert.IsTrue(run.TryCloseShop());
            Assert.That(run.Day, Is.EqualTo(2));
            Assert.That(run.Phase, Is.EqualTo(FlowerMarketPhase.DayGathering));
            Assert.That(run.RemainingGatherSeconds, Is.EqualTo(75f));
            Assert.IsFalse(run.IsNight);
            Assert.That(run.Cash, Is.EqualTo(12));
            Assert.That(run.GetStock(FlowerIngredient.Mushroom), Is.EqualTo(1));
            Assert.That(run.GetStock(FlowerIngredient.WeatherFruit), Is.EqualTo(1));
            Assert.That(run.OrderStage, Is.EqualTo(FlowerOrderStage.Idle));
            Assert.That(run.CurrentRecipe, Is.Null);
            OpenShopDirectly(run);
            Assert.IsTrue(run.TryCook(FlowerRecipe.FruitBento));

            run.Reset();
            Assert.IsFalse(run.IsStarted);
            Assert.That(run.Phase, Is.EqualTo(FlowerMarketPhase.Locked));
            Assert.That(run.RemainingGatherSeconds, Is.Zero);
            Assert.That(run.Day, Is.Zero);
            Assert.That(run.Cash, Is.Zero);
            Assert.That(run.GetStock(FlowerIngredient.Mushroom), Is.Zero);
            Assert.That(run.GetStock(FlowerIngredient.WeatherFruit), Is.Zero);
            Assert.That(run.OrderStage, Is.EqualTo(FlowerOrderStage.Idle));
            Assert.That(run.CurrentRecipe, Is.Null);
            Assert.That(run.CookTimeRemaining, Is.Zero);
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

            Assert.IsTrue(run.TryCook(FlowerRecipe.FruitBento));
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
