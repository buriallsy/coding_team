using UnityEngine;

namespace BorrowingSun
{
    /// <summary>Presentation adapter. Journey owns ladder traversal; the downloaded clip never moves the controller.</summary>
    [DefaultExecutionOrder(-50)]
    public sealed class SkywellClimbAnimation : MonoBehaviour
    {
        [SerializeField] private SkywellJourney journey;
        [SerializeField] private StandardHumanoidVisual visual;
        public StandardHumanoidVisual Visual => visual;
        public bool HasClimbAnimation => visual && visual.HasClimbAnimation;

        public void Configure(SkywellJourney owner, StandardHumanoidVisual presentation, AnimationClip clip)
        {
            journey = owner;
            visual = presentation;
            visual.ConfigureClimbing(clip);
        }

        private void Update()
        {
            if (!visual) return;
            bool active = journey && journey.enabled && journey.IsClimbing && journey.ClimbPhase == SkywellClimbPhase.Climbing;
            visual.SetClimbingPose(active, active ? journey.ClimbVelocity : 0,
                journey ? journey.LadderFacingDirection : Vector3.forward);
        }

        private void OnDisable()
        {
            if (visual) visual.SetClimbingPose(false, 0, Vector3.forward);
        }
    }
}
