using System.Collections.Generic;
using System.Linq;
using CarWash.Data;
using CarWash.Editor;
using CarWash.Vehicles;
using NUnit.Framework;

namespace CarWash.Tests
{
    /// <summary>T3.6 surprise pick + trigger rules against the real vehicle/critter data.</summary>
    public class SurpriseTriggerTests
    {
        static readonly HashSet<string> WithPrefab = new HashSet<string> { "C01", "C02", "C03", "C05" };
        DataBuilder.Loaded D;
        [OneTimeSetUp] public void Load() => D = DataBuilder.LoadSources();

        static List<SurpriseEntry> Pool(params (string id, float w)[] e) => e.Select(x => new SurpriseEntry { critterId = x.id, weight = x.w, count = 1 }).ToList();

        [Test] public void Pick_IsWeighted()
        {
            var pool = Pool(("C01", 1f), ("C05", 3f));
            Assert.AreEqual("C01", SurpriseTrigger.Pick(pool, 0.0, null).critterId);
            Assert.AreEqual("C01", SurpriseTrigger.Pick(pool, 0.24, null).critterId);
            Assert.AreEqual("C05", SurpriseTrigger.Pick(pool, 0.26, null).critterId);
            Assert.AreEqual("C05", SurpriseTrigger.Pick(pool, 0.999, null).critterId);
        }

        [Test] public void Pick_SkipsUnspawnable_AndEmptyIsNull()
        {
            var pool = Pool(("C04", 5f), ("C01", 1f));
            Assert.AreEqual("C01", SurpriseTrigger.Pick(pool, 0.0, WithPrefab.Contains).critterId);
            Assert.IsNull(SurpriseTrigger.Pick(Pool(("C04", 1f)), 0.5, WithPrefab.Contains));
            Assert.IsNull(SurpriseTrigger.Pick(null, 0.5, null));
        }

        [Test] public void WashedFraction_FromArrivalDirt()
        {
            Assert.AreEqual(0f, SurpriseTrigger.WashedFraction(0.6f, 0.6f), 1e-4);
            Assert.AreEqual(0.5f, SurpriseTrigger.WashedFraction(0.8f, 0.6f), 1e-4);
            Assert.AreEqual(1f, SurpriseTrigger.WashedFraction(1f, 0.6f), 1e-4);
            Assert.AreEqual(0f, SurpriseTrigger.WashedFraction(0.5f, 0.6f), 1e-4); // re-dirtied below the start
            Assert.AreEqual(1f, SurpriseTrigger.WashedFraction(1f, 1f), 1e-4);
        }

        [Test] public void EveryMvpVehicle_CanProduceASurprise()
        {
            foreach (var v in D.vehicles.Where(v => v.mvp))
                Assert.IsNotNull(SurpriseTrigger.Pick(v.surprisePool, 0.5, WithPrefab.Contains), v.id);
        }

        [Test] public void PoolCritters_AllExistInData()
        {
            var ids = new HashSet<string>(D.critters.Select(c => c.id));
            foreach (var v in D.vehicles) foreach (var e in v.surprisePool) Assert.IsTrue(ids.Contains(e.critterId), $"{v.id} → {e.critterId}");
        }

        [Test] public void MvpCritterTriggers_MatchGdd()
        {
            TriggerKind K(string id) => D.critters.First(c => c.id == id).Trigger;
            Assert.AreEqual(TriggerKind.OnOpen, K("C01"));
            Assert.AreEqual(TriggerKind.OnCleanPct, K("C02"));
            Assert.AreEqual(TriggerKind.OnCleanPct, K("C03"));
            Assert.AreEqual(TriggerKind.OnOpen, K("C05"));
        }
    }
}
