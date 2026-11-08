using BorrowingSun2D.Core;
using Godot;

namespace BorrowingSun3D;

internal sealed class TowerRig3D
{
    private const float SurfaceTolerance = .27f;
    private const float StationRadius = 1.45f;
    private const float NorthMarkerRadius = .75f;
    private const float DeckRadius = 2.5f;
    private const float DoorwayInnerEdge = 1.65f;
    private const float DoorwayOuterEdge = 4.48f;
    private const float DoorwayHalfWidth = 1.12f;

    private readonly AnimatableBody3D lift;
    private readonly CollisionShape3D bridge;
    private readonly MeshInstance3D bridgeVisual;
    private readonly MeshInstance3D bridgeStowed;
    private readonly Marker3D spawn;
    private readonly Marker3D station;
    private readonly Marker3D northMarker;
    private readonly Transform3D lowPose;
    private bool projectedBridge = true;

    internal TowerRig3D(Node3D scene)
    {
        lift = scene.GetNode<AnimatableBody3D>("Lift");
        bridge = lift.GetNode<CollisionShape3D>("BridgeCollision");
        bridgeVisual = lift.GetNode<MeshInstance3D>("BridgeVisual");
        bridgeStowed = lift.GetNode<MeshInstance3D>("BridgeStowed");
        spawn = scene.GetNode<Marker3D>("Entry/Spawn");
        station = lift.GetNode<Marker3D>("Station");
        northMarker = scene.GetNode<Marker3D>("SunCourt/Marker");
        lowPose = lift.Transform;
    }

    internal Vector3 Spawn => spawn.GlobalPosition;
    internal Vector3 Station => station.GlobalPosition;
    internal Vector3 NorthMarker => northMarker.GlobalPosition;
    internal bool BridgeCollisionEnabled => !bridge.Disabled;

    internal bool IsAtStation(Vector3 feet) => IsNearSurface(feet, Station, StationRadius);
    internal bool IsOnDeck(Vector3 feet) => IsNearSurface(feet, Station, DeckRadius);
    internal bool IsAtNorthMarker(Vector3 feet) => IsNearSurface(feet, NorthMarker, NorthMarkerRadius);

    internal bool IsAtDoorway(Vector3 feet)
    {
        Vector3 offset = feet - Station;
        if (Mathf.Abs(offset.Y) > SurfaceTolerance) return false;
        return IsInDoorway(-offset.X, offset.Z) || IsInDoorway(-offset.Z, offset.X);
    }

    internal void Synchronize(TowerState state)
    {
        // Positive mechanism heading carries the west-facing bridge toward world north (-Z).
        var yaw = new Basis(Vector3.Up, -Mathf.DegToRad(state.Heading));
        var pose = new Transform3D(yaw * lowPose.Basis, lowPose.Origin + Vector3.Up * state.Height);
        if (!lift.Transform.IsEqualApprox(pose)) lift.Transform = pose;

        bool connected = state.DockedPort is Port.Entry or Port.SunCourt;
        if (connected == projectedBridge) return;
        projectedBridge = connected;
        bridge.SetDeferred(CollisionShape3D.PropertyName.Disabled, !connected);
        bridgeVisual.Visible = connected;
        bridgeStowed.Visible = !connected;
    }

    private static bool IsNearSurface(Vector3 feet, Vector3 center, float radius)
    {
        Vector3 offset = feet - center;
        return Mathf.Abs(offset.Y) <= SurfaceTolerance
            && offset.X * offset.X + offset.Z * offset.Z <= radius * radius;
    }

    private static bool IsInDoorway(float outward, float across) =>
        outward >= DoorwayInnerEdge && outward <= DoorwayOuterEdge && Mathf.Abs(across) <= DoorwayHalfWidth;
}
