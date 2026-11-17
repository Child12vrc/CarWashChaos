using UnityEngine;

namespace CarWash.Critters
{
    /// <summary>
    /// C05 Possum (GDD §7): plays dead after popping out. Grab it (no stun needed) and cage it — if it's left alone for
    /// 12 s it wakes up, chases the nearest player and bites (knockdown), then runs off and plays dead again.
    /// </summary>
    public class Possum : CritterBase
    {
        public const float PlayDeadTime = 12f;
        public int Bites { get; private set; }

        protected override bool LooksKnockedOut => State.Value == CritterState.Stunned || State.Value == CritterState.Act;

        protected override void OnStateEntered(CritterState s)
        {
            if (s == CritterState.Idle) Brain.TrySet(CritterState.Act); // straight back to playing dead
        }

        protected override void Behave(float dt)
        {
            float speed = Data != null ? Data.moveSpeed : 3f;
            switch (Brain.State)
            {
                case CritterState.Act: // playing dead
                    if (Agent.hasPath) Agent.ResetPath();
                    if (Brain.Wetness > MaxWetness.Value * 0.25f) { Brain.TrySet(CritterState.Flee); return; }
                    if (Brain.StateTime > PlayDeadTime) Brain.TrySet(CritterState.Chase);
                    break;
                case CritterState.Chase:
                    var p = NearestPlayer(out float d);
                    if (p == null || d > 20f || Brain.StateTime > 10f) { Brain.TrySet(CritterState.Act); return; }
                    Agent.speed = speed * 1.4f;
                    Agent.SetDestination(p.transform.position);
                    if (d < 1.2f)
                    {
                        var push = (p.transform.position - transform.position); push.y = 0f;
                        p.Motor.AddKnockback(push.normalized * 12f + Vector3.up * 3f); // above knockdownSpeed → Knocked
                        Bites++;
                        Debug.Log($"[CRITTER] possum bit client {p.OwnerClientId}");
                        Brain.TrySet(CritterState.Flee);
                    }
                    break;
                case CritterState.Flee:
                    Agent.speed = speed;
                    if (Arrived || Brain.StateTime > 3f) Brain.TrySet(CritterState.Act);
                    break;
                default:
                    Brain.TrySet(CritterState.Act);
                    break;
            }
        }
    }
}
