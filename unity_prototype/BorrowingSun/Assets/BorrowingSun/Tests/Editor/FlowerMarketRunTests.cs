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
            Assert.IsFalse(run.TryNextDay());
            Assert.IsTrue(run.TryStart());
            Assert.That(run.Day, Is.EqualTo(1));
            Assert.IsTrue(run.TryHarvest(FlowerIngredient.Mushroom));
            Assert.IsFalse(run.TryStart());
            Assert.That(run.GetStock(FlowerIngredient.Mushroom), Is.EqualTo(1));
        }

        [Test]
        public void MissingIngredientDoesNotDebitAvailableStock()
        {
            var run = new FlowerMarketRun();
            run.TryStart();
            run.TryHarvest(FlowerIngredient.Mushroom);

            Assert.IsFalse(run.CanCook(FlowerRecipe.FruitBento));
            Assert.IsFalse(run.TryCook(FlowerRecipe.FruitBento));
            Assert.That(run.GetStock(FlowerIngredient.Mushroom), Is.EqualTo(1));
            Assert.That(run.OrderStage, Is.EqualTo(FlowerOrderStage.Idle));

            run.TryHarvest(FlowerIngredient.WeatherFruit);
            Assert.IsTrue(run.TryCook(FlowerRecipe.FruitBento));
            Assert.That(run.GetStock(FlowerIngredient.Mushroom), Is.Zero);
            Assert.That(run.GetStock(FlowerIngredient.WeatherFruit), Is.Zero);
        }

        [TestCase(FlowerRecipe.MushroomBento, 2, 0, 3f, 12)]
        [TestCase(FlowerRecipe.FruitBento, 1, 1, 4f, 22)]
        public void EachRecipeConsumesItsIngredientsAndPaysExactlyOnce(
            FlowerRecipe recipe, int mushrooms, int fruit, float seconds, int price)
        {
            var run = new FlowerMarketRun();
            run.TryStart();
            for (var i = 0; i < mushrooms; i++) run.TryHarvest(FlowerIngredient.Mushroom);
            for (var i = 0; i < fruit; i++) run.TryHarvest(FlowerIngredient.WeatherFruit);

            Assert.IsTrue(run.CanCook(recipe));
            Assert.IsTrue(run.TryCook(recipe));
            Assert.That(run.GetStock(FlowerIngredient.Mushroom), Is.Zero);
            Assert.That(run.GetStock(FlowerIngredient.WeatherFruit), Is.Zero);
            Assert.That(run.OrderStage, Is.EqualTo(FlowerOrderStage.Cooking));
            Assert.That(run.CookTimeRemaining, Is.EqualTo(seconds));
            Assert.IsFalse(run.TryCook(recipe));
            Assert.IsFalse(run.TryNextDay());

            run.Tick(seconds - .5f);
            Assert.IsFalse(run.TryServe());
            Assert.That(run.Cash, Is.Zero);
            run.Tick(.5f);
            Assert.That(run.OrderStage, Is.EqualTo(FlowerOrderStage.Ready));
            Assert.IsFalse(run.TryNextDay());
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
            run.TryCook(FlowerRecipe.MushroomBento);
            run.Tick(3f);
            run.TryServe();

            Assert.IsTrue(run.TryNextDay());
            Assert.That(run.Day, Is.EqualTo(2));
            Assert.That(run.Cash, Is.EqualTo(12));
            Assert.That(run.GetStock(FlowerIngredient.Mushroom), Is.EqualTo(1));
            Assert.That(run.GetStock(FlowerIngredient.WeatherFruit), Is.EqualTo(1));
            Assert.That(run.OrderStage, Is.EqualTo(FlowerOrderStage.Idle));
            Assert.That(run.CurrentRecipe, Is.Null);
            Assert.IsTrue(run.TryCook(FlowerRecipe.FruitBento));

            run.Reset();
            Assert.IsFalse(run.IsStarted);
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
    }
}
