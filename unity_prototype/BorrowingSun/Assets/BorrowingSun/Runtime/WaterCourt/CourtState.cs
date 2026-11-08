using System;

namespace BorrowingSun
{
    /// <summary>Deterministic water/transport rules; all scene animation observes this state.</summary>
    public sealed class CourtState
    {
        public const float LowHeight = 0f;
        public const float HighHeight = 3f;
        public const float WaterSpeed = 0.65f;
        public const int FinalStop = 7;

        public float WaterHeight { get; private set; }
        public bool CardInserted { get; private set; }
        public bool RainActive { get; private set; }
        public bool DrainOpen { get; private set; }
        public int CartStop { get; private set; }
        public int PendingCartStop { get; private set; } = -1;
        public bool CartInMotion => PendingCartStop >= 0;
        public bool IsLow => WaterHeight <= 0.001f;
        public bool IsHigh => WaterHeight >= HighHeight - 0.001f;
        public bool WaterInMotion => (RainActive && !IsHigh) || (DrainOpen && !IsLow);
        public bool Delivered => CartStop == FinalStop;
        public bool Completed => Delivered && !CardInserted;

        public void Reset()
        {
            WaterHeight = 0;
            CardInserted = false;
            RainActive = false;
            DrainOpen = false;
            CartStop = 0;
            PendingCartStop = -1;
        }

        public bool InsertCard()
        {
            if (CardInserted || CartInMotion) return false;
            CardInserted = true;
            return true;
        }

        public bool RemoveCard()
        {
            if (!CardInserted || CartInMotion) return false;
            CardInserted = false;
            RainActive = false;
            return true;
        }

        public bool ToggleRain()
        {
            if (!CardInserted || CartInMotion) return false;
            RainActive = !RainActive;
            // A mechanical interlock stops the two water controls fighting each other.
            if (RainActive) DrainOpen = false;
            return true;
        }

        public bool ToggleDrain()
        {
            if (CartInMotion) return false;
            DrainOpen = !DrainOpen;
            if (DrainOpen) RainActive = false;
            return true;
        }

        public void Advance(float seconds)
        {
            if (seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds) || CartInMotion) return;
            float direction = RainActive ? 1 : DrainOpen ? -1 : 0;
            WaterHeight = Math.Max(LowHeight, Math.Min(HighHeight, WaterHeight + direction * WaterSpeed * seconds));
        }

        public bool CanMoveCart(int direction, out string reason)
        {
            reason = "";
            if (direction != -1 && direction != 1) { reason = "每次只能移动到相邻停靠点。"; return false; }
            if (CartInMotion) { reason = "苗车正在移动。"; return false; }
            if (WaterInMotion) { reason = "浮架升降中，请等停稳再过接缝。"; return false; }
            int next = CartStop + direction;
            if (next < 0) { reason = "已经到入口了。"; return false; }
            if (next > FinalStop) { reason = "苗车已送到收货台。"; return false; }
            int edge = Math.Min(CartStop, next);
            if (edge == 0 && !IsLow) { reason = "入口与下层货台尚未齐平，需要低水位。"; return false; }
            if (edge == 1 && !IsHigh) { reason = "苗车到不了上岸货台，需要把浮架升高。"; return false; }
            if ((edge == 4 || edge == 5) && !IsLow)
            { reason = "横桥还在高处；让浮架下降，桥面才会与两岸齐平。"; return false; }
            return true;
        }

        public bool BeginCartMove(int direction)
        {
            if (!CanMoveCart(direction, out _)) return false;
            PendingCartStop = CartStop + direction;
            return true;
        }

        public bool CompleteCartMove()
        {
            if (!CartInMotion) return false;
            CartStop = PendingCartStop;
            PendingCartStop = -1;
            return true;
        }
    }
}
