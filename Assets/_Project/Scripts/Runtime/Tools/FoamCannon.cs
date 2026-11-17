using CarWash.Core;
using CarWash.Dirt;
using UnityEngine;

namespace CarWash.Tools
{
    /// <summary>
    /// T02: paints the FOAM layer on vehicles (needed before the washer can remove GREASE) and leaves slippery
    /// FoamPatch areas on the floor. No cleaning on its own.
    /// </summary>
    public class FoamCannon : SprayTool
    {
        [SerializeField] private FoamPatch _patchPrefab;
        [SerializeField] private float _patchSpacing = 0.9f;

        protected override Vector4 StrokeStrength(Vector3 cleanRateDt) => new Vector4(1f, 0f, 0f, 0f);
        protected override StrokeType StrokeTypeFor() => StrokeType.Foam;

        /// <summary>P1: a swarm inside the lag-compensated foam cone is smothered even if no thin ray touched it.</summary>
        protected override void OnConeCritter(Critters.CritterBase c, float dt)
        {
            if (c is Critters.BeeSwarm bees) { bees.ServerFoamKill(); return; }
            base.OnConeCritter(c, dt);
        }

        protected override void OnHitExtra(RaycastHit hit, Vector3 dir, float dt, int rays)
        {
            // bees: foam smothers the whole swarm (GDD §7)
            var bees = hit.collider.GetComponentInParent<Critters.BeeSwarm>();
            if (bees != null) { bees.ServerFoamKill(); return; }
            // floor = static Default-layer geometry facing up
            if (_patchPrefab == null || hit.collider.gameObject.layer != Layers.Default || hit.rigidbody != null || hit.normal.y < 0.7f) return;
            if (FoamPatch.TryRefreshNear(hit.point, _patchSpacing)) return;
            FoamPatch.ServerSpawn(_patchPrefab, hit.point, hit.normal, _data.balance.dirt.foamDuration);
        }
    }
}
