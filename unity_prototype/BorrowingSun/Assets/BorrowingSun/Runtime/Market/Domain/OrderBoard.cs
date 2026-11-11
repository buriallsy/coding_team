using System;
using System.Collections.Generic;

namespace BorrowingSun.Market
{
    public sealed class OrderBoard
    {
        public const int WindowCount = 2;
        private sealed class Order
        {
            internal long Id;
            internal RecipeDefinition Recipe;
            internal OrderStage Stage;
            internal float Remaining;
        }

        private readonly Inventory inventory;
        private readonly IOrderSelector selector;
        private readonly Order[] orders = new Order[WindowCount];
        private readonly List<RecipeDefinition> menu = new List<RecipeDefinition>();
        private long nextId;

        internal OrderBoard(Inventory inventory, IOrderSelector selector)
        {
            this.inventory = inventory;
            this.selector = selector;
        }

        public OrderSnapshot GetOrder(int slot)
        {
            if (!ValidSlot(slot) || orders[slot] == null) return null;
            var order = orders[slot];
            return new OrderSnapshot(order.Id, order.Recipe, order.Stage, order.Remaining);
        }

        internal bool Open(IReadOnlyList<RecipeDefinition> selected)
        {
            Close();
            foreach (var recipe in selected) menu.Add(recipe);
            for (int slot = 0; slot < WindowCount; slot++) Refill(slot);
            return orders[0] != null || orders[1] != null;
        }

        internal void Refill(int slot)
        {
            if (!ValidSlot(slot) || orders[slot] != null || nextId == long.MaxValue) return;
            var available = new List<RecipeDefinition>();
            foreach (var recipe in menu)
                if (inventory.Covers(recipe.Ingredients)) available.Add(recipe);
            if (available.Count == 0) return;
            int chosen = selector.ChooseIndex(available.Count);
            // A rejected strategy result must not reserve or consume inventory.
            if (chosen < 0 || chosen >= available.Count) return;
            var selected = available[chosen];
            if (!inventory.TryReserve(selected.Ingredients)) return;
            orders[slot] = new Order { Id = ++nextId, Recipe = selected, Stage = OrderStage.Waiting };
        }

        internal MarketActionResult Cook(int slot, float multiplier)
        {
            if (!ValidSlot(slot)) return MarketActionResult.InvalidSlot;
            var order = orders[slot];
            if (order == null) return MarketActionResult.SlotEmpty;
            if (order.Stage != OrderStage.Waiting) return MarketActionResult.AlreadyCooking;
            if (!inventory.TryConsumeReservation(order.Recipe.Ingredients)) return MarketActionResult.InsufficientStock;
            order.Stage = OrderStage.Cooking;
            order.Remaining = order.Recipe.CookSeconds * multiplier;
            return MarketActionResult.Success;
        }

        internal void Tick(float deltaSeconds)
        {
            if (!MarketNumbers.Finite(deltaSeconds) || deltaSeconds <= 0f) return;
            foreach (var order in orders)
            {
                if (order == null || order.Stage != OrderStage.Cooking) continue;
                order.Remaining = Math.Max(0f, order.Remaining - deltaSeconds);
                if (order.Remaining == 0f) order.Stage = OrderStage.Ready;
            }
        }

        internal MarketActionResult CanServe(int slot, out int payment)
        {
            payment = 0;
            if (!ValidSlot(slot)) return MarketActionResult.InvalidSlot;
            var order = orders[slot];
            if (order == null) return MarketActionResult.SlotEmpty;
            if (order.Stage != OrderStage.Ready) return MarketActionResult.NotReady;
            payment = order.Recipe.Price;
            return MarketActionResult.Success;
        }

        internal void Complete(int slot) => orders[slot] = null;

        internal void Close()
        {
            for (int slot = 0; slot < WindowCount; slot++)
            {
                var order = orders[slot];
                if (order == null) continue;
                if (order.Stage == OrderStage.Waiting) inventory.Release(order.Recipe.Ingredients);
                else inventory.Refund(order.Recipe.Ingredients);
                orders[slot] = null;
            }
            menu.Clear();
        }

        internal void Reset()
        {
            Close();
            nextId = 0;
            selector.Reset();
        }

        private static bool ValidSlot(int slot) => slot >= 0 && slot < WindowCount;
    }
}
