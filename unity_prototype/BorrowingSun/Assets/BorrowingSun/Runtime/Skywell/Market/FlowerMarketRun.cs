using System;

namespace BorrowingSun
{
    public enum FlowerIngredient { Mushroom, WeatherFruit, SunnyFruit, RainNightFruit }
    public enum FlowerRecipe { MushroomBento, FruitBento }
    public enum FlowerOrderStage { Idle, Cooking, Ready, Served }
    public enum FlowerMarketPhase { Locked, DayGathering, DuskChoice, NightExploration, ReturningToShop, Trading }

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
        private static readonly FlowerIngredient[] FruitPriority =
            { FlowerIngredient.RainNightFruit, FlowerIngredient.WeatherFruit, FlowerIngredient.SunnyFruit };

        private readonly int[] stock = new int[(int)FlowerIngredient.RainNightFruit + 1];
        private readonly float dayGatherSeconds;
        private readonly float nightGatherSeconds;
        private FlowerIngredient? reservedFruit;

        public bool IsStarted => Phase != FlowerMarketPhase.Locked;
        public FlowerMarketPhase Phase { get; private set; }
        public bool CanGather => Phase == FlowerMarketPhase.DayGathering || Phase == FlowerMarketPhase.NightExploration;
        public bool IsNight => Phase == FlowerMarketPhase.NightExploration ||
            Phase == FlowerMarketPhase.ReturningToShop || Phase == FlowerMarketPhase.Trading;
        public float RemainingGatherSeconds { get; private set; }
        public int Day { get; private set; }
        public int Cash { get; private set; }
        public FlowerOrderStage OrderStage { get; private set; }
        public FlowerRecipe? CurrentRecipe { get; private set; }
        public float CookTimeRemaining { get; private set; }

        private bool HasPendingOrder => OrderStage == FlowerOrderStage.Cooking || OrderStage == FlowerOrderStage.Ready;

        public FlowerMarketRun(float dayGatherSeconds = 75f, float nightGatherSeconds = 45f)
        {
            if (!(dayGatherSeconds > 0f) || float.IsInfinity(dayGatherSeconds))
                throw new ArgumentOutOfRangeException(nameof(dayGatherSeconds));
            if (!(nightGatherSeconds > 0f) || float.IsInfinity(nightGatherSeconds))
                throw new ArgumentOutOfRangeException(nameof(nightGatherSeconds));

            this.dayGatherSeconds = dayGatherSeconds;
            this.nightGatherSeconds = nightGatherSeconds;
        }

        public bool TryStart()
        {
            if (IsStarted) return false;
            BeginDay();
            return true;
        }

        public bool TryEndDay()
        {
            if (Phase != FlowerMarketPhase.DayGathering) return false;
            Phase = FlowerMarketPhase.DuskChoice;
            RemainingGatherSeconds = 0f;
            return true;
        }

        public bool TryChooseDusk(bool explore)
        {
            if (Phase != FlowerMarketPhase.DuskChoice) return false;
            Phase = explore ? FlowerMarketPhase.NightExploration : FlowerMarketPhase.ReturningToShop;
            RemainingGatherSeconds = explore ? nightGatherSeconds : 0f;
            return true;
        }

        public bool TryReturnToShop()
        {
            if (Phase != FlowerMarketPhase.NightExploration) return false;
            Phase = FlowerMarketPhase.ReturningToShop;
            RemainingGatherSeconds = 0f;
            return true;
        }

        public bool TryOpenShop()
        {
            if (Phase != FlowerMarketPhase.ReturningToShop) return false;
            Phase = FlowerMarketPhase.Trading;
            return true;
        }

        public FlowerIngredient? GetOrchardProduce(bool raining, bool directlySunlit)
        {
            if (!CanGather) return null;
            if (raining) return IsNight ? FlowerIngredient.RainNightFruit : FlowerIngredient.WeatherFruit;
            return !IsNight && directlySunlit ? FlowerIngredient.SunnyFruit : (FlowerIngredient?)null;
        }

        public int GetStock(FlowerIngredient ingredient)
        {
            int index = (int)ingredient;
            return index >= 0 && index < stock.Length ? stock[index] : 0;
        }

        public bool TryHarvest(FlowerIngredient ingredient)
        {
            if (!CanGather) return false;

            bool available = ingredient switch
            {
                FlowerIngredient.Mushroom => true,
                FlowerIngredient.WeatherFruit => !IsNight,
                FlowerIngredient.SunnyFruit => !IsNight,
                FlowerIngredient.RainNightFruit => IsNight,
                _ => false
            };
            if (!available) return false;

            stock[(int)ingredient]++;
            return true;
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

        public bool CanCook(FlowerRecipe recipe) => TryGetRecipe(recipe, out var info) && CanCook(info, out _);

        public bool TryCook(FlowerRecipe recipe)
        {
            if (!TryGetRecipe(recipe, out var info) || !CanCook(info, out var fruit)) return false;

            stock[(int)FlowerIngredient.Mushroom] -= info.Mushrooms;
            if (fruit.HasValue) stock[(int)fruit.Value] -= info.WeatherFruit;
            reservedFruit = fruit;
            CurrentRecipe = recipe;
            CookTimeRemaining = info.CookSeconds;
            OrderStage = FlowerOrderStage.Cooking;
            return true;
        }

        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds > 0f)) return;

            if (CanGather)
            {
                RemainingGatherSeconds = Math.Max(0f, RemainingGatherSeconds - deltaSeconds);
                if (RemainingGatherSeconds > 0f) return;
                if (Phase == FlowerMarketPhase.DayGathering) TryEndDay();
                else TryReturnToShop();
                return;
            }

            if (OrderStage != FlowerOrderStage.Cooking) return;
            CookTimeRemaining = Math.Max(0f, CookTimeRemaining - deltaSeconds);
            if (CookTimeRemaining == 0f) OrderStage = FlowerOrderStage.Ready;
        }

        public bool TryServe()
        {
            if (Phase != FlowerMarketPhase.Trading || OrderStage != FlowerOrderStage.Ready || !CurrentRecipe.HasValue ||
                !TryGetRecipe(CurrentRecipe.Value, out var info)) return false;

            Cash += info.Price;
            OrderStage = FlowerOrderStage.Served;
            return true;
        }

        public bool TryCloseShop()
        {
            if (Phase != FlowerMarketPhase.Trading) return false;

            if (HasPendingOrder && CurrentRecipe.HasValue && TryGetRecipe(CurrentRecipe.Value, out var info))
            {
                stock[(int)FlowerIngredient.Mushroom] += info.Mushrooms;
                if (reservedFruit.HasValue) stock[(int)reservedFruit.Value] += info.WeatherFruit;
            }
            ClearOrder();
            BeginDay();
            return true;
        }

        public void Reset()
        {
            Day = Cash = 0;
            Phase = FlowerMarketPhase.Locked;
            RemainingGatherSeconds = 0f;
            Array.Clear(stock, 0, stock.Length);
            ClearOrder();
        }

        private bool CanCook(FlowerRecipeInfo info, out FlowerIngredient? fruit)
        {
            fruit = null;
            if (Phase != FlowerMarketPhase.Trading || HasPendingOrder || GetStock(FlowerIngredient.Mushroom) < info.Mushrooms)
                return false;

            if (info.WeatherFruit == 0) return true;
            foreach (var ingredient in FruitPriority)
            {
                if (GetStock(ingredient) < info.WeatherFruit) continue;
                fruit = ingredient;
                return true;
            }
            return false;
        }

        private void BeginDay()
        {
            Day++;
            Phase = FlowerMarketPhase.DayGathering;
            RemainingGatherSeconds = dayGatherSeconds;
        }

        private void ClearOrder()
        {
            OrderStage = FlowerOrderStage.Idle;
            CurrentRecipe = null;
            reservedFruit = null;
            CookTimeRemaining = 0f;
        }
    }
}
