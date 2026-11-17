using System.IO;
using UnityEditor;
using UnityEngine;

namespace CarWash.Editor
{
    /// <summary>Flat-color URP Lit materials for graybox builders (MaterialBuilder lite).</summary>
    public static class GrayboxMaterials
    {
        public const string Dir = "Assets/_Project/Art/Materials/Graybox";

        public static Material Get(string name, Color color, float smoothness = 0.2f)
        {
            Directory.CreateDirectory(Dir);
            string path = $"{Dir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.color = color;
            m.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(m);
            return m;
        }

        public static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat, bool collider = true, int layer = 0)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            if (mat != null && TileMeters.TryGetValue(mat, out var tile)) WorldUv(go.GetComponent<MeshFilter>(), size, tile);
            return go;
        }

        // ---- P6: textured surfaces (docs/ART_IMPORT.md Environment) ----
        public const string EnvTex = "Assets/_Project/Art/Environment/Textures/";
        static readonly System.Collections.Generic.Dictionary<Material, float> TileMeters = new System.Collections.Generic.Dictionary<Material, float>();

        /// <summary>URP Lit with a seamless texture; boxes using it get UVs in metres / tileMeters (no stretching).</summary>
        public static Material Textured(string name, Color tint, string texFile, float tileMeters, float smoothness = 0.2f)
        {
            var m = Get(name, tint, smoothness);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(EnvTex + texFile);
            if (tex == null) return m;  // art not delivered → stays graybox
            m.SetTexture("_BaseMap", tex);
            m.mainTexture = tex;
            m.SetTextureScale("_BaseMap", Vector2.one);
            TileMeters[m] = tileMeters;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Cube mesh copy whose per-face UVs are scaled to the face size in metres / tile.</summary>
        static void WorldUv(MeshFilter mf, Vector3 size, float tile)
        {
            var mesh = Object.Instantiate(mf.sharedMesh);
            mesh.name = "CubeWorldUV";
            var n = mesh.normals; var uv = mesh.uv;
            for (int i = 0; i < uv.Length; i++)
            {
                var a = new Vector3(Mathf.Abs(n[i].x), Mathf.Abs(n[i].y), Mathf.Abs(n[i].z));
                Vector2 dims = a.x > 0.5f ? new Vector2(size.z, size.y) : a.y > 0.5f ? new Vector2(size.x, size.z) : new Vector2(size.x, size.y);
                uv[i] = Vector2.Scale(uv[i], dims / Mathf.Max(0.01f, tile));
            }
            mesh.uv = uv;
            mf.sharedMesh = mesh;
        }
    }
}
