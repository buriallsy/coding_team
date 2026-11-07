namespace BorrowingSun2D.Core;

// Owns sequencing only. Scene adapters supply spatial facts and call geometry first.
public sealed class GameSession
{
    public TowerMechanism Tower { get; } = new();
    public SunMirror Mirror { get; } = new();
    public LightReceiver Receiver { get; } = new();
    public DeliveryState Delivery { get; } = new();
    public bool TransferRequested { get; private set; }

    public MechanismResult RequestTransfer()
    {
        if (Delivery.Phase != DeliveryPhase.Waiting || !Tower.Latched || Tower.IsTurning || Tower.IsLifting
            || !Receiver.CargoUnlocked || !Receiver.IsLit || Tower.DockedPort != Port.Cargo)
            return MechanismResult.ConditionsNotMet;
        TransferRequested = true;
        return MechanismResult.Applied;
    }
    public void PauseTransfer() => TransferRequested = false;

    // Call Tower.Tick, update physical geometry and trace the light BEFORE this method.
    // Separate phases prevent use of light calculated from the preceding frame's tower pose.
    public void Resolve(float delta, bool geometryHit)
    {
        GameTuning.ValidateDelta(delta);
        Receiver.Tick(delta, Mirror.SunEnabled && geometryHit);
        if (TransferRequested && Delivery.Phase == DeliveryPhase.Waiting && Receiver.CargoUnlocked && Receiver.IsLit
            && Tower.DockedPort == Port.Cargo)
            Tower.TransferToBallast(delta);
        Delivery.Tick(delta);
    }
    public DeliveryResult TryDepart(bool playerOnCart) => Delivery.TryDepart(
        Receiver.CargoUnlocked, Receiver.IsLit, Tower.DockedPort == Port.Cargo,
        Tower.Ballast >= TowerMechanism.Capacity - GameTuning.Epsilon, playerOnCart);
    public void Reset()
    {
        Tower.Reset(); Mirror.Reset(); Receiver.Reset(); Delivery.Reset(); TransferRequested = false;
    }
}
