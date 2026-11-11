using System;
using System.Collections.Generic;

namespace BorrowingSun.Market
{
    // Total stock includes reserved raw ingredients until cooking consumes them.
    // Only the domain coordinator and order board may mutate the counts.
    public sealed class Inventory
    {
        private readonly int[] stock = new int[MarketNumbers.IngredientCount];
        private readonly int[] reserved = new int[MarketNumbers.IngredientCount];

        public int Count(IngredientId ingredient) => MarketNumbers.IngredientValid(ingredient) ? stock[(int)ingredient] : 0;
        public int Reserved(IngredientId ingredient) => MarketNumbers.IngredientValid(ingredient) ? reserved[(int)ingredient] : 0;
        public int Available(IngredientId ingredient) => Count(ingredient) - Reserved(ingredient);

        internal bool CanAdd(IngredientId ingredient, int quantity) =>
            MarketNumbers.IngredientValid(ingredient) && quantity > 0 && Count(ingredient) <= int.MaxValue - quantity;

        internal bool TryAdd(IngredientId ingredient, int quantity)
        {
            if (!CanAdd(ingredient, quantity)) return false;
            stock[(int)ingredient] += quantity;
            return true;
        }

        internal bool Covers(IReadOnlyList<IngredientAmount> ingredients)
        {
            foreach (var item in ingredients)
                if (Available(item.Ingredient) < item.Quantity) return false;
            return true;
        }

        internal bool TryReserve(IReadOnlyList<IngredientAmount> ingredients)
        {
            if (!Covers(ingredients)) return false;
            foreach (var item in ingredients) reserved[(int)item.Ingredient] += item.Quantity;
            return true;
        }

        internal bool TryConsumeReservation(IReadOnlyList<IngredientAmount> ingredients)
        {
            foreach (var item in ingredients)
                if (reserved[(int)item.Ingredient] < item.Quantity || stock[(int)item.Ingredient] < item.Quantity) return false;
            foreach (var item in ingredients)
            {
                reserved[(int)item.Ingredient] -= item.Quantity;
                stock[(int)item.Ingredient] -= item.Quantity;
            }
            return true;
        }

        internal void Release(IReadOnlyList<IngredientAmount> ingredients)
        {
            foreach (var item in ingredients) reserved[(int)item.Ingredient] -= item.Quantity;
        }

        internal void Refund(IReadOnlyList<IngredientAmount> ingredients)
        {
            foreach (var item in ingredients) stock[(int)item.Ingredient] += item.Quantity;
        }

        internal void Reset()
        {
            Array.Clear(stock, 0, stock.Length);
            Array.Clear(reserved, 0, reserved.Length);
        }
    }
}
