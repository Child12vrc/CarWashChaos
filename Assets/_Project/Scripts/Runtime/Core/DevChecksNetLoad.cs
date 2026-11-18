using System.Collections;
using System.Linq;
using CarWash.Day;
using CarWash.Player;
using CarWash.Tools;
using CarWash.Vehicles;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// M4.7 N1/N3 load scenario (every exe: -autotest -autotest-netload [-players 4] [-netstats 60]): a busy 4-player day so
    /// NetStatsRecorder sees realistic traffic. Players 1-3 each take the washer of bay 1-3 and wash their customer car while
    /// strafing round it (walking, not teleporting), now and then spraying loose props (they get pushed) and swinging.
    /// Player 4 carries the net pole: walks up to the others and knocks them down (ragdolls), grabs and throws props.
    /// Recording starts once two bays have customer cars (or 45 s into the day); the report is logged as [NETSTATS] on every peer.
    /// </summary>
    public static class DevChecksNetLoad
    {
        public static bool Requested => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-autotest-netload") >= 0;
        static void Log(string m) => DevAutopilot.Log("netload: " + m);

        static int ArgInt(string name, int def)
        {
            var a = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length && int.TryParse(a[i + 1], out int v) ? v : def;
        }

        public static IEnumerator Run(PlayerNet me)
        {
            var input = me.GetComponent<PlayerInputReader>();
            input.scripted = true;
            int expected = ArgInt("-players", 4), seconds = ArgInt("-netstats", 60);
            var nm = NetworkManager.Singleton;
            float t0 = Time.time;
            while (Object.FindObjectsByType<PlayerNet>(FindObjectsSortMode.None).Length < expected && Time.time - t0 < 180f) yield return null;
            var players = Object.FindObjectsByType<PlayerNet>(FindObjectsSortMode.None).OrderBy(p => p.OwnerClientId).ToList();
            int k = players.IndexOf(me);
            Log($"{players.Count} players, I am #{k + 1} (client {me.OwnerClientId}), mode {Net.NetSession.Mode}");
            var rec = NetStatsRecorder.Create($"netload#{k + 1}");
            while (DayManager.Instance == null) yield return null;
            var dm = DayManager.Instance;
            if (nm.IsServer)
            {
                DayManager.DayLengthOverride = 600f; DayManager.MaxOvertimeOverride = 60f; dm.DayLengthNet.Value = 600f;
                LotState.Instance?.ServerSetOpenCount(3);
            }
            me.RequestToggleReady();
            yield return new WaitForSeconds(2f);
            if (nm.IsServer)
            {
                var board = Object.FindObjectsByType<DayBoard>(FindObjectsSortMode.None).FirstOrDefault();
                float tw = Time.time;
                while (board != null && dm.State.Value == GameState.Lobby && Time.time - tw < 60f)
                {
                    me.Motor.Teleport(board.transform.position + new Vector3(0f, -1.55f, 1.8f));
                    yield return new WaitForSeconds(0.4f);
                    me.Look.LookAt(board.transform.position);
                    yield return new WaitForSeconds(0.4f);
                    input.sInteract = true;
                    yield return new WaitForSeconds(2f);
                }
            }
            t0 = Time.time;
            while (dm.State.Value != GameState.Day && Time.time - t0 < 90f) yield return null;
            Log($"day state {dm.State.Value}");
            float dayStart = Time.time;
            if (k < 3) yield return Washer(me, input, k, rec, dayStart, seconds);
            else yield return Chaos(me, input, players, rec, dayStart, seconds);
            input.sUse = false; input.sMove = Vector2.zero;
            while (!rec.Done) yield return null;
            yield return new WaitForSeconds(2f);
            input.scripted = false;
        }

        static float _nextDue;
        static void Due(NetStatsRecorder rec, float dayStart, int seconds)
        {
            if (rec.Recording || rec.Done || Time.time < _nextDue) return;
            _nextDue = Time.time + 0.5f;
            int cars = Object.FindObjectsByType<BayButton>(FindObjectsSortMode.None).Count(b => b.WashingCar != null);
            if (Time.time - dayStart > 45f || (cars >= 2 && Time.time - dayStart > 8f)) rec.Begin(seconds);
        }

        static IEnumerator Washer(PlayerNet me, PlayerInputReader input, int bay, NetStatsRecorder rec, float dayStart, int seconds)
        {
            var tools = me.GetComponent<PlayerTools>();
            var btn = Object.FindObjectsByType<BayButton>(FindObjectsSortMode.None).FirstOrDefault(b => b.BayIndex == bay);
            for (int attempt = 0; attempt < 4 && tools.Equipped == null; attempt++)
            {
                // the nearest free washer to my bay; if the others took them all, a foam cannon (also a sprayer)
                ToolBase washer = Object.FindObjectsByType<PressureWasher>(FindObjectsSortMode.None).Where(w => !w.IsHeld)
                    .OrderBy(w => btn != null ? (w.transform.position - btn.transform.position).sqrMagnitude : 0f).FirstOrDefault();
                if (washer == null) washer = Object.FindObjectsByType<FoamCannon>(FindObjectsSortMode.None).FirstOrDefault(w => !w.IsHeld);
                if (washer == null) break;
                me.Motor.Teleport(washer.transform.position + new Vector3(0f, -washer.transform.position.y + 0.05f, -1.3f));
                yield return new WaitForSeconds(0.3f);
                me.Look.LookAt(washer.transform.position);
                yield return new WaitForSeconds(0.3f);
                input.sInteract = true;
                yield return new WaitForSeconds(1f);
            }
            Log($"bay {bay + 1}: equipped {(tools.Equipped != null ? tools.Equipped.ToolId : "nothing")}");
            float side = bay % 2 == 0 ? 1f : -1f, nextProp = Time.time + 8f + bay * 3f, nextSwing = Time.time + 14f + bay * 4f, flip = Time.time + 4f, nextLog = Time.time + 15f;
            while (!rec.Done)
            {
                Due(rec, dayStart, seconds);
                var car = btn != null ? btn.WashingCar : null;
                if (Time.time > nextLog)
                {
                    nextLog = Time.time + 15f;
                    var dn = car != null ? car.GetComponent<Dirt.DirtNetSync>() : null;
                    Log($"bay {bay + 1}: car {(car != null ? car.DataId : "-")} clean {(dn != null ? dn.CleanPct.Value * 100f : -1f):0}% (recording {rec.Recording})");
                }
                if (Time.time > flip) { side = -side; flip = Time.time + Random.Range(3f, 6f); }
                if (Time.time > nextProp)
                {
                    // spray a loose prop near us for 1.5 s (it gets pushed / wet)
                    nextProp = Time.time + Random.Range(10f, 16f);
                    var prop = Object.FindObjectsByType<Grabbable>(FindObjectsSortMode.None).Where(g => g.Mass < 15f)
                        .OrderBy(g => (g.transform.position - me.transform.position).sqrMagnitude).FirstOrDefault();
                    if (prop != null && (prop.transform.position - me.transform.position).sqrMagnitude < 64f)
                    {
                        input.sMove = Vector2.zero;
                        for (float t = 0; t < 1.5f; t += Time.deltaTime) { me.Look.LookAt(prop.transform.position); input.sUse = true; yield return null; }
                        input.sUse = false;
                    }
                }
                if (Time.time > nextSwing) { nextSwing = Time.time + Random.Range(15f, 22f); input.sSwing = true; }
                if (car != null)
                {
                    var c = car.transform.position;
                    var flat = me.transform.position - c; flat.y = 0f;
                    float dist = flat.magnitude;
                    me.Look.LookAt(c + Vector3.up * (0.5f + Mathf.PingPong(Time.time * 0.6f, 0.9f)) + car.transform.forward * Mathf.Sin(Time.time * 0.9f) * 1.6f);
                    input.sMove = new Vector2(side * 0.6f, dist > 3.2f ? 0.5f : dist < 2.0f ? -0.5f : 0f);
                    input.sUse = Mathf.Repeat(Time.time, 5f) < 3.8f;
                }
                else
                {
                    // no customer yet: wander near the bay and spray the floor
                    input.sUse = Mathf.Repeat(Time.time, 4f) < 1.5f;
                    var target = (btn != null ? btn.transform.position : me.transform.position) + new Vector3(Mathf.Sin(Time.time * 0.4f) * 3f, -1.2f, -3f);
                    me.Look.LookAt(target);
                    input.sMove = new Vector2(side * 0.4f, 0.3f);
                }
                yield return null;
            }
        }

        static IEnumerator Chaos(PlayerNet me, PlayerInputReader input, System.Collections.Generic.List<PlayerNet> players, NetStatsRecorder rec, float dayStart, int seconds)
        {
            var tools = me.GetComponent<PlayerTools>();
            var grabber = me.GetComponent<PlayerGrabber>();
            yield return DevChecksFoam.Equip<NetPole>(me);
            int netSlot = Enumerable.Range(0, PlayerInventory.SlotCount).FirstOrDefault(i => tools.Inventory.Get(i) is NetPole);
            int cycle = 0;
            while (!rec.Done)
            {
                Due(rec, dayStart, seconds);
                cycle++;
                if (cycle % 2 == 1)
                {
                    // walk up to another player and knock them down
                    var others = players.Where(p => p != null && p != me).ToList();
                    var target = others[cycle / 2 % others.Count];
                    input.sSlot = netSlot;
                    yield return WalkTo(me, input, () => target.transform.position, 1.3f, 12f, rec, dayStart, seconds);
                    me.Look.LookAt(target.transform.position + Vector3.up * 1.0f);
                    yield return null;
                    input.sSwing = true;
                    yield return new WaitForSeconds(1.2f);
                }
                else
                {
                    // hands free, grab a light prop, carry it a few metres, throw it
                    int empty = Enumerable.Range(0, PlayerInventory.SlotCount).FirstOrDefault(i => tools.Inventory.Get(i) == null);
                    input.sSlot = empty;
                    yield return new WaitForSeconds(0.3f);
                    var prop = Object.FindObjectsByType<Grabbable>(FindObjectsSortMode.None).Where(g => g.Mass < 15f && Vehicles.LotPaths.Instance != null)
                        .OrderBy(g => (g.transform.position - me.transform.position).sqrMagnitude).Skip(cycle % 3).FirstOrDefault();
                    if (prop == null) { yield return new WaitForSeconds(1f); continue; }
                    yield return WalkTo(me, input, () => prop.transform.position, 1.4f, 10f, rec, dayStart, seconds);
                    me.Look.LookAt(prop.GetComponent<Collider>() != null ? prop.GetComponent<Collider>().bounds.center : prop.transform.position);
                    yield return null;
                    input.sGrab = true; yield return new WaitForSeconds(0.15f); input.sGrab = false;
                    yield return new WaitForSeconds(0.4f);
                    var dest = me.transform.position + Quaternion.Euler(0f, Random.Range(-90f, 90f), 0f) * me.transform.forward * 5f;
                    dest.x = Mathf.Clamp(dest.x, PlayArea.MinX + 3f, PlayArea.MaxX - 3f); dest.z = Mathf.Clamp(dest.z, PlayArea.MinZ + 3f, PlayArea.MaxZ - 3f);
                    yield return WalkTo(me, input, () => dest, 0.8f, 5f, rec, dayStart, seconds);
                    if (grabber.Held != null)
                    {
                        me.Look.LookAt(me.transform.position + me.transform.forward * 6f + Vector3.up * 2f);
                        input.sGrab = true; yield return new WaitForSeconds(0.6f); input.sGrab = false;
                    }
                    yield return new WaitForSeconds(0.8f);
                }
            }
        }

        static IEnumerator WalkTo(PlayerNet me, PlayerInputReader input, System.Func<Vector3> target, float stopAt, float timeout, NetStatsRecorder rec, float dayStart, int seconds)
        {
            float t0 = Time.time, lastProgress = Time.time, best = float.MaxValue;
            while (Time.time - t0 < timeout && !rec.Done)
            {
                Due(rec, dayStart, seconds);
                var p = target();
                var d = p - me.transform.position; d.y = 0f;
                float dist = d.magnitude;
                if (dist < stopAt) break;
                if (dist < best - 0.2f) { best = dist; lastProgress = Time.time; }
                if (Time.time - lastProgress > 1.5f) { input.sJump = true; lastProgress = Time.time; }
                me.Look.LookAt(new Vector3(p.x, me.Look.Head.position.y, p.z));
                input.sMove = new Vector2(0f, 1f);
                input.sSprint = dist > 4f;
                yield return null;
            }
            input.sMove = Vector2.zero; input.sSprint = false;
        }
    }
}
