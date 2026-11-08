using UnityEngine;

namespace BorrowingSun
{
    /// <summary>Only presents the ladder between Journey's anchors; lift and traversal remain their owners.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class SkywellLadderVisual : MonoBehaviour
    {
        [SerializeField] private Transform ladderBottom, ladderTop, leftRail, rightRail;
        [SerializeField] private Renderer[] rungs;
        [SerializeField, Tooltip("Rail base relative to the lower feet anchor, in world metres.")]
        private float bottomOffset = -.03f;
        [SerializeField, Min(0), Tooltip("Rail above the current upper feet anchor, in world metres.")]
        private float topOverhang = .07f;

        public Transform BottomAnchor => ladderBottom;
        public Transform TopAnchor => ladderTop;
        public Transform LeftRail => leftRail;
        public Transform RightRail => rightRail;
        public Renderer[] Rungs => rungs;
        public float BottomOffset => bottomOffset;
        public float TopOverhang => topOverhang;
        public bool IsConfigured => ladderBottom && ladderTop && leftRail && rightRail && rungs != null && rungs.Length > 0;

        public void Configure(Transform bottom, Transform top, Transform left, Transform right, Renderer[] steps)
        {
            ladderBottom = bottom; ladderTop = top; leftRail = left; rightRail = right; rungs = steps;
            Refresh();
        }

        private void OnEnable() => Refresh();
        private void LateUpdate() => Refresh();
        // Freeze the current shape when disabled; never restore a height that disagrees with the lift.
        private void OnDisable() => Refresh();
        private void OnValidate() => topOverhang = Mathf.Max(0, topOverhang);

        public void Refresh()
        {
            if (!IsConfigured) return;
            float bottom = ladderBottom.position.y + bottomOffset;
            float top = ladderTop.position.y + Mathf.Max(0, topOverhang);
            if (top <= bottom) return;
            ResizeRail(leftRail, bottom, top);
            ResizeRail(rightRail, bottom, top);
            // The original 0.4m rung spacing stays fixed instead of compressing with the ladder root.
            foreach (var rung in rungs)
            {
                if (!rung) continue;
                var bounds = rung.bounds;
                bool visible = bounds.min.y >= bottom - .0001f && bounds.max.y <= top + .0001f;
                if (rung.enabled != visible) rung.enabled = visible;
            }
        }

        private static void ResizeRail(Transform rail, float bottom, float top)
        {
            Vector3 lower = rail.position; lower.y = bottom;
            Vector3 upper = lower; upper.y = top;
            // Unit-cube rails: converting both endpoints preserves world height under parent scaling.
            if (rail.parent)
            {
                lower = rail.parent.InverseTransformPoint(lower);
                upper = rail.parent.InverseTransformPoint(upper);
            }
            Vector3 axis = upper - lower;
            Vector3 position = (lower + upper) * .5f;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, axis);
            Vector3 scale = rail.localScale; scale.y = axis.magnitude;
            if ((rail.localPosition - position).sqrMagnitude > .00000001f || Quaternion.Angle(rail.localRotation, rotation) > .001f)
                rail.SetLocalPositionAndRotation(position, rotation);
            if ((rail.localScale - scale).sqrMagnitude > .00000001f) rail.localScale = scale;
        }
    }
}
