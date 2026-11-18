using System.Linq;
using CarWash.Editor;
using CarWash.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CarWash.Tests
{
    /// <summary>Player looks (docs/ART_IMPORT.md "Player characters"): 12 Quaternius humanoids, defaults P1-P4 first, each with a
    /// humanoid avatar on the shared controller, its own ragdoll and a colour outline; the Y Bot is no longer in the player.</summary>
    public class PlayerLookTests
    {
        static GameObject Player() => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab");

        [Test]
        public void CharactersImportAsHumanoid()
        {
            foreach (var (id, _) in PlayerLooks.Catalog)
            {
                var mi = (ModelImporter)AssetImporter.GetAtPath(PlayerLooks.PathOf(id));
                Assert.IsNotNull(mi, id);
                Assert.AreEqual(ModelImporterAnimationType.Human, mi.animationType, id);
                var anim = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerLooks.PathOf(id)).GetComponent<Animator>();
                Assert.IsTrue(anim.avatar != null && anim.avatar.isValid && anim.avatar.isHuman, id + " avatar");
            }
        }

        [Test]
        public void PlayerHasTwelveLooks_DefaultsFirst()
        {
            var p = Player();
            var ap = p.GetComponent<PlayerAppearance>();
            Assert.IsNotNull(ap);
            Assert.AreEqual(12, ap.Count);
            for (int i = 0; i < PlayerAppearance.Defaults.Length; i++) Assert.AreEqual(i, ap.IndexOf(PlayerAppearance.Defaults[i]));
            Assert.IsNull(p.transform.Find("Body/Look_YBot"), "Y Bot no longer used");
        }

        [Test]
        public void EveryLookHasControllerRagdollAndOutline()
        {
            var p = Player();
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerLooks.ControllerPath);
            foreach (var anim in p.GetComponentsInChildren<Animator>(true))
            {
                Assert.AreEqual(ctrl, anim.runtimeAnimatorController, anim.name);
                Assert.IsTrue(anim.isHuman, anim.name);
                Assert.AreEqual(11, anim.GetComponentsInChildren<Rigidbody>(true).Length, anim.name);
                var outlines = anim.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.sharedMaterial != null && r.sharedMaterial.name.Contains("Outline")).ToArray();
                Assert.Greater(outlines.Length, 0, anim.name + " outline");
                Assert.IsTrue(outlines.All(o => o.bones.Length > 0 && o.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off));
            }
        }
    }
}
