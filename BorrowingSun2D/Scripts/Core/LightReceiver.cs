using System;

namespace BorrowingSun2D.Core;

public sealed class LightReceiver
{
    public float ContinuousSeconds { get; private set; }
    public bool IsLit { get; private set; }
    public bool CargoUnlocked { get; private set; }

    // The geometry solver supplies actual hits; docking alone is never proof of light.
    public void Tick(float delta, bool validTwoMirrorPath)
    {
        GameTuning.ValidateDelta(delta);
        IsLit = validTwoMirrorPath;
        ContinuousSeconds = IsLit
            ? Math.Min(GameTuning.ReceiveSeconds, ContinuousSeconds + delta) : 0f;
        if (ContinuousSeconds >= GameTuning.ReceiveSeconds) CargoUnlocked = true;
    }
    public void Reset() { ContinuousSeconds = 0f; IsLit = CargoUnlocked = false; }
}
