using Unity.Netcode;
using UnityEngine;

namespace CarWash.Player
{
    /// <summary>Status flags + timers (TECH S02). Owner writes, everyone reads (HUD / body animation).</summary>
    public class PlayerStatus : NetworkBehaviour
    {
        readonly NetworkVariable<byte> _flags = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        float _knockedT, _stungT, _soakedT;

        public StatusFlags Flags => (StatusFlags)_flags.Value;
        public bool Has(StatusFlags f) => (Flags & f) != 0;
        /// <summary>Owner: local timer; everyone else: the replicated flag (P1 — remote copies used to always read false).</summary>
        public bool IsKnocked => IsOwner ? _knockedT > 0f : Has(StatusFlags.Knocked);

        /// <summary>Movement multiplier from slow effects.</summary>
        public float SpeedMultiplier(Data.BalanceData.PlayerTuning t)
        {
            float m = 1f;
            if (_soakedT > 0f) m *= 1f - t.soakedSlow;
            if (_stungT > 0f) m *= 1f - t.stungSlow;
            return m;
        }

        /// <summary>Server-side helpers: forward to the owner, who simulates status timers.</summary>
        public void ServerSoak(float seconds) { if (IsOwner) Soak(seconds); else if (IsServer) SoakRpc(seconds); }
        public void ServerSting(float seconds) { if (IsOwner) Sting(seconds); else if (IsServer) StingRpc(seconds); }
        [Rpc(SendTo.Owner)] void SoakRpc(float s) => Soak(s);
        [Rpc(SendTo.Owner)] void StingRpc(float s) => Sting(s);

        public void Knock(float seconds) { _knockedT = Mathf.Max(_knockedT, seconds); if (IsOwner) PlayerLook.Local?.AddShake(0.7f); }
        /// <summary>Owner: back on our feet (ragdoll finished getting up) → controls return immediately.</summary>
        public void ClearKnock() => _knockedT = 0f;
        public void Soak(float seconds) { _soakedT = Mathf.Max(_soakedT, seconds); }
        public void Sting(float seconds) { _stungT = Mathf.Max(_stungT, seconds); if (IsOwner) PlayerLook.Local?.AddShake(0.45f); } // GDD: sting = screen wobble

        /// <summary>Owner tick: decays timers and publishes flags.</summary>
        public void OwnerTick(float dt, bool crouching, bool inFoam, bool carrying)
        {
            _knockedT = Mathf.Max(0f, _knockedT - dt);
            _stungT = Mathf.Max(0f, _stungT - dt);
            _soakedT = Mathf.Max(0f, _soakedT - dt);
            var f = StatusFlags.None;
            if (_knockedT > 0f) f |= StatusFlags.Knocked;
            if (_stungT > 0f) f |= StatusFlags.Stung;
            if (_soakedT > 0f) f |= StatusFlags.Soaked;
            if (carrying) f |= StatusFlags.Carrying;
            if (crouching) f |= StatusFlags.Crouching;
            if (inFoam) f |= StatusFlags.InFoam;
            if ((byte)f != _flags.Value) _flags.Value = (byte)f;
        }
    }
}
