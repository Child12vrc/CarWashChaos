using System;
using UnityEngine;

namespace CarWash.Data
{
    /// <summary>GDD §9. Source: Data/Source/upgrades.json. values[i] = effect value at level i+1.</summary>
    public class UpgradeData : ScriptableObject
    {
        public string id;
        public string displayName;
        public string description;
        public string stat;
        public int[] costs = Array.Empty<int>();
        public float[] values = Array.Empty<float>();
        public int MaxLevel => costs.Length;
    }
}
