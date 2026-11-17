using System.Collections.Generic;
using CarWash.Core;
using CarWash.Dirt;
using CarWash.Player;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Tools
{
    /// <summary>
    /// Cone-ray spray (TECH S03). Server: `rays` NonAlloc raycasts in the cone → dirt strokes, prop forces, player knockback/soak,
    /// critter wetness. RMB toggles narrow (default) / wide nozzle. Visuals run locally on every peer (SprayVfx).
    /// </summary>
    public abstract class SprayTool : ToolBase
    {
        [SerializeField] private SprayVfx _vfx;
        [SerializeField] private float _propForceScale = 1f;

        public readonly NetworkVariable<bool> WideNozzle = new NetworkVariable<bool>();

        static readonly RaycastHit[] Hits = new RaycastHit[16];
        static readonly System.Collections.Generic.HashSet<Critters.CritterBase> WetThisTick = new System.Collections.Generic.HashSet<Critters.CritterBase>();
        static readonly Dictionary<Collider, DirtNetSync> DirtCache = new Dictionary<Collider, DirtNetSync>();
        float _spin;

        public float Range => (Data != null ? Data.range : 6f) * (WideNozzle.Value ? 0.6f : 1f) * RangeMultiplier;
        public float ConeDeg => (Data != null ? Data.coneDeg : 12f) * (WideNozzle.Value ? 2.2f : 1f);
        protected virtual float RangeMultiplier => 1f;
        protected virtual float CleanMultiplier => 1f;

        byte _source;      // P1: holder's clientId+1 → their own echo is skipped (they predicted it)
        bool _predicting;  // P1: owner-side local paint pass (no gameplay effects)

        /// <summary>
        /// P1 owner prediction (non-host clients): same cone, paint the dirt locally this frame so washing reacts with no
        /// round-trip delay. Only the dirt — knockback, props, critters, foam patches stay server-side.
        /// </summary>
        public override void OwnerPredict(Vector3 origin, Vector3 dir, float dt)
        {
            var d = Data;
            if (d == null || IsServer) return;
            int rays = Mathf.Max(1, d.rays);
            _predicting = true;
            try
            {
                for (int r = 0; r < rays; r++)
                {
                    Vector3 rd = ConeDir(dir, r, rays);
                    if (Cast(origin, rd, Range, out var hit)) OnRayHit(hit, rd, hit.distance / Range, dt, rays);
                }
            }
            finally { _predicting = false; }
        }

        public override void ServerUse(Vector3 origin, Vector3 dir, float dt)
        {
            var d = Data;
            if (d == null) return;
            int rays = Mathf.Max(1, d.rays);
            _source = HolderPlayer != null ? (byte)Mathf.Min(255, (int)HolderPlayer.OwnerClientId + 1) : (byte)0;
            if (HolderPlayer != null && HolderPlayer.IsOwner) _source = 0; // host's own spraying: nothing predicted
            _spin += 137.5f;
            PushPerPlayer.Clear();
            WetThisTick.Clear();
            for (int r = 0; r < rays; r++)
            {
                Vector3 rd = ConeDir(dir, r, rays);
                if (!Cast(origin, rd, Range, out var hit)) continue;
                float t = hit.distance / Range;
                OnRayHit(hit, rd, t, dt, rays);
            }
            ServerWetCrittersInCone(origin, dir, dt);
            // one knockback/soak message per hit player per tick (not per ray)
            var bal = _data.balance.player;
            foreach (var kv in PushPerPlayer)
            {
                kv.Key.AddKnockback(kv.Value);
                kv.Key.GetComponent<PlayerStatus>()?.ServerSoak(bal.soakedTime);
            }
        }

        static readonly Dictionary<PlayerMotor, Vector3> PushPerPlayer = new Dictionary<PlayerMotor, Vector3>();

        /// <summary>Extra cone half-angle for critters (P1 lag tolerance: the sprayer aimed at where they saw it).</summary>
        public const float CritterConeSlackDeg = 8f;

        /// <summary>
        /// P1: a client sees critters ~RTT/2 behind the server; thin rays then miss small, fleeing critters. Anything whose
        /// centre is inside the cone (+slack) and in range, with no wall/car in between, gets wet even if no ray touched it.
        /// </summary>
        void ServerWetCrittersInCone(Vector3 origin, Vector3 dir, float dt)
        {
            var d = Data;
            if (d == null) return;
            float half = ConeDeg * 0.5f + CritterConeSlackDeg;
            float lag = ViewLagSeconds();
            foreach (var c in Critters.CritterBase.All)
            {
                if (c == null || !c.IsSpawned || WetThisTick.Contains(c)) continue;
                // test where the critter is now AND where the sprayer saw it (lag-compensated)
                if (!InCone(origin, dir, c.transform.position, half) && (lag <= 0f || !InCone(origin, dir, c.PositionAgo(lag), half))) continue;
                WetThisTick.Add(c);
                OnConeCritter(c, dt);
            }
        }

        /// <summary>Server: a critter is inside the (lag-compensated) cone this tick. Default: wetness.</summary>
        protected virtual void OnConeCritter(Critters.CritterBase c, float dt)
        {
            var d = Data;
            if (d != null && d.wetnessDps > 0f) c.ServerAddWetness(d.wetnessDps * dt);
        }

        bool InCone(Vector3 origin, Vector3 dir, Vector3 pos, float half)
        {
            var to = pos + Vector3.up * 0.35f - origin;
            float dist = to.magnitude;
            if (dist > Range + 0.5f || dist < 0.01f || Vector3.Angle(dir, to) > half) return false;
            return !Physics.Raycast(origin, to / dist, dist - 0.4f, (1 << Layers.Default) | (1 << Layers.Vehicle), QueryTriggerInteraction.Ignore);
        }

        Vector3 ConeDir(Vector3 dir, int i, int count)
        {
            if (i == 0) return dir;
            float ang = (i / (float)(count - 1)) * 360f + _spin;
            var right = Vector3.Cross(dir, Vector3.up).sqrMagnitude < 0.01f ? Vector3.right : Vector3.Cross(dir, Vector3.up).normalized;
            var tilt = Quaternion.AngleAxis(ConeDeg * 0.5f, right) * dir;
            return Quaternion.AngleAxis(ang, dir) * tilt;
        }

        bool Cast(Vector3 origin, Vector3 dir, float range, out RaycastHit best)
        {
            best = default;
            int n = Physics.RaycastNonAlloc(origin, dir, Hits, range, Layers.SprayMask, QueryTriggerInteraction.Ignore);
            float bestD = float.MaxValue; bool found = false;
            for (int i = 0; i < n; i++)
            {
                if (Hits[i].distance >= bestD) continue;
                if (HolderPlayer != null && Hits[i].collider.transform.IsChildOf(HolderPlayer.transform)) continue;
                best = Hits[i]; bestD = Hits[i].distance; found = true;
            }
            return found;
        }

        /// <summary>Server: one ray hit. t = distance/range (0 near, 1 far).</summary>
        protected virtual void OnRayHit(RaycastHit hit, Vector3 dir, float t, float dt, int rays)
        {
            var d = Data;
            var bal = _data.balance;
            // dirt
            var sync = DirtOf(hit.collider);
            if (sync != null)
            {
                float radius = Mathf.Lerp(bal.dirt.brushMinRadius, bal.dirt.brushMaxRadius, t) * (WideNozzle.Value ? 1.6f : 1f);
                Vector4 str = StrokeStrength(d.cleanRate * dt * CleanMultiplier * (WideNozzle.Value ? 0.5f : 1f));
                if (_predicting) { sync.LocalPredict(hit.point, radius, str, StrokeTypeFor()); return; }
                sync.ServerWorldStroke(hit.point, radius, str, StrokeTypeFor(), _source);
            }
            if (_predicting) return;
            // physics props
            var rb = hit.rigidbody;
            if (rb != null && !rb.isKinematic)
                // DESIGN-GAP: knockback = force per ray (N). Split across rays a thin cone (one ray) could not beat ground friction.
                rb.AddForceAtPosition(dir * (d.knockback * _propForceScale * dt) * (1f - 0.5f * t), hit.point, ForceMode.Impulse);
            // other players: push + soak
            var motor = hit.collider.GetComponentInParent<PlayerMotor>();
            if (motor != null && (HolderPlayer == null || motor.gameObject != HolderPlayer.gameObject))
            {
                // DESIGN-GAP: TECH's dir*knockback*0.4*dt*30 per tick would add 12 m/s every tick (≈60 m/s steady).
                // Using *5 instead: point-blank steady push ≈ 10 m/s → knockdown after ~1 s, mid-range ≈ 5 m/s shove.
                PushPerPlayer.TryGetValue(motor, out var push);
                PushPerPlayer[motor] = push + dir * (d.knockback * bal.player.pvpKnockbackScale * dt * 5f / rays) * (1f - 0.5f * t);
            }
            // critters: wetness HP (GDD §7) — wetnessDps while any ray of the cone is on it
            if (d.wetnessDps > 0f)
            {
                var critter = hit.collider.GetComponentInParent<Critters.CritterBase>();
                if (critter != null && WetThisTick.Add(critter)) critter.ServerAddWetness(d.wetnessDps * dt); // once per critter per tick
            }
            OnHitExtra(hit, dir, dt, rays);
        }

        protected virtual Vector4 StrokeStrength(Vector3 cleanRateDt) => new Vector4(cleanRateDt.x, cleanRateDt.y, 0f, cleanRateDt.z);
        protected virtual StrokeType StrokeTypeFor() => StrokeType.Clean;
        protected virtual void OnHitExtra(RaycastHit hit, Vector3 dir, float dt, int rays) { } // critters (T3.1), foam floor (T1.5)

        static DirtNetSync DirtOf(Collider c)
        {
            if (!DirtCache.TryGetValue(c, out var s) || (s == null && c != null))
            {
                s = c.GetComponentInParent<DirtNetSync>();
                DirtCache[c] = s;
            }
            return s;
        }

        public void ServerToggleNozzle() { if (IsServer) WideNozzle.Value = !WideNozzle.Value; }

        protected override void OnUseVisual(bool on)
        {
            if (_vfx != null) _vfx.SetActive(on, Nozzle, Range, ConeDeg, HolderPlayer);
        }
    }
}
