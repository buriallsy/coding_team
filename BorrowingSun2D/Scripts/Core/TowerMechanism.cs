using System;

namespace BorrowingSun2D.Core;

public enum Weather { None, Rain, Wind, Sunny }
public enum Port { None, Entry, SunCourt, East }
public enum TurnDirection { Clockwise, Counterclockwise }
public enum MechanismResult
{
    Applied, DoorwayOccupied, Moving, NeedHigh, NeedEntryHeading, Empty, Full, TurnLimit, UnsupportedWeather,
    CounterweightCommitted,
}
public readonly record struct TowerState(float Water, float Height, float Heading, bool Latched,
    bool Draining, bool IsLifting, bool IsTurning, Weather ActiveWeather, Port DockedPort, float Counterweight);

public sealed class TowerMechanism
{
    public const float Capacity = 6f;
    public const float TravelMetres = 6f;
    public const float SunCourtHeading = 90f;
    public const float EastHeading = 180f;
    private const float WaterRate = 1.5f;
    private const float LiftSpeed = 1.8f;
    private const float TurnSpeed = 45f;
    private const float DockTolerance = 0.001f;

    private float targetHeading;
    private float storedWater;

    public float Water => storedWater - Counterweight;
    public float Counterweight { get; private set; }
    public float Height { get; private set; }
    public float Heading { get; private set; }
    public bool Latched { get; private set; }
    public bool Draining { get; private set; }
    public Weather ActiveWeather { get; private set; }

    private float HeightTarget => Latched ? TravelMetres : Water / Capacity * TravelMetres;
    public bool IsLifting => Math.Abs(Height - HeightTarget) > DockTolerance
        || Draining || ActiveWeather == Weather.Rain && storedWater < Capacity;
    public bool IsTurning => Math.Abs(Heading - targetHeading) > DockTolerance;
    public Port DockedPort => IsLifting || IsTurning ? Port.None
        : Height <= DockTolerance && AtHeading(0f) ? Port.Entry
        : Latched && AtHeading(SunCourtHeading) ? Port.SunCourt
        : Latched && AtHeading(EastHeading) ? Port.East
        : Port.None;
    public TowerState State => new(Water, Height, Heading, Latched, Draining, IsLifting, IsTurning,
        ActiveWeather, DockedPort, Counterweight);

    public void Tick(float delta)
    {
        if (Draining)
        {
            storedWater = Math.Max(0f, storedWater - WaterRate * delta);
            Draining = storedWater > 0f;
        }
        else if (ActiveWeather == Weather.Rain)
        {
            storedWater = Math.Min(Capacity, storedWater + WaterRate * delta);
        }

        Height = MoveTowards(Height, HeightTarget, LiftSpeed * delta);
        if (storedWater >= Capacity && Height >= TravelMetres)
            Latched = true;

        Heading = MoveTowards(Heading, targetHeading, TurnSpeed * delta);
    }

    public MechanismResult Cast(Weather weather, bool doorwayOccupied, TurnDirection direction)
    {
        if (weather is not (Weather.Rain or Weather.Wind)) return MechanismResult.UnsupportedWeather;
        if (doorwayOccupied) return MechanismResult.DoorwayOccupied;

        if (weather == Weather.Rain)
        {
            if (IsTurning) return MechanismResult.Moving;
            if (storedWater >= Capacity) return MechanismResult.Full;
            Draining = false;
            ActiveWeather = Weather.Rain;
            return MechanismResult.Applied;
        }

        if (IsLifting || IsTurning) return MechanismResult.Moving;
        if (!Latched) return MechanismResult.NeedHigh;

        float nextHeading = direction == TurnDirection.Clockwise
            ? Heading < SunCourtHeading - DockTolerance ? SunCourtHeading : EastHeading
            : Heading > SunCourtHeading + DockTolerance ? SunCourtHeading : 0f;
        if (AtHeading(nextHeading)) return MechanismResult.TurnLimit;

        ActiveWeather = Weather.Wind;
        targetHeading = nextHeading;
        return MechanismResult.Applied;
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
        if (IsTurning) return MechanismResult.Moving;
        if (Heading > DockTolerance) return MechanismResult.NeedEntryHeading;
        if (Counterweight > 0f) return MechanismResult.CounterweightCommitted;
        if (Water <= 0f) return MechanismResult.Empty;

        Recall();
        Latched = false;
        Draining = true;
        return MechanismResult.Applied;
    }

    internal void TransferToCounterweight(float amount, float required)
    {
        if (DockedPort != Port.East || amount <= 0f) return;
        // Both readouts are shares of the same stored water. The mechanical lock holds the lift.
        float limit = Math.Min(storedWater, required);
        Counterweight = Math.Max(Counterweight, Math.Min(limit, Counterweight + amount));
    }

    internal bool TrySpendWater(float amount)
    {
        if (!float.IsFinite(amount) || amount <= 0f || Water < amount) return false;
        storedWater -= amount;
        return true;
    }

    public void Reset()
    {
        storedWater = Counterweight = Height = Heading = targetHeading = 0f;
        ActiveWeather = Weather.None;
        Latched = Draining = false;
    }

    private bool AtHeading(float angle) => Math.Abs(Heading - angle) <= DockTolerance;

    private static float MoveTowards(float value, float target, float step)
    {
        float difference = target - value;
        return Math.Abs(difference) <= step ? target : value + Math.Sign(difference) * step;
    }
}
