using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BorrowingSun.Market
{
    // The recipe source is replaceable without changing stock, orders or the session.
    public interface IRecipeCatalog
    {
        IReadOnlyList<RecipeDefinition> All { get; }
        bool TryGet(string id, out RecipeDefinition recipe);
    }

    public sealed class RecipeCatalog : IRecipeCatalog
    {
        private readonly Dictionary<string, RecipeDefinition> byId;
        public IReadOnlyList<RecipeDefinition> All { get; }

        public RecipeCatalog(IEnumerable<RecipeDefinition> recipes)
        {
            if (recipes == null) throw new ArgumentNullException(nameof(recipes));
            byId = new Dictionary<string, RecipeDefinition>(StringComparer.Ordinal);
            var list = new List<RecipeDefinition>();
            foreach (var recipe in recipes)
            {
                if (recipe == null || byId.ContainsKey(recipe.Id))
                    throw new ArgumentException("Recipes must be non-null and have unique ids.", nameof(recipes));
                byId.Add(recipe.Id, recipe);
                list.Add(recipe);
            }
            if (list.Count == 0) throw new ArgumentException("At least one recipe is required.", nameof(recipes));
            All = new ReadOnlyCollection<RecipeDefinition>(list);
        }

        public bool TryGet(string id, out RecipeDefinition recipe)
        {
            recipe = null;
            return id != null && byId.TryGetValue(id, out recipe);
        }

        public static RecipeCatalog CreateDefault() => new RecipeCatalog(new[]
        {
            new RecipeDefinition("mushroom", "菌菇便当", 3f, 12,
                new[] { new IngredientAmount(IngredientId.Mushroom, 2) }),
            new RecipeDefinition("sun", "晴果菌菇盒", 4f, 20,
                new[] { new IngredientAmount(IngredientId.Mushroom, 1), new IngredientAmount(IngredientId.SunFruit, 1) }),
            new RecipeDefinition("rain", "雨果菌菇盒", 4f, 22,
                new[] { new IngredientAmount(IngredientId.Mushroom, 1), new IngredientAmount(IngredientId.RainFruit, 1) }),
            new RecipeDefinition("moon", "月果菌菇盒", 4f, 24,
                new[] { new IngredientAmount(IngredientId.Mushroom, 1), new IngredientAmount(IngredientId.MoonFruit, 1) }),
            new RecipeDefinition("storm", "雨夜双拼", 5f, 28,
                new[] { new IngredientAmount(IngredientId.Mushroom, 1), new IngredientAmount(IngredientId.StormFruit, 1) })
        });
    }

    // This small strategy owns customer selection only, never inventory or prices.
    public interface IOrderSelector
    {
        int ChooseIndex(int candidateCount);
        void Reset();
    }

    public sealed class RoundRobinOrderSelector : IOrderSelector
    {
        private int cursor;
        public int ChooseIndex(int candidateCount)
        {
            if (candidateCount <= 0) throw new ArgumentOutOfRangeException(nameof(candidateCount));
            int chosen = cursor % candidateCount;
            cursor = cursor == int.MaxValue ? 0 : cursor + 1;
            return chosen;
        }
        public void Reset() => cursor = 0;
    }
}
