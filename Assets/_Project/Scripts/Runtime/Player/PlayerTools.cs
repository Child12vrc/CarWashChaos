using CarWash.Tools;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Player
{
    /// <summary>
    /// Use intents for the active inventory tool (TECH S01/S03). Owner sends UseRpc(origin, dir) at ~30 Hz while LMB is held;
    /// the server runs the tool's hit logic. RMB = nozzle mode, 1-4 = slot, G = drop (when not carrying a prop).
    /// </summary>
    public class PlayerTools : NetworkBehaviour
    {
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private PlayerLook _look;
        [SerializeField] private PlayerGrabber _grabber;
        [SerializeField] private PlayerInventory _inventory;
        [SerializeField] private float _sendRate = 30f;

        public ToolBase Equipped => _inventory != null ? _inventory.Active : null;
        public PlayerInventory Inventory => _inventory;
        float _sendT;
        bool _wasUsing;

        void Update()
        {
            if (!IsSpawned || !IsOwner) return;
            var tool = Equipped;
            bool handsFree = _grabber == null || _grabber.Held == null;
            bool using_ = tool != null && handsFree && _input.UseHeld && tool.OwnerWantsUse(_input);
            if (using_)
            {
                _sendT += Time.deltaTime;
                if (_sendT >= 1f / _sendRate || !_wasUsing)
                {
                    var ray = _look.AimRay;
                    float useDt = Mathf.Min(_sendT, 0.1f);
                    UseRpc(ray.origin, ray.direction, useDt);
                    if (!IsServer) tool.OwnerPredict(ray.origin, ray.direction.normalized, Mathf.Clamp(useDt, 0.005f, 0.1f)); // P1: instant local paint
                    _sendT = 0f;
                }
            }
            else if (_wasUsing) StopRpc();
            _wasUsing = using_;

            if (_input.SlotPressed >= 0) SelectRpc(_input.SlotPressed);
            if (tool != null && handsFree && _input.AltPressed) AltRpc();
            if (tool != null && handsFree && _input.DropPressed) DropRpc();
        }

        // ---------------- server ----------------
        public void ServerEquip(ToolBase tool) => _inventory.ServerAdd(tool);
        public void ServerDrop() => _inventory.ServerDropSlot(_inventory.ActiveSlot.Value);

        [Rpc(SendTo.Server)]
        void UseRpc(Vector3 origin, Vector3 dir, float dt)
        {
            var tool = Equipped;
            if (tool == null) return;
            if ((origin - _look.Head.position).sqrMagnitude > 2.25f) origin = _look.Head.position; // aim from own head
            if (!tool.Using.Value) tool.Using.Value = true;
            tool.ServerUse(origin, dir.normalized, Mathf.Clamp(dt, 0.005f, 0.1f));
        }

        [Rpc(SendTo.Server)]
        void StopRpc() { if (Equipped != null) Equipped.Using.Value = false; }

        [Rpc(SendTo.Server)]
        void AltRpc() { (Equipped as SprayTool)?.ServerToggleNozzle(); }

        [Rpc(SendTo.Server)]
        void DropRpc() => ServerDrop();

        [Rpc(SendTo.Server)]
        void SelectRpc(int slot) => _inventory.ServerSelect(slot);
    }
}
