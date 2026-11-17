using System.Linq;
using CarWash.Data;
using CarWash.Editor;
using NUnit.Framework;
using UnityEngine;

namespace CarWash.Tests
{
    public class DataValidationTests
    {
        [Test]
        public void SourceJson_IsValid()
        {
            var d = DataBuilder.LoadSources();
            var errors = d.Validate();
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void SourceJson_ContainsAllMvpRows()
        {
            var d = DataBuilder.LoadSources();
            CollectionAssert.IsSubsetOf(new[] { "T01", "T02", "T03", "T04", "T06" }, d.tools.Where(t => t.mvp).Select(t => t.id).ToArray());
            CollectionAssert.IsSubsetOf(new[] { "V01", "V02", "V03", "V04", "B01" }, d.vehicles.Where(v => v.mvp).Select(v => v.id).ToArray());
            CollectionAssert.IsSubsetOf(new[] { "C01", "C02", "C03", "C05" }, d.critters.Where(c => c.mvp).Select(c => c.id).ToArray());
            Assert.AreEqual(1, d.weeks.Count(w => w.week == 1));
        }

        [Test]
        public void SourceJson_ValuesMatchGdd()
        {
            var d = DataBuilder.LoadSources();
            var v01 = d.vehicles.First(v => v.id == "V01");
            Assert.AreEqual(40, v01.basePay);
            Assert.AreEqual(90f, v01.timeLimit);
            Assert.AreEqual(VehicleSize.S, v01.Size);
            Assert.AreEqual(2, d.vehicles.First(v => v.id == "B01").surprisePool.First(s => s.critterId == "C03").count);
            Assert.AreEqual(new[] { 80, 160, 320 }, d.upgrades.First(u => u.id == "U01").costs);
            Assert.AreEqual(50, d.balance.economy.startMoney);
            Assert.AreEqual(TriggerKind.OnOpen, d.critters.First(c => c.id == "C01").Trigger);
            Assert.IsTrue(d.tools.First(t => t.id == "T02").Flags.HasFlag(ToolFlags.Foam));
        }

        [Test]
        public void Validator_DetectsDuplicateAndBrokenReference()
        {
            var d = DataBuilder.LoadSources();
            var dup = ScriptableObject.CreateInstance<ToolData>();
            dup.id = "T01";
            d.tools.Add(dup);
            d.vehicles[0].surprisePool = new[] { new SurpriseEntry { critterId = "C99" } };
            var errors = d.Validate();
            Assert.IsTrue(errors.Any(e => e.Contains("duplicate id 'T01'")));
            Assert.IsTrue(errors.Any(e => e.Contains("unknown critter 'C99'")));
        }

        [Test]
        public void JsonSplitter_HandlesBracesInsideStrings()
        {
            var items = DataJson.SplitItems("{ \"items\": [ {\"a\":\"x{y}\"}, {\"b\":[1,{\"c\":2}]} ] }");
            Assert.AreEqual(2, items.Count);
            Assert.AreEqual("{\"a\":\"x{y}\"}", items[0]);
        }

        [Test]
        public void GeneratedRegistry_MatchesSource()
        {
            DataBuilder.BuildAll();
            var reg = DataBuilder.LoadRegistry();
            Assert.NotNull(reg);
            Assert.NotNull(reg.balance);
            Assert.AreEqual(DataBuilder.LoadSources().vehicles.Count, reg.vehicles.Length);
            Assert.NotNull(reg.Get<VehicleData>("B01"));
            Assert.IsNull(reg.Get<VehicleData>("nope"));
        }
    }
}
