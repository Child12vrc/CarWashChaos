using System;
using UnityEngine;

namespace CarWash.Data
{
    /// <summary>GDD §6. Source: Data/Source/vehicles.json</summary>
    public class VehicleData : ScriptableObject
    {
        public string id;
        public string displayName;
        public bool mvp;
        public bool isBoss;
        public string size = "M";
        public int tier = 1;
        public int basePay;
        public float timeLimit;
        [Tooltip("Initial dirt amount 0..1 per channel: x=mud(R) y=grease(G) z=spots(B)")] public Vector3 dirt;
        public int trashCount;
        public int textureSize = 512;
        public SurpriseEntry[] surprisePool = Array.Empty<SurpriseEntry>();
        public string prefab;

        public VehicleSize Size => Enum.TryParse(size, out VehicleSize s) ? s : VehicleSize.M;
    }
}
