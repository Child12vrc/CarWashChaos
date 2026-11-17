using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CarWash.Dirt;
using CarWash.Economy;
using CarWash.Net;
using CarWash.Player;
using CarWash.Tools;
using CarWash.Vehicles;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// P1 client check (-autotest-p1, with -netsim delay,jitter,loss): on every client at once — walk a circle while
    /// sampling every other player + the moving customer car (jumps / rubber-band), knock yourself down and watch the
    /// others' knockdowns arrive, hold a tool and check everyone's held tools sit in their holder's hands, wash a car
    /// (client 1) and compare hit latency + local vs server clean%, money label vs value. Logged as [AUTOTEST] P1 ….
    /// </summary>
    public static class DevChecksP1
    {
        static void Log(string m) => DevAutopilot.Log("P1 " + m);

        public static IEnumerator Run(PlayerNet me)
        {
            var input = me.GetComponent<PlayerInputReader>();
            input.scripted = true;
            var sim = NetSession.ArgNetSim;
            Log($"client {me.OwnerClientId} netsim={(sim != null ? $"{sim[0]}ms ±{sim[1]} loss {sim[2]}%" : "off")}");
            float t0 = Time.time;
            while (Object.FindObjectsByType<PlayerNet>(FindObjectsSortMode.None).Length < 4 && Time.time - t0 < 90f) yield return null;
            var others = Object.FindObjectsByType<PlayerNet>(FindObjectsSortMode.None).Where(p => p != me).OrderBy(p => p.OwnerClientId).ToArray();

            // ---- 1) movement: walk a circle, sample everyone else + any moving vehicle
            me.Motor.Teleport(new Vector3(-8f + me.OwnerClientId * 4f, 0.05f, -9f));
            yield return new WaitForSeconds(0.5f);
            var stats = others.ToDictionary(p => p, _ => new MotionStats());
            var carStats = new Dictionary<Vehicle, MotionStats>();
            float tw = Time.time;
            while (Time.time - tw < 8f)
            {
                input.sMove = new Vector2(0f, 1f);
                input.sSprint = (Time.time - tw) % 4f > 2f;
                me.Look.SetYaw(me.Look.transform.eulerAngles.y + 90f * Time.deltaTime);
                foreach (var p in others) if (p != null) stats[p].Add(p.TryGetComponent<RemoteVisualSmoother>(out var sm) ? sm.VisualPosition : p.transform.position, Time.deltaTime); // what is drawn
                foreach (var v in Object.FindObjectsByType<Vehicle>(FindObjectsSortMode.None))
                {
                    if (!carStats.TryGetValue(v, out var cs)) carStats[v] = cs = new MotionStats();
                    cs.Add(v.transform.position, Time.deltaTime);
                }
                yield return null;
            }
            input.sMove = Vector2.zero; input.sSprint = false;
            float runLimit = 14f; // 2× sprint speed
            foreach (var kv in stats.Where(k => k.Value.Distance > 1f)) Log($"move c{kv.Key.OwnerClientId}: {kv.Value.Summary(runLimit)}");
            foreach (var kv in carStats.Where(k => k.Value.Distance > 1f))
                Log($"car {kv.Key.DataId}: {kv.Value.Summary(14f)}");

            // ---- 2) knockdown sync: everyone knocks themself at the same moment; who do I see go down?
            yield return WaitUntilTimeSlot(20f);
            var seenKnocked = new HashSet<ulong>();
            float kt = Time.time;
            me.Motor.AddKnockback(new Vector3(0f, 3f, 12f));
            bool meKnocked = false;
            while (Time.time - kt < 6f)
            {
                meKnocked |= me.Status.IsKnocked;
                foreach (var p in others) if (p != null && p.Status.IsKnocked) seenKnocked.Add(p.OwnerClientId);
                yield return null;
            }
            Log($"knockdown: me={meKnocked}, saw others knocked {seenKnocked.Count}/{others.Length} [{string.Join(",", seenKnocked)}]");

            // ---- 3) held tools: take a different dev-yard tool each, walk, check every holder's tool is in their hands
            var pickTypes = new System.Type[] { typeof(NetPole), typeof(Towel), typeof(FoamCannon) };
            var want = pickTypes[(int)(me.OwnerClientId - 1) % pickTypes.Length];
            var tool = Object.FindObjectsByType<ToolBase>(FindObjectsSortMode.None).Where(t => t.GetType() == want && !t.IsHeld)
                .OrderBy(t => (t.transform.position - DevYard.Origin).sqrMagnitude).FirstOrDefault();
            if (tool != null)
            {
                var tp = tool.transform.position;
                me.Motor.Teleport(new Vector3(tp.x, 0.05f, tp.z) + new Vector3(0f, 0f, tp.y > 0.5f ? 1.2f : -1.2f));
                yield return new WaitForSeconds(0.4f);
                me.Look.LookAt(tool.transform.position);
                yield return new WaitForSeconds(0.3f);
                input.sInteract = true;
                float te = Time.time;
                while (tool.HolderPlayer != me && Time.time - te < 3f) yield return null;
                Log($"pickup {want.Name}: held={tool.HolderPlayer == me} after {Time.time - te:0.00}s");
            }
            yield return WaitUntilTimeSlot(20f);
            float worst = 0f; int checkedTools = 0;
            float th = Time.time;
            input.sMove = new Vector2(1f, 0f);
            while (Time.time - th < 4f)
            {
                foreach (var t in Object.FindObjectsByType<ToolBase>(FindObjectsSortMode.None))
                    if (t.HolderPlayer != null && t.HolderPlayer != me && !t.Stowed)
                    { worst = Mathf.Max(worst, Vector3.Distance(t.transform.position, t.HolderPlayer.Look.Head.position)); checkedTools++; }
                yield return null;
            }
            input.sMove = Vector2.zero;
            Log($"held tools of others: worst tool↔head {worst:0.00}m over {checkedTools} samples");

            // ---- 4) washing (client 1 only sprays; everyone compares the V02 test car)
            var car = Object.FindObjectsByType<DirtNetSync>(FindObjectsSortMode.None).OrderBy(d => (d.transform.position - DevYard.CarV02).sqrMagnitude).First();
            if (me.OwnerClientId == 1)
            {
                me.Motor.Teleport(new Vector3(DevYard.WasherReel.x + 1.5f, 0.05f, DevYard.WasherReel.z));
                yield return new WaitForSeconds(0.3f);
                yield return DevChecksFoam.Equip<PressureWasher>(me);
                me.Motor.Teleport(car.transform.position + new Vector3(-2.4f, 0.05f, 0f));
                yield return new WaitForSeconds(0.3f);
                me.Look.LookAt(car.transform.position + Vector3.up * 0.7f);
                float s0 = car.CleanPct.Value, l0 = car.Painter.CleanPct, ts = Time.time, firstServer = -1f, firstLocal = -1f;
                while (Time.time - ts < 6f)
                {
                    input.sUse = true;
                    me.Look.LookAt(car.transform.position + new Vector3(0f, 0.7f, Mathf.Sin((Time.time - ts) * 2f) * 1.5f));
                    if (firstLocal < 0f && car.Painter.CleanPct > l0 + 0.002f) firstLocal = Time.time - ts;
                    if (firstServer < 0f && car.CleanPct.Value > s0 + 0.002f) firstServer = Time.time - ts;
                    yield return null;
                }
                input.sUse = false;
                Log($"wash feedback: local paint +{firstLocal:0.00}s, server % +{firstServer:0.00}s after pressing");
                for (int i = 0; i < PlayerInventory.SlotCount; i++) if (me.GetComponent<PlayerTools>().Inventory.Get(i) == null) { input.sSlot = i; break; }
            }
            else yield return new WaitForSeconds(8f);
            yield return new WaitForSeconds(4f);
            Log($"dirt {car.name}: server {car.CleanPct.Value * 100f:0.0}% local {car.Painter.CleanPct * 100f:0.0}% Δ {Mathf.Abs(car.CleanPct.Value - car.Painter.CleanPct) * 100f:0.0}");

            // ---- 5) money label
            var eco = EconomyState.Instance;
            Log($"money label '{UI.Hud.Instance?.Day?.MoneyText}' value {eco?.Money.Value} match={UI.Hud.Instance?.Day?.MoneyText == "$" + eco?.Money.Value}");
            input.scripted = false;
        }

        /// <summary>Line up the clients: wait for the next multiple of `period` seconds of server time.</summary>
        static IEnumerator WaitUntilTimeSlot(float period)
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            double t = nm.ServerTime.Time;
            double next = System.Math.Ceiling(t / period) * period + 1.0;
            while (nm.ServerTime.Time < next) yield return null;
        }
    }
}
