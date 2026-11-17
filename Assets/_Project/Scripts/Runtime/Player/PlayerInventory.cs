using System;
using CarWash.Tools;
using Unity.Netcode;

namespace CarWash.Player
{
    /// <summary>
    /// 4 tool slots (TECH S02: NetworkList&lt;NetworkObjectReference&gt;). Server-authoritative: E adds a tool to the first free
    /// slot (full → swaps with the active one), 1-4 selects, G drops the active tool. Inactive tools are stowed (hidden).
    /// </summary>
    public class PlayerInventory : NetworkBehaviour
    {
        public const int SlotCount = 4;

        NetworkList<NetworkObjectReference> _slots;
        public readonly NetworkVariable<int> ActiveSlot = new NetworkVariable<int>(0);
        public event Action Changed;

        void Awake() => _slots = new NetworkList<NetworkObjectReference>();

        public override void OnNetworkSpawn()
        {
            if (IsServer && _slots.Count == 0)
                for (int i = 0; i < SlotCount; i++) _slots.Add(default);
            _slots.OnListChanged += _ => Refresh();
            ActiveSlot.OnValueChanged += (_, __) => Refresh();
            Refresh();
        }

        public ToolBase Get(int i)
        {
            if (_slots == null || i < 0 || i >= _slots.Count) return null;
            return _slots[i].TryGet(out var no) ? no.GetComponent<ToolBase>() : null;
        }

        public ToolBase Active => Get(ActiveSlot.Value);

        void Refresh()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                var t = Get(i);
                if (t != null) t.SetStowed(i != ActiveSlot.Value);
            }
            Changed?.Invoke();
        }

        // ---------------- server ----------------
        public bool ServerAdd(ToolBase tool)
        {
            if (!IsServer || tool == null || tool.IsHeld) return false;
            int idx = -1;
            for (int k = 0; k < SlotCount; k++)
            {
                int i = (ActiveSlot.Value + k) % SlotCount;
                if (Get(i) == null) { idx = i; break; }
            }
            if (idx < 0) { ServerDropSlot(ActiveSlot.Value); idx = ActiveSlot.Value; }
            if (!tool.ServerSetHolder(OwnerClientId)) return false;
            _slots[idx] = tool.NetworkObject;
            ActiveSlot.Value = idx;
            Refresh();
            Core.GameEvents.RaiseToolEquipped(OwnerClientId, tool.ToolId);
            return true;
        }

        public void ServerSelect(int i)
        {
            if (!IsServer || i < 0 || i >= SlotCount || i == ActiveSlot.Value) return;
            var old = Active;
            if (old != null) old.Using.Value = false;
            ActiveSlot.Value = i;
        }

        public void ServerDropSlot(int i)
        {
            var t = Get(i);
            if (!IsServer || t == null) return;
            var look = GetComponent<PlayerLook>();
            var head = look.Head;
            _slots[i] = default;
            t.SetStowed(false);
            t.ServerDropAt(head.position + head.forward * 0.8f, head.forward * 1.5f);
        }

        public void ServerDropAll()
        {
            for (int i = 0; i < SlotCount; i++) ServerDropSlot(i);
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) ServerDropAll();
        }
    }
}
