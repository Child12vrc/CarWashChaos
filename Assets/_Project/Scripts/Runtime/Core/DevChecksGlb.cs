using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CarWash.Dirt;
using CarWash.Player;
using CarWash.Tools;
using CarWash.Vehicles;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// GLB vehicle check, client side (-autotest-glb; pairs with DevGlbScenario on the host). For V01/V02/V03 in the bays:
    ///  1) doors — aim at Door_L, Door_R, Trunk in turn and press E: exactly that panel must open (screenshot), then close;
    ///  2) wash — pressure washer from 8 stands round the car, aiming at every patch of the body (jump shots for high,
    ///     upward-facing patches such as the pickup roof), then the towel for the water spots, until the server's clean %
    ///     reaches 100 % (or stops improving). Logged as [AUTOTEST] glb:.
    /// </summary>
    public static class DevChecksGlb
    {
        static void Log(string m) => DevAutopilot.Log("glb: " + m);

        public static IEnumerator Run(PlayerNet me)
        {
            var input = me.GetComponent<PlayerInputReader>();
            input.scripted = true;
            var done = new HashSet<string>();
            float t0 = Time.time;
            while (done.Count < Expected.Length && Time.time - t0 < 2400f)
            {
                var car = Object.FindObjectsByType<Vehicle>(FindObjectsSortMode.None)
                    .Where(v => v.IsSpawned && v.State.Value == VehicleState.Washing && v.Stationary && v.transform.Find("Model") != null && !done.Contains(v.DataId))
                    .OrderBy(v => v.DataId).FirstOrDefault();
                if (car == null) { yield return new WaitForSeconds(0.5f); continue; }
                done.Add(car.DataId);
                Log($"{car.DataId} parked in bay {car.BayIndex.Value + 1}");
                yield return Doors(me, input, car);
                yield return Wash(me, input, car);
                yield return Finish(me, input, car);
            }
            Log($"cars checked: {string.Join(", ", done)}");
            Log("DONE");
            input.scripted = false;
        }

        static readonly string[] Expected = ExpectedCars();
        static string[] ExpectedCars()
        {
            var a = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(a, "-glb-cars");
            return i >= 0 && i + 1 < a.Length ? a[i + 1].Split(',') : new[] { "V01", "V02", "V03", "V04", "B01" };
        }

        /// <summary>DONE on the car's bay button → inspection → it leaves (the host then brings the next batch).</summary>
        static IEnumerator Finish(PlayerNet me, PlayerInputReader input, Vehicle car)
        {
            var btn = Object.FindObjectsByType<Day.BayButton>(FindObjectsSortMode.None).FirstOrDefault(b => b.BayIndex == car.BayIndex.Value);
            if (btn == null) yield break;
            me.Motor.Teleport(btn.transform.position + new Vector3(-1.3f, -1.25f, 0f));
            yield return new WaitForSeconds(0.4f);
            me.Look.LookAt(btn.transform.position);
            yield return new WaitForSeconds(0.3f);
            input.sInteract = true;
            float tw = Time.time;
            while (car != null && car.State.Value == VehicleState.Washing && Time.time - tw < 4f) yield return null;
            Log($"{car?.DataId} DONE → {(car != null ? car.State.Value.ToString() : "gone")}");
            me.Motor.Teleport(new Vector3(-3.5f, 0.05f, -10f));
            yield return new WaitForSeconds(0.5f);
        }

        // ---------------- doors ----------------
        static IEnumerator Doors(PlayerNet me, PlayerInputReader input, Vehicle car)
        {
            var inter = me.GetComponent<PlayerInteractor>();
            var doors = car.Doors;
            var results = new List<string>();
            foreach (var door in doors)
            {
                var box = door.GetComponent<BoxCollider>();
                var target = box != null ? box.bounds.center : door.transform.position;
                var outward = door.IsTrunk ? -car.transform.forward : car.transform.right * Mathf.Sign(car.transform.InverseTransformPoint(target).x);
                var stand = target + outward * 1.35f; stand.y = 0.05f;
                me.Motor.Teleport(stand);
                yield return new WaitForSeconds(0.35f);
                me.Look.LookAt(target);
                yield return new WaitForSeconds(0.25f);
                string prompt = inter.PromptText();
                var before = doors.Select(d => d.Open.Value).ToArray();
                input.sInteract = true;
                float tw = Time.time;
                while (!door.Open.Value && Time.time - tw < 2f) yield return null;
                yield return new WaitForSeconds(0.5f);
                var opened = doors.Where((d, i) => d.Open.Value && !before[i]).Select(d => d.name).ToArray();
                bool ok = opened.Length == 1 && opened[0] == door.name;
                results.Add($"{door.name}: prompt='{prompt}' opened=[{string.Join(",", opened)}] {(ok ? "OK" : "WRONG")}");
                me.Motor.Teleport(stand + outward * 0.8f); // step back from the swinging panel
                yield return new WaitForSeconds(0.2f);
            }
            // overview with everything open
            var c = car.transform;
            me.Motor.Teleport(c.position - c.forward * 4.6f + c.right * 1.6f + Vector3.up * 0.05f);
            yield return new WaitForSeconds(0.4f);
            me.Look.LookAt(c.position + Vector3.up * 0.9f);
            yield return new WaitForSeconds(0.5f);
            ScreenCapture.CaptureScreenshot($"autotest_glb_{car.DataId}_doors.png");
            yield return new WaitForSeconds(0.3f);
            Log($"{car.DataId} doors: {string.Join(" | ", results)}");
            // close them again (aimed one by one)
            foreach (var door in doors)
            {
                if (!door.Open.Value) continue;
                var box = door.GetComponent<BoxCollider>();
                var target = box != null ? box.bounds.center : door.transform.position;
                var outward = door.IsTrunk ? -car.transform.forward : car.transform.right * Mathf.Sign(car.transform.InverseTransformPoint(door.transform.position).x);
                var stand = door.transform.position + outward * 1.9f; stand.y = 0.05f;
                me.Motor.Teleport(stand);
                yield return new WaitForSeconds(0.3f);
                me.Look.LookAt(target);
                yield return new WaitForSeconds(0.2f);
                input.sInteract = true;
                float tw = Time.time;
                while (door.Open.Value && Time.time - tw < 1.5f) yield return null;
            }
            Log($"{car.DataId} doors closed again: {doors.All(d => !d.Open.Value)}");
        }

        static IEnumerator Equip<T>(PlayerNet me, PlayerInputReader input, Vector3 near) where T : ToolBase
        {
            var tools = me.GetComponent<PlayerTools>();
            var inv = me.GetComponent<PlayerInventory>();
            // washers hang on their bay's hose reel (HoseTether.Anchor): judge them by the reel, not by where they lie
            Vector3 Home(T x) { var h = x.GetComponent<HoseTether>(); return h != null ? h.Anchor.Value : x.transform.position; }
            var tool = Object.FindObjectsByType<T>(FindObjectsSortMode.None).Where(x => !x.IsHeld).OrderBy(x => (Home(x) - near).sqrMagnitude).FirstOrDefault();
            for (int s = 0; s < PlayerInventory.SlotCount; s++)
            {
                if (!(inv.Get(s) is T held)) continue;
                if (tool == null || (Home(held) - near).sqrMagnitude <= (Home(tool) - near).sqrMagnitude) { input.sSlot = s; yield return new WaitForSeconds(0.4f); yield break; }
                input.sSlot = s; yield return new WaitForSeconds(0.3f);
                input.sDrop = true; yield return new WaitForSeconds(0.5f); // the wrong bay's washer
            }
            if (tool == null) { Log($"no free {typeof(T).Name}"); yield break; }
            // a washer in hand would yank us back to its reel: put it away (empty slot) before walking to another tool
            if (tools.Equipped is PressureWasher && typeof(T) != typeof(PressureWasher))
                for (int s = 0; s < PlayerInventory.SlotCount; s++)
                    if (inv.Get(s) == null) { input.sSlot = s; yield return new WaitForSeconds(0.4f); break; }
            var tp = tool.transform.position;
            me.Motor.Teleport(new Vector3(tp.x, 0.05f, tp.z) + Vector3.ProjectOnPlane(near - tp, Vector3.up).normalized * 1.1f);
            yield return new WaitForSeconds(0.35f);
            var col = tool.GetComponent<Collider>();
            me.Look.LookAt(col != null && col.enabled ? col.bounds.center : tool.transform.position); // GLB: root = grip (towel edge)
            yield return new WaitForSeconds(0.3f);
            input.sInteract = true;
            yield return new WaitForSeconds(0.8f);
            Log($"equipped {typeof(T).Name}: {tools.Equipped is T}");
        }

        // ---------------- washing ----------------
        /// <summary>A sample on the body: world point + its UV2 (dirt texel) and every (stand, eye height) it is visible from.</summary>
        class Pt { public Vector3 p; public Vector2 uv; public List<(int stand, int h)> from = new List<(int, int)>(); }

        // standing / crouched / jump apex (VehicleGlbBuilder.BakeReachability uses the same heights)
        static readonly float[] Heights = { 1.65f, 1.1f, 2.75f };

        /// <summary>Area-weighted samples over every counted face (not the underside, not baked-unreachable), ~spacing apart.</summary>
        static List<Pt> Points(DirtNetSync sync, float spacing)
        {
            var mf = sync.Painter.GetComponent<MeshFilter>();
            var mesh = mf.sharedMesh; var t = mf.transform;
            var v = mesh.vertices; var nrm = mesh.normals; var tri = mesh.triangles; var uv = mesh.uv2;
            var hidden = new List<Vector2>(); mesh.GetUVs(2, hidden);
            var rng = new System.Random(7);
            var cells = new Dictionary<Vector3Int, Pt>();
            var extra = new List<Pt>(); // every face gets its centroid too (small faces in a shared grid cell were never checked)
            for (int i = 0; i < tri.Length; i += 3)
            {
                int i0 = tri[i], i1 = tri[i + 1], i2 = tri[i + 2];
                if ((nrm[i0] + nrm[i1] + nrm[i2]).normalized.y < -0.6f) continue;
                if (hidden.Count == v.Length && hidden[i0].x > 0.5f && hidden[i1].x > 0.5f && hidden[i2].x > 0.5f) continue;
                Vector3 a = t.TransformPoint(v[i0]), b = t.TransformPoint(v[i1]), c = t.TransformPoint(v[i2]);
                float area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                extra.Add(new Pt { p = (a + b + c) / 3f, uv = (uv[i0] + uv[i1] + uv[i2]) / 3f });
                int count = Mathf.Clamp(Mathf.CeilToInt(area / (spacing * spacing)) * 3, 3, 600);
                for (int k = 0; k < count; k++)
                {
                    float r1 = (float)rng.NextDouble(), r2 = (float)rng.NextDouble();
                    if (r1 + r2 > 1f) { r1 = 1f - r1; r2 = 1f - r2; }
                    var p = a + (b - a) * r1 + (c - a) * r2;
                    var key = Vector3Int.FloorToInt(p / spacing);
                    if (!cells.ContainsKey(key)) cells[key] = new Pt { p = p, uv = uv[i0] + (uv[i1] - uv[i0]) * r1 + (uv[i2] - uv[i0]) * r2 };
                }
            }
            return cells.Values.Concat(extra).ToList();
        }

        /// <summary>Every spot within 5 m of the car where a player capsule fits (bay walls, the car's hull) — 0.35 m grid.</summary>
        static Vector3[] Grid(Vehicle car)
        {
            var c = car.transform;
            var list = new List<Vector3>();
            int mask = (1 << Layers.Default) | (1 << Layers.Vehicle) | (1 << Layers.Prop);
            for (float x = -5f; x <= 5f; x += 0.35f)
                for (float z = -6f; z <= 6f; z += 0.35f)
                {
                    var p = c.position + c.right * x + c.forward * z;
                    // capsule bottom 0.5 m: the M4.10 ground slabs (bay floors 5 cm) must not count as obstacles
                    if (Physics.CheckCapsule(new Vector3(p.x, 0.5f, p.z), new Vector3(p.x, 1.6f, p.z), 0.36f, mask, QueryTriggerInteraction.Ignore)) continue;
                    list.Add(new Vector3(p.x, 0.05f, p.z));
                }
            return list.ToArray();
        }

        /// <summary>Fill Pt.from with up to 6 (stand, height) whose eye sees the point within reach, nearest stands first.</summary>
        static int Plan(Vehicle car, List<Pt> points, Vector3[] stands, float reach)
        {
            var body = car.Dirt.Painter.GetComponent<Collider>();
            int unseen = 0;
            int rFar = 0, rMiss = 0, rBlocked = 0; var blockers = new Dictionary<string, int>();
            foreach (var pt in points)
            {
                pt.from.Clear();
                var near = Enumerable.Range(0, stands.Length).Where(s => (stands[s] - pt.p).sqrMagnitude < reach * reach + 9f)
                    .OrderBy(s => (stands[s] - pt.p).sqrMagnitude).ToArray();
                for (int h = 0; h < Heights.Length && pt.from.Count < 6; h++)
                    foreach (int s in near)
                    {
                        if (pt.from.Count >= 6) break;
                        var eye = stands[s] + Vector3.up * Heights[h];
                        var d = pt.p - eye; float dist = d.magnitude;
                        if (dist > reach || dist < 0.3f) { rFar++; continue; }
                        if (!body.Raycast(new Ray(eye, d / dist), out var hit, dist + 0.05f) || (hit.point - pt.p).magnitude > 0.12f) { rMiss++; continue; } // = VehicleGlbBuilder.SprayTolerance
                        if (Physics.Raycast(eye, d / dist, out var bh, hit.distance - 0.05f, (1 << Layers.Default) | (1 << Layers.WashSurface), QueryTriggerInteraction.Ignore)) { rBlocked++; blockers[bh.collider.name] = blockers.TryGetValue(bh.collider.name, out var bc) ? bc + 1 : 1; continue; }
                        pt.from.Add((s, h));
                    }
                if (pt.from.Count == 0) unseen++;
            }
            Log($"plan (reach {reach:0.0}): far {rFar}, body-miss {rMiss}, blocked {rBlocked} by " + string.Join(", ", blockers.OrderByDescending(kv => kv.Value).Take(4).Select(kv => kv.Key + " " + kv.Value)));
            return unseen;
        }

        /// <summary>Dirt left at each point (texel readback of the client's own painter): mud for the washer, spots for the towel.</summary>
        static float[] DirtAt(DirtPainter painter, List<Pt> points, bool spots)
        {
            var init = Read(painter.DevInitRT); var clean = Read(painter.DevCleanRT);
            bool srgb = painter.DevInitRT.sRGB;
            var r = new float[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                var a = init.GetPixelBilinear(points[i].uv.x, points[i].uv.y);
                var b = clean.GetPixelBilinear(points[i].uv.x, points[i].uv.y);
                float mud0 = srgb ? Mathf.GammaToLinearSpace(a.r) : a.r; // the shader samples the sRGB init RT linearised
                r[i] = a.a < 0.5f ? 0f : Faint(spots ? Mathf.Clamp01(b.b - b.a) : Mathf.Clamp01(mud0 - b.r));
            }
            Object.Destroy(init); Object.Destroy(clean);
            return r;
        }

        /// <summary>Same dead zone as DirtEval.shader (residue under 5 % counts as clean).</summary>
        static float Faint(float d) => Mathf.Clamp01((d - 0.05f) / 0.95f);

        static Texture2D Read(RenderTexture rt)
        {
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBAHalf, false, true);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            return tex;
        }

        /// <summary>World points of the texels still dirty (mud for the washer / spots for the towel), up to `max` spread evenly.</summary>
        static List<Pt> TexelTargets(Vehicle car, bool spots, int max = 400)
        {
            var painter = car.Dirt.Painter;
            var init = Read(painter.DevInitRT); var clean = Read(painter.DevCleanRT);
            bool srgb = painter.DevInitRT.sRGB;
            int w = init.width;
            var a0 = init.GetPixels(); var c0 = clean.GetPixels();
            Object.Destroy(init); Object.Destroy(clean);
            var dirty = new List<Vector2>();
            for (int i = 0; i < a0.Length; i++)
            {
                if (a0[i].a < 0.5f) continue;
                float mud0 = srgb ? Mathf.GammaToLinearSpace(a0[i].r) : a0[i].r;
                float d = spots ? Faint(Mathf.Clamp01(c0[i].b - c0[i].a)) : Faint(Mathf.Clamp01(mud0 - c0[i].r));
                if (d > 0.01f) dirty.Add(new Vector2((i % w + 0.5f) / w, (i / w + 0.5f) / w));
            }
            var mf = painter.GetComponent<MeshFilter>(); var mesh = mf.sharedMesh; var tf = mf.transform;
            var v = mesh.vertices; var uv = mesh.uv2; var tri = mesh.triangles;
            var result = new List<Pt>();
            int step = Mathf.Max(1, dirty.Count / max);
            for (int k = 0; k < dirty.Count; k += step)
            {
                var t = dirty[k];
                for (int i = 0; i < tri.Length; i += 3)
                {
                    Vector2 a = uv[tri[i]], b = uv[tri[i + 1]], c = uv[tri[i + 2]];
                    float den = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
                    if (Mathf.Abs(den) < 1e-12f) continue;
                    float l1 = ((b.y - c.y) * (t.x - c.x) + (c.x - b.x) * (t.y - c.y)) / den;
                    float l2 = ((c.y - a.y) * (t.x - c.x) + (a.x - c.x) * (t.y - c.y)) / den;
                    float l3 = 1f - l1 - l2;
                    if (l1 < -0.02f || l2 < -0.02f || l3 < -0.02f) continue;
                    result.Add(new Pt { p = tf.TransformPoint(v[tri[i]] * l1 + v[tri[i + 1]] * l2 + v[tri[i + 2]] * l3), uv = t });
                    break;
                }
            }
            return result;
        }

        /// <summary>Final passes like a player would do them: look at what is still dirty on the body and hit exactly that.</summary>
        static IEnumerator TexelRounds(PlayerNet me, PlayerInputReader input, Vehicle car, Vector3[] stands, float reach, bool wipe)
        {
            for (int round = 0; round < 4 && car.Dirt.CleanPct.Value < 0.999f; round++)
            {
                var targets = TexelTargets(car, wipe);
                Plan(car, targets, stands, reach);
                var todo = targets.Where(p => p.from.Count > 0).ToList();
                Log($"{car.DataId} {(wipe ? "towel" : "washer")} texel pass {round + 1}: clean {car.Dirt.CleanPct.Value * 100f:0.00}%, {targets.Count} dirty texel targets ({targets.Count - todo.Count} not seen from any stand)");
                if (todo.Count == 0) break;
                var score = new Dictionary<(int, int), int>();
                foreach (var p in todo) foreach (var f in p.from) score[f] = score.TryGetValue(f, out int n) ? n + 1 : 1;
                var plan = todo.GroupBy(p => p.from.OrderByDescending(f => score[f]).First()).ToList();
                yield return Execute(me, input, stands, plan, wipe ? 4 : 3, wipe);
                yield return new WaitForSeconds(0.8f);
            }
        }

        /// <summary>Every texel still counted dirty after the wash (mud or spots past the dead zone), mapped back onto the body:
        /// how many, where (car-local), its face normal, and how far the nearest bot sample was.</summary>
        static void DirtyTexels(Vehicle car, List<Pt> points)
        {
            var painter = car.Dirt.Painter;
            var init = Read(painter.DevInitRT); var clean = Read(painter.DevCleanRT);
            bool srgb = painter.DevInitRT.sRGB;
            int w = init.width;
            var a0 = init.GetPixels(); var c0 = clean.GetPixels();
            Object.Destroy(init); Object.Destroy(clean);
            var dirty = new List<Vector2>();
            for (int i = 0; i < a0.Length; i++)
            {
                if (a0[i].a < 0.5f) continue;
                float mud0 = srgb ? Mathf.GammaToLinearSpace(a0[i].r) : a0[i].r;
                if (Faint(Mathf.Clamp01(mud0 - c0[i].r)) > 0.02f || Faint(Mathf.Clamp01(c0[i].b - c0[i].a)) > 0.02f)
                    dirty.Add(new Vector2((i % w + 0.5f) / w, (i / w + 0.5f) / w));
            }
            int covered = a0.Count(p => p.a >= 0.5f);
            var mf = painter.GetComponent<MeshFilter>(); var mesh = mf.sharedMesh; var tf = mf.transform;
            var v = mesh.vertices; var uv = mesh.uv2; var tri = mesh.triangles; var n = mesh.normals;
            var report = new List<string>();
            int unmapped = 0;
            foreach (var t in dirty.Where((_, k) => k % Mathf.Max(1, dirty.Count / 10) == 0).Take(10))
            {
                bool found = false;
                for (int i = 0; i < tri.Length && !found; i += 3)
                {
                    Vector2 a = uv[tri[i]], b = uv[tri[i + 1]], c = uv[tri[i + 2]];
                    float den = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
                    if (Mathf.Abs(den) < 1e-12f) continue;
                    float l1 = ((b.y - c.y) * (t.x - c.x) + (c.x - b.x) * (t.y - c.y)) / den;
                    float l2 = ((c.y - a.y) * (t.x - c.x) + (a.x - c.x) * (t.y - c.y)) / den;
                    float l3 = 1f - l1 - l2;
                    if (l1 < -0.02f || l2 < -0.02f || l3 < -0.02f) continue;
                    var p = v[tri[i]] * l1 + v[tri[i + 1]] * l2 + v[tri[i + 2]] * l3;
                    var nn = (n[tri[i]] + n[tri[i + 1]] + n[tri[i + 2]]).normalized;
                    var world = tf.TransformPoint(p);
                    float near = points.Count > 0 ? points.Min(q => (q.p - world).magnitude) : -1f;
                    report.Add($"{car.transform.InverseTransformPoint(world).ToString("F2")} n{nn.ToString("F1")} nearestSample {near:0.00}m");
                    found = true;
                }
                if (!found) unmapped++;
            }
            Log($"{car.DataId} dirty texels left: {dirty.Count}/{covered} counted ({unmapped} of the listed not on any face): {string.Join(" | ", report)}");
        }

        static IEnumerator Wash(PlayerNet me, PlayerInputReader input, Vehicle car)
        {
            var sync = car.Dirt;
            float start = sync.CleanPct.Value;
            float tStart = Time.time;
            var points = Points(sync, 0.15f);
            yield return Equip<PressureWasher>(me, input, car.transform.position);
            var sprayStands = Grid(car);
            // like a player on the hose: only stands the washer's tether lets us reach
            var tether = me.GetComponent<PlayerTools>().Equipped?.GetComponent<HoseTether>();
            if (tether != null) { var anc = tether.Anchor.Value; sprayStands = sprayStands.Where(s => new Vector2(s.x - anc.x, s.z - anc.z).magnitude <= tether.Length - 0.3f).ToArray(); }
            Log($"{car.DataId}: {points.Count} body samples, {Plan(car, points, sprayStands, 5.5f)} not visible to the washer");
            yield return Rounds(me, input, car, points, sprayStands, wipe: false);
            yield return TexelRounds(me, input, car, sprayStands, 5.5f, wipe: false);
            ScreenCapture.CaptureScreenshot($"autotest_glb_{car.DataId}_washed.png");
            yield return Equip<Towel>(me, input, car.transform.position);
            var towelStands = sprayStands;
            Log($"{car.DataId}: {Plan(car, points, towelStands, 1.75f)} samples not reachable with the towel");
            yield return Rounds(me, input, car, points, towelStands, wipe: true);
            yield return TexelRounds(me, input, car, towelStands, 1.75f, wipe: true);
            DirtyTexels(car, points);
            float end = sync.CleanPct.Value;
            Log($"{car.DataId} WASH {start * 100f:0.0}% → {end * 100f:0.0}% in {Time.time - tStart:0}s → {(end >= 0.999f ? "100% OK" : "NOT 100%")}");
            var c = car.transform;
            me.Motor.Teleport(c.position + c.right * 2.2f - c.forward * 3.4f + Vector3.up * 0.05f);
            yield return new WaitForSeconds(0.3f);
            me.Look.LookAt(c.position + Vector3.up * 0.8f);
            yield return new WaitForSeconds(0.4f);
            ScreenCapture.CaptureScreenshot($"autotest_glb_{car.DataId}_clean.png");
            yield return new WaitForSeconds(0.3f);
        }

        /// <summary>Repeat: read which samples are still dirty, shoot only those from a stand that sees them.</summary>
        static IEnumerator Rounds(PlayerNet me, PlayerInputReader input, Vehicle car, List<Pt> points, Vector3[] stands, bool wipe)
        {
            var sync = car.Dirt;
            for (int round = 0; round < 10 && sync.CleanPct.Value < 0.999f; round++)
            {
                var dirt = DirtAt(sync.Painter, points, wipe);
                var todo = points.Where((p, i) => dirt[i] > 0.002f && p.from.Count > 0).ToList();
                int stuck = points.Where((p, i) => dirt[i] > 0.002f && p.from.Count == 0).Count();
                Log($"{car.DataId} {(wipe ? "towel" : "washer")} round {round + 1}: clean {sync.CleanPct.Value * 100f:0.0}% (mud {sync.Painter.MudPct * 100f:0.0}% spots {sync.Painter.SpotsPct * 100f:0.0}%), dirty samples {todo.Count} (+{stuck} unreachable)");
                if (todo.Count == 0) break;
                if (round == 9) Diagnose(car, stands, todo.Take(12));
                // shoot from the stand that covers most of what's left, round-robin through the heights
                var score = new Dictionary<(int, int), int>();
                foreach (var p in todo) foreach (var f in p.from) score[f] = score.TryGetValue(f, out int n) ? n + 1 : 1;
                var plan = todo.GroupBy(p => p.from.OrderByDescending(f => score[f]).First()).ToList();
                yield return Execute(me, input, stands, plan, wipe ? 4 : 3, wipe);
                yield return new WaitForSeconds(0.8f);
            }
        }

        /// <summary>Why is a visible sample still dirty? Log what the spray ray from its first planned eye actually hits.</summary>
        static void Diagnose(Vehicle car, Vector3[] stands, IEnumerable<Pt> stuck)
        {
            var hits = new RaycastHit[16];
            var c = car.transform;
            foreach (var pt in stuck)
            {
                var (s, h) = pt.from[0];
                var eye = stands[s] + Vector3.up * Heights[h];
                var d = (pt.p - eye).normalized;
                int n = Physics.RaycastNonAlloc(eye, d, hits, 8f, Layers.SprayMask, QueryTriggerInteraction.Ignore);
                var sorted = hits.Take(n).OrderBy(x => x.distance).Take(3)
                    .Select(x => $"{x.collider.name}(L{x.collider.gameObject.layer}{(Vehicles.VehicleHull.Is(x.collider) ? ",hull" : "")}) d{x.distance:0.00} off{(x.point - pt.p).magnitude:0.00}");
                Log($"{car.DataId} stuck local {c.InverseTransformPoint(pt.p).ToString("F2")} uv {pt.uv.ToString("F3")} eye h{Heights[h]} dist {(pt.p - eye).magnitude:0.0}: {string.Join(" | ", sorted)}");
            }
        }

        static IEnumerator Execute(PlayerNet me, PlayerInputReader input, Vector3[] stands, List<IGrouping<(int stand, int h), Pt>> plan, int framesPer, bool wipe)
        {
            int k = 0;
            foreach (var group in plan.OrderBy(g => g.Key.stand * 10 + g.Key.h))
            {
                int h = group.Key.h;
                var targets = group.Select(s => s.p).ToList();
                me.Motor.Teleport(stands[group.Key.stand]);
                input.sCrouch = h == 1;
                yield return new WaitForSeconds(h == 1 ? 0.45f : 0.25f);
                if (h != 2)
                {
                    foreach (var p in targets)
                        for (int f = 0; f < framesPer; f++)
                        {
                            input.sCrouch = h == 1;
                            me.Look.LookAt(p);
                            if (wipe) input.sLook = new Vector2((k++ % 2 == 0 ? 1f : -1f) * 40f, 0f);
                            input.sUse = true;
                            yield return null;
                        }
                }
                else
                {
                    for (int i = 0; i < targets.Count;)
                    {
                        input.sJump = true;
                        yield return new WaitForSeconds(0.3f); // rise towards the apex
                        float air = Time.time;
                        while (Time.time - air < 0.35f && i < targets.Count)
                        {
                            for (int f = 0; f < framesPer; f++)
                            {
                                me.Look.LookAt(targets[i]);
                                if (wipe) input.sLook = new Vector2((k++ % 2 == 0 ? 1f : -1f) * 40f, 0f);
                                input.sUse = true;
                                yield return null;
                            }
                            i++;
                        }
                        input.sUse = false;
                        yield return new WaitForSeconds(0.5f); // land
                    }
                }
                input.sUse = false; input.sLook = Vector2.zero; input.sCrouch = false;
            }
        }
    }
}
