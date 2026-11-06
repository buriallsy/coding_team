using Godot;

namespace BorrowingSun2D;

public partial class SlicePlayer : CharacterBody2D
{
    public const float FootOffset = 17f;
    private const float WalkSpeed = 218f;
    private const float Gravity = 1120f;

    public Vector2 Feet => Position + Vector2.Down * FootOffset;
    public float Facing { get; private set; } = 1f;

    public void Step(float delta)
    {
        float axis = Input.GetAxis("walk_left", "walk_right");
        if (axis != 0f) Facing = Mathf.Sign(axis);

        Velocity = new Vector2(axis * WalkSpeed, Mathf.Min(Velocity.Y + Gravity * delta, 640f));
        MoveAndSlide();
    }

    public void Respawn(Vector2 feet)
    {
        Position = feet - Vector2.Down * FootOffset;
        Velocity = Vector2.Zero;
        Facing = 1f;
    }
}
