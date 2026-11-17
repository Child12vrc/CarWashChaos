using CarWash.Editor;
using CarWash.Player;
using NUnit.Framework;
using UnityEngine;

namespace CarWash.Tests
{
    public class MotorMathTests
    {
        [Test]
        public void FoamSlide_IsMuchLongerThanNormalStop()
        {
            var t = DataBuilder.LoadSources().balance.player;
            float normal = MotorMath.StoppingDistance(t.sprintSpeed, t.friction);
            float foam = MotorMath.StoppingDistance(t.sprintSpeed, t.foamFriction);
            Assert.Less(normal, 1.0f, "normal ground should stop quickly");
            Assert.Greater(foam, 4f * normal, "foam slide must be obvious");
            Assert.Greater(foam, 2.5f);
        }

        [Test]
        public void Accelerate_ReachesButNeverExceedsWishSpeed()
        {
            var v = Vector3.zero;
            for (int i = 0; i < 120; i++) v = MotorMath.Accelerate(v, Vector3.forward, 4.5f, 40f, 1f / 60f);
            Assert.AreEqual(4.5f, v.magnitude, 0.01f);
        }

        [Test]
        public void Knockback_DecaysToZero()
        {
            var k = new Vector3(10f, 0, 0);
            for (int i = 0; i < 300; i++) k = MotorMath.DecayKnockback(k, 6f, 1f / 60f);
            Assert.AreEqual(Vector3.zero, k);
        }

        [Test]
        public void Knockback_HalfLife_MatchesRate()
        {
            var k = MotorMath.DecayKnockback(new Vector3(10f, 0, 0), 6f, Mathf.Log(2f) / 6f);
            Assert.AreEqual(5f, k.x, 0.01f);
        }
    }
}
