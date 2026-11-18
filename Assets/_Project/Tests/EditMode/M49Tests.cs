using System.Linq;
using CarWash.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CarWash.Tests
{
    /// <summary>M4.9: G1 day / night + G2 clouds (sky), G7 bay lights + switch.</summary>
    public class M49Tests
    {
        static void WithLot(System.Action<GameObject[]> body)
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/SudsyLot.unity", OpenSceneMode.Additive);
            try { body(scene.GetRootGameObjects()); }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void Lot_HasTheDayNightDriver_WithTheDaySkyAndGlowMaterials()
        {
            WithLot(roots =>
            {
                var dn = roots.SelectMany(r => r.GetComponentsInChildren<Visual.DayNightCycle>(true)).Single();
                var so = new SerializedObject(dn);
                var sky = (Material)so.FindProperty("_skyAsset").objectReferenceValue;
                Assert.AreEqual("CarWash/DaySky", sky.shader.name);
                Assert.IsTrue(sky.HasProperty("_CloudCover") && sky.HasProperty("_Stars"), "clouds + stars live in the sky shader");
                Assert.IsNotNull(so.FindProperty("_sun").objectReferenceValue);
                Assert.GreaterOrEqual(so.FindProperty("_glowMats").arraySize, 2, "lamp heads + signs glow");
                Assert.GreaterOrEqual(so.FindProperty("_far").arraySize, 2, "distant city darkens at night");
            });
        }

        [Test]
        public void StreetLamps_HaveShadowlessNightLights()
        {
            WithLot(roots =>
            {
                var nl = roots.SelectMany(r => r.GetComponentsInChildren<Visual.NightLight>(true)).ToArray();
                Assert.Greater(nl.Length, 10);
                foreach (var n in nl)
                    foreach (var l in n.GetComponentsInChildren<Light>(true))
                    {
                        Assert.AreEqual(LightShadows.None, l.shadows, n.name);
                        Assert.IsFalse(l.enabled, n.name + " off in daylight (scene default)");
                    }
            });
        }

        [Test]
        public void EveryBay_HasCeilingLights_AndASpawnedWallSwitch()
        {
            WithLot(roots =>
            {
                var bays = roots.SelectMany(r => r.GetComponentsInChildren<Vehicles.Bay>(true)).ToArray();
                var lights = roots.SelectMany(r => r.GetComponentsInChildren<Visual.BayLights>(true)).ToArray();
                Assert.AreEqual(bays.Length, lights.Length);
                foreach (var bl in lights)
                {
                    var so = new SerializedObject(bl);
                    int n = so.FindProperty("_lights").arraySize;
                    Assert.That(n, Is.InRange(1, 2), "1–2 lights per bay");
                    Assert.IsNotNull(so.FindProperty("_onMat").objectReferenceValue);
                }
                var spawn = roots.SelectMany(r => r.GetComponentsInChildren<Net.NetworkSpawnList>(true)).Single();
                var entries = new SerializedObject(spawn).FindProperty("_entries");
                int switches = 0;
                for (int i = 0; i < entries.arraySize; i++)
                {
                    var p = entries.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue as Unity.Netcode.NetworkObject;
                    if (p != null && p.GetComponent<Day.BayLightSwitch>() != null) switches++;
                }
                Assert.AreEqual(bays.Length, switches, "one switch per bay");
            });
        }

        [Test]
        public void VehicleShader_ReceivesForwardPlusLights_AndCarsHaveHeadlights()
        {
            StringAssert.Contains("_CLUSTER_LIGHT_LOOP", System.IO.File.ReadAllText("Assets/_Project/Shaders/VehicleDirt.shader"));
            var car = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Vehicles/V02_Sedan.prefab");
            Assert.IsNotNull(car.GetComponent<Visual.VehicleHeadlights>());
            var sw = AssetDatabase.LoadAssetAtPath<GameObject>(DayBuilder.BayLightSwitchPath);
            Assert.AreEqual(Core.Layers.Interactable, sw.layer);
        }
    }
}
