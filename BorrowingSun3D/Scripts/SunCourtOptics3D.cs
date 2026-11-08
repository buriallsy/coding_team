using BorrowingSun2D.Core;
using Godot;
using CoreVector = System.Numerics.Vector3;

namespace BorrowingSun3D;

internal sealed class SunCourtOptics3D
{
    private const uint OcclusionMask = 1u | 2u | 8u;
    private const float StationRadius = 1.45f;
    private const float StationHeightTolerance = .27f;
    private const float CollectorRadius = .62f;
    private const float RelayRadius = .54f;
    private const float ReceiverRadius = .52f;
    private const float RayLength = 24f;

    private static readonly Color ReceiverIdle = new("82774b");
    private static readonly Color ReceiverBright = new("ffda83");

    private readonly Node3D scene;
    private readonly Marker3D station;
    private readonly Marker3D sun;
    private readonly Marker3D collector;
    private readonly Marker3D relay;
    private readonly Marker3D receiver;
    private readonly Basis parkedAim;
    private readonly Basis calibratedAim;
    private readonly StandardMaterial3D receiverMaterial;
    private readonly MeshInstance3D incomingBeam;
    private readonly MeshInstance3D firstBeam;
    private readonly MeshInstance3D secondBeam;
    private readonly PhysicsRayQueryParameters3D rayQuery = new()
    {
        CollisionMask = OcclusionMask,
        HitFromInside = true,
    };

    internal SunCourtOptics3D(Node3D scene)
    {
        this.scene = scene;
        station = scene.GetNode<Marker3D>("SunCourt/SolarStation");
        sun = scene.GetNode<Marker3D>("SunCourt/SunOrigin");
        collector = scene.GetNode<Marker3D>("SunCourt/Collector");
        relay = scene.GetNode<Marker3D>("Lift/Relay");
        receiver = scene.GetNode<Marker3D>("East/Receiver");
        parkedAim = collector.Transform.Basis;
        calibratedAim = scene.GetNode<Marker3D>("SunCourt/CollectorAim").Transform.Basis;

        CreateDisc(collector, "CollectorDisc", CollectorRadius, MakeMaterial(new Color("a9c6bc")));
        CreateDisc(relay, "RelayDisc", RelayRadius, MakeMaterial(new Color("c2d8d2")));
        receiverMaterial = MakeMaterial(ReceiverIdle);
        CreateDisc(receiver, "ReceiverDisc", ReceiverRadius, receiverMaterial);

        var beams = new Node3D { Name = "LightSegments" };
        scene.AddChild(beams);
        var beamMesh = new CylinderMesh { TopRadius = .031f, BottomRadius = .031f, Height = 1f, RadialSegments = 12 };
        StandardMaterial3D beamMaterial = MakeMaterial(new Color("ffcc68"));
        incomingBeam = CreateBeam(beams, "Incoming", beamMesh, beamMaterial);
        firstBeam = CreateBeam(beams, "FirstReflection", beamMesh, beamMaterial);
        secondBeam = CreateBeam(beams, "SecondReflection", beamMesh, beamMaterial);
    }

    internal Weather ActiveWeather { get; private set; }
    internal bool IsSunny => ActiveWeather == Weather.Sunny;
    internal bool IsCalibrated { get; private set; }
    internal LightPath3D Path { get; private set; }
    internal bool TowerHit => Path.TowerHit;
    internal bool ReceiverLit => Path.ReceiverLit;

    internal bool IsAtStation(Vector3 feet)
    {
        Vector3 offset = feet - station.GlobalPosition;
        return Mathf.Abs(offset.Y) <= StationHeightTolerance
            && offset.X * offset.X + offset.Z * offset.Z <= StationRadius * StationRadius;
    }

    internal void AimWithWind(bool calibrate)
    {
        ActiveWeather = Weather.Wind;
        IsCalibrated = calibrate;
        collector.Transform = new Transform3D(calibrate ? calibratedAim : parkedAim, collector.Position);
    }

    internal void ClearSky() => ActiveWeather = Weather.Sunny;

    internal void Recall() => ActiveWeather = Weather.None;

    internal void Reset()
    {
        Recall();
        IsCalibrated = false;
        collector.Transform = new Transform3D(parkedAim, collector.Position);
        Tick();
    }

    internal void Tick()
    {
        bool wasLit = ReceiverLit;
        // Each +Z normal comes from an authored pose. The tower mirror inherits the physical lift yaw.
        Path = IsSunny
            ? Optics3D.Trace(ToCore(sun.GlobalPosition), ToCore(sun.GlobalTransform.Basis.Z),
                Surface(collector, CollectorRadius), Surface(relay, RelayRadius), Surface(receiver, ReceiverRadius),
                FirstObstacle, RayLength)
            : default;
        if (wasLit != ReceiverLit)
            receiverMaterial.AlbedoColor = ReceiverLit ? ReceiverBright : ReceiverIdle;

        ShowBeam(incomingBeam, Path.Incoming);
        ShowBeam(firstBeam, Path.FirstReflection);
        ShowBeam(secondBeam, Path.SecondReflection);
    }

    private CoreVector? FirstObstacle(CoreVector from, CoreVector to)
    {
        rayQuery.From = ToScene(from);
        rayQuery.To = ToScene(to);
        var hit = scene.GetWorld3D().DirectSpaceState.IntersectRay(rayQuery);
        return hit.Count == 0 ? null : ToCore(hit["position"].AsVector3());
    }

    private static OpticalSurface3D Surface(Marker3D marker, float radius) =>
        new(ToCore(marker.GlobalPosition), ToCore(marker.GlobalTransform.Basis.Z), radius);

    private static void CreateDisc(Node3D marker, string name, float radius, StandardMaterial3D material)
    {
        // CylinderMesh is Y-up. A quarter turn makes the disc face the marker's +Z optical normal.
        var disc = new MeshInstance3D
        {
            Name = name,
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = .075f, RadialSegments = 32 },
            MaterialOverride = material,
            RotationDegrees = new Vector3(90f, 0f, 0f),
        };
        marker.AddChild(disc);
    }

    private static StandardMaterial3D MakeMaterial(Color color) => new()
    {
        AlbedoColor = color,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
    };

    private static MeshInstance3D CreateBeam(Node3D parent, string name, Mesh mesh, Material material)
    {
        var beam = new MeshInstance3D
        {
            Name = name,
            Mesh = mesh,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false,
        };
        parent.AddChild(beam);
        return beam;
    }

    private static void ShowBeam(MeshInstance3D mesh, LightSegment3D? segment)
    {
        if (segment is not { } light)
        {
            mesh.Hide();
            return;
        }

        Vector3 start = ToScene(light.From);
        Vector3 end = ToScene(light.To);
        Vector3 delta = end - start;
        float length = delta.Length();
        if (length <= .0001f)
        {
            mesh.Hide();
            return;
        }

        Vector3 direction = delta / length;
        Vector3 reference = Mathf.Abs(direction.Dot(Vector3.Up)) > .95f ? Vector3.Right : Vector3.Up;
        Vector3 across = direction.Cross(reference).Normalized();
        Vector3 third = across.Cross(direction);
        mesh.GlobalTransform = new Transform3D(new Basis(across, delta, third), (start + end) * .5f);
        mesh.Show();
    }

    private static CoreVector ToCore(Vector3 value) => new(value.X, value.Y, value.Z);
    internal static Vector3 ToScene(CoreVector value) => new(value.X, value.Y, value.Z);
}
