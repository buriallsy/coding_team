using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace BorrowingSun
{
    /// <summary>Only the cave's presentation changes; its solid walls keep collision and sun shadows.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class SkywellCaveCutaway : MonoBehaviour
    {
        [SerializeField] private CourtPlayer player;
        [SerializeField] private CourtCamera cameraRig;
        [SerializeField] private Transform caveFrame;
        [SerializeField] private Bounds[] localVolumes = Array.Empty<Bounds>();
        [SerializeField] private Renderer[] surfaces = Array.Empty<Renderer>();
        [SerializeField] private Collider[] solids = Array.Empty<Collider>();
        private ShadowCastingMode[] authoredModes;
        private static readonly Vector3[] ViewOffsets = {
            Vector3.up*.18f, Vector3.up*1.1f, Vector3.up*1.85f,
            Vector3.up*.6f+Vector3.left*1.25f, Vector3.up*.6f+Vector3.right*1.25f,
            Vector3.up*.6f+Vector3.forward*1.25f, Vector3.up*.6f+Vector3.back*1.25f
        };
        public bool IsInside { get; private set; }
        public int CutawayCount { get; private set; }

        public void Configure(CourtPlayer actor, CourtCamera camera, Transform frame, Bounds[] volumes,
            Renderer[] renderers, Collider[] colliders)
        {
            Restore();
            player = actor; cameraRig = camera; caveFrame = frame; localVolumes = volumes;
            surfaces = renderers; solids = colliders; authoredModes = null;
        }
        private void Awake() { CacheModes(); }
        private void CacheModes()
        {
            if (authoredModes != null) return;
            authoredModes = new ShadowCastingMode[surfaces.Length];
            for (int i = 0; i < surfaces.Length; i++)
                if (surfaces[i]) authoredModes[i] = surfaces[i].shadowCastingMode;
        }
        private void LateUpdate()
        {
            CacheModes();
            IsInside = false;
            if (player && caveFrame)
            {
                var local = caveFrame.InverseTransformPoint(player.FeetPosition + Vector3.up);
                foreach (var volume in localVolumes) if (volume.Contains(local)) { IsInside = true; break; }
            }
            bool cutaway = IsInside && cameraRig && !cameraRig.IsShoulderView && cameraRig.ViewCamera;
            CutawayCount = 0;
            for (int i = 0; i < surfaces.Length; i++)
            {
                if (!surfaces[i]) continue;
                bool blocksView = false;
                if (cutaway && i < solids.Length && solids[i])
                    foreach (var offset in ViewOffsets)
                    {
                        // Include feet and nearby floor: a head-only ray can leave the body hidden at bends.
                        Vector3 from = player.FeetPosition + offset;
                        Vector3 delta = cameraRig.ViewCamera.transform.position - from;
                        if (solids[i].Raycast(new Ray(from,delta.normalized),out _,delta.magnitude))
                        { blocksView = true; break; }
                    }
                var mode = blocksView ? ShadowCastingMode.ShadowsOnly : authoredModes[i];
                if (surfaces[i].shadowCastingMode != mode) surfaces[i].shadowCastingMode = mode;
                if (blocksView) CutawayCount++;
            }
        }
        private void Restore()
        {
            if (authoredModes != null)
                for (int i = 0; i < surfaces.Length && i < authoredModes.Length; i++)
                    if (surfaces[i]) surfaces[i].shadowCastingMode = authoredModes[i];
            IsInside = false; CutawayCount = 0;
        }
        private void OnDisable() { Restore(); }
        private void OnDestroy() { Restore(); }
    }
}
