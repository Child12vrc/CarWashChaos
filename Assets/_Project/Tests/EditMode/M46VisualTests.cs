using System.Linq;
using CarWash.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CarWash.Tests
{
    /// <summary>M4.6 B4 (sign text depth) and B5 (crate z-fighting / stacking).</summary>
    public class M46VisualTests
    {
        [Test]
        public void WorldTextShader_TestsDepth()
        {
            var s = Shader.Find("CarWash/WorldText");
            Assert.IsNotNull(s);
            Assert.IsTrue(s.isSupported);
            var src = System.IO.File.ReadAllText("Assets/_Project/Shaders/WorldText.shader");
            StringAssert.Contains("ZTest LEqual", src);
        }

        [Test]
        public void BillboardTexts_UseDepthTestedText()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/SudsyLot.unity", OpenSceneMode.Additive);
            try
            {
                var texts = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TextMesh>(true)).ToArray();
                Assert.Greater(texts.Length, 0);
                foreach (var t in texts)
                {
                    var d = t.GetComponent<Visual.WorldTextDepth>();
                    Assert.IsNotNull(d, t.name);
                    Assert.AreEqual("CarWash/WorldText", ((Shader)new SerializedObject(d).FindProperty("_shader").objectReferenceValue).name);
                }
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void Crate_FacesNoLongerCoplanar_ColliderWrapsTheMesh()
        {
            foreach (var id in new[] { "Crate", "HeavyCrate" })
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(PropBuilder.PathOf(id));
                var model = p.transform.Find("Model");
                Assert.Less(model.Find("Slats").localScale.x, 1f, id + " slats recessed");
                Assert.Greater(model.Find("DiagonalBraces").localScale.x, 1f, id + " braces proud");
                var box = p.GetComponent<BoxCollider>();
                var b = GlbKit.LocalBounds(model.gameObject, p.transform);
                Assert.LessOrEqual(b.size.x, box.size.x + 0.002f, id);
                Assert.LessOrEqual(b.size.y, box.size.y + 0.002f, id);
                Assert.LessOrEqual(b.size.z, box.size.z + 0.002f, id);
            }
        }
    }
}
