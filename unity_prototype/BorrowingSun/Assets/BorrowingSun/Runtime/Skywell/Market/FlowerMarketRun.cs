using System;

namespace BorrowingSun
{
    public enum FlowerIngredient { Mushroom, WeatherFruit }
    public enum FlowerRecipe { MushroomBento, FruitBento }
    public enum FlowerOrderStage { Idle, Cooking, Ready, Served }

    public readonly struct FlowerRecipeInfo
    {
        public int Mushrooms { get; }
        public int WeatherFruit { get; }
        public float CookSeconds { get; }
        public int Price { get; }

        internal FlowerRecipeInfo(int mushrooms, int weatherFruit, float cookSeconds, int price)
        {
            Mushrooms = mushrooms;
            WeatherFruit = weatherFruit;
            CookSeconds = cookSeconds;
            Price = price;
        }
    }

    public sealed class FlowerMarketRun
    {
        private int mushrooms;
        private int weatherFruit;

        public bool IsStarted => Day > 0;
        public int Day { get; private set; }
        public int Cash { get; private set; }
        public FlowerOrderStage OrderStage { get; private set; }
        public FlowerRecipe? CurrentRecipe { get; private set; }
        public float CookTimeRemaining { get; private set; }

        private bool HasPendingOrder => OrderStage == FlowerOrderStage.Cooking || OrderStage == FlowerOrderStage.Ready;

        public bool TryStart()
        {
            if (IsStarted) return false;
            Day = 1;
            return true;
        }

        public int GetStock(FlowerIngredient ingredient) => ingredient switch
        {
            FlowerIngredient.Mushroom => mushrooms,
            FlowerIngredient.WeatherFruit => weatherFruit,
            _ => 0
        };

        public bool TryHarvest(FlowerIngredient ingredient)
        {
            if (!IsStarted) return false;

            switch (ingredient)
            {
                case FlowerIngredient.Mushroom:
                    mushrooms++;
                    return true;
                case FlowerIngredient.WeatherFruit:
                    weatherFruit++;
                    return true;
                default:
                    return false;
            }
        }

        public static bool TryGetRecipe(FlowerRecipe recipe, out FlowerRecipeInfo info)
        {
            switch (recipe)
            {
                case FlowerRecipe.MushroomBento:
                    info = new FlowerRecipeInfo(mushrooms: 2, weatherFruit: 0, cookSeconds: 3f, price: 12);
                    return true;
                case FlowerRecipe.FruitBento:
                    info = new FlowerRecipeInfo(mushrooms: 1, weatherFruit: 1, cookSeconds: 4f, price: 22);
                    return true;
                default:
                    info = default;
                    return false;
            }
        }

        public bool CanCook(FlowerRecipe recipe) => TryGetRecipe(recipe, out var info) && CanCook(info);

        public bool TryCook(FlowerRecipe recipe)
        {
            if (!TryGetRecipe(recipe, out var info) || !CanCook(info)) return false;

            mushrooms -= info.Mushrooms;
            weatherFruit -= info.WeatherFruit;
            CurrentRecipe = recipe;
            CookTimeRemaining = info.CookSeconds;
            OrderStage = FlowerOrderStage.Cooking;
            return true;
        }

        public void Tick(float deltaSeconds)
        {
            if (OrderStage != FlowerOrderStage.Cooking || !(deltaSeconds > 0f)) return;

            CookTimeRemaining = Math.Max(0f, CookTimeRemaining - deltaSeconds);
            if (CookTimeRemaining == 0f) OrderStage = FlowerOrderStage.Ready;
        }

        public bool TryServe()
        {
            if (OrderStage != FlowerOrderStage.Ready || !CurrentRecipe.HasValue ||
                !TryGetRecipe(CurrentRecipe.Value, out var info)) return false;

            Cash += info.Price;
            OrderStage = FlowerOrderStage.Served;
            return true;
        }

        public bool TryNextDay()
        {
            if (!IsStarted || HasPendingOrder) return false;

            Day++;
            ClearOrder();
            return true;
        }

        public void Reset()
        {
            Day = Cash = mushrooms = weatherFruit = 0;
            ClearOrder();
        }

        private bool CanCook(FlowerRecipeInfo info) =>
            IsStarted && !HasPendingOrder && mushrooms >= info.Mushrooms && weatherFruit >= info.WeatherFruit;

        private void ClearOrder()
        {
            OrderStage = FlowerOrderStage.Idle;
            CurrentRecipe = null;
            CookTimeRemaining = 0f;
        }
    }
}
