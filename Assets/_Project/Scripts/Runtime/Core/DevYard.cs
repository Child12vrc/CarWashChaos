using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// Dev-only test area east of the exit ramp (outside the customer flow): static dirty test cars, a test washer
    /// with its own reel, foam floor zone, cone row. Shared by LotBuilder (placement) and the autotests (positions).
    /// P7: release builds strip it (DevOnlyObject on the yard, devOnly spawn entries; DevFlags.DevContent).
    /// </summary>
    public static class DevYard
    {
        public static readonly Vector3 Origin = new Vector3(31f, 0f, 4f);
        public static Vector3 CarV02 => Origin + new Vector3(-3f, 0f, 0f);
        public static Vector3 CarV04 => Origin + new Vector3(3f, 0f, 0f);
        public static Vector3 WasherReel => Origin + new Vector3(-6f, 1f, -6f);
        public static Vector3 FoamZone => Origin + new Vector3(0f, 0f, 10f);   // 5.6 x 8 m, spans z 10..18 relative to world
        public static Vector3 ConeRow => Origin + new Vector3(-6f, 0f, -9f);    // 5 cones eastward, 0.9 m apart
        public static Vector3 FoamCannon => Origin + new Vector3(-1.5f, 1f, -5f);
        public static Vector3 Towel => Origin + new Vector3(1.5f, 1f, -5f);
        public static Vector3 NetPole => Origin + new Vector3(4.5f, 1f, -5f);
        public static Vector3 Raccoon => Origin + new Vector3(2f, 0.05f, -8f);   // T3.1 test critters (generic behaviour)
        public static Vector3 Possum => Origin + new Vector3(5.5f, 0.05f, 15f);  // far from the raccoon so the net test hits the right one
        public static Vector3 Slime => Origin + new Vector3(-3f, 0.05f, -4.5f);   // next to the V02 test car (re-dirt check)
        public static Vector3 Bees => Origin + new Vector3(5.5f, 0.05f, 4f);
    }
}
