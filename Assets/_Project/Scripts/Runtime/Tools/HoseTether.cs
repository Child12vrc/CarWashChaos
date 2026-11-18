using CarWash.Data;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Tools
{
    /// <summary>
    /// TECH S03 HoseTether: the washer is tied to its bay's hose reel (anchor = spawn point). Beyond hoseLength the
    /// holder is pulled back (owner-side, motor is owner-authoritative); a loose washer is dragged back by the server.
    /// Visual: local 8-point verlet rope from the reel to the nozzle.
    /// </summary>
    [RequireComponent(typeof(ToolBase))]
    public class HoseTether : NetworkBehaviour
    {
        const int Points = 8;

        [SerializeField] private DataRegistry _data;
        [SerializeField] private LineRenderer _line;
        [SerializeField] private Material _reelMaterial;
        [SerializeField] private Transform _hoseIn; // GLB washer: Socket_HoseIn (back of the gun); graybox: the nozzle

        public readonly NetworkVariable<Vector3> Anchor = new NetworkVariable<Vector3>();
        public float Length => (_data != null ? _data.balance.tools.hoseLength : 8f) * LengthMultiplier;
        public float LengthMultiplier { get; set; } = 1f; // U03 (T5.1)

        ToolBase _tool;
        Rigidbody _rb;
        readonly Vector3[] _pos = new Vector3[Points], _prev = new Vector3[Points];
        bool _ropeInit;

        void Awake() { _tool = GetComponent<ToolBase>(); _rb = GetComponent<Rigidbody>(); }

        public override void OnNetworkSpawn()
        {
            if (IsServer && Anchor.Value == Vector3.zero) Anchor.Value = transform.position;
            var reel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            reel.name = "HoseReel(local)";
            Destroy(reel.GetComponent<Collider>());
            reel.transform.SetPositionAndRotation(Anchor.Value, Quaternion.Euler(0, 0, 90));
            reel.transform.localScale = new Vector3(0.45f, 0.12f, 0.45f);
            if (_reelMaterial) reel.GetComponent<Renderer>().sharedMaterial = _reelMaterial;
            reel.transform.SetParent(null);
            _reelObject = reel;
        }

        GameObject _reelObject;
        public override void OnNetworkDespawn() { if (_reelObject) Destroy(_reelObject); }

        void Update()
        {
            if (!IsSpawned) return;
            var holder = _tool.HolderPlayer;
            if (holder != null && holder.IsOwner && !_tool.Stowed)
                holder.Motor.SetTether(Anchor.Value, Length, _data != null ? _data.balance.tools.hosePull : 12f);
            else if (holder != null && holder.IsOwner)
                holder.Motor.ClearTether(this);
        }

        void FixedUpdate()
        {
            if (!IsServer || _tool.IsHeld || _rb.isKinematic) return;
            Vector3 d = Anchor.Value - _rb.position;
            float excess = d.magnitude - Length;
            if (excess > 0f) _rb.AddForce(d.normalized * excess * 30f, ForceMode.Acceleration);
        }

        void LateUpdate()
        {
            if (_line == null) return;
            bool show = !_tool.Stowed;
            _line.enabled = show;
            if (!show) return;
            Vector3 a = Anchor.Value, b = _hoseIn != null ? _hoseIn.position : _tool.Nozzle.position;
            if (!_ropeInit)
            {
                for (int i = 0; i < Points; i++) _pos[i] = _prev[i] = Vector3.Lerp(a, b, i / (Points - 1f));
                _ropeInit = true;
            }
            float seg = Mathf.Max(Vector3.Distance(a, b), Length * 0.6f) / (Points - 1);
            float dt = Time.deltaTime;
            for (int i = 1; i < Points - 1; i++)
            {
                var v = (_pos[i] - _prev[i]) * 0.96f;
                _prev[i] = _pos[i];
                _pos[i] += v + Vector3.down * 9.81f * dt * dt;
                if (_pos[i].y < 0.03f) _pos[i].y = 0.03f;
            }
            _pos[0] = a; _pos[Points - 1] = b;
            for (int it = 0; it < 4; it++)
                for (int i = 0; i < Points - 1; i++)
                {
                    var delta = _pos[i + 1] - _pos[i];
                    float dist = delta.magnitude;
                    if (dist < 1e-4f || dist <= seg) continue;
                    var corr = delta * ((dist - seg) / dist) * 0.5f;
                    if (i != 0) _pos[i] += corr;
                    if (i + 1 != Points - 1) _pos[i + 1] -= corr;
                }
            _line.positionCount = Points;
            _line.SetPositions(_pos);
        }
    }
}
