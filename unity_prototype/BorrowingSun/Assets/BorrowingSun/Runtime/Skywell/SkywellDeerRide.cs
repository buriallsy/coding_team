using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BorrowingSun
{
    /// <summary>Optional, player-controlled cloud crossing. Owns riding motion; CourtPlayer retains the single collision body.</summary>
    [DefaultExecutionOrder(10)]
    public sealed class SkywellDeerRide : MonoBehaviour
    {
        [SerializeField] private SkywellController court;
        [SerializeField] private Transform deerVisual, saddle, mountPoint, home;
        [SerializeField] private Collider[] safePlatforms = Array.Empty<Collider>();
        [Header("白鹿骑乘")]
        [SerializeField, Min(1)] private float moveSpeed = 6;
        [SerializeField, Min(1)] private float groundAcceleration = 22;
        [SerializeField] private SkywellJumpAssist jumpAssist = new SkywellJumpAssist();
        [SerializeField, Min(.2f)] private float jumpHeight = 2.6f;
        [SerializeField, Min(1)] private float gravity = 20;
        [SerializeField, Min(1)] private float fallRecoveryDepth = 5;
        [SerializeField, Min(.2f)] private float interactionRadius = 2.1f;
        [SerializeField, Range(.3f,.8f)] private float collisionRadius = .55f;
        [SerializeField, Min(1.8f)] private float collisionHeight = 2.2f;
        [SerializeField, Range(0,2)] private float cameraHeightOffset = .65f;
        [Header("首次送达后的归途")]
        [SerializeField, Min(.1f)] private float returnLandingPause = .45f;
        [SerializeField, Min(1.5f)] private float returnParkingDistance = 2.5f;
        private SkywellCloudFootTravel footDriver;
        private int outboundPlatformIndex, returnPlatformIndex;
        private bool footRouteUnlocked, returning, returnBlocked;
        private float returnPause, returnArcTime, returnArcDuration;
        private Vector3 returnLaunch, returnLanding, returnArcVelocity;
        private bool returnPlanReady, returnAirborne;
        private readonly RaycastHit[] returnHits = new RaycastHit[32];
        private CourtPlayer player;
        private CharacterController body;
        private StandardHumanoidVisual riderVisual;
        private Transform originalParent;
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private bool initialPoseReady, available, mounted, grounded, focused = true;
        private Vector3 horizontalVelocity, safePosition, safeFacing;
        private float verticalVelocity, stableTime, unsupportedTime, gaitPhase;
        private Collider lastSafePlatform;
        private readonly RaycastHit[] groundHits = new RaycastHit[24];
        private readonly Collider[] overlaps = new Collider[24];
        private readonly Transform[] legs = new Transform[4];
        private readonly Quaternion[] legRest = new Quaternion[4];
        private static readonly string[] LegNames = { "LF", "RF", "LB", "RB" };
        private static readonly float[] ReturnSteeringAngles = { 0, 35, -35, 70, -70 };
        private static readonly Vector3[] FootDirections = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };

        public bool IsAvailable => available;
        public bool FootRouteUnlocked => footRouteUnlocked;
        public bool IsReturning => returning;
        public bool ReturnBlocked => returnBlocked;
        public int ReturnLandingCount { get; private set; }
        public SkywellCloudFootTravel FootDriver => footDriver;
        public void RegisterFootDriver(SkywellCloudFootTravel driver) => footDriver = driver;
        public bool IsMounted => mounted;
        public bool IsGrounded => mounted && grounded;
        public Transform DeerTransform => deerVisual;
        public Transform MountPoint => mountPoint;
        public Collider LastSafePlatform => lastSafePlatform;
        public Collider[] SafePlatforms => safePlatforms;
        public int RecoveryCount { get; private set; }
        public SkywellJumpAssist JumpAssist => jumpAssist;
        public string Prompt => mounted ? grounded ? "WASD 骑行 · Space 跳跃 · E 下鹿" : "WASD 调整落点 · 落稳后 E 下鹿"
            : available && NearDeer() ? "白鹿俯身相邀 · E 骑乘" : null;

        public void Configure(SkywellController owner, Transform visual, Transform saddleAnchor, Transform interactionPoint,
            Collider[] landingSurfaces, Transform homePoint)
        {
            court = owner; deerVisual = visual; saddle = saddleAnchor; mountPoint = interactionPoint;
            safePlatforms = landingSurfaces ?? Array.Empty<Collider>(); home = homePoint;
            CacheBindings();
        }
        public void SetAvailable(bool value)
        {
            if (!value && mounted) ReleaseMount(true);
            available = value;
        }
        // Grove owns the approach/eating sequence. It submits poses instead of competing for this root Transform.
        public bool SetUnriddenPose(Vector3 position, Quaternion rotation)
        {
            if (mounted || available || returning || !deerVisual) return false;
            Vector3 velocity = Time.deltaTime > 0 ? Vector3.ProjectOnPlane(position - deerVisual.position, Vector3.up) / Time.deltaTime : Vector3.zero;
            deerVisual.SetPositionAndRotation(position, rotation);
            AnimateLegs(velocity.magnitude, true, velocity);
            return true;
        }
        private void Awake() { focused = Application.isFocused; CacheBindings(); }
        private void CacheBindings()
        {
            if (court)
            {
                player = court.Player;
                if (player) { body = player.GetComponent<CharacterController>(); riderVisual = player.GetComponentInChildren<StandardHumanoidVisual>(true); }
            }
            if (!deerVisual) return;
            if (!initialPoseReady)
            {
                originalParent = deerVisual.parent; initialPosition = deerVisual.position; initialRotation = deerVisual.rotation;
                initialPoseReady = true;
            }
            var children = deerVisual.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < legs.Length; i++)
                if (!legs[i]) foreach (var child in children) if (child.name == LegNames[i]) { legs[i] = child; legRest[i] = child.localRotation; break; }
        }
        private bool NearDeer()
        {
            if (!player || !deerVisual || !mountPoint || !isActiveAndEnabled) return false;
            return Vector3.Distance(player.FeetPosition, mountPoint.position) <= interactionRadius
                || Vector3.Distance(player.FeetPosition, deerVisual.position) <= 1.65f;
        }
        public bool TryPrimary()
        {
            if (!isActiveAndEnabled || !court || court.ObservationInputBlocked || court.WeatherInputBlocked) return false;
            if (mounted)
            {
                if (!grounded) { court.ShowMessage("先让白鹿落稳，再从鞍侧下来。", 2); return true; }
                if (!TryDismountPoint(out Vector3 point)) { court.ShowMessage("这里没有能站稳的落脚处，先骑到云台中央。", 3); return true; }
                ReleaseMount(false); player.Teleport(point);
                court.ShowMessage("白鹿留在身旁，靠近仍可骑乘。", 3);
                return true;
            }
            if (!available || !NearDeer()) return false;
            CacheBindings();
            if (!body || !player || player.IsSwimming || player.IsDropping || player.IsAutoPositioning || player.IsPushingCart
                || court.Journey && court.Journey.ControlsMovement || court.Voyage && court.Voyage.Phase >= SkywellVoyagePhase.Boarding && court.Voyage.Phase <= SkywellVoyagePhase.Docking)
                return false;
            Vector3 oldPosition = player.FeetPosition;
            Vector3 position = deerVisual.position + Vector3.up * .03f;
            if (!TrySafeFootprint(position, out Collider support)) { court.ShowMessage("等白鹿在云台上站稳，再骑上去。", 3); return true; }
            player.Teleport(position);
            if (!player.BeginExternalMotion(this, collisionRadius, collisionHeight)) { player.Teleport(oldPosition); return true; }
            mounted = true; grounded = false; stableTime = unsupportedTime = 0;
            jumpAssist.Reset();
            if (court.CameraRig) court.CameraRig.SetPlatformingView(true);
            horizontalVelocity = Vector3.zero; verticalVelocity = -2;
            safePosition = position; safeFacing = player.FacingDirection; lastSafePlatform = support;
            deerVisual.SetParent(player.transform, true); deerVisual.localPosition = Vector3.zero;
            deerVisual.rotation = Quaternion.LookRotation(player.FacingDirection, Vector3.up);
            if (riderVisual) riderVisual.SetRidingPose(saddle);
            if (court.CameraRig) court.CameraRig.SetFollowHeightOffset(cameraHeightOffset);
            court.ShowMessage("白鹿稳稳托住你。WASD 骑行，Space 跃向下一片云台。", 5);
            return true;
        }
        private void Update()
        {
            if (returning)
            {
                if (!court || !court.isActiveAndEnabled || !focused || court.ObservationInputBlocked || court.WeatherInputBlocked) return;
                TickReturn(Time.deltaTime);
                return;
            }
            if (!mounted) return;
            if (!court || !court.isActiveAndEnabled) { ReleaseMount(true); return; }
            if (!player || !player.isActiveAndEnabled || !player.ExternalMotionOwnedBy(this)) { ReleaseMount(false); return; }
            if (!focused || !court || court.ObservationInputBlocked || court.WeatherInputBlocked) { jumpAssist.Reset(); return; }
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            var keyboard = Keyboard.current;
            Vector2 axis = Vector2.zero;
            if (keyboard != null)
            {
                axis.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
                axis.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
            }
            axis = Vector2.ClampMagnitude(axis, 1);
            Vector3 forward = court.CameraRig.PlanarMovementForward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 desired = (forward * axis.y + right * axis.x) * moveSpeed;
            horizontalVelocity = jumpAssist.Steer(horizontalVelocity, desired, grounded, groundAcceleration, dt);
            jumpAssist.Tick(dt, grounded && TryRouteCenterSupport(player.FeetPosition, out _),
                keyboard != null && keyboard.spaceKey.wasPressedThisFrame);
            if (jumpAssist.Consume())
            {
                if (TrySafeFootprint(player.FeetPosition, out Collider launch))
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
            bool supported = grounded && TrySafeFootprint(player.FeetPosition, out landing);
            if (supported)
            {
                stableTime += dt; unsupportedTime = 0;
                if (stableTime > .12f)
                {
                    safePosition = player.FeetPosition; safeFacing = player.FacingDirection; lastSafePlatform = landing;
                    RecordStableRouteLanding(landing);
                    if (!mounted) return;
                }
            }
            else { stableTime = 0; unsupportedTime = grounded ? unsupportedTime + dt : 0; }
            deerVisual.rotation = Quaternion.RotateTowards(deerVisual.rotation, Quaternion.LookRotation(player.FacingDirection, Vector3.up), 720 * dt);
            AnimateLegs(player.PlanarVelocity.magnitude, grounded, player.PlanarVelocity);
            if (player.FeetPosition.y < safePosition.y - fallRecoveryDepth || unsupportedTime > 1.2f) RecoverToSafePlatform();
        }
        private void AnimateLegs(float speed, bool onGround, Vector3 velocity)
        {
            if (!deerVisual) return;
            float dt = Mathf.Max(0, Time.deltaTime);
            gaitPhase += speed * dt * 3.5f;
            Vector3 local = deerVisual.InverseTransformDirection(velocity);
            for (int i = 0; i < legs.Length; i++)
            {
                if (!legs[i]) continue;
                float swing = onGround ? Mathf.Sin(gaitPhase + (i == 0 || i == 3 ? 0 : Mathf.PI)) * Mathf.Clamp01(speed / 2) * 22 : (i < 2 ? -30 : 25);
                float forwardWeight = local.sqrMagnitude > .001f ? Mathf.Clamp(local.z / Mathf.Max(.01f, velocity.magnitude), -1, 1) : 1;
                float sideWeight = local.sqrMagnitude > .001f ? Mathf.Clamp(local.x / Mathf.Max(.01f, velocity.magnitude), -1, 1) : 0;
                legs[i].localRotation = Quaternion.Slerp(legs[i].localRotation,
                    legRest[i] * Quaternion.Euler(onGround ? swing * forwardWeight : swing, 0, onGround ? -swing * sideWeight : 0), 1 - Mathf.Exp(-dt * 18));
            }
        }
        private bool TrySurface(Vector3 feet, out RaycastHit surface)
        {
            surface = default; float distance = float.PositiveInfinity;
            int count = Physics.RaycastNonAlloc(feet + Vector3.up * .25f, Vector3.down, groundHits, .65f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == groundHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var hit = groundHits[i];
                if (!hit.collider || player && hit.collider.transform.IsChildOf(player.transform) || hit.distance >= distance) continue;
                surface = hit; distance = hit.distance;
            }
            return surface.collider && surface.normal.y > .7f && Array.IndexOf(safePlatforms, surface.collider) >= 0;
        }
        public bool TryRouteCenterSupport(Vector3 feet, out Collider support)
        {
            bool found = TrySurface(feet, out RaycastHit hit);
            support = found ? hit.collider : null;
            return found && Mathf.Abs(hit.point.y - feet.y) <= .18f;
        }
        private bool TrySafeFootprint(Vector3 feet, out Collider support) => TryRouteFootprint(feet, collisionRadius, out support);
        public bool TryRouteFootprint(Vector3 feet, float radius, out Collider support)
        {
            support = null;
            if (!TrySurface(feet, out RaycastHit center)) return false;
            float inset = Mathf.Max(.1f, radius) * .8f;
            foreach (Vector3 direction in FootDirections)
            {
                if (!TrySurface(feet + direction * inset, out RaycastHit edge) || Mathf.Abs(edge.point.y - center.point.y) > .18f) return false;
            }
            support = center.collider; return true;
        }
        private bool TryDismountPoint(out Vector3 result)
        {
            result = default;
            Vector3 forward = player.FacingDirection, right = Vector3.Cross(Vector3.up, forward);
            Vector3[] directions = { right, -right, -forward, forward };
            foreach (Vector3 direction in directions)
            {
                Vector3 point = player.FeetPosition + direction * .95f;
                if (!TrySurface(point, out RaycastHit floor)) continue;
                point.y = floor.point.y + .03f;
                int count = Physics.OverlapCapsuleNonAlloc(point + Vector3.up * .34f, point + Vector3.up * 1.5f, .29f,
                    overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                bool clear = count < overlaps.Length;
                for (int i = 0; i < count && clear; i++)
                    if (overlaps[i] && !overlaps[i].transform.IsChildOf(player.transform)) clear = false;
                if (!clear) continue;
                result = point; return true;
            }
            return false;
        }
        private Vector3 RecoveryPoint()
        {
            if (lastSafePlatform && lastSafePlatform.enabled && lastSafePlatform.gameObject.activeInHierarchy) return safePosition;
            foreach (var platform in safePlatforms)
            {
                if (!platform || !platform.enabled || !platform.gameObject.activeInHierarchy) continue;
                var bounds = platform.bounds;
                if (platform.Raycast(new Ray(new Vector3(bounds.center.x, bounds.max.y + 1, bounds.center.z), Vector3.down), out RaycastHit hit, bounds.size.y + 2))
                    return hit.point + Vector3.up * .03f;
            }
            return player ? player.SpawnPosition : (home ? home.position : initialPosition);
        }
        private void RecoverToSafePlatform()
        {
            Vector3 point = RecoveryPoint();
            bool wasViewFacing = player.ViewFacingActive;
            Vector3 heading = player.FacingDirection;
            player.Teleport(point);
            if (!player.BeginExternalMotion(this, collisionRadius, collisionHeight)) { ReleaseMount(false); return; }
            horizontalVelocity = Vector3.zero; verticalVelocity = -2; grounded = false; stableTime = unsupportedTime = 0;
            // Retain shoulder orientation; fixed view resumes the last safe facing through the normal movement entry.
            if (wasViewFacing) player.SetViewFacing(heading);
            player.MoveExternal(this, Vector3.zero, safeFacing, 0);
            jumpAssist.Reset();
            RecoveryCount++;
            court.ShowMessage("白鹿踏回刚才站稳的云台。看准落点，再试一次。", 4);
        }
        private void ReleaseMount(bool moveToSafety)
        {
            if (!mounted) return;
            Vector3 recovery = moveToSafety ? RecoveryPoint() : Vector3.zero;
            mounted = false; grounded = false;
            jumpAssist.Reset();
            if (court && court.CameraRig) court.CameraRig.SetPlatformingView(false);
            if (riderVisual) riderVisual.SetRidingPose(null);
            if (court && court.CameraRig) court.CameraRig.SetFollowHeightOffset(0);
            if (player) player.EndExternalMotion(this);
            if (deerVisual) deerVisual.SetParent(originalParent, true);
            for (int i = 0; i < legs.Length; i++) if (legs[i]) legs[i].localRotation = legRest[i];
            horizontalVelocity = Vector3.zero; verticalVelocity = 0; stableTime = unsupportedTime = 0;
            if (moveToSafety && player)
            {
                if (deerVisual) deerVisual.position = recovery;
                player.Teleport(recovery);
            }
        }
        private void RecordStableRouteLanding(Collider support)
        {
            if (footRouteUnlocked || safePlatforms.Length < 3) return;
            int index = Array.IndexOf(safePlatforms, support);
            if (index == 0) outboundPlatformIndex = 0;
            else if (index == outboundPlatformIndex + 1) outboundPlatformIndex = index;
            if (outboundPlatformIndex != safePlatforms.Length - 1 || stableTime < .3f) return;

            // This is earned by the real ordered landing chain. Dismount at the current supported
            // feet, without relocating the player or granting a route from a trigger volume.
            footRouteUnlocked = true;
            ReleaseMount(false);
            available = false; returning = true; returnBlocked = false;
            returnPlatformIndex = safePlatforms.Length - 2; ReturnLandingCount = 0;
            returnPause = returnLandingPause; returnPlanReady = returnAirborne = false;
            if (court) court.ShowMessage("白鹿稳稳放下你，踏云返回果树旁。云路已记住你的脚步，按 Space 也能跃过。", 6);
        }

        /// <summary>Ballistic reach uses the same height, gravity and speed limit as the ridden deer.</summary>
        public static bool TryReturnFlight(Vector3 from, Vector3 to, float height, float acceleration,
            float maximumSpeed, out Vector3 velocity, out float duration)
        {
            velocity = Vector3.zero; duration = 0;
            if (height <= 0 || acceleration <= 0 || maximumSpeed <= 0 || float.IsNaN(height) || float.IsInfinity(height)
                || float.IsNaN(acceleration) || float.IsInfinity(acceleration) || float.IsNaN(maximumSpeed) || float.IsInfinity(maximumSpeed)) return false;
            float vertical = Mathf.Sqrt(2 * acceleration * height);
            float discriminant = vertical * vertical - 2 * acceleration * (to.y - from.y);
            if (discriminant <= .0001f) return false;
            duration = (vertical + Mathf.Sqrt(discriminant)) / acceleration;
            Vector3 horizontal = Vector3.ProjectOnPlane(to - from, Vector3.up) / duration;
            if (float.IsNaN(duration) || float.IsInfinity(duration) || float.IsNaN(horizontal.sqrMagnitude) || float.IsInfinity(horizontal.sqrMagnitude)
                || horizontal.magnitude > maximumSpeed + .001f) { duration = 0; return false; }
            velocity = horizontal + Vector3.up * vertical;
            return true;
        }

        private bool PlatformFootprint(Collider platform, Vector3 near, out Vector3 point)
        {
            point = default;
            if (!platform || !platform.enabled || !platform.gameObject.activeInHierarchy) return false;
            Bounds bounds = platform.bounds;
            if (!platform.Raycast(new Ray(new Vector3(near.x, bounds.max.y + 1, near.z), Vector3.down),
                out RaycastHit center, bounds.size.y + 2) || center.normal.y < .7f) return false;
            foreach (var offset in FootDirections)
            {
                Vector3 origin = new Vector3(near.x, bounds.max.y + 1, near.z) + offset * collisionRadius * .8f;
                if (!platform.Raycast(new Ray(origin, Vector3.down), out RaycastHit edge, bounds.size.y + 2)
                    || edge.normal.y < .7f || Mathf.Abs(edge.point.y - center.point.y) > .12f) return false;
            }
            point = center.point + Vector3.up * .03f;
            return true;
        }

        private bool ReturnEdge(Collider platform, Vector3 toward, out Vector3 point)
        {
            point = default;
            if (!platform || !PlatformFootprint(platform, platform.bounds.center, out Vector3 center)) return false;
            Vector3 direction = Vector3.ProjectOnPlane(toward - center, Vector3.up).normalized;
            point = center;
            float distance = platform.bounds.size.magnitude;
            // Sample actual polygon tops, not an AABB used as imaginary landing ground.
            for (float step = .12f; step < distance; step += .12f)
            {
                if (!PlatformFootprint(platform, center + direction * step, out Vector3 candidate)) break;
                point = candidate;
            }
            // Keep a little more space than the five-point footprint to make every landing legible.
            if (PlatformFootprint(platform, point - direction * .12f, out Vector3 inset)) point = inset;
            return true;
        }

        private bool PrepareReturnHop()
        {
            if (returnPlatformIndex < 0 || returnPlatformIndex + 1 >= safePlatforms.Length) return false;
            var source = safePlatforms[returnPlatformIndex + 1];
            var destination = safePlatforms[returnPlatformIndex];
            if (!source || !destination || !ReturnEdge(source, destination.bounds.center, out returnLaunch)
                || !ReturnEdge(destination, source.bounds.center, out returnLanding)) return false;
            if (!TryReturnFlight(returnLaunch, returnLanding, jumpHeight, gravity, moveSpeed,
                out returnArcVelocity, out returnArcDuration)) return false;
            // Reject an obstructed arc before takeoff. Per-frame sweeps still guard moving obstacles.
            Vector3 previous = returnLaunch;
            int samples = Mathf.Max(12, Mathf.CeilToInt(returnArcDuration / .04f));
            for (int i = 1; i <= samples; i++)
            {
                float time = returnArcDuration * i / samples;
                Vector3 next = returnLaunch + returnArcVelocity * time + Vector3.down * (.5f * gravity * time * time);
                if (!ReturnSegmentClear(previous, next)) return false;
                previous = next;
            }
            returnArcTime = 0; returnPlanReady = true; returnAirborne = false;
            returnBlocked = false;
            return true;
        }

        private bool ReturnSegmentClear(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            if (delta.sqrMagnitude < .000001f) return true;
            float radius = collisionRadius * .85f;
            Vector3 bottom = from + Vector3.up * (radius + .06f);
            Vector3 top = from + Vector3.up * Mathf.Max(radius + .06f, collisionHeight - radius);
            int overlapsAtStart = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, overlaps,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (overlapsAtStart == overlaps.Length) return false;
            for (int i = 0; i < overlapsAtStart; i++)
                if (overlaps[i] && !(player && overlaps[i].transform.IsChildOf(player.transform))
                    && !(deerVisual && overlaps[i].transform.IsChildOf(deerVisual))) return false;
            int count = Physics.CapsuleCastNonAlloc(bottom, top, radius, delta.normalized, returnHits,
                delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == returnHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var hit = returnHits[i];
                if (!hit.collider || player && hit.collider.transform.IsChildOf(player.transform)
                    || deerVisual && hit.collider.transform.IsChildOf(deerVisual)) continue;
                // The capsule may touch the authored top at the very end of a landing; never
                // disregard side walls, a roof, or a destination below its actual solid surface.
                if (Array.IndexOf(safePlatforms, hit.collider) >= 0 && hit.normal.y > .7f && to.y >= hit.point.y) continue;
                return false;
            }
            return true;
        }

        private bool WalkReturnToLaunch(float dt, bool beginHop = true)
        {
            Vector3 from = deerVisual.position;
            Vector3 delta = Vector3.ProjectOnPlane(returnLaunch - from, Vector3.up);
            if (delta.magnitude < .035f)
            {
                // Millimetre-scale alignment remains swept and grounded, never a route teleport.
                if (!ReturnSegmentClear(from, returnLaunch)) { returnBlocked = true; return false; }
                deerVisual.position = returnLaunch;
                returnAirborne = beginHop; returnArcTime = 0; returnBlocked = false;
                return true;
            }
            Vector3 direction = delta.normalized;
            float step = Mathf.Min(moveSpeed * .7f * dt, delta.magnitude);
            // Normal return starts close to the next launch. When scenery intervenes, take a
            // small supported sidestep; no movement goes through a solid or outside the platform.
            foreach (float angle in ReturnSteeringAngles)
            {
                Vector3 travel = Quaternion.Euler(0, angle, 0) * direction;
                Vector3 desired = from + travel * step;
                if (!PlatformFootprint(safePlatforms[returnPlatformIndex + 1], desired, out Vector3 next)
                    || !ReturnSegmentClear(from, next)) continue;
                Vector3 velocity = (next - from) / Mathf.Max(.0001f, dt);
                deerVisual.SetPositionAndRotation(next, Quaternion.Slerp(deerVisual.rotation,
                    Quaternion.LookRotation(travel), 1 - Mathf.Exp(-dt * 9)));
                AnimateLegs(velocity.magnitude, true, velocity);
                returnBlocked = false;
                return false;
            }
            returnBlocked = true; AnimateLegs(0, true, Vector3.zero); return false;
        }

        private bool PrepareReturnParking()
        {
            if (safePlatforms.Length == 0 || !safePlatforms[0]) return false;
            Collider island = safePlatforms[0];
            Vector3 inward = Vector3.ProjectOnPlane(island.bounds.center - deerVisual.position, Vector3.up);
            if (inward.magnitude < returnParkingDistance) return false;
            Vector3 goal = deerVisual.position + inward.normalized * returnParkingDistance;
            if (!PlatformFootprint(island, goal, out returnLaunch)) return false;
            returnPlanReady = true; returnAirborne = false; returnBlocked = false;
            return true;
        }

        private void TickReturn(float dt)
        {
            if (!deerVisual || dt <= 0) return;
            if (returnPause > 0)
            {
                returnPause = Mathf.Max(0, returnPause - dt); AnimateLegs(0, true, Vector3.zero); return;
            }
            if (returnPlatformIndex < 0)
            {
                // The final hop is complete, but its landing is still the traveller's approach.
                // Walk inward on real island support before offering another ride.
                if (!returnPlanReady && !PrepareReturnParking()) { returnBlocked = true; returnPause = .5f; return; }
                if (!WalkReturnToLaunch(dt, false)) return;
                returning = false; available = true; returnBlocked = false; returnPlanReady = false;
                AnimateLegs(0, true, Vector3.zero);
                return;
            }
            if (!returnPlanReady && !PrepareReturnHop()) { returnBlocked = true; returnPause = .5f; return; }
            if (!returnAirborne) { WalkReturnToLaunch(dt); return; }
            float nextTime = Mathf.Min(returnArcDuration, returnArcTime + dt);
            Vector3 next = returnLaunch + returnArcVelocity * nextTime + Vector3.down * (.5f * gravity * nextTime * nextTime);
            if (!ReturnSegmentClear(deerVisual.position, next)) { returnBlocked = true; return; }
            returnBlocked = false;
            Vector3 direction = Vector3.ProjectOnPlane(returnArcVelocity, Vector3.up).normalized;
            deerVisual.SetPositionAndRotation(next, Quaternion.LookRotation(direction));
            AnimateLegs(moveSpeed, false, returnArcVelocity);
            returnArcTime = nextTime;
            if (returnArcTime < returnArcDuration) return;
            if (!PlatformFootprint(safePlatforms[returnPlatformIndex], next, out Vector3 supported)
                || Vector3.Distance(next, supported) > .06f) { returnBlocked = true; return; }
            ReturnLandingCount++;
            returnPlatformIndex--; returnPlanReady = returnAirborne = false;
            returnPause = returnLandingPause;
        }

        public void ResetRide()
        {
            if (footDriver) footDriver.ResetTravel();
            returning = returnAirborne = returnPlanReady = returnBlocked = footRouteUnlocked = false;
            outboundPlatformIndex = 0; returnPlatformIndex = -1; ReturnLandingCount = 0;
            returnPause = returnArcTime = 0;
            ReleaseMount(true); available = false; RecoveryCount = 0; lastSafePlatform = null;
            if (court && court.CameraRig) court.CameraRig.SetFollowHeightOffset(0);
            if (deerVisual && initialPoseReady)
            {
                deerVisual.SetParent(originalParent, true);
                deerVisual.SetPositionAndRotation(home ? home.position : initialPosition, home ? home.rotation : initialRotation);
            }
            for (int i = 0; i < legs.Length; i++) if (legs[i]) legs[i].localRotation = legRest[i];
        }
        private void OnDisable() { ReleaseMount(true); }
        private void OnDestroy() { ReleaseMount(true); }
        private void OnApplicationFocus(bool value) { focused = value; }
    }
}
