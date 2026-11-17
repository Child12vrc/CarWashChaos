using System.Linq;
using CarWash.Core;
using CarWash.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CarWash.Tests
{
    /// <summary>P3-P5: pose codec accuracy/size, the player prefab's ragdoll, collision rules, camera rig present.</summary>
    public class RagdollTests
    {
        [Test] public void PoseCodec_RoundTrip_Under1Degree()
        {
            var rng = new System.Random(7);
            float worst = 0f;
            for (int i = 0; i < 2000; i++)
            {
                var q = Quaternion.Euler((float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f);
                worst = Mathf.Max(worst, Quaternion.Angle(q, RagdollPose.Unpack(RagdollPose.Pack(q))));
            }
            Assert.Less(worst, 1f, $"worst {worst:0.000}°");
        }

        [Test] public void PoseCodec_IdentityAndSize()
        {
            Assert.Less(Quaternion.Angle(Quaternion.identity, RagdollPose.Unpack(RagdollPose.Pack(Quaternion.identity))), 0.2f);
            Assert.AreEqual(56, RagdollPose.BytesPerPose);   // 1.1 KB/s per downed player at 20 Hz
        }

        static GameObject Player() => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab")
            ?? AssetDatabase.FindAssets("t:Prefab Player").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<GameObject>).FirstOrDefault(g => g != null && g.GetComponent<PlayerNet>() != null);

        [Test] public void PlayerPrefab_HasElevenBoneRagdoll()
        {
            var p = Player();
            Assert.NotNull(p, "player prefab");
            var rag = p.GetComponent<PlayerRagdoll>();
            Assert.NotNull(rag);
            var bodies = p.GetComponentsInChildren<Rigidbody>(true);
            Assert.AreEqual(RagdollPose.BoneCount, bodies.Length);
            Assert.IsTrue(bodies.All(b => b.isKinematic), "kinematic until knocked down");
            Assert.IsTrue(bodies.All(b => b.gameObject.layer == Layers.Ragdoll));
            Assert.IsTrue(bodies.All(b => b.GetComponent<Collider>() != null));
            Assert.AreEqual(RagdollPose.BoneCount - 1, p.GetComponentsInChildren<CharacterJoint>(true).Length);
            Assert.AreEqual(68f, bodies.Sum(b => b.mass), 1f);   // 15+15+5 + 2×(2.5+2) + 2×(7+5)
            Assert.NotNull(p.GetComponent<PlayerCameraRig>());
        }

        [Test] public void Ragdoll_IgnoresPlayersToolsTriggers_HitsWorld()
        {
            CarWash.Editor.ProjectConfigurator.Apply();
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(Layers.Ragdoll, Layers.Player));
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(Layers.Ragdoll, Layers.Tool));
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(Layers.Ragdoll, Layers.Trigger));
            Assert.IsFalse(Physics.GetIgnoreLayerCollision(Layers.Ragdoll, Layers.Default));
            Assert.IsFalse(Physics.GetIgnoreLayerCollision(Layers.Ragdoll, Layers.Vehicle));
            Assert.IsFalse(Physics.GetIgnoreLayerCollision(Layers.Ragdoll, Layers.Ragdoll));
        }

        [Test] public void Timings_AndAbuseLimits()
        {
            Assert.Greater(PlayerRagdoll.RegrabCooldown, 2f);
            Assert.GreaterOrEqual(PlayerRagdoll.EscapePresses, 4);
            Assert.Less(PlayerRagdoll.GetUpTime, 1.5f);
            Assert.Less(PlayerCameraRig.BlendTime, 0.6f);
        }
    }
}
