using CarWash.Data;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Economy
{
    /// <summary>
    /// Shared money + reputation (TECH: EconomyState NetworkBehaviour). Server-only writes; clients read.
    /// Lives on the GameDirector prefab next to DayManager. Shop purchases (TryBuy) arrive with T5.1.
    /// </summary>
    public class EconomyState : NetworkBehaviour
    {
        public static EconomyState Instance { get; private set; }

        [SerializeField] private DataRegistry _data;

        public readonly NetworkVariable<int> Money = new NetworkVariable<int>();
        public readonly NetworkVariable<float> Rep = new NetworkVariable<float>();

        public BalanceData.EconomyTuning Tuning => _data.balance.economy;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (IsServer) ServerReset();
        }

        public override void OnNetworkDespawn() { if (Instance == this) Instance = null; }

        public void ServerReset()
        {
            Money.Value = Tuning.startMoney;
            Rep.Value = Tuning.repStart;
        }

        public void ServerAddMoney(int amount) { if (IsServer) Money.Value += amount; }
        public void ServerAddRep(float delta) { if (IsServer) Rep.Value = EconomyCalc.ApplyRep(Rep.Value, delta, Tuning); }
    }
}
