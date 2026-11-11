using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BorrowingSun.Market
{
    public enum MarketPhase { Locked, Day, Dusk, NightForage, Returning, Menu, Business, Summary }
    public enum IngredientId { Mushroom, RainFruit, MoonFruit, StormFruit, SunFruit }
    public enum ForageKind { MushroomPatch, WeatherTree }
    public enum OrderStage { Waiting, Cooking, Ready }
    public enum MarketActionResult
    {
        Success, InvalidPhase, AlreadyStarted, InvalidArgument, InsufficientStock,
        EmptyMenu, InvalidSlot, SlotEmpty, NotReady, AlreadyCooking,
        AlreadyUpgraded, InsufficientFunds, NotAvailable, CapacityExceeded
    }

    internal static class MarketNumbers
    {
        internal const int IngredientCount = 5;
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static bool IngredientValid(IngredientId ingredient) =>
            (int)ingredient >= 0 && (int)ingredient < IngredientCount;
        internal static void Positive(float value, string name)
        {
            if (!Finite(value) || value <= 0f) throw new ArgumentOutOfRangeException(name);
        }
    }

    public sealed class MarketSettings
    {
        public float DaySeconds { get; }
        public float NightSeconds { get; }
        public float BusinessSeconds { get; }
        public float ShortBusinessSeconds { get; }
        public int UpgradeCost { get; }
        public float UpgradeTimeMultiplier { get; }

        public MarketSettings(float daySeconds = 180f, float nightSeconds = 90f,
            float businessSeconds = 120f, float shortBusinessSeconds = 60f,
            int upgradeCost = 36, float upgradeTimeMultiplier = .7f)
        {
            MarketNumbers.Positive(daySeconds, nameof(daySeconds));
            MarketNumbers.Positive(nightSeconds, nameof(nightSeconds));
            MarketNumbers.Positive(businessSeconds, nameof(businessSeconds));
            MarketNumbers.Positive(shortBusinessSeconds, nameof(shortBusinessSeconds));
            if (upgradeCost <= 0) throw new ArgumentOutOfRangeException(nameof(upgradeCost));
            if (!MarketNumbers.Finite(upgradeTimeMultiplier) || upgradeTimeMultiplier <= 0f || upgradeTimeMultiplier >= 1f)
                throw new ArgumentOutOfRangeException(nameof(upgradeTimeMultiplier));
            DaySeconds = daySeconds;
            NightSeconds = nightSeconds;
            BusinessSeconds = businessSeconds;
            ShortBusinessSeconds = shortBusinessSeconds;
            UpgradeCost = upgradeCost;
            UpgradeTimeMultiplier = upgradeTimeMultiplier;
        }
    }

    public readonly struct IngredientAmount
    {
        public IngredientId Ingredient { get; }
        public int Quantity { get; }

        public IngredientAmount(IngredientId ingredient, int quantity)
        {
            if (!MarketNumbers.IngredientValid(ingredient)) throw new ArgumentOutOfRangeException(nameof(ingredient));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            Ingredient = ingredient;
            Quantity = quantity;
        }
    }

    public sealed class RecipeDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public IReadOnlyList<IngredientAmount> Ingredients { get; }
        public float CookSeconds { get; }
        public int Price { get; }

        public RecipeDefinition(string id, string name, float cookSeconds, int price,
            IEnumerable<IngredientAmount> ingredients)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Recipe id is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Recipe name is required.", nameof(name));
            MarketNumbers.Positive(cookSeconds, nameof(cookSeconds));
            if (price <= 0) throw new ArgumentOutOfRangeException(nameof(price));
            if (ingredients == null) throw new ArgumentNullException(nameof(ingredients));
            var totals = new int[MarketNumbers.IngredientCount];
            foreach (var item in ingredients)
            {
                if (!MarketNumbers.IngredientValid(item.Ingredient) || item.Quantity <= 0)
                    throw new ArgumentException("Recipe ingredients must be positive valid values.", nameof(ingredients));
                int index = (int)item.Ingredient;
                if (totals[index] > int.MaxValue - item.Quantity)
                    throw new ArgumentOutOfRangeException(nameof(ingredients));
                totals[index] += item.Quantity;
            }
            var normalized = new List<IngredientAmount>();
            for (int i = 0; i < totals.Length; i++)
                if (totals[i] > 0) normalized.Add(new IngredientAmount((IngredientId)i, totals[i]));
            if (normalized.Count == 0) throw new ArgumentException("Recipe must consume ingredients.", nameof(ingredients));
            Id = id;
            Name = name;
            CookSeconds = cookSeconds;
            Price = price;
            Ingredients = new ReadOnlyCollection<IngredientAmount>(normalized);
        }
    }

    public sealed class OrderSnapshot
    {
        public long Id { get; }
        public RecipeDefinition Recipe { get; }
        public OrderStage Stage { get; }
        public float RemainingSeconds { get; }

        internal OrderSnapshot(long id, RecipeDefinition recipe, OrderStage stage, float remainingSeconds)
        {
            Id = id;
            Recipe = recipe;
            Stage = stage;
            RemainingSeconds = remainingSeconds;
        }
    }
}
