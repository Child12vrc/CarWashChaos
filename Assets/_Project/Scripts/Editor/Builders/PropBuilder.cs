using System.Collections.Generic;
using CarWash.Core;
using CarWash.Player;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace CarWash.Editor
{
    /// <summary>Graybox physics props (ASSETS: cones, bucket, tire, crate, hay bale...). Server-authoritative NetworkRigidbody.</summary>
    public static class PropBuilder
    {
        public const string Dir = "Assets/_Project/Prefabs/Props";

        struct Spec
        {
            public string id, nameKo; public PrimitiveType shape; public Vector3 size; public float mass; public Color color;
            public Spec(string id, string n, PrimitiveType s, Vector3 size, float mass, Color c) { this.id = id; nameKo = n; shape = s; this.size = size; this.mass = mass; color = c; }
        }

        static readonly Spec[] Specs =
        {
            new Spec("Crate", "나무 상자", PrimitiveType.Cube, new Vector3(0.8f, 0.8f, 0.8f), 20f, new Color(0.65f, 0.45f, 0.25f)),
            new Spec("HeavyCrate", "무거운 상자", PrimitiveType.Cube, new Vector3(1.2f, 1.2f, 1.2f), 60f, new Color(0.4f, 0.28f, 0.15f)),
            new Spec("Cone", "고깔", PrimitiveType.Cylinder, new Vector3(0.35f, 0.35f, 0.35f), 2f, new Color(1f, 0.45f, 0.1f)),
            new Spec("Bucket", "양동이", PrimitiveType.Cylinder, new Vector3(0.35f, 0.2f, 0.35f), 1.5f, new Color(0.2f, 0.5f, 0.9f)),
            new Spec("Tire", "타이어", PrimitiveType.Cylinder, new Vector3(0.7f, 0.12f, 0.7f), 10f, new Color(0.1f, 0.1f, 0.1f)),
            new Spec("HayBale", "건초 더미", PrimitiveType.Cube, new Vector3(1.0f, 0.5f, 0.5f), 25f, new Color(0.9f, 0.8f, 0.35f)),
        };

        public static string PathOf(string id) => $"{Dir}/{id}.prefab";

        /// <summary>Interior trash (ASSETS: cup, wrapper, bottle, diaper, fries ≤150 tris).</summary>
        static readonly Spec[] TrashSpecs =
        {
            new Spec("Trash_Cup", "종이컵", PrimitiveType.Cylinder, new Vector3(0.08f, 0.06f, 0.08f), 0.1f, new Color(0.95f, 0.95f, 0.95f)),
            new Spec("Trash_Bottle", "빈 병", PrimitiveType.Cylinder, new Vector3(0.07f, 0.12f, 0.07f), 0.3f, new Color(0.3f, 0.75f, 0.45f)),
            new Spec("Trash_Wrapper", "과자 봉지", PrimitiveType.Cube, new Vector3(0.16f, 0.02f, 0.12f), 0.05f, new Color(0.95f, 0.3f, 0.3f)),
            new Spec("Trash_Fries", "감자튀김 상자", PrimitiveType.Cube, new Vector3(0.1f, 0.12f, 0.06f), 0.15f, new Color(0.95f, 0.75f, 0.2f)),
            new Spec("Trash_Diaper", "기저귀", PrimitiveType.Cube, new Vector3(0.2f, 0.06f, 0.15f), 0.3f, new Color(0.85f, 0.9f, 0.95f)),
        };

        public static List<GameObject> BuildAll()
        {
            var list = new List<GameObject>();
            foreach (var s in Specs) list.Add(Build(s));
            list.Add(BuildCautionSign());
            foreach (var s in TrashSpecs)
            {
                var go = Build(s, trash: true);
                list.Add(go);
            }
            return list;
        }

        public static IEnumerable<NetworkObject> TrashPrefabs()
        {
            foreach (var s in TrashSpecs) yield return AssetDatabase.LoadAssetAtPath<GameObject>(PathOf(s.id)).GetComponent<NetworkObject>();
        }

        static GameObject Build(Spec s, bool trash = false)
        {
            var go = GameObject.CreatePrimitive(s.shape);
            go.name = s.id;
            go.layer = Layers.Prop;
            go.transform.localScale = s.size;
            go.GetComponent<Renderer>().sharedMaterial = GrayboxMaterials.Get("Prop_" + s.id, s.color);
            if (s.shape == PrimitiveType.Cylinder)
            {
                // primitive cylinder has a capsule collider; a convex mesh gives proper flat-bottom physics
                Object.DestroyImmediate(go.GetComponent<Collider>());
                var mc = go.AddComponent<MeshCollider>();
                mc.convex = true;
            }
            foreach (var col in go.GetComponents<Collider>()) col.sharedMaterial = PhysFor(s.id);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = s.mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            go.AddComponent<NetworkObject>();
            var nt = go.AddComponent<NetworkTransform>();
            nt.Interpolate = true;
            go.AddComponent<NetworkRigidbody>();
            var g = go.AddComponent<Grabbable>();
            var so = new SerializedObject(g);
            so.FindProperty("_displayName").stringValue = s.nameKo;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (trash)
            {
                go.tag = Layers.TagTrash;
                go.AddComponent<Vehicles.TrashItem>();
            }
            return PrefabBuilder.Save(go, PathOf(s.id));
        }

        /// <summary>P6 physic materials per prop kind (friction / bounce tuned for spraying, throwing and wet floors).</summary>
        static PhysicsMaterial PhysFor(string id)
        {
            if (id.Contains("Crate")) return EnvironmentBuilder.Phys("Wood", 0.55f, 0.65f, 0.05f);
            if (id == "Cone") return EnvironmentBuilder.Phys("RubberCone", 0.9f, 1.0f, 0.3f);
            if (id == "Tire") return EnvironmentBuilder.Phys("RubberTire", 0.95f, 1.0f, 0.45f);
            if (id == "HayBale") return EnvironmentBuilder.Phys("Hay", 0.9f, 1.0f, 0.0f);
            if (id == "Bucket" || id == "CautionSign" || id.StartsWith("Trash")) return EnvironmentBuilder.Phys("Plastic", 0.45f, 0.55f, 0.2f);
            return null;
        }

        /// <summary>P6: "미끄럼 주의" A-frame (art: Signs/sign_caution_slippery.png on both faces), 3 kg, knock-over-able.</summary>
        static GameObject BuildCautionSign()
        {
            var go = new GameObject("CautionSign") { layer = Layers.Prop };
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(0.5f, 0.8f, 0.42f); col.center = new Vector3(0f, 0f, 0f);
            col.sharedMaterial = PhysFor("CautionSign");
            for (int side = -1; side <= 1; side += 2)
            {
                var q = EnvironmentBuilder.Sign(go.transform, "sign_caution_slippery.png", Vector3.zero, side < 0 ? 0f : 180f, 0.78f);
                if (q == null) { var box = GrayboxMaterials.Box(go.transform, "Panel" + side, new Vector3(0, 0, side * 0.1f), new Vector3(0.48f, 0.78f, 0.02f), GrayboxMaterials.Get("Prop_CautionSign", new Color(1f, 0.85f, 0.1f)), collider: false); box.layer = Layers.Prop; continue; }
                q.layer = Layers.Prop;
                q.transform.localPosition = new Vector3(0f, 0f, side * 0.1f);
                q.transform.localRotation = Quaternion.Euler(side * 12f, side < 0 ? 180f : 0f, 0f);
                q.transform.localScale = new Vector3(0.48f, 0.78f, 1f);
            }
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 3f; rb.interpolation = RigidbodyInterpolation.Interpolate; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            go.AddComponent<NetworkObject>();
            go.AddComponent<NetworkTransform>().Interpolate = true;
            go.AddComponent<NetworkRigidbody>();
            var g = go.AddComponent<Grabbable>();
            var so = new SerializedObject(g);
            so.FindProperty("_displayName").stringValue = "미끄럼 주의 표지판";
            so.ApplyModifiedPropertiesWithoutUndo();
            return PrefabBuilder.Save(go, PathOf("CautionSign"));
        }

        /// <summary>Adds the lot's starting props as server spawn entries (never in-scene NetworkObjects).</summary>
        public static void PlaceInLot(SpawnListWriter list)
        {
            void Put(string id, Vector3 pos, float yaw = 0f) => list.Add(PathOf(id), pos, new Vector3(0, yaw, 0));
            Put("Crate", new Vector3(-3.5f, 0.41f, -8.5f));
            Put("Crate", new Vector3(-3.5f, 1.22f, -8.5f), 20f);
            Put("HeavyCrate", new Vector3(-3.5f, 0.61f, -6.6f)); // P6: moved off the animal cage footprint
            for (int i = 0; i < 5; i++) Put("Cone", Core.DevYard.ConeRow + new Vector3(i * 0.9f, 0.36f, 0f)); // dev yard, within its washer reach
            Put("Bucket", new Vector3(-12f, 0.21f, 1f));
            Put("Tire", new Vector3(12f, 0.13f, -8f));
            Put("Tire", new Vector3(12.8f, 0.13f, -7.2f));
            Put("HayBale", new Vector3(15f, 0.26f, 10f));
            // P6: caution A-frames by the bay entrances + extra props around the lot
            Put("CautionSign", new Vector3(-4.4f, 0.42f, 10.2f), 30f);
            Put("CautionSign", new Vector3(4.4f, 0.42f, 10.2f), -20f);
            Put("Cone", new Vector3(19.2f, 0.36f, -8.2f)); Put("Cone", new Vector3(19.2f, 0.36f, -12.5f));
            Put("Bucket", new Vector3(-5.6f, 0.21f, -1.2f)); Put("Bucket", new Vector3(2.6f, 0.21f, -1.2f));
            Put("Tire", new Vector3(13.2f, 0.37f, -7.6f), 10f);
            Put("HayBale", new Vector3(16.1f, 0.26f, 10.3f), 15f);
        }
    }
}
