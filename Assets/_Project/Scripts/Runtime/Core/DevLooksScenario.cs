using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CarWash.Player;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// Player-look check, host side (-autotest -autotest-looks on the hosting exe; it only observes). From the host's own peer:
    /// which look every remote player shows (must match their replicated choice), their animator Speed / foot stride while
    /// they walk and run (retargeted locomotion on a remote copy), ragdoll / carried / thrown states, outline colour + name
    /// tag. Screenshots autotest_looks_host_*.png. Logged as [HOSTSCN] looks:.
    /// </summary>
    public static class DevLooksScenario
    {
        static void Log(string m) => Debug.Log("[HOSTSCN] looks: " + m);

        public static IEnumerator Run()
        {
            var nm = NetworkManager.Singleton;
            while (nm == null || nm.ConnectedClientsIds.Count < 3) yield return null;
            yield return new WaitForSeconds(2f);
            var me = PlayerLook.Local != null ? PlayerLook.Local.GetComponent<PlayerNet>() : null;
            var shots = new HashSet<string>();
            var stride = new Dictionary<ulong, (float lo, float hi, float speed)>();
            float end = Time.time + 300f, nextLog = 0f;
            me?.Motor.Teleport(new Vector3(0f, 0.05f, -16f));
            while (Time.time < end)
            {
                var remotes = Object.FindObjectsByType<PlayerAppearance>(FindObjectsSortMode.None).Where(a => !a.IsOwner).OrderBy(a => a.OwnerClientId).ToArray();
                foreach (var a in remotes)
                {
                    var l = a.Current; if (l == null) continue;
                    var anim = l.animator;
                    var hips = anim.GetBoneTransform(HumanBodyBones.Hips); var foot = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
                    float f = Vector3.Dot(foot.position - hips.position, a.transform.forward);
                    float sp = anim.GetFloat("Speed");
                    stride.TryGetValue(a.OwnerClientId, out var s);
                    if (sp > 0.5f) s = (Mathf.Min(s.lo == 0 ? 9f : s.lo, f), Mathf.Max(s.hi == 0 ? -9f : s.hi, f), Mathf.Max(s.speed, sp));
                    stride[a.OwnerClientId] = s;
                    var rag = a.GetComponent<PlayerRagdoll>();
                    string key = null;
                    if (sp > 1.4f) key = "run";
                    else if (rag.IsHeld) key = "carried";
                    else if (rag.IsDown) key = "ragdoll";
                    if (key != null && shots.Add(key))
                    {
                        Log($"host view c{a.OwnerClientId} {key}: look {l.id}, body active={l.root.activeSelf}, animator Speed {sp:0.00}, ragdoll {rag.State.Value} held={rag.IsHeld}, tag '{a.TagText}'");
                        yield return Watch(me, key == "run" || key == "walk" ? a.transform.position + Vector3.up : rag.Hips.position, key);
                        ScreenCapture.CaptureScreenshot($"autotest_looks_host_{key}.png");
                        yield return null;
                    }
                }
                if (Time.time >= nextLog)
                {
                    nextLog = Time.time + 3f;
                    Log("remotes: " + string.Join(" | ", remotes.Select(a =>
                    {
                        stride.TryGetValue(a.OwnerClientId, out var s);
                        var outline = a.Current?.renderers.FirstOrDefault(r => r != null && r.sharedMaterial != null && r.sharedMaterial.name.Contains("Outline"));
                        var mpb = new MaterialPropertyBlock(); if (outline != null) outline.GetPropertyBlock(mpb, 0);
                        var c = outline != null ? mpb.GetColor("_BaseColor") : Color.clear;
                        return $"c{a.OwnerClientId} look#{a.Look.Value} {a.Current?.id} active={a.Current?.root.activeSelf} stride {s.hi - s.lo:0.00}m speedMax {s.speed:0.00} rag {a.GetComponent<PlayerRagdoll>().State.Value} outline ({c.r:0.00},{c.g:0.00},{c.b:0.00}) tag '{a.TagText}'";
                    })));
                }
                yield return null;
            }
        }

        static IEnumerator Watch(PlayerNet me, Vector3 target, string key)
        {
            if (me == null) yield break;
            var p = target + new Vector3(2.4f, 0f, -2.4f); p.y = 0.05f;
            me.Motor.Teleport(p);
            yield return new WaitForSeconds(0.25f);
            me.Look.LookAt(target + Vector3.up * (key == "run" ? 0f : 0.3f));
            yield return new WaitForSeconds(0.2f);
        }
    }
}
