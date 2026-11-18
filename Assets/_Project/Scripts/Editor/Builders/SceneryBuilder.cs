using System.Collections.Generic;
using System.IO;
using System.Linq;
using CarWash.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CarWash.Editor
{
    /// <summary>
    /// Map completeness (after the P9 player characters): nothing empty under the sky from any spot a player can stand.
    ///  - Outer ground around the lot (four slabs flush with the lot ground, no z-fighting), roads continuing both ways and
    ///    bending away behind the blocks (the customers' approach / exit / angry-leave routes drive on them, so cars appear and
    ///    vanish out of sight), sidewalks, a parking lot and a little park.
    ///  - City blocks filling every cell between the lot and the skyline ring (r 110): near blocks lit + textured (brick / blue
    ///    metal), far blocks unlit flat colours without shadows (cheap; they melt into the fog like the skyline).
    ///  - Fences / railings along the play-area edge, trees, street lamps, utility poles with sagging wires, parked GLB cars,
    ///    billboards. All scenery is on the Scenery layer with NO colliders and batching-static.
    ///  - PlayArea walls: 4 thick invisible boxes + a ceiling on PlayBounds (players, props, tools, critters, ragdolls stop;
    ///    vehicles pass the road openings — layer matrix in ProjectConfigurator).
    /// </summary>
    public static class SceneryBuilder
    {
        public const string MatDir = "Assets/_Project/Art/Materials/Scenery";
        static System.Random _rng;
        static float Rand(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

        // --------- routes the customers drive (LotBuilder.BuildPaths uses the same points) ---------
        public static readonly Vector3[] Approach =
        {
            new Vector3(-78f, 0, -84f), new Vector3(-77f, 0, -62f), new Vector3(-74f, 0, -46f), new Vector3(-68f, 0, -33f),
            new Vector3(-60f, 0, -24f), new Vector3(-52f, 0, -18f), new Vector3(-44f, 0, LotBuilder.RoadZ),
        };
        public static readonly Vector3[] LeaveWest =
        {
            new Vector3(-46f, 0, LotBuilder.RoadZ + Vehicles.LotPaths.LeaveLane), new Vector3(-56f, 0, -11f), new Vector3(-64f, 0, -4f),
            new Vector3(-70f, 0, 6f), new Vector3(-73f, 0, 20f), new Vector3(-75f, 0, 40f), new Vector3(-76f, 0, 64f),
        };
        public static readonly Vector3[] Depart =
        {
            new Vector3(44f, 0, LotBuilder.RoadZ), new Vector3(52f, 0, -18f), new Vector3(60f, 0, -24f), new Vector3(68f, 0, -33f),
            new Vector3(74f, 0, -46f), new Vector3(77f, 0, -62f), new Vector3(78f, 0, -84f),
        };
        // visual-only mirror of the west north branch on the east side
        static readonly Vector3[] EastNorth = LeaveWest.Select(p => new Vector3(-p.x, 0, p.z)).ToArray();

        static readonly Rect ParkingLot = new Rect(-52f, -9f, 18f, 24f);   // x -52..-34, z -9..15 (west, between wall and branch)
        static readonly Rect Park = new Rect(42f, -8f, 14f, 24f);           // x 42..56 (east)

        public static void Build(Transform env)
        {
            _rng = new System.Random(20261118);
            var root = new GameObject("Scenery") { layer = Layers.Scenery }.transform;
            root.SetParent(env, false);
            OuterGround(root);
            Roads(root);
            Blocks(root);
            Fences(root);
            Trees(root);
            Lamps(root);
            UtilityPoles(root);
            ParkedCars(root);
            Billboards(root);
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = Layers.Scenery;
                foreach (var c in t.GetComponents<Collider>()) Object.DestroyImmediate(c);
                if (t.GetComponent<TextMesh>() == null) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic); // text meshes stay dynamic
            }
            Bounds(env);
        }

        // ------------------------------------------------------------------ materials
        static Material Lit(string name, Color c, float smooth = 0.15f) => GrayboxMaterials.Get("Scenery_" + name, c, smooth);

        /// <summary>Chain-link: lit, alpha-blended grey (reads as mesh, you see the city through it).</summary>
        static Material SeeThrough(string name, Color c)
        {
            Directory.CreateDirectory(MatDir);
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", c); m.color = c;
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f); m.SetFloat("_ZWrite", 0f); m.SetFloat("_Cull", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetOverrideTag("RenderType", "Transparent"); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Unlit(string name, Color c)
        {
            Directory.CreateDirectory(MatDir);
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", c); m.color = c;
            EditorUtility.SetDirty(m);
            return m;
        }

        static GameObject Box(Transform p, string n, Vector3 c, Vector3 s, Material m, float yaw = 0f, bool shadows = true)
        {
            var go = GrayboxMaterials.Box(p, n, c, s, m, collider: false, layer: Layers.Scenery);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var r = go.GetComponent<MeshRenderer>();
            if (!shadows) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
            return go;
        }

        // ------------------------------------------------------------------ ground + roads
        static void OuterGround(Transform root)
        {
            // flush with the lot's Ground box (x -38..38, z -22..20, top y 0) → four slabs around it, no overlap
            var g = GrayboxMaterials.Textured("Scenery_Ground", new Color(0.62f, 0.64f, 0.6f), "tex_concrete_wet.png", 6f, 0.1f);
            const float R = 330f;
            Box(root, "GroundN", new Vector3(0, -0.5f, (20f + R) / 2f), new Vector3(2 * R, 1f, R - 20f), g);
            Box(root, "GroundS", new Vector3(0, -0.5f, (-22f - R) / 2f), new Vector3(2 * R, 1f, R - 22f), g);
            Box(root, "GroundW", new Vector3((-38f - R) / 2f, -0.5f, -1f), new Vector3(R - 38f, 1f, 42f), g);
            Box(root, "GroundE", new Vector3((38f + R) / 2f, -0.5f, -1f), new Vector3(R - 38f, 1f, 42f), g);
            // sidewalk south of the road (inside the play area) + curb line
            var walk = GrayboxMaterials.Textured("Sidewalk", new Color(1.05f, 1.05f, 1.02f), "tex_concrete_wet.png", 3f); // M4.7 E1: stochastic concrete
            Box(root, "SidewalkS", new Vector3(0f, 0.012f, -20.6f), new Vector3(86f, 0.02f, 2.6f), walk); // flat: players walk on the lot ground under it
            // parking lot + park surfaces
            var lotM = GrayboxMaterials.Textured("ParkingAsphalt", new Color(0.7f, 0.72f, 0.75f), "tex_asphalt.png", 4f); // M4.7 E1
            Box(root, "ParkingLot", new Vector3(ParkingLot.center.x, 0.01f, ParkingLot.center.y), new Vector3(ParkingLot.width, 0.02f, ParkingLot.height), lotM);
            var line = Lit("ParkingLine", new Color(0.92f, 0.92f, 0.9f));
            for (int i = 0; i < 7; i++)
            {
                Box(root, "PLine" + i, new Vector3(ParkingLot.xMin + 1.5f + i * 2.6f, 0.025f, ParkingLot.yMin + 3f), new Vector3(0.12f, 0.01f, 5f), line);
                Box(root, "PLineB" + i, new Vector3(ParkingLot.xMin + 1.5f + i * 2.6f, 0.025f, ParkingLot.yMax - 3f), new Vector3(0.12f, 0.01f, 5f), line);
            }
            var gt = GroundTextures.Grass(); // M4.7 E1: generated tileable grass on the stochastic ground shader
            var grass = GrayboxMaterials.Ground("Grass", Color.white, gt.albedo, gt.normal, gt.mask, 2.5f, 0.2f);
            Box(root, "ParkGrass", new Vector3(Park.center.x, 0.02f, Park.center.y), new Vector3(Park.width, 0.04f, Park.height), grass);
        }

        static Material RoadMat => GrayboxMaterials.Textured("Road", new Color(0.5f, 0.5f, 0.53f), "tex_asphalt.png", 4f);

        static void Roads(Transform root)
        {
            var road = RoadMat; var line = GrayboxMaterials.Get("PaintLine", new Color(0.95f, 0.85f, 0.25f));
            float z = LotBuilder.RoadZ;
            // the lot's own road is x -35..35; continue it straight to both junctions
            Box(root, "RoadW", new Vector3(-41f, 0.005f, z), new Vector3(12f, 0.01f, 5f), road);
            Box(root, "RoadE", new Vector3(41f, 0.005f, z), new Vector3(12f, 0.01f, 5f), road);
            Box(root, "RoadWLine", new Vector3(-41f, 0.012f, z), new Vector3(12f, 0.01f, 0.15f), line);
            Box(root, "RoadELine", new Vector3(41f, 0.012f, z), new Vector3(12f, 0.01f, 0.15f), line);
            Polyline(root, "ApproachRoad", Approach, road, line, 7f);
            Polyline(root, "LeaveWestRoad", new[] { new Vector3(-46f, 0, z) }.Concat(LeaveWest.Skip(1)).ToArray(), road, line, 7f);
            Polyline(root, "DepartRoad", Depart, road, line, 7f);
            Polyline(root, "EastNorthRoad", new[] { new Vector3(46f, 0, z) }.Concat(EastNorth.Skip(1)).ToArray(), road, line, 7f);
            // far ends keep going (into the fog) so no road ever just stops in view
            Polyline(root, "ApproachFar", new[] { Approach[0], Approach[0] + new Vector3(-2f, 0, -60f) }, road, line, 7f);
            Polyline(root, "DepartFar", new[] { Depart[Depart.Length - 1], Depart[Depart.Length - 1] + new Vector3(2f, 0, -60f) }, road, line, 7f);
            Polyline(root, "WestFar", new[] { LeaveWest[LeaveWest.Length - 1], LeaveWest[LeaveWest.Length - 1] + new Vector3(-2f, 0, 60f) }, road, line, 7f);
            Polyline(root, "EastFar", new[] { EastNorth[EastNorth.Length - 1], EastNorth[EastNorth.Length - 1] + new Vector3(2f, 0, 60f) }, road, line, 7f);
        }

        static void Polyline(Transform root, string name, Vector3[] pts, Material road, Material line, float width)
        {
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                var a = pts[i]; var b = pts[i + 1];
                var d = b - a; float len = d.magnitude + width * 0.5f; // overlap at the joints (no gaps on the bends)
                float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                var mid = (a + b) * 0.5f;
                Box(root, $"{name}{i}", new Vector3(mid.x, 0.004f + i * 0.0004f, mid.z), new Vector3(width, 0.01f, len), road, yaw);
                Box(root, $"{name}Line{i}", new Vector3(mid.x, 0.013f + i * 0.0004f, mid.z), new Vector3(0.15f, 0.01f, d.magnitude), line, yaw);
            }
        }

        static float DistToPolyline(Vector3 p, Vector3[] pts)
        {
            float best = float.MaxValue; p.y = 0;
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                var a = pts[i]; var ab = pts[i + 1] - a;
                float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                best = Mathf.Min(best, (a + ab * t - p).magnitude);
            }
            return best;
        }

        static IEnumerable<Vector3[]> AllRoads()
        {
            float z = LotBuilder.RoadZ;
            yield return new[] { new Vector3(-46f, 0, z), new Vector3(46f, 0, z) };
            yield return Approach.Prepend(Approach[0] + new Vector3(-2f, 0, -60f)).ToArray();
            yield return LeaveWest.Prepend(new Vector3(-46f, 0, z)).Append(LeaveWest[LeaveWest.Length - 1] + new Vector3(-2f, 0, 60f)).ToArray();
            yield return Depart.Append(Depart[Depart.Length - 1] + new Vector3(2f, 0, -60f)).ToArray();
            yield return EastNorth.Prepend(new Vector3(46f, 0, z)).Append(EastNorth[EastNorth.Length - 1] + new Vector3(2f, 0, 60f)).ToArray();
        }

        static bool Free(Vector3 c, float half, float roadClear = 11f)
        {
            // the play area (+ its fences / lamps) and the parking lot / park stay open; roads keep their corridor
            if (c.x + half > PlayArea.MinX - 4f && c.x - half < PlayArea.MaxX + 4f && c.z + half > PlayArea.MinZ - 9f && c.z - half < PlayArea.MaxZ + 7.5f) return false;
            if (Overlaps(ParkingLot, c, half + 2f) || Overlaps(Park, c, half + 2f)) return false;
            foreach (var r in AllRoads()) if (DistToPolyline(c, r) < half + roadClear - 4f) return false;
            return true;
        }

        static bool Overlaps(Rect r, Vector3 c, float half) => c.x + half > r.xMin && c.x - half < r.xMax && c.z + half > r.yMin && c.z - half < r.yMax;

        // ------------------------------------------------------------------ city blocks
        static void Blocks(Transform root)
        {
            var blocks = new GameObject("Blocks").transform; blocks.SetParent(root, false);
            var near = new[]
            {
                GrayboxMaterials.Textured("Scenery_Brick", new Color(0.95f, 0.9f, 0.85f), "tex_brick.png", 2.5f),
                GrayboxMaterials.Textured("Scenery_BrickDark", new Color(0.7f, 0.62f, 0.6f), "tex_brick.png", 2.5f),
                GrayboxMaterials.Textured("Scenery_Metal", new Color(0.85f, 0.9f, 1f), "tex_metal_blue.png", 3f, 0.4f),
                Lit("Plaster", new Color(0.86f, 0.82f, 0.72f)), Lit("PlasterBlue", new Color(0.62f, 0.72f, 0.82f)),
            };
            var roofM = Lit("Roof", new Color(0.3f, 0.31f, 0.33f));
            var windowM = Lit("Windows", new Color(0.2f, 0.27f, 0.38f), 0.7f);
            var far = new[]
            {
                Unlit("FarA", new Color(0.62f, 0.68f, 0.78f)), Unlit("FarB", new Color(0.7f, 0.74f, 0.82f)),
                Unlit("FarC", new Color(0.56f, 0.62f, 0.72f)), Unlit("FarD", new Color(0.76f, 0.78f, 0.84f)),
            };
            const float step = 15f;
            int n = 0;
            for (float x = -112f; x <= 112f; x += step)
                for (float z = -112f; z <= 112f; z += step)
                {
                    var c = new Vector3(x + Rand(-2f, 2f), 0, z + Rand(-2f, 2f));
                    float d = new Vector2(c.x, c.z).magnitude;
                    if (d > 108f) continue;
                    // a cell may hold one big block or two smaller ones (more silhouettes near the lot)
                    float half = Rand(5f, 6.8f);
                    if (!Free(c, half))
                    {
                        // try a smaller block before giving up the cell (fills the gaps along the roads)
                        half = 3.6f;
                        if (!Free(c, half, 9f)) continue;
                    }
                    bool isNear = d < 62f;
                    float h = isNear ? Rand(5f, 15f) : Rand(10f, 36f) * Mathf.Lerp(1f, 1.3f, Mathf.InverseLerp(62f, 108f, d));
                    var size = new Vector3(half * 2f * Rand(0.85f, 1f), h, half * 2f * Rand(0.85f, 1f));
                    float yaw = Rand(-6f, 6f);
                    var b = Box(blocks, "Block" + n++, c + Vector3.up * h * 0.5f, size, isNear ? near[_rng.Next(near.Length)] : far[_rng.Next(far.Length)], yaw, shadows: isNear);
                    if (isNear)
                    {
                        Box(b.transform.parent, b.name + "_Roof", c + Vector3.up * (h + 0.25f), new Vector3(size.x + 0.4f, 0.5f, size.z + 0.4f), roofM, yaw);
                        // window bands on all four sides (a cheap read of "building", not a box)
                        for (float wy = 2.2f; wy < h - 1f; wy += 3f)
                        {
                            var rot = Quaternion.Euler(0, yaw, 0);
                            Box(blocks, b.name + "_WinZ" + wy, c + Vector3.up * wy + rot * new Vector3(0, 0, size.z * 0.5f + 0.03f), new Vector3(size.x * 0.8f, 1.1f, 0.08f), windowM, yaw, false);
                            Box(blocks, b.name + "_WinZ-" + wy, c + Vector3.up * wy + rot * new Vector3(0, 0, -size.z * 0.5f - 0.03f), new Vector3(size.x * 0.8f, 1.1f, 0.08f), windowM, yaw, false);
                            Box(blocks, b.name + "_WinX" + wy, c + Vector3.up * wy + rot * new Vector3(size.x * 0.5f + 0.03f, 0, 0), new Vector3(0.08f, 1.1f, size.z * 0.8f), windowM, yaw, false);
                            Box(blocks, b.name + "_WinX-" + wy, c + Vector3.up * wy + rot * new Vector3(-size.x * 0.5f - 0.03f, 0, 0), new Vector3(0.08f, 1.1f, size.z * 0.8f), windowM, yaw, false);
                        }
                    }
                }
            Debug.Log($"[Scenery] {n} city blocks");
        }

        // ------------------------------------------------------------------ edges of the play area
        static void Fences(Transform root)
        {
            var post = Lit("FencePost", new Color(0.55f, 0.57f, 0.6f), 0.5f);
            var mesh = SeeThrough("FenceMesh", new Color(0.55f, 0.58f, 0.62f, 0.32f));
            var brick = GrayboxMaterials.Textured("Brick", Color.white, "tex_brick.png", 2f);
            // west / east: chain-link along the lot edge north of the road (the road openings stay clear for the cars)
            foreach (float x in new[] { PlayArea.MinX - 0.15f, PlayArea.MaxX + 0.15f })
            {
                float z0 = -11.5f, z1 = PlayArea.MaxZ;
                Box(root, "FenceRailTop" + x, new Vector3(x, 2.3f, (z0 + z1) / 2f), new Vector3(0.06f, 0.06f, z1 - z0), post);
                Box(root, "FenceMesh" + x, new Vector3(x, 1.2f, (z0 + z1) / 2f), new Vector3(0.03f, 2.2f, z1 - z0), mesh);
                for (float z = z0; z <= z1 + 0.01f; z += 3f) Box(root, $"FencePost{x}_{z}", new Vector3(x, 1.25f, z), new Vector3(0.1f, 2.5f, 0.1f), post);
            }
            // south: low brick wall + railing beyond the sidewalk
            Box(root, "SouthWall", new Vector3(5f, 0.45f, PlayArea.MinZ - 0.3f), new Vector3(PlayArea.MaxX - PlayArea.MinX + 14f, 0.9f, 0.35f), brick);
            Box(root, "SouthRail", new Vector3(5f, 1.35f, PlayArea.MinZ - 0.3f), new Vector3(PlayArea.MaxX - PlayArea.MinX + 14f, 0.06f, 0.06f), post);
            for (float x = PlayArea.MinX - 7f; x <= PlayArea.MaxX + 7f; x += 3f) Box(root, "SouthRailPost" + x, new Vector3(x, 1.1f, PlayArea.MinZ - 0.3f), new Vector3(0.07f, 0.5f, 0.07f), post);
            // north: the lot's brick fence covers x -28..28; continue it east over the dev-yard end
            Box(root, "FenceN_East", new Vector3(33.2f, 1f, 18.5f), new Vector3(10.4f, 2f, 0.2f), brick);
        }

        // ------------------------------------------------------------------ trees, lamps, poles
        static void Tree(Transform root, Vector3 p, float s)
        {
            var bark = Lit("Bark", new Color(0.4f, 0.29f, 0.2f));
            var leaves = new[] { Lit("LeavesA", new Color(0.3f, 0.52f, 0.28f)), Lit("LeavesB", new Color(0.38f, 0.58f, 0.3f)), Lit("LeavesC", new Color(0.28f, 0.47f, 0.32f)) };
            var t = new GameObject("Tree") { layer = Layers.Scenery }.transform; t.SetParent(root, false); t.position = p;
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder); trunk.transform.SetParent(t, false);
            trunk.transform.localPosition = new Vector3(0, 1.2f * s, 0); trunk.transform.localScale = new Vector3(0.28f * s, 1.2f * s, 0.28f * s);
            trunk.GetComponent<MeshRenderer>().sharedMaterial = bark;
            var m = leaves[_rng.Next(leaves.Length)];
            for (int k = 0; k < 3; k++)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Sphere); c.transform.SetParent(t, false);
                c.transform.localPosition = new Vector3(Rand(-0.5f, 0.5f) * s, (2.6f + k * 0.7f) * s, Rand(-0.5f, 0.5f) * s);
                c.transform.localScale = Vector3.one * Rand(1.6f, 2.3f) * s;
                c.GetComponent<MeshRenderer>().sharedMaterial = m;
            }
        }

        static void Trees(Transform root)
        {
            var trees = new GameObject("Trees").transform; trees.SetParent(root, false);
            // street trees along the far sidewalk of the road (south), both road continuations and the curves
            for (float x = -60f; x <= 64f; x += 9f) Tree(trees, new Vector3(x + Rand(-1f, 1f), 0, -24.5f), Rand(0.9f, 1.2f));
            foreach (var road in new[] { Approach, LeaveWest, Depart, EastNorth })
                for (int i = 0; i + 1 < road.Length; i++)
                {
                    var a = road[i]; var b = road[i + 1]; var side = Vector3.Cross(Vector3.up, (b - a).normalized);
                    for (float t = 0.2f; t < 1f; t += 0.45f)
                        foreach (int sgn in new[] { -1, 1 })
                        {
                            var p = Vector3.Lerp(a, b, t) + side * sgn * 6f;
                            if (!PlayArea.Contains(p, -4f)) Tree(trees, p, Rand(0.8f, 1.2f));
                        }
                }
            // the strip between the west parking lot and the bending road
            for (float z = -6f; z <= 14f; z += 5f) Tree(trees, new Vector3(Rand(-56f, -54f), 0, z + Rand(-1f, 1f)), Rand(0.9f, 1.2f));
            // the little park east of the lot
            for (int i = 0; i < 9; i++) Tree(trees, new Vector3(Rand(Park.xMin + 1.5f, Park.xMax - 1.5f), 0, Rand(Park.yMin + 1.5f, Park.yMax - 1.5f)), Rand(0.9f, 1.4f));
        }

        static void Lamp(Transform root, Vector3 p, float yaw)
        {
            var pole = Lit("LampPole", new Color(0.3f, 0.32f, 0.35f), 0.5f);
            var head = Lit("LampHead", new Color(1f, 0.95f, 0.8f), 0.8f);
            var l = new GameObject("Lamp") { layer = Layers.Scenery }.transform; l.SetParent(root, false); l.position = p; l.rotation = Quaternion.Euler(0, yaw, 0);
            var a = GrayboxMaterials.Box(l, "Pole", new Vector3(0, 3.5f, 0), new Vector3(0.14f, 7f, 0.14f), pole, collider: false);
            GrayboxMaterials.Box(l, "Arm", new Vector3(0, 6.9f, 0.8f), new Vector3(0.1f, 0.1f, 1.6f), pole, collider: false);
            GrayboxMaterials.Box(l, "Head", new Vector3(0, 6.8f, 1.5f), new Vector3(0.35f, 0.15f, 0.6f), head, collider: false);
            // M4.9 G1: a real light under the head at night (no shadows); lamps switch on one by one
            var hp = l.TransformPoint(new Vector3(0, 6.65f, 1.5f));
            EnvironmentBuilder.NightSpot(l, "LampLight", hp, hp + Vector3.down * 5f + l.forward * 1.2f, 18f, 45f, new Color(1f, 0.82f, 0.58f), 0.3f + Mathf.Repeat(p.x * 0.37f + p.z * 0.11f, 1f) * 0.12f, 115f);
        }

        static void Lamps(Transform root)
        {
            var lamps = new GameObject("Lamps").transform; lamps.SetParent(root, false);
            for (float x = -64f; x <= 66f; x += 13f) Lamp(lamps, new Vector3(x, 0, -21.8f), 0f);          // far side of the road, arms over it
            foreach (var road in new[] { Approach, Depart })
                for (int i = 0; i + 1 < road.Length; i += 1)
                {
                    var a = road[i]; var b = road[i + 1]; var dir = (b - a).normalized; var side = Vector3.Cross(Vector3.up, dir);
                    var p = (a + b) * 0.5f + side * 4.6f;
                    Lamp(lamps, p, Mathf.Atan2(-side.x, -side.z) * Mathf.Rad2Deg);
                }
        }

        static void UtilityPoles(Transform root)
        {
            var poles = new GameObject("UtilityPoles").transform; poles.SetParent(root, false);
            var wood = Lit("PoleWood", new Color(0.42f, 0.33f, 0.24f));
            var wireM = Lit("Wire", new Color(0.08f, 0.08f, 0.09f));
            var tops = new List<Vector3>();
            for (float x = -66f; x <= 70f; x += 17f)
            {
                var p = new Vector3(x, 0, 21.5f);
                Box(poles, "Pole" + x, p + Vector3.up * 4.5f, new Vector3(0.28f, 9f, 0.28f), wood);
                Box(poles, "Cross" + x, p + Vector3.up * 8.4f, new Vector3(2.2f, 0.16f, 0.16f), wood);
                tops.Add(p + Vector3.up * 8.5f);
            }
            for (int i = 0; i + 1 < tops.Count; i++)
                foreach (float off in new[] { -0.9f, 0f, 0.9f })
                {
                    var w = new GameObject("Wire") { layer = Layers.Scenery };
                    w.transform.SetParent(poles, false);
                    var lr = w.AddComponent<LineRenderer>();
                    lr.useWorldSpace = true; lr.widthMultiplier = 0.035f; lr.shadowCastingMode = ShadowCastingMode.Off; lr.sharedMaterial = wireM;
                    const int k = 9; lr.positionCount = k;
                    for (int j = 0; j < k; j++)
                    {
                        float t = j / (k - 1f);
                        lr.SetPosition(j, Vector3.Lerp(tops[i], tops[i + 1], t) + new Vector3(off, -Mathf.Sin(t * Mathf.PI) * 0.7f, 0));
                    }
                }
        }

        // ------------------------------------------------------------------ parked cars + billboards
        static void ParkedCars(Transform root)
        {
            var cars = new GameObject("ParkedCars").transform; cars.SetParent(root, false);
            var models = new[] { "V01", "V02", "V03", "V04" }.Select(VehicleGlbBuilder.GlbFor).Where(p => p != null).Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p)).ToArray();
            if (models.Length == 0) return;
            var tints = new[] { new Color(0.85f, 0.2f, 0.2f), new Color(0.95f, 0.95f, 0.95f), new Color(0.15f, 0.15f, 0.18f), new Color(0.3f, 0.55f, 0.85f), new Color(0.9f, 0.75f, 0.2f), new Color(0.4f, 0.6f, 0.4f) };
            void Car(Vector3 p, float yaw)
            {
                var src = models[_rng.Next(models.Length)];
                var go = Object.Instantiate(src, cars, false);
                go.name = "Parked_" + src.name;
                go.transform.SetPositionAndRotation(p, Quaternion.Euler(0, yaw, 0));
                var tint = tints[_rng.Next(tints.Length)];
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] == null) continue;
                        var lit = GlbKit.Lit(mats[i]);
                        mats[i] = lit.name.StartsWith("Soap_Blue") || lit.name.StartsWith("Soap Blue") ? GlbKit.Tinted(lit, "Parked" + ColorUtility.ToHtmlStringRGB(tint), tint * 1.1f) : lit;
                    }
                    r.sharedMaterials = mats;
                }
            }
            // parking lot west of the lot: two rows facing each other
            for (int i = 0; i < 6; i++)
            {
                if (_rng.NextDouble() < 0.8) Car(new Vector3(ParkingLot.xMin + 2.8f + i * 2.6f, 0, ParkingLot.yMin + 3f), 0f);
                if (_rng.NextDouble() < 0.8) Car(new Vector3(ParkingLot.xMin + 2.8f + i * 2.6f, 0, ParkingLot.yMax - 3f), 180f);
            }
            // kerbside along the far sidewalk east and west of the play area
            foreach (float x in new[] { -58f, -45f, 48f, 60f }) Car(new Vector3(x, 0, -19.2f), 90f);
        }

        static void Billboards(Transform root)
        {
            var bb = new GameObject("Billboards").transform; bb.SetParent(root, false);
            var frame = Lit("BillboardFrame", new Color(0.22f, 0.24f, 0.28f), 0.4f);
            var ads = new (string text, Color bg, Color fg)[]
            {
                ("서디 세차장 — 거품 가득!", new Color(0.2f, 0.45f, 0.85f), Color.white),
                ("너구리 출몰 주의", new Color(0.95f, 0.8f, 0.2f), new Color(0.15f, 0.12f, 0.1f)),
                ("왁스 1+1 행사중", new Color(0.85f, 0.25f, 0.3f), Color.white),
                ("동네 피자 배달 30분", new Color(0.25f, 0.6f, 0.35f), Color.white),
            };
            // boards show their -Z face (text side) to the lot: south ones turned 180°, north ones 0°
            var spots = new[] { (new Vector3(10f, 0, -26.8f), 180f), (new Vector3(-40f, 0, -27f), 200f), (new Vector3(-12f, 0, 24f), 0f), (new Vector3(50f, 0, 23.5f), 20f) };
            for (int i = 0; i < spots.Length; i++)
            {
                var (p, yaw) = spots[i];
                var b = new GameObject("Billboard" + i) { layer = Layers.Scenery }.transform; b.SetParent(bb, false); b.position = p; b.rotation = Quaternion.Euler(0, yaw, 0);
                GrayboxMaterials.Box(b, "PoleL", new Vector3(-3f, 3.5f, 0), new Vector3(0.35f, 7f, 0.35f), frame, collider: false);
                GrayboxMaterials.Box(b, "PoleR", new Vector3(3f, 3.5f, 0), new Vector3(0.35f, 7f, 0.35f), frame, collider: false);
                GrayboxMaterials.Box(b, "Frame", new Vector3(0, 8.2f, 0.05f), new Vector3(9.4f, 4.4f, 0.3f), frame, collider: false);
                var ad = ads[i % ads.Length];
                GrayboxMaterials.Box(b, "Board", new Vector3(0, 8.2f, -0.12f), new Vector3(9f, 4f, 0.05f), Lit("Ad" + i, ad.bg, 0.3f), collider: false);
                var txt = new GameObject("Text") { layer = Layers.Scenery };
                txt.transform.SetParent(b, false);
                txt.transform.localPosition = new Vector3(0, 8.2f, -0.17f);
                txt.transform.localRotation = Quaternion.identity;
                var tm = txt.AddComponent<TextMesh>();
                tm.text = ad.text; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
                tm.font = UI.UiFactory.Font; tm.fontSize = 64; tm.characterSize = 0.075f; tm.color = ad.fg; tm.fontStyle = FontStyle.Bold;
                txt.GetComponent<MeshRenderer>().sharedMaterial = UI.UiFactory.Font.material;
                // M4.6 B4: depth-tested text (the font's GUI material ignores depth → text showed through buildings)
                SceneBuilder.Set(txt.AddComponent<Visual.WorldTextDepth>(), "_shader", Shader.Find("CarWash/WorldText"));
                // TextMesh faces -Z: boards face the lot (their -Z side)
            }
        }

        // ------------------------------------------------------------------ invisible play-area walls
        static void Bounds(Transform env)
        {
            var root = new GameObject("PlayBounds") { layer = Layers.PlayBounds }.transform;
            root.SetParent(env, false);
            const float T = 4f, H = PlayArea.WallHeight;
            float cx = (PlayArea.MinX + PlayArea.MaxX) / 2f, cz = (PlayArea.MinZ + PlayArea.MaxZ) / 2f;
            float w = PlayArea.MaxX - PlayArea.MinX, d = PlayArea.MaxZ - PlayArea.MinZ;
            void Wall(string n, Vector3 c, Vector3 s)
            {
                var go = new GameObject(n) { layer = Layers.PlayBounds };
                go.transform.SetParent(root, false);
                go.transform.position = c;
                go.AddComponent<BoxCollider>().size = s;
            }
            Wall("WallW", new Vector3(PlayArea.MinX - T / 2f, H / 2f - 1f, cz), new Vector3(T, H + 2f, d + 2 * T));
            Wall("WallE", new Vector3(PlayArea.MaxX + T / 2f, H / 2f - 1f, cz), new Vector3(T, H + 2f, d + 2 * T));
            Wall("WallS", new Vector3(cx, H / 2f - 1f, PlayArea.MinZ - T / 2f), new Vector3(w + 2 * T, H + 2f, T));
            Wall("WallN", new Vector3(cx, H / 2f - 1f, PlayArea.MaxZ + T / 2f), new Vector3(w + 2 * T, H + 2f, T));
            Wall("Ceiling", new Vector3(cx, H + T / 2f, cz), new Vector3(w + 2 * T, T, d + 2 * T));
            Wall("Floor", new Vector3(cx, -3f, cz), new Vector3(w + 2 * T, 2f, d + 2 * T)); // anything that tunnels through the ground stops here
        }
    }
}
