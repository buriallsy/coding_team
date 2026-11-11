using Godot;

namespace BorrowingSun2D;

internal sealed class CargoTrack2D
{
    private const float StationRange = 14f;
    private const float SurfaceTolerance = 8f;

    private readonly AnimatableBody2D carrier;
    private readonly CollisionShape2D carrierSurface;
    private readonly CollisionShape2D deliverySurface;
    private readonly Marker2D departure;
    private readonly Marker2D terminal;
    private readonly Marker2D goal;

    internal CargoTrack2D(Node2D scene)
    {
        carrier = scene.GetNode<AnimatableBody2D>("CargoCarrier");
        carrierSurface = carrier.GetNode<CollisionShape2D>("Surface");
        deliverySurface = scene.GetNode<CollisionShape2D>("Delivery/Surface");
        departure = scene.GetNode<Marker2D>("CargoDeparture");
        terminal = scene.GetNode<Marker2D>("CargoTerminal");
        goal = scene.GetNode<Marker2D>("Delivery/Goal");
    }

    internal Rect2 CarrierFloor => Surface(carrierSurface);
    internal Rect2 DeliveryFloor => Surface(deliverySurface);
    internal Vector2 Departure => departure.GlobalPosition;
    internal Vector2 Terminal => terminal.GlobalPosition;
    internal Vector2 Goal => goal.GlobalPosition;
    internal Vector2 SafeFeet => new(carrier.GlobalPosition.X, CarrierFloor.Position.Y - 1f);

    internal bool IsAboard(Vector2 feet) => NearStation(feet, carrier.GlobalPosition);
    internal bool IsOnDeck(Vector2 feet)
    {
        Rect2 floor = CarrierFloor;
        return feet.X >= floor.Position.X && feet.X <= floor.End.X
            && Mathf.Abs(feet.Y - floor.Position.Y) <= SurfaceTolerance;
    }
    internal bool IsAtDeliveryStation(Vector2 feet) => NearStation(feet, Goal);

    internal void Synchronize(float travelProgress)
    {
        Vector2 position = Departure.Lerp(Terminal, travelProgress);
        if (!carrier.GlobalPosition.IsEqualApprox(position))
            carrier.GlobalPosition = position;
    }

    private static bool NearStation(Vector2 feet, Vector2 marker) =>
        Mathf.Abs(feet.X - marker.X) <= StationRange && Mathf.Abs(feet.Y - marker.Y) <= SurfaceTolerance;

    private static Rect2 Surface(CollisionShape2D collider)
    {
        Vector2 size = ((RectangleShape2D)collider.Shape).Size;
        return new Rect2(collider.GlobalPosition - size * .5f, size);
    }
}
