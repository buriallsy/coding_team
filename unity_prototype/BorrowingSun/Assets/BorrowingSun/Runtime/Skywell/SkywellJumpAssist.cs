using System;
using UnityEngine;

namespace BorrowingSun
{
    /// <summary>Local input tolerance shared by riding and walking; each driver owns its own instance and physical velocity.</summary>
    [Serializable]
    public sealed class SkywellJumpAssist
    {
        [Min(0), Tooltip("Seconds after leaving real route support in which Space still jumps.")]
        public float coyoteSeconds = .12f;
        [Min(0), Tooltip("Seconds an early Space press waits for the next real landing.")]
        public float bufferSeconds = .15f;
        [Min(0), Tooltip("Air steering with directional input; no automatic landing attraction.")]
        public float airSteeringAcceleration = 24;
        [Min(0), Tooltip("Air slowdown after releasing movement. Ground braking retains the driver's existing acceleration.")]
        public float airBrakingAcceleration = 6;
        [Range(0, 1), Tooltip("Minimum directional launch speed, so a standing jump does not require a hidden run-up.")]
        public float takeoffSpeedFraction = .9f;
        private float grace, queued;
        private bool consumed;

        public void Tick(float dt, bool supported, bool pressed)
        {
            grace = supported ? Mathf.Max(0, coyoteSeconds) + .0001f : Mathf.Max(0, grace - dt);
            queued = pressed ? Mathf.Max(0, bufferSeconds) + .0001f : Mathf.Max(0, queued - dt);
            if (supported) consumed = false;
        }
        public bool Consume()
        {
            if (consumed || grace <= 0 || queued <= 0) return false;
            consumed = true; grace = queued = 0; return true;
        }
        public void Reset() { grace = queued = 0; consumed = false; }
        public Vector3 Steer(Vector3 velocity, Vector3 desired, bool grounded, float groundAcceleration, float dt)
        {
            float acceleration = grounded ? groundAcceleration : desired.sqrMagnitude > .001f ? airSteeringAcceleration : airBrakingAcceleration;
            return Vector3.MoveTowards(velocity, desired, Mathf.Max(0, acceleration) * dt);
        }
        public Vector3 LaunchVelocity(Vector3 velocity, Vector3 desired)
        {
            if (desired.sqrMagnitude < .001f) return velocity;
            return desired.normalized * Mathf.Max(velocity.magnitude, desired.magnitude * Mathf.Clamp01(takeoffSpeedFraction));
        }
    }
}
