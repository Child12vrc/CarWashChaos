using System.Collections;
using System.Linq;
using CarWash.Critters;
using CarWash.Player;
using CarWash.Tools;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// M4.8 F5 / F6 check (client: -autotest -autotest-f5; host observes). Swing at the host → knockdown + hit feedback (hit
    /// marker / hit-stop); F on the downed host → carried (limbs dangle locally), thrown; Q on the dev-yard raccoon → launched,
    /// rolls, jelly wobble; F on the dizzy raccoon → carried by hand, thrown again. Logged as [AUTOTEST] f5:.
    /// </summary>
    public static class DevChecksF5
    {
        public static bool Requested => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-autotest-f5") >= 0;
        static void Log(string m) => DevAutopilot.Log("f5: " + m);

        public static IEnumerator Run(PlayerNet me)
        {
            var input = me.GetComponent<PlayerInputReader>();
            var tools = me.GetComponent<PlayerTools>();
            var grabber = me.GetComponent<PlayerGrabber>();
            input.scripted = true;
            yield return new WaitForSeconds(2f);
            yield return DevChecksFoam.Equip<Towel>(me);
            Log($"tool {(tools.Equipped != null ? tools.Equipped.ToolId : "none")}");

            // 1) swing at the host
            var host = Object.FindObjectsByType<PlayerNet>(FindObjectsSortMode.None).FirstOrDefault(p => p != me);
            var rag = host.GetComponent<PlayerRagdoll>();
            var hp = host.transform.position;
            me.Motor.Teleport(new Vector3(hp.x, 0.05f, hp.z) + host.transform.forward * 1.2f);
            yield return new WaitForSeconds(0.3f);
            me.Look.LookAt(host.transform.position + Vector3.up * 1.0f);
            yield return null;
            float fx0 = tools.LastHitFx;
            input.sSwing = true;
            float t0 = Time.time;
            while (!rag.IsDown && Time.time - t0 < 2f) yield return null;
            yield return new WaitForSeconds(0.2f);
            Log($"swing → host down={rag.IsDown}, hit feedback={(tools.LastHitFx > fx0)} → {(rag.IsDown && tools.LastHitFx > fx0 ? "OK" : "FAIL")}");

            // 2) F on the downed host: carry, watch the limbs dangle, throw
            yield return new WaitForSeconds(0.8f);
            for (int s = 0; s < PlayerInventory.SlotCount; s++) if (tools.Inventory.Get(s) == null) { input.sSlot = s; break; } // hands free
            yield return new WaitForSeconds(0.3f);
            for (int tries = 0; tries < 6 && grabber.HeldPlayer == null; tries++)
            {
                var hips = rag.Hips.position;
                me.Motor.Teleport(new Vector3(hips.x, 0.05f, hips.z) + (me.transform.position - hips).WithY(0f).normalized * 1.0f);
                yield return new WaitForSeconds(0.15f);
                me.Look.LookAt(rag.Hips.position);
                yield return null;
                input.sGrab = true; yield return new WaitForSeconds(0.1f); input.sGrab = false;
                yield return new WaitForSeconds(0.5f);
            }
            Log($"grab downed host: held={(grabber.HeldPlayer != null)} prompt='{grabber.PromptText()}'");
            // carry while turning: the limbs (local physics on this client) should swing
            var limb = rag.GetComponentsInChildren<Rigidbody>(true).Skip(3).FirstOrDefault();
            float limbMove = 0f; Vector3 lastLimb = limb != null ? limb.position - rag.Hips.position : Vector3.zero;
            t0 = Time.time;
            while (Time.time - t0 < 2f)
            {
                input.sLook = new Vector2(Mathf.Sin((Time.time - t0) * 6f) * 25f, 0f);
                yield return null;
                if (limb != null) { var rel = limb.position - rag.Hips.position; limbMove += (rel - lastLimb).magnitude; lastLimb = rel; }
            }
            input.sLook = Vector2.zero;
            Log($"carried 2 s: limb swing (relative to hips) {limbMove:0.00} m → {(limbMove > 0.3f ? "dangles" : "STIFF")}");
            ScreenCapture.CaptureScreenshot("autotest_f5_carry.png");
            var before = rag.Hips.position;
            me.Look.LookAt(me.Look.Head.position + me.transform.forward * 5f + Vector3.up * 1.5f);
            input.sGrab = true; yield return new WaitForSeconds(0.7f); input.sGrab = false;
            yield return new WaitForSeconds(1.2f);
            Log($"throw host: flew {Vector3.Distance(before, rag.Hips.position):0.0} m, still held={(grabber.HeldPlayer != null)} → {(grabber.HeldPlayer == null && Vector3.Distance(before, rag.Hips.position) > 1.5f ? "OK" : "FAIL")}");

            // 3) critter: smack (launch + roll + jelly), then F pick-up + throw
            var raccoon = CritterBase.All.FirstOrDefault(c => c.CritterId == "C01");
            if (raccoon == null) { Log("no raccoon (release build: dev yard off) — critter part skipped"); input.scripted = false; yield break; }
            raccoon.DevRequestReveal();
            yield return new WaitForSeconds(1.5f);
            yield return DevChecksFoam.Equip<Towel>(me);
            var anim = raccoon.GetComponent<CritterAnimator>();
            bool launched = false;
            for (int tries = 0; tries < 5 && !launched; tries++)
            {
                yield return new WaitForSeconds(0.85f);
                var rp = raccoon.transform.position;
                me.Motor.Teleport(new Vector3(rp.x, 0.05f, rp.z) + (me.transform.position - rp).WithY(0f).normalized * 1.0f);
                yield return new WaitForSeconds(0.1f);
                me.Look.LookAt(raccoon.GetComponentInChildren<Collider>().bounds.center);
                input.sSwing = true;
                t0 = Time.time;
                while (Time.time - t0 < 0.8f && !raccoon.Thrown.Value) yield return null;
                launched = raccoon.Thrown.Value;
            }
            var start = raccoon.transform.position;
            float maxJelly = 0f, maxRoll = 0f; t0 = Time.time;
            var pivot = anim != null ? anim.BodyPivot : null;
            var rest = pivot != null ? pivot.localRotation : Quaternion.identity;
            while (Time.time - t0 < 3f)
            {
                if (anim != null) maxJelly = Mathf.Max(maxJelly, anim.JellyAmount);
                if (pivot != null) maxRoll = Mathf.Max(maxRoll, Quaternion.Angle(rest, pivot.localRotation));
                if (Time.time - t0 > 0.3f && !raccoon.Thrown.Value) break;
                yield return null;
            }
            ScreenCapture.CaptureScreenshot("autotest_f5_critter_landed.png");
            Log($"smack raccoon: launched={launched} flew {Vector3.Distance(start, raccoon.transform.position):0.0} m, state {raccoon.State.Value}, jelly max {maxJelly:0.00}, body turned up to {maxRoll:0}° → {(launched && maxJelly > 0.03f ? "OK" : "FAIL")}");

            for (int s = 0; s < PlayerInventory.SlotCount; s++) if (tools.Inventory.Get(s) == null) { input.sSlot = s; break; }
            yield return new WaitForSeconds(0.3f);
            for (int tries = 0; tries < 6 && grabber.HeldCritter == null; tries++)
            {
                var rp = raccoon.transform.position;
                me.Motor.Teleport(new Vector3(rp.x, 0.05f, rp.z) + (me.transform.position - rp).WithY(0f).normalized * 1.0f);
                yield return new WaitForSeconds(0.15f);
                me.Look.LookAt(raccoon.GetComponentInChildren<Collider>().bounds.center);
                yield return null;
                string prompt = grabber.PromptText();
                input.sGrab = true; yield return new WaitForSeconds(0.1f); input.sGrab = false;
                yield return new WaitForSeconds(0.5f);
                if (tries == 0) Log($"critter prompt '{prompt}'");
            }
            Log($"F pick-up raccoon: held={(grabber.HeldCritter != null)} prompt='{grabber.PromptText()}'");
            start = raccoon.transform.position;
            me.Look.LookAt(me.Look.Head.position + me.transform.forward * 5f + Vector3.up * 1.2f);
            input.sGrab = true; yield return new WaitForSeconds(0.7f); input.sGrab = false;
            yield return new WaitForSeconds(0.3f);
            bool flying = raccoon.Thrown.Value;
            t0 = Time.time; while (raccoon.Thrown.Value && Time.time - t0 < 4f) yield return null;
            Log($"throw raccoon: flying={flying} landed {Vector3.Distance(start, raccoon.transform.position):0.0} m away, state {raccoon.State.Value} → {(flying && Vector3.Distance(start, raccoon.transform.position) > 2f ? "OK" : "FAIL")}");
            input.scripted = false;
        }

        static Vector3 WithY(this Vector3 v, float y) { v.y = y; return v; }
    }
}
