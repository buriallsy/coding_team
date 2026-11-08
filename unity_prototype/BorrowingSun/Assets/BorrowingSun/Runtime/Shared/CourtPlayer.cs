using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BorrowingSun
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class CourtPlayer : MonoBehaviour
    {
        [SerializeField] private CourtController court;
        [SerializeField] private MonoBehaviour gameplayOwner;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Vector3 spawnPosition = new Vector3(0, 0.1f, -11);
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private bool protectPuzzleEdges;
        private CharacterController body;
        private Vector3 lastSafePosition;
        private float verticalSpeed;
        private readonly RaycastHit[] groundHits = new RaycastHit[12];
        private readonly RaycastHit[] dropHits = new RaycastHit[24];
        private readonly Collider[] dropOverlaps = new Collider[24];
        private Func<Vector3, bool> dropLandingFilter;
        private float maximumDrop = 3.2f;
        private Vector3 lastMoveDirection, dropLanding;
        private float dropStartHeight, dropElapsed;
        private bool assistedMovement, pushingCart;
        private bool viewFacingActive;
        private Vector3 viewFacing;
        private MonoBehaviour externalMotionOwner;
        private bool externalMotionActive;
        private float regularBodyHeight, regularBodyRadius, regularStepOffset;
        private Vector3 regularBodyCenter;
        public bool IsExternallyDriven => externalMotionActive;
        public bool ExternalMotionOwnedBy(MonoBehaviour owner) => externalMotionActive && externalMotionOwner == owner;

        /// <summary>One explicit driver may supply displacement; this component retains the single character collision body.</summary>
        public bool BeginExternalMotion(MonoBehaviour owner, float radius = .55f, float height = 2.2f)
        {
            if (!owner || !owner.isActiveAndEnabled || !isActiveAndEnabled || IsSwimming || IsDropping || assistedMovement
                || externalMotionActive && externalMotionOwner != owner) return false;
            if (externalMotionActive) return true;
            if (!body) body = GetComponent<CharacterController>();
            if (!body || !body.enabled) return false;
            regularBodyHeight = body.height; regularBodyRadius = body.radius;
            regularBodyCenter = body.center; regularStepOffset = body.stepOffset;
            externalMotionOwner = owner; externalMotionActive = true;
            body.height = Mathf.Max(height, radius * 2 + .1f);
            body.radius = Mathf.Clamp(radius, .1f, body.height * .5f - .01f);
            body.center = Vector3.up * (body.height * .5f);
            body.stepOffset = Mathf.Min(.24f, body.height * .2f);
            verticalSpeed = 0; PlanarVelocity = Vector3.zero;
            return true;
        }

        public CollisionFlags MoveExternal(MonoBehaviour owner, Vector3 displacement, Vector3 facing, float seconds)
        {
            if (!ExternalMotionOwnedBy(owner) || !body || !body.enabled) return CollisionFlags.None;
            facing = Vector3.ProjectOnPlane(facing, Vector3.up);
            if (!viewFacingActive && facing.sqrMagnitude > .001f) FacingDirection = facing.normalized;
            Vector3 before = FeetPosition;
            CollisionFlags result = body.Move(displacement);
            PlanarVelocity = seconds > 0 ? Vector3.ProjectOnPlane(FeetPosition - before, Vector3.up) / seconds : Vector3.zero;
            return result;
        }

        public void EndExternalMotion(MonoBehaviour owner)
        {
            if (ExternalMotionOwnedBy(owner)) ReleaseExternalMotion();
        }
        private void ReleaseExternalMotion()
        {
            if (!externalMotionActive) return;
            externalMotionActive = false; externalMotionOwner = null;
            if (body)
            {
                body.radius = Mathf.Min(regularBodyRadius, body.height * .5f - .01f);
                body.height = regularBodyHeight; body.radius = regularBodyRadius;
                body.center = regularBodyCenter; body.stepOffset = regularStepOffset;
            }
            verticalSpeed = -2; PlanarVelocity = Vector3.zero;
        }
        public bool IsSwimming { get; private set; }
        public bool IsSurfaceSwimming { get; private set; }
        public float OxygenUrgency { get; set; }
        private float swimMin, swimMax;
        public void ConfigureSwimming(bool enabled, float minimumFeet = 0, float maximumFeet = 0)
        { IsSwimming=enabled;IsSurfaceSwimming=false;swimMin=minimumFeet;swimMax=maximumFeet;verticalSpeed=0;IsDropping=false;ReleaseAssistedMovement(); }
        // The water-area owner chooses a surface height; the existing character body
        // still resolves buoyancy, boarding edges and every horizontal collision.
        public void ConfigureSurfaceSwimming(bool enabled, float feetHeight=0)
        {
            ConfigureSwimming(enabled,feetHeight,feetHeight);
            IsSurfaceSwimming=enabled;
        }
        private Vector3 assistedTarget, assistedFacing;
        private ICourtGameplay Gameplay => gameplayOwner != null ? gameplayOwner as ICourtGameplay : court;

        public Vector3 FeetPosition => transform.position;
        public Vector3 SpawnPosition => spawnPosition;
        public bool PuzzleEdgeProtectionEnabled => protectPuzzleEdges;
        public bool IsDropping { get; private set; }
        public bool IsAutoPositioning => assistedMovement && !pushingCart;
        public bool IsPushingCart => assistedMovement && pushingCart;
        public Vector3 PlanarVelocity { get; private set; }
        public Vector3 FacingDirection { get; private set; } = Vector3.forward;
        public bool ViewFacingActive => viewFacingActive;
        // Applied platform rotation, wrapped to one turn. A following camera observes the delta
        // without deriving its heading from locomotion or introducing another movement owner.
        public float CarriedYaw { get; private set; }

        /// <summary>Opt-in strafe facing. The view owns heading; this component still owns movement and collision.</summary>
        public void SetViewFacing(Vector3 direction)
        {
            direction = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (!isActiveAndEnabled || assistedMovement || direction.sqrMagnitude < .0001f
                || float.IsNaN(direction.sqrMagnitude) || float.IsInfinity(direction.sqrMagnitude))
            { ClearViewFacing(); return; }
            viewFacingActive = true;
            viewFacing = direction.normalized;
            FacingDirection = viewFacing;
        }

        /// <summary>Release strafe facing without rotating the character; legacy movement resumes on the next input.</summary>
        public void ClearViewFacing() => viewFacingActive = false;

        internal void SetAssistedTarget(Vector3 target, Vector3 facing, bool pushing)
        {
            ClearViewFacing();
            assistedMovement = true;
            pushingCart = pushing;
            assistedTarget = target;
            assistedFacing = Vector3.ProjectOnPlane(facing, Vector3.up).normalized;
        }

        internal void ReleaseAssistedMovement()
        {
            assistedMovement = pushingCart = false;
            PlanarVelocity = Vector3.zero;
        }

        /// <summary>Opt in for no-jump puzzle spaces; older courtyard scenes keep their original movement.</summary>
        public void ConfigurePuzzleEdgeProtection(bool enabled = true)
        {
            protectPuzzleEdges = enabled;
        }

        public void ConfigureSafeDrop(Func<Vector3, bool> landingFilter, float maxDrop = 3.2f)
        {
            if (float.IsNaN(maxDrop) || float.IsInfinity(maxDrop) || maxDrop < 0.45f)
                throw new ArgumentOutOfRangeException(nameof(maxDrop));
            dropLandingFilter = landingFilter;
            maximumDrop = Mathf.Min(3.2f, maxDrop);
            IsDropping = false;
        }

        public void Initialize(CourtController owner, Camera camera, Vector3 spawn)
        {
            court = owner;
            InitializeGameplay(owner, camera, spawn);
        }

        public void InitializeGameplay(ICourtGameplay owner, Camera camera, Vector3 spawn)
        {
            gameplayOwner = owner as MonoBehaviour;
            viewCamera = camera;
            spawnPosition = spawn;
            body = GetComponent<CharacterController>();
            body.height = 1.8f;
            body.radius = 0.3f;
            body.center = new Vector3(0, 0.9f, 0);
            body.stepOffset = 0.24f;
            body.slopeLimit = 48;
            body.skinWidth = 0.03f;
            body.minMoveDistance = 0;
            lastSafePosition = spawn;
            if (!Application.isPlaying) ResetPlayer();
        }

        private void Awake()
        {
            body = GetComponent<CharacterController>();
            lastSafePosition = spawnPosition;
        }

        private void Update()
        {
            var gameplay = Gameplay;
            if (gameplay == null || viewCamera == null || body == null) return;
            var keyboard = Keyboard.current;
            if (externalMotionActive)
            {
                if (!externalMotionOwner || !externalMotionOwner.isActiveAndEnabled) ReleaseExternalMotion();
                else
                {
                    // The rider driver runs after this Update. Keep interaction dispatch here,
                    // exactly once, while skipping walking gravity, edge protection and hazard recovery.
                    if (keyboard != null)
                    {
                        if (keyboard.rKey.wasPressedThisFrame) { gameplay.ResetCourt(); return; }
                        if (keyboard.eKey.wasPressedThisFrame) gameplay.InteractPrimary();
                        if (keyboard.qKey.wasPressedThisFrame) gameplay.InteractSecondary();
                    }
                    return;
                }
            }
            Vector2 axis = Vector2.zero;
            if (keyboard != null && !gameplay.IsObserving && !IsDropping && !assistedMovement)
            {
                axis.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0)
                    - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
                axis.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0)
                    - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
            }
            axis = Vector2.ClampMagnitude(axis, 1);
            Vector3 forward = viewFacingActive ? viewFacing : Vector3.ProjectOnPlane(viewCamera.transform.forward, Vector3.up).normalized;
            Vector3 right = viewFacingActive ? Vector3.Cross(Vector3.up, forward) : Vector3.ProjectOnPlane(viewCamera.transform.right, Vector3.up).normalized;
            Vector3 horizontal = (forward * axis.y + right * axis.x) * moveSpeed;
            if(IsSwimming)
            {
                float vertical=0;
                if(!IsSurfaceSwimming&&keyboard!=null&&!gameplay.IsObserving)
                    vertical=(keyboard.spaceKey.isPressed?1:0)-(keyboard.leftCtrlKey.isPressed||keyboard.rightCtrlKey.isPressed?1:0);
                Vector3 swim=(forward*axis.y+right*axis.x+Vector3.up*vertical);
                swim=Vector3.ClampMagnitude(swim,1)*3.2f;
                if(!viewFacingActive&&horizontal.sqrMagnitude>.001f)FacingDirection=horizontal.normalized;
                float dy=Mathf.Clamp(FeetPosition.y+swim.y*Time.deltaTime,swimMin,swimMax)-FeetPosition.y;
                Vector3 before=FeetPosition;body.Move(new Vector3(swim.x*Time.deltaTime,dy,swim.z*Time.deltaTime));
                PlanarVelocity=Time.deltaTime>0?(FeetPosition-before)/Time.deltaTime:Vector3.zero;
                if(keyboard!=null){if(keyboard.rKey.wasPressedThisFrame)gameplay.ResetCourt();if(keyboard.eKey.wasPressedThisFrame)gameplay.InteractPrimary();if(keyboard.qKey.wasPressedThisFrame)gameplay.InteractSecondary();}
                return;
            }
            if (assistedMovement && !gameplay.IsObserving)
            {
                Vector3 remaining = Vector3.ProjectOnPlane(assistedTarget - FeetPosition, Vector3.up);
                horizontal = Time.deltaTime > 0 ? Vector3.ClampMagnitude(remaining / Time.deltaTime, moveSpeed) : Vector3.zero;
            }
            // Direction remains readable while normal edge protection prevents actual movement.
            if (horizontal.sqrMagnitude > 0.001f) lastMoveDirection = horizontal.normalized;
            if (assistedMovement && assistedFacing.sqrMagnitude > .1f) FacingDirection = assistedFacing;
            else if (!viewFacingActive && horizontal.sqrMagnitude > .001f) FacingDirection = horizontal.normalized;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame && !gameplay.IsObserving && !IsDropping && !assistedMovement)
                TryDropDown();
            if (IsDropping)
            {
                Vector3 remaining = Vector3.ProjectOnPlane(dropLanding - FeetPosition, Vector3.up);
                horizontal = Time.deltaTime > 0 ? Vector3.ClampMagnitude(remaining / Time.deltaTime, moveSpeed) : Vector3.zero;
                dropElapsed += Time.deltaTime;
            }
            if (body.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            verticalSpeed = Mathf.Max(-22, verticalSpeed - 22 * Time.deltaTime);
            Vector3 horizontalStep = horizontal * Time.deltaTime;
            if (protectPuzzleEdges && !IsDropping) horizontalStep = ConstrainToSupportedFloor(horizontalStep);
            Vector3 beforeMove = FeetPosition;
            body.Move(horizontalStep + Vector3.up * (verticalSpeed * Time.deltaTime));
            PlanarVelocity = Time.deltaTime > 0 ? Vector3.ProjectOnPlane(FeetPosition - beforeMove, Vector3.up) / Time.deltaTime : Vector3.zero;

            if (IsDropping && body.isGrounded && FeetPosition.y < dropStartHeight - 0.25f) IsDropping = false;
            else if (IsDropping && dropElapsed > 1.5f && body.isGrounded)
            {
                IsDropping = false;
                gameplay.ShowMessage("出台路径被挡住了，人物仍留在承托面上。");
            }

            if (gameplay.IsInsideHazard(FeetPosition)) Recover();
            else if (body.isGrounded && gameplay.IsPermanentGround(FeetPosition)) lastSafePosition = FeetPosition;

            if (keyboard == null) return;
            if (keyboard.rKey.wasPressedThisFrame) gameplay.ResetCourt();
            if (IsDropping || assistedMovement) return;
            if (keyboard.eKey.wasPressedThisFrame) gameplay.InteractPrimary();
            if (keyboard.qKey.wasPressedThisFrame) gameplay.InteractSecondary();
        }

        public bool TryGetDropLanding(out Vector3 landing)
        {
            landing = default;
            if (dropLandingFilter == null || IsDropping || assistedMovement || body == null || !body.enabled || !body.isGrounded
                || Gameplay == null || Gameplay.IsObserving || lastMoveDirection.sqrMagnitude < 0.5f) return false;
            Vector3 start = FeetPosition;
            // A short deliberate step off an adjacent ledge; never a leap across a valley.
            for (float forward = 0.5f; forward <= 1.51f; forward += 0.15f)
            {
                Vector3 probe = start + lastMoveDirection * forward;
                int count = Physics.RaycastNonAlloc(probe + Vector3.up * 0.15f, Vector3.down,
                    dropHits, maximumDrop + 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                if (count == dropHits.Length) continue;
                RaycastHit floor = default;
                float nearest = float.PositiveInfinity;
                for (int i = 0; i < count; i++)
                {
                    RaycastHit hit = dropHits[i];
                    if (IsOwnCollider(hit.collider) || hit.distance >= nearest) continue;
                    floor = hit;
                    nearest = hit.distance;
                }
                if (floor.collider == null || floor.normal.y < Mathf.Cos(body.slopeLimit * Mathf.Deg2Rad)) continue;
                float drop = start.y - floor.point.y;
                if (drop < 0.45f || drop > maximumDrop) continue;
                Vector3 candidate = new Vector3(probe.x, floor.point.y, probe.z);
                if (!dropLandingFilter(candidate) || !HasDropFootprint(candidate) || !DropCapsuleIsClear(candidate)) continue;
                Vector3 launch = new Vector3(candidate.x, start.y, candidate.z);
                if (!DropCapsuleIsClear(start) || !DropCapsuleIsClear(launch)
                    || !DropSweepIsClear(start, launch, null) || !DropSweepIsClear(launch, candidate, floor.collider)) continue;
                landing = candidate;
                return true;
            }
            return false;
        }

        public bool TryDropDown()
        {
            if (!TryGetDropLanding(out Vector3 landing))
            {
                Gameplay?.ShowMessage("前方没有可安全下落的平台，或有栏杆、货物挡住。", 3);
                return false;
            }
            dropLanding = landing;
            dropStartHeight = FeetPosition.y;
            dropElapsed = 0;
            IsDropping = true;
            Gameplay.ShowMessage("主动下台 · 落地后恢复行走。", 2);
            return true;
        }

        private bool HasDropFootprint(Vector3 feet)
        {
            float radius = body.radius + body.skinWidth;
            float tolerance = radius * Mathf.Tan(body.slopeLimit * Mathf.Deg2Rad) + 0.035f;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 0.25f;
                Vector3 sample = feet + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
                if (!TryFloor(sample, tolerance, out _)) return false;
            }
            return true;
        }

        private void DropCapsule(Vector3 feet, out Vector3 bottom, out Vector3 top, out float radius)
        {
            Vector3 scale = transform.lossyScale;
            float physicalRadius = body.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float segment = Mathf.Max(0, body.height * Mathf.Abs(scale.y) * 0.5f - physicalRadius);
            Vector3 center = feet + transform.TransformVector(body.center) + Vector3.up * (body.skinWidth + 0.025f);
            radius = physicalRadius + body.skinWidth;
            bottom = center - Vector3.up * segment;
            top = center + Vector3.up * segment;
        }

        private bool DropCapsuleIsClear(Vector3 feet)
        {
            DropCapsule(feet, out Vector3 bottom, out Vector3 top, out float radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, dropOverlaps,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == dropOverlaps.Length) return false;
            for (int i = 0; i < count; i++) if (!IsOwnCollider(dropOverlaps[i])) return false;
            return true;
        }

        private bool DropSweepIsClear(Vector3 from, Vector3 to, Collider landingFloor)
        {
            Vector3 delta = to - from;
            if (delta.sqrMagnitude < 0.000001f) return true;
            DropCapsule(from, out Vector3 bottom, out Vector3 top, out float radius);
            int count = Physics.CapsuleCastNonAlloc(bottom, top, radius, delta.normalized, dropHits,
                delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == dropHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = dropHits[i];
                if (IsOwnCollider(hit.collider)) continue;
                if (hit.collider == landingFloor && hit.distance >= delta.magnitude - 0.06f
                    && hit.normal.y >= Mathf.Cos(body.slopeLimit * Mathf.Deg2Rad)) continue;
                return false;
            }
            return true;
        }

        private bool IsOwnCollider(Collider collider) => collider == body || collider.transform.IsChildOf(transform);

        internal bool TryAssistedGround(Vector3 position, float tolerance, out Vector3 ground)
        {
            ground = position;
            if (!TryFloor(position, tolerance, out float floor)) return false;
            ground.y = floor;
            return TrySupportedFootprint(ground, out _) && AssistedCapsuleClear(ground);
        }

        internal bool CanWalkAssistedSegment(Vector3 from, Vector3 to)
        {
            Vector3 delta = Vector3.ProjectOnPlane(to - from, Vector3.up);
            int count = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / .18f));
            Vector3 cursor = from;
            for (int i = 1; i <= count; i++)
            {
                Vector3 next = from + delta * (i / (float)count);
                next.y = cursor.y;
                if (!TryAssistedGround(next, body.stepOffset + .06f, out next)) return false;
                AssistedCapsule(cursor, out Vector3 bottom, out Vector3 top, out float radius);
                Vector3 segment = next - cursor;
                if (segment.sqrMagnitude > .000001f)
                {
                    int hits = Physics.CapsuleCastNonAlloc(bottom, top, radius, segment.normalized, dropHits,
                        segment.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    if (hits == dropHits.Length) return false;
                    for (int h = 0; h < hits; h++)
                    {
                        RaycastHit hit = dropHits[h];
                        if (IsOwnCollider(hit.collider) || hit.normal.y >= Mathf.Cos(body.slopeLimit * Mathf.Deg2Rad)) continue;
                        return false;
                    }
                }
                cursor = next;
            }
            return Mathf.Abs(cursor.y - to.y) < .35f;
        }

        private void AssistedCapsule(Vector3 feet, out Vector3 bottom, out Vector3 top, out float radius)
        {
            // Match the controller's contact skin, so a player already touching the cart can walk away.
            radius = Mathf.Max(.1f, body.radius - body.skinWidth * .6f);
            Vector3 center = feet + body.center + Vector3.up * .05f;
            float segment = body.height * .5f - body.radius;
            bottom = center - Vector3.up * segment;
            top = center + Vector3.up * segment;
        }

        private bool AssistedCapsuleClear(Vector3 feet)
        {
            AssistedCapsule(feet, out Vector3 bottom, out Vector3 top, out float radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, dropOverlaps,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == dropOverlaps.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Collider obstacle = dropOverlaps[i];
                if (IsOwnCollider(obstacle)) continue;
                if (obstacle.Raycast(new Ray(feet + Vector3.up * .4f, Vector3.down), out RaycastHit floor, .8f)
                    && floor.normal.y >= Mathf.Cos(body.slopeLimit * Mathf.Deg2Rad)
                    && Mathf.Abs(floor.point.y - feet.y) <= .3f) continue;
                return false;
            }
            return true;
        }

        private Vector3 ConstrainToSupportedFloor(Vector3 step)
        {
            if (step.sqrMagnitude < 0.0000001f) return Vector3.zero;
            // Check the path, not only its endpoint: a slow frame must not hop over a narrow gap.
            int count = Mathf.Max(1, Mathf.CeilToInt(step.magnitude / 0.12f));
            Vector3 increment = step / count;
            Vector3 start = FeetPosition;
            Vector3 cursor = start;
            for (int i = 0; i < count; i++)
            {
                if (TrySupportedFootprint(cursor + increment, out float floor))
                {
                    cursor += increment;
                    cursor.y = floor;
                    continue;
                }

                // Keep movement along the lip responsive when diagonal input also points into a gap.
                Vector3 first = Mathf.Abs(increment.x) >= Mathf.Abs(increment.z)
                    ? new Vector3(increment.x, 0, 0) : new Vector3(0, 0, increment.z);
                Vector3 second = increment - first;
                bool moved = false;
                if (first.sqrMagnitude > 0 && TrySupportedFootprint(cursor + first, out floor))
                { cursor += first; cursor.y = floor; moved = true; }
                if (second.sqrMagnitude > 0 && TrySupportedFootprint(cursor + second, out floor))
                { cursor += second; cursor.y = floor; moved = true; }
                if (!moved) break;
            }
            // CharacterController owns vertical motion and slope collision response.
            return new Vector3(cursor.x - start.x, 0, cursor.z - start.z);
        }

        private bool TrySupportedFootprint(Vector3 feet, out float floor)
        {
            float stepHeight = body.stepOffset + body.skinWidth + 0.03f;
            if (!TryFloor(feet, stepHeight, out floor)) return false;
            // Leave most of the capsule on the deck instead of waiting until its centre has fallen.
            // Heights are relative to the centre hit so ordinary ramps still admit the whole foot.
            float inset = body.radius * 0.7f;
            float slopeHeight = inset * Mathf.Tan(body.slopeLimit * Mathf.Deg2Rad) + 0.04f;
            Vector3 atFloor = new Vector3(feet.x, floor, feet.z);
            return TryFloor(atFloor + Vector3.right * inset, slopeHeight, out _)
                && TryFloor(atFloor - Vector3.right * inset, slopeHeight, out _)
                && TryFloor(atFloor + Vector3.forward * inset, slopeHeight, out _)
                && TryFloor(atFloor - Vector3.forward * inset, slopeHeight, out _);
        }

        private bool TryFloor(Vector3 feet, float heightTolerance, out float floor)
        {
            float reach = heightTolerance + 0.02f;
            int count = Physics.RaycastNonAlloc(feet + Vector3.up * reach, Vector3.down,
                groundHits, reach * 2, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            floor = feet.y;
            bool found = false;
            float minimumNormal = Mathf.Cos(body.slopeLimit * Mathf.Deg2Rad);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = groundHits[i];
                if (hit.collider == body || hit.transform.IsChildOf(transform)) continue;
                if (hit.normal.y < minimumNormal || Mathf.Abs(hit.point.y - feet.y) > heightTolerance) continue;
                if (hit.distance >= nearest) continue;
                nearest = hit.distance;
                floor = hit.point.y;
                found = true;
            }
            return found;
        }

        /// <summary>Opt-in continuous carry for a translating and rotating platform. The caller owns support detection.</summary>
        public void CarryByPlatform(Vector3 delta, float yawDelta)
        {
            if (body == null || !body.enabled) return;
            Relocate(FeetPosition + delta);
            Quaternion turn = Quaternion.Euler(0, yawDelta, 0);
            FacingDirection = turn * FacingDirection;
            if (viewFacingActive) viewFacing = turn * viewFacing;
            CarriedYaw = Mathf.Repeat(CarriedYaw + yawDelta, 360);
        }

        public void CarryByPlatform(float delta)
        {
            if (body == null || !body.enabled || Mathf.Abs(delta) < 0.00001f) return;
            // The two decks and supported actor relocate atomically. Moving a collider into an
            // enabled CharacterController and then calling Move would apply depenetration twice.
            Relocate(FeetPosition + new Vector3(0, delta, 0));
        }

        public void Teleport(Vector3 position)
        {
            ReleaseExternalMotion();
            IsDropping = false;
            ClearViewFacing();
            ReleaseAssistedMovement();
            Relocate(position);
        }

        private void OnDisable() { ReleaseExternalMotion(); ClearViewFacing(); ReleaseAssistedMovement(); }

        private void Relocate(Vector3 position)
        {
            if (body == null) body = GetComponent<CharacterController>();
            bool wasEnabled = body.enabled;
            body.enabled = false;
            transform.position = position;
            body.enabled = wasEnabled;
            verticalSpeed = -2;
        }

        public void ResetPlayer()
        {
            lastSafePosition = spawnPosition;
            Teleport(spawnPosition);
        }

        public void Recover()
        {
            Teleport(lastSafePosition + Vector3.up * 0.06f);
            Gameplay?.ShowMessage("已回到最近的安全岸边，苗车和水位保留。R 可重新开始整关。");
        }
    }
}
