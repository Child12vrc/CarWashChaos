using CarWash.Player;
using CarWash.Vehicles;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Day
{
    /// <summary>
    /// Bay DONE button (TECH S06, on the bay's button panel). E while a car is washing in this bay → inspection → pay.
    /// Networked so PlayerInteractor can target it; spawned per bay from the scene NetworkSpawnList.
    /// The bay is the nearest LotPaths bay (same on every peer).
    /// </summary>
    public class BayButton : NetworkBehaviour, IInteractable
    {
        [SerializeField] private Renderer _lamp;

        int _bay = -1;
        float _nextScan;
        Vehicle _cached;
        MaterialPropertyBlock _mpb;

        public int BayIndex
        {
            get
            {
                if (_bay < 0 && LotPaths.Instance != null)
                {
                    float best = float.MaxValue;
                    foreach (var b in LotPaths.Instance.Bays)
                    {
                        float d = (b.transform.position - transform.position).sqrMagnitude;
                        if (d < best) { best = d; _bay = b.Index; }
                    }
                }
                return _bay;
            }
        }

        /// <summary>The car currently washing in this bay (any peer), or null.</summary>
        public Vehicle WashingCar
        {
            get
            {
                if (Time.time >= _nextScan || (_cached != null && _cached.State.Value != VehicleState.Washing))
                {
                    _nextScan = Time.time + 0.25f;
                    _cached = null;
                    foreach (var v in FindObjectsByType<Vehicle>(FindObjectsSortMode.None))
                        if (v.IsSpawned && v.State.Value == VehicleState.Washing && v.BayIndex.Value == BayIndex) { _cached = v; break; }
                }
                return _cached != null && _cached.State.Value == VehicleState.Washing ? _cached : null;
            }
        }

        public string Prompt
        {
            get
            {
                var v = WashingCar;
                return v == null ? "" : $"세차 완료! 정산하기  (청결 {Mathf.FloorToInt(v.Dirt.CleanPct.Value * 100f)}%)";
            }
        }

        public bool CanInteract(PlayerNet player) => WashingCar != null;

        public void Interact(PlayerNet player)
        {
            var v = WashingCar;
            if (v != null && QueueLane.Instance != null)
            {
                Debug.Log($"[DAY] DONE pressed in bay {BayIndex + 1} by client {player.OwnerClientId} for {v.DataId}");
                QueueLane.Instance.ServerFinish(v);
            }
        }

        void Update()
        {
            if (_lamp == null) return;
            _mpb ??= new MaterialPropertyBlock();
            bool ready = WashingCar != null;
            var c = ready ? new Color(0.2f, 1f, 0.35f) : new Color(0.35f, 0.1f, 0.1f);
            _lamp.GetPropertyBlock(_mpb);
            _mpb.SetColor("_BaseColor", c);
            _mpb.SetColor("_EmissionColor", ready ? c * 2f : Color.black);
            _lamp.SetPropertyBlock(_mpb);
        }
    }
}
