using UnityEngine;
using UnityEngine.InputSystem;

namespace BorrowingSun
{
    [DefaultExecutionOrder(-20)]
    public sealed class CourtCamera : MonoBehaviour
    {
        [SerializeField] private CourtController court;
        [SerializeField] private CourtPlayer player;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Vector3 overviewPosition = new Vector3(19, 25, -27);
        [SerializeField] private Vector3 overviewTarget = new Vector3(1, 1.5f, -0.5f);
        [SerializeField] private float overviewSize = 14f;
        [SerializeField] private Vector3 pavilionPosition = new Vector3(-5, 2.5f, -7.6f);
        [SerializeField] private Vector3 pavilionTarget = new Vector3(1, 3.1f, 2);
        [SerializeField] private bool followPlayer;
        [SerializeField] private float followSize = 10f;
        [SerializeField] private float followSmoothing = 0.18f;
        [Header("L5 fixed / free shoulder orbit")]
        [SerializeField, Tooltip("Opt-in only. Leave disabled in existing L1/L3 scenes to retain their camera controls.")]
        private bool fixedAndShoulderControls;
        [SerializeField, Min(.25f)] private float shoulderDistance = 3.2f;
        [SerializeField] private float shoulderSideOffset = .55f;
        [SerializeField, Min(.25f)] private float shoulderHeight = 1.6f;
        [SerializeField, Range(35, 85)] private float shoulderFieldOfView = 60;
        [SerializeField, Min(.001f), Tooltip("Free orbit degrees per mouse pixel. Changes only camera angles, never character facing.")]
        private float shoulderSensitivity = .14f;
        [SerializeField, Tooltip("Negative angles look up. Keep an upward range so high collectors can be aimed at.")]
        private Vector2 shoulderPitchLimits = new Vector2(-65, 70);
        [SerializeField, Range(-85, 85)] private float shoulderInitialPitch = 12;
        [SerializeField, Range(.05f, .4f)] private float shoulderCollisionRadius = .2f;
        [SerializeField, Min(0)] private float shoulderWallPadding = .06f;
        [Header("Cloud traversal framing / local only")]
        [SerializeField, Min(.25f)] private float platformingDistance = 5.8f;
        [SerializeField, Range(0, 30)] private float platformingPitchOffset = 12;
        [SerializeField, Range(35, 85)] private float platformingFieldOfView = 65;
        private bool platformingView;
        private float platformingWeight;
        public bool PlatformingViewActive => platformingView;
        public void SetPlatformingView(bool active) => platformingView = active;
        // Input follows the requested yaw this frame, rather than the camera Transform from last LateUpdate.
        public Vector3 PlanarMovementForward => IsShoulderView && !GameplayPoseFrozen
            ? Quaternion.Euler(0, shoulderYaw, 0) * Vector3.forward
            : viewCamera ? Vector3.ProjectOnPlane(viewCamera.transform.forward, Vector3.up).normalized : Vector3.forward;

        private bool shoulderView, shoulderLookReady, pointerInteraction;
        private bool applicationFocused = true, pointerReleasedByUser, ownsPointerLock, pointerLockRequested;
        private float shoulderYaw, shoulderPitch;
        private int ignoreLookThroughFrame = -1;
        private readonly RaycastHit[] shoulderHits = new RaycastHit[32];

        [Header("Temporary interaction framing")]
        [SerializeField, Range(30, 75)] private float interactionPitch = 50;
        [SerializeField, Range(1, 1.7f)] private float interactionFramePadding = 1.2f;
        private bool interactionFraming;
        private Vector3 interactionPosition, savedInteractionPosition;
        private Quaternion interactionRotation, savedInteractionRotation;
        private float savedInteractionFov;
        private bool savedInteractionOrthographic;
        public bool InteractionFramingActive => interactionFraming;

        /// <summary>Frame physical interaction bounds without changing the selected gameplay mode or character heading.</summary>
        public void SetInteractionFraming(bool active, Bounds bounds = default)
        {
            active &= IsShoulderView && isActiveAndEnabled && viewCamera && player;
            if (active == interactionFraming) return;
            interactionFraming = active;
            if (!viewCamera) return;
            if (!active)
            {
                viewCamera.transform.SetPositionAndRotation(savedInteractionPosition, savedInteractionRotation);
                viewCamera.fieldOfView = savedInteractionFov;
                viewCamera.orthographic = savedInteractionOrthographic;
                return;
            }
            savedInteractionPosition = viewCamera.transform.position;
            savedInteractionRotation = viewCamera.transform.rotation;
            savedInteractionFov = viewCamera.fieldOfView;
            savedInteractionOrthographic = viewCamera.orthographic;
            // Use the established oblique direction for spatial continuity. Fit in camera space,
            // including depth, so a tall collector and a wide tower both stay in the frame.
            interactionRotation = Quaternion.Euler(interactionPitch, orbitYaw, 0);
            Quaternion inverse = Quaternion.Inverse(interactionRotation);
            float verticalTangent = Mathf.Tan(25 * Mathf.Deg2Rad);
            float horizontalTangent = verticalTangent * Mathf.Max(.5f, viewCamera.aspect);
            float distance = 10;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = inverse * Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                        distance = Mathf.Max(distance, Mathf.Abs(corner.x) * interactionFramePadding / horizontalTangent - corner.z,
                            Mathf.Abs(corner.y) * interactionFramePadding / verticalTangent - corner.z);
                    }
            interactionPosition = bounds.center - interactionRotation * Vector3.forward * distance;
            ApplyInteractionFraming();
        }
        private void ApplyInteractionFraming()
        {
            viewCamera.orthographic = false;
            viewCamera.fieldOfView = 50;
            viewCamera.transform.SetPositionAndRotation(interactionPosition, interactionRotation);
        }

        public bool FixedAndShoulderEnabled => fixedAndShoulderControls;
        // Selected gameplay mode persists while Story temporarily owns the same Camera.
        public bool IsShoulderView => fixedAndShoulderControls && shoulderView;
        private bool GameplayPoseFrozen => fixedAndShoulderControls && (pointerInteraction || InputSuppressed);

        public void ConfigureFixedAndShoulder(bool enabled)
        {
            if (fixedAndShoulderControls == enabled) return;
            SetInteractionFraming(false);
            ReleaseShoulderPointer();
            fixedAndShoulderControls = enabled;
            shoulderView = shoulderLookReady = pointerInteraction = pointerReleasedByUser = false;
            if (enabled) IsObserving = IsOverview = false;
            ApplyView();
        }

        /// <summary>Temporarily freeze gameplay framing and free the pointer; never changes the selected view or look angles.</summary>
        public void SetPointerInteraction(bool active)
        {
            if (!fixedAndShoulderControls) return;
            pointerInteraction = active;
            RefreshShoulderPointer();
        }

        /// <summary>L5 routes a plain-world click here after excluding its HUD, modal UI and closing-frame inputs.</summary>
        public void ResumeShoulderLook()
        {
            if (!IsShoulderView || InputSuppressed || pointerInteraction || !applicationFocused || !isActiveAndEnabled) return;
            pointerReleasedByUser = false;
            if (Cursor.lockState != CursorLockMode.Locked) pointerLockRequested = false;
            RefreshShoulderPointer();
        }

        /// <summary>Explicit replay reset. Temporary dialogue, observation and travel must not call this.</summary>
        public void ResetGameplayView()
        {
            if (!fixedAndShoulderControls) return;
            SetInteractionFraming(false);
            shoulderView = shoulderLookReady = pointerReleasedByUser = false;
            platformingView = false; platformingWeight = 0;
            ReleaseShoulderPointer();
            IsObserving = IsOverview = false;
            ApplyView();
        }

        private float followHeightOffset;
        public void SetFollowHeightOffset(float offset) => followHeightOffset = Mathf.Clamp(offset, 0, 2);

        private Vector3 followAnchor, followVelocity;
        private bool followAnchorReady;
        private Vector3 gameplayFocusPoint;
        private float gameplayFocusWeight;
        private Vector3 feedbackPoint,feedbackPlayerStart;
        private float feedbackUntil,feedbackScale=1,feedbackWeight;
        // A short, interruptible look toward a physical consequence; orbit input and movement remain live.
        public void ShowPhysicalFeedback(Vector3 point,float seconds,float weight=.4f,float scale=1.25f)
        {feedbackPoint=point;feedbackPlayerStart=player.FeetPosition;feedbackUntil=Time.time+seconds;feedbackWeight=Mathf.Clamp01(weight);feedbackScale=scale;}
        public void ClearPhysicalFeedback(){feedbackUntil=0;}
        private bool FeedbackActive=>Time.time<feedbackUntil&&(player.FeetPosition-feedbackPlayerStart).sqrMagnitude<4;
        private bool journeyView;
        [SerializeField] private bool orbitEnabled;
        private float orbitYaw=-40,orbitPitch=50,zoom=1;
        public bool OrbitEnabled=>orbitEnabled;
        public float OrbitYaw=>orbitYaw;
        public void ConfigureOrbit(bool enabled){orbitEnabled=enabled;ApplyView();}
        private Vector3 journeyOffset;
        private float journeySize;
        public void SetJourneyView(bool active, Vector3 offset = default, float size = 11)
        {
            bool changed=journeyView!=active;
            journeyView=active;journeyOffset=offset;journeySize=size;
            if(changed||IsObserving||IsOverview)ExitObservation();
        }

        public bool IsObserving { get; private set; }
        public bool IsOverview { get; private set; }
        public bool FollowViewEnabled => followPlayer;
        public Camera ViewCamera => viewCamera;

        public void Initialize(CourtController owner, CourtPlayer actor, Camera camera)
        {
            court = owner;
            player = actor;
            viewCamera = camera;
            ApplyView();
        }

        public void ToggleObservation()
        {
            if (fixedAndShoulderControls) return;
            IsObserving = !IsObserving;
            IsOverview = false;
            ApplyView();
        }

        /// <summary>Use a close gameplay view without changing the existing camera-relative input heading.</summary>
        public void ConfigureFollowView(CourtPlayer actor, float size = 10f, float smoothing = 0.18f)
        {
            if (actor == null) throw new System.ArgumentNullException(nameof(actor));
            player = actor;
            followPlayer = true;
            followSize = Mathf.Clamp(size, 6f, 16f);
            followSmoothing = Mathf.Clamp(smoothing, 0.05f, 1f);
            IsObserving = false;
            IsOverview = false;
            ApplyView();
        }

        public void ToggleOverview()
        {
            if (fixedAndShoulderControls || !followPlayer) return;
            IsObserving = false;
            IsOverview = !IsOverview;
            ApplyView();
        }

        /// <summary>Include a nearby interaction's result while retaining the close view and fixed input heading.</summary>
        public void SetGameplayFocus(Vector3 worldPoint, float weight)
        {
            gameplayFocusPoint = worldPoint;
            gameplayFocusWeight = Mathf.Clamp(weight, 0, 0.4f);
        }

        public void ConfigureViews(Vector3 position, Vector3 target, float size,
            Vector3 observationPosition, Vector3 observationTarget)
        {
            overviewPosition = position;
            overviewTarget = target;
            overviewSize = size;
            pavilionPosition = observationPosition;
            pavilionTarget = observationTarget;
            ApplyView();
        }

        public void ExitObservation()
        {
            IsObserving = false;
            IsOverview = false;
            ApplyView();
        }

        private void Awake()
        {
            applicationFocused = Application.isFocused;
            // Existing L1 camera transforms remain untouched; only opted-in scenes initialise a follow view.
            if (followPlayer) ApplyView();
        }

        public bool InputSuppressed {get;set;}
        private void Update()
        {
            if (fixedAndShoulderControls)
            {
                UpdateFixedAndShoulderControls();
                return;
            }
            if(InputSuppressed)return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if(orbitEnabled)
            {
                if(keyboard.zKey.isPressed)orbitYaw-=65*Time.unscaledDeltaTime;
                if(keyboard.xKey.isPressed)orbitYaw+=65*Time.unscaledDeltaTime;
                var mouse=Mouse.current;
                if(mouse!=null&&mouse.rightButton.isPressed){var d=mouse.delta.ReadValue();orbitYaw+=d.x*.18f;orbitPitch=Mathf.Clamp(orbitPitch-d.y*.14f,30,75);}
                if(mouse!=null)zoom=Mathf.Clamp(zoom-mouse.scroll.ReadValue().y*.001f,.65f,1.6f);
                if(keyboard.fKey.wasPressedThisFrame||keyboard.cKey.wasPressedThisFrame){orbitYaw=-40;orbitPitch=50;zoom=1;ExitObservation();}
            }
            else if (keyboard.cKey.wasPressedThisFrame) ToggleObservation();
            if (keyboard.vKey.wasPressedThisFrame) ToggleOverview();
            if (keyboard.escapeKey.wasPressedThisFrame) ExitObservation();
        }

        private void UpdateFixedAndShoulderControls()
        {
            RefreshShoulderPointer();
            if (InputSuppressed || pointerInteraction || !applicationFocused) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                // Escape releases the pointer only. Story/weather own their own modal Escape handling.
                pointerReleasedByUser = true;
                ReleaseShoulderPointer();
                return;
            }
            if (keyboard != null && keyboard.zKey.wasPressedThisFrame)
            {
                shoulderView = !shoulderView;
                pointerReleasedByUser = false;
                if (shoulderView && !shoulderLookReady)
                {
                    // Initialise behind the character once. Later mode switches, movement
                    // and platform turns preserve the player's independently chosen orbit.
                    ReadPlayerHeading();
                    shoulderPitch = ClampShoulderPitch(shoulderInitialPitch);
                    shoulderLookReady = true;
                }
                RefreshShoulderPointer();
                ApplyView();
            }
            if (!IsShoulderView || !ownsPointerLock || Cursor.lockState != CursorLockMode.Locked
                || Time.frameCount <= ignoreLookThroughFrame || Mouse.current == null) return;
            Vector2 delta = Mouse.current.delta.ReadValue();
            shoulderYaw = Mathf.Repeat(shoulderYaw + delta.x * Mathf.Max(.001f, shoulderSensitivity), 360);
            shoulderPitch = ClampShoulderPitch(shoulderPitch - delta.y * Mathf.Max(.001f, shoulderSensitivity));
        }

        private void ReadPlayerHeading()
        {
            Vector3 heading = player ? Vector3.ProjectOnPlane(player.FacingDirection, Vector3.up) : Vector3.forward;
            if (heading.sqrMagnitude > .0001f) shoulderYaw = Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg;
        }

        private float ClampShoulderPitch(float value)
        {
            float minimum = Mathf.Clamp(Mathf.Min(shoulderPitchLimits.x, shoulderPitchLimits.y), -85, 85);
            float maximum = Mathf.Clamp(Mathf.Max(shoulderPitchLimits.x, shoulderPitchLimits.y), -85, 85);
            return Mathf.Clamp(value, minimum, maximum);
        }

        private void RefreshShoulderPointer()
        {
            bool wantsLock = Application.isPlaying && IsShoulderView && isActiveAndEnabled && applicationFocused
                && !InputSuppressed && !pointerInteraction && !pointerReleasedByUser;
            if (!wantsLock)
            {
                ReleaseShoulderPointer();
                return;
            }
            if (!pointerLockRequested)
            {
                pointerLockRequested = ownsPointerLock = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                // The OS warp can arrive on the next input update, after the frame that requested the lock.
                ignoreLookThroughFrame = Time.frameCount + 1;
            }
            else if (ownsPointerLock && Cursor.lockState != CursorLockMode.Locked)
            {
                // Respect the Editor/OS releasing the pointer; do not fight it by re-locking every frame.
                pointerReleasedByUser = true;
                ReleaseShoulderPointer();
            }
        }

        private void ReleaseShoulderPointer()
        {
            if (ownsPointerLock)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            ownsPointerLock = pointerLockRequested = false;
            ignoreLookThroughFrame = Time.frameCount + 1;
        }

        private void OnEnable()
        {
            applicationFocused = Application.isFocused;
            // Re-evaluate once in Update, after the controller has restored its modal state.
            pointerLockRequested = false;
            ignoreLookThroughFrame = Time.frameCount + 1;
        }
        private void OnDisable()
        {
            SetInteractionFraming(false);
            ReleaseShoulderPointer();
        }
        private void OnDestroy() => ReleaseShoulderPointer();
        private void OnApplicationQuit() => ReleaseShoulderPointer();
        private void OnApplicationFocus(bool focused)
        {
            applicationFocused = focused;
            if (!fixedAndShoulderControls) return;
            if (!focused)
            {
                ReleaseShoulderPointer();
            }
            else
            {
                // Focus loss is not a manual Escape. A returning game can request its pointer once.
                pointerReleasedByUser = false;
                pointerLockRequested = false;
                ignoreLookThroughFrame = Time.frameCount + 1;
            }
        }

        private void LateUpdate()
        {
            if (interactionFraming) { ApplyInteractionFraming(); return; }
            if (GameplayPoseFrozen) return;
            if (!followPlayer || IsObserving || IsOverview || viewCamera == null || player == null) return;
            platformingWeight = Mathf.MoveTowards(platformingWeight, platformingView ? 1 : 0, Time.unscaledDeltaTime * 4);
            Vector3 target = GameplayFollowTarget();
            if (!followAnchorReady)
            {
                followAnchor = target;
                followVelocity = Vector3.zero;
                followAnchorReady = true;
            }
            else followAnchor = Vector3.SmoothDamp(followAnchor, target, ref followVelocity,
                followSmoothing, Mathf.Infinity, Time.unscaledDeltaTime);
            ApplyFollowTransform();
        }

        private Vector3 GameplayFollowTarget() => IsShoulderView
            ? player.FeetPosition + Vector3.up * (Mathf.Max(.25f, shoulderHeight) + followHeightOffset)
            : Vector3.Lerp(player.FeetPosition + Vector3.up * (1 + followHeightOffset), FeedbackActive?feedbackPoint:gameplayFocusPoint,
                FeedbackActive?feedbackWeight:gameplayFocusWeight);

        private void ApplyFollowTransform()
        {
            if (GameplayPoseFrozen) return;
            if (IsShoulderView)
            {
                ApplyShoulderTransform();
                return;
            }
            if(orbitEnabled || fixedAndShoulderControls)
            {
                viewCamera.orthographic=false;viewCamera.fieldOfView=50;
                feedbackZoom=Mathf.Lerp(feedbackZoom,FeedbackActive?feedbackScale:1,1-Mathf.Exp(-Time.unscaledDeltaTime*5));
                Vector3 orbitOffset=Quaternion.Euler(orbitPitch,orbitYaw,0)*Vector3.back*((journeyView?journeySize*1.8f:20)*zoom*feedbackZoom);
                viewCamera.transform.SetPositionAndRotation(followAnchor+orbitOffset,Quaternion.LookRotation(-orbitOffset,Vector3.up));
                return;
            }
            Vector3 offset = journeyView ? journeyOffset : overviewPosition - overviewTarget;
            viewCamera.orthographicSize=journeyView?journeySize:followSize;
            viewCamera.transform.SetPositionAndRotation(followAnchor + offset,
                Quaternion.LookRotation(-offset, Vector3.up));
        }
        private void ApplyShoulderTransform()
        {
            viewCamera.orthographic = false;
            viewCamera.fieldOfView = Mathf.Clamp(Mathf.Lerp(shoulderFieldOfView, platformingFieldOfView, platformingWeight), 35, 85);
            Quaternion orbit = Quaternion.Euler(ClampShoulderPitch(shoulderPitch + platformingPitchOffset * platformingWeight), shoulderYaw, 0);
            // Both yaw and pitch orbit the camera around the traveller. The existing
            // solid-world sphere sweep also catches ground when looking upward.
            Vector3 desired = followAnchor + orbit * new Vector3(Mathf.Lerp(shoulderSideOffset, .15f, platformingWeight), 0,
                -Mathf.Max(.25f, Mathf.Lerp(shoulderDistance, platformingDistance, platformingWeight)));
            Vector3 pivot = player.FeetPosition + Vector3.up * (Mathf.Max(.25f, shoulderHeight) + followHeightOffset);
            Vector3 boom = desired - pivot;
            float distance = boom.magnitude;
            if (distance > .0001f)
            {
                Vector3 direction = boom / distance;
                float radius = Mathf.Clamp(shoulderCollisionRadius, .05f, .4f);
                int count = Physics.SphereCastNonAlloc(pivot, radius, direction, shoulderHits, distance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                var hits = shoulderHits;
                // The usual path allocates nothing. A crowded fixture must not silently miss a nearer wall.
                if (count == shoulderHits.Length)
                {
                    hits = Physics.SphereCastAll(pivot, radius, direction, distance,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    count = hits.Length;
                }
                float clearDistance = distance;
                for (int i = 0; i < count; i++)
                {
                    var hit = hits[i];
                    if (!hit.collider || hit.collider.transform.IsChildOf(player.transform)) continue;
                    clearDistance = Mathf.Min(clearDistance, Mathf.Max(0, hit.distance - Mathf.Max(0, shoulderWallPadding)));
                }
                desired = pivot + direction * clearDistance;
            }
            viewCamera.transform.SetPositionAndRotation(desired, orbit);
        }

        private float feedbackZoom=1;

        private void ApplyView()
        {
            if (GameplayPoseFrozen || fixedAndShoulderControls && !isActiveAndEnabled) return;
            if (viewCamera == null) return;
            viewCamera.orthographic = !IsObserving;
            bool following = followPlayer && !IsObserving && !IsOverview && player != null;
            viewCamera.orthographicSize = following ? followSize : overviewSize;
            viewCamera.fieldOfView = 66f;
            viewCamera.nearClipPlane = 0.1f;
            viewCamera.farClipPlane = 250f;
            if (following)
            {
                followAnchor = GameplayFollowTarget();
                followVelocity = Vector3.zero;
                followAnchorReady = true;
                ApplyFollowTransform();
                return;
            }
            followAnchorReady = false;
            viewCamera.transform.position = IsObserving ? pavilionPosition : overviewPosition;
            viewCamera.transform.rotation = Quaternion.LookRotation(
                (IsObserving ? pavilionTarget : overviewTarget) - viewCamera.transform.position, Vector3.up);
        }
    }
}
