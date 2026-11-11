namespace BorrowingSun.Market
{
    public sealed class Wallet
    {
        public int Cash { get; private set; }
        public int TotalRevenue { get; private set; }
        public int TonightRevenue { get; private set; }
        public int LastNightRevenue { get; private set; }
        public bool KitchenUpgraded { get; private set; }

        internal bool CanCredit(int payment) => payment > 0 && Cash <= int.MaxValue - payment &&
            TotalRevenue <= int.MaxValue - payment && TonightRevenue <= int.MaxValue - payment;

        internal void Credit(int payment)
        {
            Cash += payment;
            TotalRevenue += payment;
            TonightRevenue += payment;
        }

        internal MarketActionResult Upgrade(int cost)
        {
            if (KitchenUpgraded) return MarketActionResult.AlreadyUpgraded;
            if (Cash < cost) return MarketActionResult.InsufficientFunds;
            Cash -= cost;
            KitchenUpgraded = true;
            return MarketActionResult.Success;
        }

        internal void FinishEvening() => LastNightRevenue = TonightRevenue;
        internal void StartDay() => TonightRevenue = 0;
        internal void Reset()
        {
            Cash = TotalRevenue = TonightRevenue = LastNightRevenue = 0;
            KitchenUpgraded = false;
        }
    }
}
