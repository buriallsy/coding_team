using System;

namespace BorrowingSun
{
    /// <summary>One rain card, two independent stored water levels, and a shared cargo route.</summary>
    public sealed class BorrowedRainState
    {
        public const float HighHeight = 3f;
        public const float WaterSpeed = 0.65f;
        public const int FinalStop = 5;
        public const int Hand = -1;
        public float WaterA { get; private set; }
        public float WaterB { get; private set; }
        public int CardStation { get; private set; } = Hand;
        public bool RainActive { get; private set; }
        public bool DrainAOpen { get; private set; }
        public bool DrainBOpen { get; private set; }
        public int CartStop { get; private set; }
        public int PendingCartStop { get; private set; } = -1;
        public bool CartInMotion => PendingCartStop >= 0;
        public bool IsALow => WaterA <= 0.001f;
        public bool IsAHigh => WaterA >= HighHeight - 0.001f;
        public bool IsBLow => WaterB <= 0.001f;
        public bool IsBHigh => WaterB >= HighHeight - 0.001f;
        public bool IsRemoteRain => RainActive && CardStation == 1;
        // The duct has a docking lip: only the upper mechanical stop makes a sealed connection.
        public bool IsGutterAligned => IsAHigh && !AMoving;
        public bool RainToB => IsRemoteRain && IsGutterAligned;
        public bool RainToWaste => IsRemoteRain && !IsGutterAligned;
        public bool AMoving => (RainActive && CardStation == 0 && !IsAHigh) || (DrainAOpen && !IsALow);
        public bool BMoving => (RainToB && !IsBHigh) || (DrainBOpen && !IsBLow);
        public bool WaterInMotion => AMoving || BMoving;
        public bool Delivered => CartStop == FinalStop;
        public bool CargoRouteConnected => IsALow && IsBHigh && !WaterInMotion;
        public bool Completed => Delivered && CardStation == Hand && CargoRouteConnected;

        public void Reset()
        {
            WaterA = WaterB = 0;
            CardStation = Hand;
            RainActive = DrainAOpen = DrainBOpen = false;
            CartStop = 0;
            PendingCartStop = -1;
        }

        public bool InsertCard(int station)
        {
            if (!ValidStation(station) || CardStation != Hand || CartInMotion) return false;
            CardStation = station;
            return true;
        }

        public bool RemoveCard(int station)
        {
            if (!ValidStation(station) || CardStation != station || CartInMotion) return false;
            CardStation = Hand;
            RainActive = false;
            return true;
        }

        public bool ToggleRain(int station)
        {
            if (!ValidStation(station) || CardStation != station || CartInMotion) return false;
            RainActive = !RainActive;
            // Only the basin receiving this source is interlocked. The other basin remains independent.
            if (RainActive && station == 0) DrainAOpen = false;
            if (RainActive && station == 1) DrainBOpen = false;
            return true;
        }

        public bool ToggleDrain(int basin)
        {
            if (!ValidStation(basin) || CartInMotion) return false;
            if (basin == 0) DrainAOpen = !DrainAOpen;
            else DrainBOpen = !DrainBOpen;
            bool opened = basin == 0 ? DrainAOpen : DrainBOpen;
            if (opened && CardStation == basin) RainActive = false;
            return true;
        }

        public void Advance(float seconds)
        {
            if (seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds) || CartInMotion) return;
            // Resolve the inlet before the tick. A departing upper dock cannot feed B during a large tick.
            bool feedB = RainToB;
            float rateA = RainActive && CardStation == 0 ? 1 : DrainAOpen ? -1 : 0;
            float rateB = feedB ? 1 : DrainBOpen ? -1 : 0;
            WaterA = ClampWater(WaterA + rateA * WaterSpeed * seconds);
            WaterB = ClampWater(WaterB + rateB * WaterSpeed * seconds);
        }

        public bool CanMoveCart(int direction, out string reason)
        {
            reason = "";
            if (direction != -1 && direction != 1) { reason = "苗车只能前进或倒退一站。"; return false; }
            if (CartInMotion) { reason = "苗车正在过接缝，请等它停稳。"; return false; }
            int next = CartStop + direction;
            if (next < 0) { reason = "已经回到西侧起点。"; return false; }
            if (next > FinalStop) { reason = "苗车已停在东侧收货位。"; return false; }
            if (WaterInMotion) { reason = "升降设备尚在运动，停稳后再推车。"; return false; }
            int edge = Math.Min(CartStop, next);
            if (edge <= 1 && !IsALow)
            { reason = "廊桥与货台有高差，当前不能跨过接缝。"; return false; }
            if ((edge == 2 || edge == 3) && !IsBHigh)
            { reason = "对岸浮台尚未与货路齐平。"; return false; }
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

        private static bool ValidStation(int value) => value == 0 || value == 1;
        private static float ClampWater(float value)
        {
            float clamped = Math.Max(0, Math.Min(HighHeight, value));
            if (clamped < 0.001f) return 0;
            return clamped > HighHeight - 0.001f ? HighHeight : clamped;
        }
    }
}
