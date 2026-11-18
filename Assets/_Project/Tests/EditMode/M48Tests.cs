using CarWash.Data;
using CarWash.Economy;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CarWash.Tests
{
    /// <summary>M4.8: foam shells, drying tip, critter throw plumbing.</summary>
    public class M48Tests
    {
        [Test]
        public void DryingTip_AddsFifteenPercentOfBasePay()
        {
            var e = new BalanceData.EconomyTuning();
            var ctx = new PayContext { basePay = 100, cleanPct = 0.95f, elapsed = 10f, timeLimit = 100f };
            var plain = EconomyCalc.CalcPay(ctx, e, 0.05f);
            ctx.dried = true;
            var dried = EconomyCalc.CalcPay(ctx, e, 0.05f);
            Assert.AreEqual(0, plain.tip);
            Assert.AreEqual(Mathf.RoundToInt(100 * e.dryTip), dried.tip);
            Assert.AreEqual(plain.pay + dried.tip, dried.pay);
        }

        [Test]
        public void EveryCarBody_HasTheInstancedFoamShell()
        {
            int bodies = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/Vehicles" }))
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (var painter in p.GetComponentsInChildren<Dirt.DirtPainter>(true))
                {
                    bodies++;
                    var m = (Material)new SerializedObject(painter).FindProperty("_foamShell").objectReferenceValue;
                    Assert.IsNotNull(m, p.name);
                    Assert.AreEqual("CarWash/FoamShell", m.shader.name);
                    Assert.IsTrue(m.enableInstancing, "drawn as instanced shells");
                    // the body renderer keeps ONE material (extra materials made the GPU Resident Drawer skip the car)
                    Assert.AreEqual(1, painter.GetComponent<MeshRenderer>().sharedMaterials.Length, p.name);
                }
            }
            Assert.Greater(bodies, 5);
        }
    }
}
