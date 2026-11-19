using System.Linq;
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
        public void EveryCar_HasADriver_BlenderInteriorsUseTheirSocketAndHandTargets()
        {
            int glb = 0;
            foreach (var id in new[] { "V01_Hatchback", "V02_Sedan", "V03_Pickup", "V04_Minivan", "B01_MonsterTruck" })
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/Vehicles/{id}.prefab");
                var drv = p.GetComponent<Vehicles.CustomerDriver>();
                Assert.IsNotNull(drv, id + " has a driver (v0.8 lost it when the Blender interiors arrived)");
                var so = new SerializedObject(drv);
                Assert.IsNotNull(so.FindProperty("_seat").objectReferenceValue, id);
                Assert.IsNotNull(((Transform)so.FindProperty("_seat").objectReferenceValue).Find("Roof"), id + " roof anchor");
                string short_ = id.Substring(0, 3);
                if (System.IO.File.Exists($"Assets/_Project/Art/Models/Interiors/Interior_{short_}.glb"))
                {
                    glb++;
                    Assert.AreEqual("Hand_L", ((Transform)so.FindProperty("_handL").objectReferenceValue).name, id);
                    Assert.AreEqual("Hand_R", ((Transform)so.FindProperty("_handR").objectReferenceValue).name, id);
                    Assert.AreEqual(0.09f, so.FindProperty("_hipHeight").floatValue, 1e-4f, id);
                    // the cushion anchor sits 0.09 m under the model's hip socket
                    var hips = p.GetComponentsInChildren<Transform>(true).First(t => t.name == "Socket_DriverHips");
                    var seat = (Transform)so.FindProperty("_seat").objectReferenceValue;
                    Assert.AreEqual(hips.position.y - 0.09f * hips.lossyScale.y, seat.position.y, 0.02f, id);
                }
            }
            Assert.Greater(glb, 0, "Blender interiors in use");
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
