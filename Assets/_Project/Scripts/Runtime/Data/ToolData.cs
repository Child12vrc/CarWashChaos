using System;
using UnityEngine;

namespace CarWash.Data
{
    /// <summary>GDD §4 / TECH S03. Source: Data/Source/tools.json</summary>
    public class ToolData : ScriptableObject
    {
        public string id;
        public string displayName;
        public bool mvp;
        public bool startUnlocked;
        public float weight;
        public float range;
        public float coneDeg;
        public int rays;
        [Tooltip("Clean rate per second for R=mud, G=grease, B=spots")] public Vector3 cleanRate;
        public float knockback;
        public float wetnessDps;
        public string[] flags = Array.Empty<string>();
        public string prefab;

        public ToolFlags Flags
        {
            get
            {
                var f = ToolFlags.None;
                foreach (var s in flags) if (Enum.TryParse(s, out ToolFlags v)) f |= v;
                return f;
            }
        }
    }
}
