using System.Collections;
using System.Linq;
using CarWash.Player;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// P3-P5 check (-autotest-ragdoll on two clients + editor host). Lower client id = victim, the other = grabber.
    /// Schedule on server time (T0 = next multiple of 10 s): T0 victim knocked → ragdoll on every peer (same pose
    /// sampled at T0+2.5) → T0+3 grabber grabs → carries → T0+6 throws → victim lands, gets up, can walk →
    /// T0+16 knocked again → T0+18 grabbed → victim mashes Space → escapes → grabber's re-grab refused.
    /// Victim also measures the P5 camera (roll, angular speed, wall clipping, blend in/out).
    /// </summary>
    public static class DevChecksRagdoll
    {
        static void Log(string m) => DevAutopilot.Log("P3 " + m);
        static double Now => NetworkManager.Singleton.ServerTime.Time;
        static IEnumerator Until(double t) { while (Now < t) yield return null; }

        public static IEnumerator Run(PlayerNet me)
        {
            var input = me.GetComponent<PlayerInputReader>();
            input.scripted = true;
            float tw = Time.time;
            while (Object.FindObjectsByType<PlayerNet>(FindObjectsSortMode.None).Count(p => p.OwnerClientId != 0) < 2 && Time.time - tw < 60f) yield return null;
            var clients = Object.FindObjectsByType<PlayerNet>(FindObjectsSortMode.None).Where(p => p.OwnerClientId != 0).OrderBy(p => p.OwnerClientId).ToArray();
            var victim = clients[0]; var grabber = clients[1];
            bool amVictim = me == victim;
            var vr = victim.GetComponent<PlayerRagdoll>();
            double t0 = System.Math.Ceiling(Now / 10.0) * 10.0 + 10.0;
            Log($"role={(amVictim ? "victim" : "grabber")} victim=c{victim.OwnerClientId} grabber=c{grabber.OwnerClientId} T0 in {t0 - Now:0.0}s");

            // positions
            var basePos = new Vector3(6f, 0.05f, -7f);   // knocked south into the open lot (north = closed bay shutters)
            me.Motor.Teleport(amVictim ? basePos : basePos + new Vector3(-2.5f, 0f, 0f));
            me.Look.SetYaw(amVictim ? 270f : 90f);
            yield return Until(t0);

            if (amVictim)
            {
                me.Motor.AddKnockback(new Vector3(-2f, 3f, -12f));
                var rig = me.GetComponent<PlayerCameraRig>();
                float maxRoll = 0f, maxAng = 0f, maxBlend = 0f; bool clipped = false; var cam = me.Look.Camera.transform; var lastRot = cam.rotation;
                double end = t0 + 1.5; bool wasDown = false;
                while (Now < end)
                {
                    yield return null;
                    maxRoll = Mathf.Max(maxRoll, rig.Blend > 0.95f ? rig.LastRoll : 0f);
                    maxAng = Mathf.Max(maxAng, Quaternion.Angle(lastRot, cam.rotation) / Mathf.Max(1e-4f, Time.deltaTime));
                    lastRot = cam.rotation; maxBlend = Mathf.Max(maxBlend, rig.Blend); wasDown |= vr.IsDown;
                    clipped |= Physics.CheckSphere(cam.position, 0.08f, (1 << Layers.Default) | (1 << Layers.Vehicle), QueryTriggerInteraction.Ignore);
                }
                Log($"victim down={wasDown} snapshots rx={vr.SnapshotsReceived} camera: blend {maxBlend:0.00}, max roll {maxRoll:0.0}°, max turn {maxAng:0}°/s, inside wall={clipped}");
            }
            // pose agreement sample (both clients + host log)
            yield return Until(t0 + 1.6);
            var head = vr.GetComponentsInChildren<Rigidbody>(true).FirstOrDefault(b => b.name.Contains("Head"));
            Log($"pose@T0+1.6 victim hips {vr.Hips.position:F2} head {(head != null ? head.transform.position.ToString("F2") : "-")} state={vr.State.Value}");

            if (!amVictim)
            {
                // grab: look at the downed body and press F
                yield return Until(t0 + 1.8);
                me.Motor.Teleport(vr.Hips.position + new Vector3(-1.6f, 0f, 0f) - Vector3.up * (vr.Hips.position.y - 0.05f));
                yield return new WaitForSeconds(0.25f);
                me.Look.LookAt(vr.Hips.position);
                yield return new WaitForSeconds(0.25f);
                string prompt = me.GetComponent<PlayerGrabber>().PromptText();
                input.sGrab = true; yield return new WaitForSeconds(0.15f); input.sGrab = false;
                var gr = me.GetComponent<PlayerGrabber>();
                float tg = Time.time; while (gr.HeldPlayer == null && Time.time - tg < 2f) yield return null;
                Log($"grab: prompt='{prompt}' holding={gr.HeldPlayer != null}");
                // carry 2 s
                var start = vr.Hips.position;
                input.sMove = new Vector2(0f, 1f);
                yield return Until(t0 + 4.3);
                input.sMove = Vector2.zero;
                float follow = Vector3.Distance(vr.Hips.position, gr.HoldPoint);
                Log($"carry: victim moved {Vector3.Distance(start, vr.Hips.position):0.0} m, hips↔hold point {follow:0.00} m, victim held={vr.IsHeld}");
                // throw: hold F 0.9 s, release
                yield return Until(t0 + 4.6);
                var before = vr.Hips.position;
                me.Look.SetPitch(-15f); // throw forward-up like a player would (looking down at the body throws it into the ground)
                input.sGrab = true; yield return new WaitForSeconds(0.9f); input.sGrab = false;
                float peak = before.y; for (float tt = 0; tt < 1.2f; tt += Time.deltaTime) { peak = Mathf.Max(peak, vr.Hips.position.y); yield return null; }
                Log($"throw: victim flew {Vector3.Distance(before, vr.Hips.position):0.0} m (peak +{peak - before.y:0.0} m), held={vr.IsHeld}");
            }
            else
            {
                // wait to be released, land and get up
                yield return Until(t0 + 6.5);
                float tu = Time.time; while (vr.State.Value != PlayerRagdoll.Mode.Off && Time.time - tu < 12f) yield return null;
                var rig = me.GetComponent<PlayerCameraRig>();
                float tb = Time.time; while (rig.Blend > 0f && Time.time - tb < 3f) yield return null;
                var p0 = me.transform.position;
                var cc = me.GetComponent<CharacterController>();
                string diag = $"cc={cc.enabled} knocked={me.Status.IsKnocked} flags={me.Status.Flags} controls={vr.ControlsBody} scripted={input.scripted}";
                input.sMove = new Vector2(0f, 1f); yield return new WaitForSeconds(0.6f); input.sMove = Vector2.zero;
                diag += $" → moved {Vector3.Distance(p0, me.transform.position):0.00} m";
                Log($"got up after {Time.time - tu:0.0}s (state {vr.State.Value}), camera back to first person blend={rig.Blend:0.00}, can walk={Vector3.Distance(p0, me.transform.position) > 0.5f} [{diag}]");
            }

            // ---- round 2: escape + re-grab cooldown
            yield return Until(t0 + 16.0);
            if (amVictim) { me.Motor.Teleport(basePos); yield return null; me.Motor.AddKnockback(new Vector3(0f, 3f, -12f)); }
            yield return Until(t0 + 17.6);
            if (!amVictim)
            {
                var gr = me.GetComponent<PlayerGrabber>();
                me.Motor.Teleport(vr.Hips.position + new Vector3(-1.6f, 0f, 0f) - Vector3.up * (vr.Hips.position.y - 0.05f));
                yield return new WaitForSeconds(0.25f); me.Look.LookAt(vr.Hips.position); yield return new WaitForSeconds(0.25f);
                input.sGrab = true; yield return new WaitForSeconds(0.15f); input.sGrab = false;
                float tg = Time.time; while (gr.HeldPlayer == null && Time.time - tg < 2f) yield return null;
                bool held = gr.HeldPlayer != null;
                yield return Until(t0 + 20.2);
                bool stillHolding = gr.HeldPlayer != null;
                // immediate re-grab attempt
                me.Look.LookAt(vr.Hips.position); yield return null;
                input.sGrab = true; yield return new WaitForSeconds(0.15f); input.sGrab = false;
                yield return new WaitForSeconds(1f);
                Log($"round 2: grabbed={held}, after victim mashing holding={stillHolding}, re-grab right away → holding={gr.HeldPlayer != null} (cooldown {PlayerRagdoll.RegrabCooldown}s)");
            }
            else
            {
                yield return Until(t0 + 18.3);
                bool heldBefore = vr.IsHeld;
                for (int i = 0; i < 8; i++) { input.sJump = true; yield return new WaitForSeconds(0.2f); }
                yield return new WaitForSeconds(0.6f);
                Log($"escape: held before mashing={heldBefore}, after 8× Space held={vr.IsHeld}");
            }
            yield return Until(t0 + 26.0);
            input.scripted = false;
        }
    }
}
