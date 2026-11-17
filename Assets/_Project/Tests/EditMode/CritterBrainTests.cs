using CarWash.Critters;
using NUnit.Framework;

namespace CarWash.Tests
{
    /// <summary>T3.1 CritterBrain transitions (TECH S07).</summary>
    public class CritterBrainTests
    {
        static CritterBrain Raccoon() => new CritterBrain(100f, 6f, 5f, needsStun: true, capturable: true);
        static CritterBrain Possum() => new CritterBrain(100f, 6f, 5f, needsStun: false, capturable: true);
        static CritterBrain Revealed(CritterBrain b) { b.Reveal(); b.Tick(1.01f); return b; }

        [Test] public void StartsHidden_IgnoresWaterAndBehaviour()
        {
            var b = Raccoon();
            Assert.AreEqual(CritterState.Hidden, b.State);
            Assert.IsFalse(b.AddWetness(500f)); Assert.AreEqual(0f, b.Wetness);
            Assert.IsFalse(b.TrySet(CritterState.Wander));
        }

        [Test] public void Reveal_LastsOneSecond_ThenIdle()
        {
            var b = Raccoon();
            Assert.IsTrue(b.Reveal()); Assert.AreEqual(CritterState.Reveal, b.State);
            b.Tick(0.5f); Assert.AreEqual(CritterState.Reveal, b.State);
            b.Tick(0.6f); Assert.AreEqual(CritterState.Idle, b.State);
            Assert.IsFalse(b.Reveal());
        }

        [Test] public void Wetness_Accumulates_StunsAtMax()
        {
            var b = Revealed(Raccoon());
            Assert.IsFalse(b.AddWetness(60f)); Assert.AreEqual(60f, b.Wetness, 1e-4);
            Assert.IsTrue(b.AddWetness(50f)); Assert.AreEqual(CritterState.Stunned, b.State); Assert.AreEqual(100f, b.Wetness, 1e-4);
        }

        [Test] public void Wetness_DecaysFivePerSecond_WhenFree()
        {
            var b = Revealed(Raccoon());
            b.AddWetness(50f); b.Tick(2f);
            Assert.AreEqual(40f, b.Wetness, 1e-4);
            b.Tick(100f); Assert.AreEqual(0f, b.Wetness, 1e-4);
        }

        [Test] public void Stun_EndsAfterStunTime_ResetsWetness_Flees()
        {
            var b = Revealed(Raccoon());
            b.AddWetness(100f);
            b.Tick(5.9f); Assert.AreEqual(CritterState.Stunned, b.State); Assert.AreEqual(100f, b.Wetness, 1e-4); // no decay while stunned
            b.Tick(0.2f); Assert.AreEqual(CritterState.Flee, b.State); Assert.AreEqual(0f, b.Wetness);
        }

        [Test] public void NeedsStun_CannotCaptureUntilStunned()
        {
            var b = Revealed(Raccoon());
            Assert.IsFalse(b.CanCapture); Assert.IsFalse(b.TryCarry(true));
            b.AddWetness(100f);
            Assert.IsTrue(b.CanCapture); Assert.IsTrue(b.TryCarry(true)); Assert.AreEqual(CritterState.Carried, b.State);
        }

        [Test] public void NoStunNeeded_Possum_CanBeGrabbedRightAway()
        {
            var b = Revealed(Possum());
            Assert.IsTrue(b.CanCapture);
            Assert.IsTrue(b.TryCarry(false));
        }

        [Test] public void NotCapturable_NeverCarried()
        {
            var bees = Revealed(new CritterBrain(100f, 6f, 5f, true, capturable: false));
            bees.AddWetness(100f);
            Assert.AreEqual(CritterState.Stunned, bees.State);
            Assert.IsFalse(bees.CanCapture); Assert.IsFalse(bees.TryCarry(true));
        }

        [Test] public void Net_IsSecure_HandEscapesWhenStunWouldEnd()
        {
            var net = Revealed(Raccoon()); net.AddWetness(100f); net.Tick(2f); net.TryCarry(true);
            net.Tick(60f); Assert.AreEqual(CritterState.Carried, net.State);

            var hand = Revealed(Raccoon()); hand.AddWetness(100f); hand.Tick(2f); hand.TryCarry(false);
            hand.Tick(3.9f); Assert.AreEqual(CritterState.Carried, hand.State);
            hand.Tick(0.2f); Assert.AreEqual(CritterState.Flee, hand.State); // 6 s stun − 2 s already used
        }

        [Test] public void Carried_IgnoresWater_ReleaseFlees()
        {
            var b = Revealed(Raccoon()); b.AddWetness(100f); b.TryCarry(true);
            Assert.IsFalse(b.AddWetness(50f));
            b.Release(); Assert.AreEqual(CritterState.Flee, b.State); Assert.AreEqual(0f, b.Wetness);
        }

        [Test] public void Capture_FromCarried_IsTerminal()
        {
            var b = Revealed(Raccoon()); b.AddWetness(100f); b.TryCarry(true);
            Assert.IsTrue(b.Capture()); Assert.AreEqual(CritterState.Captured, b.State);
            Assert.IsFalse(b.TrySet(CritterState.Wander)); Assert.IsFalse(b.AddWetness(10f));
            b.Escape(); Assert.AreEqual(CritterState.Captured, b.State);
        }

        [Test] public void Escape_AtDayEnd()
        {
            var b = Revealed(Raccoon()); b.TrySet(CritterState.Steal);
            b.Escape(); Assert.AreEqual(CritterState.Escaped, b.State); Assert.IsTrue(b.IsGone);
        }

        [Test] public void TrySet_OnlyFreeStates()
        {
            var b = Revealed(Raccoon());
            Assert.IsTrue(b.TrySet(CritterState.Chase));
            Assert.IsFalse(b.TrySet(CritterState.Stunned));
            Assert.IsFalse(b.TrySet(CritterState.Captured));
            Assert.AreEqual(CritterState.Chase, b.State);
        }

        [Test] public void HpScale_PerPlayer()
        {
            Assert.AreEqual(1f, CritterBrain.HpScale(1, 0.25f), 1e-4);
            Assert.AreEqual(1.75f, CritterBrain.HpScale(4, 0.25f), 1e-4);
            Assert.AreEqual(1.75f, CritterBrain.HpScale(8, 0.25f), 1e-4);
        }

        [Test] public void ChangedEvent_Fires()
        {
            var b = Raccoon(); int n = 0;
            b.Changed += (_, __) => n++;
            b.Reveal(); b.Tick(1.1f); b.AddWetness(100f);
            Assert.AreEqual(3, n);
        }
    }
}
