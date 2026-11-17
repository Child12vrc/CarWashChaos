using System;
using CarWash.Core;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Player
{
    /// <summary>
    /// T4.4 ping (GDD §3: MMB, "Look here!"): owner raycasts 60 m → server → every peer shows a marker for 5 s in the
    /// pinger's colour with a label of what was pinged (critter / car / tool / spot). One live ping per player.
    /// </summary>
    public class PlayerPing : NetworkBehaviour
    {
        public struct Ping { public ulong client; public int color; public Vector3 pos; public string label; public float until; }

        /// <summary>Every peer: a ping arrived (HUD PingMarkers draws it).</summary>
        public static event Action<Ping> Pinged;
        public const float Lifetime = 5f;

        PlayerInputReader _input;
        PlayerLook _look;
        PlayerNet _net;
        static readonly RaycastHit[] Hits = new RaycastHit[8];

        void Awake() { _input = GetComponent<PlayerInputReader>(); _look = GetComponent<PlayerLook>(); _net = GetComponent<PlayerNet>(); }

        void Update()
        {
            if (!IsSpawned || !IsOwner || _input == null || !_input.PingPressed) return;
            OwnerPing();
        }

        /// <summary>Owner: ping whatever the crosshair points at (also used by automated checks).</summary>
        public void OwnerPing()
        {
            var ray = _look.AimRay;
            int n = Physics.RaycastNonAlloc(ray, Hits, 60f, ~(1 << Layers.FPArms), QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; RaycastHit hit = default; bool any = false;
            for (int i = 0; i < n; i++)
                if (Hits[i].distance < best && !Hits[i].collider.transform.IsChildOf(transform)) { best = Hits[i].distance; hit = Hits[i]; any = true; }
            var pos = any ? hit.point : ray.origin + ray.direction * 20f;
            PingRpc(pos, any ? Label(hit.collider) : "여기!");
        }

        static string Label(Collider c)
        {
            var critter = c.GetComponentInParent<Critters.CritterBase>();
            if (critter != null) return critter.DisplayName + "!";
            var v = c.GetComponentInParent<Vehicles.Vehicle>();
            if (v != null && v.Data != null) return v.Data.displayName;
            var tool = c.GetComponentInParent<Tools.ToolBase>();
            if (tool != null && tool.Data != null) return tool.Data.displayName;
            return "여기!";
        }

        [Rpc(SendTo.Server)]
        void PingRpc(Vector3 pos, string label, RpcParams p = default)
        {
            if (p.Receive.SenderClientId != OwnerClientId) return;
            if (label != null && label.Length > 24) label = label.Substring(0, 24);
            ShowRpc(pos, label);
        }

        [Rpc(SendTo.Everyone)]
        void ShowRpc(Vector3 pos, string label)
        {
            Pinged?.Invoke(new Ping { client = OwnerClientId, color = Mathf.Max(0, _net.ColorIndex.Value), pos = pos, label = label, until = Time.time + Lifetime });
        }
    }
}
