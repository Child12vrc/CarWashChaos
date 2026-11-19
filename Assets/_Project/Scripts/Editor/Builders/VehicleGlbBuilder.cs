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
        /// <summary>"…_v2.glb" → 2, "…_v3.glb" → 3, no suffix → 1.</summary>
        public static int Revision(string path)
        {
            var m = System.Text.RegularExpressions.Regex.Match(Path.GetFileNameWithoutExtension(path), @"_v(\d+)$");
            return m.Success ? int.Parse(m.Groups[1].Value) : 1;
        }

        /// <summary>Newest model revision the builder understands. v3 = Batch H (merged `Static`, `&lt;Node&gt;_LOD1/_LOD2` siblings,
        /// UV2 kept identical across the body LODs) → LODGroup via LodKit (M4.11).</summary>
        public const int MaxRevision = 3;
        /// <summary>Screen-relative LOD heights for cars (≈ 5 m long, 60° FOV): LOD1 from ~15 m, LOD2 from ~40 m, culled past ~800 m.</summary>
        public static readonly float[] LodHeights = { 0.28f, 0.10f, 0.005f };

        public static string GlbFor(string id)
        {
            if (!Directory.Exists(ModelDir)) return null;
            // newest revision first: "<id>_<name>_v2.glb" (Blender pass with door openings) beats "<id>_<name>.glb"
            var f = Directory.GetFiles(ModelDir, id + "_*.glb")
                .Where(x => AssetDatabase.LoadAssetAtPath<GameObject>(x.Replace('\\', '/')) != null)
                .Where(x => Revision(x) <= MaxRevision)
                .OrderByDescending(Revision).ThenBy(x => x).FirstOrDefault();
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
            int rev = Revision(glbPath);
            // v3: keep the delivered UV2 — Body_LOD1/2 share its layout (and the dirt RTs), a regenerated one would not match
            var mesh = MergedCopy(bodyMf.sharedMesh, $"{MeshDir}/{id}_Body.asset", bodyT, model.transform, keepUv2: rev >= 3);
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
            // M4.11: Body_LOD1 / Body_LOD2 — the dirt material, fed the same textures by the painter (no collider: washing hits LOD0)
            var bodyLods = new List<Renderer>();
            foreach (var lodName in new[] { "Body_LOD1", "Body_LOD2" })
            {
                var lt = model.transform.Find(lodName);
                var lr = lt != null ? lt.GetComponent<MeshRenderer>() : null;
                if (lr == null) continue;
                lr.sharedMaterials = new[] { DirtTestBuilder.VehicleMaterial() };
                bodyLods.Add(lr);
            }
            var pso = new SerializedObject(painter);
            var arr = pso.FindProperty("_lodRenderers");
            arr.arraySize = bodyLods.Count;
            for (int i = 0; i < bodyLods.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = bodyLods[i];
            pso.ApplyModifiedPropertiesWithoutUndo();

            // --- body-coloured parts outside the dirt body ---
            var paint = GrayboxMaterials.Get("VehiclePaint", Color.white, 0.55f);
            foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.transform == bodyT) continue;
                string n = r.name;
                if (!(n.StartsWith("Door") || n.StartsWith("Trunk") || n.StartsWith("Mirror"))) continue; // Trunk_LOD* too (Batch H)
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

            // Batch F #8 airboat: the propeller turns (hub origin, about local Z)
            var prop = VehicleInteriorBuilder.Deep(model.transform, "Propeller");
            if (prop != null) prop.gameObject.AddComponent<Visual.Spinner>().Init(Vector3.forward, 540f);

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
            // top = the body's real height over the glass span, not the whole body's: a cab lower than the box behind it
            // (V05 ice-cream truck 2.10 vs 2.68 m, V07 / B02 2.50 vs 3.0 m) otherwise got an invisible block of air on the
            // cab roof that stopped the spray and the towel short of the box's front face (V05 stuck at 98.9 %)
            float cabTop = glassT != null ? TopOver(mesh, glassB.min.z, glassB.max.z, belt + 0.1f, bodyB.max.y) : bodyB.max.y;
            cabin.center = new Vector3(bodyB.center.x, (belt + cabTop) * 0.5f, glassB.center.z);
            cabin.size = new Vector3(cw, cabTop - belt, Mathf.Min(glassB.size.z, bodyB.size.z));

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
            // M4.11: the LODGroup is built on the vehicle root by VehicleBuilder once the cabin exists (BuildLods)
            return p;
        }

        /// <summary>M4.11: LOD0/1/2 from the model's `_LODn` siblings (v3); the cabin (Interior) is drawn at LOD0 only — from
        /// ~15 m on it is a few pixels behind tinted glass. No `_LODn` nodes (v2) → no LODGroup.</summary>
        public static LODGroup BuildLods(Transform vehicleRoot, string id)
        {
            var interior = vehicleRoot.Find("Interior");
            var cabin = interior != null ? new HashSet<Renderer>(interior.GetComponentsInChildren<Renderer>(true)) : null;
            var lg = LodKit.Build(vehicleRoot, LodHeights, lod0Only: cabin);
            if (lg != null) Debug.Log($"[VehicleGlb] {id}: LODGroup {string.Join(" / ", lg.GetLODs().Select(l => l.renderers.Length))} renderers (cabin {cabin?.Count ?? 0} at LOD0 only)");
            return lg;
        }

        /// <summary>Highest body vertex whose z lies in [z0, z1], clamped to [min, max].</summary>
        public static float TopOver(Mesh m, float z0, float z1, float min, float max)
        {
            float top = float.MinValue;
            foreach (var v in m.vertices) if (v.z >= z0 && v.z <= z1 && v.y > top) top = v.y;
            return top == float.MinValue ? max : Mathf.Clamp(top, min, max);
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
        /// standing and jump apex — the same three as the DevChecksGlb bot (a 2.2 m half-jump eye counted V07's box front over
        /// the cab, which only that fleeting height sees).</summary>
        public static readonly float[] EyeHeights = { 1.1f, 1.65f, 2.75f };
        /// <summary>Towel: data range 1 m + 0.8 m (Towel.ServerUse), aimed ray.</summary>
        public const float TowelReach = 1.8f;

        /// <summary>
        /// UV3.x = 1 on faces the washer or the towel cannot reach from any player eye position (the inside of a cab seen only
        /// through the windscreen, a 3 m monster-truck roof). DirtUnwrapPaint leaves them out of the clean % like the underside, so 100 % is always
        /// reachable. Returns the hidden share of the counted UV2 area.
        /// </summary>
        public static float BakeReachability(Mesh m, Transform bodyT = null, Transform model = null)
        {
            var probe = new GameObject("ReachProbe") { hideFlags = HideFlags.HideAndDontSave };
            var mc = probe.AddComponent<MeshCollider>();
            mc.sharedMesh = m;
            // v2 cars (door openings): every other part (closed doors / trunk, glass, mirrors, bumpers, wheels…) also blocks the
            // view — the jambs, sills and recesses behind closed panels and the cabin seen through the glass are not washable
            // from outside, so they are left out of the clean % (v2 V01 was stuck at 91 %)
            _blockers.Clear();
            if (bodyT != null && model != null)
                foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.transform == bodyT || mf.sharedMesh == null) continue;
                    if (LodKit.IsLodNode(mf.name)) continue; // Body_LOD1 sits on the body: it would "block" every face (LOD0 parts block instead)
                    var mat = bodyT.worldToLocalMatrix * mf.transform.localToWorldMatrix; // into body-local space (= m's space)
                    var go = new GameObject("Blocker") { hideFlags = HideFlags.HideAndDontSave };
                    go.transform.SetPositionAndRotation(mat.GetPosition(), mat.rotation);
                    go.transform.localScale = mat.lossyScale;
                    var bc = go.AddComponent<MeshCollider>(); bc.sharedMesh = mf.sharedMesh;
                    _blockers.Add(bc);
                }
            Physics.SyncTransforms();
            var b = m.bounds;
            var eyes = new List<Vector3>();
            for (int k = 0; k < 48; k++)
            {
                float a = k * Mathf.PI * 2f / 48f;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                // on the footprint RECTANGLE's edge + margin (an ellipse through the bounds put the diagonal eyes inside long
                // boxy bodies: V07's eye sat in its cab and "reached" the box front through the cab cavity)
                float edge = Mathf.Min(Mathf.Abs(dir.x) > 1e-4f ? b.extents.x / Mathf.Abs(dir.x) : float.MaxValue,
                                       Mathf.Abs(dir.z) > 1e-4f ? b.extents.z / Mathf.Abs(dir.z) : float.MaxValue);
                foreach (float margin in new[] { 1.4f, 0.45f })
                {
                    var e = b.center + dir * (edge + margin);
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
                    if (Reach(mc, eyes, s, nn)) reachable++;
                bool reach = reachable >= 3;
                if (reach) continue;
                hidden += ua;
                hiddenTris.Add(i / 3);
            }
            Object.DestroyImmediate(probe);
            foreach (var b2 in _blockers) if (b2 != null) Object.DestroyImmediate(b2.gameObject);
            _blockers.Clear();
            SplitHidden(m, hiddenTris);
            return total > 0f ? hidden / total : 0f;
        }

        /// <summary>A point counts as reachable only if the aimed spray lands this close to it: inside the smallest washer brush
        /// (balance brushMinRadius 0.15 m, soft edge) — panel gaps / bumper lips the ray stops 0.15-0.2 m short of stay dirty
        /// for real, so they are left out of the clean % instead of capping every car at ~99.5 %.</summary>
        public const float SprayTolerance = 0.12f;
        /// <summary>Minimum sine of the angle between the eye ray and the face (≈ 15°) for that eye to count.</summary>
        public const float MinIncidence = 0.26f;
        /// <summary>Towel: minimum sine of the wiping angle (≈ 24°).</summary>
        public const float TowelIncidence = 0.4f;

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

        static readonly List<MeshCollider> _blockers = new List<MeshCollider>();

        static bool Reach(MeshCollider mc, List<Vector3> eyes, Vector3 c, Vector3 faceN)
        {
            bool spray = false;
            foreach (var e in eyes)
            {
                var d = c - e; float dist = d.magnitude;
                if (dist > 6f || (spray && dist > TowelReach)) continue;
                var ray = new Ray(e, d / dist);
                if (!mc.Raycast(ray, out var hit, dist + 0.05f) || (hit.point - c).magnitude > SprayTolerance) continue;
                // seen edge-on only (V05's 2.68 m roof from the 2.75 m jump apex, its box front over the cab from the side): the
                // player can neither see that dirt nor hold the spray there long enough — not counted
                if (Vector3.Dot(-ray.direction, faceN) < MinIncidence) continue;
                // the hit must be on this face's side, not on a parallel wall in front of it: V07's box front sits 2 cm behind
                // the cab's back wall — rays landing on the cab wall were within SprayTolerance and counted it as reachable
                if (Vector3.Dot(hit.normal, faceN) < 0.7f) continue;
                bool blocked = false;
                foreach (var bl in _blockers) if (bl.Raycast(ray, out _, hit.distance - 0.03f)) { blocked = true; break; }
                if (blocked) continue;
                spray = true;
                // the towel has to rub, not just hit: it needs a real angle (V07's
                // side chamfers seen at 15–20° stayed spotted)
                // from the jump apex (0.35 s in the air) only faces looking up (roofs, hoods) can be rubbed — not a vertical strip above
                // head height like V07's box front over the cab
                if (dist <= TowelReach && Vector3.Dot(-ray.direction, faceN) >= TowelIncidence && (e.y < 2.5f || faceN.y > 0.5f)) return true;
            }
            return false;
        }

        /// <summary>Longest body edge the reachability mask works with (m).</summary>
        public const float MaxBakeEdge = 0.3f;

        /// <summary>
        /// v0.9: splits every triangle at the midpoint of its longest edge until no edge is longer than `maxEdge` (midpoints
        /// shared per edge, so the surface stays closed). The reachability mask is per face: the V08 tank's deck is a few
        /// huge triangles that run under the turret — one visible corner made the whole buried part count (stuck at 96 %).
        /// </summary>
        public static void Subdivide(Mesh m, float maxEdge)
        {
            var pos = new List<Vector3>(); m.GetVertices(pos);
            var nrm = new List<Vector3>(); m.GetNormals(nrm);
            var tan = new List<Vector4>(); m.GetTangents(tan);
            var col = new List<Color>(); m.GetColors(col);
            var uv0 = new List<Vector2>(); m.GetUVs(0, uv0);
            var uv1 = new List<Vector2>(); m.GetUVs(1, uv1);
            int n0 = pos.Count;
            bool hn = nrm.Count == n0, ht = tan.Count == n0, hc = col.Count == n0, h0 = uv0.Count == n0, h1 = uv1.Count == n0;
            var mids = new Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (mids.TryGetValue(key, out int k)) return k;
                k = pos.Count;
                pos.Add((pos[a] + pos[b]) * 0.5f);
                if (hn) nrm.Add((nrm[a] + nrm[b]).normalized);
                if (ht) { var t = (tan[a] + tan[b]) * 0.5f; tan.Add(new Vector4(t.x, t.y, t.z, tan[a].w)); }
                if (hc) col.Add((col[a] + col[b]) * 0.5f);
                if (h0) uv0.Add((uv0[a] + uv0[b]) * 0.5f);
                if (h1) uv1.Add((uv1[a] + uv1[b]) * 0.5f);
                mids[key] = k;
                return k;
            }
            var todo = new Stack<(int, int, int)>();
            var src = m.triangles;
            for (int i = src.Length - 3; i >= 0; i -= 3) todo.Push((src[i], src[i + 1], src[i + 2]));
            var outT = new List<int>(src.Length * 2);
            float max2 = maxEdge * maxEdge;
            while (todo.Count > 0)
            {
                var (a, b, c) = todo.Pop();
                float ab = (pos[a] - pos[b]).sqrMagnitude, bc = (pos[b] - pos[c]).sqrMagnitude, ca = (pos[c] - pos[a]).sqrMagnitude;
                float longest = Mathf.Max(ab, Mathf.Max(bc, ca));
                if (longest <= max2) { outT.Add(a); outT.Add(b); outT.Add(c); continue; }
                // split the longest edge, keeping the winding
                if (longest == ab) { int d = Mid(a, b); todo.Push((a, d, c)); todo.Push((d, b, c)); }
                else if (longest == bc) { int d = Mid(b, c); todo.Push((a, b, d)); todo.Push((a, d, c)); }
                else { int d = Mid(c, a); todo.Push((a, b, d)); todo.Push((d, b, c)); }
            }
            if (pos.Count > 65535) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(pos);
            if (hn) m.SetNormals(nrm);
            if (ht) m.SetTangents(tan);
            if (hc) m.SetColors(col);
            if (h0) m.SetUVs(0, uv0);
            if (h1) m.SetUVs(1, uv1);
            m.SetTriangles(outT, 0);
        }

        /// <summary>Copy of the imported body with every submesh merged into submesh 0 (saved as an asset of ours).</summary>
        static Mesh MergedCopy(Mesh src, string path, Transform bodyT = null, Transform model = null, bool keepUv2 = false)
        {
            Directory.CreateDirectory(MeshDir);
            var m = Object.Instantiate(src);
            m.name = Path.GetFileNameWithoutExtension(path);
            var tris = new List<int>();
            for (int s = 0; s < src.subMeshCount; s++) tris.AddRange(src.GetTriangles(s));
            m.subMeshCount = 1;
            m.SetTriangles(tris, 0);
            Subdivide(m, MaxBakeEdge);
            m.RecalculateBounds();
            // ART NOTE (2026-11-18): the delivered TEXCOORD_1 has overlapping / sliver islands (V02: the rear panel shares texels
            // with hood slivers) -> those texels can never read clean. The dirt UV2 of OUR merged copy is regenerated with
            // Unity's non-overlapping, area-proportional unwrapper (the .glb itself is untouched; dirt is procedural anyway).
            // Batch H (v3): the Blender pass checked 0 overlap at every LOD and the body LODs share this layout → keep it.
            if (!keepUv2)
            {
                UnwrapParam.SetDefaults(out var up);
                up.packMargin = 4f / 512f;
                Unwrapping.GenerateSecondaryUVSet(m, up);
            }
            float hidden = BakeReachability(m, bodyT, model);
            Debug.Log($"[VehicleGlb] {m.name}: {hidden * 100f:0.00}% of the counted UV2 area is out of every player's reach → not counted");
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) { EditorUtility.CopySerialized(m, existing); Object.DestroyImmediate(m); EditorUtility.SetDirty(existing); return existing; }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
