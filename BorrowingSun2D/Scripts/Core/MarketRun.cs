using System;
using System.Collections.Generic;
using System.Linq;

namespace BorrowingSun2D.Core;

internal enum MarketPhase { Tutorial, Day, Dusk, NightForage, Menu, Business }

internal sealed class MarketRun
{
    internal const int DaySeconds = 180;
    internal const int BusinessSeconds = 120;
    internal const int KitchenUpgradeCost = 36;

    private int openingSeconds;
    internal Inventory Bag { get; } = new();
    internal TakeawayShop Shop { get; }
    internal MarketPhase Phase { get; private set; }
    internal int Day { get; private set; }
    internal float Remaining { get; private set; }
    internal int Balance { get; private set; }
    internal int TotalRevenue { get; private set; }
    internal int TonightRevenue { get; private set; }
    internal int LastNightRevenue { get; private set; }
    internal bool IsExploring => Phase is MarketPhase.Day or MarketPhase.NightForage;
    internal bool IsNight => Phase is MarketPhase.Dusk or MarketPhase.NightForage or MarketPhase.Menu
        or MarketPhase.Business;

    internal MarketRun(Random random) => Shop = new TakeawayShop(Bag, random);

    internal bool Start()
    {
        if (Phase != MarketPhase.Tutorial) return false;
        NextDay();
        return true;
    }

    internal void Tick(float delta)
    {
        if (delta <= 0f || Phase is not (MarketPhase.Day or MarketPhase.NightForage or MarketPhase.Business))
            return;

        if (Phase == MarketPhase.Business) Shop.Tick(Math.Min(delta, Remaining));
        Remaining = Math.Max(0f, Remaining - delta);
        if (Remaining > 0f) return;

        switch (Phase)
        {
            case MarketPhase.Day: Phase = MarketPhase.Dusk; break;
            case MarketPhase.NightForage: OpenMenu(); break;
            case MarketPhase.Business: NextDay(); break;
        }
    }

    internal bool ChooseNight(bool explore)
    {
        if (Phase != MarketPhase.Dusk) return false;
        openingSeconds = explore ? BusinessSeconds / 2 : BusinessSeconds;
        Phase = explore ? MarketPhase.NightForage : MarketPhase.Menu;
        Remaining = explore ? DaySeconds / 2 : 0;
        return true;
    }

    internal bool OpenMenu()
    {
        if (Phase != MarketPhase.NightForage) return false;
        Remaining = 0;
        Phase = MarketPhase.Menu;
        return true;
    }

    internal bool OpenShop(IEnumerable<string> recipeIds)
    {
        if (Phase != MarketPhase.Menu) return false;
        var ids = new HashSet<string>(recipeIds, StringComparer.Ordinal);
        Recipe[] selected = RecipeBook.All.Where(r => ids.Contains(r.Id) && Bag.Covers(r.Ingredients)).ToArray();
        if (selected.Length == 0 || selected.Length != ids.Count) return false;
        Shop.Open(selected);
        Phase = MarketPhase.Business;
        Remaining = openingSeconds;
        return true;
    }

    internal bool TryCook(int slot) => Phase == MarketPhase.Business && Shop.TryCook(slot);

    internal bool TryServe(int slot)
    {
        if (Phase != MarketPhase.Business) return false;
        int payment = Shop.TryServe(slot);
        if (payment == 0) return false;

        Balance += payment;
        TotalRevenue += payment;
        TonightRevenue += payment;
        Shop.Refill(slot);
        return true;
    }

    internal bool TryUpgradeKitchen()
    {
        if (Phase != MarketPhase.Menu || Shop.KitchenUpgraded || Balance < KitchenUpgradeCost) return false;
        Balance -= KitchenUpgradeCost;
        Shop.UpgradeKitchen();
        return true;
    }

    internal bool EndEvening()
    {
        if (Phase is not (MarketPhase.Menu or MarketPhase.Business)) return false;
        NextDay();
        return true;
    }

    private void NextDay()
    {
        Shop.Close();
        LastNightRevenue = TonightRevenue;
        TonightRevenue = 0;
        Day++;
        Phase = MarketPhase.Day;
        Remaining = DaySeconds;
    }
}
