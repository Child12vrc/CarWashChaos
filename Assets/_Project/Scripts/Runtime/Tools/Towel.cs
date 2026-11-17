using CarWash.Core;
using CarWash.Dirt;
using CarWash.Player;
using UnityEngine;

namespace CarWash.Tools
{
    /// <summary>
    /// T03 (TECH S03): close-range wipe, removes water SPOTS only (A channel), and only while the player is actually
    /// wiping (mouse moving faster than a threshold). SphereCast from the eye.
    /// </summary>
    public class Towel : ToolBase
    {
        [SerializeField] private float _wipeThreshold = 3f;   // mouse delta (px/frame)
        [SerializeField] private float _castRadius = 0.2f;

        static readonly RaycastHit[] Hits = new RaycastHit[8];

        public override bool OwnerWantsUse(PlayerInputReader input) => input.Look.magnitude > _wipeThreshold;

        public override void ServerUse(Vector3 origin, Vector3 dir, float dt)
        {
            var d = Data;
            float reach = (d != null ? d.range : 1f) + 0.8f;
            int n = Physics.SphereCastNonAlloc(origin, _castRadius, dir, Hits, reach, 1 << Layers.Vehicle, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; DirtNetSync target = null; Vector3 point = default;
            for (int i = 0; i < n; i++)
            {
                if (Hits[i].distance >= best) continue;
                var s = Hits[i].collider.GetComponentInParent<DirtNetSync>();
                if (s == null) continue;
                best = Hits[i].distance; target = s;
                point = Hits[i].point == Vector3.zero ? origin + dir * Hits[i].distance : Hits[i].point;
            }
            if (target == null) return;
            float rate = d != null ? d.cleanRate.z : 1.5f;
            target.ServerWorldStroke(point, _data.balance.tools.towelRadius * 0.4f, new Vector4(0f, 0f, 0f, rate * dt), StrokeType.Clean);
        }
    }
}
