using NUnit.Framework;
using UnityEngine;

namespace BorrowingSun.Tests
{
    public sealed class CourtPlayerExternalMotionTests
    {
        private GameObject actor, cameraObject, ownerObject, otherObject;
        private CourtPlayer player;
        private CharacterController body;
        private SkywellDeerRide owner, other;
        [SetUp] public void Setup()
        {
            cameraObject = new GameObject("external-camera");
            actor = new GameObject("external-player");
            ownerObject = new GameObject("external-owner"); otherObject = new GameObject("other-owner");
            owner = ownerObject.AddComponent<SkywellDeerRide>(); other = otherObject.AddComponent<SkywellDeerRide>();
            player = actor.AddComponent<CourtPlayer>();
            player.InitializeGameplay(null, cameraObject.AddComponent<Camera>(), Vector3.zero);
            body = actor.GetComponent<CharacterController>();
        }
        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(ownerObject); Object.DestroyImmediate(otherObject);
            Object.DestroyImmediate(actor); Object.DestroyImmediate(cameraObject);
        }
        [Test] public void OnlyOneExternalOwnerCanMoveAndReleaseTheExistingCollisionBody()
        {
            Assert.IsTrue(player.BeginExternalMotion(owner));
            Assert.IsTrue(player.enabled && body.enabled, "Camera-facing and interaction input remain alive while mounted.");
            Assert.IsTrue(player.IsExternallyDriven);
            Assert.IsFalse(player.BeginExternalMotion(other));
            Vector3 before = player.FeetPosition;
            player.MoveExternal(other, Vector3.right, Vector3.right, 1);
            Assert.That(player.FeetPosition, Is.EqualTo(before));
            player.EndExternalMotion(other);
            Assert.IsTrue(player.ExternalMotionOwnedBy(owner));
            player.EndExternalMotion(owner);
            Assert.IsFalse(player.IsExternallyDriven);
        }
        [Test] public void ExplicitTeleportReleasesExternalOwnershipAndRestoresNormalCapsule()
        {
            float radius = body.radius, height = body.height, step = body.stepOffset;
            Vector3 center = body.center;
            Assert.IsTrue(player.BeginExternalMotion(owner, .58f, 2.3f));
            Assert.That(body.radius, Is.EqualTo(.58f).Within(.001f));
            Assert.That(body.height, Is.EqualTo(2.3f).Within(.001f));
            player.Teleport(Vector3.forward * 2);
            Assert.IsFalse(player.IsExternallyDriven);
            Assert.That(body.radius, Is.EqualTo(radius).Within(.001f));
            Assert.That(body.height, Is.EqualTo(height).Within(.001f));
            Assert.That(body.center, Is.EqualTo(center));
            Assert.That(body.stepOffset, Is.EqualTo(step).Within(.001f));
            player.MoveExternal(owner, Vector3.right, Vector3.right, 1);
            Assert.That(player.FeetPosition, Is.EqualTo(Vector3.forward * 2));
        }
        [Test] public void ExternalMovementRespectsShoulderFacingButFixedViewCanTurnWithTravel()
        {
            Assert.IsTrue(player.BeginExternalMotion(owner));
            player.SetViewFacing(Vector3.forward);
            player.MoveExternal(owner, Vector3.zero, Vector3.back, .1f);
            Assert.That(Vector3.Angle(player.FacingDirection, Vector3.forward), Is.LessThan(.01f));
            player.ClearViewFacing();
            player.MoveExternal(owner, Vector3.zero, Vector3.right, .1f);
            Assert.That(Vector3.Angle(player.FacingDirection, Vector3.right), Is.LessThan(.01f));
            player.ResetPlayer();
            Assert.IsFalse(player.IsExternallyDriven);
        }
    }
}
