using Unity.Netcode;
using UnityEngine;

namespace CarWash.Player
{
    /// <summary>
    /// Player identity: color slot (4 uniform colors, ASSETS) assigned by server; tints the Y Bot body.
    /// </summary>
    public class PlayerNet : NetworkBehaviour
    {
        public static readonly Color[] Colors =
        {
            new Color(0.20f, 0.55f, 0.95f), // 파랑
            new Color(0.95f, 0.30f, 0.25f), // 빨강
            new Color(0.30f, 0.80f, 0.35f), // 초록
            new Color(0.98f, 0.80f, 0.20f), // 노랑
        };
        public static readonly string[] ColorNames = { "파랑", "빨강", "초록", "노랑" };

        [SerializeField] private Renderer[] _bodyRenderers;

        public readonly NetworkVariable<int> ColorIndex = new NetworkVariable<int>(-1);
        public PlayerMotor Motor { get; private set; }
        public PlayerLook Look { get; private set; }
        public PlayerStatus Status { get; private set; }

        MaterialPropertyBlock _mpb;

        void Awake()
        {
            Motor = GetComponent<PlayerMotor>();
            Look = GetComponent<PlayerLook>();
            Status = GetComponent<PlayerStatus>();
        }

        /// <summary>M4.6 B3: where the server placed this player. The owner moves its (owner-authoritative) body there on spawn —
        /// the spawn transform sometimes did not reach the owning client, which then started at the origin (inside closed bay 2).</summary>
        public readonly NetworkVariable<Vector3> SpawnPos = new NetworkVariable<Vector3>();

        public override void OnNetworkSpawn()
        {
            if (IsOwner && SpawnPos.Value != Vector3.zero)
            {
                if ((transform.position - SpawnPos.Value).sqrMagnitude > 0.25f)
                    Debug.Log($"[PlayerNet] owner spawn fix: was at {transform.position} → {SpawnPos.Value}");
                Motor.Teleport(SpawnPos.Value);
                Motor.SetSpawn(SpawnPos.Value);
                StartCoroutine(HoldSpawn()); // the network transform may still apply a stale origin pose for a few frames
            }
            ColorIndex.OnValueChanged += (_, v) => ApplyColor(v);
            if (IsServer && ColorIndex.Value < 0) ColorIndex.Value = PickFreeColor();
            ApplyColor(ColorIndex.Value);
            // P1: remote copies draw the body smoothed toward the networked root
            if (!IsOwner && _bodyRenderers.Length > 0)
            {
                var body = transform.Find("Body");
                if (body != null)
                {
                    if (!TryGetComponent<Net.RemoteVisualSmoother>(out var smoother)) smoother = gameObject.AddComponent<Net.RemoteVisualSmoother>();
                    smoother.Bind(body);
                }
            }
            // First-person: hide own body but keep its shadow (P5 shows it again while in third person).
            if (IsOwner)
                foreach (var r in _bodyRenderers) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        }

        /// <summary>PlayerAppearance: the active look's renderers (body + colour outline).</summary>
        public void SetBodyRenderers(Renderer[] renderers)
        {
            _bodyRenderers = renderers ?? System.Array.Empty<Renderer>();
            if (IsSpawned && IsOwner) SetOwnBodyVisible(_ownBodyVisible);
            ApplyColor(ColorIndex.Value);
        }

        bool _ownBodyVisible;

        System.Collections.IEnumerator HoldSpawn()
        {
            for (int i = 0; i < 30; i++)
            {
                yield return null;
                var d = transform.position - SpawnPos.Value; d.y = 0f;
                if (d.sqrMagnitude > 4f && transform.position.sqrMagnitude < 1f) { Debug.Log($"[PlayerNet] owner spawn fix (late): {transform.position} → {SpawnPos.Value}"); Motor.Teleport(SpawnPos.Value); }
            }
        }

        /// <summary>Owner: show the own body (third-person comfort camera) or only its shadow (first person).</summary>
        public void SetOwnBodyVisible(bool visible)
        {
            _ownBodyVisible = visible;
            foreach (var r in _bodyRenderers) r.shadowCastingMode = visible ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        }

        // ---------------- lobby (T4.1): ready flag + colour pick, owner requests, server validates ----------------
        public readonly NetworkVariable<bool> Ready = new NetworkVariable<bool>();

        public void RequestToggleReady() => ToggleReadyRpc();
        public void RequestNextColor() => NextColorRpc();

        [Rpc(SendTo.Server)]
        void ToggleReadyRpc(RpcParams p = default)
        {
            if (p.Receive.SenderClientId != OwnerClientId) return;
            Ready.Value = !Ready.Value;
        }

        [Rpc(SendTo.Server)]
        void NextColorRpc(RpcParams p = default)
        {
            if (p.Receive.SenderClientId != OwnerClientId) return;
            var used = new bool[Colors.Length];
            foreach (var o in FindObjectsByType<PlayerNet>(FindObjectsSortMode.None))
                if (o != this && o.ColorIndex.Value >= 0) used[o.ColorIndex.Value % Colors.Length] = true;
            for (int k = 1; k <= Colors.Length; k++)
            {
                int c = (ColorIndex.Value + k) % Colors.Length;
                if (!used[c]) { ColorIndex.Value = c; return; }
            }
        }

        // ---------------- P2 unstuck: owner asks, server validates (cooldown) and picks a safe spot ----------------
        /// <summary>Server time when unstuck can be used again (replicated for the button's countdown).</summary>
        public readonly NetworkVariable<float> UnstuckReadyAt = new NetworkVariable<float>();
        public float UnstuckCooldownLeft => NetworkManager != null ? Mathf.Max(0f, UnstuckReadyAt.Value - (float)NetworkManager.ServerTime.Time) : 0f;
        /// <summary>Owner: result of the last request ("" until answered).</summary>
        public string LastUnstuckResult { get; private set; } = "";

        public void RequestUnstuck() { LastUnstuckResult = ""; UnstuckRpc(); }

        [Rpc(SendTo.Server)]
        void UnstuckRpc(RpcParams p = default)
        {
            if (p.Receive.SenderClientId != OwnerClientId) return;
            float now = (float)NetworkManager.ServerTime.Time;
            if (!Unstuck.CanUse(now, UnstuckReadyAt.Value)) { UnstuckResultRpc(false, Vector3.zero, $"{Mathf.CeilToInt(UnstuckReadyAt.Value - now)}초 후에 다시 쓸 수 있어요"); return; }
            // drop what's in the hands (held prop + active tool) where we were stuck
            GetComponent<PlayerGrabber>()?.ServerDropHeld(0f);
            var inv = GetComponent<PlayerInventory>();
            if (inv != null && inv.ActiveSlot.Value >= 0 && inv.Get(inv.ActiveSlot.Value) != null) inv.ServerDropSlot(inv.ActiveSlot.Value);
            var spawner = FindFirstObjectByType<Net.PlayerSpawner>();
            var fallback = spawner != null ? spawner.SpawnPointFor(OwnerClientId).position : transform.position + Vector3.up;
            var spot = Unstuck.FindSafeSpot(transform.position, fallback);
            UnstuckReadyAt.Value = now + Unstuck.Cooldown;
            Debug.Log($"[UNSTUCK] client {OwnerClientId}: {transform.position:F1} → {spot:F1}");
            UnstuckResultRpc(true, spot, "안전한 곳으로 이동했어요");
        }

        [Rpc(SendTo.Owner)]
        void UnstuckResultRpc(bool moved, Vector3 spot, string message)
        {
            if (moved) Motor.Teleport(spot);
            LastUnstuckResult = message;
        }

        int PickFreeColor()
        {
            var used = new bool[Colors.Length];
            foreach (var p in FindObjectsByType<PlayerNet>(FindObjectsSortMode.None))
                if (p != this && p.ColorIndex.Value >= 0) used[p.ColorIndex.Value % Colors.Length] = true;
            for (int i = 0; i < used.Length; i++) if (!used[i]) return i;
            return (int)(OwnerClientId % (ulong)Colors.Length);
        }

        void ApplyColor(int index)
        {
            if (index < 0) return;
            _mpb ??= new MaterialPropertyBlock();
            var c = Colors[index % Colors.Length];
            foreach (var r in _bodyRenderers)
            {
                if (r == null) continue;
                // Quaternius looks keep their outfit colours: only the outline shell (and a Y Bot fallback body) takes the player colour
                bool outline = r.sharedMaterial != null && r.sharedMaterial.name.Contains("Outline");
                bool ybot = r.sharedMaterial != null && r.sharedMaterial.name.ToLower().Contains("alpha");
                if (!outline && !ybot) continue;
                for (int m = 0; m < r.sharedMaterials.Length; m++)
                {
                    r.GetPropertyBlock(_mpb, m);
                    bool joints = r.sharedMaterials[m] != null && r.sharedMaterials[m].name.ToLower().Contains("joint");
                    var col = joints ? c * 0.35f : c;
                    col.a = 1f;
                    _mpb.SetColor("_BaseColor", col);
                    _mpb.SetColor("_Color", col);
                    r.SetPropertyBlock(_mpb, m);
                }
            }
        }
    }
}
