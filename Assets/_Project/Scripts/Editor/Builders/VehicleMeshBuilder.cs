using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CarWash.Editor
{
    /// <summary>
    /// Graybox vehicle bodies: a side profile (z = forward, y = up) extruded across the width → a closed shell with
    /// NO hidden interior faces (overlapping boxes made part of the surface unreachable). Flat shaded; UV2 generated
    /// with Unwrapping.GenerateSecondaryUVSet. Down-facing faces get zero dirt coverage in the init pass.
    /// </summary>
    public static class VehicleMeshBuilder
    {
        public const string Dir = "Assets/_Project/Art/Models/Graybox";

        /// <summary>Counter-clockwise profile (z right, y up), starting front-bottom.</summary>
        public static Vector2[] Profile(string id, out float width)
        {
            switch (id)
            {
                case "V01": width = 1.7f; return new[] { V(1.8f, .3f), V(1.8f, .85f), V(.8f, .95f), V(.3f, 1.45f), V(-1.5f, 1.45f), V(-1.8f, 1.1f), V(-1.8f, .3f) };
                case "V03": width = 1.9f; return new[] { V(2.5f, .35f), V(2.5f, 1f), V(1.4f, 1.1f), V(1f, 1.75f), V(-.2f, 1.75f), V(-.3f, 1.3f), V(-2.5f, 1.3f), V(-2.5f, .35f) };
                case "V04": width = 1.9f; return new[] { V(2.4f, .3f), V(2.4f, .9f), V(1.7f, 1f), V(1.1f, 1.9f), V(-2.3f, 1.9f), V(-2.4f, 1.5f), V(-2.4f, .3f) };
                case "B01": width = 2.4f; return new[] { V(2.6f, 1.3f), V(2.6f, 2.2f), V(1.2f, 2.3f), V(.7f, 3f), V(-1f, 3f), V(-1.6f, 2.3f), V(-2.6f, 2.2f), V(-2.6f, 1.3f) };
                default: width = 1.85f; return new[] { V(2.2f, .3f), V(2.2f, .85f), V(1f, .95f), V(.45f, 1.45f), V(-.9f, 1.45f), V(-1.5f, 1f), V(-2.2f, .95f), V(-2.2f, .3f) };
            }
        }

        static Vector2 V(float z, float y) => new Vector2(z, y);

        public static string PathOf(string id) => $"{Dir}/Vehicle_{id}_Body.asset";

        public static Mesh BuildBody(string id)
        {
            var prof = Profile(id, out float w);
            float hx = w * 0.5f;
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var tris = new List<int>();

            void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 n)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), n) < 0f) { var t = b; b = c; c = t; }
                int i = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                norms.Add(n); norms.Add(n); norms.Add(n);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            }

            // side walls along the profile
            for (int i = 0; i < prof.Length; i++)
            {
                var p0 = prof[i]; var p1 = prof[(i + 1) % prof.Length];
                var d = p1 - p0;
                var n = new Vector3(0f, -d.x, d.y).normalized; // outward for a CCW (z,y) profile: (dy, -dz) mapped to (y,z)
                n = new Vector3(0f, n.y, n.z);
                var a = new Vector3(-hx, p0.y, p0.x); var b = new Vector3(-hx, p1.y, p1.x);
                var c = new Vector3(hx, p1.y, p1.x); var e = new Vector3(hx, p0.y, p0.x);
                Tri(a, b, c, n); Tri(a, c, e, n);
            }
            // caps (star-shaped from the centroid)
            var cen = Vector2.zero; foreach (var p in prof) cen += p; cen /= prof.Length;
            foreach (float sx in new[] { -1f, 1f })
            {
                var n = new Vector3(sx, 0, 0);
                for (int i = 0; i < prof.Length; i++)
                {
                    var p0 = prof[i]; var p1 = prof[(i + 1) % prof.Length];
                    Tri(new Vector3(sx * hx, cen.y, cen.x), new Vector3(sx * hx, p0.y, p0.x), new Vector3(sx * hx, p1.y, p1.x), n);
                }
            }

            var mesh = new Mesh { name = $"Vehicle_{id}_Body" };
            mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            Unwrapping.GenerateSecondaryUVSet(mesh, new UnwrapParam { angleError = 0.08f, areaError = 0.15f, hardAngle = 60f, packMargin = 0.02f });
            mesh.SetUVs(2, new Vector2[mesh.vertexCount]); // UV3 "unreachable" mask (DirtUnwrapPaint): graybox bodies are fully counted

            Directory.CreateDirectory(Dir);
            string path = PathOf(id);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; EditorUtility.SetDirty(mesh); }
            else AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>Wheel positions (object space).</summary>
        public static Vector3[] Wheels(string id, out float radius)
        {
            var prof = Profile(id, out float w);
            float minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var p in prof) { minZ = Mathf.Min(minZ, p.x); maxZ = Mathf.Max(maxZ, p.x); }
            radius = id == "B01" ? 0.9f : 0.35f;
            float x = w * 0.5f, z = (maxZ - minZ) * 0.33f, y = radius;
            return new[] { new Vector3(-x, y, z), new Vector3(x, y, z), new Vector3(-x, y, -z), new Vector3(x, y, -z) };
        }
    }
}
