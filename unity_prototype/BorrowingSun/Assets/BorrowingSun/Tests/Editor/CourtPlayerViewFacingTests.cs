using NUnit.Framework;
using UnityEngine;

namespace BorrowingSun.Tests
{
    public sealed class CourtPlayerViewFacingTests
    {
        private GameObject actor, cameraObject;
        private CourtPlayer player;

        [SetUp] public void Setup()
        {
            cameraObject = new GameObject("view-facing-camera");
            var camera = cameraObject.AddComponent<Camera>();
            actor = new GameObject("view-facing-player");
            player = actor.AddComponent<CourtPlayer>();
            player.InitializeGameplay(null, camera, Vector3.zero);
        }
        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(actor);
            Object.DestroyImmediate(cameraObject);
        }
        private void AssertFacing(Vector3 expected)
        {
            Assert.That(Vector3.Angle(player.FacingDirection, expected), Is.LessThan(.01f));
            Assert.That(player.FacingDirection.y, Is.EqualTo(0).Within(.0001f));
        }

        [Test] public void PlatformRotationPreservesBoundHeadingAcrossWrappedYaw()
        {
            player.SetViewFacing(Vector3.forward);
            player.CarryByPlatform(Vector3.right, 170);
            player.CarryByPlatform(Vector3.forward, 200);
            Assert.IsTrue(player.ViewFacingActive);
            AssertFacing(Quaternion.Euler(0, 10, 0) * Vector3.forward);
            Assert.That(player.CarriedYaw, Is.EqualTo(10).Within(.001f));
            Assert.That(Vector3.Distance(player.FeetPosition, Vector3.right + Vector3.forward), Is.LessThan(.001f));
            player.ClearViewFacing();
            player.CarryByPlatform(Vector3.zero, -30);
            Assert.IsFalse(player.ViewFacingActive, "Legacy platform carry must not acquire view ownership.");
            AssertFacing(Quaternion.Euler(0, -20, 0) * Vector3.forward);
        }

        [Test] public void ClearAndReplayReleaseBindingWithoutOverwritingLegacyFacing()
        {
            // Real OnDisable delivery belongs to the Play camera lifecycle fixture.
            Assert.IsFalse(player.ViewFacingActive, "Existing scenes start with locomotion-owned facing.");
            player.SetViewFacing(Vector3.right);
            player.ClearViewFacing();
            Assert.IsFalse(player.ViewFacingActive);
            AssertFacing(Vector3.right);
            player.SetViewFacing(Vector3.right);
            player.ResetPlayer();
            Assert.IsFalse(player.ViewFacingActive);
            AssertFacing(Vector3.right);
        }

        [Test] public void SwimmingDoesNotAcquireViewBindingAndKeepsAnExistingBoundHeading()
        {
            player.ConfigureSwimming(true, -3, 2);
            Assert.IsFalse(player.ViewFacingActive);
            player.SetViewFacing(new Vector3(3, 8, 0));
            AssertFacing(Vector3.right);
            player.ConfigureSwimming(false);
            Assert.IsTrue(player.ViewFacingActive, "Changing locomotion medium must not leave a shoulder turn unbound.");
            AssertFacing(Vector3.right);
            player.Teleport(Vector3.back);
            Assert.IsFalse(player.ViewFacingActive, "Explicit relocation releases the old movement binding.");
        }

        [Test] public void InvalidHeadingReleasesBindingWithoutPoisoningCharacterFacing()
        {
            player.SetViewFacing(Vector3.right);
            player.SetViewFacing(new Vector3(float.NaN, 0, 0));
            Assert.IsFalse(player.ViewFacingActive);
            AssertFacing(Vector3.right);
            player.SetViewFacing(Vector3.forward);
            player.SetViewFacing(Vector3.up);
            Assert.IsFalse(player.ViewFacingActive);
            AssertFacing(Vector3.forward);
        }
    }
}
