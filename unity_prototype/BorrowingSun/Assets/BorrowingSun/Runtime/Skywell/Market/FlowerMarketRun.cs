using System;

namespace BorrowingSun
{
    public enum FlowerIngredient { Mushroom, WeatherFruit, SunnyFruit, RainNightFruit }
    public enum FlowerRecipe { MushroomBento, FruitBento }
    public enum FlowerOrderStage { Idle, Waiting, Cooking, Ready }
    public enum FlowerMarketPhase { Locked, DayGathering, DuskChoice, NightExploration, ReturningToShop, Trading, DaySummary }

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

    public readonly struct FlowerStoveStatus
    {
        public FlowerOrderStage Stage { get; }
        public FlowerRecipe? Recipe { get; }
        public float RemainingSeconds { get; }

        internal FlowerStoveStatus(FlowerOrderStage stage, FlowerRecipe recipe, float remainingSeconds)
        {
            Stage = stage;
            Recipe = recipe;
            RemainingSeconds = remainingSeconds;
        }
    }

    public sealed class FlowerMarketRun
    {
        public const int StoveCount = 2;

        private struct Order
        {
            internal FlowerOrderStage Stage;
            internal FlowerRecipe Recipe;
            internal FlowerIngredient? Fruit;
            internal float RemainingSeconds;
        }

        private static readonly FlowerIngredient[] FruitPriority =
            { FlowerIngredient.RainNightFruit, FlowerIngredient.WeatherFruit, FlowerIngredient.SunnyFruit };

        private readonly int[] stock = new int[(int)FlowerIngredient.RainNightFruit + 1];
        private readonly int[] refundedStock = new int[(int)FlowerIngredient.RainNightFruit + 1];
        private readonly Order[] orders = new Order[StoveCount];
        private readonly float dayGatherSeconds;
        private readonly float nightGatherSeconds;
        private readonly float mushroomCookSeconds;
        private readonly float fruitCookSeconds;
        private readonly float upgradedMushroomCookSeconds;
        private readonly float upgradedFruitCookSeconds;
        private readonly float directTradingSeconds;
        private readonly float nightTradingSeconds;
        private bool choseNightExploration;

        public bool IsStarted => Phase != FlowerMarketPhase.Locked;
        public FlowerMarketPhase Phase { get; private set; }
        public bool CanGather => Phase == FlowerMarketPhase.DayGathering || Phase == FlowerMarketPhase.NightExploration;
        public bool IsNight => Phase == FlowerMarketPhase.NightExploration ||
            Phase == FlowerMarketPhase.ReturningToShop || Phase == FlowerMarketPhase.Trading ||
            Phase == FlowerMarketPhase.DaySummary;
        public float RemainingGatherSeconds { get; private set; }
        public float TradingDurationSeconds => choseNightExploration ? nightTradingSeconds : directTradingSeconds;
        public float RemainingTradingSeconds { get; private set; }
        public int Day { get; private set; }
        public int Cash { get; private set; }
        public int TotalRevenue { get; private set; }
        public int EveningRevenue { get; private set; }
        public bool ClosedByTimeLimit { get; private set; }
        public bool KitchenUpgraded { get; private set; }
        public int KitchenUpgradeCost { get; }
        public bool OrdersAccepted { get; private set; }
        public bool CanUpgradeKitchen => Phase == FlowerMarketPhase.Trading && !OrdersAccepted &&
            !KitchenUpgraded && Cash >= KitchenUpgradeCost;

        public bool HasPendingOrders
        {
            get
            {
                foreach (var order in orders)
                    if (order.Stage != FlowerOrderStage.Idle) return true;
                return false;
            }
        }

        public FlowerMarketRun(float dayGatherSeconds = 75f, float nightGatherSeconds = 45f,
            float mushroomCookSeconds = 3f, float fruitCookSeconds = 4f,
            float upgradedMushroomCookSeconds = 2.1f, float upgradedFruitCookSeconds = 2.8f,
            int kitchenUpgradeCost = 36, float directTradingSeconds = 120f, float nightTradingSeconds = 60f)
        {
            ValidateSeconds(dayGatherSeconds, nameof(dayGatherSeconds));
            ValidateSeconds(nightGatherSeconds, nameof(nightGatherSeconds));
            ValidateSeconds(mushroomCookSeconds, nameof(mushroomCookSeconds));
            ValidateSeconds(fruitCookSeconds, nameof(fruitCookSeconds));
            ValidateSeconds(upgradedMushroomCookSeconds, nameof(upgradedMushroomCookSeconds));
            ValidateSeconds(upgradedFruitCookSeconds, nameof(upgradedFruitCookSeconds));
            ValidateSeconds(directTradingSeconds, nameof(directTradingSeconds));
            ValidateSeconds(nightTradingSeconds, nameof(nightTradingSeconds));
            if (kitchenUpgradeCost <= 0) throw new ArgumentOutOfRangeException(nameof(kitchenUpgradeCost));

            this.dayGatherSeconds = dayGatherSeconds;
            this.nightGatherSeconds = nightGatherSeconds;
            this.mushroomCookSeconds = mushroomCookSeconds;
            this.fruitCookSeconds = fruitCookSeconds;
            this.upgradedMushroomCookSeconds = upgradedMushroomCookSeconds;
            this.upgradedFruitCookSeconds = upgradedFruitCookSeconds;
            this.directTradingSeconds = directTradingSeconds;
            this.nightTradingSeconds = nightTradingSeconds;
            KitchenUpgradeCost = kitchenUpgradeCost;
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
            choseNightExploration = explore;
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

        public int GetRefundedStock(FlowerIngredient ingredient)
        {
            int index = (int)ingredient;
            return index >= 0 && index < refundedStock.Length ? refundedStock[index] : 0;
        }

        public int GetReserved(FlowerIngredient ingredient)
        {
            int reserved = 0;
            foreach (var order in orders)
            {
                if (order.Stage != FlowerOrderStage.Waiting) continue;
                TryGetRecipe(order.Recipe, out var info);
                if (ingredient == FlowerIngredient.Mushroom) reserved += info.Mushrooms;
                else if (order.Fruit == ingredient) reserved += info.WeatherFruit;
            }
            return reserved;
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

        public bool TryGetRecipe(FlowerRecipe recipe, out FlowerRecipeInfo info)
        {
            switch (recipe)
            {
                case FlowerRecipe.MushroomBento:
                    info = new FlowerRecipeInfo(mushrooms: 2, weatherFruit: 0,
                        cookSeconds: KitchenUpgraded ? upgradedMushroomCookSeconds : mushroomCookSeconds, price: 12);
                    return true;
                case FlowerRecipe.FruitBento:
                    info = new FlowerRecipeInfo(mushrooms: 1, weatherFruit: 1,
                        cookSeconds: KitchenUpgraded ? upgradedFruitCookSeconds : fruitCookSeconds, price: 22);
                    return true;
                default:
                    info = default;
                    return false;
            }
        }

        public FlowerStoveStatus GetStove(int slot)
        {
            if (!ValidSlot(slot)) return default;
            var order = orders[slot];
            return order.Stage == FlowerOrderStage.Idle
                ? default : new FlowerStoveStatus(order.Stage, order.Recipe, order.RemainingSeconds);
        }

        public bool TryUpgradeKitchen()
        {
            if (!CanUpgradeKitchen) return false;
            Cash -= KitchenUpgradeCost;
            KitchenUpgraded = true;
            return true;
        }

        public bool TryAcceptOrders()
        {
            if (Phase != FlowerMarketPhase.Trading || OrdersAccepted) return false;
            for (int slot = 0; slot < StoveCount; slot++) FillStove(slot);
            OrdersAccepted = HasPendingOrders;
            if (OrdersAccepted) RemainingTradingSeconds = TradingDurationSeconds;
            return OrdersAccepted;
        }

        public bool TryCook(int slot)
        {
            if (Phase != FlowerMarketPhase.Trading || !ValidSlot(slot)) return false;
            ref var order = ref orders[slot];
            if (order.Stage != FlowerOrderStage.Waiting || !TryGetRecipe(order.Recipe, out var info)) return false;

            stock[(int)FlowerIngredient.Mushroom] -= info.Mushrooms;
            if (order.Fruit.HasValue) stock[(int)order.Fruit.Value] -= info.WeatherFruit;
            order.Stage = FlowerOrderStage.Cooking;
            order.RemainingSeconds = info.CookSeconds;
            return true;
        }

        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds > 0f) || float.IsInfinity(deltaSeconds)) return;

            if (CanGather)
            {
                RemainingGatherSeconds = Math.Max(0f, RemainingGatherSeconds - deltaSeconds);
                if (RemainingGatherSeconds > 0f) return;
                if (Phase == FlowerMarketPhase.DayGathering) TryEndDay();
                else TryReturnToShop();
                return;
            }

            if (Phase != FlowerMarketPhase.Trading || !OrdersAccepted) return;
            RemainingTradingSeconds = Math.Max(0f, RemainingTradingSeconds - deltaSeconds);
            // At closing time, even an order that would finish on this tick remains unserved.
            if (RemainingTradingSeconds == 0f)
            {
                FinishEvening(closedByTimeLimit: true);
                return;
            }

            for (int slot = 0; slot < StoveCount; slot++)
            {
                ref var order = ref orders[slot];
                if (order.Stage != FlowerOrderStage.Cooking) continue;
                order.RemainingSeconds = Math.Max(0f, order.RemainingSeconds - deltaSeconds);
                if (order.RemainingSeconds == 0f) order.Stage = FlowerOrderStage.Ready;
            }
        }

        public bool TryServe(int slot)
        {
            if (Phase != FlowerMarketPhase.Trading || !ValidSlot(slot)) return false;
            var order = orders[slot];
            if (order.Stage != FlowerOrderStage.Ready || !TryGetRecipe(order.Recipe, out var info) ||
                TotalRevenue > int.MaxValue - info.Price) return false;

            Cash += info.Price;
            TotalRevenue += info.Price;
            EveningRevenue += info.Price;
            orders[slot] = default;
            FillStove(slot);
            return true;
        }

        public bool TryCloseShop()
        {
            if (Phase != FlowerMarketPhase.Trading) return false;
            FinishEvening(closedByTimeLimit: false);
            return true;
        }

        public bool TryStartNextDay()
        {
            if (Phase != FlowerMarketPhase.DaySummary) return false;
            BeginDay();
            return true;
        }

        private void FinishEvening(bool closedByTimeLimit)
        {
            foreach (var order in orders)
            {
                // Waiting orders reserve stock without debiting it.
                if (order.Stage != FlowerOrderStage.Cooking && order.Stage != FlowerOrderStage.Ready) continue;
                TryGetRecipe(order.Recipe, out var info);
                Refund(FlowerIngredient.Mushroom, info.Mushrooms);
                if (order.Fruit.HasValue) Refund(order.Fruit.Value, info.WeatherFruit);
            }
            ClearOrders();
            ClosedByTimeLimit = closedByTimeLimit;
            RemainingTradingSeconds = 0f;
            Phase = FlowerMarketPhase.DaySummary;
        }

        private void Refund(FlowerIngredient ingredient, int quantity)
        {
            stock[(int)ingredient] += quantity;
            refundedStock[(int)ingredient] += quantity;
        }

        public void Reset()
        {
            Day = Cash = TotalRevenue = 0;
            Phase = FlowerMarketPhase.Locked;
            RemainingGatherSeconds = 0f;
            KitchenUpgraded = false;
            Array.Clear(stock, 0, stock.Length);
            ClearOrders();
            ClearEvening();
        }

        private void FillStove(int slot)
        {
            var firstChoice = slot == 0 ? FlowerRecipe.MushroomBento : FlowerRecipe.FruitBento;
            var secondChoice = slot == 0 ? FlowerRecipe.FruitBento : FlowerRecipe.MushroomBento;
            if (!TryReserveOrder(slot, firstChoice)) TryReserveOrder(slot, secondChoice);
        }

        private bool TryReserveOrder(int slot, FlowerRecipe recipe)
        {
            if (!TryGetRecipe(recipe, out var info) ||
                GetStock(FlowerIngredient.Mushroom) - GetReserved(FlowerIngredient.Mushroom) < info.Mushrooms)
                return false;

            FlowerIngredient? fruit = null;
            if (info.WeatherFruit > 0)
            {
                foreach (var ingredient in FruitPriority)
                {
                    if (GetStock(ingredient) - GetReserved(ingredient) < info.WeatherFruit) continue;
                    fruit = ingredient;
                    break;
                }
                if (!fruit.HasValue) return false;
            }

            orders[slot] = new Order { Stage = FlowerOrderStage.Waiting, Recipe = recipe, Fruit = fruit };
            return true;
        }

        private void BeginDay()
        {
            ClearEvening();
            Day++;
            Phase = FlowerMarketPhase.DayGathering;
            RemainingGatherSeconds = dayGatherSeconds;
        }

        private void ClearOrders()
        {
            Array.Clear(orders, 0, orders.Length);
            OrdersAccepted = false;
        }

        private void ClearEvening()
        {
            choseNightExploration = ClosedByTimeLimit = false;
            RemainingTradingSeconds = 0f;
            EveningRevenue = 0;
            Array.Clear(refundedStock, 0, refundedStock.Length);
        }

        private static bool ValidSlot(int slot) => slot >= 0 && slot < StoveCount;

        private static void ValidateSeconds(float seconds, string parameter)
        {
            if (!(seconds > 0f) || float.IsInfinity(seconds))
                throw new ArgumentOutOfRangeException(parameter);
        }
    }
}
