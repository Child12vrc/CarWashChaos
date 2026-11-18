using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// Where players (and their props, tools, critters, ragdolls) may be: the lot + the adjacent stretch of road and sidewalk.
    /// LotBuilder walls it in with invisible PlayBounds boxes (cars drive through the road openings); Unstuck never picks a
    /// spot outside it. Everything beyond is scenery.
    /// </summary>
    public static class PlayArea
    {
        public const float MinX = -28f, MaxX = 38f, MinZ = -20f, MaxZ = 18.4f;
        /// <summary>Wall height (+ a ceiling) — nothing jumped, thrown or ragdolled gets over it.</summary>
        public const float WallHeight = 40f;

        public static bool Contains(Vector3 p, float margin = 0f) =>
            p.x >= MinX + margin && p.x <= MaxX - margin && p.z >= MinZ + margin && p.z <= MaxZ - margin;
    }
}
