using UnityEngine;

namespace CarWash.Critters
{
    /// <summary>
    /// C02 Bee Swarm (GDD §7, TECH S07: one NetworkObject + local particle "boids"): chases the nearest player and stings
    /// (screen wobble + slow via PlayerStatus.Sting). The washer disperses it (wetness → "stunned" = scattered for the
    /// stun time, then it reforms); a hit from the foam cannon kills the whole swarm. Not capturable.
    /// </summary>
    public class BeeSwarm : CritterBase
    {
        [SerializeField] private ParticleSystem _bees;
        [SerializeField] private BeeFlock _flock; // GLB: instanced C02_bee models (replaces the particle dots)
        public int Stings { get; private set; }
        public bool Killed { get; private set; }
        float _nextSting;

        /// <summary>Server: foam cannon hit (FoamCannon.OnHitExtra).</summary>
        public void ServerFoamKill()
        {
            if (!IsServer || Killed || Brain == null || Brain.IsGone) return;
            Killed = true;
            Debug.Log("[CRITTER] bee swarm smothered by foam");
            Brain.Escape(); // terminal; no bonus — the swarm just dies
            DieRpc();
            Invoke(nameof(Despawn), 0.6f);
        }

        void Despawn() { if (IsSpawned) NetworkObject.Despawn(true); }

        [Unity.Netcode.Rpc(Unity.Netcode.SendTo.Everyone)]
        void DieRpc()
        {
            if (_flock != null) _flock.Die();
            if (_bees == null) return;
            var main = _bees.main; main.gravityModifier = 2f;
            var em = _bees.emission; em.enabled = false;
        }

        protected override void Behave(float dt)
        {
            var p = NearestPlayer(out float d);
            if (p == null || d > 25f) { base.Behave(dt); return; }
            Brain.TrySet(CritterState.Chase);
            Agent.speed = Data != null ? Data.moveSpeed : 3.5f;
            Agent.SetDestination(p.transform.position);
            if (d < 1.4f && Time.time >= _nextSting)
            {
                _nextSting = Time.time + 2f;
                p.Status.ServerSting(_data.balance.player.stungTime);
                Stings++;
                Debug.Log($"[CRITTER] bees stung client {p.OwnerClientId}");
            }
        }

        protected override void Update()
        {
            base.Update();
            if (_flock != null) _flock.Spread = Mathf.MoveTowards(_flock.Spread, State.Value == CritterState.Stunned ? 3f : 0.55f, Time.deltaTime * 4f);
            if (_bees == null) return;
            // dispersed while "stunned": spread wide; otherwise a tight buzzing ball
            var sh = _bees.shape;
            float target = State.Value == CritterState.Stunned ? 3f : 0.55f;
            sh.radius = Mathf.MoveTowards(sh.radius, target, Time.deltaTime * 4f);
        }
    }
}
