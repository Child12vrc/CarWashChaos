using CarWash.Core;
using CarWash.Net;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CarWash.UI
{
    /// <summary>
    /// Main menu (built in code): 호스트 / 참가(IP) / 종료. Command line -host / -join auto-start.
    /// DESIGN-GAP: Lobby scene (ready, color) arrives in T4.1; until then host loads SudsyLot directly.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        public static MainMenuUI Instance { get; private set; }
        public Transform Root { get; private set; }

        InputField _ip;
        Text _status;

        void Awake()
        {
            Instance = this;
            UiFactory.EnsureEventSystem();
            var canvas = UiFactory.Canvas("MainMenuCanvas", 0, transform);
            Root = canvas.transform;
            var c = new Vector2(0.5f, 0.5f);
            UiFactory.Panel("Bg", Root, c, Vector2.zero, new Vector2(4000, 4000), new Color(0.09f, 0.13f, 0.2f));
            UiFactory.Label("Title", Root, "세차장 대소동", 96, new Vector2(0.5f, 0.75f), Vector2.zero, new Vector2(1400, 160));
            UiFactory.Label("Sub", Root, "CarWashChaos (가제)", 32, new Vector2(0.5f, 0.66f), Vector2.zero, new Vector2(1000, 60), color: new Color(0.7f, 0.85f, 1f));

            UiFactory.FramePanel("MenuPanel", Root, c, new Vector2(0, -60), new Vector2(620, 400), new Color(0.12f, 0.17f, 0.3f, 0.9f));
            UiFactory.Button("Host", Root, "호스트 시작", c, new Vector2(0, 40), new Vector2(420, 80), Host);
            _ip = UiFactory.Input("Ip", Root, "127.0.0.1", c, new Vector2(-110, -60), new Vector2(300, 70));
            UiFactory.Button("Join", Root, "참가", c, new Vector2(160, -60), new Vector2(200, 70), Join);
            UiFactory.Button("Quit", Root, "종료", c, new Vector2(0, -160), new Vector2(420, 70), Application.Quit);
            _status = UiFactory.Label("Status", Root, "", 26, new Vector2(0.5f, 0.2f), Vector2.zero, new Vector2(1400, 60), color: new Color(1f, 0.85f, 0.5f));

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        static bool _autoStarted;
        /// <summary>-autoreconnect (dev/P1): after a lost connection, join the same address again automatically.</summary>
        static bool AutoReconnect => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-autoreconnect") >= 0;

        void Start()
        {
            Services.Get<GameStateMachine>()?.Force(GameState.Menu);
            if (!string.IsNullOrEmpty(NetSession.LastDisconnectReason))
                _status.text = NetSession.LastDisconnectReason + "  (다시 참가할 수 있습니다)";
            bool again = _autoStarted && AutoReconnect && NetSession.ArgJoin != null;
            if (_autoStarted && !again) return; // command-line auto start only once
            _autoStarted = true;
            if (NetSession.ArgHost) Host();
            else if (NetSession.ArgJoin != null) { _ip.text = NetSession.ArgJoin; if (again) Invoke(nameof(Join), 3f); else Join(); }
        }

        NetSession Net => Services.Get<NetSession>();

        void Host()
        {
            if (Net == null) { _status.text = "Bootstrap 씬에서 시작해 주세요"; return; }
            if (!Net.Host()) { _status.text = "호스트 시작 실패 (포트 사용 중?)"; return; }
            Services.Get<GameStateMachine>()?.TryChange(GameState.Lobby);
            NetworkManager.Singleton.SceneManager.LoadScene(SceneNames.SudsyLot, LoadSceneMode.Single);
        }

        void Join()
        {
            if (Net == null) { _status.text = "Bootstrap 씬에서 시작해 주세요"; return; }
            _status.text = $"{_ip.text} 에 접속 중...";
            if (!Net.Join(_ip.text)) _status.text = "접속 실패";
            else Services.Get<GameStateMachine>()?.TryChange(GameState.Lobby);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
