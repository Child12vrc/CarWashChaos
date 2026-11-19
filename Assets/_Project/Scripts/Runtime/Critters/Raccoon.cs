using System.Linq;
using CarWash.Tools;
using UnityEngine;

namespace CarWash.Critters
{
    /// <summary>
    /// C01 Raccoon (GDD §7): after popping out it runs to the nearest tool lying around (not held, not the hose-tethered
    /// washer), steals it and flees to the nearest hiding spot (vent / dumpster), where it stashes the tool and lurks
    /// before stealing again. Getting soaked makes it drop the loot; stunned → net/grab → cage.
    /// </summary>
    public class Raccoon : CritterBase
    {
        // GDD §11 alley props built by LotBuilder (Vent, Dumpster)
        static readonly Vector3[] HideSpots = { new Vector3(-12f, 0f, 16.6f), new Vector3(16f, 0f, 13.9f) };

        ToolBase _target, _loot;
        Vector3 _hide;
        float _nextLook;
        public ToolBase Loot => _loot;
        public int Steals { get; private set; }

        protected override void OnStateEntered(CritterState s)
        {
            if (s == CritterState.Stunned || s == CritterState.Carried || s == CritterState.Captured || s == CritterState.Escaped || s == CritterState.Flee)
                DropLoot();
        }

        /// <summary>Despawned while carrying a tool (day end, cleanup): drop it so the tool is not lost for good.</summary>
        public override void OnNetworkDespawn()
        {
            if (IsServer) DropLoot();
            base.OnNetworkDespawn();
        }

        void DropLoot()
        {
            if (_loot == null) return;
            _loot.ServerUnsteal(transform.position + Vector3.up * 0.6f);
            Debug.Log($"[CRITTER] raccoon dropped {_loot.ToolId}");
            _loot = null;
        }

        ToolBase FindTarget()
        {
            return FindObjectsByType<ToolBase>(FindObjectsSortMode.None)
                .Where(t => t.IsSpawned && !t.IsHeld && !t.Stolen.Value && !(t is PressureWasher)
                            && (t.transform.position - transform.position).sqrMagnitude < 30f * 30f)
                .OrderBy(t => (t.transform.position - transform.position).sqrMagnitude)
                .FirstOrDefault();
        }

        protected override void Behave(float dt)
        {
            float speed = Data != null ? Data.moveSpeed : 4.5f;
            // soaked → drops the loot and runs (base handles flee)
            if (Brain.Wetness > MaxWetness.Value * 0.25f && Brain.State != CritterState.Flee) { Brain.TrySet(CritterState.Flee); return; }
            switch (Brain.State)
            {
                case CritterState.Idle:
                case CritterState.Wander:
                    if (Time.time >= _nextLook)
                    {
                        _nextLook = Time.time + 1.5f;
                        _target = FindTarget();
                        if (_target != null) { Brain.TrySet(CritterState.Steal); Agent.speed = speed; return; }
                    }
                    base.Behave(dt);
                    break;
                case CritterState.Steal:
                    if (_target == null || _target.IsHeld || _target.Stolen.Value) { Brain.TrySet(CritterState.Idle); return; }
                    Agent.SetDestination(_target.transform.position);
                    var flat = _target.transform.position - transform.position; flat.y = 0f;
                    if (flat.magnitude < 1.0f && _target.ServerSteal())
                    {
                        _loot = _target; _target = null; Steals++;
                        _hide = HideSpots.OrderBy(h => (h - transform.position).sqrMagnitude).First();
                        Home = _hide;
                        Debug.Log($"[CRITTER] raccoon stole {_loot.ToolId}, running to {_hide}");
                        Brain.TrySet(CritterState.Act);
                        Agent.SetDestination(_hide);
                    }
                    break;
                case CritterState.Act: // running to the hiding spot with the loot
                    Agent.speed = speed;
                    if (Arrived || (transform.position - _hide).sqrMagnitude < 2.5f)
                    {
                        if (_loot != null) { _loot.ServerUnsteal(_hide + new Vector3(0.6f, 0.5f, -0.6f)); Debug.Log($"[CRITTER] raccoon stashed {_loot.ToolId} at {_hide}"); _loot = null; }
                        Brain.TrySet(CritterState.Idle);
                        _nextLook = Time.time + 12f; // lurks a while before the next raid
                    }
                    break;
                default:
                    base.Behave(dt);
                    break;
            }
        }

        protected override void Update()
        {
            base.Update();
            if (IsServer && _loot != null)
                _loot.transform.SetPositionAndRotation(transform.position + Vector3.up * 0.55f + transform.forward * 0.25f, transform.rotation);
        }
    }
}
