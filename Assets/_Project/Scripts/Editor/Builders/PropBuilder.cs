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
            new Spec("TrashBag", "쓰레기봉투", PrimitiveType.Sphere, new Vector3(0.5f, 0.6f, 0.5f), 4f, new Color(0.2f, 0.2f, 0.22f)),
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
            if (!trash) { var glb = BuildGlb(s.id, s.nameKo, s.mass); if (glb != null) return glb; }
            var go = GameObject.CreatePrimitive(s.shape);
            go.name = s.id;
            go.layer = Layers.Prop;
            go.transform.localScale = s.size;
            go.GetComponent<Renderer>().sharedMaterial = GrayboxMaterials.Get("Prop_" + s.id, s.color);
            // Blender interior trash (Batch F #4, Props/T_cup…): the primitive stays as the physics shape, the model is the look
            // (pivot = bottom centre; counter-scaled because the primitive carries its size in its own scale)
            string tglb = trash ? GlbKit.Find("Props", "T_" + s.id.Substring("Trash_".Length).ToLowerInvariant()) : null;
            if (tglb != null)
            {
                go.GetComponent<MeshRenderer>().enabled = false;
                var holder = new GameObject("Look").transform; holder.SetParent(go.transform, false);
                holder.localPosition = new Vector3(0f, s.shape == PrimitiveType.Cylinder ? -1f : -0.5f, 0f);
                holder.localScale = new Vector3(1f / s.size.x, 1f / s.size.y, 1f / s.size.z);
                GlbKit.Model(tglb, holder, Layers.Prop, Vector3.zero);
            }
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
            if (id == "TrashBag") return EnvironmentBuilder.Phys("Bag", 0.8f, 0.9f, 0.05f);
            if (id == "Bucket" || id == "CautionSign" || id.StartsWith("Trash")) return EnvironmentBuilder.Phys("Plastic", 0.45f, 0.55f, 0.2f);
            return null;
        }

        /// <summary>P6: "미끄럼 주의" A-frame (art: Signs/sign_caution_slippery.png on both faces), 3 kg, knock-over-able.</summary>
        static GameObject BuildCautionSign()
        {
            var glbSign = BuildGlb("CautionSign", "미끄럼 주의 표지판", 3f);
            if (glbSign != null) return glbSign;
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

        // ---------------- GLB props (docs/ART_IMPORT.md 3D Models) ----------------
        /// <summary>Graybox id → .glb, collider kind (box / convex hull), uniform scale, material tint.</summary>
        struct GlbSpec { public string file; public bool box; public float scale; public Color tint; }

        static readonly Dictionary<string, GlbSpec> Glb = new Dictionary<string, GlbSpec>
        {
            { "Crate",       new GlbSpec { file = "P_crate",         box = true,  scale = 1.2f, tint = Color.white } },
            { "HeavyCrate",  new GlbSpec { file = "P_crate",         box = true,  scale = 1.8f, tint = new Color(0.62f, 0.55f, 0.5f) } },
            { "Cone",        new GlbSpec { file = "P_traffic_cone",  box = false, scale = 1.2f, tint = Color.white } },
            { "Bucket",      new GlbSpec { file = "P_bucket",        box = false, scale = 1f,   tint = Color.white } },
            { "Tire",        new GlbSpec { file = "P_tire",          box = false, scale = 1f,   tint = Color.white } },
            { "HayBale",     new GlbSpec { file = "P_hay_bale",      box = true,  scale = 1f,   tint = Color.white } },
            { "TrashBag",    new GlbSpec { file = "P_trash_bag",     box = false, scale = 1f,   tint = Color.white } },
            { "CautionSign", new GlbSpec { file = "P_caution_sign",  box = false, scale = 1f,   tint = Color.white } },
        };

        /// <summary>
        /// The .glb as the visual (origin bottom-centre in the file) re-centred so the prefab root sits at the bounds centre like
        /// the graybox did — the grab hold point, throw spin and lot placements all assume a centred pivot. Returns null when the
        /// model is missing (graybox stays).
        /// </summary>
        static GameObject BuildGlb(string id, string nameKo, float mass)
        {
            if (!Glb.TryGetValue(id, out var g)) return null;
            string path = GlbKit.Find("Props", g.file);
            if (path == null) return null;
            var go = new GameObject(id) { layer = Layers.Prop };
            System.Func<Material, Material> tint = null;
            if (g.tint != Color.white) tint = m => GlbKit.Tinted(m, id, g.tint);
            var model = GlbKit.Model(path, go.transform, Layers.Prop, Vector3.zero, g.scale, tint);
            if (g.file == "P_crate") UnfightCrate(model);
            var b = GlbKit.LocalBounds(model, go.transform);
            model.transform.localPosition = -b.center;
            b.center = Vector3.zero;
            if (id == "CautionSign") SignFaces(model);
            if (g.box) GlbKit.Box(go, b);
            else GlbKit.Convex(go, GlbKit.HullMesh(model, go.transform, "Prop_" + id + "_Hull"));
            foreach (var col in go.GetComponents<Collider>()) col.sharedMaterial = PhysFor(id);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            go.AddComponent<NetworkObject>();
            go.AddComponent<NetworkTransform>().Interpolate = true;
            go.AddComponent<NetworkRigidbody>();
            var grab = go.AddComponent<Grabbable>();
            var so = new SerializedObject(grab);
            so.FindProperty("_displayName").stringValue = nameKo;
            so.ApplyModifiedPropertiesWithoutUndo();
            return PrefabBuilder.Save(go, PathOf(id));
        }

        /// <summary>
        /// M4.6 B5: in P_crate the slats, the diagonal braces and the corner posts all sit on the same outer planes → z-fighting
        /// (flickering, "clipping" boards). Without touching the .glb: slats 1.5 % in, braces 1.2 % out, posts stay — every face
        /// now has its own depth. Collider bounds are taken afterwards, so the box still wraps the whole visual.
        /// </summary>
        static void UnfightCrate(GameObject model)
        {
            void Scale(string node, float s) { var t = GlbKit.Node(model, node); if (t == null) return; var b = GlbKit.LocalBounds(t.gameObject, t.parent); t.localScale = Vector3.one * s; t.localPosition = b.center * (1f - s); }
            Scale("Slats", 0.985f);
            Scale("CrateCore", 0.97f);
            Scale("DiagonalBraces", 1.012f);
        }

        /// <summary>
        /// P_caution_sign: SignFace (front leaf only, planar UV) gets sign_caution_slippery.png; a copy turned 180° covers the
        /// back leaf (the A-frame is symmetric). ART NOTE: SignFace's V runs top→bottom (image would be upside down) → the
        /// material flips V (tiling y -1) instead of touching the .glb.
        /// </summary>
        static void SignFaces(GameObject model)
        {
            var face = GlbKit.Node(model, "SignFace");
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(GrayboxMaterials.EnvTex.Replace("Textures/", "Signs/") + "sign_caution_slippery.png");
            if (face == null || tex == null) return;
            string matPath = GlbKit.MatDir + "/SignFace_caution.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, matPath); }
            m.SetTexture("_BaseMap", tex); m.mainTexture = tex; m.SetColor("_BaseColor", Color.white);
            m.SetTextureScale("_BaseMap", new Vector2(1f, -1f)); m.SetTextureOffset("_BaseMap", new Vector2(0f, 1f));
            m.SetFloat("_Smoothness", 0.3f);
            m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON"); m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            EditorUtility.SetDirty(m);
            face.GetComponent<MeshRenderer>().sharedMaterial = m;
            var back = Object.Instantiate(face.gameObject, face.parent, false);
            back.name = "SignFace_Back";
            back.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * face.localRotation;
            back.transform.localPosition = Quaternion.Euler(0f, 180f, 0f) * face.localPosition;
        }

        /// <summary>Lot spawn height for a prop resting on the ground (root = bounds centre).</summary>
        static float RestY(string id)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(PathOf(id));
            var c = p != null ? p.GetComponent<Collider>() : null;
            if (c is BoxCollider bc) return bc.size.y * 0.5f - bc.center.y + 0.01f;
            if (c is MeshCollider mc && mc.sharedMesh != null) return -mc.sharedMesh.bounds.min.y + 0.01f;
            return 0.4f;
        }
        /// <summary>Adds the lot's starting props as server spawn entries (never in-scene NetworkObjects).</summary>
        public static void PlaceInLot(SpawnListWriter list)
        {
            // GLB props: rest on the ground (or `stack` levels up) at their real height; graybox keeps the hand-set y
            void Put(string id, Vector3 pos, float yaw = 0f, int stack = 0)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PathOf(id));
                // M4.6 B5: stacked GLB props rest exactly on the one below + 2 cm (they used to start 2 cm inside it)
                if (prefab != null && prefab.transform.Find("Model") != null) { float r = RestY(id); pos.y = r + stack * ((r - 0.01f) * 2f + 0.02f); }
                list.Add(PathOf(id), pos, new Vector3(0, yaw, 0));
            }
            Put("Crate", new Vector3(-3.5f, 0.41f, -8.5f));
            Put("Crate", new Vector3(-3.5f, 1.22f, -8.5f), 20f, stack: 1);
            Put("HeavyCrate", new Vector3(-3.5f, 0.61f, -6.6f)); // P6: moved off the animal cage footprint
            list.DevOnly = true; // P7: dev yard cone row (release builds skip it)
            for (int i = 0; i < 5; i++) Put("Cone", Core.DevYard.ConeRow + new Vector3(i * 0.9f, 0.36f, 0f)); // dev yard, within its washer reach
            list.DevOnly = false;
            Put("Bucket", new Vector3(-12f, 0.21f, 1f));
            Put("Tire", new Vector3(12f, 0.13f, -8f));
            Put("Tire", new Vector3(12.8f, 0.13f, -7.2f));
            Put("HayBale", new Vector3(15f, 0.26f, 10f));
            // P6: caution A-frames by the bay entrances + extra props around the lot
            Put("CautionSign", new Vector3(-4.4f, 0.42f, 10.2f), 30f);
            Put("CautionSign", new Vector3(4.4f, 0.42f, 10.2f), -20f);
            Put("Cone", new Vector3(19.2f, 0.36f, -8.2f)); Put("Cone", new Vector3(19.2f, 0.36f, -12.5f)); // by the drying / vacuum area (G9)
            Put("Bucket", new Vector3(-5.6f, 0.21f, -1.2f)); Put("Bucket", new Vector3(2.6f, 0.21f, -1.2f));
            Put("Tire", new Vector3(12.4f, 0.37f, -7.6f), 10f, stack: 1);
            Put("HayBale", new Vector3(16.1f, 0.26f, 10.3f), 15f);
            // GLB batch: trash bags piled by the alley dumpster
            Put("TrashBag", new Vector3(13.9f, 0.3f, 15.6f), 20f); Put("TrashBag", new Vector3(14.1f, 0.3f, 14.7f), -35f); Put("TrashBag", new Vector3(18.1f, 0.3f, 15.2f), 70f);
        }
    }
}
