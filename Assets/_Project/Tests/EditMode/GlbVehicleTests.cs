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
        public void EveryVehicle_UsesItsNewestModel_DoorlessOnesHaveNoCabin()
        {
            // Batch F #7 / #8: all 12 vehicles are modelled now; the newest revision (_vN) wins
            foreach (var id in new[] { "V01", "V02", "V03", "V04", "V05", "V06", "V07", "V08", "B01", "B02", "B03", "B04" })
            {
                var glb = VehicleGlbBuilder.GlbFor(id);
                Assert.IsNotNull(glb, id);
                StringAssert.EndsWith($"_v{VehicleGlbBuilder.MaxRevision}.glb", glb, id);
                var model = Prefab(id).transform.Find("Model");
                Assert.IsNotNull(model, id);
                // M4.11 (Batch H v3): three LOD levels, each lighter than the one before; the body LODs are fed by the painter
                var lg = Prefab(id).GetComponent<LODGroup>();
                Assert.IsNotNull(lg, id + " LODGroup on the vehicle root");
                var lods = lg.GetLODs();
                Assert.AreEqual(3, lods.Length, id);
                // the cabin is drawn up close only
                var cabin = Prefab(id).transform.Find("Interior");
                if (cabin != null)
                    foreach (var r in cabin.GetComponentsInChildren<Renderer>(true))
                    {
                        Assert.IsTrue(lods[0].renderers.Contains(r), id + " cabin at LOD0: " + r.name);
                        Assert.IsFalse(lods[1].renderers.Contains(r) || lods[2].renderers.Contains(r), id + " cabin not at LOD1/2: " + r.name);
                    }
                int Tris(LOD l) => l.renderers.Where(r => r != null).Sum(r => r.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3);
                Assert.Less(Tris(lods[1]), Tris(lods[0]), id + " LOD1 < LOD0");
                Assert.Less(Tris(lods[2]), Tris(lods[1]), id + " LOD2 < LOD1");
                var painter = model.GetComponentInChildren<DirtPainter>(true);
                var lodBodies = new SerializedObject(painter).FindProperty("_lodRenderers");
                int bodyLodNodes = new[] { "Body_LOD1", "Body_LOD2" }.Count(n => model.Find(n) != null && model.Find(n).GetComponent<MeshRenderer>() != null);
                Assert.Greater(bodyLodNodes, 0, id + " has body LODs");
                Assert.AreEqual(bodyLodNodes, lodBodies.arraySize, id + " every Body_LODn is fed by the painter");
            }
            // tank / airboat / UFO: no side doors, no procedural cabin (seats would sit inside the hull / dome), so no driver
            foreach (var id in new[] { "V08", "B03", "B04" })
            {
                var go = Prefab(id);
                Assert.IsFalse(go.GetComponentsInChildren<Vehicles.VehicleDoor>(true).Any(d => !d.IsTrunk), id);
                Assert.IsNull(go.transform.Find("Interior/DriverSeat"), id);
            }
        }

        [Test]
        public void CleanPct_SnapsTo100_FromCompleteAt()
        {
            Assert.AreEqual(1f, DirtNetSync.Snap(0.995f));
            Assert.AreEqual(1f, DirtNetSync.Snap(0.998f));
            Assert.AreEqual(0.99f, DirtNetSync.Snap(0.99f));
            Assert.Less(DirtNetSync.CompleteAt, 0.999f, "DayManager auto-finishes at 0.999 → the snapped value must reach it");
        }

        [Test]
        public void HullCabinBox_DoesNotRiseAboveTheCabRoof()
        {
            // v0.9: V05 / V07 / B02 have a cab lower than the box behind it — the cabin hull box used to reach the whole body's
            // top and hid the box's front face from the spray (V05 stuck at 98.9 %)
            foreach (var id in new[] { "V05", "V07", "B02", "V01" })
            {
                var go = Prefab(id);
                var body = go.transform.Find("Model/Body").GetComponent<MeshFilter>().sharedMesh;
                var glass = go.transform.Find("Model/Glass").GetComponent<MeshFilter>().sharedMesh.bounds;
                float roof = VehicleGlbBuilder.TopOver(body, glass.min.z, glass.max.z, float.MinValue, float.MaxValue);
                var cabin = go.transform.Find("Hull").GetComponents<BoxCollider>()[1];
                Assert.LessOrEqual(cabin.center.y + cabin.size.y * 0.5f, roof + 0.01f, id);
            }
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
