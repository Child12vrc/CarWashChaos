using System;
using System.Collections.Generic;
using CarWash.Critters;
using CarWash.Data;
using UnityEngine;

namespace CarWash.Vehicles
{
    /// <summary>
    /// TECH S07 SurpriseTrigger on every customer vehicle (server only). On arrival the vehicle rolls the week's
    /// surprise chance and picks a weighted entry of its surprise pool (only critters that have a prefab); the entry
    /// fires by its CritterData trigger — OnOpen (a door/trunk opens), OnCleanPct (washed past X: progress from the
    /// arrival dirt towards 100%), OnTimer (random moment while washing, up to X s) — and pops the critters out with
    /// the reveal sting. Tip rule (GDD §8): surprise handled = every critter caged/killed and the car not re-dirtied.
    /// </summary>
    [RequireComponent(typeof(Vehicle))]
    public class SurpriseTrigger : MonoBehaviour
    {
        /// <summary>Dev/test: force the pick (vehicleId → critterId) and the chance.</summary>
        public static Func<string, string> DevForcePick;
        public static float DevChanceOverride = -1f;

        Vehicle _v;
        public SurpriseEntry Pending { get; private set; }
        public TriggerKind Kind { get; private set; }
        public float Param { get; private set; }
        public bool Fired { get; private set; }
        public bool Redirtied { get; private set; }
        readonly List<CritterBase> _spawned = new List<CritterBase>();
        float _cleanAtStart = -1f, _fireAt;

        void Awake() => _v = GetComponent<Vehicle>();
        void OnEnable() { if (_v == null) _v = GetComponent<Vehicle>(); _v.DoorOpened += OnDoorOpened; }
        void OnDisable() { if (_v != null) _v.DoorOpened -= OnDoorOpened; }

        // ---------------------------------------------------------------- pure rules (EditMode tested)
        /// <summary>Weighted pick among entries whose critter can be spawned (roll in [0,1)).</summary>
        public static SurpriseEntry Pick(IList<SurpriseEntry> pool, double roll01, Func<string, bool> spawnable)
        {
            if (pool == null) return null;
            double total = 0;
            foreach (var e in pool) if (e != null && e.weight > 0 && (spawnable == null || spawnable(e.critterId))) total += e.weight;
            if (total <= 0) return null;
            double r = roll01 * total;
            SurpriseEntry last = null;
            foreach (var e in pool)
            {
                if (e == null || e.weight <= 0 || (spawnable != null && !spawnable(e.critterId))) continue;
                last = e;
                if (r < e.weight) return e;
                r -= e.weight;
            }
            return last;
        }

        /// <summary>Washing progress from the arrival dirt (0) to spotless (1).</summary>
        public static float WashedFraction(float clean, float cleanAtStart)
            => cleanAtStart >= 0.999f ? 1f : Mathf.Clamp01((clean - cleanAtStart) / (1f - cleanAtStart));

        // ---------------------------------------------------------------- server
        /// <summary>Server, right after the vehicle spawned: roll and arm (no-op when nothing is picked).</summary>
        public void ServerRoll(float chance, System.Random rng, DataRegistry data)
        {
            Pending = null; Fired = false; Redirtied = false; _spawned.Clear(); _cleanAtStart = -1f;
            var d = _v.Data;
            if (d == null || CritterSpawner.Instance == null) return;
            if (DevChanceOverride >= 0f) chance = DevChanceOverride;
            if (d.isBoss) chance = 1f; // T3.7: the week's boss always brings its surprise (B01: 2 mud slimes)
            string forced = DevForcePick?.Invoke(d.id);
            if (forced != null)
                foreach (var e in d.surprisePool) if (e.critterId == forced) { Pending = e; break; }
            if (Pending == null)
            {
                if (rng.NextDouble() >= chance) return;
                Pending = Pick(d.surprisePool, rng.NextDouble(), CritterSpawner.Instance.Has);
            }
            if (Pending == null) return;
            var cd = data.Get<CritterData>(Pending.critterId);
            Kind = cd != null ? cd.Trigger : TriggerKind.OnOpen;
            Param = cd != null ? cd.triggerParam : 0f;
            _fireAt = Kind == TriggerKind.OnTimer ? UnityEngine.Random.Range(Param * 0.3f, Mathf.Max(Param * 0.3f + 1f, Param)) : 0f;
            Debug.Log($"[SURPRISE] {d.id} armed {Pending.critterId} x{Pending.count} on {Kind} {Param}");
        }

        void OnDoorOpened(Vehicle v, VehicleDoor door)
        {
            if (Pending == null || Fired || Kind != TriggerKind.OnOpen) return;
            // out of the door opening, on the ground
            var p = door.transform.position;
            var side = Vector3.ProjectOnPlane(p - _v.transform.position, Vector3.up).normalized;
            Fire(OnGround(p + side * 0.6f), "door " + door.name);
        }

        void Update()
        {
            if (Pending == null || Fired || _v == null || !_v.IsSpawned || !_v.IsServer) return;
            if (_v.State.Value != VehicleState.Washing) return;
            if (_cleanAtStart < 0f) _cleanAtStart = _v.Dirt.CleanPct.Value;
            switch (Kind)
            {
                case TriggerKind.OnCleanPct:
                    if (WashedFraction(_v.Dirt.CleanPct.Value, _cleanAtStart) >= Param) Fire(BesideCar(), $"washed {WashedFraction(_v.Dirt.CleanPct.Value, _cleanAtStart):P0}");
                    break;
                case TriggerKind.OnTimer:
                    if (_v.PhaseElapsed >= _fireAt) Fire(BesideCar(), $"timer {_fireAt:0}s");
                    break;
            }
        }

        Vector3 BesideCar()
        {
            var s = _v.SurpriseSocket != null ? _v.SurpriseSocket.position : _v.transform.position;
            return OnGround(s + _v.transform.right * 1.6f);
        }

        /// <summary>5 cm above the walkable ground under <paramref name="p"/> (bay floors / lanes sit 2.5–5 cm up since M4.10: a
        /// fixed y 0.05 spawned critters inside the floor slab and they dropped under the lot).</summary>
        static Vector3 OnGround(Vector3 p)
        {
            int mask = 1 << Core.Layers.Default;
            float y = Physics.Raycast(new Vector3(p.x, 1f, p.z), Vector3.down, out var hit, 3f, mask, QueryTriggerInteraction.Ignore) ? hit.point.y : 0f;
            return new Vector3(p.x, y + 0.05f, p.z);
        }

        /// <summary>Server (dev/boss): fire now.</summary>
        public void Fire(Vector3 pos, string why)
        {
            if (Pending == null || Fired || CritterSpawner.Instance == null) return;
            Fired = true;
            for (int i = 0; i < Mathf.Max(1, Pending.count); i++)
            {
                var c = CritterSpawner.Instance.ServerSpawnRevealed(Pending.critterId, pos + new Vector3(i * 0.7f, 0f, 0f), Quaternion.LookRotation(pos - _v.transform.position), true);
                if (c != null) _spawned.Add(c);
            }
            Debug.Log($"[SURPRISE] {_v.DataId} fired {Pending.critterId} x{_spawned.Count} ({why})");
        }

        public void MarkRedirtied() => Redirtied = true;

        /// <summary>For the +10% tip: fired, every critter caged/killed/gone, car never re-dirtied.</summary>
        public bool HandledClean
        {
            get
            {
                if (!Fired || Redirtied) return false;
                foreach (var c in _spawned)
                    if (c != null && c.IsSpawned && c.Brain != null && !c.Brain.IsGone) return false;
                return true;
            }
        }
    }
}
