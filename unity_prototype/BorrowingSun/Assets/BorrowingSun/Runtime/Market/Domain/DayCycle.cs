using System;

namespace BorrowingSun.Market
{
    public sealed class DayCycle
    {
        private readonly MarketSettings settings;
        private float businessDuration;
        public MarketPhase Phase { get; private set; }
        public int Day { get; private set; }
        public float RemainingSeconds { get; private set; }
        public bool IsNight => Phase != MarketPhase.Locked && Phase != MarketPhase.Day;
        public bool CanHarvest => Phase == MarketPhase.Day || Phase == MarketPhase.NightForage;

        internal DayCycle(MarketSettings settings) { this.settings = settings; }

        internal MarketActionResult Start()
        {
            if (Phase != MarketPhase.Locked) return MarketActionResult.AlreadyStarted;
            Day = 1;
            EnterDay();
            return MarketActionResult.Success;
        }

        internal void Tick(float deltaSeconds)
        {
            if (!MarketNumbers.Finite(deltaSeconds) || deltaSeconds <= 0f ||
                (Phase != MarketPhase.Day && Phase != MarketPhase.NightForage && Phase != MarketPhase.Business)) return;
            RemainingSeconds = Math.Max(0f, RemainingSeconds - deltaSeconds);
            if (RemainingSeconds > 0f) return;
            switch (Phase)
            {
                case MarketPhase.Day: Phase = MarketPhase.Dusk; break;
                case MarketPhase.NightForage: Phase = MarketPhase.Returning; break;
                case MarketPhase.Business: Phase = MarketPhase.Summary; break;
            }
        }

        internal MarketActionResult ChooseEvening(bool explore)
        {
            if (Phase != MarketPhase.Dusk) return MarketActionResult.InvalidPhase;
            businessDuration = explore ? settings.ShortBusinessSeconds : settings.BusinessSeconds;
            Phase = explore ? MarketPhase.NightForage : MarketPhase.Returning;
            RemainingSeconds = explore ? settings.NightSeconds : 0f;
            return MarketActionResult.Success;
        }

        internal MarketActionResult ReturnToShop()
        {
            if (Phase != MarketPhase.NightForage) return MarketActionResult.InvalidPhase;
            Phase = MarketPhase.Returning;
            RemainingSeconds = 0f;
            return MarketActionResult.Success;
        }

        internal MarketActionResult ArriveAtShop()
        {
            if (Phase != MarketPhase.Returning) return MarketActionResult.InvalidPhase;
            Phase = MarketPhase.Menu;
            return MarketActionResult.Success;
        }

        internal void OpenBusiness()
        {
            Phase = MarketPhase.Business;
            RemainingSeconds = businessDuration;
        }

        internal void CloseBusiness()
        {
            Phase = MarketPhase.Summary;
            RemainingSeconds = 0f;
        }

        internal MarketActionResult NextDay()
        {
            if (Phase != MarketPhase.Summary && Phase != MarketPhase.Menu) return MarketActionResult.InvalidPhase;
            if (Day == int.MaxValue) return MarketActionResult.CapacityExceeded;
            Day++;
            EnterDay();
            return MarketActionResult.Success;
        }

        internal void Reset()
        {
            Phase = MarketPhase.Locked;
            Day = 0;
            RemainingSeconds = businessDuration = 0f;
        }

        private void EnterDay()
        {
            Phase = MarketPhase.Day;
            RemainingSeconds = settings.DaySeconds;
        }
    }
}
