using System;

namespace BorrowingSun.Market
{
    public readonly struct ForageEnvironment
    {
        public bool IsNight { get; }
        public bool IsRaining { get; }
        public bool HasSunlight { get; }

        public ForageEnvironment(bool night, bool rain, bool sunlit)
        {
            IsNight = night;
            IsRaining = rain;
            HasSunlight = !night && sunlit;
        }
    }

    public sealed class ForageState
    {
        private readonly ForageKind kind;
        private readonly float regrowSeconds;
        private readonly float initialSeconds;
        public IngredientId? CurrentIngredient { get; private set; }
        public float RemainingSeconds { get; private set; }
        public bool IsMature => RemainingSeconds <= 0f;

        public ForageState(ForageKind kind, float regrowSeconds = 8f, float initialSeconds = 0f)
        {
            if (kind != ForageKind.MushroomPatch && kind != ForageKind.WeatherTree)
                throw new ArgumentOutOfRangeException(nameof(kind));
            MarketNumbers.Positive(regrowSeconds, nameof(regrowSeconds));
            if (!MarketNumbers.Finite(initialSeconds) || initialSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(initialSeconds));
            this.kind = kind;
            this.regrowSeconds = regrowSeconds;
            this.initialSeconds = initialSeconds;
            RemainingSeconds = initialSeconds;
        }

        public void Tick(float deltaSeconds, ForageEnvironment environment)
        {
            if (!MarketNumbers.Finite(deltaSeconds) || deltaSeconds < 0f) return;
            RemainingSeconds = Math.Max(0f, RemainingSeconds - deltaSeconds);
            if (!IsMature) { CurrentIngredient = null; return; }
            if (kind == ForageKind.MushroomPatch) CurrentIngredient = IngredientId.Mushroom;
            else if (environment.IsNight) CurrentIngredient = environment.IsRaining ? IngredientId.StormFruit : IngredientId.MoonFruit;
            else if (environment.IsRaining) CurrentIngredient = IngredientId.RainFruit;
            else CurrentIngredient = environment.HasSunlight ? IngredientId.SunFruit : (IngredientId?)null;
        }

        public bool TryHarvest(out IngredientId ingredient)
        {
            ingredient = default;
            if (!CurrentIngredient.HasValue) return false;
            ingredient = CurrentIngredient.Value;
            CurrentIngredient = null;
            RemainingSeconds = regrowSeconds;
            return true;
        }

        public void Reset()
        {
            CurrentIngredient = null;
            RemainingSeconds = initialSeconds;
        }
    }
}
