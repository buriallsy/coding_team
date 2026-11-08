using System;

namespace BorrowingSun
{
    /// <summary>Rain is collected at an upper dock, carried through a lower route, then spent on the eastern lift.</summary>
    public sealed class RainCargoState
    {
        public const float HighHeight = 3f;
        public const float WaterSpeed = 0.65f;
        public const float TransferSpeed = 0.8f;
        public const int FinalStop = 7;
        public const int Hand = -1;
        private float transferVolume;
        public float WaterA { get; private set; }
        public float WaterB { get; private set; }
        public float TankWater { get; private set; }
        public int CardStation { get; private set; } = Hand;
        public bool RainActive { get; private set; }
        public bool DrainAOpen { get; private set; }
        public bool DrainBOpen { get; private set; }
        public bool Transferring { get; private set; }
        public int CartStop { get; private set; }
        public int PendingCartStop { get; private set; } = -1;
        public bool CartInMotion => PendingCartStop >= 0;
        public bool IsALow => WaterA <= 0.001f;
        public bool IsAHigh => WaterA >= HighHeight - 0.001f;
        public bool IsBLow => WaterB <= 0.001f;
        public bool IsBHigh => WaterB >= HighHeight - 0.001f;
        public bool TankFull => TankWater >= HighHeight - 0.001f;
        public bool IsRemoteRain => RainActive && CardStation == 1;
        public bool AMoving => (RainActive && CardStation == 0 && !IsAHigh) || (DrainAOpen && !IsALow);
        public bool BMoving => Transferring || (DrainBOpen && !IsBLow);
        public bool RainToTank => IsRemoteRain && CartStop == 1 && IsAHigh && !AMoving && !CartInMotion;
        public bool RainToWaste => IsRemoteRain && !RainToTank;
        public bool TankFilling => RainToTank && !TankFull;
        public bool WaterInMotion => AMoving || BMoving || TankFilling;
        public bool Delivered => CartStop == FinalStop;
        public bool Completed => Delivered && CardStation == Hand;

        public void Reset()
        {
            WaterA = WaterB = TankWater = 0;
            transferVolume = 0;
            CardStation = Hand;
            RainActive = DrainAOpen = DrainBOpen = Transferring = false;
            CartStop = 0;
            PendingCartStop = -1;
        }

        public bool InsertCard(int station)
        {
            if (!ValidStation(station) || CardStation != Hand || CartInMotion || Transferring) return false;
            CardStation = station;
            return true;
        }

        public bool RemoveCard(int station)
        {
            if (!ValidStation(station) || CardStation != station || CartInMotion || Transferring) return false;
            CardStation = Hand;
            RainActive = false;
            return true;
        }

        public bool ToggleRain(int station)
        {
            if (!ValidStation(station) || CardStation != station || CartInMotion || Transferring) return false;
            RainActive = !RainActive;
            if (RainActive && station == 0) DrainAOpen = false;
            return true;
        }

        public bool ToggleDrain(int basin)
        {
            if (!ValidStation(basin) || CartInMotion || Transferring) return false;
            if (basin == 0)
            {
                DrainAOpen = !DrainAOpen;
                if (DrainAOpen && CardStation == 0) RainActive = false;
            }
            else DrainBOpen = !DrainBOpen;
            return true;
        }

        public bool CanTransferWater(out string reason)
        {
            reason = "";
            if (CartInMotion || Transferring) { reason = "请等苗车与接水设备停稳。"; return false; }
            if (CartStop != 5) { reason = "把水箱车送上东侧低位升降台，才能接入卸水口。"; return false; }
            if (!TankFull) { reason = "水箱未满，升降台需要一整箱雨水。"; return false; }
            if (!IsBLow) { reason = "先排空配重箱，使货梯回到低位接水口。"; return false; }
            if (WaterInMotion) { reason = "等升降设备停稳后，再把水箱接入。"; return false; }
            return true;
        }

        public bool TransferWater()
        {
            if (!CanTransferWater(out _)) return false;
            // The loading coupling closes the drain before pouring, including after a return trip.
            DrainBOpen = false;
            transferVolume = TankWater + WaterB;
            Transferring = true;
            return true;
        }

        public void Advance(float seconds)
        {
            if (seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds) || CartInMotion) return;
            if (Transferring)
            {
                // Derive both sides from one saved amount: two accumulated floats can otherwise
                // disagree at the last drop after many short ticks and leave the lift moving forever.
                TankWater = Math.Max(0, TankWater - TransferSpeed * seconds);
                WaterB = transferVolume - TankWater;
                if (TankWater <= 0.00001f)
                {
                    WaterB = transferVolume;
                    TankWater = 0;
                    Transferring = false;
                }
                return;
            }
            // Take the alignment before changing A: moving away from the dock immediately loses its feed.
            bool fillTank = RainToTank;
            float rateA = RainActive && CardStation == 0 ? 1 : DrainAOpen ? -1 : 0;
            WaterA = ClampWater(WaterA + rateA * WaterSpeed * seconds);
            if (fillTank) TankWater = ClampWater(TankWater + WaterSpeed * seconds);
            if (DrainBOpen) WaterB = ClampWater(WaterB - WaterSpeed * seconds);
        }

        public bool CanMoveCart(int direction, out string reason)
        {
            reason = "";
            if (direction != -1 && direction != 1) { reason = "苗车只能前进或倒退一站。"; return false; }
            if (CartInMotion) { reason = "苗车正在前往下一处停靠位，请等它停稳。"; return false; }
            int next = CartStop + direction;
            if (next < 0) { reason = "已经回到西侧起点。"; return false; }
            if (next > FinalStop) { reason = "苗车已停在东侧收货位。"; return false; }
            if (WaterInMotion) { reason = "等接水或升降结束，也可以先停雨再推车。"; return false; }
            int edge = Math.Min(CartStop, next);
            if (edge <= 1 && !IsALow)
            { reason = "接雨台还在高处；降回货台高度，才能带着水箱离开。"; return false; }
            if (edge == 4 && !IsBLow)
            { reason = "东侧升降台还未与下层货路齐平。"; return false; }
            if (edge == 5 && !IsBHigh)
            { reason = "东侧升降台未抵达上层码头，在台上卸入整箱雨水可以抬升。"; return false; }
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
