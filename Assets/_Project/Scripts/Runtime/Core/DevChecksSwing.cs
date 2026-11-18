using System.Collections;
using System.Linq;
using CarWash.Critters;
using CarWash.Player;
using CarWash.Tools;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// M4.6 D8 check (both exes: -autotest -autotest-release -autotest-swing; the host observes, the client acts).
    /// Client: washer in first person (FP hands at grip / off-hand, screenshot), a swing (arc), then the net pole: stands in front
    /// of the host (host measures the remote character's hand bones vs. the tool's grip / off-hand = two-hand IK), swings at the
    /// host (→ knocked down) and at the dev-yard raccoon (→ stunned). Logged as [AUTOTEST] swing: / [HOSTSCN] swing:.
    /// </summary>
    public static class DevChecksSwing
    {
        public static bool Requested => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-autotest-swing") >= 0;
        static void Log(string m) => DevAutopilot.Log("swing: " + m);
        static void HostLog(string m) => Debug.Log("[HOSTSCN] swing: " + m);

        static PlayerNet Other(PlayerNet me) => Object.FindObjectsByType<PlayerNet>(FindObjectsSortMode.None).FirstOrDefault(p => p != me);

        public static IEnumerator Run(PlayerNet me)
        {
            var input = me.GetComponent<PlayerInputReader>();
            var tools = me.GetComponent<PlayerTools>();
            input.scripted = true;
            yield return new WaitForSeconds(2f);

            // 1) first person: washer in both hands, then one swing
            yield return DevChecksFoam.Equip<PressureWasher>(me);
            yield return new WaitForSeconds(0.6f);
            me.Look.LookAt(me.Look.Head.position + me.transform.forward * 5f + Vector3.down * 0.6f);
            yield return new WaitForSeconds(0.4f);
            var fp = me.Look.Camera.GetComponent<FPHands>();
            var tool = tools.Equipped;
            if (fp != null && tool != null)
                Log($"FP hands visible={fp.Visible} right-grip {Vector3.Distance(fp.RightHand, tool.Grip.position):0.00} m, left-offhand {Vector3.Distance(fp.LeftHand, tool.OffHandPosition):0.00} m → {(fp.Visible ? "OK" : "FAIL")}");
            else Log($"FAIL fpHands={(fp != null)} tool={(tool != null)}");
            ScreenCapture.CaptureScreenshot("autotest_swing_fp_washer.png");
            yield return new WaitForSeconds(0.3f);
            input.sSwing = true;
            yield return new WaitForSeconds(0.13f);
            ScreenCapture.CaptureScreenshot("autotest_swing_fp_mid.png");
            Log($"own swing playing={tool != null && tool.Swinging} yaw={(tool != null ? tool.SwingYaw : 0f):0}");
            yield return new WaitForSeconds(1f);

            // 2) net pole in front of the host: the host watches the hands (IK), then gets hit
            yield return DevChecksFoam.Equip<NetPole>(me);
            var host = Other(me);
            if (host == null) { Log("FAIL no other player"); yield break; }
            var hp = host.transform.position;
            var hf = host.transform.forward; hf.y = 0f; hf.Normalize();
            me.Motor.Teleport(new Vector3(hp.x, 0.05f, hp.z) + hf * 2.6f + Vector3.Cross(Vector3.up, hf) * 0.6f);
            yield return new WaitForSeconds(0.3f);
            me.Look.LookAt(hp + Vector3.up * 1.1f);
            yield return new WaitForSeconds(3f); // host measures + screenshots the hold
            input.sSwing = true;                  // a miss first (too far) so the host sees the arc
            yield return new WaitForSeconds(1.4f);
            hp = host.transform.position;
            me.Motor.Teleport(new Vector3(hp.x, 0.05f, hp.z) + hf * 1.2f);
            yield return new WaitForSeconds(0.3f);
            me.Look.LookAt(host.transform.position + Vector3.up * 1.0f);
            yield return new WaitForSeconds(0.2f);
            input.sSwing = true;
            var rag = host.GetComponent<PlayerRagdoll>();
            float t0 = Time.time; bool down = false;
            while (Time.time - t0 < 1.5f) { if (rag != null && rag.IsDown) { down = true; break; } yield return null; }
            Log($"hit the host player → knocked down={down} → {(down ? "OK" : "FAIL")}");
            yield return new WaitForSeconds(PlayerRagdoll.DownTime + 1.5f);

            // 3) the dev-yard raccoon → stunned
            var raccoon = CritterBase.All.FirstOrDefault(c => c.CritterId == "C01");
            if (raccoon == null) { Log("no raccoon (dev yard?)"); }
            else
            {
                raccoon.DevRequestReveal();
                yield return new WaitForSeconds(1.2f);
                bool stunned = false;
                for (int tries = 0; tries < 4 && !stunned; tries++)
                {
                    yield return new WaitForSeconds(0.7f); // swing cooldown first, then approach and swing at once (it runs)
                    var rp = raccoon.transform.position;
                    var from = rp + (me.transform.position - rp).WithY(0f).normalized * 1.1f; from.y = 0.05f;
                    me.Motor.Teleport(from);
                    yield return new WaitForSeconds(0.15f);
                    var col = raccoon.GetComponentInChildren<Collider>();
                    me.Look.LookAt(col != null ? col.bounds.center : raccoon.transform.position + Vector3.up * 0.3f);
                    input.sSwing = true;
                    t0 = Time.time;
                    while (Time.time - t0 < 1f) { if (raccoon.State.Value == CritterState.Stunned) { stunned = true; break; } yield return null; }
                    Log($"try {tries + 1}: me {me.transform.position.ToString("F1")} raccoon {raccoon.transform.position.ToString("F1")} collider {(col != null ? col.bounds.center.ToString("F1") + " on=" + col.enabled + " layer=" + col.gameObject.layer : "none")} state {raccoon.State.Value}");
                }
                Log($"hit the raccoon → {raccoon.State.Value} → {(stunned ? "OK" : "FAIL")}");
                ScreenCapture.CaptureScreenshot("autotest_swing_raccoon.png");
            }
            yield return new WaitForSeconds(0.5f);
            input.scripted = false;
        }

        static Vector3 WithY(this Vector3 v, float y) { v.y = y; return v; }

        /// <summary>Host: look at the client while it holds a tool; measure its hand bones vs. the tool (two-hand IK); screenshots.</summary>
        public static IEnumerator HostObserve()
        {
            var nm = NetworkManager.Singleton;
            while (nm == null || nm.ConnectedClientsIds.Count < 2) yield return null;
            PlayerNet me = null;
            while (me == null) { me = Object.FindObjectsByType<PlayerNet>(FindObjectsSortMode.None).FirstOrDefault(p => p.IsOwner); yield return null; }
            var input = me.GetComponent<PlayerInputReader>();
            float end = Time.time + 150f, held = 0f, sumR = 0f, sumL = 0f, maxR = 0f, maxL = 0f;
            int samples = 0;
            float slide = 0f;
            bool shotHold = false, shotSwing = false, wasDown = false;
            string toolId = "";
            while (Time.time < end)
            {
                var other = Object.FindObjectsByType<PlayerNet>(FindObjectsSortMode.None).FirstOrDefault(p => !p.IsOwner);
                var t = other != null ? other.GetComponent<PlayerTools>().Equipped : null;
                var rag = me.GetComponent<PlayerRagdoll>();
                if (rag != null && rag.IsDown && !wasDown) { wasDown = true; HostLog($"host knocked down by a swing (last swing: {other?.GetComponent<PlayerTools>().LastSwingHit})"); }
                if (t is NetPole && !t.Stowed && (other.transform.position - me.transform.position).sqrMagnitude < 16f)
                {
                    if (input != null) input.scripted = true;
                    if (rag == null || !rag.ControlsBody) me.Look.LookAt(other.transform.position + Vector3.up * 1.1f);
                    held += Time.deltaTime;
                    var anim = other.GetComponent<PlayerAppearance>().Current?.animator;
                    if (anim != null && held > 0.8f && !t.Swinging)
                    {
                        var rh = anim.GetBoneTransform(HumanBodyBones.RightHand);
                        var lh = anim.GetBoneTransform(HumanBodyBones.LeftHand);
                        if (rh != null && lh != null)
                        {
                            var ikc = other.GetComponentInChildren<HandIK>();
                            float r = Vector3.Distance(rh.position, t.Grip.position), l = Vector3.Distance(lh.position, ikc != null ? ikc.LeftTarget : t.OffHandPosition);
                            slide = ikc != null ? Vector3.Distance(ikc.LeftTarget, t.OffHandPosition) : 0f;
                            sumR += r; sumL += l; maxR = Mathf.Max(maxR, r); maxL = Mathf.Max(maxL, l); samples++;
                            toolId = t.ToolId;
                        }
                    }
                    if (!shotHold && held > 1.8f)
                    {
                        shotHold = true;
                        ScreenCapture.CaptureScreenshot("autotest_swing_host_hold.png");
                        var ik = other.GetComponentInChildren<HandIK>();
                        HostLog($"remote holds {toolId}: IK weight {(ik != null ? ik.Weight : 0f):0.00}, right hand-grip avg {(samples > 0 ? sumR / samples : -1f):0.000} m (max {maxR:0.000}), left hand-target avg {(samples > 0 ? sumL / samples : -1f):0.000} m (max {maxL:0.000}; target {slide:0.00} m down the shaft from the off-hand socket), {samples} samples → {(samples > 0 && sumR / samples < 0.12f && sumL / samples < 0.15f ? "OK" : "FAIL")}");
                    }
                    if (!shotSwing && t.Swinging && t.SwingYaw < 0f)
                    {
                        shotSwing = true;
                        ScreenCapture.CaptureScreenshot("autotest_swing_host_remote_swing.png");
                        HostLog($"remote swing seen on host: yaw {t.SwingYaw:0}");
                    }
                }
                yield return null;
            }
            if (input != null) input.scripted = false;
            HostLog($"done: holdShot={shotHold} swingShot={shotSwing} hostKnocked={wasDown}");
        }
    }
}
