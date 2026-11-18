using System.IO;
using System.Linq;
using CarWash.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CarWash.Tests
{
    /// <summary>M4.10: world text is depth-tested and sharp, no coplanar flicker in the lot, nothing buried, sane camera planes.</summary>
    public class M410Tests
    {
        static void WithLot(System.Action<GameObject[]> body)
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/SudsyLot.unity", OpenSceneMode.Additive);
            try { body(scene.GetRootGameObjects()); }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void WorldTextShader_DepthTested_NoSlopeOffset()
        {
            var src = File.ReadAllText("Assets/_Project/Shaders/WorldText.shader");
            StringAssert.Contains("ZTest LEqual", src);
            StringAssert.DoesNotContain("Offset -1, -1", src, "a slope offset pushed text through meshes at grazing angles");
        }

        [Test]
        public void EveryWorldTextMesh_IsDepthTested_AndCodeAddsItOnlyWithWorldTextDepth()
        {
            WithLot(roots => Assert.IsEmpty(ClipAudit.UnsafeText(roots), "TextMesh without CarWash/WorldText"));
            // guard for new code: whoever adds a TextMesh must put WorldTextDepth on it too
            foreach (var f in Directory.GetFiles("Assets/_Project/Scripts", "*.cs", SearchOption.AllDirectories))
            {
                var s = File.ReadAllText(f);
                if (s.Contains("AddComponent<TextMesh>")) StringAssert.Contains("WorldTextDepth", s, f);
            }
            // world-space UI text (name tags) uses the depth-tested shader as well
            StringAssert.Contains("CarWash/WorldText", File.ReadAllText("Assets/_Project/Scripts/Runtime/Player/PlayerAppearance.cs"));
        }

        [Test]
        public void BakedTextTextures_AreMipmapped_Anisotropic_Trilinear()
        {
            var files = Directory.Exists("Assets/_Project/Art/Generated/Text") ? Directory.GetFiles("Assets/_Project/Art/Generated/Text", "*.png") : new string[0];
            Assert.Greater(files.Length, 8);
            foreach (var f in files)
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(f.Replace('\\', '/'));
                Assert.IsTrue(ti.mipmapEnabled, f);
                Assert.AreEqual(16, ti.anisoLevel, f);
                Assert.AreEqual(FilterMode.Trilinear, ti.filterMode, f);
            }
            WithLot(roots =>
            {
                Assert.AreEqual(0, roots.SelectMany(r => r.GetComponentsInChildren<TextMesh>(true)).Count(), "static text is baked into decals");
                var floor = roots.SelectMany(r => r.GetComponentsInChildren<UnityEngine.Rendering.Universal.DecalProjector>(true)).Where(d => d.name.StartsWith("FloorText_")).ToArray();
                Assert.GreaterOrEqual(floor.Length, 6, "서행 / 입구 / 출구 + bay numbers");
                foreach (var d in floor) Assert.Greater(d.size.y, d.size.x * 0.9f, d.name + " stretched along the driving direction");
            });
        }

        [Test]
        public void Lot_HasNoCoplanarFlicker_AndNothingIsBuried()
        {
            WithLot(roots =>
            {
                var cop = ClipAudit.CoplanarFaces(roots);
                Assert.IsEmpty(cop, string.Join("\n", cop.Take(30)));
                var props = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Where(t => t.name.StartsWith("SW_")).ToArray();
                var bur = ClipAudit.Buried(roots, props);
                Assert.IsEmpty(bur, string.Join("\n", bur.Take(30)));
            });
        }

        [Test]
        public void FpCamera_NearAndFarPlanes_HandsOnTheirOwnLayer_RagdollHitsWalls()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab");
            var cam = player.GetComponentsInChildren<Camera>(true).First(c => c.name == "FPCamera");
            Assert.That(cam.nearClipPlane, Is.InRange(0.05f, 0.1f));
            Assert.LessOrEqual(cam.farClipPlane, 400f);
            Assert.GreaterOrEqual(cam.farClipPlane, 200f, "fog ends at 190 m");
            StringAssert.Contains("CameraRenderType.Overlay", File.ReadAllText("Assets/_Project/Scripts/Runtime/Player/FPHands.cs"));
            Assert.IsFalse(Physics.GetIgnoreLayerCollision(Core.Layers.Ragdoll, Core.Layers.Default), "ragdolls collide with walls");
            Assert.IsFalse(Physics.GetIgnoreLayerCollision(Core.Layers.Player, Core.Layers.Default));
        }
    }
}
