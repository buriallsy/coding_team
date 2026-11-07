using System;

namespace BorrowingSun2D.Core;

public enum DeliveryPhase { Waiting, Travelling, Arrived, Delivered }
public enum DeliveryResult { Applied, WrongPhase, NoCargo, NoLight, NotDocked, NeedBallast, NeedPassenger, NeedDisembark }

public sealed class DeliveryState
{
    public DeliveryPhase Phase { get; private set; }
    public float TravelSeconds { get; private set; }
    public float Progress => Math.Min(1f, TravelSeconds / GameTuning.CartTravelSeconds);
    public int DeliveredCount => Phase == DeliveryPhase.Delivered ? 1 : 0;

    // Checks a snapshot of current prerequisites at the moment of departure.
    public DeliveryResult TryDepart(bool cargo, bool light, bool docked, bool ballastReady, bool passenger)
    {
        if (Phase != DeliveryPhase.Waiting) return DeliveryResult.WrongPhase;
        if (!cargo) return DeliveryResult.NoCargo;
        if (!light) return DeliveryResult.NoLight;
        if (!docked) return DeliveryResult.NotDocked;
        if (!ballastReady) return DeliveryResult.NeedBallast;
        if (!passenger) return DeliveryResult.NeedPassenger;
        Phase = DeliveryPhase.Travelling;
        return DeliveryResult.Applied;
    }
    public void Tick(float delta)
    {
        GameTuning.ValidateDelta(delta);
        if (Phase != DeliveryPhase.Travelling) return;
        TravelSeconds = Math.Min(GameTuning.CartTravelSeconds, TravelSeconds + delta);
        if (TravelSeconds >= GameTuning.CartTravelSeconds) Phase = DeliveryPhase.Arrived;
    }
    public DeliveryResult ConfirmDelivery(bool playerAtStation, bool playerOnCart)
    {
        if (Phase != DeliveryPhase.Arrived) return DeliveryResult.WrongPhase;
        if (!playerAtStation || playerOnCart) return DeliveryResult.NeedDisembark;
        Phase = DeliveryPhase.Delivered;
        return DeliveryResult.Applied;
    }
    public void Reset() { Phase = DeliveryPhase.Waiting; TravelSeconds = 0f; }
}
