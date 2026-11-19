using System.Collections.Generic;
using System.Linq;
using CarWash.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CarWash.Editor
{
    /// <summary>
    /// M4.10 W1/W3: static checks of the lot scene.
    ///  - Coplanar faces: every face of every box / quad renderer; two faces of different renderers that point the same way,
    ///    lie ≤ <see cref="Gap"/> apart and overlap (area) z-fight (flicker) → listed.
    ///  - Buried props: a prop / decoration renderer whose bounds sink more than 3 cm into a solid collider it does not own.
    ///  - World text: every TextMesh must be depth-tested (WorldTextDepth + CarWash/WorldText).
    /// Used by the EditMode tests and the menu CarWash/Audit/Clipping (logs the list).
    /// </summary>
    public static class ClipAudit
    {
        public const float Gap = 0.02f, SmallGap = 0.005f;

        struct Face { public Renderer r; public Vector3 c, n, u, v; public float hu, hv; }

        static IEnumerable<Face> Faces(Renderer r)
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) yield break;
            string m = mf.sharedMesh.name;
            var t = r.transform;
            if (m == "Cube")
            {
                var s = t.lossyScale; var h = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)) * 0.5f;
                Vector3 X = t.right, Y = t.up, Z = t.forward;
                yield return new Face { r = r, c = t.position + X * h.x, n = X, u = Y, v = Z, hu = h.y, hv = h.z };
                yield return new Face { r = r, c = t.position - X * h.x, n = -X, u = Y, v = Z, hu = h.y, hv = h.z };
                yield return new Face { r = r, c = t.position + Y * h.y, n = Y, u = X, v = Z, hu = h.x, hv = h.z };
                yield return new Face { r = r, c = t.position - Y * h.y, n = -Y, u = X, v = Z, hu = h.x, hv = h.z };
                yield return new Face { r = r, c = t.position + Z * h.z, n = Z, u = X, v = Y, hu = h.x, hv = h.y };
                yield return new Face { r = r, c = t.position - Z * h.z, n = -Z, u = X, v = Y, hu = h.x, hv = h.y };
            }
            else if (m == "Quad")
            {
                var s = t.lossyScale;
                yield return new Face { r = r, c = t.position, n = -t.forward, u = t.right, v = t.up, hu = Mathf.Abs(s.x) * 0.5f, hv = Mathf.Abs(s.y) * 0.5f };
            }
        }

        /// <summary>Overlap area of two coplanar rectangles (approximated by sampling face B's grid inside face A).</summary>
        static float Overlap(Face a, Face b)
        {
            const int N = 8; int inside = 0;
            for (int i = 0; i < N; i++) for (int j = 0; j < N; j++)
            {
                var p = b.c + b.u * b.hu * ((i + 0.5f) / N * 2f - 1f) + b.v * b.hv * ((j + 0.5f) / N * 2f - 1f);
                var d = p - a.c;
                if (Mathf.Abs(Vector3.Dot(d, a.u)) <= a.hu + 1e-4f && Mathf.Abs(Vector3.Dot(d, a.v)) <= a.hv + 1e-4f) inside++;
            }
            return inside / (float)(N * N) * (4f * b.hu * b.hv);
        }

        public static List<string> CoplanarFaces(IEnumerable<GameObject> roots)
        {
            var rs = roots.SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true)).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            var faces = rs.SelectMany(Faces).Where(f => f.hu * f.hv > 1e-5f).ToList();
            // bucket by (rounded) normal + plane offset so we only compare near-coplanar candidates
            var buckets = new Dictionary<(int, int, int, int), List<Face>>();
            (int, int, int, int) Key(Face f, int dOff)
            {
                var n = f.n;
                return (Mathf.RoundToInt(n.x * 50), Mathf.RoundToInt(n.y * 50), Mathf.RoundToInt(n.z * 50), Mathf.FloorToInt(Vector3.Dot(f.c, n) / 0.05f) + dOff);
            }
            foreach (var f in faces) { var k = Key(f, 0); if (!buckets.TryGetValue(k, out var l)) buckets[k] = l = new List<Face>(); l.Add(f); }
            var hits = new HashSet<string>();
            foreach (var f in faces)
                for (int o = -1; o <= 1; o++)
                {
                    if (!buckets.TryGetValue(Key(f, o), out var l)) continue;
                    foreach (var g in l)
                    {
                        if (g.r == f.r || g.r.GetInstanceID() < f.r.GetInstanceID()) continue;
                        if (Vector3.Dot(f.n, g.n) < 0.999f) continue;
                        if (f.r.sharedMaterial == g.r.sharedMaterial) continue;          // identical pixels: no visible flicker
                        if (f.n.y < -0.9f && f.c.y < 0.25f) continue;                    // bottoms resting on the ground: never seen
                        float dist = Mathf.Abs(Vector3.Dot(g.c - f.c, f.n));
                        if (dist > Gap) continue;
                        float area = Mathf.Max(Overlap(f, g), Overlap(g, f));
                        if (area < 0.0004f) continue;
                        if (area < 0.25f && dist > SmallGap) continue;              // small raised details (screens, buttons) are fine unless almost flush
                        hits.Add($"{Path(f.r.transform)} ↔ {Path(g.r.transform)}  gap {dist * 100f:0.0} cm, overlap {area:0.###} m², normal {f.n:F2}");
                    }
                }
            return hits.OrderBy(s => s).ToList();
        }

        /// <summary>Decoration / prop renderers sunk &gt; 3 cm into a solid collider of another object.</summary>
        public static List<string> Buried(IEnumerable<GameObject> roots, IEnumerable<Transform> props)
        {
            var solids = roots.SelectMany(r => r.GetComponentsInChildren<Collider>(true))
                .Where(c => !c.isTrigger && c.enabled && c.gameObject.activeInHierarchy && c.gameObject.layer != Layers.PlayBounds && !(c is MeshCollider)).ToArray();
            var list = new List<string>();
            foreach (var p in props)
            {
                if (Mounted.Any(m => p.name.Contains(m))) continue; // hung on a wall / pillar on purpose
                foreach (var r in p.GetComponentsInChildren<Renderer>(true))
                {
                    var b = r.bounds;
                    if (Mathf.Max(b.size.x, b.size.z) > 8f) continue;   // combined dressing meshes span the lot
                    foreach (var c in solids)
                    {
                        if (c.transform.IsChildOf(p) || p.IsChildOf(c.transform)) continue;
                        if (c.name == "Ground" || c.name.StartsWith("Ground") || c.name == "Floor") continue; // standing on the ground is fine
                        var cb = c.bounds;
                        if (!cb.Intersects(b)) continue;
                        // penetration along the smallest axis of the overlap box
                        var mn = Vector3.Max(b.min, cb.min); var mx = Vector3.Min(b.max, cb.max); var ov = mx - mn;
                        float pen = Mathf.Min(ov.x, Mathf.Min(ov.y, ov.z));
                        if (pen > 0.03f && Physics.ComputePenetration(c, c.transform.position, c.transform.rotation, BoxOf(r), r.bounds.center, Quaternion.identity, out _, out float depth) && depth > 0.03f)
                            list.Add($"{Path(r.transform)} in {Path(c.transform)} by {depth * 100f:0} cm");
                    }
                }
            }
            DisposeProbes();
            return list.Distinct().ToList();
        }

        /// <summary>Props that are meant to touch / sink slightly into what they hang on.</summary>
        public static readonly string[] Mounted = { "FoamBrushHolder", "HoseReelDrum", "Cctv", "ElectricBox", "Pipes", "Extinguisher", "BoomArm", "Parasol" }; // Parasol: the shade overhangs (its AABB is not its shape)

        static BoxCollider _probe;

        /// <summary>
        /// v0.9: removes every audit probe, including ones leaked by earlier domain reloads (HideAndDontSave survives them).
        /// A probe left behind is an invisible 0.76 m box in the editor's physics scene: LotNavMesh baked it as a walkable
        /// floor at y 0.40 over the lot, so critters walked on air and fell to the −2 m catch floor.
        /// </summary>
        public static void DisposeProbes()
        {
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                if (go != null && go.name == "ClipAuditProbe" && !EditorUtility.IsPersistent(go)) Object.DestroyImmediate(go);
            _probe = null;
        }
        static Collider BoxOf(Renderer r)
        {
            if (_probe == null) { var go = new GameObject("ClipAuditProbe") { hideFlags = HideFlags.HideAndDontSave }; _probe = go.AddComponent<BoxCollider>(); }
            _probe.size = r.bounds.size * 0.98f; _probe.center = Vector3.zero;
            return _probe;
        }

        /// <summary>World TextMeshes that are not depth-tested (would show through meshes).</summary>
        public static List<string> UnsafeText(IEnumerable<GameObject> roots)
        {
            var list = new List<string>();
            foreach (var tm in roots.SelectMany(r => r.GetComponentsInChildren<TextMesh>(true)))
            {
                var d = tm.GetComponent<Visual.WorldTextDepth>();
                var sh = d != null ? (Shader)new SerializedObject(d).FindProperty("_shader").objectReferenceValue : null;
                if (sh == null || sh.name != "CarWash/WorldText") list.Add(Path(tm.transform));
            }
            return list;
        }

        public static string Path(Transform t) { var s = t.name; while (t.parent != null) { t = t.parent; s = t.name + "/" + s; } return s; }

        [MenuItem("CarWash/Audit/Clipping + world text (SudsyLot)")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(SceneNames.Path(SceneNames.SudsyLot), OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var cop = CoplanarFaces(roots);
                Debug.Log($"[AUDIT] coplanar faces ≤{Gap * 100:0} cm: {cop.Count}\n" + string.Join("\n", cop.Take(200)));
                var props = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Where(t => t.name.StartsWith("SW_") || t.parent != null && t.parent.name == "Dressing").ToArray();
                var bur = Buried(roots, props);
                Debug.Log($"[AUDIT] buried props: {bur.Count}\n" + string.Join("\n", bur.Take(200)));
                var txt = UnsafeText(roots);
                Debug.Log($"[AUDIT] world text not depth-tested: {txt.Count}\n" + string.Join("\n", txt));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
