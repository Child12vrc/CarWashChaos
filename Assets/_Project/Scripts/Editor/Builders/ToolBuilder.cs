using System.Collections.Generic;
using CarWash.Core;
using CarWash.Tools;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace CarWash.Editor
{
    /// <summary>Graybox tool prefabs (ASSETS: viewmodel + world version = same mesh) + FoamPatch. Networked, server-auth physics.</summary>
    public static class ToolBuilder
    {
        public const string Dir = "Assets/_Project/Prefabs/Tools";
        public const string ParticleMatPath = "Assets/_Project/Art/Materials/SprayParticle.mat";
        public const string FoamPatchPath = "Assets/_Project/Prefabs/FX/FoamPatch.prefab";

        public static string PathOf(string file) => $"{Dir}/{file}.prefab";

        public static List<GameObject> BuildAll()
        {
            var patch = BuildFoamPatch();
            var washer = BuildTool<PressureWasher>("T01", "T01_PressureWasher", new Color(1f, 0.8f, 0.1f), 4f, GunShape);
            // ART_IMPORT.md tints: droplet #9FDDFB, mist #E6F6FF; splash sheet 4x4
            SetupSpray(washer, new VfxSpec
            {
                stream = "fx_water_droplet", streamColor = WithA(ArtBuilder.Hex("#9FDDFB"), 0.9f), streamSize = 0.09f,
                mist = "fx_mist_puff", mistColor = WithA(ArtBuilder.Hex("#E6F6FF"), 0.35f), mistSize = 0.45f,
                splash = "fx_splash_sheet", splashColor = Color.white, splashSize = 0.8f, sheet = true, pitch = 1f,
            }, null);
            AddHose(washer);
            // M4.6 D7: the washer wets what it hits (floor / wall decals, puddles); the foam cannon keeps its foam patches
            var vfxW = washer.GetComponentInChildren<SprayVfx>();
            // M4.6 D7: light props drift a little in the stream (was ~5.6 m per 2 s point blank → ×0.4)
            var wso = new SerializedObject(washer.GetComponent<PressureWasher>()); wso.FindProperty("_propForceScale").floatValue = 0.4f; wso.ApplyModifiedPropertiesWithoutUndo();
            SceneBuilder.Set(vfxW, "_wetDecal", WaterDecals.Wet());
            SceneBuilder.Set(vfxW, "_puddleDecal", WaterDecals.Puddle());
            SceneBuilder.Set(vfxW, "_foamDecal", WaterDecals.FloorFoam()); // M4.8 F2
            var foam = BuildTool<FoamCannon>("T02", "T02_FoamCannon", new Color(0.95f, 0.45f, 0.75f), 5f, GunShape);
            SetupSpray(foam, new VfxSpec
            {
                stream = "fx_foam_blob", streamColor = new Color(1f, 1f, 1f, 0.95f), streamSize = 0.2f,
                mist = "fx_bubble", mistColor = new Color(1f, 1f, 1f, 0.9f), mistSize = 0.14f,
                splash = "fx_foam_blob", splashColor = Color.white, splashSize = 0.55f, sheet = false, pitch = 0.7f,
            }, patch);
            var towel = BuildTool<Towel>("T03", "T03_Towel", new Color(0.3f, 0.8f, 0.9f), 0.5f, TowelShape);
            var net = BuildTool<NetPole>("T06", "T06_NetPole", new Color(0.3f, 0.7f, 0.35f), 2f, NetShape);
            return new List<GameObject> { Save(washer, "T01_PressureWasher"), Save(foam, "T02_FoamCannon"), Save(towel, "T03_Towel"), Save(net, "T06_NetPole"), patch.gameObject };
        }

        static void NetShape(Transform root, Material mat, Material dark)
        {
            GrayboxMaterials.Box(root, "Pole", new Vector3(0, 0, 0.45f), new Vector3(0.035f, 0.035f, 1.3f), dark, layer: Layers.Tool);
            var hoop = new Vector3(0, 0, 1.3f);
            GrayboxMaterials.Box(root, "HoopL", hoop + new Vector3(-0.22f, 0, 0), new Vector3(0.03f, 0.03f, 0.44f), mat, layer: Layers.Tool);
            GrayboxMaterials.Box(root, "HoopR", hoop + new Vector3(0.22f, 0, 0), new Vector3(0.03f, 0.03f, 0.44f), mat, layer: Layers.Tool);
            GrayboxMaterials.Box(root, "HoopF", hoop + new Vector3(0, 0, 0.22f), new Vector3(0.47f, 0.03f, 0.03f), mat, layer: Layers.Tool);
            GrayboxMaterials.Box(root, "Mesh", hoop + new Vector3(0, -0.12f, 0), new Vector3(0.4f, 0.22f, 0.4f),
                GrayboxMaterials.Get("NetMesh", new Color(0.85f, 0.85f, 0.8f)), collider: false, layer: Layers.Tool);
        }

        static void GunShape(Transform root, Material mat, Material dark)
        {
            GrayboxMaterials.Box(root, "Body", Vector3.zero, new Vector3(0.12f, 0.2f, 0.3f), mat, layer: Layers.Tool);
            GrayboxMaterials.Box(root, "Grip", new Vector3(0, -0.15f, -0.05f), new Vector3(0.06f, 0.18f, 0.08f), dark, layer: Layers.Tool);
            GrayboxMaterials.Box(root, "Barrel", new Vector3(0, 0.03f, 0.4f), new Vector3(0.035f, 0.035f, 0.55f), dark, layer: Layers.Tool);
        }

        static void TowelShape(Transform root, Material mat, Material dark)
        {
            GrayboxMaterials.Box(root, "Cloth", new Vector3(0, -0.05f, 0.15f), new Vector3(0.3f, 0.03f, 0.4f), mat, layer: Layers.Tool);
        }

        // ---------------- GLB tools (docs/ART_IMPORT.md 3D Models: origin = grip, +Z forward) ----------------
        /// <summary>Tool id → .glb + first-person / third-person grip offsets from the head (the root is the grip now, the
        /// graybox root was the gun body ~0.15 m above it).</summary>
        static readonly Dictionary<string, (string file, Vector3 view, Vector3 third)> Glb = new Dictionary<string, (string, Vector3, Vector3)>
        {
            { "T01", ("T_pressure_washer_gun", new Vector3(0.24f, -0.34f, 0.42f), new Vector3(0.22f, -0.55f, 0.38f)) },
            { "T02", ("T_foam_cannon",         new Vector3(0.24f, -0.30f, 0.40f), new Vector3(0.22f, -0.52f, 0.36f)) },
            { "T03", ("T_towel",               new Vector3(0.22f, -0.30f, 0.42f), new Vector3(0.22f, -0.50f, 0.40f)) },
            { "T06", ("T_net_pole",            new Vector3(0.28f, -0.42f, 0.25f), new Vector3(0.25f, -0.60f, 0.25f)) },
        };

        /// <summary>The .glb as both the view model and the world model (same object, ToolBase poses it). Colliders: a convex hull
        /// (net pole: stick box + hoop box). Returns the Socket_Nozzle (or Socket_Capture) to aim from, null if no model.</summary>
        static Transform GlbShape(string toolId, GameObject root)
        {
            if (!Glb.TryGetValue(toolId, out var g)) return null;
            string path = GlbKit.Find("Tools", g.file + "_v2") ?? GlbKit.Find("Tools", g.file); // Blender v2 tools: same nodes / sockets / origin
            if (path == null) return null;
            var model = GlbKit.Model(path, root.transform, Layers.Tool);
            if (toolId == "T06")
            {
                bool Hoop(Transform t) => t.name == "Hoop" || t.name == "Netting";
                GlbKit.Box(root, GlbKit.LocalBounds(model, root.transform, t => !Hoop(t)));
                GlbKit.Box(root, GlbKit.LocalBounds(model, root.transform, Hoop));
            }
            else GlbKit.Convex(root, GlbKit.HullMesh(model, root.transform, "Tool_" + toolId + "_Hull"));
            var so = new SerializedObject(root.GetComponent<ToolBase>());
            so.FindProperty("_viewOffset").vector3Value = g.view;
            so.FindProperty("_thirdPersonOffset").vector3Value = g.third;
            so.ApplyModifiedPropertiesWithoutUndo();
            var capture = GlbKit.Node(model, "Socket_Capture");
            if (capture != null && root.GetComponent<NetPole>() != null) SceneBuilder.Set(root.GetComponent<NetPole>(), "_capture", capture);
            return GlbKit.Node(model, "Socket_Nozzle") ?? capture;
        }

        static GameObject BuildTool<T>(string toolId, string file, Color color, float mass, System.Action<Transform, Material, Material> shape) where T : ToolBase
        {
            var root = new GameObject(file) { layer = Layers.Tool };
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = mass; rb.interpolation = RigidbodyInterpolation.Interpolate;
            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>().Interpolate = true;
            root.AddComponent<NetworkRigidbody>();
            var tool = root.AddComponent<T>();
            SceneBuilder.Set(tool, "_data", DataBuilder.LoadRegistry());
            var so = new SerializedObject(tool);
            so.FindProperty("_toolId").stringValue = toolId;
            so.ApplyModifiedPropertiesWithoutUndo();
            var nozzle = GlbShape(toolId, root);
            if (nozzle == null)
            {
                shape(root.transform, GrayboxMaterials.Get("Tool_" + toolId, color), GrayboxMaterials.Get("ToolDark", new Color(0.2f, 0.2f, 0.22f)));
                nozzle = new GameObject("Socket_Nozzle").transform;
                nozzle.SetParent(root.transform, false);
                nozzle.localPosition = new Vector3(0, 0.03f, 0.68f);
            }
            SceneBuilder.Set(tool, "_nozzle", nozzle);
            // M4.6 D8: where the left hand holds the tool (the grip / right hand is the tool origin)
            var off = root.transform.Find("Model/Socket_OffHand");
            if (off == null)
            {
                off = new GameObject("Socket_OffHand").transform;
                off.SetParent(root.transform, false);
                off.localPosition = toolId switch
                {
                    "T01" => new Vector3(0f, 0.08f, 0.42f),
                    "T02" => new Vector3(0f, -0.05f, 0.18f),
                    "T03" => new Vector3(0.1f, 0.04f, 0.2f),
                    "T06" => new Vector3(0f, 0f, 0.24f),
                    _ => new Vector3(0f, 0.05f, 0.3f),
                };
            }
            SceneBuilder.Set(tool, "_offHand", off);
            return root;
        }

        struct VfxSpec
        {
            public string stream, mist, splash;
            public Color streamColor, mistColor, splashColor;
            public float streamSize, mistSize, splashSize, pitch;
            public bool sheet;
        }

        static Color WithA(Color c, float a) { c.a = a; return c; }

        static void SetupSpray(GameObject root, VfxSpec s, FoamPatch patch)
        {
            var vfx = new GameObject("SprayVfx").AddComponent<SprayVfx>();
            vfx.transform.SetParent(root.transform, false);
            SceneBuilder.Set(vfx, "_streamMaterial", ArtBuilder.Particle(s.stream));
            SceneBuilder.Set(vfx, "_mistMaterial", ArtBuilder.Particle(s.mist));
            SceneBuilder.Set(vfx, "_splashMaterial", ArtBuilder.Particle(s.splash));
            var vso = new SerializedObject(vfx);
            vso.FindProperty("_streamColor").colorValue = s.streamColor;
            vso.FindProperty("_mistColor").colorValue = s.mistColor;
            vso.FindProperty("_splashColor").colorValue = s.splashColor;
            vso.FindProperty("_streamSize").floatValue = s.streamSize;
            vso.FindProperty("_mistSize").floatValue = s.mistSize;
            vso.FindProperty("_splashSize").floatValue = s.splashSize;
            vso.FindProperty("_splashIsSheet").boolValue = s.sheet;
            vso.FindProperty("_pitch").floatValue = s.pitch;
            vso.ApplyModifiedPropertiesWithoutUndo();
            var tool = root.GetComponent<SprayTool>();
            SceneBuilder.Set(tool, "_vfx", vfx);
            if (patch != null) SceneBuilder.Set(tool, "_patchPrefab", patch);
        }

        static GameObject Save(GameObject root, string file) => PrefabBuilder.Save(root, PathOf(file));

        static void AddHose(GameObject root)
        {
            var line = root.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = 0.045f;
            line.numCapVertices = 2;
            line.sharedMaterial = GrayboxMaterials.Get("Hose", new Color(0.1f, 0.1f, 0.12f));
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var hose = root.AddComponent<HoseTether>();
            SceneBuilder.Set(hose, "_data", DataBuilder.LoadRegistry());
            SceneBuilder.Set(hose, "_line", line);
            SceneBuilder.Set(hose, "_reelMaterial", GrayboxMaterials.Get("HoseReel", new Color(0.85f, 0.2f, 0.2f)));
            var hoseIn = root.transform.Find("Model/Socket_HoseIn");
            if (hoseIn != null) SceneBuilder.Set(hose, "_hoseIn", hoseIn);
        }

        static FoamPatch BuildFoamPatch()
        {
            var root = new GameObject("FoamPatch") { layer = Layers.FoamArea };
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true; box.size = new Vector3(1.2f, 0.6f, 1.2f); box.center = new Vector3(0, 0.3f, 0);
            // fx_foam_decal via URP DecalProjector (projects straight down onto the floor)
            var visual = new GameObject("FoamDecal").transform;
            visual.SetParent(root.transform, false);
            visual.localPosition = new Vector3(0, 0.3f, 0);
            visual.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var decal = visual.gameObject.AddComponent<UnityEngine.Rendering.Universal.DecalProjector>();
            decal.material = ArtBuilder.FoamDecal();
            decal.size = new Vector3(1.4f, 1.4f, 0.8f);
            decal.pivot = Vector3.zero;
            root.AddComponent<NetworkObject>();
            var p = root.AddComponent<FoamPatch>();
            SceneBuilder.Set(p, "_visual", visual);
            SceneBuilder.Set(p, "_decal", decal);
            return PrefabBuilder.Save(root, FoamPatchPath).GetComponent<FoamPatch>();
        }
    }
}
