using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace BorrowingSun
{
    /// <summary>Presentation only. CourtPlayer remains the authority for movement and collision.</summary>
    [RequireComponent(typeof(Animator))]
    public sealed class StandardHumanoidVisual : MonoBehaviour
    {
        [SerializeField] private CourtPlayer movement;
        [SerializeField] private Transform visualFacing;
        [SerializeField] private float turnDegreesPerSecond = 720f;
        [SerializeField] private float handHeight = 1.08f;
        [SerializeField] private float handReach = .58f;
        [SerializeField] private float handHalfWidth = .28f;
        private Animator animator;
        private float pushWeight,swimWeight;
        private Transform ridingSeat;
        private Vector3 standingLocalPosition;
        private float ridingHipHeight;
        [Header("Optional ladder presentation (L5 only)")]
        [SerializeField] private AnimationClip climbAnimation;
        [SerializeField, Min(.1f)] private float climbAnimationReferenceSpeed = .2232558f;
        [SerializeField, Min(.01f)] private float climbBlendSeconds = .16f;
        private PlayableGraph climbGraph;
        private AnimatorControllerPlayable locomotionPlayable;
        private AnimationClipPlayable climbPlayable;
        private AnimationMixerPlayable climbMixer;
        private bool climbRequested;
        private float climbVelocity, climbWeight, climbCycleTime, locomotionSpeed;
        private Vector3 climbFacing;
        public bool IsRiding => ridingSeat;
        public AnimationClip ClimbAnimation => climbAnimation;
        public bool HasClimbAnimation => climbAnimation && climbAnimation.humanMotion;
        public float ClimbPoseWeight => climbWeight;
        public float ClimbCycleTime => climbCycleTime;

        /// <summary>Opt-in presentation: the gameplay controller still owns feet, collision and climb phase.</summary>
        public void ConfigureClimbing(AnimationClip clip, float metresPerCycle = .8f)
        {
            climbAnimation = clip;
            // Two authored 0.4 m rungs per downloaded in-place cycle. Match the
            // visual phase to travelled distance; stopping or reversing remains manual.
            if (clip && clip.length > .01f)
                climbAnimationReferenceSpeed = Mathf.Max(.1f, metresPerCycle / clip.length);
        }

        public void SetClimbingPose(bool active, float signedVelocity, Vector3 ladderFacing)
        {
            climbRequested = active && HasClimbAnimation && !ridingSeat;
            climbVelocity = climbRequested ? signedVelocity : 0;
            climbFacing = ladderFacing;
        }

        /// <summary>Presentation only: keep the humanoid's hips at the saddle and pose hands/feet with existing Humanoid IK.</summary>
        public void SetRidingPose(Transform seat)
        {
            if (!visualFacing) return;
            if (!seat)
            {
                if (ridingSeat) visualFacing.localPosition = standingLocalPosition;
                ridingSeat = null;
                return;
            }
            if (!ridingSeat)
            {
                standingLocalPosition = visualFacing.localPosition;
                var hips = animator && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
                ridingHipHeight = hips ? Mathf.Clamp(Vector3.Dot(hips.position - visualFacing.position, Vector3.up), .65f, 1.25f) : .9f;
            }
            ridingSeat = seat;
        }
        private static readonly int Speed = Animator.StringToHash("Speed");
        public Animator Animator => animator;
        public float PushPoseWeight => pushWeight;

        public void Configure(CourtPlayer owner, Transform facing)
        {
            movement = owner;
            visualFacing = facing;
            animator = GetComponent<Animator>();
            animator.applyRootMotion = false;
        }

        private void Awake()
        {
            animator = GetComponent<Animator>();
            animator.applyRootMotion = false;
        }

        private void OnEnable() { pushWeight = 0; EnsureClimbGraph(); }
        private void OnDisable()
        {
            SetRidingPose(null);
            climbRequested = false;
            climbWeight = climbCycleTime = climbVelocity = 0;
            if (climbGraph.IsValid()) climbGraph.Destroy();
        }

        private void EnsureClimbGraph()
        {
            if (!Application.isPlaying || climbGraph.IsValid() || !HasClimbAnimation || !animator || !animator.runtimeAnimatorController) return;
            climbGraph = PlayableGraph.Create("L5 downloaded ladder Humanoid retarget");
            climbGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            locomotionPlayable = AnimatorControllerPlayable.Create(climbGraph, animator.runtimeAnimatorController);
            climbPlayable = AnimationClipPlayable.Create(climbGraph, climbAnimation);
            climbPlayable.SetSpeed(0);
            climbPlayable.SetApplyFootIK(false);
            climbPlayable.SetApplyPlayableIK(false);
            climbMixer = AnimationMixerPlayable.Create(climbGraph, 2);
            climbGraph.Connect(locomotionPlayable, 0, climbMixer, 0);
            climbGraph.Connect(climbPlayable, 0, climbMixer, 1);
            climbMixer.SetInputWeight(0, 1);
            climbMixer.SetInputWeight(1, 0);
            AnimationPlayableOutput.Create(climbGraph, "L5 ladder visual", animator).SetSourcePlayable(climbMixer);
            climbGraph.Play();
        }

        private void Update()
        {
            if (movement == null || visualFacing == null || animator == null) return;
            EnsureClimbGraph();
            climbWeight = Mathf.MoveTowards(climbWeight, climbRequested ? 1 : 0, Time.deltaTime / Mathf.Max(.01f, climbBlendSeconds));
            if (climbGraph.IsValid())
            {
                if (climbRequested && Mathf.Abs(climbVelocity) > .01f)
                    climbCycleTime = Mathf.Repeat(climbCycleTime + Time.deltaTime * climbVelocity / climbAnimationReferenceSpeed, climbAnimation.length);
                climbPlayable.SetTime(climbCycleTime);
                climbMixer.SetInputWeight(0, 1 - climbWeight);
                climbMixer.SetInputWeight(1, climbWeight);
            }
            swimWeight=Mathf.MoveTowards(swimWeight,movement.IsSwimming?1:0,Time.deltaTime*3);
            Vector3 direction = Vector3.ProjectOnPlane(climbRequested ? climbFacing : movement.FacingDirection, Vector3.up);
            if (direction.sqrMagnitude > .001f)
                visualFacing.rotation = Quaternion.RotateTowards(visualFacing.rotation,
                    Quaternion.LookRotation(direction)*Quaternion.Euler(swimWeight*55,0,0), turnDegreesPerSecond * Time.deltaTime);

            if (ridingSeat) visualFacing.position = ridingSeat.position - Vector3.up * ridingHipHeight;

            // Original Kenney idle/run clips are blended by actual planar velocity. Lift travel
            // does not count as locomotion; navigation into the push position does.
            float speed = ridingSeat || climbRequested || movement.IsDropping || movement.IsSwimming ? 0 : movement.PlanarVelocity.magnitude;
            if (climbGraph.IsValid())
            {
                locomotionSpeed = Mathf.Lerp(locomotionSpeed, Mathf.Clamp(speed, 0, 4f), 1 - Mathf.Exp(-Time.deltaTime / .1f));
                locomotionPlayable.SetFloat(Speed, locomotionSpeed);
            }
            else animator.SetFloat(Speed, Mathf.Clamp(speed, 0, 4f), .10f, Time.deltaTime);
            pushWeight = Mathf.MoveTowards(pushWeight, !ridingSeat && !climbRequested && movement.IsPushingCart ? 1 : 0, Time.deltaTime * 7f);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (movement == null || visualFacing == null || animator == null || !animator.isHuman) return;
            if (climbWeight > .001f)
            {
                foreach (AvatarIKGoal goal in System.Enum.GetValues(typeof(AvatarIKGoal)))
                {
                    animator.SetIKPositionWeight(goal, 0);
                    animator.SetIKRotationWeight(goal, 0);
                }
                foreach (AvatarIKHint hint in System.Enum.GetValues(typeof(AvatarIKHint))) animator.SetIKHintPositionWeight(hint, 0);
                animator.SetLookAtWeight(0);
                return;
            }
            if (ridingSeat)
            {
                Vector3 seat = ridingSeat.position;
                Vector3 forwardRide = Vector3.ProjectOnPlane(movement.FacingDirection, Vector3.up).normalized;
                Vector3 rightRide = Vector3.Cross(Vector3.up, forwardRide);
                RidingLimb(AvatarIKGoal.LeftFoot, AvatarIKHint.LeftKnee, seat - rightRide * .43f - Vector3.up * .5f + forwardRide * .12f,
                    seat - rightRide * .46f - Vector3.up * .12f + forwardRide * .5f);
                RidingLimb(AvatarIKGoal.RightFoot, AvatarIKHint.RightKnee, seat + rightRide * .43f - Vector3.up * .5f + forwardRide * .12f,
                    seat + rightRide * .46f - Vector3.up * .12f + forwardRide * .5f);
                RidingLimb(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, seat - rightRide * .23f + Vector3.up * .5f + forwardRide * .48f,
                    seat - rightRide * .48f + Vector3.up * .45f + forwardRide * .15f);
                RidingLimb(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, seat + rightRide * .23f + Vector3.up * .5f + forwardRide * .48f,
                    seat + rightRide * .48f + Vector3.up * .45f + forwardRide * .15f);
                animator.SetLookAtWeight(0);
                return;
            }
            animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0);
            animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0);
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftKnee, 0);
            animator.SetIKHintPositionWeight(AvatarIKHint.RightKnee, 0);
            // This is a procedural whitebox pose over the licensed locomotion clip, not a
            // downloaded push animation. Targets follow the actor, never move the gameplay root.
            Vector3 forward = visualFacing.forward;
            Vector3 right = visualFacing.right;
            Vector3 center = movement.FeetPosition + Vector3.up * handHeight + forward * handReach;
            SetHand(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, center - right * handHalfWidth,
                movement.FeetPosition + Vector3.up * 1.02f - right * .42f + forward * .16f);
            SetHand(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, center + right * handHalfWidth,
                movement.FeetPosition + Vector3.up * 1.02f + right * .42f + forward * .16f);
            animator.SetLookAtWeight(pushWeight * .22f, .1f, .8f, 0, .6f);
            animator.SetLookAtPosition(movement.FeetPosition + Vector3.up * 1.3f + forward * 2f);
            if(movement.IsSwimming&&movement.OxygenUrgency>0)
            {
                float urgency=movement.OxygenUrgency;
                animator.SetIKPositionWeight(AvatarIKGoal.RightHand,.5f+urgency*.5f);
                animator.SetIKPosition(AvatarIKGoal.RightHand,movement.FeetPosition+Vector3.up*1.05f+forward*.55f);
                animator.SetIKPositionWeight(AvatarIKGoal.LeftHand,.35f+urgency*.45f);
                animator.SetIKPosition(AvatarIKGoal.LeftHand,movement.FeetPosition+Vector3.up*.9f-right*.25f+forward*(.25f+.08f*Mathf.Sin(Time.time*(urgency>.5f?7:3))));
            }
        }

        private void RidingLimb(AvatarIKGoal goal, AvatarIKHint hint, Vector3 position, Vector3 bend)
        {
            animator.SetIKPositionWeight(goal, 1);
            animator.SetIKRotationWeight(goal, 0);
            animator.SetIKPosition(goal, position);
            animator.SetIKHintPositionWeight(hint, 1);
            animator.SetIKHintPosition(hint, bend);
        }

        private void SetHand(AvatarIKGoal goal, AvatarIKHint hint, Vector3 position, Vector3 elbow)
        {
            animator.SetIKPositionWeight(goal, pushWeight);
            animator.SetIKRotationWeight(goal, 0);
            animator.SetIKPosition(goal, position);
            animator.SetIKHintPositionWeight(hint, pushWeight * .5f);
            animator.SetIKHintPosition(hint, elbow);
        }
    }
}
