using CarWash.Player;
using NUnit.Framework;
using UnityEngine;

namespace CarWash.Tests
{
    /// <summary>M4.6 B1: jumping with W+A/D held while turning the mouse must not build up speed.</summary>
    public class MotorAirStrafeTests
    {
        const float Sprint = 7f, Air = 8f, Dt = 1f / 60f;

        /// <summary>Strafe-jump bot: in the air, holds W+D and keeps turning so the wish direction stays ~80° off the velocity.</summary>
        static float Simulate(bool fixedMath, float seconds)
        {
            var v = new Vector3(0f, 0f, Sprint);
            float max = 0f;
            for (float t = 0; t < seconds; t += Dt)
            {
                var input = MotorMath.ClampInput(new Vector2(1f, 1f));
                if (!fixedMath) input = new Vector2(1f, 1f); // old code: unnormalised diagonal
                float wishSpeed = Sprint * input.magnitude;
                var velDir = v.sqrMagnitude > 0 ? v.normalized : Vector3.forward;
                var wish = Quaternion.Euler(0f, 80f, 0f) * velDir; // mouse turning keeps the wish ahead of the velocity
                v = fixedMath ? MotorMath.AirAccelerate(v, wish, wishSpeed, Air, Sprint, Dt) : MotorMath.Accelerate(v, wish, wishSpeed, Air, Dt);
                max = Mathf.Max(max, v.magnitude);
            }
            return max;
        }

        [Test]
        public void OldAirMath_ReproducesTheSpeedUp() => Assert.Greater(Simulate(false, 3f), Sprint * 1.5f, "the bug: strafing stacks speed");

        [Test]
        public void AirStrafe_NeverExceedsSprintSpeed() => Assert.LessOrEqual(Simulate(true, 10f), Sprint + 1e-3f);

        [Test]
        public void DiagonalInput_IsNotFaster()
        {
            Assert.AreEqual(1f, MotorMath.ClampInput(new Vector2(1f, 1f)).magnitude, 1e-4f);
            Assert.AreEqual(0.5f, MotorMath.ClampInput(new Vector2(0f, 0.5f)).magnitude, 1e-4f);
        }

        [Test]
        public void AirControl_StillSteersAndKeepsKnockbackSpeed()
        {
            // a faster launch (e.g. leaving a slide) keeps its speed but is not increased
            var v = new Vector3(0, 0, 10f);
            var after = MotorMath.AirAccelerate(v, Vector3.right, Sprint, Air, Sprint, Dt);
            Assert.LessOrEqual(after.magnitude, 10f + 1e-3f);
            Assert.Greater(after.x, 0f, "still steers");
        }
    }
}

namespace CarWash.Tests
{
    /// <summary>M4.6 B2: prop throws are twice as strong (old: charge × 14 × massScale).</summary>
    public class ThrowForceTests
    {
        [TestCase(2f)]
        [TestCase(20f)]
        public void PropThrow_IsTwiceTheOldForce(float mass)
        {
            float massScale = Mathf.Clamp(8f / mass, 0.3f, 1f);
            float old = 1f * 14f * massScale;
            var v = PlayerGrabber.PropThrowVelocity(Vector3.zero, Vector3.forward, 1f, 14f, mass);
            Assert.AreEqual(old * 2f, v.magnitude, 1e-3f);
        }
    }
}
