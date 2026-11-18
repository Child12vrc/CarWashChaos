using System.Linq;
using CarWash.Core;
using CarWash.Dirt;
using CarWash.Editor;
using CarWash.Vehicles;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CarWash.Tests
{
    /// <summary>GLB vehicles (docs/ART_IMPORT.md 3D Models): ids with a model (V01-V04, B01) use it, the rest stay graybox.</summary>
    public class GlbVehicleTests
    {
        static GameObject Prefab(string id)
        {
            var d = DataBuilder.LoadRegistry().vehicles.First(v => v.id == id);
            return AssetDatabase.LoadAssetAtPath<GameObject>(VehicleBuilder.PathOf(d));
        }

        [TestCase("V01")]
        [TestCase("V02")]
        [TestCase("V03")]
        [TestCase("V04")]
        [TestCase("B01")]
        public void ModelledVehicleFollowsTheNodeContract(string id)
        {
            Assert.IsNotNull(VehicleGlbBuilder.GlbFor(id), "glb present + imported (glTFast)");
            var go = Prefab(id);
            Assert.IsNotNull(go.transform.Find("Model"), "built from the glb");

            // dirt body: single submesh with UV2, exact collider on the raycast-only layer
            var painter = go.GetComponentInChildren<DirtPainter>(true);
            Assert.AreEqual("Body", painter.name);
            var mesh = painter.GetComponent<MeshFilter>().sharedMesh;
            Assert.AreEqual(1, mesh.subMeshCount);
            Assert.IsTrue(mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord1), "UV2");
            Assert.AreEqual(Layers.WashSurface, painter.gameObject.layer);
            Assert.AreEqual(mesh, painter.GetComponent<MeshCollider>().sharedMesh);
            Assert.AreEqual(go.GetComponent<DirtNetSync>(), painter.GetComponentInParent<DirtNetSync>(true));

            // doors: Door_L, Door_R, Trunk in that order, hinge = the glb node, box collider on Interactable
            var doors = go.GetComponentsInChildren<VehicleDoor>(true);
            CollectionAssert.AreEqual(new[] { "Door_L", "Door_R", "Trunk" }, doors.Select(x => x.name).ToArray());
            foreach (var door in doors)
            {
                Assert.AreEqual(Layers.Interactable, door.gameObject.layer, door.name);
                Assert.IsNotNull(door.GetComponent<BoxCollider>(), door.name);
            }
            Assert.IsTrue(doors[2].IsTrunk);

            // simple physics hull (boxes only) on Vehicle, skipped by washing raycasts
            var hull = go.GetComponentInChildren<VehicleHull>();
            Assert.IsNotNull(hull);
            var hullCols = hull.GetComponents<Collider>();
            Assert.AreEqual(2, hullCols.Length);
            Assert.IsTrue(hullCols.All(c => c is BoxCollider));
            Assert.AreEqual(Layers.Vehicle, hull.gameObject.layer);
            Assert.IsFalse(go.GetComponentsInChildren<MeshCollider>(true).Any(c => c.gameObject.layer != Layers.WashSurface), "no mesh colliders in physics");

            // sockets
            var v = go.GetComponent<Vehicle>();
            Assert.IsNotNull(v.InteriorSocket); Assert.IsNotNull(v.SurpriseSocket);
            Assert.AreEqual("Socket_Interior", v.InteriorSocket.name);
        }

        [Test]
        public void WashSurfaceIsRaycastOnly()
        {
            for (int l = 0; l < 32; l++) Assert.IsTrue(Physics.GetIgnoreLayerCollision(Layers.WashSurface, l), "layer " + l);
            Assert.AreNotEqual(0, Layers.SprayMask & (1 << Layers.WashSurface));
            Assert.AreEqual(0, Layers.SprayMask & (1 << Layers.Interactable), "door boxes must not catch the spray");
        }

        [Test]
        public void UnmodelledVehiclesStayGraybox()
        {
            Assert.IsNull(VehicleGlbBuilder.GlbFor("V05"));
            var go = Prefab("V05");
            Assert.IsNull(go.transform.Find("Model"));
            Assert.AreEqual(Layers.Vehicle, go.GetComponentInChildren<DirtPainter>(true).gameObject.layer);
        }

        [Test]
        public void HingesSwingOutward()
        {
            // left door, panel behind the hinge → free edge moves to -X
            var e = VehicleGlbBuilder.OpenEuler(new Vector3(-0.77f, 0.58f, 0.89f), new Vector3(0.07f, 0.42f, -0.79f), false);
            Assert.Less((Quaternion.Euler(e) * new Vector3(0.07f, 0.42f, -0.79f)).x, 0f);
            e = VehicleGlbBuilder.OpenEuler(new Vector3(0.77f, 0.58f, 0.89f), new Vector3(-0.07f, 0.42f, -0.79f), false);
            Assert.Greater((Quaternion.Euler(e) * new Vector3(-0.07f, 0.42f, -0.79f)).x, 0f);
            // hatch (top hinge) and sedan lid open upwards, pickup tailgate (bottom hinge) drops backwards
            Assert.Greater((Quaternion.Euler(VehicleGlbBuilder.OpenEuler(Vector3.zero, new Vector3(0, -0.33f, -0.40f), true)) * new Vector3(0, -0.33f, -0.40f)).y, 0f);
            Assert.Greater((Quaternion.Euler(VehicleGlbBuilder.OpenEuler(Vector3.zero, new Vector3(0, -0.10f, -0.55f), true)) * new Vector3(0, -0.10f, -0.55f)).y, 0f);
            Assert.Less((Quaternion.Euler(VehicleGlbBuilder.OpenEuler(Vector3.zero, new Vector3(0, 0.18f, 0f), true)) * new Vector3(0, 0.18f, 0f)).z, 0f);
        }
    }
}
