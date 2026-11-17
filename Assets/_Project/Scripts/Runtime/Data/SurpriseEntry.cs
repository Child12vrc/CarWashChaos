using System;

namespace CarWash.Data
{
    /// <summary>One weighted entry of a vehicle's surprise pool. count &gt; 1 = several critters (boss).</summary>
    [Serializable]
    public class SurpriseEntry
    {
        public string critterId;
        public float weight = 1f;
        public int count = 1;
    }
}
