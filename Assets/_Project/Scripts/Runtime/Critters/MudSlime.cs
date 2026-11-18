using System.Linq;
using CarWash.Dirt;
using CarWash.Vehicles;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Critters
{
    /// <summary>
    /// C03 Mud Slime (GDD §7, TECH S07): hops to the nearest customer car and smears mud back on (ReDirt strokes on the
    /// R channel). Hitting it with water past slimeSplitWetness splits it into two smaller slimes (scale 0.6, HP 0.6,
    /// max 3 generations); the last generation can be stunned and caged. Extinguisher freeze arrives with T07.
    /// </summary>
    public class MudSlime : CritterBase
    {
        [SerializeField] private NetworkObject _selfPrefab;

        public readonly NetworkVariable<int> Gen = new NetworkVariable<int>(1);
        public int Smears { get; private set; }
        bool _split;
        float _nextSmear;
        DirtNetSync _target;
        float _hopT;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            Gen.OnValueChanged += (_, g) => ApplyScale(g);
            ApplyScale(Gen.Value);
        }

        void ApplyScale(int gen)
        {
            float s = Mathf.Pow(Balance.critter.slimeSplitScale, Mathf.Max(0, gen - 1));
            transform.localScale = Vector3.one * s;
        }

        public override void ServerAddWetness(float amount)
        {
            base.ServerAddWetness(amount);
            if (_split || Brain == null || Brain.IsGone || Brain.State == CritterState.Carried) return;
            if (Gen.Value < Balance.critter.slimeMaxGen && Brain.Wetness >= Balance.critter.slimeSplitWetness * HpMultiplier) Split();
        }

        void Split()
        {
            if (_selfPrefab == null) return;
            _split = true;
            float hp = HpMultiplier * Balance.critter.slimeSplitScale;
            for (int i = 0; i < 2; i++)
            {
                var side = (i == 0 ? 1f : -1f) * transform.right * 0.7f;
                var no = Instantiate(_selfPrefab, transform.position + side, transform.rotation);
                var child = no.GetComponent<MudSlime>();
                child.Gen.Value = Gen.Value + 1;
                child.HpMultiplier = hp;
                child.FromSurprise = FromSurprise;
                child.Home = Home;
                child.StartRevealed = true;
                no.Spawn(true);
            }
            Debug.Log($"[CRITTER] slime gen {Gen.Value} split into 2 × gen {Gen.Value + 1}");
            NetworkObject.Despawn(true);
        }

        protected override void Behave(float dt)
        {
            switch (Brain.State)
            {
                case CritterState.Idle:
                case CritterState.Wander:
                    // any dirtable car body nearby (customer cars + dev-yard test cars), skipping cars driving off
                    _target = FindObjectsByType<DirtNetSync>(FindObjectsSortMode.None)
                        .Where(s => s.IsSpawned && (s.transform.position - transform.position).sqrMagnitude < 35f * 35f)
                        .Where(s => { var v = s.GetComponent<Vehicle>(); return v == null || v.State.Value == VehicleState.Washing || v.State.Value == VehicleState.Queued || v.State.Value == VehicleState.Inspecting; })
                        .OrderBy(s => (s.transform.position - transform.position).sqrMagnitude).FirstOrDefault();
                    if (_target != null) { Brain.TrySet(CritterState.Act); return; }
                    base.Behave(dt);
                    break;
                case CritterState.Act: // go to the car and smear mud
                    if (_target == null || !_target.IsSpawned) { Brain.TrySet(CritterState.Idle); return; }
                    // car bodies are non-convex MeshColliders (Collider.ClosestPoint is undefined) → use the bounds,
                    // then raycast onto the real surface from beside the car
                    var col = _target.GetComponentInChildren<Collider>();
                    var bounds = col != null ? col.bounds : new Bounds(_target.transform.position, Vector3.one * 2f);
                    var me = transform.position;
                    // P1 bug fix: stand 0.6 m OUTSIDE the footprint (it used to settle under the body where the car's
                    // collider blocked every spray ray → unhittable slime)
                    var spot = OutsideSpot(bounds, me, 0.6f);
                    Agent.speed = Data != null ? Data.moveSpeed : 2.5f;
                    var flat = spot - me; flat.y = 0f;
                    if (flat.magnitude > 0.35f) Agent.SetDestination(spot);
                    else
                    {
                        if (Agent.hasPath) Agent.ResetPath();
                        if (Time.time >= _nextSmear)
                        {
                            _nextSmear = Time.time + 0.6f;
                            var from = me + Vector3.up * (0.35f + Random.Range(0f, 0.4f)) + transform.right * Random.Range(-0.4f, 0.4f);
                            var to = new Vector3(bounds.center.x, from.y, bounds.center.z) - from;
                            if (col != null && col.Raycast(new Ray(from, to.normalized), out var hit, to.magnitude))
                            {
                                // smear low on the body (R = mud)
                                _target.ServerWorldStroke(hit.point, 0.45f, new Vector4(0.8f, 0f, 0f, 0f), StrokeType.ReDirt);
                                Smears++;
                                _target.GetComponent<Vehicles.SurpriseTrigger>()?.MarkRedirtied();
                            }
                        }
                    }
                    if (Brain.Wetness > MaxWetness.Value * 0.25f) Brain.TrySet(CritterState.Flee);
                    break;
                default:
                    base.Behave(dt);
                    break;
            }
        }

        /// <summary>Closest point on the footprint rectangle grown by `margin` (pushed out to the nearest side when inside). Pure.</summary>
        public static Vector3 OutsideSpot(Bounds b, Vector3 p, float margin)
        {
            float minX = b.min.x - margin, maxX = b.max.x + margin, minZ = b.min.z - margin, maxZ = b.max.z + margin;
            float x = Mathf.Clamp(p.x, minX, maxX), z = Mathf.Clamp(p.z, minZ, maxZ);
            bool inside = p.x > minX && p.x < maxX && p.z > minZ && p.z < maxZ;
            if (inside)
            {
                float dl = p.x - minX, dr = maxX - p.x, dd = p.z - minZ, du = maxZ - p.z;
                float m = Mathf.Min(Mathf.Min(dl, dr), Mathf.Min(dd, du));
                if (m == dl) x = minX; else if (m == dr) x = maxX; else if (m == dd) z = minZ; else z = maxZ;
            }
            return new Vector3(x, p.y, z);
        }

        protected override void Update()
        {
            base.Update();
            if (GetComponent<CritterAnimator>() != null) return; // GLB: CritterAnimator does the hop + squash
            // hop: bounce the visual while moving (every peer — NetworkTransform moves the root)
            var v = transform.Find("Visual");
            if (v == null) return;
            bool moving = State.Value == CritterState.Act || State.Value == CritterState.Wander || State.Value == CritterState.Flee;
            _hopT += Time.deltaTime * (moving ? 7f : 0f);
            var lp = v.localPosition;
            lp.y = moving ? Mathf.Abs(Mathf.Sin(_hopT)) * 0.25f : Mathf.MoveTowards(lp.y, 0f, Time.deltaTime);
            v.localPosition = lp;
        }
    }
}
