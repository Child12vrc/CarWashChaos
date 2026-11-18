using CarWash.Core;
using CarWash.Net;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CarWash.UI
{
    /// <summary>
    /// Main menu (built in code). P7 default flow = internet play through Unity Relay:
    ///   호스트 (코드 발급) → 6-character join code (shown big in the lot by InviteCard, with a copy button)
    ///   코드로 참가 (code field)
    ///   고급 → IP로 호스트 (shows this PC's private IP:port) / IP로 참가 (LAN, port forwarding)
    /// Command line: -host-relay | -joincode CODE | -host | -join IP (auto-start once).
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        public static MainMenuUI Instance { get; private set; }
        public Transform Root { get; private set; }

        InputField _ip, _code, _nick;
        Text _status, _myIp, _nickHint;
        GameObject _main, _advanced;
        Button[] _buttons;
        bool _busy;

        void Awake()
        {
            Instance = this;
            UiFactory.EnsureEventSystem();
            var canvas = UiFactory.Canvas("MainMenuCanvas", 0, transform);
            Root = canvas.transform;
            var c = new Vector2(0.5f, 0.5f);
            UiFactory.Panel("Bg", Root, c, Vector2.zero, new Vector2(4000, 4000), new Color(0.09f, 0.13f, 0.2f));
            UiFactory.Label("Title", Root, "세차장 대소동", 96, new Vector2(0.5f, 0.8f), Vector2.zero, new Vector2(1400, 160));
            UiFactory.Label("Sub", Root, "CarWashChaos (가제)  v" + Application.version, 32, new Vector2(0.5f, 0.71f), Vector2.zero, new Vector2(1000, 60), color: new Color(0.7f, 0.85f, 1f));

            // --- default: Relay code flow ---
            _main = UiFactory.Rect("Main", Root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            var m = _main.transform;
            UiFactory.FramePanel("MenuPanel", m, c, new Vector2(0, -110), new Vector2(720, 600), new Color(0.12f, 0.17f, 0.3f, 0.9f));
            // M4.9 G4: nickname (required before hosting / joining; remembered)
            UiFactory.Label("NickLabel", m, "닉네임", 26, c, new Vector2(-250, 140), new Vector2(120, 40), color: new Color(0.75f, 0.85f, 1f));
            _nick = UiFactory.Input("Nick", m, PlayerProfile.Nickname, c, new Vector2(60, 140), new Vector2(440, 64));
            _nick.characterLimit = PlayerProfile.MaxLen;
            _nick.textComponent.fontSize = 30;
            _nick.onValueChanged.AddListener(_ => NickHint());
            _nickHint = UiFactory.Label("NickHint", m, "", 20, c, new Vector2(60, 98), new Vector2(560, 26), color: new Color(1f, 0.85f, 0.5f));
            var bHost = UiFactory.Button("Host", m, "방 만들기 (참가 코드 발급)", c, new Vector2(0, 40), new Vector2(560, 84), HostRelay);
            UiFactory.Label("CodeHint", m, "친구에게 받은 참가 코드", 22, c, new Vector2(-120, -32), new Vector2(320, 30), color: new Color(0.75f, 0.85f, 1f));
            _code = UiFactory.Input("Code", m, "", c, new Vector2(-120, -80), new Vector2(320, 74));
            _code.characterLimit = 12;
            _code.textComponent.fontSize = 36;
            _code.textComponent.alignment = TextAnchor.MiddleCenter;
            var bJoin = UiFactory.Button("JoinCode", m, "코드로 참가", c, new Vector2(170, -80), new Vector2(240, 74), JoinRelay);
            var bAdv = UiFactory.Button("Advanced", m, "고급 (IP로 플레이)", c, new Vector2(0, -185), new Vector2(560, 64), () => ShowAdvanced(true));
            var bQuit = UiFactory.Button("Quit", m, "종료", c, new Vector2(0, -270), new Vector2(560, 64), Application.Quit);
            NickHint();

            // --- 고급: direct IP (LAN / port forwarding) ---
            _advanced = UiFactory.Rect("Advanced", Root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            var a = _advanced.transform;
            UiFactory.FramePanel("AdvPanel", a, c, new Vector2(0, -90), new Vector2(820, 560), new Color(0.12f, 0.17f, 0.3f, 0.92f));
            UiFactory.Label("AdvTitle", a, "고급 — IP로 플레이 (같은 공유기 / 포트 포워딩)", 26, c, new Vector2(0, 150), new Vector2(780, 40), color: new Color(0.75f, 0.85f, 1f));
            var bIpHost = UiFactory.Button("IpHost", a, "IP로 방 만들기", c, new Vector2(0, 85), new Vector2(560, 74), HostIp);
            _myIp = UiFactory.Label("MyIp", a, "", 24, c, new Vector2(0, 25), new Vector2(780, 36), color: new Color(1f, 0.9f, 0.55f));
            _ip = UiFactory.Input("Ip", a, "127.0.0.1", c, new Vector2(-120, -50), new Vector2(340, 70));
            var bIpJoin = UiFactory.Button("IpJoin", a, "IP로 참가", c, new Vector2(180, -50), new Vector2(220, 70), JoinIp);
            UiFactory.Label("PortHint", a, $"포트 {NetSession.DefaultPort} (UDP). 인터넷 너머라면 기본 메뉴의 '참가 코드'를 쓰세요.", 20, c, new Vector2(0, -115), new Vector2(780, 30), color: new Color(1f, 1f, 1f, 0.7f));
            var bBack = UiFactory.Button("Back", a, "뒤로", c, new Vector2(0, -200), new Vector2(320, 64), () => ShowAdvanced(false));

            _buttons = new[] { bHost, bJoin, bAdv, bQuit, bIpHost, bIpJoin, bBack };
            _status = UiFactory.Label("Status", Root, "", 28, new Vector2(0.5f, 0.1f), Vector2.zero, new Vector2(1600, 90), color: new Color(1f, 0.85f, 0.5f));
            ShowAdvanced(false);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void ShowAdvanced(bool on)
        {
            _main.SetActive(!on);
            _advanced.SetActive(on);
            if (on) _myIp.text = $"내 사설 IP: {NetSession.LocalIPv4()} : {NetSession.DefaultPort}";
        }

        static bool _autoStarted;
        /// <summary>-autoreconnect (dev/P1): after a lost connection, join the same address again automatically.</summary>
        static bool AutoReconnect => DevFlags.DevContent && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-autoreconnect") >= 0;

        void Start()
        {
            Services.Get<GameStateMachine>()?.Force(GameState.Menu);
            if (!string.IsNullOrEmpty(NetSession.LastDisconnectReason))
                _status.text = NetSession.LastDisconnectReason + "  (다시 참가할 수 있습니다)";
            bool again = _autoStarted && AutoReconnect && NetSession.ArgJoin != null;
            if (_autoStarted && !again) return; // command-line auto start only once
            _autoStarted = true;
            if (NetSession.ArgHostRelay || NetSession.ArgJoinCode != null || NetSession.ArgHost || NetSession.ArgJoin != null) PlayerProfile.EnsureTemporary();
            if (DevFlags.DevContent && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-autotest") >= 0) LogNickCheck();
            if (NetSession.ArgHostRelay) HostRelay();
            else if (NetSession.ArgJoinCode != null) { _code.text = NetSession.ArgJoinCode; JoinRelay(); }
            else if (NetSession.ArgHost) HostIp();
            else if (NetSession.ArgJoin != null) { _ip.text = NetSession.ArgJoin; if (again) Invoke(nameof(JoinIp), 3f); else JoinIp(); }
        }

        /// <summary>Dev (-autotest): IME availability under the Input System + Korean glyphs of the UI font for a sample name.</summary>
        void LogNickCheck()
        {
            string ime;
            try { var prev = Input.imeCompositionMode; Input.imeCompositionMode = IMECompositionMode.On; ime = $"ok (composition '{Input.compositionString}', mode {Input.imeCompositionMode})"; Input.imeCompositionMode = prev; }
            catch (System.Exception e) { ime = "FAIL " + e.GetType().Name; }
            const string sample = "세차왕김철수abc123_-.";
            var font = UiFactory.Font;
            font.RequestCharactersInTexture(sample, 30);
            int missing = 0; foreach (char ch in sample) if (!font.GetCharacterInfo(ch, out _, 30)) missing++;
            Debug.Log($"[NICK] nickname '{PlayerProfile.Nickname}' (saved='{PlayerPrefs.GetString("cw.nickname", "")}'), validate('{sample}')={PlayerProfile.Validate(sample) ?? "ok"}, validate('김')={PlayerProfile.Validate("김")}, validate('<b>x</b>')={PlayerProfile.Validate("<b>x</b>")}, IME {ime}, glyphs missing {missing}/{sample.Length} in {font.name}");
        }

        NetSession Net => Services.Get<NetSession>();

        void SetBusy(bool busy)
        {
            _busy = busy;
            foreach (var b in _buttons) if (b != null) b.interactable = !busy;
        }

        /// <summary>M4.9 G4: live hint under the nickname field (first run: asks for one).</summary>
        void NickHint()
        {
            if (_nickHint == null || _nick == null) return;
            var t = _nick.text;
            if (string.IsNullOrEmpty(t)) { _nickHint.text = "처음이라면 닉네임을 정해 주세요 (2~12자, 한글·영문·숫자)"; _nickHint.color = new Color(1f, 0.85f, 0.5f); return; }
            var err = PlayerProfile.Validate(t);
            _nickHint.text = err ?? $"{PlayerProfile.Length(PlayerProfile.Clean(t))}/{PlayerProfile.MaxLen}자 — 이름표·결산에 이 이름이 보여요";
            _nickHint.color = err != null ? new Color(1f, 0.55f, 0.45f) : new Color(0.6f, 1f, 0.7f);
        }

        /// <summary>Saves the typed nickname; false (+ message, field focused) when it is missing or invalid.</summary>
        bool NickOk()
        {
            var t = _nick != null ? _nick.text : "";
            if (string.IsNullOrEmpty(t) && PlayerProfile.HasNickname) return true; // command-line temporary name
            if (PlayerProfile.Clean(t) == PlayerProfile.Nickname) return true;      // unchanged (also keeps a -nick test name unsaved)
            var err = PlayerProfile.Validate(t);
            if (err != null)
            {
                _status.text = "먼저 닉네임을 정해 주세요 — " + err;
                ShowAdvanced(false);
                _nick.Select(); _nick.ActivateInputField();
                return false;
            }
            PlayerProfile.Nickname = t;
            return true;
        }

        bool CanStart()
        {
            if (_busy) return false;
            if (Net == null) { _status.text = "Bootstrap 씬에서 시작해 주세요"; return false; }
            if (!NickOk()) return false;
            return true;
        }

        async void HostRelay()
        {
            if (!CanStart()) return;
            SetBusy(true);
            _status.text = "참가 코드 발급 중…";
            string err = await Net.HostRelay();
            if (this == null) return;
            SetBusy(false);
            if (err != null) { _status.text = err; return; }
            EnterLot();
        }

        async void JoinRelay()
        {
            if (!CanStart()) return;
            string code = RelayConnector.NormalizeCode(_code.text);
            if (!RelayConnector.IsValidCode(code)) { _status.text = $"참가 코드는 영문·숫자 {RelayConnector.CodeLength}자리입니다."; return; }
            SetBusy(true);
            _status.text = $"코드 {code} 로 접속 중…";
            string err = await Net.JoinRelay(code);
            if (this == null) return;
            SetBusy(false);
            if (err != null) { _status.text = err; return; }
            Services.Get<GameStateMachine>()?.TryChange(GameState.Lobby);
        }

        void HostIp()
        {
            if (!CanStart()) return;
            if (!Net.Host()) { _status.text = "방을 만들지 못했어요 (다른 프로그램이 같은 포트를 쓰는 중일 수 있어요)"; return; }
            EnterLot();
        }

        void JoinIp()
        {
            if (!CanStart()) return;
            _status.text = $"{_ip.text} 에 접속 중...";
            if (!Net.Join(_ip.text.Trim())) _status.text = "접속 실패";
            else Services.Get<GameStateMachine>()?.TryChange(GameState.Lobby);
        }

        void EnterLot()
        {
            Services.Get<GameStateMachine>()?.TryChange(GameState.Lobby);
            NetworkManager.Singleton.SceneManager.LoadScene(SceneNames.SudsyLot, LoadSceneMode.Single);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
