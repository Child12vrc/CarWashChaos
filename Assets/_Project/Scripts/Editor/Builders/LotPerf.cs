using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CarWash.Editor
{
    /// <summary>
    /// M4.11: render-cost pass over the built lot (fixed perf view: shadow casters, decal culling).
    ///  - shadows: renderers whose bounds are smaller than <see cref="ShadowMinSize"/> (cups, bottles, bolts, signs' small
    ///    parts, debris) stop casting — their shadow is a few pixels but a whole shadow-map draw each.
    ///  - decals: small projectors (stains, cracks, grime) fade out by <see cref="SmallDecalDistance"/>, the rest (road
    ///    markings, signs' text) by <see cref="DecalDistance"/> — URP culls each projector every frame otherwise.
    /// </summary>
    public static class LotPerf
    {
        public const float ShadowMinSize = 0.6f;
        public const float SmallDecalDistance = 30f, DecalDistance = 80f;

        public static void Apply(GameObject env)
        {
            int shadowsOff = 0, decals = 0;
            foreach (var r in env.GetComponentsInChildren<Renderer>(true))
            {
                if (r.shadowCastingMode == ShadowCastingMode.Off) continue;
                if (r.bounds.size.magnitude < ShadowMinSize) { r.shadowCastingMode = ShadowCastingMode.Off; shadowsOff++; }
            }
            foreach (var d in env.GetComponentsInChildren<DecalProjector>(true))
            {
                float size = Mathf.Max(d.size.x, d.size.y) * d.transform.lossyScale.x;
                d.drawDistance = size < 2.5f ? SmallDecalDistance : DecalDistance;
                d.fadeScale = 0.8f;
                decals++;
            }
            Debug.Log($"[LotPerf] shadows off on {shadowsOff} small renderers; {decals} decals culled at {SmallDecalDistance}/{DecalDistance} m");
        }
    }
}
