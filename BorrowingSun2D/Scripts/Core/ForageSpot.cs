using System;

namespace BorrowingSun2D.Core;

internal enum FoodKind { Mushroom, GameMeat, SunFruit, RainFruit, MoonFruit, StormFruit }
internal enum ForageKind { MushroomPatch, WildPatch, Tree }

internal readonly record struct ForageEnvironment(bool Night, bool Rain, bool Lit);

internal sealed class ForageSpot(ForageKind kind, float initialDelay)
{
    internal const int MinRefreshSeconds = 8;
    internal const int MaxRefreshSeconds = 14;

    private float remaining = initialDelay;
    private bool mature;
    internal FoodKind? Produce { get; private set; }

    internal void Tick(float delta, ForageEnvironment environment, Random random)
    {
        if (!mature)
        {
            remaining -= delta;
            if (remaining > 0f) return;
            mature = true;
            if (kind != ForageKind.Tree)
            {
                int mushroomChance = environment.Night || environment.Rain ? 75 : 50;
                Produce = kind == ForageKind.MushroomPatch || random.Next(100) < mushroomChance
                    ? FoodKind.Mushroom : FoodKind.GameMeat;
            }
        }

        if (kind == ForageKind.Tree)
        {
            Produce = environment switch
            {
                { Night: true, Rain: true } => FoodKind.StormFruit,
                { Night: true } => FoodKind.MoonFruit,
                { Rain: true } => FoodKind.RainFruit,
                { Lit: true } => FoodKind.SunFruit,
                _ => null,
            };
        }
    }

    internal FoodKind? Harvest(Random random)
    {
        if (Produce is not FoodKind food) return null;
        Produce = null;
        mature = false;
        remaining = random.Next(MinRefreshSeconds, MaxRefreshSeconds + 1);
        return food;
    }
}
