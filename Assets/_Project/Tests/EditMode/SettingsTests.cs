using CarWash.Core;
using CarWash.Player;
using NUnit.Framework;
using UnityEngine;

namespace CarWash.Tests
{
    /// <summary>P2: settings clamp to their ranges, persist in PlayerPrefs, reset; unstuck cooldown rule.</summary>
    public class SettingsTests
    {
        Settings S;
        [SetUp] public void Make() { S = new Settings(); S.ResetAll(); }
        [TearDown] public void Clean() => S.ResetAll();

        [Test] public void Fov_ClampedTo60_110()
        {
            S.Fov = 30f; Assert.AreEqual(60f, S.Fov);
            S.Fov = 200f; Assert.AreEqual(110f, S.Fov);
            S.Fov = 95f; Assert.AreEqual(95f, S.Fov);
        }

        [Test] public void Brightness_Volumes_Shake_Clamped()
        {
            S.Brightness = 5f; Assert.AreEqual(2f, S.Brightness);
            S.Brightness = -5f; Assert.AreEqual(-2f, S.Brightness);
            S.MasterVolume = 2f; Assert.AreEqual(1f, S.MasterVolume);
            S.SfxVolume = -1f; Assert.AreEqual(0f, S.SfxVolume);
            S.ShakeStrength = 3f; Assert.AreEqual(1f, S.ShakeStrength);
            S.Sensitivity = 0f; Assert.AreEqual(0.1f, S.Sensitivity, 1e-4);
        }

        [Test] public void Values_PersistAcrossInstances()
        {
            S.Fov = 72f; S.InvertY = true; S.HeadBob = false; S.Bindings = "{\"bindings\":[]}";
            var again = new Settings();
            Assert.AreEqual(72f, again.Fov); Assert.IsTrue(again.InvertY); Assert.IsFalse(again.HeadBob);
            StringAssert.Contains("bindings", again.Bindings);
        }

        [Test] public void ResetAll_RestoresDefaults()
        {
            S.Fov = 70f; S.ShakeStrength = 0f; S.HeadBob = false;
            S.ResetAll();
            Assert.AreEqual(90f, S.Fov); Assert.AreEqual(0.6f, S.ShakeStrength, 1e-4); Assert.IsTrue(S.HeadBob);
            Assert.AreEqual("", S.Bindings);
        }

        [Test] public void ChangedEvent_FiresOnSet()
        {
            int n = 0; S.Changed += () => n++;
            S.Fov = 80f; S.InvertY = true;
            Assert.AreEqual(2, n);
        }

        [Test] public void Unstuck_Cooldown()
        {
            Assert.IsTrue(Unstuck.CanUse(10f, 0f));
            Assert.IsFalse(Unstuck.CanUse(10f, 10f + Unstuck.Cooldown - 1f));
            Assert.AreEqual(30f, Unstuck.Cooldown);
        }
    }
}
