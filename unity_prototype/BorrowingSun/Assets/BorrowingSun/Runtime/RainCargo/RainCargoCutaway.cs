using System.Collections.Generic;
using UnityEngine;

namespace BorrowingSun
{
    /// <summary>Reveal the lower B landing through the foreground terrace without changing its collision.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class RainCargoCutaway : MonoBehaviour
    {
        private RainCargoController court;
        private Collider terrace;
        private Renderer[] surfaces = new Renderer[0];
        private bool[] originalEnabled = new bool[0];
        public bool IsCutAway { get; private set; }

        public void Initialize(RainCargoController owner)
        {
            Restore();
            court = owner;
            // This is the fixed street group. The moving B deck and its equipment are separate roots.
            Transform street = owner.Bindings.stops[6].parent;
            Transform platform = street.Find("East_receiving_terrace");
            terrace = platform != null ? platform.GetComponent<Collider>() : null;
            Renderer platformSurface = platform != null ? platform.GetComponent<Renderer>() : null;
            if (terrace == null || platformSurface == null)
            {
                Debug.LogWarning("运雨街未找到东岸平台的剖切表面。", this);
                surfaces = new Renderer[0];
                originalEnabled = new bool[0];
                return;
            }

            var selected = new List<Renderer> { platformSurface };
            Bounds floor = platformSurface.bounds;
            foreach (Renderer candidate in street.GetComponentsInChildren<Renderer>(true))
            {
                string name = candidate.name;
                bool surfaceMark = name.StartsWith("Cargo_dock_") || name == "Copper_freight_rail" || name == "Forward_chevron";
                if (!surfaceMark) continue;
                Bounds mark = candidate.bounds;
                // Only the paint/rails resting on this fixed platform, never B or an adjacent stair.
                if (mark.min.x >= floor.min.x - .06f && mark.max.x <= floor.max.x + .06f
                    && mark.min.z >= floor.min.z - .06f && mark.max.z <= floor.max.z + .06f
                    && mark.min.y >= floor.max.y - .03f && mark.max.y <= floor.max.y + .12f)
                    selected.Add(candidate);
            }
            surfaces = selected.ToArray();
            originalEnabled = new bool[surfaces.Length];
        }

        private void LateUpdate()
        {
            if (court == null || !court.isActiveAndEnabled || terrace == null || !terrace.enabled
                || court.CameraRig == null || !court.CameraRig.FollowViewEnabled
                || court.CameraRig.IsOverview || court.CameraRig.IsObserving || court.Player == null)
            {
                Restore();
                return;
            }

            Camera camera = court.CameraRig.ViewCamera;
            Vector3 feet = court.Player.FeetPosition;
            bool blocked = camera != null && (BlocksPlayer(camera, feet + Vector3.up * .35f)
                || BlocksPlayer(camera, feet + Vector3.up * .9f)
                || BlocksPlayer(camera, feet + Vector3.up * 1.5f));
            if (!blocked) { Restore(); return; }
            if (IsCutAway) return;
            for (int i = 0; i < surfaces.Length; i++)
            {
                if (surfaces[i] == null) continue;
                originalEnabled[i] = surfaces[i].enabled;
                surfaces[i].enabled = false;
            }
            IsCutAway = true;
        }

        private bool BlocksPlayer(Camera camera, Vector3 point)
        {
            Vector3 viewport = camera.WorldToViewportPoint(point);
            if (viewport.z <= camera.nearClipPlane) return false;
            // Orthographic rays start at the projected pixel, not at the camera's central position.
            Ray ray = camera.ViewportPointToRay(viewport);
            float distance = Vector3.Dot(point - ray.origin, ray.direction);
            return distance > .02f && terrace.Raycast(ray, out _, distance - .02f);
        }

        public void Restore()
        {
            if (!IsCutAway) return;
            for (int i = 0; i < surfaces.Length; i++)
                if (surfaces[i] != null) surfaces[i].enabled = originalEnabled[i];
            IsCutAway = false;
        }

        private void OnDisable() => Restore();
        private void OnDestroy() => Restore();
    }
}
