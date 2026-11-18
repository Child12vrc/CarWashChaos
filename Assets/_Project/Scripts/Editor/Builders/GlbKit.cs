using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CarWash.Editor
{
    /// <summary>
    /// docs/ART_IMPORT.md "3D Models" helpers shared by the prop / tool / critter builders: find a .glb, drop an instance of it
    /// into a prefab as "Model", swap its glTF materials for URP Lit copies (so MaterialPropertyBlock _BaseColor tints — wet
    /// critters, heavy crates — work and the SRP batcher groups them), and build simple box / convex colliders from it.
    /// The .glb files and their imported sub-assets are never modified; generated meshes/materials are our own assets.
    /// </summary>
    public static class GlbKit
    {
        public const string Root = "Assets/_Project/Art/Models/Generated";
        public const string MatDir = "Assets/_Project/Art/Materials/Glb";
        public const string MeshDir = "Assets/_Project/Prefabs/GlbMeshes";

        /// <summary>"Props", "P_crate" → path, or null when missing / not imported (glTFast absent) → graybox stays.</summary>
        public static string Find(string category, string file)
        {
            string p = $"{Root}/{category}/{file}.glb";
            return File.Exists(p) && AssetDatabase.LoadAssetAtPath<GameObject>(p) != null ? p : null;
        }

        /// <summary>Instance of the .glb under `parent` named "Model", every node on `layer`, materials converted.</summary>
        public static GameObject Model(string path, Transform parent, int layer, Vector3 localPos = default, float scale = 1f, System.Func<Material, Material> remap = null, bool convert = true)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var model = Object.Instantiate(src, parent, false);
            model.name = "Model";
            model.transform.localPosition = localPos;
            model.transform.localScale = Vector3.one * scale;
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) { if (convert) mats[i] = Lit(mats[i]); if (remap != null) mats[i] = remap(mats[i]); }
                r.sharedMaterials = mats;
                r.shadowCastingMode = ShadowCastingMode.On;
            }
            return model;
        }

        /// <summary>URP Lit copy of a glTFast material (baseColorFactor, metallic, roughness, alpha blend) — one asset per name.</summary>
        public static Material Lit(Material src)
        {
            if (src == null || src.shader.name.StartsWith("Universal Render Pipeline")) return src;
            Directory.CreateDirectory(MatDir);
            string path = $"{MatDir}/{Safe(src.name)}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            Color c = src.HasProperty("baseColorFactor") ? src.GetColor("baseColorFactor") : src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : Color.white;
            float rough = src.HasProperty("roughnessFactor") ? src.GetFloat("roughnessFactor") : 0.7f;
            float metal = src.HasProperty("metallicFactor") ? src.GetFloat("metallicFactor") : 0f;
            m.SetColor("_BaseColor", c); m.color = c;
            m.SetFloat("_Smoothness", Mathf.Clamp(1f - rough, 0f, 0.85f));
            m.SetFloat("_Metallic", metal);
            var tex = src.HasProperty("baseColorTexture") ? src.GetTexture("baseColorTexture") : null;
            if (tex != null) { m.SetTexture("_BaseMap", tex); m.mainTexture = tex; }
            bool blend = c.a < 0.99f || src.renderQueue >= (int)RenderQueue.Transparent;
            m.SetFloat("_Surface", blend ? 1f : 0f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", blend ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
            m.SetFloat("_DstBlend", blend ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            m.SetFloat("_ZWrite", blend ? 0f : 1f);
            m.SetOverrideTag("RenderType", blend ? "Transparent" : "Opaque");
            if (blend) m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); else m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = blend ? (int)RenderQueue.Transparent : -1;
            m.SetFloat("_Cull", blend ? 0f : 2f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Darker / tinted copy of a converted material (heavy crate etc.).</summary>
        public static Material Tinted(Material m, string suffix, Color mul)
        {
            string path = $"{MatDir}/{Safe(m.name)}_{suffix}.mat";
            var t = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (t == null) { t = new Material(m); AssetDatabase.CreateAsset(t, path); }
            t.CopyPropertiesFromMaterial(m);
            var c = m.GetColor("_BaseColor") * mul; c.a = m.GetColor("_BaseColor").a;
            t.SetColor("_BaseColor", c); t.color = c;
            EditorUtility.SetDirty(t);
            return t;
        }

        static string Safe(string n) => new string(n.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());

        /// <summary>Bounds of every renderer under `model`, in `space` local coordinates.</summary>
        public static Bounds LocalBounds(GameObject model, Transform space, System.Func<Transform, bool> filter = null)
        {
            bool any = false; var b = new Bounds();
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || (filter != null && !filter(mf.transform))) continue;
                var mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = space.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            return b;
        }

        /// <summary>Every vertex of the model (optionally only nodes passing `filter`) as one point-cloud mesh asset in `space`
        /// coordinates → a convex MeshCollider (PhysX cooks the hull).</summary>
        public static Mesh HullMesh(GameObject model, Transform space, string assetName, System.Func<Transform, bool> filter = null)
        {
            var pts = new List<Vector3>();
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || (filter != null && !filter(mf.transform))) continue;
                foreach (var v in mf.sharedMesh.vertices) pts.Add(space.InverseTransformPoint(mf.transform.TransformPoint(v)));
            }
            // dedupe to a 1 cm grid (cooking speed) and keep it a valid triangle list for the asset
            pts = pts.GroupBy(p => Vector3Int.RoundToInt(p * 100f)).Select(g => g.First()).ToList();
            while (pts.Count % 3 != 0) pts.Add(pts[0]);
            Directory.CreateDirectory(MeshDir);
            string path = $"{MeshDir}/{assetName}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); }
            mesh.Clear();
            mesh.name = assetName;
            mesh.SetVertices(pts);
            mesh.SetTriangles(Enumerable.Range(0, pts.Count).ToArray(), 0);
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        public static MeshCollider Convex(GameObject on, Mesh hull)
        {
            var mc = on.AddComponent<MeshCollider>();
            mc.sharedMesh = hull;
            mc.convex = true;
            return mc;
        }

        public static BoxCollider Box(GameObject on, Bounds b)
        {
            var bc = on.AddComponent<BoxCollider>();
            bc.center = b.center; bc.size = b.size;
            return bc;
        }

        public static Transform Node(GameObject model, string name) =>
            model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
    }
}
