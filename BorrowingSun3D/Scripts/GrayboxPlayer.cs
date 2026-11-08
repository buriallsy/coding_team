using Godot;

namespace BorrowingSun3D;

public partial class GrayboxPlayer : CharacterBody3D
{
    private const float WalkSpeed = 4.2f;
    private const float Gravity = 18f;

    internal Vector3 Feet => GlobalPosition;

    internal void Step(Vector2 input, float delta)
    {
        Velocity = new Vector3(input.X * WalkSpeed, Velocity.Y - Gravity * delta, input.Y * WalkSpeed);
        MoveAndSlide();
    }

    internal void Respawn(Vector3 feet)
    {
        GlobalPosition = feet;
        Velocity = Vector3.Zero;
        ResetPhysicsInterpolation();
    }
}
