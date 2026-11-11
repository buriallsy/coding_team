using System;
using System.Collections.Generic;
using System.Linq;

namespace BorrowingSun2D.Core;

internal readonly record struct Ingredient(FoodKind Food, int Quantity);
internal sealed record Recipe(string Id, string Name, int CookSeconds, int Price, IReadOnlyList<Ingredient> Ingredients);

internal static class RecipeBook
{
    internal static IReadOnlyList<Recipe> All { get; } = Array.AsReadOnly(new Recipe[]
    {
        new("mushroom", "菌菇便当", 3, 12, new Ingredient[] { new(FoodKind.Mushroom, 2) }),
        new("skewer", "菌菇肉串", 5, 20, new Ingredient[] { new(FoodKind.GameMeat, 1), new(FoodKind.Mushroom, 1) }),
        new("sun-moon", "昼夜果盘", 4, 26, new Ingredient[] { new(FoodKind.SunFruit, 1), new(FoodKind.MoonFruit, 1) }),
        new("rain", "雨果菌菇盒", 5, 22, new Ingredient[] { new(FoodKind.RainFruit, 1), new(FoodKind.Mushroom, 1) }),
        new("moon", "月果菌菇盒", 5, 24, new Ingredient[] { new(FoodKind.MoonFruit, 1), new(FoodKind.Mushroom, 1) }),
        new("storm", "雨夜双拼", 6, 28, new Ingredient[] { new(FoodKind.StormFruit, 1), new(FoodKind.GameMeat, 1) }),
    });
}

internal sealed class Inventory
{
    private readonly int[] counts = new int[Enum.GetValues<FoodKind>().Length];
    internal int Count(FoodKind food) => counts[(int)food];
    internal void Add(FoodKind food, int quantity = 1) => counts[(int)food] += quantity;
    internal bool Covers(IReadOnlyList<Ingredient> ingredients) =>
        ingredients.All(i => Count(i.Food) >= i.Quantity);

    internal bool TryWithdraw(IReadOnlyList<Ingredient> ingredients)
    {
        if (!Covers(ingredients)) return false;
        foreach (Ingredient item in ingredients) Add(item.Food, -item.Quantity);
        return true;
    }

}

internal enum OrderState { Waiting, Cooking, Ready }

internal sealed class CustomerOrder(Recipe recipe)
{
    internal Recipe Recipe { get; } = recipe;
    internal OrderState State { get; set; }
    internal float CookRemaining { get; set; }
}

internal sealed class TakeawayShop(Inventory inventory, Random random)
{
    internal const int WindowCount = 2;
    internal const int UpgradeTimeReductionPercent = 30;
    private readonly CustomerOrder?[] orders = new CustomerOrder?[WindowCount];
    private IReadOnlyList<Recipe> menu = Array.Empty<Recipe>();

    internal CustomerOrder? OrderAt(int slot) => (uint)slot < WindowCount ? orders[slot] : null;
    internal bool KitchenUpgraded { get; private set; }
    internal void UpgradeKitchen() => KitchenUpgraded = true;
    internal float CookDuration(Recipe recipe) => KitchenUpgraded
        ? recipe.CookSeconds * (100 - UpgradeTimeReductionPercent) / 100f : recipe.CookSeconds;

    internal void Open(IReadOnlyList<Recipe> selected)
    {
        menu = selected;
        for (int slot = 0; slot < orders.Length; slot++) Refill(slot);
    }

    internal void Refill(int slot)
    {
        if (orders[slot] != null) return;
        Recipe[] available = menu.Where(recipe =>
            recipe.Ingredients.All(item => Available(item.Food) >= item.Quantity)).ToArray();
        if (available.Length > 0) orders[slot] = new CustomerOrder(available[random.Next(available.Length)]);
    }

    private int Available(FoodKind food)
    {
        int available = inventory.Count(food);
        foreach (CustomerOrder? order in orders)
        {
            if (order?.State != OrderState.Waiting) continue;
            foreach (Ingredient item in order.Recipe.Ingredients)
                if (item.Food == food) available -= item.Quantity;
        }
        return available;
    }

    internal bool TryCook(int slot)
    {
        if (OrderAt(slot) is not { State: OrderState.Waiting } order
            || !inventory.TryWithdraw(order.Recipe.Ingredients)) return false;

        order.State = OrderState.Cooking;
        order.CookRemaining = CookDuration(order.Recipe);
        return true;
    }

    internal void Tick(float delta)
    {
        foreach (CustomerOrder? order in orders)
        {
            if (order is not { State: OrderState.Cooking }) continue;
            order.CookRemaining = Math.Max(0f, order.CookRemaining - delta);
            if (order.CookRemaining == 0f) order.State = OrderState.Ready;
        }
    }

    internal int TryServe(int slot)
    {
        if (OrderAt(slot) is not { State: OrderState.Ready } order) return 0;
        orders[slot] = null;
        return order.Recipe.Price;
    }

    internal void Close()
    {
        foreach (CustomerOrder? unfinished in orders)
        {
            if (unfinished is not { State: OrderState.Cooking or OrderState.Ready }) continue;
            foreach (Ingredient item in unfinished.Recipe.Ingredients) inventory.Add(item.Food, item.Quantity);
        }
        Array.Clear(orders);
        menu = Array.Empty<Recipe>();
    }
}
