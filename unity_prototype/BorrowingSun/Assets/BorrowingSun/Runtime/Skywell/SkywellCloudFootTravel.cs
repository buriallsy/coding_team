using UnityEngine;
using UnityEngine.InputSystem;

namespace BorrowingSun
{
    /// <summary>Earned, local cloud-route jumping. Reuses the player's single collision body and releases it off the route.</summary>
    [DefaultExecutionOrder(-5)]
    public sealed class SkywellCloudFootTravel : MonoBehaviour
    {
        [SerializeField] private SkywellController court;
        [SerializeField] private SkywellDeerRide ride;
        [Header("徒步跳云 / 只在已解锁云路生效")]
        [SerializeField, Min(1)] private float moveSpeed = 6;
        [SerializeField, Min(1)] private float groundAcceleration = 28;
        [SerializeField] private SkywellJumpAssist jumpAssist = new SkywellJumpAssist();
        [SerializeField, Min(.2f)] private float jumpHeight = 2.6f;
        [SerializeField, Min(1)] private float gravity = 20;
        [SerializeField, Min(1)] private float fallRecoveryDepth = 5;
        [SerializeField, Min(1)] private float endpointActivationDistance = 4.5f;
        private CourtPlayer player;
        private CharacterController body;
        private bool active, grounded, focused = true;
        private Vector3 horizontalVelocity, safePosition, safeFacing;
        private float verticalVelocity, stableTime, unsupportedTime;
        private Collider lastSafePlatform;
        private readonly RaycastHit[] exitGroundHits = new RaycastHit[24];
        private static readonly Vector3[] FootOffsets = { Vector3.zero, Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
        public bool IsActive => active;
        public bool IsGrounded => active && grounded;
        public bool IsAirborne => active && !grounded;
        public Collider LastSafePlatform => lastSafePlatform;
        public int RecoveryCount { get; private set; }
        public SkywellJumpAssist JumpAssist => jumpAssist;
        public bool CanJumpHere => Ready() && TryLocalSupport(player.FeetPosition, out _);
        public string Prompt => CanJumpHere || active ? "WASD 移动 · Space 跳云" : null;

        public void Configure(SkywellController owner, SkywellDeerRide deerRide)
        {
            court = owner; ride = deerRide;
            CacheBindings();
        }
        private void Awake() { focused = Application.isFocused; CacheBindings(); }
        private void CacheBindings()
        {
            if (court) player = court.Player;
            if (player) body = player.GetComponent<CharacterController>();
            if (ride) ride.RegisterFootDriver(this);
        }
        private bool Ready() => isActiveAndEnabled && court && court.isActiveAndEnabled && player
            && player.isActiveAndEnabled && body && body.enabled && ride && ride.isActiveAndEnabled
            && ride.FootRouteUnlocked && !ride.IsMounted && !player.IsSwimming && !player.IsDropping
            && !player.IsAutoPositioning && !player.IsPushingCart
            && !(court.Journey && court.Journey.ControlsMovement)
            && !(court.Voyage && court.Voyage.Phase >= SkywellVoyagePhase.Boarding && court.Voyage.Phase <= SkywellVoyagePhase.Docking);

        /// <summary>Actual supported cloud tops, or only the nearby endpoint apron. This never grants global jump.</summary>
        public bool TryLocalSupport(Vector3 feet, out Collider support)
        {
            support = null;
            if (!ride || !body || !ride.TryRouteFootprint(feet, body.radius, out var found)) return false;
            var route = ride.SafePlatforms;
            int index = System.Array.IndexOf(route, found);
            if (index < 0) return false;
            if (index > 0 && index < route.Length - 1) { support = found; return true; }
            if (route.Length < 3) return false;
            var cloud = route[index == 0 ? 1 : route.Length - 2];
            if (!cloud) return false;
            // Bounds only restrict the endpoint activation area; actual support above is always
            // the authored collider with the full five-point footprint, including concave holes.
            Vector3 anchor = found.bounds.ClosestPoint(cloud.bounds.center);
            if (Vector3.ProjectOnPlane(feet - anchor, Vector3.up).sqrMagnitude > endpointActivationDistance * endpointActivationDistance)
                return false;
            support = found; return true;
        }

        /// <summary>A real, fully supported walking seam beside the last island/village landing, never a cloud-edge exit.</summary>
        public bool HasEndpointExitFootprint(Vector3 feet, Collider previousSupport)
        {
            if (!ride || !body || !previousSupport || !previousSupport.enabled || !previousSupport.gameObject.activeInHierarchy)
                return false;
            var route = ride.SafePlatforms;
            if (route == null || route.Length < 3 || previousSupport != route[0] && previousSupport != route[route.Length - 1])
                return false;
            // This is only a seam handoff. Bounds limit proximity; all five support points below
            // still raycast real solids, so an endpoint AABB cannot bridge a hole or cliff.
            Vector3 nearest = previousSupport.bounds.ClosestPoint(feet);
            if (Vector3.ProjectOnPlane(feet - nearest, Vector3.up).sqrMagnitude > Mathf.Pow(body.radius + .2f, 2))
                return false;
            float inset = Mathf.Max(.1f, body.radius) * .8f;
            float slopeTolerance = inset * Mathf.Tan(body.slopeLimit * Mathf.Deg2Rad) + .04f;
            float centerHeight = feet.y;
            bool hasOrdinaryGround = false;
            for (int i = 0; i < FootOffsets.Length; i++)
            {
                Vector3 probe = new Vector3(feet.x, centerHeight, feet.z) + FootOffsets[i] * inset;
                float tolerance = i == 0 ? body.stepOffset + body.skinWidth + .03f : slopeTolerance;
                if (!TryExitGround(probe, tolerance, out RaycastHit hit)) return false;
                int index = System.Array.IndexOf(route, hit.collider);
                if (index > 0 && index < route.Length - 1) return false;
                if (i == 0) centerHeight = hit.point.y;
                hasOrdinaryGround |= index < 0;
            }
            return hasOrdinaryGround;
        }

        private bool TryExitGround(Vector3 feet, float tolerance, out RaycastHit ground)
        {
            ground = default;
            float reach = tolerance + .02f, nearest = float.PositiveInfinity;
            int count = Physics.RaycastNonAlloc(feet + Vector3.up * reach, Vector3.down, exitGroundHits,
                reach * 2, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == exitGroundHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = exitGroundHits[i];
                if (!hit.collider || player && hit.transform.IsChildOf(player.transform) || hit.distance >= nearest) continue;
                ground = hit; nearest = hit.distance;
            }
            return ground.collider && ground.normal.y >= Mathf.Cos(body.slopeLimit * Mathf.Deg2Rad)
                && Mathf.Abs(ground.point.y - feet.y) <= tolerance;
        }

        private void Update()
        {
            if (!Ready()) { Stop(true); return; }
            if (active && !player.ExternalMotionOwnedBy(this)) { Stop(false); return; }
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) return; // CourtPlayer dispatches R once.
            if (!active)
            {
                if (!focused || court.ObservationInputBlocked || court.WeatherInputBlocked || player.IsExternallyDriven
                    || !body.isGrounded || !TryLocalSupport(player.FeetPosition, out Collider support)) return;
                Vector3 approachVelocity = player.PlanarVelocity;
                if (!player.BeginExternalMotion(this, body.radius, body.height)) return;
                active = grounded = true; stableTime = unsupportedTime = 0;
                horizontalVelocity = Vector3.ClampMagnitude(approachVelocity, moveSpeed); verticalVelocity = -2;
                jumpAssist.Reset();
                if (court.CameraRig) court.CameraRig.SetPlatformingView(true);
                safePosition = player.FeetPosition; safeFacing = player.FacingDirection; lastSafePlatform = support;
            }
            if (!focused || court.ObservationInputBlocked || court.WeatherInputBlocked) { jumpAssist.Reset(); return; }
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            Vector2 axis = Vector2.zero;
            if (keyboard != null)
            {
                axis.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
                axis.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
            }
            axis = Vector2.ClampMagnitude(axis, 1);
            Vector3 forward = court.CameraRig ? court.CameraRig.PlanarMovementForward : player.FacingDirection;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 desired = (forward * axis.y + right * axis.x) * moveSpeed;
            horizontalVelocity = jumpAssist.Steer(horizontalVelocity, desired, grounded, groundAcceleration, dt);
            jumpAssist.Tick(dt, grounded && ride.TryRouteCenterSupport(player.FeetPosition, out _),
                keyboard != null && keyboard.spaceKey.wasPressedThisFrame);
            if (jumpAssist.Consume())
            {
                // Recovery still uses the last complete footprint, never an overhanging takeoff.
                if (TryLocalSupport(player.FeetPosition, out Collider launch))
                { safePosition = player.FeetPosition; safeFacing = player.FacingDirection; lastSafePlatform = launch; }
                horizontalVelocity = jumpAssist.LaunchVelocity(horizontalVelocity, desired);
                verticalVelocity = Mathf.Sqrt(2 * gravity * jumpHeight); grounded = false; stableTime = 0;
            }
            verticalVelocity = Mathf.Max(-25, verticalVelocity - gravity * dt);
            Vector3 facing = desired.sqrMagnitude > .01f ? desired.normalized : player.FacingDirection;
            CollisionFlags collision = player.MoveExternal(this, (horizontalVelocity + Vector3.up * verticalVelocity) * dt, facing, dt);
            if ((collision & CollisionFlags.Above) != 0 && verticalVelocity > 0) verticalVelocity = 0;
            grounded = (collision & CollisionFlags.Below) != 0 && verticalVelocity <= 0;
            if (grounded) verticalVelocity = -2;
            Collider landing = null;
            bool supported = grounded && ride.TryRouteFootprint(player.FeetPosition, body.radius, out landing);
            if (supported)
            {
                stableTime += dt; unsupportedTime = 0;
                if (stableTime > .1f) { safePosition = player.FeetPosition; safeFacing = player.FacingDirection; lastSafePlatform = landing; }
                if (!TryLocalSupport(player.FeetPosition, out _)) { Stop(false); return; }
            }
            else
            {
                if (grounded && HasEndpointExitFootprint(player.FeetPosition, lastSafePlatform))
                { Stop(false); return; } // Stay at the actual feet position; ordinary walking owns the path now.
                stableTime = 0; unsupportedTime = grounded ? unsupportedTime + dt : 0;
            }
            if (player.FeetPosition.y < safePosition.y - fallRecoveryDepth || unsupportedTime > .8f) Recover();
        }

        private void Recover()
        {
            if (!lastSafePlatform || !lastSafePlatform.enabled || !lastSafePlatform.gameObject.activeInHierarchy)
            { Stop(false); return; }
            player.Teleport(safePosition);
            if (!player.BeginExternalMotion(this, body.radius, body.height)) { Stop(false); return; }
            horizontalVelocity = Vector3.zero; verticalVelocity = -2;
            grounded = false; stableTime = unsupportedTime = 0;
            player.MoveExternal(this, Vector3.zero, safeFacing, 0);
            jumpAssist.Reset();
            RecoveryCount++;
            court.ShowMessage("云雾托你回到刚才站稳的地方。看准落脚处，再跳一次。", 3);
        }
        private void Stop(bool recoverIfAirborne)
        {
            if (!active) return;
            bool ownsMotion = player && player.ExternalMotionOwnedBy(this);
            bool recover = recoverIfAirborne && ownsMotion && !grounded && lastSafePlatform
                && lastSafePlatform.enabled && lastSafePlatform.gameObject.activeInHierarchy;
            active = grounded = false;
            jumpAssist.Reset();
            if (court && court.CameraRig) court.CameraRig.SetPlatformingView(false);
            if (player) player.EndExternalMotion(this);
            if (recover) player.Teleport(safePosition);
            horizontalVelocity = Vector3.zero; verticalVelocity = stableTime = unsupportedTime = 0;
        }
        public void ResetTravel()
        {
            Stop(true); lastSafePlatform = null; RecoveryCount = 0;
        }
        private void OnDisable() => Stop(true);
        private void OnDestroy() => Stop(true);
        private void OnApplicationFocus(bool value) { focused = value; }
    }
}
