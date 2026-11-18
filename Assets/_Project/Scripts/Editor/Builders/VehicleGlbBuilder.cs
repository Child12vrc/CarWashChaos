using System.Collections.Generic;
using System.IO;
using System.Linq;
using CarWash.Core;
using CarWash.Dirt;
using CarWash.Vehicles;
using UnityEditor;
using UnityEngine;

namespace CarWash.Editor
{
    /// <summary>
    /// docs/ART_IMPORT.md "3D Models": builds a vehicle's visual from Art/Models/Generated/Vehicles/&lt;id&gt;_*.glb (glTFast).
    ///  Body      → dirt body: merged into one submesh (DirtPainter unwraps submesh 0 through UV2 = TEXCOORD_1), VehicleDirt
    ///              material, exact MeshCollider on WashSurface (washing raycasts only), tag Dirtable.
    ///  Door_L/Door_R/Trunk → VehicleDoor on the hinge node (pivot = hinge), box collider on Interactable (E target, not in the
    ///              spray mask, so the spray reaches the body behind it); open direction picked so the panel swings outward.
    ///  Socket_Interior → Vehicle interior socket; Socket_Door_* / Socket_Trunk stay as given.
    ///  Physics   → VehicleHull: 2 boxes (lower body up to the belt line + cabin from the Glass bounds) on Vehicle.
    ///  Paint     → body-coloured slots on doors / trunk / mirrors get the VehiclePaint material (tinted with the car colour).
    /// The .glb and its imported sub-assets are never modified; the merged body mesh is a new asset under Prefabs/Vehicles.
    /// </summary>
    public static class VehicleGlbBuilder
    {
        public const string ModelDir = "Assets/_Project/Art/Models/Generated/Vehicles";
        public const string MeshDir = "Assets/_Project/Prefabs/Vehicles/GlbMeshes";

        public class Parts
        {
            public DirtPainter Painter;
            public Bounds Bounds;            // all renderers, vehicle-local
            public Bounds BodyBounds;
            public Transform Interior, Surprise, Driver;
            public readonly List<Renderer> PaintRenderers = new List<Renderer>();
            public readonly List<int> PaintSlots = new List<int>();
        }

        /// <summary>"V02" → ".../V02_sedan.glb", or null (graybox stays).</summary>
        public static string GlbFor(string id)
        {
            if (!Directory.Exists(ModelDir)) return null;
            var f = Directory.GetFiles(ModelDir, id + "_*.glb").OrderBy(x => x).FirstOrDefault();
            if (f == null || AssetDatabase.LoadAssetAtPath<GameObject>(f.Replace('\\', '/')) == null) return null; // glTFast missing → not imported
            return f.Replace('\\', '/');
        }

        public static Parts Build(string glbPath, string id, Transform root)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
            var model = Object.Instantiate(src, root, false);
            model.name = "Model";
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layers.Vehicle;
            var p = new Parts();

            // --- Body: the dirtable surface ---
            var bodyT = model.transform.Find("Body");
            if (bodyT == null) throw new System.Exception(glbPath + ": no Body node");
            var bodyMf = bodyT.GetComponent<MeshFilter>();
            var bodyMr = bodyT.GetComponent<MeshRenderer>();
            var bodyMat = bodyMr.sharedMaterials.FirstOrDefault();
            if (!bodyMf.sharedMesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord1))
                throw new System.Exception(glbPath + ": Body has no TEXCOORD_1 (UV2) — dirt needs it");
            var mesh = MergedCopy(bodyMf.sharedMesh, $"{MeshDir}/{id}_Body.asset");
            bodyMf.sharedMesh = mesh;
            bodyMr.sharedMaterials = new[] { DirtTestBuilder.VehicleMaterial() };
            bodyT.gameObject.layer = Layers.WashSurface;
            bodyT.gameObject.tag = Layers.TagDirtable;
            bodyT.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
            var painter = bodyT.gameObject.AddComponent<DirtPainter>();
            SceneBuilder.Set(painter, "_paintShader", Shader.Find("Hidden/CarWash/DirtUnwrapPaint"));
            SceneBuilder.Set(painter, "_evalShader", Shader.Find("Hidden/CarWash/DirtEval"));
            SceneBuilder.Set(painter, "_body", bodyMf);
            SceneBuilder.Set(painter, "_renderer", bodyMr);
            SceneBuilder.Set(painter, "_foamShell", DirtTestBuilder.FoamShellMaterial());
            p.Painter = painter;
            p.BodyBounds = mesh.bounds;

            // --- body-coloured parts outside the dirt body ---
            var paint = GrayboxMaterials.Get("VehiclePaint", Color.white, 0.55f);
            foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.transform == bodyT) continue;
                string n = r.name;
                if (!(n.StartsWith("Door") || n == "Trunk" || n.StartsWith("Mirror"))) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] == bodyMat) { mats[i] = paint; p.PaintRenderers.Add(r); p.PaintSlots.Add(i); }
                r.sharedMaterials = mats;
            }

            // --- doors / trunk (side doors first: VehicleSpawner puts trash at the side doors by index) ---
            int order = 0;
            foreach (var (node, label) in new[] { ("Door_L", "운전석 문"), ("Door_R", "조수석 문"), ("Trunk", "트렁크") })
            {
                var t = model.transform.Find(node);
                if (t == null) { Debug.LogWarning($"[VehicleGlb] {id}: no {node}"); continue; }
                t.SetSiblingIndex(order++);
                t.gameObject.layer = Layers.Interactable;
                var b = t.GetComponent<MeshFilter>().sharedMesh.bounds;
                var bc = t.gameObject.AddComponent<BoxCollider>();
                bc.center = b.center; bc.size = Vector3.Max(b.size, Vector3.one * 0.08f);
                var door = t.gameObject.AddComponent<VehicleDoor>();
                var so = new SerializedObject(door);
                so.FindProperty("_label").stringValue = label;
                so.FindProperty("_hinge").objectReferenceValue = t;
                so.FindProperty("_openEuler").vector3Value = OpenEuler(t.localPosition, b.center, node == "Trunk");
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // --- physics hull: lower body + cabin boxes ---
            var bodyB = mesh.bounds;
            var glassT = model.transform.Find("Glass");
            var glassB = glassT != null ? glassT.GetComponent<MeshFilter>().sharedMesh.bounds : new Bounds(bodyB.center + Vector3.up * bodyB.extents.y * 0.5f, new Vector3(bodyB.size.x * 0.9f, bodyB.size.y * 0.5f, bodyB.size.z * 0.5f));
            float belt = Mathf.Clamp(glassB.min.y, bodyB.min.y + 0.2f, bodyB.max.y - 0.1f);
            var hull = new GameObject("Hull", typeof(VehicleHull)) { layer = Layers.Vehicle };
            hull.transform.SetParent(root, false);
            var lower = hull.AddComponent<BoxCollider>();
            lower.center = new Vector3(bodyB.center.x, (bodyB.min.y + belt) * 0.5f, bodyB.center.z);
            lower.size = new Vector3(bodyB.size.x, belt - bodyB.min.y, bodyB.size.z);
            var cabin = hull.AddComponent<BoxCollider>();
            float cw = Mathf.Min(glassB.size.x, bodyB.size.x);
            cabin.center = new Vector3(bodyB.center.x, (belt + bodyB.max.y) * 0.5f, glassB.center.z);
            cabin.size = new Vector3(cw, bodyB.max.y - belt, Mathf.Min(glassB.size.z, bodyB.size.z));

            // --- sockets ---
            p.Interior = model.transform.Find("Socket_Interior");
            var doorL = model.transform.Find("Socket_Door_L");
            Vector3 dl = doorL != null ? doorL.localPosition : new Vector3(-bodyB.extents.x, 0.5f, bodyB.extents.z * 0.4f);
            p.Surprise = Socket(root, "Socket_Surprise", new Vector3(0f, bodyB.max.y + 0.2f, bodyB.center.z));
            p.Driver = Socket(root, "Socket_Driver", new Vector3(dl.x * 0.35f, dl.y + 0.4f, dl.z - 0.5f));
            if (p.Interior == null) p.Interior = Socket(root, "Socket_Interior", new Vector3(0f, dl.y + 0.2f, bodyB.center.z));

            var all = new Bounds(bodyB.center, bodyB.size);
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var rb = r.bounds; // root sits at the origin while building → world = vehicle-local
                all.Encapsulate(rb);
            }
            p.Bounds = all;
            return p;
        }

        static Transform Socket(Transform root, string n, Vector3 local)
        {
            var t = new GameObject(n).transform;
            t.SetParent(root, false);
            t.localPosition = local;
            return t;
        }

        /// <summary>
        /// Hinge rotation that swings the panel outward (EditMode tested): side doors around Y so the free edge moves away
        /// from the car's centre line; trunk / tailgate around X so the panel ends up raised and behind (hatch, lid or tailgate).
        /// </summary>
        public static Vector3 OpenEuler(Vector3 hingeLocal, Vector3 panelCenter, bool trunk)
        {
            Vector3 best = Vector3.zero; float bestScore = float.MinValue;
            if (!trunk)
            {
                float side = Mathf.Sign(hingeLocal.x == 0f ? 1f : hingeLocal.x);
                foreach (float a in new[] { 75f, -75f })
                {
                    var q = Quaternion.Euler(0f, a, 0f) * panelCenter;
                    float s = side * (q.x - panelCenter.x);
                    if (s > bestScore) { bestScore = s; best = new Vector3(0f, a, 0f); }
                }
                return best;
            }
            foreach (float a in new[] { 85f, -85f })
            {
                var q = Quaternion.Euler(a, 0f, 0f) * panelCenter;
                float s = q.y - q.z; // up and backwards (rear = -Z)
                if (s > bestScore) { bestScore = s; best = new Vector3(a, 0f, 0f); }
            }
            return best;
        }

        /// <summary>Where a player's eye can be: a ring 1.4 m out from the body (sprayer) and one right at it (towel), crouched,
        /// standing, half-jump and jump apex.</summary>
        public static readonly float[] EyeHeights = { 1.1f, 1.65f, 2.2f, 2.75f };
        /// <summary>Towel: data range 1 m + 0.8 m (Towel.ServerUse), aimed ray.</summary>
        public const float TowelReach = 1.8f;

        /// <summary>
        /// UV3.x = 1 on faces the washer or the towel cannot reach from any player eye position (the inside of a cab seen only
        /// through the windscreen, a 3 m monster-truck roof). DirtUnwrapPaint leaves them out of the clean % like the underside, so 100 % is always
        /// reachable. Returns the hidden share of the counted UV2 area.
        /// </summary>
        public static float BakeReachability(Mesh m)
        {
            var probe = new GameObject("ReachProbe") { hideFlags = HideFlags.HideAndDontSave };
            var mc = probe.AddComponent<MeshCollider>();
            mc.sharedMesh = m;
            Physics.SyncTransforms();
            var b = m.bounds;
            var eyes = new List<Vector3>();
            for (int k = 0; k < 48; k++)
            {
                float a = k * Mathf.PI * 2f / 48f;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                foreach (float margin in new[] { 1.4f, 0.45f })
                {
                    var e = b.center + Vector3.Scale(dir, b.extents + new Vector3(margin, 0f, margin));
                    foreach (float h in EyeHeights) eyes.Add(new Vector3(e.x, h, e.z));
                }
            }
            var v = m.vertices; var n = m.normals; var uv = m.uv2; var tri = m.triangles;
            var hiddenTris = new HashSet<int>();
            float total = 0f, hidden = 0f;
            for (int i = 0; i < tri.Length; i += 3)
            {
                int i0 = tri[i], i1 = tri[i + 1], i2 = tri[i + 2];
                var nn = (n[i0] + n[i1] + n[i2]).normalized;
                if (nn.y < -0.6f) continue; // underside: already not counted
                float ua = Mathf.Abs((uv[i1].x - uv[i0].x) * (uv[i2].y - uv[i0].y) - (uv[i2].x - uv[i0].x) * (uv[i1].y - uv[i0].y)) * 0.5f;
                total += ua;
                var c = (v[i0] + v[i1] + v[i2]) / 3f;
                // counted only if the washer (any eye, 6 m) AND the towel (aimed, reach TowelReach) can get there: water spots
                // appear wherever the spray hits, and only the towel dries them. Sampled at the centroid + 3 points towards the
                // corners: a face that is mostly tucked away (half hidden behind a bumper lip) is left out too.
                int reachable = 0;
                foreach (var s in new[] { c, Vector3.Lerp(c, v[i0], 0.7f), Vector3.Lerp(c, v[i1], 0.7f), Vector3.Lerp(c, v[i2], 0.7f) })
                    if (Reach(mc, eyes, s)) reachable++;
                bool reach = reachable >= 3;
                if (reach) continue;
                hidden += ua;
                hiddenTris.Add(i / 3);
            }
            Object.DestroyImmediate(probe);
            SplitHidden(m, hiddenTris);
            return total > 0f ? hidden / total : 0f;
        }

        /// <summary>A point counts as reachable only if the aimed spray lands this close to it: inside the smallest washer brush
        /// (balance brushMinRadius 0.15 m, soft edge) — panel gaps / bumper lips the ray stops 0.15-0.2 m short of stay dirty
        /// for real, so they are left out of the clean % instead of capping every car at ~99.5 %.</summary>
        public const float SprayTolerance = 0.12f;

        /// <summary>UV3.x mask per FACE: hidden triangles get their own vertex copies (mask 1), so the flag never bleeds into a
        /// counted neighbour through a shared vertex (and no counted face is half-hidden by interpolation).</summary>
        static void SplitHidden(Mesh m, HashSet<int> hiddenTris)
        {
            var pos = new List<Vector3>(); m.GetVertices(pos);
            var nrm = new List<Vector3>(); m.GetNormals(nrm);
            var tan = new List<Vector4>(); m.GetTangents(tan);
            var col = new List<Color>(); m.GetColors(col);
            var uv0 = new List<Vector2>(); m.GetUVs(0, uv0);
            var uv1 = new List<Vector2>(); m.GetUVs(1, uv1);
            var tri = m.triangles;
            int n0 = pos.Count;
            var mask = Enumerable.Repeat(Vector2.zero, n0).ToList();
            foreach (int t in hiddenTris)
                for (int k = 0; k < 3; k++)
                {
                    int src = tri[t * 3 + k];
                    tri[t * 3 + k] = pos.Count;
                    pos.Add(pos[src]); mask.Add(Vector2.right);
                    if (nrm.Count == n0) nrm.Add(nrm[src]);
                    if (tan.Count == n0) tan.Add(tan[src]);
                    if (col.Count == n0) col.Add(col[src]);
                    if (uv0.Count == n0) uv0.Add(uv0[src]);
                    if (uv1.Count == n0) uv1.Add(uv1[src]);
                }
            if (pos.Count > 65535) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(pos);
            if (nrm.Count == pos.Count) m.SetNormals(nrm);
            if (tan.Count == pos.Count) m.SetTangents(tan);
            if (col.Count == pos.Count) m.SetColors(col);
            if (uv0.Count == pos.Count) m.SetUVs(0, uv0);
            if (uv1.Count == pos.Count) m.SetUVs(1, uv1);
            m.SetUVs(2, mask);
            m.SetTriangles(tri, 0);
            m.RecalculateBounds();
        }

        static bool Reach(MeshCollider mc, List<Vector3> eyes, Vector3 c)
        {
            bool spray = false;
            foreach (var e in eyes)
            {
                var d = c - e; float dist = d.magnitude;
                if (dist > 6f || (spray && dist > TowelReach)) continue;
                if (!mc.Raycast(new Ray(e, d / dist), out var hit, dist + 0.05f) || (hit.point - c).magnitude > SprayTolerance) continue;
                spray = true;
                if (dist <= TowelReach) return true;
            }
            return false;
        }

        /// <summary>Copy of the imported body with every submesh merged into submesh 0 (saved as an asset of ours).</summary>
        static Mesh MergedCopy(Mesh src, string path)
        {
            Directory.CreateDirectory(MeshDir);
            var m = Object.Instantiate(src);
            m.name = Path.GetFileNameWithoutExtension(path);
            var tris = new List<int>();
            for (int s = 0; s < src.subMeshCount; s++) tris.AddRange(src.GetTriangles(s));
            m.subMeshCount = 1;
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            // ART NOTE (2026-11-18): the delivered TEXCOORD_1 has overlapping / sliver islands (V02: the rear panel shares texels
            // with hood slivers) -> those texels can never read clean. The dirt UV2 of OUR merged copy is regenerated with
            // Unity's non-overlapping, area-proportional unwrapper (the .glb itself is untouched; dirt is procedural anyway).
            UnwrapParam.SetDefaults(out var up);
            up.packMargin = 4f / 512f;
            Unwrapping.GenerateSecondaryUVSet(m, up);
            float hidden = BakeReachability(m);
            Debug.Log($"[VehicleGlb] {m.name}: {hidden * 100f:0.00}% of the counted UV2 area is out of every player's reach → not counted");
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) { EditorUtility.CopySerialized(m, existing); Object.DestroyImmediate(m); EditorUtility.SetDirty(existing); return existing; }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
