using CarWash.Core;
using CarWash.Critters;
using UnityEngine;

namespace CarWash.Tools
{
    /// <summary>
    /// T06 net pole (GDD §7): LMB nets a stunned (or no-stun-needed) critter within range; it stays in the net — secure,
    /// unlike carrying by hand — until the critter reaches the Animal Control cage or the pole is dropped/put away.
    /// </summary>
    public class NetPole : ToolBase
    {
        [SerializeField] private Transform _capture; // GLB: Socket_Capture (hoop centre) — every peer hangs the netted critter here
        public Transform Capture => _capture;

        static readonly RaycastHit[] Hits = new RaycastHit[8];

        public override void ServerUse(Vector3 origin, Vector3 dir, float dt)
        {
            if (HolderPlayer == null) return;
            foreach (var c in CritterBase.All)
                if (c != null && c.IsCarried && c.CarrierClient.Value == HolderPlayer.OwnerClientId) return; // net already full
            float reach = (Data != null ? Data.range : 2.5f) + 0.8f;
            // P1: 0.7 m swept radius (was 0.45) — what the netter saw lags the server by ~RTT/2
            int n = Physics.SphereCastNonAlloc(origin, 0.7f, dir, Hits, reach, 1 << Layers.Critter, QueryTriggerInteraction.Collide);
            CritterBase best = null; float bestD = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = Hits[i].collider.GetComponentInParent<CritterBase>();
                if (c == null || Hits[i].distance >= bestD || c.Brain == null || !c.Brain.CanCapture) continue;
                best = c; bestD = Hits[i].distance;
            }
            // P1 lag compensation: also accept a critter that was on the net's path where the holder saw it
            float lag = ViewLagSeconds();
            if (best == null && lag > 0f)
                foreach (var c in CritterBase.All)
                {
                    if (c == null || c.Brain == null || !c.Brain.CanCapture) continue;
                    var p = c.PositionAgo(lag) + Vector3.up * 0.3f;
                    float along = Vector3.Dot(p - origin, dir);
                    if (along < 0f || along > reach) continue;
                    if ((origin + dir * along - p).sqrMagnitude > 0.7f * 0.7f || along >= bestD) continue;
                    best = c; bestD = along;
                }
            if (best != null) best.ServerTryCarry(HolderPlayer, this);
        }
    }
}
