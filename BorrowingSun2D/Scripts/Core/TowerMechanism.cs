using System;

namespace BorrowingSun2D.Core;

public enum Weather { None, Rain, Wind, Sun }
public enum Port { None, Entry, SunCourt, Cargo }
public enum MechanismResult { Applied, DoorwayOccupied, Moving, NeedHigh, NeedEntryHeading, Empty, Full, Unsupported, ConditionsNotMet }
public readonly record struct TowerState(float Water, float Height, float Heading, bool Latched,
    bool Draining, bool IsLifting, bool IsTurning, Weather ActiveWeather, Port DockedPort);

public sealed class TowerMechanism
{
    public const float Capacity = 6f;
    public const float TravelMetres = 6f;
    public const float SunCourtHeading = 90f;
    private const float WaterRate = 1.5f;
    private const float LiftSpeed = 1.8f;
    private const float TurnSpeed = 45f;
    private const float DockTolerance = 0.001f;

    private float targetHeading;

    public float Water { get; private set; }
    public float Ballast { get; private set; }
    public float Height { get; private set; }
    public float Heading { get; private set; }
    public bool Latched { get; private set; }
    public bool Draining { get; private set; }
    public Weather ActiveWeather { get; private set; }

    private float HeightTarget => Latched ? TravelMetres : Water / Capacity * TravelMetres;
    public bool IsLifting => Math.Abs(Height - HeightTarget) > DockTolerance
        || Draining || ActiveWeather == Weather.Rain && Water < Capacity;
    public bool IsTurning => Math.Abs(Heading - targetHeading) > DockTolerance;
    public Port DockedPort => IsLifting || IsTurning ? Port.None
        : Height <= DockTolerance && Heading <= DockTolerance ? Port.Entry
        : Latched && Math.Abs(Heading - SunCourtHeading) <= DockTolerance ? Port.SunCourt
        : Latched && Math.Abs(Heading - GameTuning.CargoHeading) <= DockTolerance ? Port.Cargo
        : Port.None;
    public TowerState State => new(Water, Height, Heading, Latched, Draining, IsLifting, IsTurning,
        ActiveWeather, DockedPort);

    public void Tick(float delta)
    {
        GameTuning.ValidateDelta(delta);
        if (Draining)
        {
            Water = Math.Max(0f, Water - WaterRate * delta);
            Draining = Water > 0f;
        }
        else if (ActiveWeather == Weather.Rain)
        {
            Water = Math.Min(Capacity, Water + WaterRate * delta);
        }

        Height = MoveTowards(Height, HeightTarget, LiftSpeed * delta);
        if (Water >= Capacity && Height >= TravelMetres)
            Latched = true;

        Heading = MoveTowards(Heading, targetHeading, TurnSpeed * delta);
    }

    public MechanismResult Cast(Weather weather, bool doorwayOccupied)
    {
        if (doorwayOccupied) return MechanismResult.DoorwayOccupied;

        if (weather == Weather.Rain)
        {
            if (Ballast > 0f) return MechanismResult.ConditionsNotMet;
            if (IsTurning) return MechanismResult.Moving;
            if (Water >= Capacity) return MechanismResult.Full;
            Draining = false;
            ActiveWeather = Weather.Rain;
            return MechanismResult.Applied;
        }

        if (weather != Weather.Wind) return MechanismResult.Unsupported;
        if (IsLifting || IsTurning) return MechanismResult.Moving;
        if (!Latched) return MechanismResult.NeedHigh;
        ActiveWeather = Weather.Wind;
        // A second cast at the north stop returns the bridge toward the entry heading.
        targetHeading = DockedPort == Port.SunCourt ? 0f : SunCourtHeading;
        return MechanismResult.Applied;
    }

    // Explicit target selection for the complete level; Cast(Wind) preserves the original MVP route.
    public MechanismResult RequestDock(Port target, bool doorwayOccupied)
    {
        if (target != Port.Entry && target != Port.SunCourt && target != Port.Cargo)
            return MechanismResult.Unsupported;
        if (doorwayOccupied) return MechanismResult.DoorwayOccupied;
        if (IsLifting || IsTurning) return MechanismResult.Moving;
        if (!Latched) return MechanismResult.NeedHigh;
        targetHeading = target == Port.Entry ? 0f : target == Port.SunCourt ? SunCourtHeading : GameTuning.CargoHeading;
        ActiveWeather = Weather.Wind;
        return MechanismResult.Applied;
    }

    // Owns both quantities so transfer cannot create water. Session gates light/cargo prerequisites.
    public float TransferToBallast(float delta)
    {
        GameTuning.ValidateDelta(delta);
        if (!Latched || DockedPort != Port.Cargo) return 0f;
        float amount = Math.Min(Water, Math.Min(Capacity - Ballast, GameTuning.TransferUnitsPerSecond * delta));
        Water -= amount;
        Ballast += amount;
        ActiveWeather = Weather.None;
        return amount;
    }

    public void Recall()
    {
        ActiveWeather = Weather.None;
        targetHeading = Heading;
    }

    public MechanismResult ToggleDrain(bool doorwayOccupied)
    {
        if (Draining)
        {
            Draining = false;
            return MechanismResult.Applied;
        }

        if (doorwayOccupied) return MechanismResult.DoorwayOccupied;
        if (Ballast > 0f) return MechanismResult.ConditionsNotMet;
        if (IsTurning) return MechanismResult.Moving;
        if (Heading > DockTolerance) return MechanismResult.NeedEntryHeading;
        if (Water <= 0f) return MechanismResult.Empty;

        Recall();
        Latched = false;
        Draining = true;
        return MechanismResult.Applied;
    }

    public void Reset()
    {
        Water = Ballast = Height = Heading = targetHeading = 0f;
        ActiveWeather = Weather.None;
        Latched = Draining = false;
    }

    private static float MoveTowards(float value, float target, float step)
    {
        float difference = target - value;
        return Math.Abs(difference) <= step ? target : value + Math.Sign(difference) * step;
    }
}
