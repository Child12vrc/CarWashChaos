using UnityEngine;

namespace CarWash.Vehicles
{
    /// <summary>
    /// One wash bay (GDD §11: 6x10 m, roof, drain, wall hose reel, button panel, shutters). Static scene object;
    /// open/closed comes from LotState. Points: Entry (north opening) → Stop (centre, car faces south) → Exit (south opening).
    /// </summary>
    public class Bay : MonoBehaviour
    {
        [SerializeField] private int _index;
        [SerializeField] private Transform _entry, _stop, _exit, _hoseReel, _panel;
        [SerializeField] private Transform[] _shutters;
        [SerializeField] private float _shutterSpeed = 2f;

        public int Index => _index;
        public Transform Entry => _entry;
        public Transform Stop => _stop;
        public Transform Exit => _exit;
        public Transform HoseReel => _hoseReel;
        public Transform Panel => _panel;
        public bool IsOpen => LotState.Instance != null ? LotState.Instance.IsOpen(_index) : _index == 0;

        /// <summary>Vehicle currently assigned to this bay (server bookkeeping, T2.2+).</summary>
        public Object Occupant { get; set; }

        float _shutterT = -1f;

        void Update()
        {
            float target = IsOpen ? 1f : 0f;
            if (_shutterT < 0f) _shutterT = target;
            _shutterT = Mathf.MoveTowards(_shutterT, target, Time.deltaTime * _shutterSpeed);
            foreach (var s in _shutters)
            {
                if (s == null) continue;
                var p = s.localPosition;
                // P6: rolls up into a thin coil under the roof (it used to slide up and stick out above the roof)
                s.localPosition = new Vector3(p.x, Mathf.Lerp(1.5f, 3.3f, _shutterT), p.z);
                var sc = s.localScale; s.localScale = new Vector3(sc.x, Mathf.Lerp(3f, 0.3f, _shutterT), sc.z);
            }
        }
    }
}
