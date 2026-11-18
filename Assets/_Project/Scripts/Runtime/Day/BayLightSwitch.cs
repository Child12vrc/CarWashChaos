using CarWash.Player;
using CarWash.Vehicles;
using CarWash.Visual;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Day
{
    /// <summary>
    /// M4.9 G7: the wall switch of one bay. E toggles the bay's ceiling lights for everybody (one bool NetworkVariable,
    /// nothing else is synced). The server switches every bay on when night falls and off in the morning (G1 clock);
    /// players can override any time. The rocker + its little LED show the state.
    /// </summary>
    public class BayLightSwitch : NetworkBehaviour, IInteractable
    {
        [SerializeField] private Transform _rocker;
        [SerializeField] private Renderer _led;
        [SerializeField] private Material _ledOn, _ledOff;

        public readonly NetworkVariable<bool> On = new NetworkVariable<bool>(false);
        int _bay = -1;
        bool _wasNight;

        public int BayIndex
        {
            get
            {
                if (_bay < 0 && LotPaths.Instance != null)
                {
                    float best = float.MaxValue;
                    foreach (var b in LotPaths.Instance.Bays)
                    {
                        float d = (b.transform.position - transform.position).sqrMagnitude;
                        if (d < best) { best = d; _bay = b.Index; }
                    }
                }
                return _bay;
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) { _wasNight = DayNightCycle.Night > 0.5f; On.Value = _wasNight; }
            On.OnValueChanged += Changed;
            Changed(false, On.Value);
        }
        public override void OnNetworkDespawn() => On.OnValueChanged -= Changed;

        void Changed(bool was, bool now)
        {
            var bl = BayLights.ForBay(BayIndex);
            if (bl != null) bl.On = now;
            if (_rocker != null) _rocker.localRotation = Quaternion.Euler(0f, 0f, now ? -14f : 14f);
            if (_led != null) _led.sharedMaterial = now ? _ledOn : _ledOff;
            if (IsSpawned && was != now) Core.Services.Get<Core.AudioService>()?.Play3D("light_switch", transform.position);
        }

        void Update()
        {
            if (!IsSpawned) return;
            var bl = BayLights.ForBay(BayIndex);
            if (bl != null && bl.On != On.Value) bl.On = On.Value; // the scene lights may wake up after the spawn
            if (!IsServer) return;
            bool night = DayNightCycle.Night > 0.5f;
            if (night != _wasNight) { _wasNight = night; On.Value = night; } // auto on at dusk / off in the morning
        }

        public string Prompt => On.Value ? $"차고지 {BayIndex + 1} 조명 끄기" : $"차고지 {BayIndex + 1} 조명 켜기";
        public bool CanInteract(PlayerNet player) => true;
        public void Interact(PlayerNet player) => On.Value = !On.Value; // server (PlayerInteractor's RPC)
    }
}
