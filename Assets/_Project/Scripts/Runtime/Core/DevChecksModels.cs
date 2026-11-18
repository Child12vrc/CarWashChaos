using System.Collections;
using System.Linq;
using CarWash.Critters;
using CarWash.Player;
using CarWash.Tools;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// GLB props / tools / critters check, client side (-autotest -autotest-models; the hosting exe observes with
    /// DevModelsScenario). Props: every lot prop rests on the ground (not sunk, not floating), grab → held at the hold point
    /// (bounds centre), mass, drop → lands. Tools: lying models rest on the ground; held = view model in view, spray stream
    /// leaves Socket_Nozzle aimed at the crosshair hit, hose ends at Socket_HoseIn. Critters: client-side code animation
    /// (walk legs ∝ speed, slime squash, bee wings / flock), possum play-dead roll, stunned raccoon flipped, netted raccoon
    /// hangs at Socket_Capture by Socket_Carry. Logged as [AUTOTEST] models:, screenshots autotest_models_*.png.
    /// </summary>
    public static class DevChecksModels
    {
        static void Log(string m) => DevAutopilot.Log("models: " + m);

        static readonly string[] PropIds = { "Crate", "HeavyCrate", "Cone", "Bucket", "Tire", "HayBale", "TrashBag", "CautionSign" };

        public static IEnumerator Run(PlayerNet me)
        {
            var input = me.GetComponent<PlayerInputReader>();
            input.scripted = true;
            yield return new WaitForSeconds(2f); // props settle
            yield return Props(me, input);
            yield return ToolsCheck(me, input);
            yield return CrittersCheck(me, input);
            input.scripted = false;
        }

        // ---------------- props ----------------
        static Grabbable[] Instances(string id) => Object.FindObjectsByType<Grabbable>(FindObjectsSortMode.None)
            .Where(g => g.name.StartsWith(id + "(") || g.name == id).ToArray();

        /// <summary>Bottom of the collider vs what is under it: "ground" / "on X" / "SUNK" / "FLOATING".</summary>
        static string Rest(Collider c)
        {
            var b = c.bounds;
            if (b.min.y < -0.06f) return $"SUNK (min y {b.min.y:0.00})";
            var hits = Physics.RaycastAll(new Vector3(b.center.x, b.min.y + 0.05f, b.center.z), Vector3.down, 0.2f, ~0, QueryTriggerInteraction.Ignore);
            var under = hits.Where(h => h.collider != c).OrderBy(h => h.distance).FirstOrDefault();
            if (under.collider == null) return b.min.y < 0.06f ? $"ground (min y {b.min.y:0.00})" : $"FLOATING (min y {b.min.y:0.00})";
            return $"on {under.collider.name} (gap {under.distance - 0.05f:0.00})";
        }

        static IEnumerator Props(PlayerNet me, PlayerInputReader input)
        {
            var grabber = me.GetComponent<PlayerGrabber>();
            foreach (var id in PropIds)
            {
                var all = Instances(id);
                if (all.Length == 0) { Log($"prop {id}: NONE in the lot"); continue; }
                bool glb = all[0].transform.Find("Model") != null;
                var col = all[0].GetComponent<Collider>();
                string kind = col is BoxCollider ? "box" : col is MeshCollider mc && mc.convex ? "convex" : col.GetType().Name;
                Log($"prop {id}: x{all.Length} glb={glb} collider={kind} mass={all[0].Mass}kg rest=[{string.Join(", ", all.Select(g => Rest(g.GetComponent<Collider>())))}]");

                var g = all.OrderBy(x => (x.transform.position - me.transform.position).sqrMagnitude).First();
                var c = g.GetComponent<Collider>().bounds.center;
                me.Motor.Teleport(new Vector3(c.x, 0.05f, c.z) - Flat(c - me.transform.position).normalized * 1.6f);
                yield return new WaitForSeconds(0.25f);
                me.Look.LookAt(c);
                yield return new WaitForSeconds(0.2f);
                string prompt = grabber.PromptText();
                input.sGrab = true; yield return new WaitForSeconds(0.3f); input.sGrab = false;
                yield return new WaitForSeconds(0.7f);
                bool held = grabber.Held == g;
                float off = (g.GetComponent<Collider>().bounds.center - grabber.HoldPoint).magnitude;
                if (held && (id == "Cone" || id == "CautionSign" || id == "TrashBag")) ScreenCapture.CaptureScreenshot($"autotest_models_hold_{id}.png");
                yield return new WaitForSeconds(0.2f);
                if (held) { input.sDrop = true; yield return new WaitForSeconds(1.8f); }
                Log($"grab {id}: prompt='{prompt}' held={held} holdOffset={(held ? off.ToString("0.00") + "m" : "-")} → dropped rest={Rest(g.GetComponent<Collider>())}");
            }
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude < 1e-4f ? Vector3.forward : v; }

        // ---------------- tools ----------------
        static IEnumerator ToolsCheck(PlayerNet me, PlayerInputReader input)
        {
            foreach (var t in Object.FindObjectsByType<ToolBase>(FindObjectsSortMode.None).Where(x => !x.IsHeld).OrderBy(x => x.ToolId))
                Log($"tool {t.ToolId} lying at {t.transform.position.ToString("F1")}: glb={t.transform.Find("Model") != null} cols={t.GetComponents<Collider>().Length} rest={Rest(t.GetComponent<Collider>())}");
            var cam = me.Look.Camera;
            yield return ToolPose<PressureWasher>(me, input, cam, "washer", true);
            yield return ToolPose<FoamCannon>(me, input, cam, "foam", true);
            yield return ToolPose<Towel>(me, input, cam, "towel", false);
            yield return ToolPose<NetPole>(me, input, cam, "net", false);
        }

        static IEnumerator ToolPose<T>(PlayerNet me, PlayerInputReader input, Camera cam, string tag, bool spray) where T : ToolBase
        {
            yield return DevChecksFoam.Equip<T>(me);
            var tool = me.GetComponent<PlayerTools>().Equipped as T;
            if (tool == null) { Log($"{tag}: not equipped"); yield break; }
            // aim at the dev-yard V02 test car from 3.5 m (washer within its hose) or just ahead
            var target = DevYard.CarV02 + Vector3.up * 0.8f;
            if (spray && (me.transform.position - target).magnitude > 6f)
            {
                var from = target + Flat(me.transform.position - target).normalized * 3.5f; from.y = 0.05f;
                me.Motor.Teleport(from);
                yield return new WaitForSeconds(0.3f);
            }
            me.Look.LookAt(spray ? target : me.transform.position + me.transform.forward * 3f + Vector3.up * 0.6f);
            yield return new WaitForSeconds(0.3f);
            var nozzle = tool.Nozzle;
            var vp = cam.WorldToViewportPoint(nozzle.position);
            bool inView = vp.z > 0f && vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f;
            var grip = cam.transform.InverseTransformPoint(tool.transform.position);
            string extra = "";
            if (spray)
            {
                input.sUse = true;
                for (int i = 0; i < 25; i++) { me.Look.LookAt(target); input.sUse = true; yield return null; }
                ScreenCapture.CaptureScreenshot($"autotest_models_{tag}.png");
                yield return null;
                var stream = tool.GetComponentInChildren<SprayVfx>().transform.Find("Stream");
                var aimHit = Physics.Raycast(cam.transform.position, cam.transform.forward, out var h, 12f, Layers.SprayMask, QueryTriggerInteraction.Ignore) ? h.point : cam.transform.position + cam.transform.forward * 6f;
                float ang = stream != null ? Vector3.Angle(stream.forward, aimHit - nozzle.position) : -1f;
                extra = $" stream@nozzle={(stream != null ? (stream.position - nozzle.position).magnitude.ToString("0.000") : "none")}m streamVsAim={ang:0.0}°";
                input.sUse = false;
                var hose = tool.GetComponent<LineRenderer>();
                var hoseIn = tool.transform.Find("Model/Socket_HoseIn");
                if (hose != null && hoseIn != null) extra += $" hoseEnd→HoseIn={(hose.GetPosition(hose.positionCount - 1) - hoseIn.position).magnitude:0.00}m";
            }
            else
            {
                if (tool is Towel) { for (int i = 0; i < 10; i++) { input.sLook = new Vector2(i % 2 == 0 ? 30f : -30f, 0f); yield return null; } input.sLook = Vector2.zero; }
                ScreenCapture.CaptureScreenshot($"autotest_models_{tag}.png");
                yield return null;
            }
            Log($"{tag}: glb={tool.transform.Find("Model") != null} nozzle={nozzle.name} inView={inView} vp=({vp.x:0.00},{vp.y:0.00},{vp.z:0.00}) grip(cam)={grip.ToString("F2")}{extra}");
            yield return new WaitForSeconds(0.3f);
        }

        // ---------------- critters ----------------
        static IEnumerator CrittersCheck(PlayerNet me, PlayerInputReader input)
        {
            var raccoon = CritterBase.All.FirstOrDefault(c => c.CritterId == "C01");
            var possum = CritterBase.All.FirstOrDefault(c => c.CritterId == "C05");
            var slime = CritterBase.All.FirstOrDefault(c => c.CritterId == "C03");
            var bees = CritterBase.All.FirstOrDefault(c => c.CritterId == "C02") as BeeSwarm;
            // tools first (net in a slot, washer in hand at the dev-yard reel), then pop everyone out so the raccoon is stunned while still near the reel
            yield return DevChecksFoam.Equip<NetPole>(me);
            me.Motor.Teleport(new Vector3(DevYard.WasherReel.x + 1.5f, 0.05f, DevYard.WasherReel.z));
            yield return new WaitForSeconds(0.3f);
            yield return DevChecksFoam.Equip<PressureWasher>(me);
            foreach (var c in new CritterBase[] { raccoon, possum, slime, bees }) if (c != null) c.DevRequestReveal();
            Log($"critters: raccoon={raccoon != null} possum={possum != null} slime={slime != null} bees={bees != null}; animators: {string.Join(", ", CritterBase.All.Select(c => c.CritterId + "=" + (c.GetComponent<CritterAnimator>() != null)))}");
            yield return new WaitForSeconds(1.5f);

            // 1) motion sampled for 4 s (the brain wanders them): leg swing ∝ speed, tail wag, slime squash, bee wings
            float t0 = Time.time;
            float rMax = 0f, legLo = 999f, legHi = -999f, legAtSpeed = 0f, legIdle = 0f, tailLo = 999f, tailHi = -999f, sLo = 9f, sHi = 0f, wLo = 999f, wHi = -999f;
            var ra = raccoon != null ? raccoon.GetComponent<CritterAnimator>() : null;
            var leg = raccoon != null ? Find(raccoon.transform, "Leg_FL") : null;
            var tail = raccoon != null ? Find(raccoon.transform, "Tail") : null;
            var sBody = slime != null ? Find(slime.transform, "Body") : null;
            var wing = bees != null ? Find(bees.transform, "Wing_L") : null;
            while (Time.time - t0 < 4f)
            {
                if (ra != null && leg != null)
                {
                    float a = Signed(leg.localEulerAngles.x);
                    rMax = Mathf.Max(rMax, ra.Speed); legLo = Mathf.Min(legLo, a); legHi = Mathf.Max(legHi, a);
                    if (ra.Speed > 0.8f) legAtSpeed = Mathf.Max(legAtSpeed, Mathf.Abs(a));
                    if (ra.Speed < 0.05f) legIdle = Mathf.Max(legIdle, Mathf.Abs(a));
                }
                if (tail != null) { float y = Signed(tail.localEulerAngles.y); tailLo = Mathf.Min(tailLo, y); tailHi = Mathf.Max(tailHi, y); }
                if (sBody != null) { sLo = Mathf.Min(sLo, sBody.localScale.y); sHi = Mathf.Max(sHi, sBody.localScale.y); }
                if (wing != null) { float z = Signed(wing.localEulerAngles.z); wLo = Mathf.Min(wLo, z); wHi = Mathf.Max(wHi, z); }
                yield return null;
            }
            Log($"anim raccoon: speed max {rMax:0.0} m/s, leg swing {legLo:0}..{legHi:0}° (|max| moving {legAtSpeed:0}°, idle {legIdle:0}°), tail wag {tailLo:0}..{tailHi:0}°");
            Log($"anim slime: Body scale.y {sLo:0.00}..{sHi:0.00}; bees: flock={bees?.GetComponentInChildren<BeeFlock>()?.Count ?? 0} wing flap {wLo:0}..{wHi:0}°");

            // 3) raccoon: washer → stunned → flipped belly-up; net → hangs in the hoop
            if (raccoon != null)
            {
                yield return DevChecksFoam.Equip<PressureWasher>(me);
                float ts = Time.time;
                while (Time.time - ts < 15f && raccoon.State.Value != CritterState.Stunned)
                {
                    var c = raccoon.transform.position;
                    var reel = DevYard.WasherReel; reel.y = 0f;
                    me.Motor.Teleport(c + Flat(reel - c).normalized * 2.8f + new Vector3(0, 0.05f, 0));
                    me.Look.LookAt(c + Vector3.up * 0.3f);
                    input.sUse = true;
                    yield return null;
                }
                input.sUse = false;
                yield return new WaitForSeconds(0.8f);
                yield return LookFrom(me, raccoon.transform.position, 1.6f);
                ScreenCapture.CaptureScreenshot("autotest_models_raccoon_stunned.png");
                yield return null;
                Log($"raccoon stunned: state={raccoon.State.Value} flip={ra.Flip:0.00} body roll={Signed(Find(raccoon.transform, "Body").localEulerAngles.z):0}°");

                yield return DevChecksFoam.Equip<NetPole>(me);
                float tn = Time.time;
                while (Time.time - tn < 4f && !raccoon.IsCarried)
                {
                    var c = raccoon.transform.position;
                    me.Motor.Teleport(c + new Vector3(0f, 0.05f, -1.8f));
                    me.Look.LookAt(c + Vector3.up * 0.3f);
                    input.sUse = true;
                    yield return null;
                }
                input.sUse = false;
                me.Look.SetPitch(10f);
                yield return new WaitForSeconds(0.8f);
                var net = me.GetComponent<PlayerTools>().Equipped as NetPole;
                float gap = net != null && net.Capture != null && ra.CarrySocket != null ? (ra.CarrySocket.position - net.Capture.position).magnitude : -1f;
                ScreenCapture.CaptureScreenshot("autotest_models_raccoon_netted.png");
                yield return null;
                Log($"raccoon netted: carried={raccoon.IsCarried} Socket_Carry→Socket_Capture {gap:0.000} m (client view)");
                yield return new WaitForSeconds(3f); // host logs its own view of the netted raccoon meanwhile
            }

            // 2) possum plays dead (Act) → rolled on its side
            if (possum != null)
            {
                float tw = Time.time; while (possum.State.Value != CritterState.Act && Time.time - tw < 4f) yield return null;
                yield return new WaitForSeconds(0.6f);
                var pa = possum.GetComponent<CritterAnimator>();
                yield return LookFrom(me, possum.transform.position, 1.8f);
                ScreenCapture.CaptureScreenshot("autotest_models_possum_playdead.png");
                yield return null;
                Log($"possum: state={possum.State.Value} flip={(pa != null ? pa.Flip : -1f):0.00} body roll={Signed(Find(possum.transform, "Body").localEulerAngles.z):0}°");
            }

            // 4) bees: washer disperses the flock (Spread grows), screenshot of the GLB swarm
            if (bees != null && bees.IsSpawned)
            {
                yield return DevChecksFoam.Equip<PressureWasher>(me);
                var flock = bees.GetComponentInChildren<BeeFlock>();
                yield return LookFrom(me, bees.transform.position + Vector3.up * 1.2f, 3f);
                ScreenCapture.CaptureScreenshot("autotest_models_bees.png");
                float tb = Time.time;
                while (Time.time - tb < 8f && bees.State.Value != CritterState.Stunned)
                {
                    var c = bees.transform.position + Vector3.up * 1.2f;
                    me.Motor.Teleport(c + Flat(me.transform.position - c).normalized * 2.5f - Vector3.up * 1.15f);
                    me.Look.LookAt(c);
                    input.sUse = true;
                    yield return null;
                }
                input.sUse = false;
                yield return new WaitForSeconds(1f);
                Log($"bees: state={bees.State.Value} spread={(flock != null ? flock.Spread : -1f):0.00}");
            }
        }

        static IEnumerator LookFrom(PlayerNet me, Vector3 target, float dist)
        {
            var from = target + new Vector3(dist * 0.6f, 0f, -dist * 0.8f); from.y = 0.05f;
            me.Motor.Teleport(from);
            yield return new WaitForSeconds(0.3f);
            me.Look.LookAt(target + Vector3.up * 0.2f);
            yield return new WaitForSeconds(0.3f);
        }

        static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        static float Signed(float a) => a > 180f ? a - 360f : a;
    }
}
