using System;

namespace BorrowingSun2D.Core;

internal enum CargoPhase { WaitingForLight, Loading, Ready, Travelling, Arrived, Delivered }

internal enum CargoActionResult
{
    Applied, NeedFruit, NeedLight, NeedCounterweight, NeedEastDock, NeedBoarding, NeedArrival,
    NeedDeliveryStation, AlreadyDelivered, AlreadyDeparted,
}

internal sealed class CargoJourney
{
    internal const float RequiredLightSeconds = 1.5f;
    internal const float CounterweightRequired = 4f;
    internal const float TravelSeconds = 3.4f;
    private const float TransferRate = 1f;

    private float lightSeconds;
    private float travelSeconds;

    internal CargoPhase Phase { get; private set; }
    internal float LightProgress => lightSeconds / RequiredLightSeconds;
    internal float TravelProgress => travelSeconds / TravelSeconds;
    internal bool HasFruit => Phase is not (CargoPhase.WaitingForLight or CargoPhase.Delivered);
    internal int DeliveredCount => Phase == CargoPhase.Delivered ? 1 : 0;

    internal void Tick(float delta, TowerMechanism tower, bool receiverLit)
    {
        switch (Phase)
        {
            case CargoPhase.WaitingForLight:
                lightSeconds = receiverLit ? Math.Min(RequiredLightSeconds, lightSeconds + delta) : 0f;
                if (lightSeconds >= RequiredLightSeconds) Phase = CargoPhase.Loading;
                break;
            case CargoPhase.Loading when receiverLit:
                tower.TransferToCounterweight(TransferRate * delta, CounterweightRequired);
                if (tower.Counterweight >= CounterweightRequired) Phase = CargoPhase.Ready;
                break;
            case CargoPhase.Travelling:
                travelSeconds = Math.Min(TravelSeconds, travelSeconds + delta);
                if (travelSeconds >= TravelSeconds) Phase = CargoPhase.Arrived;
                break;
        }
    }

    internal CargoActionResult TryLaunch(TowerMechanism tower, bool receiverLit, bool playerAboard)
    {
        if (Phase is CargoPhase.Travelling or CargoPhase.Arrived or CargoPhase.Delivered)
            return CargoActionResult.AlreadyDeparted;
        if (Phase == CargoPhase.WaitingForLight) return CargoActionResult.NeedFruit;
        if (!receiverLit) return CargoActionResult.NeedLight;
        if (tower.DockedPort != Port.East) return CargoActionResult.NeedEastDock;
        if (tower.Counterweight < CounterweightRequired) return CargoActionResult.NeedCounterweight;
        if (!playerAboard) return CargoActionResult.NeedBoarding;

        Phase = CargoPhase.Travelling;
        return CargoActionResult.Applied;
    }

    internal CargoActionResult TryDeliver(bool atDeliveryStation)
    {
        if (Phase == CargoPhase.Delivered) return CargoActionResult.AlreadyDelivered;
        if (Phase != CargoPhase.Arrived) return CargoActionResult.NeedArrival;
        if (!atDeliveryStation) return CargoActionResult.NeedDeliveryStation;

        Phase = CargoPhase.Delivered;
        return CargoActionResult.Applied;
    }

    internal void Reset()
    {
        Phase = CargoPhase.WaitingForLight;
        lightSeconds = travelSeconds = 0f;
    }
}
