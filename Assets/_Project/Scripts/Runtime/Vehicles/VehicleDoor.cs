using CarWash.Player;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Vehicles
{
    /// <summary>
    /// Door / trunk on a vehicle (TECH S06: rotates the hinge transform, IInteractable, NetworkVariable&lt;bool&gt; open).
    /// Only usable while the vehicle is parked. Opening raises Vehicle.DoorOpened on the server (surprises, T3.6)
    /// and reveals the interior trash next to it.
    /// </summary>
    public class VehicleDoor : NetworkBehaviour, IInteractable
    {
        [SerializeField] private string _label = "문";
        [SerializeField] private Transform _hinge;
        [SerializeField] private Vector3 _openEuler = new Vector3(0, -70f, 0);
        [SerializeField] private float _speed = 4f;

        public readonly NetworkVariable<bool> Open = new NetworkVariable<bool>();
        public Vehicle Vehicle { get; private set; }
        public bool IsTrunk => _label.Contains("트렁크");
        float _t;

        public void Bind(Vehicle v) => Vehicle = v;

        public string Prompt => (Open.Value ? $"{_label} 닫기" : $"{_label} 열기");
        public bool CanInteract(PlayerNet p) => Vehicle != null && Vehicle.Stationary;

        public void Interact(PlayerNet p)
        {
            if (!CanInteract(p)) return;
            ServerSetOpen(!Open.Value);
        }

        public void ServerSetOpen(bool open)
        {
            if (!IsServer || Open.Value == open) return;
            Open.Value = open;
            if (open && Vehicle != null) Vehicle.ServerNotifyDoorOpened(this);
        }

        void Update()
        {
            _t = Mathf.MoveTowards(_t, Open.Value ? 1f : 0f, Time.deltaTime * _speed);
            if (_hinge) _hinge.localRotation = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(_openEuler), _t);
        }
    }
}
