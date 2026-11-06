using BorrowingSun2D.Core;
using Godot;

namespace BorrowingSun2D;

internal sealed class TowerRig2D
{
    private const float SurfaceTolerance = 8f;
    private const float StationRange = 82f;

    private readonly AnimatableBody2D lift;
    private readonly CollisionShape2D entry;
    private readonly CollisionShape2D sunCourt;
    private readonly CollisionShape2D east;
    private readonly CollisionShape2D deck;
    private readonly CollisionShape2D bridgeHinge;
    private readonly CollisionShape2D bridgeExtension;
    private readonly CollisionShape2D eastBridgeHinge;
    private readonly CollisionShape2D eastBridgeExtension;
    private readonly Marker2D station;
    private readonly Marker2D spawn;
    private readonly Marker2D sunCourtMarker;
    private readonly Marker2D goal;
    private readonly float lowTop;
    private readonly float highTop;
    private Port projectedPort = Port.Entry;

    internal TowerRig2D(Node2D scene)
    {
        lift = scene.GetNode<AnimatableBody2D>("Lift");
        entry = scene.GetNode<CollisionShape2D>("Entry/Surface");
        sunCourt = scene.GetNode<CollisionShape2D>("SunCourt/Surface");
        east = scene.GetNode<CollisionShape2D>("East/Surface");
        deck = lift.GetNode<CollisionShape2D>("Deck");
        bridgeHinge = lift.GetNode<CollisionShape2D>("BridgeHinge");
        bridgeExtension = lift.GetNode<CollisionShape2D>("BridgeExtension");
        eastBridgeHinge = lift.GetNode<CollisionShape2D>("EastBridgeHinge");
        eastBridgeExtension = lift.GetNode<CollisionShape2D>("EastBridgeExtension");
        station = lift.GetNode<Marker2D>("WeatherStation");
        spawn = scene.GetNode<Marker2D>("Entry/Spawn");
        sunCourtMarker = scene.GetNode<Marker2D>("SunCourt/Milestone");
        goal = scene.GetNode<Marker2D>("East/Goal");
        lowTop = EntryFloor.Position.Y;
        highTop = CourtFloor.Position.Y;
    }

    internal bool BridgeCollisionEnabled => !bridgeExtension.Disabled;
    internal bool EastBridgeCollisionEnabled => !eastBridgeExtension.Disabled;
    internal Rect2 EntryFloor => Surface(entry);
    internal Rect2 CourtFloor => Surface(sunCourt);
    internal Rect2 EastFloor => Surface(east);
    internal Rect2 Deck => Surface(deck);
    internal Rect2 BridgeHinge => Surface(bridgeHinge);
    internal Rect2 BridgeExtension => Surface(bridgeExtension);
    internal Rect2 EastBridgeHinge => Surface(eastBridgeHinge);
    internal Rect2 EastBridgeExtension => Surface(eastBridgeExtension);
    internal Vector2 Station => station.GlobalPosition;
    internal Vector2 Spawn => spawn.GlobalPosition;
    internal Vector2 SunCourtMarker => sunCourtMarker.GlobalPosition;
    internal Vector2 Goal => goal.GlobalPosition;

    internal bool IsAtStation(Vector2 feet) => IsAtHeight(feet, Deck.Position.Y)
        && Mathf.Abs(feet.X - Station.X) <= StationRange;

    internal bool IsAtDoorway(Vector2 feet)
    {
        Rect2 surface = Deck;
        if (!IsAtHeight(feet, surface.Position.Y)) return false;
        bool leftThreshold = feet.X >= BridgeExtension.Position.X - 8f && feet.X <= surface.Position.X + 40f;
        bool rightThreshold = feet.X >= surface.End.X - 40f && feet.X <= EastBridgeExtension.End.X + 8f;
        return leftThreshold || rightThreshold;
    }

    internal bool IsAtShoreFlag(Port port, Vector2 feet) => port switch
    {
        Port.SunCourt => feet.X <= SunCourtMarker.X && IsAtHeight(feet, SunCourtMarker.Y),
        Port.East => feet.X >= Goal.X && IsAtHeight(feet, Goal.Y),
        _ => false,
    };

    internal void Synchronize(TowerState state)
    {
        // Leave the animatable body's physics synchronization active, including on reset.
        float top = Mathf.Lerp(lowTop, highTop, state.Height / TowerMechanism.TravelMetres);
        if (!Mathf.IsEqualApprox(lift.Position.Y, top))
            lift.Position = new Vector2(lift.Position.X, top);

        Port port = state.DockedPort;
        if (port == projectedPort) return;
        projectedPort = port;
        bridgeExtension.SetDeferred(CollisionShape2D.PropertyName.Disabled, port is not (Port.Entry or Port.SunCourt));
        eastBridgeExtension.SetDeferred(CollisionShape2D.PropertyName.Disabled, port != Port.East);
    }

    private static bool IsAtHeight(Vector2 feet, float height) => Mathf.Abs(feet.Y - height) <= SurfaceTolerance;

    private static Rect2 Surface(CollisionShape2D collider)
    {
        Vector2 size = ((RectangleShape2D)collider.Shape).Size;
        return new Rect2(collider.GlobalPosition - size * .5f, size);
    }
}
