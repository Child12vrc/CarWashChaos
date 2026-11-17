using UnityEngine;

namespace CarWash.Critters
{
    /// <summary>Animal Control cage by the office (GDD §7/§11). Static scene object; a carried critter inside → captured.</summary>
    public class AnimalCage : MonoBehaviour
    {
        public static AnimalCage Instance { get; private set; }
        [SerializeField] private Vector3 _size = new Vector3(2.4f, 2.2f, 2.4f);

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Inside the cage volume (a little generous so the carry point counts when you stand at the door).</summary>
        public bool Contains(Vector3 world)
        {
            var local = transform.InverseTransformPoint(world);
            var h = _size * 0.5f + new Vector3(0.6f, 0.6f, 0.6f);
            return Mathf.Abs(local.x) <= h.x && local.y >= -0.5f && local.y <= _size.y + 0.6f && Mathf.Abs(local.z) <= h.z;
        }
    }
}
