using System.Collections.Generic;
using System.Linq;
using CarWash.Core;
using CarWash.Net;
using CarWash.Vehicles;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CarWash.Editor
{
    /// <summary>
    /// Graybox "Sudsy Lot" (GDD §11). x = west(-)→east(+), z = south(-)→north(+).
    /// Flow: road (z=-15.5, west→east) → queue lane x=-20 (4 slots, head north) → feed lane z=12 → bay (enter north,
    /// stop facing south) → exit lane z=-5 → road → despawn east. Office/lockers south-west, alley north behind the fence.
    /// </summary>
    public static class LotBuilder
    {
        public static readonly Vector3[] BayCenters = { new Vector3(-8f, 0, 4f), new Vector3(0f, 0, 4f), new Vector3(8f, 0, 4f) };
        public const float RoadZ = -15.5f, QueueX = -20f, FeedZ = 12f, ExitZ = -5f;
        public static readonly float[] QueueSlotZ = { 6f, 0.5f, -5f, -10.5f };
        public static readonly Vector3 OfficeCenter = new Vector3(-12f, 0, -10f);
        public static readonly Vector3 LockerArea = new Vector3(-3.5f, 0, -11f);

        [MenuItem("CarWash/Build/Scene SudsyLot")]
        public static void BuildSudsyLot()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var env = new GameObject("Environment").transform;
            var M = new Mats();

            GrayboxMaterials.Box(env, "Ground", new Vector3(0, -0.5f, -1f), new Vector3(76, 1, 42), M.asphalt); // wide enough for the road spawn/despawn points
            GrayboxMaterials.Box(env, "Road", new Vector3(0, 0.005f, RoadZ), new Vector3(70, 0.01f, 5), M.road, collider: false);
            GrayboxMaterials.Box(env, "RoadLine", new Vector3(0, 0.012f, RoadZ), new Vector3(70, 0.01f, 0.15f), M.line, collider: false);
            Lane(env, M, "QueueLane", new Vector3(QueueX, 0, -2.5f), new Vector3(3.2f, 0.01f, 24f));
            Lane(env, M, "FeedLane", new Vector3(-6f, 0, FeedZ), new Vector3(30f, 0.01f, 3.2f));
            Lane(env, M, "ExitLane", new Vector3(6f, 0, ExitZ), new Vector3(30f, 0.01f, 3.2f));
            Lane(env, M, "ExitRamp", new Vector3(20f, 0, -10.5f), new Vector3(3.2f, 0.01f, 11f));
            for (int i = 0; i < QueueSlotZ.Length; i++)
                GrayboxMaterials.Box(env, "QueueMark" + i, new Vector3(QueueX, 0.014f, QueueSlotZ[i] + 2.4f), new Vector3(3f, 0.01f, 0.15f), M.line, collider: false);

            var bays = new Bay[BayCenters.Length];
            var bayRoot = new GameObject("Bays").transform; bayRoot.SetParent(env);
            for (int i = 0; i < BayCenters.Length; i++) bays[i] = BuildBay(bayRoot, i, M);

            // Office (terminal, day board, cage), lockers, vacuum station, alley
            GrayboxMaterials.Box(env, "Office", OfficeCenter + new Vector3(0, 1.5f, 0), new Vector3(8, 3, 6), M.office);
            GrayboxMaterials.Box(env, "ShopTerminal", OfficeCenter + new Vector3(2.5f, 0.6f, 3.3f), new Vector3(0.8f, 1.2f, 0.5f), M.panel);
            GrayboxMaterials.Box(env, "DayBoard", OfficeCenter + new Vector3(-1.5f, 1.6f, 3.05f), new Vector3(2.4f, 1.2f, 0.1f), M.board);
            for (int i = 0; i < 4; i++)
            {
                var locker = GrayboxMaterials.Box(env, "Locker" + i, LockerArea + new Vector3(-1.5f + i, 1f, -2.6f), new Vector3(0.8f, 2f, 0.5f), M.wall);
                // M4.9 G3: [E] opens the wardrobe (a trigger on the Interactable layer in front of the door; the box stays solid)
                var face = new GameObject("WardrobeDoor") { layer = Layers.Interactable };
                face.transform.SetParent(locker.transform, false);
                face.transform.localPosition = new Vector3(0f, 0f, 0.6f); face.transform.localScale = new Vector3(1f, 1f, 0.2f);
                var trig = face.AddComponent<BoxCollider>(); trig.isTrigger = true;
                face.AddComponent<Player.WardrobeLocker>();
                GrayboxMaterials.Box(locker.transform, "Handle", new Vector3(0.32f, 0f, 0.52f), new Vector3(0.05f, 0.12f, 0.06f), GrayboxMaterials.Get("Panel", new Color(0.25f, 0.3f, 0.4f)), collider: false);
            }
            VacuumStation(env, M.panel);
            BuildCage(env, M);
            GrayboxMaterials.Box(env, "FenceN", new Vector3(0, 1f, 18.5f), new Vector3(56, 2f, 0.2f), M.brick);
            GrayboxMaterials.Box(env, "Dumpster", new Vector3(16f, 0.75f, 15.5f), new Vector3(3f, 1.5f, 1.8f), M.prop);
            GrayboxMaterials.Box(env, "Vent", new Vector3(-12f, 0.6f, 17.8f), new Vector3(1.2f, 1.2f, 1f), M.panel);
            GrayboxMaterials.Box(env, "MudPuddle1", new Vector3(0f, 0.008f, 15.2f), new Vector3(2.5f, 0.01f, 1.8f), M.mud, collider: false);
            GrayboxMaterials.Box(env, "MudPuddle2", new Vector3(6f, 0.008f, 16.2f), new Vector3(1.8f, 0.01f, 1.4f), M.mud, collider: false);

            // Dev yard (test content outside the customer flow): foam floor zone + marker
            var dev = new GameObject("DevYard", typeof(DevOnlyObject)).transform; dev.SetParent(env); // P7: removed in release
            var foam = GrayboxMaterials.Box(dev, "FoamTrigger_Test", DevYard.FoamZone + new Vector3(0, 0.5f, 0), new Vector3(5.6f, 1f, 8f), M.foam, layer: Layers.FoamArea);
            Object.DestroyImmediate(foam.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(foam.GetComponent<MeshFilter>());
            foam.GetComponent<BoxCollider>().isTrigger = true;
            GrayboxMaterials.Box(dev, "FoamVisual_Test", DevYard.FoamZone + new Vector3(0, 0.03f, 0), new Vector3(5.6f, 0.02f, 8f), M.foam, collider: false);
            GrayboxMaterials.Box(dev, "DevYardPad", DevYard.Origin + new Vector3(0, 0.004f, 2f), new Vector3(14f, 0.01f, 26f), M.laneMat, collider: false);

            var sun = new GameObject("Sun", typeof(Light)).GetComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.2f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var cam = new GameObject("OverviewCamera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            cam.transform.SetPositionAndRotation(new Vector3(0, 30f, -34f), Quaternion.Euler(45f, 0, 0));

            EnvironmentBuilder.Build(env, bays); // P6: sky, skyline, post, signs, physic materials
            SceneryBuilder.Build(env);            // map completeness: city around the lot + invisible play-area walls
            EnvDressingBuilder.Build(env);        // M4.7 E2/E3: decals + set dressing (combined meshes, off every route)
            EnvironmentBuilder.DayNight(env);    // M4.9 G1/G2: local day → night (sky, sun, fog, post, lamps / signs glow)
            var lotPaths = BuildPaths(bays);
            BuildCustomers(lotPaths);
            BuildSpawner();

            var spawns = new SpawnListWriter();
            spawns.Add(PrefabBuilder.LotStatePath, Vector3.zero, Vector3.zero);
            spawns.Add(DayBuilder.DirectorPath, Vector3.zero, Vector3.zero);
            foreach (var b in bays) spawns.Add(DayBuilder.BayButtonPath, b.Panel.position + new Vector3(-0.1f, 0f, 0f), Vector3.zero); // DONE on the inner face of each bay panel
            foreach (var b in bays) spawns.Add(DayBuilder.BayLightSwitchPath, b.Panel.position + new Vector3(-0.08f, 0.15f, 0.7f), Vector3.zero); // M4.9 G7: light switch next to it
            spawns.Add(DayBuilder.DayBoardPath, OfficeCenter + new Vector3(-1.5f, 1.6f, 3.05f), Vector3.zero);
            PropBuilder.PlaceInLot(spawns);
            spawns.Add(ToolBuilder.PathOf("T01_PressureWasher"), bays[0].HoseReel.position + new Vector3(0.25f, 0f, 0f), Vector3.zero);
            // DESIGN-GAP: GDD U05 says W1 Day1 has no foam cannon; until the shop (T5.1) it lies by the office
            spawns.Add(ToolBuilder.PathOf("T03_Towel"), OfficeCenter + new Vector3(4.6f, 1.0f, 2f), Vector3.zero);
            // dev yard test content (P7: dev-only, not spawned in release builds)
            spawns.DevOnly = true;
            spawns.Add(ToolBuilder.PathOf("T01_PressureWasher"), DevYard.WasherReel, Vector3.zero);
            spawns.Add(ToolBuilder.PathOf("T02_FoamCannon"), DevYard.FoamCannon, Vector3.zero);
            spawns.Add(ToolBuilder.PathOf("T03_Towel"), DevYard.Towel, Vector3.zero);
            spawns.Add(VehicleBuilder.TestCarPath("V02"), DevYard.CarV02 + new Vector3(0, 0.02f, 0), new Vector3(0, 180, 0));
            spawns.Add(VehicleBuilder.TestCarPath("V04"), DevYard.CarV04 + new Vector3(0, 0.02f, 0), new Vector3(0, 180, 0));
            // T3.1: net pole by the Animal Control cage; dev-yard critters (generic behaviour) + a second net
            spawns.DevOnly = false; // the cage net pole is real game content
            spawns.Add(ToolBuilder.PathOf("T06_NetPole"), CagePos + new Vector3(0f, 1f, -2f), Vector3.zero);
            spawns.DevOnly = true;
            spawns.Add(ToolBuilder.PathOf("T06_NetPole"), DevYard.NetPole, Vector3.zero);
            spawns.Add(CritterBuilder.PathOf("C01_Raccoon"), DevYard.Raccoon, Vector3.zero);
            spawns.Add(CritterBuilder.PathOf("C05_Possum"), DevYard.Possum, Vector3.zero);
            spawns.Add(CritterBuilder.PathOf("C03_MudSlime"), DevYard.Slime, Vector3.zero);
            spawns.Add(CritterBuilder.PathOf("C02_BeeSwarm"), DevYard.Bees, Vector3.zero);
            spawns.WriteTo(new GameObject("NetworkSpawns"));

            new GameObject("HUD").AddComponent<UI.Hud>();
            LotNavMesh.Bake(env.gameObject, scene);
            SceneBuilder.Save(scene, SceneNames.SudsyLot);
        }

        /// <summary>Animal Control cage (GDD §11 office area): between the office and the lockers, clear of the exit lane.</summary>
        public static readonly Vector3 CagePos = new Vector3(-6.6f, 0f, -10.2f);

        /// <summary>
        /// Vacuum station by the exit lane. GLB batch: T_shop_vac.glb stands on a low pedestal (DESIGN-GAP: T04 청소기 has no
        /// holdable tool code yet — interior trash is picked up by hand — so the model dresses the station instead of a view model).
        /// </summary>
        static void VacuumStation(Transform env, Material panel)
        {
            string glb = GlbKit.Find("Tools", "T_shop_vac");
            if (glb == null) { GrayboxMaterials.Box(env, "VacuumStation", new Vector3(14f, 0.75f, -10f), new Vector3(1f, 1.5f, 1f), panel); return; }
            var station = GrayboxMaterials.Box(env, "VacuumStation", new Vector3(14f, 0.15f, -10f), new Vector3(1.4f, 0.3f, 1.6f), panel);
            var vac = new GameObject("ShopVac");
            vac.transform.SetParent(station.transform.parent, false);
            vac.transform.localPosition = new Vector3(14f, 0.3f, -10f);
            vac.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            var model = GlbKit.Model(glb, vac.transform, Layers.Default, Vector3.zero, 1.6f);
            GlbKit.Box(vac, GlbKit.LocalBounds(model, vac.transform));
        }

        static void BuildCage(Transform env, Mats M)
        {
            var cage = new GameObject("AnimalCage").transform;
            cage.SetParent(env); cage.position = CagePos;
            var bars = GrayboxMaterials.Get("CageBars", new Color(0.55f, 0.6f, 0.65f), 0.6f);
            GrayboxMaterials.Box(cage, "Floor", new Vector3(0, 0.03f, 0), new Vector3(2.2f, 0.06f, 2.2f), M.panel);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    GrayboxMaterials.Box(cage, $"Post{x}{z}", new Vector3(x * 1.05f, 1.0f, z * 1.05f), new Vector3(0.08f, 2f, 0.08f), bars);
            GrayboxMaterials.Box(cage, "Roof", new Vector3(0, 2.02f, 0), new Vector3(2.2f, 0.05f, 2.2f), bars);
            for (int i = -3; i <= 3; i++) // thin bars on the west/east/south sides, north (lot side) left open as the door
            {
                GrayboxMaterials.Box(cage, "BarW" + i, new Vector3(-1.05f, 1f, i * 0.3f), new Vector3(0.03f, 2f, 0.03f), bars, collider: false);
                GrayboxMaterials.Box(cage, "BarE" + i, new Vector3(1.05f, 1f, i * 0.3f), new Vector3(0.03f, 2f, 0.03f), bars, collider: false);
                GrayboxMaterials.Box(cage, "BarS" + i, new Vector3(i * 0.3f, 1f, -1.05f), new Vector3(0.03f, 2f, 0.03f), bars, collider: false);
            }
            GrayboxMaterials.Box(cage, "Sign", new Vector3(0, 2.35f, 1.0f), new Vector3(1.6f, 0.4f, 0.05f), GrayboxMaterials.Get("CageSign", new Color(0.95f, 0.75f, 0.2f)), collider: false);
            cage.gameObject.AddComponent<Critters.AnimalCage>();
        }

        static void Lane(Transform env, Mats m, string name, Vector3 c, Vector3 size) =>
            GrayboxMaterials.Box(env, name, c + new Vector3(0, 0.006f, 0), size, m.laneMat, collider: false);

        static Bay BuildBay(Transform parent, int i, Mats M)
        {
            var b = new GameObject("Bay" + (i + 1)).transform;
            b.SetParent(parent); b.localPosition = BayCenters[i];
            GrayboxMaterials.Box(b, "Floor", new Vector3(0, 0.01f, 0), new Vector3(6, 0.02f, 10), M.concrete); // P6: collider → slippery wet-concrete physic material
            GrayboxMaterials.Box(b, "WallW", new Vector3(-3.1f, 1.5f, 0), new Vector3(0.2f, 3f, 10), M.wall);
            GrayboxMaterials.Box(b, "WallE", new Vector3(3.1f, 1.5f, 0), new Vector3(0.2f, 3f, 10), M.wall);
            GrayboxMaterials.Box(b, "Roof", new Vector3(0, 3.6f, 0), new Vector3(6.6f, 0.2f, 10.4f), M.roof);
            GrayboxMaterials.Box(b, "DrainGrate", new Vector3(0, 0.025f, 0), new Vector3(1.2f, 0.01f, 0.4f), M.road, collider: false);
            // GLB batch (DESIGN-GAP): reel moved from the front corner (z -4.2) to the middle of the west wall — with the 8 m GDD hose
            // the far rear corner of a 4.6-5.2 m GLB car (bay z +3) was out of reach; from mid-wall every point in the bay is within 7.6 m
            var reel = new GameObject("Socket_HoseReel").transform; reel.SetParent(b, false); reel.localPosition = new Vector3(-2.75f, 1.0f, 0f);
            var panel = GrayboxMaterials.Box(b, "ButtonPanel", new Vector3(2.95f, 1.3f, -4.2f), new Vector3(0.1f, 0.6f, 0.4f), M.panel).transform;
            var shutters = new Transform[2];
            shutters[0] = GrayboxMaterials.Box(b, "ShutterN", new Vector3(0, 1.5f, 5.05f), new Vector3(6f, 3f, 0.12f), M.shutter).transform;
            shutters[1] = GrayboxMaterials.Box(b, "ShutterS", new Vector3(0, 1.5f, -5.05f), new Vector3(6f, 3f, 0.12f), M.shutter).transform;
            Transform P(string n, Vector3 local) { var t = new GameObject(n).transform; t.SetParent(b, false); t.localPosition = local; return t; }
            var bay = b.gameObject.AddComponent<Bay>();
            var so = new SerializedObject(bay);
            so.FindProperty("_index").intValue = i;
            so.FindProperty("_entry").objectReferenceValue = P("Entry", new Vector3(0, 0, 5.5f));
            so.FindProperty("_stop").objectReferenceValue = P("Stop", Vector3.zero);
            so.FindProperty("_exit").objectReferenceValue = P("Exit", new Vector3(0, 0, -5.5f));
            so.FindProperty("_hoseReel").objectReferenceValue = reel;
            so.FindProperty("_panel").objectReferenceValue = panel;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabBuilder.SetArray(bay, "_shutters", shutters);
            BayFixtures(b, i); // M4.9 G7
            return bay;
        }

        /// <summary>M4.9 G7: two LED bars under the bay roof + a shadowless spot under each (switched by the bay's wall switch).</summary>
        static void BayFixtures(Transform b, int i)
        {
            var off = GrayboxMaterials.Get("BayLED_Off", new Color(0.55f, 0.57f, 0.6f), 0.6f);
            var on = GrayboxMaterials.Get("BayLED_On", new Color(0.95f, 0.97f, 1f), 0.6f);
            on.EnableKeyword("_EMISSION"); on.SetColor("_EmissionColor", new Color(1f, 0.98f, 0.94f) * 4f); on.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(on);
            var root = new GameObject("BayLights").transform; root.SetParent(b, false);
            var lights = new List<Light>(); var bars = new List<Renderer>();
            foreach (float x in new[] { -1.45f, 1.45f })
            {
                GrayboxMaterials.Box(root, "LedHousing", new Vector3(x, 3.47f, 0f), new Vector3(0.22f, 0.06f, 4.6f), GrayboxMaterials.Get("Panel", new Color(0.25f, 0.3f, 0.4f)), collider: false);
                bars.Add(GrayboxMaterials.Box(root, "LedBar", new Vector3(x, 3.435f, 0f), new Vector3(0.14f, 0.02f, 4.4f), off, collider: false).GetComponent<Renderer>());
                var lg = new GameObject("BayLight") { layer = b.gameObject.layer }; lg.transform.SetParent(root, false);
                lg.transform.localPosition = new Vector3(x, 3.38f, 0f); lg.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Spot; l.range = 8f; l.spotAngle = 140f; l.innerSpotAngle = 80f; l.intensity = 7f; l.color = new Color(1f, 0.98f, 0.94f);
                l.shadows = LightShadows.None; l.enabled = false;
                lights.Add(l);
            }
            foreach (var r in bars) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var bl = root.gameObject.AddComponent<Visual.BayLights>();
            var so = new SerializedObject(bl);
            so.FindProperty("_bay").intValue = i;
            so.FindProperty("_onMat").objectReferenceValue = on;
            so.FindProperty("_offMat").objectReferenceValue = off;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabBuilder.SetArray(bl, "_lights", lights.Cast<Object>().ToArray());
            PrefabBuilder.SetArray(bl, "_bars", bars.Cast<Object>().ToArray());
        }

        static LotPaths BuildPaths(Bay[] bays)
        {
            var root = new GameObject("LotPaths").transform;
            Transform[] Points(string group, params Vector3[] ps)
            {
                var g = new GameObject(group).transform; g.SetParent(root);
                var arr = new Transform[ps.Length];
                for (int i = 0; i < ps.Length; i++) { arr[i] = new GameObject($"{group}_{i}").transform; arr[i].SetParent(g); arr[i].position = ps[i]; }
                return arr;
            }
            var paths = root.gameObject.AddComponent<LotPaths>();
            PrefabBuilder.SetArray(paths, "_arrival", Points("Arrival", new Vector3(-34f, 0, RoadZ), new Vector3(QueueX, 0, RoadZ), new Vector3(QueueX, 0, -13f)));
            var slots = new Vector3[QueueSlotZ.Length];
            for (int i = 0; i < slots.Length; i++) slots[i] = new Vector3(QueueX, 0, QueueSlotZ[i]);
            PrefabBuilder.SetArray(paths, "_queueSlots", Points("QueueSlot", slots));
            PrefabBuilder.SetArray(paths, "_feedLane", Points("Feed", new Vector3(QueueX, 0, FeedZ), new Vector3(-8f, 0, FeedZ), new Vector3(0f, 0, FeedZ), new Vector3(8f, 0, FeedZ)));
            PrefabBuilder.SetArray(paths, "_exitLane", Points("Exit", new Vector3(-8f, 0, ExitZ), new Vector3(0f, 0, ExitZ), new Vector3(8f, 0, ExitZ),
                new Vector3(20f, 0, ExitZ), new Vector3(20f, 0, RoadZ), new Vector3(36f, 0, RoadZ)));
            PrefabBuilder.SetArray(paths, "_bays", bays);
            // map polish: customers appear / vanish on the far road behind the city blocks (SceneryBuilder draws the same roads)
            PrefabBuilder.SetArray(paths, "_approach", Points("Approach", SceneryBuilder.Approach));
            PrefabBuilder.SetArray(paths, "_depart", Points("Depart", SceneryBuilder.Depart));
            PrefabBuilder.SetArray(paths, "_leaveWest", Points("LeaveWest", SceneryBuilder.LeaveWest));
            return paths;
        }

        static void BuildCustomers(LotPaths paths)
        {
            var go = new GameObject("Customers");
            var spawner = go.AddComponent<VehicleSpawner>();
            SceneBuilder.Set(spawner, "_data", DataBuilder.LoadRegistry());
            PrefabBuilder.SetArray(spawner, "_vehiclePrefabs", System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Cast<Object>(VehicleBuilder.VehiclePrefabs())));
            PrefabBuilder.SetArray(spawner, "_trashPrefabs", System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Cast<Object>(PropBuilder.TrashPrefabs())));
            var critters = go.AddComponent<Critters.CritterSpawner>();
            PrefabBuilder.SetArray(critters, "_prefabs", new[] { "C01_Raccoon", "C02_BeeSwarm", "C03_MudSlime", "C05_Possum" }
                .Select(f => (Object)AssetDatabase.LoadAssetAtPath<GameObject>(CritterBuilder.PathOf(f)).GetComponent<Unity.Netcode.NetworkObject>()).ToArray());
            var lane = go.AddComponent<QueueLane>();
            SceneBuilder.Set(lane, "_data", DataBuilder.LoadRegistry());
            SceneBuilder.Set(lane, "_paths", paths);
            SceneBuilder.Set(lane, "_spawner", spawner);
        }

        static void BuildSpawner()
        {
            var spawner = new GameObject("PlayerSpawner").AddComponent<PlayerSpawner>();
            var points = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                points[i] = new GameObject("Spawn" + i).transform;
                points[i].SetParent(spawner.transform);
                points[i].position = LockerArea + new Vector3(-1.5f + i, 0.05f, -0.5f);
                points[i].rotation = Quaternion.Euler(0, 0f, 0);
            }
            SceneBuilder.Set(spawner, "_playerPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(PrefabBuilder.PlayerPath).GetComponent<NetworkObject>());
            PrefabBuilder.SetArray(spawner, "_spawnPoints", points);
        }

        class Mats
        {
            public Material asphalt = GrayboxMaterials.Textured("Asphalt", new Color(0.8f, 0.8f, 0.82f), "tex_asphalt.png", 4f);
            public Material concrete = GrayboxMaterials.Textured("Concrete", Color.white, "tex_concrete_wet.png", 3f, 0.55f);
            public Material road = GrayboxMaterials.Textured("Road", new Color(0.5f, 0.5f, 0.53f), "tex_asphalt.png", 4f);
            public Material laneMat = GrayboxMaterials.Textured("Lane", new Color(0.65f, 0.66f, 0.7f), "tex_asphalt.png", 4f);
            public Material line = GrayboxMaterials.Get("PaintLine", new Color(0.95f, 0.85f, 0.25f));
            public Material wall = GrayboxMaterials.Textured("BayWall", Color.white, "tex_metal_blue.png", 2f, 0.45f);
            public Material brick = GrayboxMaterials.Textured("Brick", Color.white, "tex_brick.png", 2f);
            public Material roof = GrayboxMaterials.Get("BayRoof", new Color(0.85f, 0.3f, 0.3f));
            public Material shutter = GrayboxMaterials.Pbr("Shutter", new Color(0.85f, 0.86f, 0.88f), "painted_metal_shutter", 3f); // M4.7 A1 (flat grey if not delivered)
            public Material office = GrayboxMaterials.Textured("Office", Color.white, "tex_brick.png", 2f);
            public Material panel = GrayboxMaterials.Get("Panel", new Color(0.25f, 0.3f, 0.4f));
            public Material board = GrayboxMaterials.Get("Board", new Color(0.15f, 0.35f, 0.2f));
            public Material cage = GrayboxMaterials.Get("Cage", new Color(0.6f, 0.6f, 0.2f));
            public Material foam = GrayboxMaterials.Get("Foam", new Color(0.97f, 0.97f, 1f), 0.6f);
            public Material prop = GrayboxMaterials.Get("Prop", new Color(0.4f, 0.6f, 0.4f));
            public Material mud = GrayboxMaterials.Get("MudPuddle", new Color(0.36f, 0.25f, 0.15f), 0.7f);
        }
    }
}
