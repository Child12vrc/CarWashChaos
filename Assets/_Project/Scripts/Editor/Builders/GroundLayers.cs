using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CarWash.Editor
{
    /// <summary>
    /// M4.10 W3: the ground was a stack of thin box slabs (road, lanes, ramps, bay floors, lines, queue marks, puddles…) whose
    /// tops sat 0–2 cm apart, so different materials z-fought (flickered) — worst at a distance and at joints. Run once after
    /// the lot is built:
    ///  - every thin ground-level slab gets a fixed top height by kind: roads 2 cm, lanes / pads 3.5 cm, floors 5 cm
    ///    (≥ 1.5 cm between kinds — resolvable to ~100 m with the FP camera's near 0.08);
    ///  - every painted marking (lines, marks, stripes, puddles) becomes a DecalProjector (flush, cannot z-fight);
    ///  - the solid-colour decal materials are shared per colour (GPU instanced).
    /// Thick things (kerbs, sidewalks, walls) are left alone.
    /// </summary>
    public static class GroundLayers
    {
        public const float RoadTop = 0.025f, LaneTop = 0.05f, FloorTop = 0.05f, PadTop = 0.075f; // > 2 cm apart where they overlap (ClipAudit.Gap); bay floors never overlap lanes
        const string Dir = "Assets/_Project/Art/Generated/Decals/Paint";
        static readonly string[] MarkingWords = { "Line", "Mark", "Stripe", "Puddle", "Crosswalk", "Zebra", "Parking", "Paint", "Visual" };

        public static int Raised, Decaled;

        static bool IsMarking(string n) => MarkingWords.Any(w => n.Contains(w));

        static float TopFor(string n)
        {
            if (n.Contains("Pad")) return PadTop;
            if (n.StartsWith("Floor")) return FloorTop;
            if (n.Contains("Lane") || n.Contains("Ramp") || n.Contains("Lot") || n.Contains("Apron")) return LaneTop;
            return RoadTop;
        }

        public static void Apply(Transform env)
        {
            Raised = Decaled = 0;
            var thin = env.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => r.GetComponent<MeshFilter>() != null && r.GetComponent<MeshFilter>().sharedMesh != null && r.GetComponent<MeshFilter>().sharedMesh.name == "Cube")
                .Where(r => Mathf.Abs(Vector3.Dot(r.transform.up, Vector3.up)) > 0.999f)
                .Where(r => { var b = r.bounds; return b.size.y <= 0.06f && b.max.y <= 0.12f && b.min.y >= -0.1f && Mathf.Max(b.size.x, b.size.z) > 0.05f; })
                .ToList();
            foreach (var r in thin)
            {
                var t = r.transform;
                if (IsMarking(t.name)) { ToDecal(r); continue; }
                float bottom = r.bounds.min.y, top = TopFor(t.name);
                if (top <= bottom + 0.005f) continue;
                var s = t.localScale; var p = t.position;
                float k = (top - bottom) / Mathf.Max(1e-4f, r.bounds.size.y);
                t.localScale = new Vector3(s.x, s.y * k, s.z);
                t.position = new Vector3(p.x, (top + bottom) * 0.5f, p.z);
                Raised++;
            }
            Debug.Log($"[GroundLayers] slabs re-levelled {Raised}, markings → decals {Decaled}");
        }

        static readonly Dictionary<Color32, Material> _mats = new Dictionary<Color32, Material>();

        static void ToDecal(MeshRenderer r)
        {
            var t = r.transform; var b = r.bounds;
            var m = r.sharedMaterial;
            Color c = m != null && m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
            var go = new GameObject("Decal_" + t.name) { layer = t.gameObject.layer };
            go.transform.SetParent(t.parent, false);
            go.transform.SetPositionAndRotation(new Vector3(t.position.x, 0.3f, t.position.z), Quaternion.Euler(90f, t.eulerAngles.y, 0f));
            var d = go.AddComponent<DecalProjector>();
            d.material = PaintMat(c, t.name.Contains("Puddle") ? 0.8f : 0.95f);
            var s = t.lossyScale;
            d.size = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.z), 0.6f);
            d.pivot = new Vector3(0f, 0f, 0.3f);
            d.drawDistance = 140f; d.fadeScale = 0.9f;
            Object.DestroyImmediate(t.gameObject);
            Decaled++;
        }

        /// <summary>A solid-colour decal (8×8 texture of that colour, shared per colour).</summary>
        public static Material PaintMat(Color c, float alpha)
        {
            var key = (Color32)new Color(c.r, c.g, c.b, alpha);
            if (_mats.TryGetValue(key, out var mm) && mm != null) return mm;
            Directory.CreateDirectory(Dir);
            string hex = ColorUtility.ToHtmlStringRGBA(key);
            string texPath = $"{Dir}/paint_{hex}.png", matPath = $"{Dir}/Decal_paint_{hex}.mat";
            if (!File.Exists(texPath))
            {
                var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
                var px = Enumerable.Repeat((Color)key, 64).ToArray(); tex.SetPixels(px); tex.Apply();
                File.WriteAllBytes(texPath, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
                if (AssetImporter.GetAtPath(texPath) is TextureImporter ti) { ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.mipmapEnabled = false; ti.SaveAndReimport(); }
            }
            var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (m == null) { m = new Material(Shader.Find("Shader Graphs/Decal")); AssetDatabase.CreateAsset(m, matPath); }
            var t2 = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (m.HasProperty("Base_Map")) m.SetTexture("Base_Map", t2);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t2);
            if (m.HasProperty("Normal_Blend")) m.SetFloat("Normal_Blend", 0f);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            _mats[key] = m;
            return m;
        }
    }
}
