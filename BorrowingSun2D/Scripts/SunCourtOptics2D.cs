using BorrowingSun2D.Core;
using Godot;
using CoreVector = System.Numerics.Vector2;

namespace BorrowingSun2D;

public partial class SunCourtOptics2D : Node2D
{
    private const uint OcclusionMask = 1u | 2u | 8u;
    private const float StationRange = 66f;
    private const float StationHeightTolerance = 8f;

    private Marker2D station = null!;
    private Marker2D sun = null!;
    private Marker2D collector = null!;
    private Marker2D relay = null!;
    private Marker2D receiver = null!;
    private CollisionShape2D beamStop = null!;
    private float parkedAim;
    private float calibratedAim;
    private float northNormalAngle;
    private float eastNormalAngle;
    private bool sunlightAvailable = true;

    internal Weather ActiveWeather { get; private set; }
    internal bool IsSunny => sunlightAvailable && ActiveWeather == Weather.Sunny;
    internal bool IsCalibrated { get; private set; }
    internal LightPath2D Path { get; private set; }
    internal bool TowerHit => Path.TowerHit;
    internal bool ReceiverLit => Path.ReceiverLit;
    internal Vector2 StationPosition => station.GlobalPosition;
    internal Vector2 SunOrigin => sun.GlobalPosition;
    internal OpticalSurface2D Collector => Surface(collector, 20f);
    internal OpticalSurface2D Relay => Surface(relay, 23f);
    internal OpticalSurface2D Receiver => Surface(receiver, 17f);
    internal Rect2 BeamStopBounds
    {
        get
        {
            Vector2 size = ((RectangleShape2D)beamStop.Shape).Size;
            return new Rect2(beamStop.GlobalPosition - size * .5f, size);
        }
    }

    public override void _Ready()
    {
        station = GetNode<Marker2D>("../SunCourt/SolarStation");
        sun = GetNode<Marker2D>("../SunCourt/SunOrigin");
        collector = GetNode<Marker2D>("../SunCourt/Collector");
        relay = GetNode<Marker2D>("../Lift/Relay");
        receiver = GetNode<Marker2D>("../East/Receiver");
        beamStop = GetNode<CollisionShape2D>("../BeamStop/Surface");
        parkedAim = collector.Rotation;
        calibratedAim = parkedAim + collector.GetNode<Marker2D>("RelayAim").Rotation;
        northNormalAngle = relay.GetNode<Marker2D>("NorthPose").Rotation;
        eastNormalAngle = relay.GetNode<Marker2D>("EastPose").Rotation;
        Reset();
    }

    internal bool IsAtStation(Vector2 feet) =>
        Mathf.Abs(feet.X - StationPosition.X) <= StationRange
        && Mathf.Abs(feet.Y - StationPosition.Y) <= StationHeightTolerance;

    internal void AimWithWind(bool calibrate)
    {
        ActiveWeather = Weather.Wind;
        IsCalibrated = calibrate;
        collector.Rotation = calibrate ? calibratedAim : parkedAim;
    }

    internal void ClearSky() => ActiveWeather = Weather.Sunny;

    internal void Recall() => ActiveWeather = Weather.None;

    internal void Reset()
    {
        Recall();
        IsCalibrated = false;
        collector.Rotation = parkedAim;
        Tick(0f);
    }

    internal void Tick(float towerHeading, bool sunlightAvailable = true)
    {
        this.sunlightAvailable = sunlightAvailable;
        // The side section uses an authored continuous projection of the rotor's azimuth.
        // The mirror keeps its pose independently of which objects the ray eventually hits.
        float weight = Mathf.InverseLerp(TowerMechanism.SunCourtHeading, TowerMechanism.EastHeading, towerHeading);
        relay.Rotation = Mathf.LerpAngle(northNormalAngle, eastNormalAngle, weight);
        Path = IsSunny ? Optics2D.Trace(ToCore(SunOrigin), ToCore(sun.GlobalTransform.X), Collector,
            Relay, Receiver, FirstObstacle) : default;
    }

    private CoreVector? FirstObstacle(CoreVector from, CoreVector to)
    {
        var query = PhysicsRayQueryParameters2D.Create(ToScene(from), ToScene(to), OcclusionMask);
        query.HitFromInside = true;
        var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
        return hit.Count == 0 ? null : ToCore(hit["position"].AsVector2());
    }

    private static OpticalSurface2D Surface(Marker2D marker, float halfLength) =>
        new(ToCore(marker.GlobalPosition), ToCore(marker.GlobalTransform.X.Normalized()), halfLength);

    private static CoreVector ToCore(Vector2 value) => new(value.X, value.Y);
    internal static Vector2 ToScene(CoreVector value) => new(value.X, value.Y);
}
