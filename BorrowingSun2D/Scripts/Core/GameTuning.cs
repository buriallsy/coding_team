using System;

namespace BorrowingSun2D.Core;

// Approved design defaults, not claims of measured completion time.
public static class GameTuning
{
    public const float ReceiveSeconds = 3f;
    public const float TransferUnitsPerSecond = 1f;
    public const float CartTravelSeconds = 12f;
    public const float CargoHeading = 180f;
    public const float Epsilon = 0.001f;
    public const float TargetPlaySeconds = 180f;
    public static void ValidateDelta(float delta)
    {
        if (!float.IsFinite(delta) || delta < 0f)
            throw new ArgumentOutOfRangeException(nameof(delta));
    }
}
