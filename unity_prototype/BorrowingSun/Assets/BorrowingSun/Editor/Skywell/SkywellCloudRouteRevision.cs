using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BorrowingSun.Editor
{
    /// <summary>One incremental author-scene migration; the caller owns backup and saving.</summary>
    public static class SkywellCloudRouteRevision
    {
        private const string RootPath = "Revision_15_Hidden_deer_grove/Unlocked_cloud_platforms";
        private const string IslandPath = "Revision_15_Hidden_deer_grove/Grove_island_solid";
        private const string NorthPath = "M1_Rotation_and_lift/Fixed_quays_and_guide_frame/North_fixed_service_gallery/Outer_mountain_court";
        private static readonly Vector3[] Tops =
        {
            new Vector3(14.6f,28.5f,136), new Vector3(11.2f,29.1f,131.6f),
            new Vector3(7.8f,27.9f,134.6f), new Vector3(3.6f,26.6f,130.5f)
        };
        private static readonly Vector2[] Sizes =
        {
            new Vector2(4.8f,4.2f), new Vector2(3.4f,3.2f),
            new Vector2(3.8f,3.6f), new Vector2(4.5f,4.5f)
        };

        public static void Apply(SkywellController court)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !court || court.gameObject.scene.path != SkywellBuilder.ScenePath)
                throw new InvalidOperationException("Apply the cloud route only to the L5 author scene outside Play Mode; the caller must back up and save it.");
            var grove = court.Grove;
            var ride = grove ? grove.Ride : null;
            var root = Required(court.transform, RootPath);
            var island = Required(court.transform, IslandPath).GetComponent<Collider>();
            var north = Required(court.transform, NorthPath).GetComponent<Collider>();
            if (!grove || !ride || grove.CloudPlatforms != root || !island || !north)
                throw new InvalidOperationException("The existing grove, cloud root and safe floors must already be connected.");
            if (root.Find("Cloud_platform_3") || root.Find("Landing_3") || root.Find("Cloud_soft_rim_3") || ride.SafePlatforms.Length != 5)
                throw new InvalidOperationException("Expected the original three-cloud route. This is a one-time migration; use Audit after applying it.");
            if (Vector3.Distance(root.lossyScale, Vector3.one) > .001f || Quaternion.Angle(root.rotation, Quaternion.identity) > .01f)
                throw new InvalidOperationException("Unexpected scaled or rotated cloud route root; inspect the current author geometry before migrating.");
            var platforms = new Transform[4]; var landings = new Transform[4]; var rims = new Transform[4];
            for (int i = 0; i < 3; i++)
            {
                platforms[i] = Required(root, "Cloud_platform_" + i);
                landings[i] = Required(root, "Landing_" + i);
                rims[i] = Required(root, "Cloud_soft_rim_" + i);
                var mesh = platforms[i].GetComponent<MeshFilter>();
                var collider = platforms[i].GetComponent<MeshCollider>();
                if (!mesh || !mesh.sharedMesh || !collider || collider.sharedMesh != mesh.sharedMesh || collider.isTrigger || !collider.enabled
                    || ride.SafePlatforms[i + 1] != collider || Quaternion.Angle(platforms[i].rotation, Quaternion.identity) > .01f)
                    throw new InvalidOperationException("The current cloud mesh/collider binding is invalid: " + platforms[i].name);
            }
            if (ride.SafePlatforms[0] != island || ride.SafePlatforms[4] != north)
                throw new InvalidOperationException("The original island and village endpoints must remain the safe-route endpoints.");

            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Four varied deer cloud platforms");
            try
            {
                // Copies keep the original mesh and material assets; no duplicate asset/GUID or physics system is created.
                platforms[3] = Clone(platforms[2], "Cloud_platform_3");
                landings[3] = Clone(landings[2], "Landing_3");
                rims[3] = Clone(rims[2], "Cloud_soft_rim_3");
                for (int i = 0; i < 4; i++)
                {
                    Undo.RecordObjects(new Object[] { platforms[i], landings[i], rims[i] }, "Place varied cloud platform");
                    var mesh = platforms[i].GetComponent<MeshFilter>().sharedMesh;
                    var size = mesh.bounds.size;
                    platforms[i].localScale = new Vector3(Sizes[i].x / size.x, 1, Sizes[i].y / size.z);
                    Vector3 localTop = new Vector3(mesh.bounds.center.x, mesh.bounds.max.y, mesh.bounds.center.z);
                    platforms[i].position = Tops[i] - platforms[i].TransformVector(localTop);
                    landings[i].position = Tops[i];
                    rims[i].position = Tops[i] - Vector3.up * .60f;
                    rims[i].localScale = new Vector3(Sizes[i].x * 1.18f, 1.35f, Sizes[i].y * 1.18f);
                }
                Undo.RecordObject(ride, "Rebind four physical cloud landing surfaces");
                using (var serialized = new SerializedObject(ride))
                {
                    var safe = serialized.FindProperty("safePlatforms"); safe.arraySize = 6;
                    safe.GetArrayElementAtIndex(0).objectReferenceValue = island;
                    for (int i = 0; i < 4; i++) safe.GetArrayElementAtIndex(i + 1).objectReferenceValue = platforms[i].GetComponent<Collider>();
                    safe.GetArrayElementAtIndex(5).objectReferenceValue = north;
                    serialized.ApplyModifiedProperties();
                }
                EditorUtility.SetDirty(ride);
                Audit(court, new List<string>());
                Undo.CollapseUndoOperations(group);
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }

        public static void Audit(SkywellController court, List<string> lines)
        {
            if (!court || lines == null) throw new ArgumentNullException(!court ? nameof(court) : nameof(lines));
            var grove = court.Grove; var ride = grove ? grove.Ride : null;
            var root = Required(court.transform, RootPath);
            var island = Required(court.transform, IslandPath).GetComponent<Collider>();
            var north = Required(court.transform, NorthPath).GetComponent<Collider>();
            if (!grove || !ride || grove.CloudPlatforms != root || ride.SafePlatforms.Length != 6
                || ride.SafePlatforms[0] != island || ride.SafePlatforms[5] != north)
                throw new InvalidOperationException("Expected island -> four cloud colliders -> original village floor, with the existing grove unlock owner.");
            if (root.Cast<Transform>().Count(t => t.name.StartsWith("Cloud_platform_", StringComparison.Ordinal)) != 4
                || root.Cast<Transform>().Count(t => t.name.StartsWith("Landing_", StringComparison.Ordinal)) != 4
                || root.Cast<Transform>().Count(t => t.name.StartsWith("Cloud_soft_rim_", StringComparison.Ordinal)) != 4)
                throw new InvalidOperationException("The cloud route must contain exactly four physical platforms, four landing markers and four soft rims.");
            var topPoints = new List<Vector3>();
            for (int i = 0; i < 4; i++)
            {
                var platform = Required(root, "Cloud_platform_" + i);
                var marker = Required(root, "Landing_" + i);
                var rim = Required(root, "Cloud_soft_rim_" + i);
                var filter = platform.GetComponent<MeshFilter>(); var collider = platform.GetComponent<MeshCollider>();
                if (!filter || !filter.sharedMesh || !filter.sharedMesh.isReadable || !collider || !collider.enabled || collider.isTrigger
                    || collider.sharedMesh != filter.sharedMesh || ride.SafePlatforms[i + 1] != collider || rim.GetComponentsInChildren<Collider>(true).Length != 0)
                    throw new InvalidOperationException("Cloud mesh, collision, safe-platform order or non-solid rim is invalid: " + platform.name);
                // Inactive colliders have empty physics bounds. Read the actual shared mesh through the saved Transform instead.
                Bounds bounds = WorldMeshBounds(filter);
                Vector3 top = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
                if (Vector3.Distance(marker.position, top) > .01f)
                    throw new InvalidOperationException("Landing marker no longer matches the actual mesh top: " + marker.name);
                topPoints.Add(top);
                lines.Add("CLOUD " + i + " top=" + top.ToString("F3") + " actual mesh bounds=" + bounds
                    + " mesh=" + AssetDatabase.GetAssetPath(filter.sharedMesh) + " collider=" + collider.name
                    + " rim(non-solid)=" + rim.position.ToString("F3") + " / scale=" + rim.localScale.ToString("F3"));
            }
            using (var serialized = new SerializedObject(ride))
            {
                float height = serialized.FindProperty("jumpHeight").floatValue;
                float speed = serialized.FindProperty("moveSpeed").floatValue;
                float gravity = serialized.FindProperty("gravity").floatValue;
                for (int i = 1; i < topPoints.Count; i++)
                    if (Mathf.Abs(topPoints[i].y - topPoints[i - 1].y) >= height)
                        throw new InvalidOperationException("A cloud height step reaches or exceeds the current deer jump apex.");
                lines.Add("Existing riding values retained: speed=" + speed + ", jumpHeight=" + height + ", gravity=" + gravity
                    + ". Physical bidirectional input validation is still required; geometry audit alone does not prove traversability.");
            }
            lines.Add("PASS: Four actual cloud meshes/colliders, matching landing markers and non-solid rims; existing unlock root activeSelf="
                + root.gameObject.activeSelf + ", root scale=" + root.localScale + "; island and northern village endpoint references retained. No scene save performed by this revision.");
        }

        private static Bounds WorldMeshBounds(MeshFilter filter)
        {
            var vertices = filter.sharedMesh.vertices;
            if (vertices.Length == 0) throw new InvalidOperationException("Cloud mesh has no vertices: " + filter.name);
            var bounds = new Bounds(filter.transform.TransformPoint(vertices[0]), Vector3.zero);
            for (int i = 1; i < vertices.Length; i++) bounds.Encapsulate(filter.transform.TransformPoint(vertices[i]));
            return bounds;
        }
        private static Transform Clone(Transform source, string name)
        {
            var copy = Object.Instantiate(source.gameObject, source.parent); copy.name = name;
            Undo.RegisterCreatedObjectUndo(copy, "Add fourth cloud route element"); return copy.transform;
        }
        private static Transform Required(Transform parent, string path) => parent.Find(path) ? parent.Find(path)
            : throw new InvalidOperationException("Missing authored cloud route object: " + path);
    }
}
