using System.Collections.Generic;
using System.IO;
using CarWash.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CarWash.Editor
{
    /// <summary>
    /// M4.7 E2 + E3: breaks up the lot with decals and set dressing (deterministic, seed 4711).
    ///  E2 decals (URP DecalProjector, one material per kind, GPU instanced, generated textures in Art/Generated/Decals):
    ///     oil stains, tyre marks, cracks, water marks, manholes, drains, bay-corner grime / limescale, lane arrows, flyers,
    ///     leaf litter, gravel strips.
    ///  E3 dressing: grass clumps / weeds (alpha-clipped crossed cards from a 4-variant atlas, ONE combined mesh, no collider,
    ///     no shadows) along fences / walls / curbs and in asphalt cracks; small props (pallets, drums, deco cones, hose coils,
    ///     stones, rubbish) built into ONE combined mesh on ONE palette-atlas material (= one draw call); only pallets and drums
    ///     get colliders, and only at hand-picked spots against walls.
    /// Never on the car path (queue lane, feed lane, bays, exit lane, road) or near interaction points / spawns / props.
    /// </summary>
    public static class EnvDressingBuilder
    {
        public const string DecalTexDir = "Assets/_Project/Art/Generated/Decals";
        public const string DecalMatDir = "Assets/_Project/Art/Materials/Decals";
        public const string MeshDir = "Assets/_Project/Art/Generated/Dressing";

        // ---------------- exclusion zones (x, z rects) ----------------
        static readonly Rect[] CarPath =
        {
            new Rect(-23f, -18.5f, 6f, 33f),    // queue lane x = -20 (road → feed lane)
            new Rect(-23f, 9.5f, 35f, 5f),      // feed lane z = 12 → bay entrances
            new Rect(-11.5f, -1.5f, 23f, 11f),  // the three bays (x -11.1 … 11.1, z -1 … 9)
            new Rect(-12f, -7.8f, 52f, 5.6f),   // exit lane z = -5 → east
            new Rect(-40f, -18.6f, 80f, 6.2f),  // road z = -15.5
            new Rect(31.5f, -13f, 4f, 9f),       // M4.9 G9: exit ramp x = 33.5
            new Rect(19.5f, -12.2f, 11.5f, 5.6f), // M4.9 G9: drying / vacuum area
        };

        static readonly Vector3[] KeepClear =
        {
            LotBuilder.OfficeCenter + new Vector3(-1.5f, 0f, 3.05f), // day board
            LotBuilder.OfficeCenter + new Vector3(2.5f, 0f, 3.3f),   // shop terminal
            LotBuilder.LockerArea, new Vector3(14f, 0f, -10f),       // lockers, vacuum station
            LotBuilder.CagePos, new Vector3(-3.5f, 0f, -8.5f), new Vector3(-3.5f, 0f, -6.6f), // cage, crates
            new Vector3(-12f, 0f, 1f), new Vector3(12f, 0f, -8f), new Vector3(15f, 0f, 10f), new Vector3(16.1f, 0f, 10.3f), // props
            new Vector3(19.2f, 0f, -8.2f), new Vector3(19.2f, 0f, -12.5f), new Vector3(-5.6f, 0f, -1.2f), new Vector3(2.6f, 0f, -1.2f),
            new Vector3(14f, 0f, 15.2f), new Vector3(18.1f, 0f, 15.2f), new Vector3(16f, 0f, 15.5f), // trash bags, dumpster
            new Vector3(-12f, 0f, 17.8f), // vent
        };

        public static bool InCarPath(Vector3 p, float margin = 0f)
        {
            foreach (var r in CarPath) if (p.x > r.xMin - margin && p.x < r.xMax + margin && p.z > r.yMin - margin && p.z < r.yMax + margin) return true;
            return false;
        }

        public static bool Clear(Vector3 p, float radius)
        {
            if (InCarPath(p, radius * 0.5f)) return false;
            foreach (var k in KeepClear) if ((new Vector2(p.x - k.x, p.z - k.z)).sqrMagnitude < (radius + 1.6f) * (radius + 1.6f)) return false;
            foreach (var s in LotBuilderSpawns) if ((new Vector2(p.x - s.x, p.z - s.z)).sqrMagnitude < 9f) return false;
            foreach (var (c, r) in SelfWashBuilder.Footprints) { float rr = Mathf.Max(radius, 0.35f) + r; if ((new Vector2(p.x - c.x, p.z - c.z)).sqrMagnitude < rr * rr) return false; } // G9 props (≥0.35: same answer as the 0.3 test check)
            return true;
        }

        static IEnumerable<Vector3> LotBuilderSpawns { get { for (int i = 0; i < 4; i++) yield return LotBuilder.LockerArea + new Vector3(-1.5f + i, 0f, -0.5f); } }

        // ---------------- entry ----------------
        public static void Build(Transform env)
        {
            var rng = new System.Random(4711);
            var root = new GameObject("Dressing").transform;
            root.SetParent(env, false);
            Decals(root, rng);
            Dressing(root, rng);
        }

        // ======================================================== E2 decals
        static void Decals(Transform root, System.Random rng)
        {
            var parent = new GameObject("Decals").transform; parent.SetParent(root, false);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            int n = 0;
            void D(string kind, Vector3 p, float w, float h, float yaw)
            {
                var go = new GameObject($"Decal_{kind}_{n++}");
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(new Vector3(p.x, 0.25f, p.z), Quaternion.Euler(90f, yaw, 0f));
                var d = go.AddComponent<DecalProjector>();
                d.material = DecalMat(kind);
                d.size = new Vector3(w, h, 0.5f);
                d.pivot = new Vector3(0f, 0f, 0.25f);
                d.drawDistance = 70f; d.fadeScale = 0.85f;
            }
            float[] bays = { -8f, 0f, 8f };
            // oil under each bay's stop point + along the queue / around the lot
            foreach (var bx in bays) { D("oil", new Vector3(bx + R(-0.6f, 0.6f), 0, R(2.5f, 5.5f)), R(1.2f, 1.9f), R(1.0f, 1.6f), R(0, 360)); D("oil", new Vector3(bx + R(-1f, 1f), 0, R(6f, 8f)), R(0.6f, 1.0f), R(0.5f, 0.9f), R(0, 360)); }
            foreach (var z in LotBuilder.QueueSlotZ) D("oil", new Vector3(LotBuilder.QueueX + R(-0.4f, 0.4f), 0, z + R(-0.5f, 0.5f)), R(0.9f, 1.5f), R(0.8f, 1.3f), R(0, 360));
            D("oil", new Vector3(14.6f, 0, -8.2f), 1.4f, 1.1f, 30f);
            for (int i = 0; i < 4; i++) D("oil", new Vector3(R(-30f, 30f), 0, R(-17f, -14f)), R(0.8f, 1.6f), R(0.7f, 1.3f), R(0, 360));
            // tyre marks along the lanes (+ two skid arcs at the queue → feed turn)
            for (int i = 0; i < 3; i++) D("tyre", new Vector3(LotBuilder.QueueX + R(-0.5f, 0.5f), 0, R(-10f, 6f)), 1.7f, R(3f, 5f), R(-4f, 4f));
            for (int i = 0; i < 3; i++) D("tyre", new Vector3(R(-16f, 6f), 0, LotBuilder.FeedZ + R(-0.4f, 0.4f)), 1.7f, R(3f, 5f), 90f + R(-4f, 4f));
            for (int i = 0; i < 4; i++) D("tyre", new Vector3(R(-8f, 30f), 0, LotBuilder.ExitZ + R(-0.5f, 0.5f)), 1.7f, R(3f, 6f), 90f + R(-5f, 5f));
            D("tyre", new Vector3(-19f, 0, 11f), 1.8f, 3.5f, 45f); D("tyre", new Vector3(-9f, 0, -4.2f), 1.8f, 3.2f, 125f);
            for (int i = 0; i < 5; i++) D("tyre", new Vector3(R(-34f, 34f), 0, LotBuilder.RoadZ + R(-1.2f, 1.2f)), 1.7f, R(4f, 7f), 90f + R(-3f, 3f));
            // cracks: open asphalt (outside the bays)
            for (int i = 0, tries = 0; i < 22 && tries < 400; tries++)
            {
                var p = new Vector3(R(-27f, 37f), 0, R(-19.5f, 17.5f));
                if (p.x > -11.5f && p.x < 11.5f && p.z > -1.5f && p.z < 9.5f) continue;
                D("crack", p, R(1.5f, 3.6f), R(1.5f, 3.6f), R(0, 360)); i++;
            }
            // water marks / wet trails out of each bay, puddle rings by the reels
            foreach (var bx in bays) { D("water", new Vector3(bx + R(-0.8f, 0.8f), 0, R(-2.5f, -1.2f)), R(1.6f, 2.4f), R(2.5f, 3.5f), R(-10f, 10f)); D("water", new Vector3(bx + R(-2f, 2f), 0, R(0.5f, 8f)), R(1f, 1.6f), R(1f, 1.6f), R(0, 360)); }
            // manholes on the road / lot, a drain in each bay + gutters at the curb
            D("manhole", new Vector3(-10f, 0, -16.6f), 0.9f, 0.9f, 0f); D("manhole", new Vector3(17.5f, 0, -14.6f), 0.9f, 0.9f, 37f); D("manhole", new Vector3(-2f, 0, 14.6f), 0.9f, 0.9f, 12f);
            // (bay centre drains: the SW_DrainGrate prop of SelfWashBuilder, G9 — not doubled here)
            D("drain", new Vector3(-24f, 0, -18.1f), 1.2f, 0.45f, 0f); D("drain", new Vector3(10f, 0, -18.1f), 1.2f, 0.45f, 0f); D("drain", new Vector3(28f, 0, -18.1f), 1.2f, 0.45f, 0f);
            // bay corners: grime / limescale where the walls meet the floor
            foreach (var bx in bays)
                foreach (var sx in new[] { -1f, 1f })
                    foreach (var z in new[] { -0.4f, 8.4f })
                        D("grime", new Vector3(bx + sx * 2.65f, 0, z), R(0.9f, 1.3f), R(0.9f, 1.3f), R(0, 360));
            // painted lane arrows
            D("arrow", new Vector3(LotBuilder.QueueX, 0, -7.5f), 1.0f, 2.2f, 0f); D("arrow", new Vector3(LotBuilder.QueueX, 0, 3.5f), 1.0f, 2.2f, 0f);
            D("arrow", new Vector3(-14f, 0, LotBuilder.FeedZ), 1.0f, 2.2f, 90f);
            D("arrow", new Vector3(-2f, 0, LotBuilder.ExitZ), 1.0f, 2.2f, 90f); D("arrow", new Vector3(16f, 0, LotBuilder.ExitZ), 1.0f, 2.2f, 90f);
            // flyers, leaf litter, gravel strips (along the fence / office / curb)
            for (int i = 0, tries = 0; i < 9 && tries < 300; tries++)
            {
                var p = new Vector3(R(-27f, 36f), 0, R(-19.8f, 18f));
                if (InCarPath(p) && !(p.z < -12.4f && p.z > -18.6f)) continue; // flyers may lie on the road edge
                D("flyer", p, R(0.22f, 0.32f), R(0.3f, 0.42f), R(0, 360)); i++;
            }
            for (int i = 0; i < 9; i++) D("leaves", new Vector3(R(-27f, 27f), 0, R(17.0f, 17.9f)), R(1.2f, 2.4f), R(0.8f, 1.4f), R(-20f, 20f));
            for (int i = 0; i < 6; i++) D("leaves", new Vector3(R(-30f, 34f), 0, R(-19.6f, -19.0f)), R(1.2f, 2.2f), R(0.6f, 1.0f), R(-15f, 15f));
            for (int i = 0; i < 7; i++) D("gravel", new Vector3(R(-27f, 27f), 0, 17.95f), R(2.5f, 4f), 0.7f, 90f + R(-3f, 3f));
            D("gravel", new Vector3(-16.6f, 0, -10f), 0.7f, 5f, 0f); D("gravel", new Vector3(-12f, 0, -13.6f), 0.7f, 6.5f, 90f);
        }

        static readonly Dictionary<string, Material> DecalMats = new Dictionary<string, Material>();
        internal static Material DecalMat(string kind)
        {
            if (DecalMats.TryGetValue(kind, out var m) && m != null) return m;
            Directory.CreateDirectory(DecalMatDir);
            string path = $"{DecalMatDir}/Decal_{kind}.mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Shader Graphs/Decal")); AssetDatabase.CreateAsset(m, path); }
            var tex = DecalTex(kind);
            if (m.HasProperty("Base_Map")) m.SetTexture("Base_Map", tex);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("Normal_Blend")) m.SetFloat("Normal_Blend", kind == "water" || kind == "oil" ? 0.6f : 0.25f);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            DecalMats[kind] = m;
            return m;
        }

        // ---------------- decal textures (procedural, 256²–512², RGBA: colour + coverage) ----------------
        static Texture2D DecalTex(string kind)
        {
            Directory.CreateDirectory(DecalTexDir);
            string path = $"{DecalTexDir}/decal_{kind}.png";
            if (!File.Exists(path))
            {
                int S = kind == "crack" || kind == "tyre" || kind == "gravel" || kind == "leaves" ? 512 : 256;
                var px = new Color[S * S];
                var rng = new System.Random(kind.GetHashCode() & 0xffff);
                float Rn() => (float)rng.NextDouble();
                switch (kind)
                {
                    case "oil": Blob(px, S, new Color(0.03f, 0.03f, 0.025f), 0.85f, 0.35f, rng, rainbow: true); break;
                    case "water": Blob(px, S, new Color(0.05f, 0.06f, 0.07f), 0.45f, 0.45f, rng, ring: true); break;
                    case "grime": Blob(px, S, new Color(0.22f, 0.2f, 0.12f), 0.6f, 0.6f, rng, ring: false, chalk: true); break;
                    case "tyre":
                        for (int y = 0; y < S; y++)
                            for (int x = 0; x < S; x++)
                            {
                                float u = x / (float)S, v = y / (float)S;
                                float a = 0f;
                                foreach (float cx in new[] { 0.2f, 0.8f })
                                {
                                    float d = Mathf.Abs(u - cx - Mathf.Sin(v * 3.1f) * 0.02f);
                                    float tread = 0.75f + 0.25f * Mathf.Sin(v * 220f);
                                    a = Mathf.Max(a, Mathf.Clamp01(1f - d / 0.07f) * tread);
                                }
                                a *= Mathf.SmoothStep(0f, 0.15f, v) * Mathf.SmoothStep(0f, 0.15f, 1f - v) * (0.55f + 0.45f * GroundTextures.Fbm(u, v, 6, 3));
                                px[y * S + x] = new Color(0.02f, 0.02f, 0.02f, a * 0.75f);
                            }
                        break;
                    case "crack":
                        for (int k = 0; k < 4; k++) Crack(px, S, new Vector2(0.5f + (Rn() - 0.5f) * 0.3f, 0.5f + (Rn() - 0.5f) * 0.3f), Rn() * 6.28f, 0.45f, 3.2f, rng, 0);
                        break;
                    case "manhole": Manhole(px, S); break;
                    case "drain": Drain(px, S); break;
                    case "arrow": Arrow(px, S); break;
                    case "flyer": Flyer(px, S, rng); break;
                    case "leaves": Scatter(px, S, rng, 160, leaf: true); break;
                    case "gravel": Scatter(px, S, rng, 900, leaf: false); break;
                }
                var t = new Texture2D(S, S, TextureFormat.RGBA32, true);
                t.SetPixels(px); t.Apply();
                File.WriteAllBytes(path, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                if (AssetImporter.GetAtPath(path) is TextureImporter ti) { ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport(); }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void Blob(Color[] px, int S, Color col, float peak, float soft, System.Random rng, bool rainbow = false, bool ring = false, bool chalk = false)
        {
            var amp = new float[6]; var ph = new float[6];
            for (int k = 0; k < 6; k++) { amp[k] = (float)rng.NextDouble() * 0.14f / (k + 1); ph[k] = (float)rng.NextDouble() * 6.283f; }
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x + 0.5f) / S * 2f - 1f, v = (y + 0.5f) / S * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v), a = Mathf.Atan2(v, u);
                    float edge = 0.75f; for (int k = 0; k < 6; k++) edge += amp[k] * Mathf.Sin((k + 2) * a + ph[k]);
                    float n = GroundTextures.Fbm(x / (float)S, y / (float)S, 5, 9);
                    float alpha = peak * Mathf.Clamp01((edge - r) / soft) * (0.6f + 0.4f * n);
                    var c = col;
                    if (rainbow) { float h = Mathf.Repeat(r * 3.5f + n, 1f); var rb = Color.HSVToRGB(h, 0.6f, 0.35f); c = Color.Lerp(col, rb, 0.25f * Mathf.Clamp01(r * 1.6f)); }
                    if (ring) alpha *= Mathf.Lerp(0.55f, 1f, Mathf.SmoothStep(edge - 0.35f, edge - 0.05f, r));
                    if (chalk) c = Color.Lerp(c, new Color(0.78f, 0.78f, 0.72f), Mathf.SmoothStep(0.6f, 0.8f, n) * 0.6f);
                    px[y * S + x] = new Color(c.r, c.g, c.b, alpha);
                }
        }

        static void Crack(Color[] px, int S, Vector2 p, float ang, float len, float width, System.Random rng, int depth)
        {
            int steps = (int)(len * S / 2f);
            for (int i = 0; i < steps; i++)
            {
                ang += ((float)rng.NextDouble() - 0.5f) * 0.6f;
                p += new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (2f / S);
                if (p.x < 0.03f || p.x > 0.97f || p.y < 0.03f || p.y > 0.97f) return;
                float w = width * (1f - i / (float)steps * 0.7f);
                int cx = (int)(p.x * S), cy = (int)(p.y * S), r = Mathf.CeilToInt(w);
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int xx = cx + dx, yy = cy + dy; if (xx < 0 || yy < 0 || xx >= S || yy >= S) continue;
                        float d = Mathf.Sqrt(dx * dx + dy * dy); float a = Mathf.Clamp01(w - d + 0.5f);
                        ref var c = ref px[yy * S + xx];
                        if (a > c.a) c = new Color(0.02f, 0.02f, 0.02f, a * 0.9f);
                    }
                if (depth < 3 && rng.NextDouble() < 0.012) Crack(px, S, p, ang + ((float)rng.NextDouble() > 0.5f ? 0.9f : -0.9f), len * 0.45f, width * 0.6f, rng, depth + 1);
            }
        }

        static void Manhole(Color[] px, int S)
        {
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x + 0.5f) / S * 2f - 1f, v = (y + 0.5f) / S * 2f - 1f, r = Mathf.Sqrt(u * u + v * v);
                    if (r > 0.98f) { px[y * S + x] = Color.clear; continue; }
                    float grid = (Mathf.Repeat(u * 7f, 1f) < 0.18f || Mathf.Repeat(v * 7f, 1f) < 0.18f) ? 0.7f : 1f;
                    float rim = r > 0.86f ? 0.8f : 1f;
                    float g = 0.28f * grid * rim * (0.85f + 0.15f * GroundTextures.Fbm(u * 0.5f + 0.5f, v * 0.5f + 0.5f, 4, 21));
                    px[y * S + x] = new Color(g, g, g * 1.02f, Mathf.Clamp01((0.98f - r) * 40f));
                }
        }

        static void Drain(Color[] px, int S)
        {
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x + 0.5f) / S, v = (y + 0.5f) / S;
                    bool frame = u < 0.08f || u > 0.92f || v < 0.08f || v > 0.92f;
                    bool slot = !frame && Mathf.Repeat(v * 9f, 1f) > 0.45f;
                    var c = frame ? new Color(0.3f, 0.3f, 0.3f) : slot ? new Color(0.02f, 0.02f, 0.02f) : new Color(0.24f, 0.24f, 0.25f);
                    px[y * S + x] = new Color(c.r, c.g, c.b, 1f);
                }
        }

        static void Arrow(Color[] px, int S)
        {
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x + 0.5f) / S - 0.5f, v = (y + 0.5f) / S;
                    bool shaft = Mathf.Abs(u) < 0.12f && v > 0.08f && v < 0.62f;
                    bool head = v >= 0.6f && v < 0.95f && Mathf.Abs(u) < (0.95f - v) * 1.15f;
                    float wear = GroundTextures.Fbm(u + 0.5f, v, 8, 33);
                    float a = (shaft || head) ? Mathf.Clamp01(0.95f - Mathf.SmoothStep(0.55f, 0.75f, wear)) : 0f;
                    px[y * S + x] = new Color(0.92f, 0.92f, 0.88f, a * 0.85f);
                }
        }

        static void Flyer(Color[] px, int S, System.Random rng)
        {
            var paper = Color.HSVToRGB((float)rng.NextDouble(), 0.25f, 0.95f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x + 0.5f) / S, v = (y + 0.5f) / S;
                    bool inside = u > 0.06f && u < 0.94f && v > 0.04f && v < 0.96f;
                    var c = paper * (0.9f + 0.1f * GroundTextures.Fbm(u, v, 4, 5));
                    if (v > 0.72f && v < 0.86f && u > 0.14f && u < 0.86f) c = new Color(0.8f, 0.15f, 0.12f);                 // headline
                    else if (v > 0.2f && v < 0.66f && Mathf.Repeat(v * 14f, 1f) < 0.35f && u > 0.14f && u < 0.86f) c *= 0.35f; // text lines
                    px[y * S + x] = new Color(c.r, c.g, c.b, inside ? 1f : 0f);
                }
        }

        static void Scatter(Color[] px, int S, System.Random rng, int count, bool leaf)
        {
            for (int i = 0; i < count; i++)
            {
                float cx = 0.08f + (float)rng.NextDouble() * 0.84f, cy = 0.08f + (float)rng.NextDouble() * 0.84f;
                float edge = Mathf.Min(Mathf.Min(cx, 1f - cx), Mathf.Min(cy, 1f - cy));
                if (rng.NextDouble() > edge * 6f) continue; // denser in the middle
                float size = leaf ? 0.02f + (float)rng.NextDouble() * 0.025f : 0.004f + (float)rng.NextDouble() * 0.008f;
                float ang = (float)rng.NextDouble() * 6.28f;
                var col = leaf ? Color.Lerp(new Color(0.55f, 0.32f, 0.1f), new Color(0.75f, 0.6f, 0.2f), (float)rng.NextDouble())
                               : Color.Lerp(new Color(0.42f, 0.4f, 0.37f), new Color(0.7f, 0.68f, 0.62f), (float)rng.NextDouble());
                int r = Mathf.CeilToInt(size * S * 1.2f), px0 = (int)(cx * S), py0 = (int)(cy * S);
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int xx = px0 + dx, yy = py0 + dy; if (xx < 0 || yy < 0 || xx >= S || yy >= S) continue;
                        float lx = (dx * Mathf.Cos(ang) + dy * Mathf.Sin(ang)) / (size * S), ly = (-dx * Mathf.Sin(ang) + dy * Mathf.Cos(ang)) / (size * S);
                        float d = leaf ? lx * lx + ly * ly * 3.2f : lx * lx + ly * ly;
                        if (d < 1f) { float shade = 0.8f + 0.2f * (1f - d); px[yy * S + xx] = new Color(col.r * shade, col.g * shade, col.b * shade, 1f); }
                    }
            }
        }

        // ======================================================== E3 dressing meshes
        class MeshB
        {
            public readonly List<Vector3> v = new List<Vector3>(); public readonly List<Vector2> uv = new List<Vector2>(); public readonly List<int> t = new List<int>();
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
            {
                int i = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d); uv.Add(ua); uv.Add(ub); uv.Add(uc); uv.Add(ud);
                t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);
            }
            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector2 u)
            {
                int i = v.Count; v.Add(a); v.Add(b); v.Add(c); uv.Add(u); uv.Add(u); uv.Add(u); t.Add(i); t.Add(i + 1); t.Add(i + 2);
            }
            public Mesh Build(string name)
            {
                var m = new Mesh { name = name, indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
                return m;
            }
        }

        // palette atlas: 4×4 flat colours (one material for every small prop)
        static readonly Color[] Palette =
        {
            new Color(0.62f, 0.47f, 0.3f), new Color(0.45f, 0.33f, 0.2f), new Color(0.15f, 0.3f, 0.6f), new Color(0.6f, 0.15f, 0.12f),
            new Color(0.45f, 0.25f, 0.12f), new Color(0.95f, 0.4f, 0.08f), new Color(0.9f, 0.9f, 0.88f), new Color(0.15f, 0.45f, 0.2f),
            new Color(0.08f, 0.08f, 0.08f), new Color(0.5f, 0.5f, 0.48f), new Color(0.36f, 0.35f, 0.33f), new Color(0.6f, 0.48f, 0.32f),
            new Color(0.2f, 0.45f, 0.25f), new Color(0.7f, 0.72f, 0.75f), new Color(0.85f, 0.84f, 0.8f), new Color(0.9f, 0.75f, 0.15f),
        };
        enum Sw { WoodL, WoodD, DrumBlue, DrumRed, Rust, Orange, White, HoseGreen, Rubber, Stone, StoneDark, Cardboard, Bottle, Can, Paper, Yellow }
        static Vector2 SwUv(Sw s) { int i = (int)s; return new Vector2((i % 4 + 0.5f) / 4f, (i / 4 + 0.5f) / 4f); }

        static void Box(MeshB b, Vector3 c, Vector3 size, Quaternion rot, Sw sw)
        {
            var h = size * 0.5f; var u = SwUv(sw);
            Vector3 P(float x, float y, float z) => c + rot * Vector3.Scale(h, new Vector3(x, y, z));
            void F(Vector3 a, Vector3 bb, Vector3 cc, Vector3 d) => b.Quad(a, bb, cc, d, u, u, u, u);
            F(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1)); F(P(1, -1, -1), P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1));
            F(P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1)); F(P(1, -1, 1), P(1, -1, -1), P(1, 1, -1), P(1, 1, 1));
            F(P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), P(-1, 1, -1)); F(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1));
        }

        static void Cylinder(MeshB b, Vector3 c, float r, float h, int seg, Sw side, Sw cap, float rTop = -1f)
        {
            if (rTop < 0f) rTop = r;
            var us = SwUv(side); var uc = SwUv(cap);
            for (int i = 0; i < seg; i++)
            {
                float a0 = i / (float)seg * Mathf.PI * 2f, a1 = (i + 1) / (float)seg * Mathf.PI * 2f;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                b.Quad(c + d1 * r, c + d0 * r, c + d0 * rTop + Vector3.up * h, c + d1 * rTop + Vector3.up * h, us, us, us, us);
                if (rTop > 0.001f) b.Tri(c + Vector3.up * h, c + d1 * rTop + Vector3.up * h, c + d0 * rTop + Vector3.up * h, uc);
            }
        }

        static void Torus(MeshB b, Vector3 c, float R, float r, int segR, int segr, Sw sw, float squashY = 0.7f)
        {
            var u = SwUv(sw);
            Vector3 P(int i, int j)
            {
                float a = i / (float)segR * Mathf.PI * 2f, t = j / (float)segr * Mathf.PI * 2f;
                var ring = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                return c + ring * (R + r * Mathf.Cos(t)) + Vector3.up * (r * Mathf.Sin(t) * squashY + r);
            }
            for (int i = 0; i < segR; i++)
                for (int j = 0; j < segr; j++)
                    b.Quad(P(i, j), P(i + 1, j), P(i + 1, j + 1), P(i, j + 1), u, u, u, u);
        }

        static void Rock(MeshB b, Vector3 c, float size, System.Random rng, Sw sw)
        {
            // jittered octahedron-ish (8 faces split into 4 tris each)
            var u = SwUv(sw);
            Vector3 J(Vector3 d) => c + Vector3.Scale(d, new Vector3(size, size * 0.6f, size)) * (0.75f + (float)rng.NextDouble() * 0.5f);
            Vector3[] ax = { J(Vector3.right), J(Vector3.left), J(Vector3.up), J(Vector3.down * 0.3f), J(Vector3.forward), J(Vector3.back) };
            int[,] f = { { 2, 4, 0 }, { 2, 0, 5 }, { 2, 5, 1 }, { 2, 1, 4 }, { 3, 0, 4 }, { 3, 5, 0 }, { 3, 1, 5 }, { 3, 4, 1 } };
            for (int k = 0; k < 8; k++) b.Tri(ax[f[k, 0]], ax[f[k, 1]], ax[f[k, 2]], u);
        }

        static void Pallet(MeshB b, Vector3 c, float yaw, int layers)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            for (int l = 0; l < layers; l++)
            {
                float y0 = l * 0.145f;
                for (int k = -1; k <= 1; k++) Box(b, c + rot * new Vector3(k * 0.5f, y0 + 0.05f, 0), new Vector3(0.1f, 0.1f, 1.2f), rot, Sw.WoodD);     // stringers
                for (int k = 0; k < 7; k++) Box(b, c + rot * new Vector3(0, y0 + 0.12f, -0.54f + k * 0.18f), new Vector3(1.0f, 0.025f, 0.12f), rot, Sw.WoodL); // deck boards
            }
        }

        static void Drum(MeshB b, Vector3 c, Sw col)
        {
            Cylinder(b, c, 0.29f, 0.88f, 14, col, col);
            Cylinder(b, c + Vector3.up * 0.29f, 0.3f, 0.03f, 14, Sw.Rust, Sw.Rust); // rib
            Cylinder(b, c + Vector3.up * 0.58f, 0.3f, 0.03f, 14, Sw.Rust, Sw.Rust);
        }

        static void Cone(MeshB b, Vector3 c)
        {
            Box(b, c + Vector3.up * 0.015f, new Vector3(0.38f, 0.03f, 0.38f), Quaternion.identity, Sw.Rubber);
            Cylinder(b, c + Vector3.up * 0.03f, 0.15f, 0.2f, 12, Sw.Orange, Sw.Orange, 0.11f);
            Cylinder(b, c + Vector3.up * 0.23f, 0.11f, 0.08f, 12, Sw.White, Sw.White, 0.09f);
            Cylinder(b, c + Vector3.up * 0.31f, 0.09f, 0.18f, 12, Sw.Orange, Sw.Orange, 0.02f);
        }

        // grass cards: atlas tile 0 green grass, 1 dry grass, 2 broad-leaf weed, 3 grass with small flowers
        static void Clump(MeshB b, Vector3 c, float w, float h, int tile, System.Random rng, int cards = 3)
        {
            float u0 = (tile % 2) * 0.5f, v0 = (tile / 2) * 0.5f;
            float yaw0 = (float)rng.NextDouble() * 180f;
            for (int k = 0; k < cards; k++)
            {
                var d = Quaternion.Euler(0, yaw0 + k * 180f / cards, 0) * Vector3.right * (w * 0.5f);
                var lean = new Vector3((float)rng.NextDouble() - 0.5f, 0, (float)rng.NextDouble() - 0.5f) * h * 0.25f;
                b.Quad(c - d, c + d, c + d + Vector3.up * h + lean, c - d + Vector3.up * h + lean,
                    new Vector2(u0 + 0.01f, v0 + 0.01f), new Vector2(u0 + 0.49f, v0 + 0.01f), new Vector2(u0 + 0.49f, v0 + 0.49f), new Vector2(u0 + 0.01f, v0 + 0.49f));
            }
        }

        static void Dressing(Transform root, System.Random rng)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var props = new MeshB(); var grass = new MeshB();
            var colliders = new GameObject("DressingColliders").transform; colliders.SetParent(root, false);
            void Col(Vector3 c, Vector3 size, float yaw)
            {
                var go = new GameObject("Col") { layer = Layers.Default };
                go.transform.SetParent(colliders, false);
                go.transform.SetPositionAndRotation(c + Vector3.up * size.y * 0.5f, Quaternion.Euler(0, yaw, 0));
                go.AddComponent<BoxCollider>().size = size;
                go.isStatic = true;
            }

            // --- hand-picked heavy pieces (with colliders), all against a wall / fence, off every route ---
            Pallet(props, new Vector3(-25.2f, 0, 17.4f), 8f, 4); Col(new Vector3(-25.2f, 0, 17.4f), new Vector3(1.05f, 0.6f, 1.25f), 8f);
            Pallet(props, new Vector3(-23.8f, 0, 17.5f), -4f, 2); Col(new Vector3(-23.8f, 0, 17.5f), new Vector3(1.05f, 0.3f, 1.25f), -4f);
            Pallet(props, new Vector3(22.4f, 0, 17.5f), 92f, 3); Col(new Vector3(22.4f, 0, 17.5f), new Vector3(1.25f, 0.45f, 1.05f), 92f);
            foreach (var (p, col) in new[] { (new Vector3(-16.4f, 0, -8.1f), Sw.DrumBlue), (new Vector3(-16.45f, 0, -8.75f), Sw.DrumRed), (new Vector3(-16.4f, 0, -9.4f), Sw.DrumBlue),
                                             (new Vector3(16.5f, 0, -11.3f), Sw.Rust), (new Vector3(17.15f, 0, -11.6f), Sw.DrumRed), (new Vector3(24.6f, 0, 17.6f), Sw.DrumBlue) })
            { Drum(props, p, col); Col(p, new Vector3(0.6f, 0.9f, 0.6f), 0f); }
            // --- light pieces, no colliders ---
            Cone(props, new Vector3(20.6f, 0, 17.7f)); Cone(props, new Vector3(20.6f, 0.2f, 17.7f)); Cone(props, new Vector3(21.1f, 0, 17.8f));
            Torus(props, new Vector3(-14.8f, 0, 17.6f), 0.36f, 0.035f, 18, 6, Sw.HoseGreen); Torus(props, new Vector3(-14.8f, 0.07f, 17.6f), 0.3f, 0.035f, 18, 6, Sw.HoseGreen);
            Torus(props, new Vector3(9.6f, 0, 17.4f), 0.34f, 0.035f, 18, 6, Sw.Rubber);
            // rubbish near the dumpster, the fence and the office
            for (int i = 0, tries = 0; i < 26 && tries < 600; tries++)
            {
                var p = i < 10 ? new Vector3(R(12.5f, 19.5f), 0, R(13.8f, 17.9f)) : new Vector3(R(-27f, 27f), 0, R(17.2f, 18.0f));
                if (i >= 18) p = new Vector3(R(-16.8f, -7.6f), 0, R(-13.8f, -13.3f));
                if (!Clear(p, 0.2f) && i >= 10) continue;
                if ((new Vector2(p.x - 16f, p.z - 15.5f)).sqrMagnitude < 2.6f) continue; // not inside the dumpster
                var rot = Quaternion.Euler(R(-8f, 8f), R(0, 360), R(-8f, 8f));
                switch (rng.Next(4))
                {
                    case 0: Box(props, p + Vector3.up * 0.06f, new Vector3(R(0.18f, 0.32f), R(0.08f, 0.14f), R(0.14f, 0.26f)), rot, Sw.Cardboard); break;
                    case 1: Box(props, p + Vector3.up * 0.035f, new Vector3(0.07f, 0.07f, 0.22f), rot, Sw.Bottle); break;
                    case 2: Box(props, p + Vector3.up * 0.03f, new Vector3(0.065f, 0.12f, 0.065f), Quaternion.Euler(90, R(0, 360), 0), Sw.Can); break;
                    default: Box(props, p + Vector3.up * 0.005f, new Vector3(R(0.2f, 0.3f), 0.01f, R(0.15f, 0.25f)), rot, Sw.Paper); break;
                }
                i++;
            }
            // stones / gravel heaps along the north fence and the office's west wall
            for (int i = 0; i < 70; i++)
            {
                var p = i < 50 ? new Vector3(R(-27f, 27f), 0, R(17.75f, 18.3f)) : new Vector3(R(-16.85f, -16.25f), 0, R(-12.5f, -7.6f));
                if (!Clear(p, 0.1f)) continue;
                Rock(props, p + Vector3.up * 0.02f, R(0.04f, 0.13f), rng, rng.NextDouble() < 0.5 ? Sw.Stone : Sw.StoneDark);
            }

            // --- grass + weeds: fence base, office walls, outer bay walls, curb, cracks in the open lot ---
            int clumps = 0;
            void G(Vector3 p, float s)
            {
                int tile = rng.NextDouble() < 0.55 ? 0 : rng.NextDouble() < 0.5 ? 1 : rng.NextDouble() < 0.6 ? 2 : 3;
                Clump(grass, p, R(0.35f, 0.7f) * s, R(0.22f, 0.5f) * s, tile, rng); clumps++;
            }
            for (int i = 0; i < 260; i++) { var p = new Vector3(R(-27.5f, 27.5f), 0, R(17.85f, 18.35f)); if ((p.x > 13.5f && p.x < 18.5f) || Mathf.Abs(p.x + 12f) < 0.9f) continue; G(p, R(1.0f, 1.6f)); }
            for (int i = 0; i < 40; i++) { var p = new Vector3(R(-16.45f, -16.15f), 0, R(-12.8f, -7.3f)); if (Clear(p, 0.05f)) G(p, R(0.6f, 1f)); }
            for (int i = 0; i < 30; i++) { var p = new Vector3(R(-15.8f, -8.2f), 0, R(-13.4f, -13.15f)); if (Clear(p, 0.05f)) G(p, R(0.6f, 1f)); }
            foreach (float wx in new[] { -11.35f, 11.35f })
                for (int i = 0; i < 18; i++) G(new Vector3(wx + R(-0.12f, 0.12f), 0, R(-0.8f, 8.8f)), R(0.6f, 1f));
            for (int i = 0; i < 70; i++) G(new Vector3(R(-36f, 36f), 0, R(-19.4f, -19.15f)), R(0.5f, 0.9f)); // curb / sidewalk crack line
            for (int i = 0, tries = 0; i < 45 && tries < 900; tries++)
            {
                var p = new Vector3(R(-27f, 37f), 0, R(-12f, 17f));
                if (!Clear(p, 0.3f)) continue;
                G(p, R(0.35f, 0.6f)); i++; // little crack weeds in the open lot
            }

            Directory.CreateDirectory(MeshDir);
            var pm = SaveMesh(props.Build("Dressing_Props"), "Dressing_Props");
            var gm = SaveMesh(grass.Build("Dressing_Grass"), "Dressing_Grass");
            Renderer("DressingProps", root, pm, PaletteMat(), true);
            Renderer("DressingGrass", root, gm, GrassMat(), false);
            Debug.Log($"[Dressing] props {props.v.Count} verts, grass {clumps} clumps ({grass.v.Count} verts), colliders {colliders.childCount}");
        }

        static Mesh SaveMesh(Mesh m, string name)
        {
            string path = $"{MeshDir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) { EditorUtility.CopySerialized(m, existing); Object.DestroyImmediate(m); EditorUtility.SetDirty(existing); return existing; }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static void Renderer(string name, Transform root, Mesh mesh, Material mat, bool shadows)
        {
            var go = new GameObject(name) { layer = Layers.Scenery };
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic);
        }

        static Material PaletteMat()
        {
            string tp = MeshDir + "/dress_palette.png";
            if (!File.Exists(tp))
            {
                var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                for (int i = 0; i < 16; i++) t.SetPixel(i % 4, i / 4, Palette[i]);
                t.Apply();
                File.WriteAllBytes(tp, t.EncodeToPNG()); Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(tp, ImportAssetOptions.ForceUpdate);
                if (AssetImporter.GetAtPath(tp) is TextureImporter ti) { ti.filterMode = FilterMode.Point; ti.mipmapEnabled = false; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport(); }
            }
            var m = GrayboxMaterials.Get("Dress_Palette", Color.white, 0.25f);
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tp)); m.mainTexture = m.GetTexture("_BaseMap");
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material GrassMat()
        {
            string tp = MeshDir + "/dress_grass_atlas.png";
            if (!File.Exists(tp))
            {
                const int S = 512, H = S / 2;
                var px = new Color[S * S];
                var rng = new System.Random(77);
                for (int tile = 0; tile < 4; tile++)
                {
                    int ox = (tile % 2) * H, oy = (tile / 2) * H;
                    int blades = tile == 2 ? 9 : 34;
                    for (int k = 0; k < blades; k++)
                    {
                        float bx = 0.15f + (float)rng.NextDouble() * 0.7f, bh = 0.45f + (float)rng.NextDouble() * 0.5f, lean = ((float)rng.NextDouble() - 0.5f) * 0.35f;
                        float bw = tile == 2 ? 0.09f : 0.018f + (float)rng.NextDouble() * 0.012f;
                        Color c = tile == 1 ? Color.Lerp(new Color(0.55f, 0.5f, 0.25f), new Color(0.7f, 0.62f, 0.32f), (float)rng.NextDouble())
                                : tile == 2 ? Color.Lerp(new Color(0.2f, 0.38f, 0.14f), new Color(0.32f, 0.5f, 0.18f), (float)rng.NextDouble())
                                : Color.Lerp(new Color(0.18f, 0.36f, 0.1f), new Color(0.4f, 0.58f, 0.2f), (float)rng.NextDouble());
                        for (int y = 0; y < H; y++)
                        {
                            float v = y / (float)H; if (v > bh) break;
                            float t01 = v / bh, half = bw * (tile == 2 ? Mathf.Sin(t01 * Mathf.PI) : (1f - t01));
                            float cx = bx + lean * t01 * t01;
                            for (int x = 0; x < H; x++)
                            {
                                float u = x / (float)H;
                                if (Mathf.Abs(u - cx) < half) px[(oy + y) * S + ox + x] = new Color(c.r * (0.7f + 0.3f * t01), c.g * (0.7f + 0.3f * t01), c.b, 1f);
                            }
                        }
                    }
                    if (tile == 3)
                        for (int f = 0; f < 10; f++)
                        {
                            int fx = ox + (int)((0.2f + (float)rng.NextDouble() * 0.6f) * H), fy = oy + (int)((0.4f + (float)rng.NextDouble() * 0.45f) * H);
                            var fc = rng.NextDouble() < 0.5 ? new Color(0.95f, 0.85f, 0.2f) : new Color(0.95f, 0.95f, 0.92f);
                            for (int dy = -4; dy <= 4; dy++) for (int dx = -4; dx <= 4; dx++) if (dx * dx + dy * dy <= 16) px[(fy + dy) * S + fx + dx] = new Color(fc.r, fc.g, fc.b, 1f);
                        }
                }
                var t = new Texture2D(S, S, TextureFormat.RGBA32, true);
                t.SetPixels(px); t.Apply();
                File.WriteAllBytes(tp, t.EncodeToPNG()); Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(tp, ImportAssetOptions.ForceUpdate);
                if (AssetImporter.GetAtPath(tp) is TextureImporter ti) { ti.alphaIsTransparency = true; ti.mipMapsPreserveCoverage = true; ti.alphaTestReferenceValue = 0.5f; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport(); }
            }
            var m = GrayboxMaterials.Get("Dress_Grass", Color.white, 0.1f);
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tp)); m.mainTexture = m.GetTexture("_BaseMap");
            m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", 0f); m.doubleSidedGI = true;
            m.renderQueue = (int)RenderQueue.AlphaTest;
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
