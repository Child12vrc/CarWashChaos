using UnityEngine;

namespace CarWash.Data
{
    /// <summary>GDD §7. Source: Data/Source/critters.json</summary>
    public class CritterData : ScriptableObject
    {
        public string id;
        public string displayName;
        public bool mvp;
        public string trigger = "OnOpen";
        [Tooltip("OnCleanPct: dirt fraction threshold, OnTimer: max seconds")] public float triggerParam;
        public float maxWetness = 100f;
        public float stunTime = 6f;
        public float moveSpeed = 3f;
        public bool needsStunToCapture = true;
        public bool capturable = true;
        public string prefab;

        public TriggerKind Trigger => System.Enum.TryParse(trigger, out TriggerKind t) ? t : TriggerKind.OnOpen;
    }
}
