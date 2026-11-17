using System;
using UnityEngine;

namespace CarWash.Data
{
    /// <summary>GDD §10. Source: Data/Source/weeks.json</summary>
    public class WeekData : ScriptableObject
    {
        public string id;
        public int week = 1;
        public int[] tiers = { 1 };
        public float surpriseChance = 0.35f;
        public int[] rentPerDay = { 60, 80, 100, 120, 150 };
        public string bossVehicleId;
        [Tooltip("Vehicle ids that may only appear from day N: 'V04@4'")] public string[] lateVehicles = Array.Empty<string>();
    }
}
