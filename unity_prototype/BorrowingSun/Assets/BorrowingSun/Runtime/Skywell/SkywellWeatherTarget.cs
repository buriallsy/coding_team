using System;
using System.Collections.Generic;
using UnityEngine;

namespace BorrowingSun
{
    /// <summary>Visible weather target only; the controller owns selection, input, and weather.</summary>
    [DisallowMultipleComponent]
    public sealed class SkywellWeatherTarget : MonoBehaviour
    {
        [SerializeField] private int stationIndex;
        [SerializeField] private string displayName;
        [SerializeField] private Renderer[] highlightRenderers = Array.Empty<Renderer>();
        [SerializeField] private Collider[] hitColliders = Array.Empty<Collider>();
        [SerializeField] private Color availableColor = new Color(1f, .88f, .53f);
        [SerializeField] private Color hoveredColor = new Color(1f, .98f, .84f);
        [SerializeField, Range(0, 1)] private float availableTint = .38f;
        [SerializeField, Range(0, 1)] private float hoveredTint = .76f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private readonly List<HighlightSlot> slots = new List<HighlightSlot>();
        private readonly List<MeshSurface> meshSurfaces = new List<MeshSurface>();
        private bool meshSurfacesReady;
        private bool highlighted, hovered;

        public int StationIndex => stationIndex;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public Renderer[] HighlightRenderers => highlightRenderers;
        public Collider[] HitColliders => hitColliders;

        public float SurfaceDistance(Vector3 position)
        {
            float nearest = float.PositiveInfinity;
            foreach (var renderer in highlightRenderers)
                if (renderer && renderer.enabled && renderer.gameObject.activeInHierarchy)
                    nearest = Mathf.Min(nearest, Vector3.Distance(position, renderer.bounds.ClosestPoint(position)));
            foreach (var collider in hitColliders)
                if (collider && collider.enabled && !collider.isTrigger && collider.gameObject.activeInHierarchy)
                    nearest = Mathf.Min(nearest, Vector3.Distance(position, collider.ClosestPoint(position)));
            return nearest;
        }

        /// <summary>Eligibility uses visible surface samples; an actual click still requires TryHit's exact mesh ray.</summary>
        public bool HasVisibleSurface(Camera camera, Vector3 playerPosition, float maximumRange)
        {
            if (!camera || !isActiveAndEnabled || SurfaceDistance(playerPosition) > maximumRange) return false;
            bool Visible(Vector3 sample)
            {
                Vector3 screen = camera.WorldToViewportPoint(sample);
                if (screen.z <= camera.nearClipPlane || screen.x < 0 || screen.x > 1 || screen.y < 0 || screen.y > 1) return false;
                Ray ray = camera.ViewportPointToRay(screen);
                return TryHit(ray, out float distance) && Vector3.Distance(playerPosition, ray.GetPoint(distance)) <= maximumRange;
            }
            foreach (var renderer in highlightRenderers)
            {
                if (!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                Bounds bounds = renderer.bounds;
                if (Visible(bounds.center) || Visible(bounds.ClosestPoint(camera.transform.position))) return true;
                for (int axis = 0; axis < 3; axis++)
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 offset = Vector3.zero; offset[axis] = bounds.extents[axis] * .85f * side;
                        if (Visible(bounds.center + offset)) return true;
                    }
            }
            foreach (var collider in hitColliders)
                if (collider && collider.enabled && !collider.isTrigger && collider.gameObject.activeInHierarchy
                    && (Visible(collider.bounds.center) || Visible(collider.ClosestPoint(camera.transform.position)))) return true;
            return false;
        }

        public bool TryGetFramingBounds(float floorHeight, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (var renderer in highlightRenderers)
            {
                if (!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                Bounds part = renderer.bounds;
                // Buried supports do not need to fill the selection view. This affects framing only,
                // never the real mesh hit-test, zone bounds or highlighted surfaces.
                Vector3 low = part.min, high = part.max;
                low.y = Mathf.Max(low.y, floorHeight - 1);
                high.y = Mathf.Min(high.y, floorHeight + 12);
                if (low.y > high.y) continue;
                part.SetMinMax(low, high);
                if (!found) bounds = part; else bounds.Encapsulate(part);
                found = true;
            }
            return found;
        }

        public void Configure(int index, string label, Renderer[] renderers, Collider[] colliders)
        {
            RestoreHighlight();
            stationIndex = index;
            displayName = label;
            highlightRenderers = renderers != null ? (Renderer[])renderers.Clone() : Array.Empty<Renderer>();
            hitColliders = colliders != null ? (Collider[])colliders.Clone() : Array.Empty<Collider>();
            meshSurfaces.Clear();
            meshSurfacesReady = false;
        }

        public void SetHighlight(bool visible, bool hovered)
        {
            if (!visible || !isActiveAndEnabled)
            {
                RestoreHighlight();
                return;
            }
            if (!highlighted) CaptureHighlight();
            highlighted = true;
            this.hovered = hovered;
            ApplyHighlight();
        }

        /// <summary>Tests collider/mesh surfaces, then rejects nearer solid occluders.</summary>
        public bool TryHit(Ray ray, out float distance)
        {
            distance = float.PositiveInfinity;
            if (!isActiveAndEnabled || ray.direction.sqrMagnitude < .000001f)
                return false;
            ray.direction = ray.direction.normalized;
            if (hitColliders != null) foreach (var collider in hitColliders)
            {
                if (!collider || !collider.enabled || collider.isTrigger || !collider.gameObject.activeInHierarchy)
                    continue;
                if (collider.Raycast(ray, out RaycastHit hit, distance) && hit.distance < distance)
                    distance = hit.distance;
            }
            if (!meshSurfacesReady) CacheMeshSurfaces();
            foreach (var surface in meshSurfaces)
                if (surface.TryHit(ray, distance, out float meshDistance)) distance = meshDistance;
            if (float.IsPositiveInfinity(distance)) return false;
            // No generous bounds fallback: a click through a frame's empty centre is not a hit.
            // The small tolerance excludes the selected surface itself from the occlusion query.
            if (Physics.Raycast(ray, Mathf.Max(0, distance - .01f), Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
            {
                distance = float.PositiveInfinity;
                return false;
            }
            return true;
        }

        private void CacheMeshSurfaces()
        {
            meshSurfacesReady = true;
            if (highlightRenderers == null) return;
            foreach (var renderer in highlightRenderers)
            {
                if (!(renderer is MeshRenderer)) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh || !filter.sharedMesh.isReadable) continue;
                bool duplicate = false;
                foreach (var existing in meshSurfaces)
                    if (existing.Renderer == renderer) { duplicate = true; break; }
                if (!duplicate) meshSurfaces.Add(new MeshSurface(renderer, filter.sharedMesh));
            }
        }

        private void LateUpdate()
        {
            if (highlighted) ApplyHighlight();
        }

        private void OnDisable() => RestoreHighlight();
        private void OnDestroy() => RestoreHighlight();

        private void CaptureHighlight()
        {
            slots.Clear();
            if (highlightRenderers == null) return;
            for (int r = 0; r < highlightRenderers.Length; r++)
            {
                var renderer = highlightRenderers[r];
                if (!renderer) continue;
                bool duplicate = false;
                for (int earlier = 0; earlier < r; earlier++)
                    if (highlightRenderers[earlier] == renderer) { duplicate = true; break; }
                if (duplicate) continue;
                Material[] materials = renderer.sharedMaterials;
                for (int index = 0; index < materials.Length; index++)
                {
                    Material material = materials[index];
                    if (!material || (!material.HasProperty(BaseColorId) && !material.HasProperty(ColorId)))
                        continue;
                    var slot = new HighlightSlot(renderer, material, index);
                    renderer.GetPropertyBlock(slot.Original, index);
                    renderer.GetPropertyBlock(slot.Working, index);
                    slot.HasOriginal = !slot.Original.isEmpty;
                    slots.Add(slot);
                }
            }
        }

        private void ApplyHighlight()
        {
            Color target = hovered ? hoveredColor : availableColor;
            float amount = hovered ? hoveredTint : availableTint;
            foreach (var slot in slots)
            {
                if (!slot.Renderer || !slot.Material) continue;
                // Existing weather/optics feedback writes renderer-level blocks. Read their
                // current values without replacing them; our temporary override lives per slot.
                if (!slot.HasOriginal) slot.Renderer.GetPropertyBlock(slot.Working);
                MaterialPropertyBlock source = slot.HasOriginal ? slot.Original : slot.Working;
                int colorId = slot.Material.HasProperty(BaseColorId) ? BaseColorId : ColorId;
                Color original = source.HasColor(colorId) ? source.GetColor(colorId) : slot.Material.GetColor(colorId);
                Color tint = Color.Lerp(original, target, amount);
                tint.a = original.a;
                if (slot.Material.HasProperty(BaseColorId)) slot.Working.SetColor(BaseColorId, tint);
                if (slot.Material.HasProperty(ColorId)) slot.Working.SetColor(ColorId, tint);
                if (slot.Material.HasProperty(EmissionColorId) && slot.Material.IsKeywordEnabled("_EMISSION"))
                {
                    Color emission = source.HasColor(EmissionColorId) ? source.GetColor(EmissionColorId) : slot.Material.GetColor(EmissionColorId);
                    slot.Working.SetColor(EmissionColorId, emission + target * (hovered ? .45f : .18f));
                }
                slot.Renderer.SetPropertyBlock(slot.Working, slot.Index);
            }
        }

        private void RestoreHighlight()
        {
            foreach (var slot in slots)
                if (slot.Renderer) slot.Renderer.SetPropertyBlock(slot.HasOriginal ? slot.Original : null, slot.Index);
            slots.Clear();
            highlighted = hovered = false;
        }

        private sealed class HighlightSlot
        {
            public readonly Renderer Renderer;
            public readonly Material Material;
            public readonly int Index;
            public readonly MaterialPropertyBlock Original = new MaterialPropertyBlock();
            public readonly MaterialPropertyBlock Working = new MaterialPropertyBlock();
            public bool HasOriginal;
            public HighlightSlot(Renderer renderer, Material material, int index)
            { Renderer = renderer; Material = material; Index = index; }
        }

        private sealed class MeshSurface
        {
            public readonly Renderer Renderer;
            private readonly Transform transform;
            private readonly Vector3[] vertices;
            private readonly int[] triangles;
            public MeshSurface(Renderer renderer, Mesh mesh)
            {
                Renderer = renderer;
                transform = renderer.transform;
                vertices = mesh.vertices;
                triangles = mesh.triangles;
            }

            public bool TryHit(Ray worldRay, float limit, out float distance)
            {
                distance = limit;
                if (!Renderer || !Renderer.enabled || !Renderer.gameObject.activeInHierarchy ||
                    !Renderer.bounds.IntersectRay(worldRay, out float boundsDistance) || boundsDistance > limit)
                    return false;
                Matrix4x4 worldToLocal = transform.worldToLocalMatrix;
                Vector3 origin = worldToLocal.MultiplyPoint3x4(worldRay.origin);
                // Do not normalize after transforming: t remains world distance even under scale.
                Vector3 direction = worldToLocal.MultiplyVector(worldRay.direction);
                bool found = false;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 a = vertices[triangles[i]];
                    Vector3 ab = vertices[triangles[i + 1]] - a;
                    Vector3 ac = vertices[triangles[i + 2]] - a;
                    Vector3 p = Vector3.Cross(direction, ac);
                    float determinant = Vector3.Dot(ab, p);
                    if (Mathf.Abs(determinant) < .00000001f) continue;
                    float inverse = 1f / determinant;
                    Vector3 fromA = origin - a;
                    float u = Vector3.Dot(fromA, p) * inverse;
                    if (u < 0 || u > 1) continue;
                    Vector3 q = Vector3.Cross(fromA, ab);
                    float v = Vector3.Dot(direction, q) * inverse;
                    if (v < 0 || u + v > 1) continue;
                    float t = Vector3.Dot(ac, q) * inverse;
                    if (t < 0 || t >= distance) continue;
                    distance = t;
                    found = true;
                }
                return found;
            }
        }
    }
}
