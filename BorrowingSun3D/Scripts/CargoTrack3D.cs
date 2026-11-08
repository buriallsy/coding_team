using Godot;

namespace BorrowingSun3D;

internal sealed class CargoTrack3D
{
    private const float BoardingRadius = .65f;
    private const float DeliveryRadius = .75f;
    private const float SurfaceTolerance = .27f;
    private const float RecoveryClearance = .04f;

    private readonly AnimatableBody3D carrier;
    private readonly CollisionShape3D surface;
    private readonly Node3D fruit;
    private readonly Marker3D departure;
    private readonly Marker3D terminal;
    private readonly Marker3D goal;
    private readonly float surfaceHalfHeight;

    internal CargoTrack3D(Node3D scene)
    {
        carrier = scene.GetNode<AnimatableBody3D>("CargoCarrier");
        surface = carrier.GetNode<CollisionShape3D>("Surface");
        fruit = carrier.GetNode<Node3D>("FruitCrate");
        departure = scene.GetNode<Marker3D>("CargoDeparture");
        terminal = scene.GetNode<Marker3D>("CargoTerminal");
        goal = scene.GetNode<Marker3D>("Delivery/Goal");
        surfaceHalfHeight = ((BoxShape3D)surface.Shape).Size.Y * .5f;
    }

    internal Vector3 Departure => departure.GlobalPosition;
    internal Vector3 Terminal => terminal.GlobalPosition;
    internal Vector3 Goal => goal.GlobalPosition;
    internal Vector3 CarrierPosition => carrier.GlobalPosition;
    internal Vector3 SafeFeet => DeckTop + Vector3.Up * RecoveryClearance;
    internal bool FruitVisible => fruit.IsVisibleInTree();

    private Vector3 DeckTop => surface.ToGlobal(Vector3.Up * surfaceHalfHeight);

    internal bool IsAboard(Vector3 feet) => IsNearSurface(feet, DeckTop, BoardingRadius);
    internal bool IsAtDeliveryStation(Vector3 feet) => IsNearSurface(feet, Goal, DeliveryRadius);

    internal void Synchronize(float travelProgress, bool hasFruit)
    {
        // Keep native AnimatableBody3D synchronization for both travel and reset.
        Vector3 position = Departure.Lerp(Terminal, travelProgress);
        if (!carrier.GlobalPosition.IsEqualApprox(position))
            carrier.GlobalPosition = position;

        if (fruit.Visible != hasFruit)
            fruit.Visible = hasFruit;
    }

    private static bool IsNearSurface(Vector3 feet, Vector3 center, float radius)
    {
        Vector3 offset = feet - center;
        return Mathf.Abs(offset.Y) <= SurfaceTolerance
            && offset.X * offset.X + offset.Z * offset.Z <= radius * radius;
    }
}
