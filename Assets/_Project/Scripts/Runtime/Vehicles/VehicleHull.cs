using System.Collections.Generic;
using UnityEngine;

namespace CarWash.Vehicles
{
    /// <summary>
    /// Simple physics hull of a GLB vehicle (box colliders on the Vehicle layer: players, props, camera, ground).
    /// Washing raycasts skip it so they reach the exact body mesh on the WashSurface layer (otherwise the hit point
    /// would sit on the box, away from the paint, and 100% clean would be unreachable).
    /// </summary>
    public class VehicleHull : MonoBehaviour
    {
        static readonly Dictionary<Collider, bool> Cache = new Dictionary<Collider, bool>();

        public static bool Is(Collider c)
        {
            if (c == null) return false;
            if (!Cache.TryGetValue(c, out bool hull))
            {
                hull = c.GetComponent<VehicleHull>() != null;
                Cache[c] = hull;
            }
            return hull;
        }
    }
}
