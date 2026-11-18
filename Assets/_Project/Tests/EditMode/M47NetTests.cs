using CarWash.Dirt;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Tests
{
    /// <summary>M4.7 N2: compact network encodings.</summary>
    public class M47NetTests
    {
        [Test]
        public void DirtStroke_IsEighteenBytes_AndDecodesToItsQuantizedSelf()
        {
            var s = new DirtStroke(new Vector3(1.234f, 0.876f, -2.345f), 0.41f, new Vector4(1.5f, 0.75f, 0f, 1.5f), StrokeType.Clean) { source = 3 };
            using var w = new FastBufferWriter(64, Allocator.Temp);
            w.WriteNetworkSerializable(s);
            Assert.AreEqual(18, w.Length);
            using var r = new FastBufferReader(w, Allocator.Temp);
            r.ReadNetworkSerializable(out DirtStroke d);
            var q = s.Quantized();
            Assert.AreEqual(q.pos, d.pos);
            Assert.AreEqual(q.radius, d.radius);
            Assert.AreEqual(q.strength, d.strength);
            Assert.AreEqual(StrokeType.Clean, d.type);
            Assert.AreEqual(3, d.source);
            Assert.Less(Vector3.Distance(s.pos, d.pos), 0.005f, "~mm precision on a car");
            Assert.Less(Mathf.Abs(s.radius - d.radius), 0.001f);
        }

        [Test]
        public void SprayBurst_NewerWrapsAround()
        {
            Assert.IsTrue(Player.PlayerTools.NewerBurst(5, 4));
            Assert.IsFalse(Player.PlayerTools.NewerBurst(4, 4));
            Assert.IsFalse(Player.PlayerTools.NewerBurst(3, 4));
            Assert.IsTrue(Player.PlayerTools.NewerBurst(1, 65535), "wraps");
        }

        [Test]
        public void RelayRetry_OnlyTransientErrors()
        {
            Assert.IsTrue(Net.RelayConnector.IsTransient(new System.Exception("HTTP/1.1 504 Gateway Timeout (504)")));
            Assert.IsTrue(Net.RelayConnector.IsTransient(new System.Exception("Request timed out")));
            Assert.IsFalse(Net.RelayConnector.IsTransient(new System.Exception("HTTP/1.1 404 Not Found (404)")));
        }
    }
}

namespace CarWash.Tests
{
    /// <summary>M4.7 A: user art wired (PolyHaven PBR sets, Hunyuan slime) without breaking prefab content.</summary>
    public class M47ArtTests
    {
        [Test]
        public void VehiclePrefabs_EveryRendererHasAMesh()
        {
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/Vehicles" }))
            {
                var p = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                foreach (var mf in p.GetComponentsInChildren<MeshFilter>(true))
                    Assert.IsNotNull(mf.sharedMesh, $"{p.name}/{mf.name}: mesh lost (per-box UV meshes must not be used in prefabs)");
            }
        }

        [TestCase("Asphalt", "asphalt_02")]
        [TestCase("Brick", "red_brick_03")]
        [TestCase("BayWall", "box_profile_metal_sheet")]
        [TestCase("Shutter", "painted_metal_shutter")]
        [TestCase("Interior_Carpet", "dirty_carpet")]
        public void PolyHavenSet_IsWired(string material, string set)
        {
            if (!CarWash.Editor.GrayboxMaterials.HasSet(set)) Assert.Ignore(set + " not delivered");
            var m = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{CarWash.Editor.GrayboxMaterials.Dir}/{material}.mat");
            Assert.IsNotNull(m, material);
            StringAssert.Contains(set, UnityEditor.AssetDatabase.GetAssetPath(m.GetTexture("_BumpMap")), "PolyHaven normal map");
            if (m.shader.name == "CarWash/GroundStochastic") // M4.7 E1: ground sets on the stochastic shader
            {
                StringAssert.Contains(set + "_mask", UnityEditor.AssetDatabase.GetAssetPath(m.GetTexture("_MaskMap")), "ARM → mask");
                return;
            }
            StringAssert.Contains(set + "_mask", UnityEditor.AssetDatabase.GetAssetPath(m.GetTexture("_MetallicGlossMap")), "ARM → metallic/smoothness mask");
            Assert.IsTrue(m.IsKeywordEnabled("_METALLICSPECGLOSSMAP") && m.IsKeywordEnabled("_OCCLUSIONMAP") && m.IsKeywordEnabled("_NORMALMAP"));
        }

        [Test]
        public void PbrMask_PacksArmChannels()
        {
            if (!CarWash.Editor.GrayboxMaterials.HasSet("asphalt_02")) Assert.Ignore("not delivered");
            var mask = CarWash.Editor.GrayboxMaterials.PbrMask("asphalt_02");
            var arm = new Texture2D(2, 2); arm.LoadImage(System.IO.File.ReadAllBytes(CarWash.Editor.GrayboxMaterials.PolyHavenDir + "/asphalt_02/asphalt_02_arm_1k.jpg"));
            var raw = new Texture2D(2, 2); raw.LoadImage(System.IO.File.ReadAllBytes(UnityEditor.AssetDatabase.GetAssetPath(mask)));
            var a = arm.GetPixel(100, 200); var r = raw.GetPixel(100, 200);
            Assert.AreEqual(a.b, r.r, 0.01f, "R = metallic");
            Assert.AreEqual(a.r, r.g, 0.01f, "G = AO");
            Assert.AreEqual(1f - a.g, r.a, 0.01f, "A = 1 - roughness");
        }
    }
}

namespace CarWash.Tests
{
    /// <summary>M4.7 E: the environment pass keeps every route open and stays cheap.</summary>
    public class M47EnvTests
    {
        [Test]
        public void Dressing_OffRoutes_TwoDrawCalls_ManyDecals()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/SudsyLot.unity", UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                GameObject dressing = null;
                foreach (var go in scene.GetRootGameObjects()) { var t = go.transform.Find("Dressing"); if (t != null) dressing = t.gameObject; }
                Assert.IsNotNull(dressing, "Environment/Dressing built");
                var cols = dressing.transform.Find("DressingColliders");
                Assert.Greater(cols.childCount, 5);
                foreach (Transform c in cols)
                {
                    Assert.IsFalse(CarWash.Editor.EnvDressingBuilder.InCarPath(c.position, 0.3f), "collider on a car route at " + c.position);
                    Assert.IsTrue(CarWash.Editor.EnvDressingBuilder.Clear(c.position, 0.3f), "collider near an interaction point / spawn at " + c.position);
                    Assert.IsTrue(CarWash.Core.PlayArea.Contains(c.position), "inside the play area");
                }
                var renderers = dressing.GetComponentsInChildren<MeshRenderer>();
                Assert.AreEqual(2, renderers.Length, "props + grass = one combined mesh each");
                foreach (var r in renderers) Assert.IsNull(r.GetComponent<Collider>(), "decoration meshes have no colliders");
                int decals = 0; foreach (var c in dressing.GetComponentsInChildren<Component>()) if (c != null && c.GetType().Name == "DecalProjector") decals++;
                Assert.GreaterOrEqual(decals, 60);
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void GroundMaterials_UseStochasticShader()
        {
            foreach (var n in new[] { "Asphalt", "Concrete", "Road", "Grass" })
            {
                var m = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{CarWash.Editor.GrayboxMaterials.Dir}/{n}.mat");
                Assert.IsNotNull(m, n);
                Assert.AreEqual("CarWash/GroundStochastic", m.shader.name, n);
                Assert.IsNotNull(m.GetTexture("_BaseMap"), n);
            }
        }
    }
}
