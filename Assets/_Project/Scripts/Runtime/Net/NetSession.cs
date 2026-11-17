using System;
using Unity.Multiplayer.Tools.NetworkSimulator.Runtime;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarWash.Net
{
    /// <summary>
    /// Wraps NetworkManager + transport selection (TECH S01). Dev = UTP 127.0.0.1:24565.
    /// Command line: -host  |  -join &lt;ip&gt;  |  -netsim delayMs,jitterMs,lossPercent (dev builds/editor: Multiplayer
    /// Tools Network Simulator on this peer). Losing the connection (host quit, timeout, kicked) shuts down cleanly and
    /// returns to the main menu with the reason (P1).
    /// </summary>
    public class NetSession : MonoBehaviour
    {
        public const ushort DefaultPort = 24565;
        public const int MaxPlayers = 4;
        /// <summary>P1: detect a dead peer in 10 s instead of UTP's 30 s default.</summary>
        public const int DisconnectTimeoutMs = 10000, HeartbeatMs = 500;
        public const int InterpolationBufferTicks = 3;

        [SerializeField] private NetBackend _backend = NetBackend.Utp;
        [SerializeField] private NetworkManager _manager;

        public NetworkManager Manager => _manager;
        public bool IsActive => _manager != null && (_manager.IsServer || _manager.IsClient);
        public event Action<ulong> PeerJoined;
        public event Action<ulong> PeerLeft;

        public static bool ArgHost { get; private set; }
        public static string ArgJoin { get; private set; }
        /// <summary>-netsim delay,jitter,loss (ms, ms, %), or null.</summary>
        public static int[] ArgNetSim { get; private set; }
        /// <summary>Why the last session ended (shown by the main menu), null when the player left on purpose.</summary>
        public static string LastDisconnectReason { get; set; }
        public static NetSession Instance { get; private set; }

        void Awake()
        {
            Instance = this;
            // P1: 3 extra ticks (~100 ms at 30 Hz) of interpolation buffer — measured: with ±30 ms jitter + 1-5% loss,
            // 1 extra tick still left 30-48% of rendered frames without movement on remote players (buffer underruns).
            Unity.Netcode.Components.NetworkTransform.InterpolationBufferTickOffset = InterpolationBufferTicks;
            if (_manager == null) _manager = GetComponent<NetworkManager>();
            ParseArgs(Environment.GetCommandLineArgs());
            _manager.OnClientConnectedCallback += id =>
            {
                if (_manager.IsServer) Debug.Log($"[NET] client {id} connected ({_manager.ConnectedClientsIds.Count} peers)");
                else if (id == _manager.LocalClientId) Debug.Log($"[NET] connected to server as client {id}");
                PeerJoined?.Invoke(id);
            };
            _manager.OnClientDisconnectCallback += OnDisconnect;
            _manager.ConnectionApprovalCallback = Approve;
            var utp = Utp;
            if (utp != null) { utp.DisconnectTimeoutMS = DisconnectTimeoutMs; utp.HeartbeatTimeoutMS = HeartbeatMs; }
        }

        public static void ParseArgs(string[] args)
        {
            ArgHost = false; ArgJoin = null; ArgNetSim = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-host") ArgHost = true;
                else if (args[i] == "-join") ArgJoin = i + 1 < args.Length ? args[i + 1] : "127.0.0.1";
                else if (args[i] == "-netsim" && i + 1 < args.Length) ArgNetSim = ParseNetSim(args[i + 1]);
            }
        }

        /// <summary>"150,30,5" → {150, 30, 5}; invalid → null (EditMode tested).</summary>
        public static int[] ParseNetSim(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            var parts = s.Split(',');
            var r = new int[3];
            for (int i = 0; i < 3; i++)
            {
                if (i >= parts.Length) { r[i] = 0; continue; }
                if (!int.TryParse(parts[i].Trim(), out r[i]) || r[i] < 0) return null;
            }
            r[2] = Mathf.Min(r[2], 100);
            return r;
        }

        void OnDisconnect(ulong id)
        {
            PeerLeft?.Invoke(id);
            if (_manager.IsServer && id != NetworkManager.ServerClientId) Debug.Log($"[NET] client {id} left ({_manager.ConnectedClientsIds.Count} peers)");
            // this client lost the server (host quit / timeout / rejected): clean shutdown → main menu with the reason
            if (!_manager.IsServer && (id == _manager.LocalClientId || id == NetworkManager.ServerClientId))
            {
                string reason = _manager.DisconnectReason;
                LastDisconnectReason = FriendlyReason(reason);
                Debug.Log($"[NET] disconnected from server: {LastDisconnectReason}");
                _manager.Shutdown();
                if (SceneManager.GetActiveScene().name != Core.SceneNames.MainMenu) SceneManager.LoadScene(Core.SceneNames.MainMenu);
            }
        }

        /// <summary>NGO/UTP disconnect text → what the player reads (EditMode tested).</summary>
        public static string FriendlyReason(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "호스트와의 연결이 끊어졌습니다.";
            if (raw.Contains("최대") || raw.Contains("가득")) return raw;                       // our own approval message
            if (raw.Contains("ClosedByRemote")) return "호스트가 게임을 종료했습니다.";
            if (raw.Contains("Timeout") || raw.Contains("timed out")) return "호스트 응답이 없습니다 (연결 시간 초과).";
            if (raw.Contains("MaxConnectionAttempts") || raw.Contains("Failed to connect")) return "호스트에 접속할 수 없습니다. 주소를 확인해 주세요.";
            return "호스트와의 연결이 끊어졌습니다.";
        }

        void Approve(NetworkManager.ConnectionApprovalRequest req, NetworkManager.ConnectionApprovalResponse res)
        {
            bool full = _manager.ConnectedClientsIds.Count >= MaxPlayers;
            res.Approved = !full;
            res.Reason = full ? "서버가 가득 찼습니다 (최대 4명)" : null;
            res.CreatePlayerObject = false; // PlayerSpawner spawns players once the game scene is loaded
        }

        UnityTransport Utp => _manager.GetComponent<UnityTransport>();

        public bool Host(ushort port = DefaultPort)
        {
            if (_backend != NetBackend.Utp) throw new NotSupportedException("Steam backend arrives in M7");
            LastDisconnectReason = null;
            Utp.SetConnectionData("0.0.0.0", port, "0.0.0.0");
            ApplyNetSim();
            return _manager.StartHost();
        }

        public bool Join(string address, ushort port = DefaultPort)
        {
            LastDisconnectReason = null;
            Utp.SetConnectionData(string.IsNullOrEmpty(address) ? "127.0.0.1" : address, port);
            ApplyNetSim();
            return _manager.StartClient();
        }

        /// <summary>Editor/dev: simulate a bad connection on this peer (set before Host/Join, or call again any time).</summary>
        public static int[] EditorNetSim;

        public void ApplyNetSim()
        {
            var p = ArgNetSim ?? EditorNetSim;
            if (p == null) return;
            var sim = GetComponent<NetworkSimulator>();
            if (sim == null) sim = gameObject.AddComponent<NetworkSimulator>();
            sim.ConnectionPreset = NetworkSimulatorPreset.Create($"netsim {p[0]}/{p[1]}/{p[2]}", "", p[0], p[1], 0, p[2]);
            Debug.Log($"[NET] network simulator: delay {p[0]} ms, jitter ±{p[1]} ms, loss {p[2]}%");
        }

        public void Leave()
        {
            LastDisconnectReason = null;
            if (_manager != null && IsActive) _manager.Shutdown();
        }
    }
}
