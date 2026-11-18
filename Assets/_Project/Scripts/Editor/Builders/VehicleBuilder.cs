using System.Collections.Generic;
using System.Linq;
using CarWash.Core;
using CarWash.Data;
using CarWash.Dirt;
using CarWash.Vehicles;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace CarWash.Editor
{
    /// <summary>
    /// PrefabBuilder from VehicleData (T2.2): one networked prefab per vehicle id at VehicleData.prefab
    /// (body + Door_L/Door_R/Trunk on hinges + sockets, DirtNetSync, Vehicle, VehiclePathMover, carving obstacle).
    /// Also the static dirty test cars used in the dev yard.
    /// </summary>
    public static class VehicleBuilder
    {
        public const string Dir = "Assets/_Project/Prefabs/Vehicles";
        public static string TestCarPath(string id) => $"{Dir}/TestCar_{id}.prefab";
        public static string PathOf(VehicleData d) => $"Assets/_Project/{d.prefab}.prefab";

        /// <summary>Graybox shape per vehicle (non-MVP ids reuse the closest MVP silhouette).</summary>
        public static string ShapeOf(string id) =>
            id == "V05" || id == "V06" ? "V04" : id == "V07" || id == "V08" || id.StartsWith("B") ? "B01" : id;

        public static List<GameObject> BuildAll()
        {
            var list = new List<GameObject>
            {
                BuildTestCar("V02", new Vector3(0.9f, 0f, 0f), new Color(0.15f, 0.4f, 0.85f)),
                BuildTestCar("V04", new Vector3(0.3f, 0.75f, 0f), new Color(0.9f, 0.9f, 0.88f)),
            };
            foreach (var d in DataBuilder.LoadRegistry().vehicles) list.Add(BuildVehicle(d));
            return list;
        }

        public static IEnumerable<NetworkObject> VehiclePrefabs() =>
            DataBuilder.LoadRegistry().vehicles.Select(d => AssetDatabase.LoadAssetAtPath<GameObject>(PathOf(d))).Where(g => g != null).Select(g => g.GetComponent<NetworkObject>());

        static GameObject BuildVehicle(VehicleData d)
        {
            string shape = ShapeOf(d.id);
            var root = new GameObject(System.IO.Path.GetFileName(d.prefab)) { layer = Layers.Vehicle };
            var rb = root.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.Interpolate;
            root.AddComponent<NetworkObject>();
            var nt = root.AddComponent<NetworkTransform>();
            nt.SyncRotAngleX = nt.SyncRotAngleZ = false; nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false; nt.Interpolate = true;

            DirtPainter painter;
            Transform interior, surprise, driver;
            var cabin = new VehicleInteriorBuilder.Cabin();
            float w, minZ, maxZ, maxY;
            VehicleGlbBuilder.Parts glb = null;
            string glbPath = VehicleGlbBuilder.GlbFor(d.id); // only the exact id's model (V01/V02/V03 now); others stay graybox
            if (glbPath != null)
            {
                glb = VehicleGlbBuilder.Build(glbPath, d.id, root.transform);
                painter = glb.Painter;
                interior = glb.Interior; surprise = glb.Surprise; driver = glb.Driver;
                w = glb.BodyBounds.size.x; minZ = glb.Bounds.min.z; maxZ = glb.Bounds.max.z; maxY = glb.Bounds.max.y;
                // M4.6 D3: cabin = the glass box (belt line → roof, windscreen → rear window) inside the body
                var glass = root.transform.Find("Model/Glass");
                var gb = glass != null ? GlbKit.LocalBounds(glass.gameObject, root.transform) : new Bounds(glb.BodyBounds.center + Vector3.up * 0.3f, glb.BodyBounds.size * 0.6f);
                var bb = glb.BodyBounds;
                cabin.halfW = Mathf.Min(gb.size.x, bb.size.x) * 0.5f - 0.1f;
                cabin.beltY = gb.min.y; cabin.roofY = gb.max.y;
                cabin.floorY = Mathf.Max(bb.min.y + 0.25f, gb.min.y - 0.75f);
                cabin.rearZ = gb.min.z + 0.15f; cabin.frontZ = gb.max.z - 0.25f;
            }
            else
            {
                var body = DirtTestBuilder.CreateCarVisual(shape, root.transform, out painter, out _);
                body.layer = Layers.Vehicle; body.tag = Layers.TagDirtable;
                var prof = VehicleMeshBuilder.Profile(shape, out w);
                minZ = prof.Min(p => p.x); maxZ = prof.Max(p => p.x); maxY = prof.Max(p => p.y);

                // doors: hinge at the front edge, panel extends backwards; trunk hinge at the rear top
                var doorMat = GrayboxMaterials.Get("DoorPanel", new Color(0.3f, 0.32f, 0.36f));
                float doorZ = Mathf.Lerp(minZ, maxZ, 0.62f), doorY = 0.55f + (shape == "B01" ? 1.0f : 0f);
                Door(root.transform, "Door_L", new Vector3(-w * 0.5f - 0.04f, doorY, doorZ), new Vector3(0.06f, 0.7f, 1.1f), new Vector3(0, 0, -0.55f), new Vector3(0, -75f, 0), "운전석 문", doorMat);
                Door(root.transform, "Door_R", new Vector3(w * 0.5f + 0.04f, doorY, doorZ), new Vector3(0.06f, 0.7f, 1.1f), new Vector3(0, 0, -0.55f), new Vector3(0, 75f, 0), "조수석 문", doorMat);
                Door(root.transform, "Trunk", new Vector3(0, maxY * 0.72f, minZ - 0.04f), new Vector3(w * 0.8f, 0.5f, 0.06f), new Vector3(0, -0.25f, 0), new Vector3(-80f, 0, 0), "트렁크", doorMat);

                interior = Socket(root.transform, "Socket_Interior", new Vector3(0, doorY, doorZ - 0.5f));
                cabin.halfW = w * 0.5f - 0.12f; cabin.floorY = doorY - 0.3f; cabin.beltY = doorY + 0.35f; cabin.roofY = maxY - 0.05f;
                cabin.frontZ = doorZ + 0.55f; cabin.rearZ = Mathf.Max(minZ + 0.4f, doorZ - 1.4f);
                surprise = Socket(root.transform, "Socket_Surprise", new Vector3(0, maxY + 0.2f, 0));
                driver = Socket(root.transform, "Socket_Driver", new Vector3(-w * 0.25f, doorY + 0.3f, doorZ - 0.3f));
            }

            var trashSpots = VehicleInteriorBuilder.Build(root.transform, d.id, cabin); // M4.6 D3
            var sync = root.AddComponent<DirtNetSync>();
            SceneBuilder.Set(sync, "_painter", painter);
            SceneBuilder.Set(sync, "_data", DataBuilder.LoadRegistry());
            var mover = root.AddComponent<VehiclePathMover>();
            var mso = new SerializedObject(mover); mso.FindProperty("_halfLength").floatValue = (maxZ - minZ) * 0.5f; mso.ApplyModifiedPropertiesWithoutUndo();
            var v = root.AddComponent<Vehicle>();
            root.AddComponent<SurpriseTrigger>(); // T3.6
            SceneBuilder.Set(v, "_data", DataBuilder.LoadRegistry());
            SceneBuilder.Set(v, "_interior", interior);
            SceneBuilder.Set(v, "_surprise", surprise);
            SceneBuilder.Set(v, "_driver", driver);
            PrefabBuilder.SetArray(v, "_trashSpots", trashSpots);
            var vso = new SerializedObject(v); vso.FindProperty("_dataId").stringValue = d.id;
            if (glb != null)
            {
                var pr = vso.FindProperty("_paintRenderers"); var ps = vso.FindProperty("_paintSlots");
                pr.arraySize = glb.PaintRenderers.Count; ps.arraySize = glb.PaintSlots.Count;
                for (int i = 0; i < glb.PaintRenderers.Count; i++) { pr.GetArrayElementAtIndex(i).objectReferenceValue = glb.PaintRenderers[i]; ps.GetArrayElementAtIndex(i).intValue = glb.PaintSlots[i]; }
            }
            vso.ApplyModifiedPropertiesWithoutUndo();

            var obs = root.AddComponent<NavMeshObstacle>();
            obs.carving = true; obs.shape = NavMeshObstacleShape.Box;
            obs.center = new Vector3(0, maxY * 0.5f, (minZ + maxZ) * 0.5f); obs.size = new Vector3(w + 0.2f, maxY, maxZ - minZ + 0.2f);
            obs.carveOnlyStationary = true;
            return PrefabBuilder.Save(root, PathOf(d));
        }

        static Transform Socket(Transform root, string n, Vector3 p) { var t = new GameObject(n).transform; t.SetParent(root, false); t.localPosition = p; return t; }

        static VehicleDoor Door(Transform parent, string name, Vector3 hingePos, Vector3 panelSize, Vector3 panelOffset, Vector3 openEuler, string label, Material m)
        {
            var hinge = new GameObject(name).transform;
            hinge.SetParent(parent, false); hinge.localPosition = hingePos;
            hinge.gameObject.layer = Layers.Vehicle;
            GrayboxMaterials.Box(hinge, "Panel", panelOffset, panelSize, m, layer: Layers.Vehicle);
            var door = hinge.gameObject.AddComponent<VehicleDoor>();
            var so = new SerializedObject(door);
            so.FindProperty("_label").stringValue = label;
            so.FindProperty("_hinge").objectReferenceValue = hinge;
            so.FindProperty("_openEuler").vector3Value = openEuler;
            so.ApplyModifiedPropertiesWithoutUndo();
            return door;
        }

        static GameObject BuildTestCar(string id, Vector3 dirt, Color paint)
        {
            var root = new GameObject("TestCar_" + id) { layer = Layers.Vehicle };
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>();
            var body = DirtTestBuilder.CreateCarVisual(id, root.transform, out var painter, out _);
            body.layer = Layers.Vehicle;
            body.tag = Layers.TagDirtable;
            var pso = new SerializedObject(painter);
            pso.FindProperty("_baseColor").colorValue = paint;
            pso.ApplyModifiedPropertiesWithoutUndo();
            var sync = root.AddComponent<DirtNetSync>();
            SceneBuilder.Set(sync, "_painter", painter);
            SceneBuilder.Set(sync, "_data", DataBuilder.LoadRegistry());
            var so = new SerializedObject(sync);
            so.FindProperty("_defaultDirt").vector3Value = dirt;
            so.ApplyModifiedPropertiesWithoutUndo();
            return PrefabBuilder.Save(root, TestCarPath(id));
        }
    }
}
