using CarWash.Core;
using CarWash.Data;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Player
{
    /// <summary>
    /// E-interaction (TECH S02): owner raycasts 2.5m for an IInteractable on a NetworkObject, shows its prompt,
    /// and asks the server to run Interact (server re-validates range + CanInteract).
    /// </summary>
    public class PlayerInteractor : NetworkBehaviour
    {
        [SerializeField] private DataRegistry _data;
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private PlayerLook _look;

        readonly RaycastHit[] _hits = new RaycastHit[8];
        PlayerNet _self;

        public IInteractable Target { get; private set; }
        public NetworkObject TargetObject { get; private set; }

        void Awake() => _self = GetComponent<PlayerNet>();

        void Update()
        {
            if (!IsSpawned || !IsOwner) return;
            FindTarget();
            if (_input.InteractPressed && TargetObject != null)
            {
                bool aimedNb = Target is NetworkBehaviour;
                InteractRpc(TargetObject, aimedNb, aimedNb ? new NetworkBehaviourReference((NetworkBehaviour)Target) : default);
            }
        }

        void FindTarget()
        {
            Target = null; TargetObject = null;
            var ray = _look.AimRay;
            int n = Physics.RaycastNonAlloc(ray, _hits, _data.balance.player.interactRange, Layers.InteractMask, QueryTriggerInteraction.Collide);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (_hits[i].distance >= best) continue;
                var it = _hits[i].collider.GetComponentInParent<IInteractable>();
                if (it == null || !it.CanInteract(_self)) continue;
                var no = (it as Component)?.GetComponentInParent<NetworkObject>();
                if (no == null) continue;
                best = _hits[i].distance; Target = it; TargetObject = no;
            }
        }

        public string PromptText() => Target != null ? "[E] " + Target.Prompt : null;

        [Rpc(SendTo.Server)]
        void InteractRpc(NetworkObjectReference target, bool hasAimed, NetworkBehaviourReference aimed)
        {
            if (!target.TryGet(out var no)) return;
            if (Vector3.Distance(_look.Head.position, no.transform.position) > _data.balance.player.interactRange + 6f) return; // generous for big vehicles
            // the aimed component itself (Door_R / Trunk on a car, not just the first door), when it belongs to that object
            if (hasAimed && aimed.TryGet(out NetworkBehaviour nb) && nb.NetworkObject == no && nb is IInteractable aimedIt)
            {
                if (aimedIt.CanInteract(_self)) aimedIt.Interact(_self);
                return;
            }
            foreach (var it in no.GetComponentsInChildren<IInteractable>())
                if (it.CanInteract(_self)) { it.Interact(_self); break; }
        }
    }
}
