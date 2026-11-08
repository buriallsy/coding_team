using NUnit.Framework;
using UnityEngine;

namespace BorrowingSun.Tests
{
    public sealed class SkywellJumpAssistTests
    {
        [Test] public void EdgeGraceAllowsOneJumpButNotAnAirJump()
        {
            var input = new SkywellJumpAssist();
            input.Tick(.016f, true, false);
            input.Tick(.08f, false, true);
            Assert.IsTrue(input.Consume());
            input.Tick(.016f, false, true);
            Assert.IsFalse(input.Consume(), "Pressing again cannot grant another airborne jump.");
        }
        [Test] public void LatePressExpiresAndCannotUseOldSupport()
        {
            var input = new SkywellJumpAssist();
            input.Tick(.016f, true, false);
            input.Tick(.2f, false, true);
            Assert.IsFalse(input.Consume());
        }
        [Test] public void EarlyPressSurvivesUntilPhysicalLandingOnlyWithinBuffer()
        {
            var input = new SkywellJumpAssist();
            input.Tick(.016f, false, true);
            Assert.IsFalse(input.Consume());
            input.Tick(.08f, true, false);
            Assert.IsTrue(input.Consume());
            input.Reset();
            input.Tick(.016f, false, true);
            input.Tick(.2f, true, false);
            Assert.IsFalse(input.Consume());
        }
        [Test] public void ResetAndModalCancelClearPendingInput()
        {
            var input = new SkywellJumpAssist();
            input.Tick(.016f, true, true); input.Reset();
            Assert.IsFalse(input.Consume());
            input.Tick(.016f, true, false);
            Assert.IsFalse(input.Consume());
        }
        [Test] public void NeutralAirInputRetainsMoreMomentumThanGroundBraking()
        {
            var input = new SkywellJumpAssist();
            var air = input.Steer(Vector3.forward * 6, Vector3.zero, false, 28, .1f);
            var ground = input.Steer(Vector3.forward * 6, Vector3.zero, true, 28, .1f);
            Assert.That(air.z, Is.EqualTo(5.4f).Within(.001f));
            Assert.Greater(air.z, ground.z);
            Assert.LessOrEqual(input.Steer(Vector3.zero, Vector3.right * 6, false, 28, 1).magnitude, 6);
        }
        [Test] public void DirectionalStandingJumpHasReadableLaunchWithoutChangingSpeedLimit()
        {
            var input = new SkywellJumpAssist();
            var desired = new Vector3(1,0,1).normalized * 6;
            Assert.That(input.LaunchVelocity(Vector3.zero, desired).magnitude, Is.EqualTo(5.4f).Within(.001f));
            Assert.Less(Vector3.Angle(desired, input.LaunchVelocity(Vector3.left * 6, desired)), .01f);
            Assert.That(input.LaunchVelocity(Vector3.forward * 6, desired).magnitude, Is.EqualTo(6).Within(.001f));
            Assert.AreEqual(Vector3.zero, input.LaunchVelocity(Vector3.zero, Vector3.zero));
        }
    }
}
