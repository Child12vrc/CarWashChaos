using System.Linq;
using CarWash.Critters;
using CarWash.Editor;
using CarWash.Player;
using CarWash.Tools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CarWash.Tests
{
    /// <summary>GLB props / tools / critters (docs/ART_IMPORT.md 3D Models) are built from the delivered models with the agreed
    /// colliders, masses, sockets and code-animation wiring.</summary>
    public class GlbModelTests
    {
        static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/" + path + ".prefab");

        [TestCase("Crate", 20f, true)]
        [TestCase("HeavyCrate", 60f, true)]
        [TestCase("Cone", 2f, false)]
        [TestCase("Bucket", 1.5f, false)]
        [TestCase("Tire", 10f, false)]
        [TestCase("HayBale", 25f, true)]
        [TestCase("TrashBag", 4f, false)]
        [TestCase("CautionSign", 3f, false)]
        public void PropUsesItsModelWithSimpleCollider(string id, float mass, bool box)
        {
            var p = Load("Props/" + id);
            Assert.IsNotNull(p, id);
            Assert.IsNotNull(p.transform.Find("Model"), "built from the glb");
            Assert.AreEqual(mass, p.GetComponent<Rigidbody>().mass, 0.01f, "mass kept");
            var cols = p.GetComponents<Collider>();
            Assert.AreEqual(1, cols.Length, "one simple collider on the root");
            if (box) Assert.IsInstanceOf<BoxCollider>(cols[0]);
            else Assert.IsTrue(cols[0] is MeshCollider mc && mc.convex, "convex hull");
            Assert.IsNotNull(p.GetComponent<Grabbable>());
            // root = bounds centre (hold point / spin), so the collider is centred on it
            var centre = cols[0] is BoxCollider bc ? bc.center : ((MeshCollider)cols[0]).sharedMesh.bounds.center;
            Assert.Less(centre.magnitude, 0.08f);
        }

        [Test]
        public void CautionSignHasTheSlipperyImageOnBothLeaves()
        {
            var p = Load("Props/CautionSign");
            var faces = p.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.StartsWith("SignFace")).ToArray();
            Assert.AreEqual(2, faces.Length);
            foreach (var f in faces) StringAssert.Contains("sign_caution_slippery", f.sharedMaterial.mainTexture.name);
        }

        [TestCase("T01_PressureWasher", "Socket_Nozzle")]
        [TestCase("T02_FoamCannon", "Socket_Nozzle")]
        [TestCase("T03_Towel", "Socket_Nozzle")]
        [TestCase("T06_NetPole", "Socket_Capture")]
        public void ToolAimsFromItsSocket(string file, string socket)
        {
            var p = Load("Tools/" + file);
            Assert.IsNotNull(p.transform.Find("Model"), "built from the glb (origin = grip)");
            var tool = p.GetComponent<ToolBase>();
            Assert.AreEqual(socket, tool.Nozzle.name);
            Assert.Greater(p.transform.InverseTransformPoint(tool.Nozzle.position).z, 0.2f, "socket ahead of the grip (+Z)");
            Assert.IsTrue(p.GetComponents<Collider>().Length >= 1);
        }

        [Test]
        public void WasherHoseStartsAtHoseIn_NetHangsAtCapture()
        {
            var hose = Load("Tools/T01_PressureWasher").GetComponent<HoseTether>();
            Assert.AreEqual("Socket_HoseIn", ((Transform)new SerializedObject(hose).FindProperty("_hoseIn").objectReferenceValue).name);
            Assert.AreEqual("Socket_Capture", Load("Tools/T06_NetPole").GetComponent<NetPole>().Capture.name);
        }

        [TestCase("C01_Raccoon", CritterAnimator.Kind.Rigid)]
        [TestCase("C05_Possum", CritterAnimator.Kind.Rigid)]
        [TestCase("C03_MudSlime", CritterAnimator.Kind.Slime)]
        public void CritterIsAnimatedFromItsNodes(string file, CritterAnimator.Kind kind)
        {
            var p = Load("Critters/" + file);
            var a = p.GetComponent<CritterAnimator>();
            Assert.IsNotNull(a);
            var so = new SerializedObject(a);
            Assert.AreEqual((int)kind, so.FindProperty("_kind").enumValueIndex);
            Assert.AreEqual("Socket_Carry", a.CarrySocket.name);
            Assert.IsNotNull(so.FindProperty("_body").objectReferenceValue);
            if (kind == CritterAnimator.Kind.Quadruped)
                foreach (var leg in new[] { "_legFL", "_legFR", "_legBL", "_legBR", "_tail", "_head" })
                    Assert.IsNotNull(so.FindProperty(leg).objectReferenceValue, leg);
            else if (kind == CritterAnimator.Kind.Slime) Assert.AreEqual(3, so.FindProperty("_drips").arraySize);
            // nothing animation-related is networked: only CritterBase's NetworkVariables + the root NetworkTransform
            Assert.AreEqual(1, p.GetComponentsInChildren<Unity.Netcode.Components.NetworkTransform>(true).Length);
        }

        [TestCase("C01_Raccoon", 0.66f)]
        [TestCase("C05_Possum", 0.52f)]
        public void HunyuanCritterIsUsed_ScaledFeetDown_PbrKept(string file, float height)
        {
            var p = Load("Critters/" + file);
            var pivot = p.transform.Find("Visual/HyPivot");
            Assert.IsNotNull(pivot, "solid _hy model used");
            var r = pivot.GetComponentInChildren<MeshRenderer>();
            var m = r.sharedMaterial;
            Assert.IsTrue(m.HasProperty("normalTexture") && m.GetTexture("normalTexture") != null, "PBR material kept (normal map)");
            var b = GlbKit.LocalBounds(pivot.GetChild(0).gameObject, p.transform);
            Assert.AreEqual(height, b.size.y, 0.02f, "scaled to the critter size");
            Assert.AreEqual(0f, b.min.y, 0.02f, "feet on the ground");
            Assert.IsNotNull(p.GetComponent<CapsuleCollider>(), "gameplay capsule kept");
        }

        [Test]
        public void BeeSwarmIsSeveralBeeModels()
        {
            var p = Load("Critters/C02_BeeSwarm");
            var flock = p.GetComponentInChildren<BeeFlock>(true);
            Assert.IsNotNull(flock);
            Assert.AreEqual(CritterBuilder.SwarmSize, flock.Count);
            Assert.AreEqual(CritterBuilder.SwarmSize, p.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Wing_L"));
        }
    }
}
