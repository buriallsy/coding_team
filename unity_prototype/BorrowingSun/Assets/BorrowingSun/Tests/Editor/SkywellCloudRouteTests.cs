using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BorrowingSun.Tests
{
    public sealed class SkywellCloudRouteTests
    {
        private readonly List<Object> objects = new List<Object>();
        private GameObject Make(string name)
        {
            var value = new GameObject(name); objects.Add(value); return value;
        }
        [TearDown] public void Cleanup()
        {
            for (int i = objects.Count - 1; i >= 0; i--) if (objects[i]) Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }
        [TestCase(0f)]
        [TestCase(1.2f)]
        [TestCase(-1.2f)]
        public void ReturnArcReachesItsRealLandingOnTheDescendingHalf(float difference)
        {
            Vector3 from = new Vector3(2, 28, 4), to = from + new Vector3(4, difference, 0);
            Assert.IsTrue(SkywellDeerRide.TryReturnFlight(from, to, 2.6f, 20, 6, out Vector3 velocity, out float duration));
            Vector3 actual = from + velocity * duration + Vector3.down * (10 * duration * duration);
            Assert.That(Vector3.Distance(actual, to), Is.LessThan(.0001f));
            Assert.That(Vector3.ProjectOnPlane(velocity, Vector3.up).magnitude, Is.LessThanOrEqualTo(6.001f));
            Assert.That(velocity.y - 20 * duration, Is.LessThan(0), "The planned return lands while descending, not by teleporting up a face.");
        }
        [Test] public void ReturnRejectsUnreachableGeometryAndInvalidPhysics()
        {
            Assert.IsFalse(SkywellDeerRide.TryReturnFlight(Vector3.zero, new Vector3(8, 1.2f, 0), 2.6f, 20, 6, out _, out _));
            Assert.IsFalse(SkywellDeerRide.TryReturnFlight(Vector3.zero, Vector3.up * 2.7f, 2.6f, 20, 6, out _, out _));
            Assert.IsFalse(SkywellDeerRide.TryReturnFlight(Vector3.zero, Vector3.right, 2.6f, 0, 6, out _, out _));
            Assert.IsFalse(SkywellDeerRide.TryReturnFlight(Vector3.zero, Vector3.right, 2.6f, 20, float.NaN, out _, out _));
        }
        [Test] public void FootprintUsesActualMeshAndRejectsItsVisibleHoleOrRim()
        {
            var mesh = new Mesh { name = "cloud test ring with actual hole" }; objects.Add(mesh);
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            AddRectangle(vertices, triangles, -3, -.7f, -3, 3);
            AddRectangle(vertices, triangles, .7f, 3, -3, 3);
            AddRectangle(vertices, triangles, -.7f, .7f, -3, -.7f);
            AddRectangle(vertices, triangles, -.7f, .7f, .7f, 3);
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            var floor = Make("ring landing").AddComponent<MeshCollider>(); floor.sharedMesh = mesh;
            var ride = Make("ride").AddComponent<SkywellDeerRide>();
            ride.Configure(null, null, null, null, new Collider[] { floor }, null);
            Physics.SyncTransforms();
            Assert.IsTrue(ride.TryRouteFootprint(new Vector3(2, .03f, 0), .3f, out Collider actual));
            Assert.That(actual, Is.SameAs(floor));
            Assert.IsFalse(ride.TryRouteFootprint(new Vector3(0, .03f, 0), .3f, out _), "AABB covers the hole but the mesh does not.");
            Assert.IsFalse(ride.TryRouteFootprint(new Vector3(.8f, .03f, 0), .3f, out _), "A real centre hit is insufficient when the foot extends over the hole.");
            floor.enabled = false; Physics.SyncTransforms();
            Assert.IsFalse(ride.TryRouteFootprint(new Vector3(2, .03f, 0), .3f, out _));
        }
        [Test] public void HumanActivationIsLocalToCloudsAndEndpointApronsAndStillRequiresEarnedUnlock()
        {
            var owner = Make("cloud court").AddComponent<SkywellController>();
            var camera = Make("cloud camera").AddComponent<Camera>();
            var player = Make("cloud player").AddComponent<CourtPlayer>();
            player.InitializeGameplay(owner, camera, new Vector3(9, .03f, 0));
            using (var serialized = new SerializedObject(owner))
            {
                serialized.FindProperty("player").objectReferenceValue = player;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var island = Floor("wide island", Vector3.zero, new Vector3(20, 1, 20));
            var cloud = Floor("cloud", Vector3.right * 14, new Vector3(3, 1, 3));
            var village = Floor("wide village", Vector3.right * 28, new Vector3(20, 1, 20));
            var ride = Make("cloud ride").AddComponent<SkywellDeerRide>();
            ride.Configure(owner, null, null, null, new Collider[] { island, cloud, village }, null);
            var foot = Make("local human driver").AddComponent<SkywellCloudFootTravel>(); foot.Configure(owner, ride);
            Physics.SyncTransforms();
            Assert.IsTrue(foot.TryLocalSupport(new Vector3(9, .03f, 0), out _));
            Assert.IsTrue(foot.TryLocalSupport(new Vector3(14, .03f, 0), out _));
            Assert.IsFalse(foot.TryLocalSupport(new Vector3(0, .03f, 0), out _), "No whole-island jump grant.");
            Assert.IsFalse(foot.TryLocalSupport(new Vector3(30, .03f, 0), out _), "No whole-village jump grant.");
            Assert.IsFalse(foot.CanJumpHere, "Standing in the local area alone cannot grant first-crossing progression.");
            Assert.IsFalse(ride.FootRouteUnlocked || foot.IsActive || player.IsExternallyDriven);
            Assert.That(ride.FootDriver, Is.SameAs(foot));
            ride.ResetRide();
            Assert.IsFalse(ride.FootRouteUnlocked || ride.IsReturning || foot.IsActive);
        }
        [Test] public void EndpointExitAcceptsARealMixedIslandPathSeamWithoutMakingThePathJumpable()
        {
            var foot = MakeExitFixture(out var ride, out var island, out _, out _, out var path);
            Vector3 seam = new Vector3(1.95f, .03f, 0);
            Assert.IsFalse(ride.TryRouteFootprint(seam, .3f, out _), "One footprint edge is on the authored path, outside the cloud-route set.");
            Assert.IsTrue(foot.HasEndpointExitFootprint(seam, island), "Mixed real island/path support must hand back ordinary walking.");
            Assert.IsTrue(foot.HasEndpointExitFootprint(new Vector3(2.3f, .03f, 0), island), "The immediately adjacent path is a valid endpoint exit.");
            Assert.IsFalse(foot.TryLocalSupport(new Vector3(2.3f, .03f, 0), out _), "Walking off the route cannot extend the Space-jump activation area.");
            path.enabled = false; Physics.SyncTransforms();
            Assert.IsFalse(foot.HasEndpointExitFootprint(seam, island), "A missing outer foot point is still a cliff, not an exit.");
        }
        [Test] public void EndpointExitRejectsTriggersAirborneFeetAndNonEndpointLandings()
        {
            var foot = MakeExitFixture(out _, out var island, out var cloud, out _, out var path);
            Vector3 seam = new Vector3(1.95f, .03f, 0);
            Assert.IsFalse(foot.HasEndpointExitFootprint(seam + Vector3.up, island), "Air above a path must retain jump/recovery ownership.");
            Assert.IsFalse(foot.HasEndpointExitFootprint(seam, cloud), "The last stable cloud cannot release movement at an endpoint it never reached.");
            Assert.IsFalse(foot.HasEndpointExitFootprint(new Vector3(0, .03f, 0), island), "Pure route support is not a non-route exit.");
            path.isTrigger = true; Physics.SyncTransforms();
            Assert.IsFalse(foot.HasEndpointExitFootprint(seam, island), "Visual or trigger surfaces cannot supply the missing foot support.");
        }
        [Test] public void EndpointExitRejectsARealHoleAndACloudSurfaceAtTheSeam()
        {
            var foot = MakeExitFixture(out _, out var island, out var cloud, out _, out var path);
            Vector3 seam = new Vector3(1.95f, .03f, 0);
            path.enabled = false;
            var mesh = new Mesh { name = "walking path with a foot-sized hole" }; objects.Add(mesh);
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            AddRectangle(vertices, triangles, 2, 4, -2, -.15f);
            AddRectangle(vertices, triangles, 2, 4, .15f, 2);
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            var holePath = Make("holed path").AddComponent<MeshCollider>(); holePath.sharedMesh = mesh;
            Physics.SyncTransforms();
            Assert.IsFalse(foot.HasEndpointExitFootprint(seam, island), "The path bounds cover the foot but its actual hole has no support.");
            holePath.enabled = false;
            cloud.transform.position = path.transform.position; cloud.size = path.size;
            Physics.SyncTransforms();
            Assert.IsFalse(foot.HasEndpointExitFootprint(seam, island), "A mixed island/cloud edge remains part of jumping, not ordinary walking.");
        }
        private SkywellCloudFootTravel MakeExitFixture(out SkywellDeerRide ride, out BoxCollider island,
            out BoxCollider cloud, out BoxCollider village, out BoxCollider path)
        {
            var owner = Make("endpoint court").AddComponent<SkywellController>();
            var camera = Make("endpoint camera").AddComponent<Camera>();
            var player = Make("endpoint player").AddComponent<CourtPlayer>();
            player.InitializeGameplay(owner, camera, new Vector3(0, .03f, 0));
            using (var serialized = new SerializedObject(owner))
            {
                serialized.FindProperty("player").objectReferenceValue = player;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            island = Floor("island endpoint", Vector3.zero, new Vector3(4, 1, 4));
            cloud = Floor("route cloud", Vector3.right * 6, new Vector3(2, 1, 2));
            village = Floor("village endpoint", Vector3.right * 12, new Vector3(4, 1, 4));
            path = Floor("authored ordinary path", Vector3.right * 3, new Vector3(2, 1, 4));
            ride = Make("endpoint ride").AddComponent<SkywellDeerRide>();
            ride.Configure(owner, null, null, null, new Collider[] { island, cloud, village }, null);
            var foot = Make("endpoint foot driver").AddComponent<SkywellCloudFootTravel>(); foot.Configure(owner, ride);
            Physics.SyncTransforms();
            return foot;
        }
        private BoxCollider Floor(string name, Vector3 top, Vector3 size)
        {
            var collider = Make(name).AddComponent<BoxCollider>();
            collider.size = size; collider.transform.position = top - Vector3.up * size.y * .5f; return collider;
        }
        private static void AddRectangle(List<Vector3> vertices, List<int> triangles, float x0, float x1, float z0, float z1)
        {
            int start = vertices.Count;
            vertices.Add(new Vector3(x0, 0, z0)); vertices.Add(new Vector3(x0, 0, z1));
            vertices.Add(new Vector3(x1, 0, z1)); vertices.Add(new Vector3(x1, 0, z0));
            triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }
    }
}
