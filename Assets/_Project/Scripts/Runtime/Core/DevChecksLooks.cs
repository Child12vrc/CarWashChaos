using System.Collections;
using System.Linq;
using CarWash.Player;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// Player-look check, client side (-autotest -autotest-looks on two clients; the hosting exe observes with DevLooksScenario).
    /// Lobby: default look by join order, Z/X step (same RPC as the keys) replicated; walk / run: the retargeted Mixamo
    /// locomotion drives the Quaternius body (animator Speed + foot swing); then the P3/P4 sequence (knockdown → ragdoll →
    /// carried → thrown → get up → escape) from DevChecksRagdoll on the new bodies. Logged as [AUTOTEST] looks:.
    /// </summary>
    public static class DevChecksLooks
    {
        static void Log(string m) => DevAutopilot.Log("looks: " + m);

        public static IEnumerator Run(PlayerNet me)
        {
            var input = me.GetComponent<PlayerInputReader>();
            input.scripted = true;
            var ap = me.GetComponent<PlayerAppearance>();
            float tw = Time.time;
            while (Object.FindObjectsByType<PlayerNet>(FindObjectsSortMode.None).Count(p => p.OwnerClientId != 0) < 2 && Time.time - tw < 60f) yield return null;
            yield return new WaitForSeconds(1f);
            Log($"me c{me.OwnerClientId}: default look {ap.Current?.id} (#{ap.Look.Value}), everyone: {string.Join(", ", Object.FindObjectsByType<PlayerAppearance>(FindObjectsSortMode.None).OrderBy(a => a.OwnerClientId).Select(a => $"c{a.OwnerClientId}={a.Current?.id}"))}");

            // lobby picker: first client steps forward twice, the second steps back once
            bool first = me.OwnerClientId == Object.FindObjectsByType<PlayerNet>(FindObjectsSortMode.None).Where(p => p.OwnerClientId != 0).Min(p => p.OwnerClientId);
            int before = ap.Look.Value;
            if (first) { ap.RequestStep(1); yield return new WaitForSeconds(0.4f); ap.RequestStep(1); }
            else ap.RequestStep(-1);
            yield return new WaitForSeconds(1f);
            var active = ap.Current != null && ap.Current.root.activeSelf && ap.Current.animator.enabled;
            Log($"me c{me.OwnerClientId}: look #{before}→#{ap.Look.Value} {ap.Current?.id} ({ap.CurrentName}), active body={active}, lobby text='{UI.Hud.Instance?.Lobby?.Text}'");

            // walk then run (owner animator from observed velocity, same code path as remote copies)
            var basePos = first ? new Vector3(-2f, 0.05f, -9f) : new Vector3(2f, 0.05f, -12f);
            me.Motor.Teleport(basePos);
            me.Look.SetYaw(90f);
            yield return new WaitForSeconds(0.5f);
            yield return Move(me, input, false, "walk");
            yield return Move(me, input, true, "run");
            input.sMove = Vector2.zero; input.sSprint = false;
            yield return new WaitForSeconds(1f);

            yield return DevChecksRagdoll.Run(me);
            input.scripted = false;
        }

        static IEnumerator Move(PlayerNet me, PlayerInputReader input, bool sprint, string tag)
        {
            var anim = me.GetComponent<PlayerAppearance>().Current.animator;
            var foot = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            float t0 = Time.time, maxSpeed = 0f, lo = 9f, hi = -9f;
            var p0 = me.transform.position;
            while (Time.time - t0 < 2.5f)
            {
                input.sMove = new Vector2(0f, 1f); input.sSprint = sprint;
                if (Time.time - t0 > 0.8f)
                {
                    maxSpeed = Mathf.Max(maxSpeed, anim.GetFloat("Speed"));
                    float z = hips.InverseTransformPoint(foot.position).z;   // foot ahead/behind the hips → stride
                    float f = Vector3.Dot(foot.position - hips.position, me.transform.forward);
                    lo = Mathf.Min(lo, f); hi = Mathf.Max(hi, f);
                }
                yield return null;
            }
            float v = (me.transform.position - p0).magnitude / 2.5f;
            Log($"{tag}: moved {v:0.0} m/s, animator Speed max {maxSpeed:0.00}, left foot swing {lo:0.00}..{hi:0.00} m (stride {hi - lo:0.00} m)");
            input.sMove = Vector2.zero; input.sSprint = false;
            yield return new WaitForSeconds(0.6f);
        }
    }
}
