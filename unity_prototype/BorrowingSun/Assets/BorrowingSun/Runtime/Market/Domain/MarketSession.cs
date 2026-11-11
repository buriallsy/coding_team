using System;
using System.Collections.Generic;

namespace BorrowingSun.Market
{
    // Application coordinator: world location, weather commands and Unity objects stay outside this class.
    public sealed class MarketSession
    {
        private readonly MarketSettings settings;
        private readonly IRecipeCatalog recipes;
        private readonly Inventory inventory;
        private readonly OrderBoard orders;
        private readonly DayCycle cycle;
        private readonly Wallet wallet;

        public MarketPhase Phase => cycle.Phase;
        public int Day => cycle.Day;
        public float RemainingSeconds => cycle.RemainingSeconds;
        public bool IsNight => cycle.IsNight;
        public bool CanHarvest => cycle.CanHarvest;
        public int Cash => wallet.Cash;
        public int TotalRevenue => wallet.TotalRevenue;
        public int TonightRevenue => wallet.TonightRevenue;
        public int LastNightRevenue => wallet.LastNightRevenue;
        public bool KitchenUpgraded => wallet.KitchenUpgraded;
        public IReadOnlyList<RecipeDefinition> Recipes => recipes.All;

        public MarketSession(MarketSettings settings = null, IRecipeCatalog recipes = null, IOrderSelector selector = null)
        {
            this.settings = settings ?? new MarketSettings();
            this.recipes = recipes ?? RecipeCatalog.CreateDefault();
            inventory = new Inventory();
            wallet = new Wallet();
            cycle = new DayCycle(this.settings);
            orders = new OrderBoard(inventory, selector ?? new RoundRobinOrderSelector());
        }

        public int Stock(IngredientId ingredient) => inventory.Count(ingredient);
        public int Reserved(IngredientId ingredient) => inventory.Reserved(ingredient);
        public OrderSnapshot GetOrder(int slot) => orders.GetOrder(slot);
        public bool CanMake(string recipeId) => recipes.TryGet(recipeId, out var recipe) && inventory.Covers(recipe.Ingredients);

        public MarketActionResult TryStart() => cycle.Start();
        public MarketActionResult TryChooseEvening(bool explore) => cycle.ChooseEvening(explore);
        public MarketActionResult TryReturnToShop() => cycle.ReturnToShop();
        public MarketActionResult TryArriveAtShop() => cycle.ArriveAtShop();

        public MarketActionResult TryHarvest(IngredientId ingredient, int quantity = 1)
        {
            if (!CanHarvest) return MarketActionResult.InvalidPhase;
            if (!MarketNumbers.IngredientValid(ingredient) || quantity <= 0) return MarketActionResult.InvalidArgument;
            return inventory.TryAdd(ingredient, quantity) ? MarketActionResult.Success : MarketActionResult.CapacityExceeded;
        }

        public MarketActionResult TryHarvest(ForageState spot)
        {
            if (!CanHarvest) return MarketActionResult.InvalidPhase;
            if (spot == null) return MarketActionResult.InvalidArgument;
            if (!spot.CurrentIngredient.HasValue) return MarketActionResult.NotAvailable;
            if (!inventory.CanAdd(spot.CurrentIngredient.Value, 1)) return MarketActionResult.CapacityExceeded;
            if (!spot.TryHarvest(out var ingredient)) return MarketActionResult.NotAvailable;
            inventory.TryAdd(ingredient, 1);
            return MarketActionResult.Success;
        }

        public MarketActionResult TryOpenBusiness(IEnumerable<string> recipeIds)
        {
            if (Phase != MarketPhase.Menu) return MarketActionResult.InvalidPhase;
            if (recipeIds == null) return MarketActionResult.InvalidArgument;
            var selected = new List<RecipeDefinition>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in recipeIds)
            {
                if (id == null || !seen.Add(id) || !recipes.TryGet(id, out var recipe)) return MarketActionResult.InvalidArgument;
                selected.Add(recipe);
            }
            if (selected.Count == 0) return MarketActionResult.EmptyMenu;
            if (!selected.Exists(recipe => inventory.Covers(recipe.Ingredients))) return MarketActionResult.InsufficientStock;
            if (!orders.Open(selected)) { orders.Close(); return MarketActionResult.NotAvailable; }
            cycle.OpenBusiness();
            return MarketActionResult.Success;
        }

        public MarketActionResult TryCook(int slot)
        {
            if (Phase != MarketPhase.Business) return MarketActionResult.InvalidPhase;
            return orders.Cook(slot, KitchenUpgraded ? settings.UpgradeTimeMultiplier : 1f);
        }

        public MarketActionResult TryServe(int slot)
        {
            if (Phase != MarketPhase.Business) return MarketActionResult.InvalidPhase;
            var result = orders.CanServe(slot, out int payment);
            if (result != MarketActionResult.Success) return result;
            if (!wallet.CanCredit(payment)) return MarketActionResult.CapacityExceeded;
            orders.Complete(slot);
            wallet.Credit(payment);
            orders.Refill(slot);
            return MarketActionResult.Success;
        }

        public MarketActionResult TryClose()
        {
            if (Phase != MarketPhase.Business) return MarketActionResult.InvalidPhase;
            orders.Close();
            wallet.FinishEvening();
            cycle.CloseBusiness();
            return MarketActionResult.Success;
        }

        public MarketActionResult TryNextDay()
        {
            var previous = Phase;
            var result = cycle.NextDay();
            if (result != MarketActionResult.Success) return result;
            orders.Close();
            if (previous == MarketPhase.Menu) wallet.FinishEvening();
            wallet.StartDay();
            return MarketActionResult.Success;
        }

        public MarketActionResult TryUpgradeKitchen()
        {
            if (Phase != MarketPhase.Menu) return MarketActionResult.InvalidPhase;
            return wallet.Upgrade(settings.UpgradeCost);
        }

        public void Tick(float deltaSeconds)
        {
            if (!MarketNumbers.Finite(deltaSeconds) || deltaSeconds <= 0f) return;
            bool wasBusiness = Phase == MarketPhase.Business;
            if (wasBusiness) orders.Tick(Math.Min(deltaSeconds, RemainingSeconds));
            cycle.Tick(deltaSeconds);
            if (wasBusiness && Phase == MarketPhase.Summary)
            {
                orders.Close();
                wallet.FinishEvening();
            }
        }

        public void Reset()
        {
            orders.Reset();
            inventory.Reset();
            wallet.Reset();
            cycle.Reset();
        }
    }
}
