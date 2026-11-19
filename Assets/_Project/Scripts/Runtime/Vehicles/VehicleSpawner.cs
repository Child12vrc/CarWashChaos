using System.Linq;
using System.Collections.Generic;
using CarWash.Data;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Vehicles
{
    /// <summary>
    /// Server: instantiates a vehicle prefab for a VehicleData id at the road entry, configures its dirt and spawns
    /// its interior trash (VehicleData.trashCount). Which vehicle and when is decided by DayManager (T2.4).
    /// </summary>
    public class VehicleSpawner : MonoBehaviour
    {
        public static VehicleSpawner Instance { get; private set; }

        [SerializeField] private DataRegistry _data;
        [SerializeField] private NetworkObject[] _vehiclePrefabs;
        [SerializeField] private NetworkObject[] _trashPrefabs;

        readonly Dictionary<string, NetworkObject> _byId = new Dictionary<string, NetworkObject>();

        void Awake()
        {
            Instance = this;
            foreach (var p in _vehiclePrefabs) if (p != null) _byId[p.GetComponent<Vehicle>().DataId] = p;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public bool Has(string id) => _byId.ContainsKey(id);

        public Vehicle ServerSpawn(string vehicleId, Vector3 pos, Quaternion rot, bool noGrease)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || !_byId.TryGetValue(vehicleId, out var prefab)) return null;
            var no = Instantiate(prefab, pos, rot);
            var v = no.GetComponent<Vehicle>();
            v.ServerConfigure(noGrease);
            no.Spawn(true);
            SpawnTrash(v);
            return v;
        }

        void SpawnTrash(Vehicle v)
        {
            var d = v.Data;
            if (d == null || _trashPrefabs.Length == 0) return;
            if (!v.Doors.Any(dd => !dd.IsTrunk)) { SpawnTrashDoorless(v, d.trashCount); return; } // no side doors (Batch F #8 tank / airboat / UFO)
            int sideDoors = 0;
            foreach (var door in v.Doors) if (!door.IsTrunk) sideDoors++;
            var spots = v.TrashSpots;
            for (int i = 0; i < d.trashCount; i++)
            {
                if (spots.Count > 0)
                {
                    // M4.6 D3: on the procedural / delivered interior (seat or floor spot), revealed by the door on that side
                    var spot = spots[i % spots.Count];
                    var sl = v.transform.InverseTransformPoint(spot.position) + new Vector3(0f, 0.04f * (i / spots.Count), 0f);
                    int di = NearestSideDoor(v, spot.position);
                    var tp = Instantiate(_trashPrefabs[Random.Range(0, _trashPrefabs.Length)], v.transform.TransformPoint(sl), v.transform.rotation * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                    var ti = tp.GetComponent<TrashItem>();
                    ti.ServerAttach(v, sl, di);
                    tp.Spawn(true);
                    v.Trash.Add(ti);
                    continue;
                }
                int doorIndex = i % Mathf.Max(1, sideDoors);
                var door = v.Doors[doorIndex];
                // just outside the door panel, at seat height; revealed when the door opens
                var local = v.transform.InverseTransformPoint(door.transform.position);
                local += new Vector3(Mathf.Sign(local.x) * 0.2f, -0.1f + 0.1f * (i / sideDoors), -0.7f + 0.2f * (i / sideDoors));
                var prefab = _trashPrefabs[Random.Range(0, _trashPrefabs.Length)];
                var t = Instantiate(prefab, v.transform.TransformPoint(local), v.transform.rotation);
                var item = t.GetComponent<TrashItem>();
                item.ServerAttach(v, local, doorIndex);
                t.Spawn(true);
                v.Trash.Add(item);
            }
        }
        /// <summary>Batch F #8: vehicles without doors (V08 tank, B03 airboat, B04 UFO) — trash lies in the open cabin / deck
        /// (the interior trash spots, else around Socket_Interior), always visible (TrashItem.DoorOpen → true, door index −1).</summary>
        void SpawnTrashDoorless(Vehicle v, int count)
        {
            var spots = v.TrashSpots;
            Vector3 basePos = v.InteriorSocket != null ? v.transform.InverseTransformPoint(v.InteriorSocket.position) : new Vector3(0f, 1f, 0f);
            for (int i = 0; i < count; i++)
            {
                var local = spots.Count > 0
                    ? v.transform.InverseTransformPoint(spots[i % spots.Count].position) + new Vector3(0f, 0.04f * (i / spots.Count), 0f)
                    : basePos + new Vector3((i % 2 == 0 ? -0.25f : 0.25f), 0.05f, -0.2f * (i / 2));
                var t = Instantiate(_trashPrefabs[Random.Range(0, _trashPrefabs.Length)], v.transform.TransformPoint(local), v.transform.rotation * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                var item = t.GetComponent<TrashItem>();
                item.ServerAttach(v, local, -1);
                t.Spawn(true);
                v.Trash.Add(item);
            }
        }

        static int NearestSideDoor(Vehicle v, Vector3 p)
        {
            int best = 0; float bd = float.MaxValue;
            for (int k = 0; k < v.Doors.Count; k++)
            {
                if (v.Doors[k].IsTrunk) continue;
                float d = (v.Doors[k].transform.position - p).sqrMagnitude;
                if (d < bd) { bd = d; best = k; }
            }
            return best;
        }
    }
}
