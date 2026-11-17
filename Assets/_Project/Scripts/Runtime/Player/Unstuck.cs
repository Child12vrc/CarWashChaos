using UnityEngine;
using UnityEngine.AI;

namespace CarWash.Player
{
    /// <summary>
    /// P2 "끼임 방지" (unstuck): server picks the nearest safe standing spot — on the NavMesh, with room for the player
    /// capsule (no walls/cars/props), searched in rings around the stuck position; fallback = the player's locker
    /// spawn point. Cooldown 30 s per player (PlayerNet).
    /// </summary>
    public static class Unstuck
    {
        public const float Cooldown = 30f;
        public const float Radius = 0.35f, Height = 1.8f;
        const int BlockMask = (1 << Core.Layers.Default) | (1 << Core.Layers.Vehicle) | (1 << Core.Layers.Prop) | (1 << Core.Layers.Interactable);

        /// <summary>True when the capsule at `feet` overlaps nothing solid.</summary>
        public static bool HasRoom(Vector3 feet)
        {
            var a = feet + Vector3.up * (Radius + 0.05f);
            var b = feet + Vector3.up * (Height - Radius);
            return !Physics.CheckCapsule(a, b, Radius, BlockMask, QueryTriggerInteraction.Ignore);
        }

        public static bool CanUse(float now, float readyAt) => now >= readyAt;

        /// <summary>Nearest safe spot (≤ 10 m), or `fallback` when nothing is found.</summary>
        public static Vector3 FindSafeSpot(Vector3 from, Vector3 fallback)
        {
            for (int ring = 0; ring <= 5; ring++)
            {
                float r = ring * 2f;
                int steps = ring == 0 ? 1 : 8 + ring * 4;
                Vector3 best = default; float bestD = float.MaxValue; bool found = false;
                for (int i = 0; i < steps; i++)
                {
                    float ang = i * Mathf.PI * 2f / steps;
                    var probe = from + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                    if (!NavMesh.SamplePosition(probe, out var hit, 1.5f, NavMesh.AllAreas)) continue;
                    var feet = hit.position + Vector3.up * 0.05f;
                    if (!HasRoom(feet)) continue;
                    float d = (feet - from).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = feet; found = true; }
                }
                if (found && bestD > 0.25f) return best;  // must actually move us out
                if (found && ring > 0) return best;
            }
            return fallback;
        }
    }
}
