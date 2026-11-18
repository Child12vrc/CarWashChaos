using System;
using System.Collections.Generic;
using System.Linq;
using CarWash.Core;
using CarWash.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CarWash.UI
{
    /// <summary>
    /// P2 Esc menu (the game keeps running — multiplayer): tabs 화면 / 소리 / 조작 / 키 설정, saved instantly to
    /// PlayerPrefs through Settings. Bottom: 끼임 방지 (server-validated unstuck, 30 s cooldown), 나가기, 닫기.
    /// </summary>
    public class SettingsMenu : MonoBehaviour
    {
        public static SettingsMenu Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance._panel != null && Instance._panel.gameObject.activeSelf;

        public static void Toggle() { if (Instance != null) Instance.SetOpen(!IsOpen); }

        Image _panel;
        readonly List<GameObject> _tabs = new List<GameObject>();
        readonly List<Action> _refresh = new List<Action>();
        Text _unstuckLabel, _status;
        Settings S => Services.Get<Settings>();
        InputActionRebindingExtensions.RebindingOperation _rebind;

        public int CurrentTab { get; private set; }
        public string StatusText => _status != null ? _status.text : null;

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; _rebind?.Dispose(); }

        public void Build(Transform root)
        {
            var c = new Vector2(0.5f, 0.5f);
            _panel = UiFactory.FramePanel("SettingsMenu", root, c, Vector2.zero, new Vector2(1000, 800), new Color(0.1f, 0.14f, 0.26f, 0.97f));
            var p = _panel.transform;
            var txt = UiFactory.Skin != null ? UiFactory.Skin.panelText : Color.white;
            UiFactory.Label("Title", p, "설정", 44, new Vector2(0.5f, 1f), new Vector2(0, -52), new Vector2(600, 60), color: txt);
            string[] tabNames = { "화면", "소리", "조작", "키 설정", "도움말" };
            for (int i = 0; i < tabNames.Length; i++)
            {
                int ti = i;
                UiFactory.Button("Tab" + i, p, tabNames[i], new Vector2(0.5f, 1f), new Vector2(-400 + i * 200, -120), new Vector2(184, 56), () => ShowTab(ti));
                var page = UiFactory.Rect("Page" + i, p, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
                _tabs.Add(page);
            }
            BuildScreen(_tabs[0].transform, txt);
            BuildSound(_tabs[1].transform, txt);
            BuildControls(_tabs[2].transform, txt);
            BuildKeys(_tabs[3].transform, txt);
            BuildHelp(_tabs[4].transform, txt); // M4.6 D1

            var bottom = new Vector2(0.5f, 0f);
            UiFactory.Button("Unstuck", p, "끼임 방지", bottom, new Vector2(-300, 60), new Vector2(260, 64), () => { var me = Me; if (me != null) { me.RequestUnstuck(); _status.text = "확인 중…"; } });
            _unstuckLabel = UiFactory.Label("UnstuckCd", p, "", 20, bottom, new Vector2(-230, 108), new Vector2(460, 28), TextAnchor.MiddleLeft, new Color(1f, 0.85f, 0.4f));
            UiFactory.Button("Leave", p, "나가기", bottom, new Vector2(0, 60), new Vector2(240, 64), Leave);
            UiFactory.Button("Close", p, "닫기", bottom, new Vector2(300, 60), new Vector2(240, 64), () => SetOpen(false));
            _status = UiFactory.Label("Status", p, "", 22, bottom, new Vector2(0, 118), new Vector2(900, 30), color: new Color(0.7f, 1f, 0.7f));
            ShowTab(0);
            _panel.gameObject.SetActive(false);
        }

        static PlayerNet Me => PlayerLook.Local != null ? PlayerLook.Local.GetComponent<PlayerNet>() : null;

        public void SetOpen(bool open)
        {
            if (_panel == null) return;
            if (!open) CancelRebind();
            _panel.gameObject.SetActive(open);
            PlayerLook.SetCursorLocked(!open);
            if (open) RefreshAll();
        }

        public void ShowTab(int i)
        {
            CurrentTab = i;
            for (int k = 0; k < _tabs.Count; k++) _tabs[k].SetActive(k == i);
            RefreshAll();
        }

        void RefreshAll() { foreach (var r in _refresh) r(); }

        void Leave()
        {
            SetOpen(false);
            Services.Get<Net.NetSession>()?.Leave();
            SceneManager.LoadScene(SceneNames.MainMenu);
        }

        void Update()
        {
            if (!IsOpen) return;
            var me = Me;
            if (me != null)
            {
                float cd = me.UnstuckCooldownLeft;
                _unstuckLabel.text = cd > 0.05f ? $"다시 사용까지 {Mathf.CeilToInt(cd)}초" : "사용 가능 (들고 있는 물건은 내려놓아요)";
                if (!string.IsNullOrEmpty(me.LastUnstuckResult) && _status.text == "확인 중…") _status.text = me.LastUnstuckResult;
            }
        }

        // ---------------------------------------------------------------- rows
        float _rowY;
        Vector2 Row(int i) => new Vector2(0, 200 - i * 78);

        void Label(Transform page, int row, string text, Color c) =>
            UiFactory.Label("L" + row, page, text, 28, new Vector2(0.5f, 0.5f), Row(row) + new Vector2(-250, 0), new Vector2(360, 50), TextAnchor.MiddleLeft, c);

        /// <summary>Slider row: getter/setter on Settings, value text formatted.</summary>
        void SliderRow(Transform page, int row, string label, Color c, float min, float max, Func<float> get, Action<float> set, Func<float, string> fmt)
        {
            Label(page, row, label, c);
            var bg = UiFactory.Panel("S" + row, page, new Vector2(0.5f, 0.5f), Row(row) + new Vector2(130, 0), new Vector2(380, 24), new Color(0, 0, 0, 0.45f));
            var fillArea = UiFactory.Rect("FillArea", bg.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var fill = UiFactory.Panel("Fill", fillArea, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0, 24), UiFactory.BarGreen);
            fill.rectTransform.anchorMin = new Vector2(0, 0); fill.rectTransform.anchorMax = new Vector2(0, 1); fill.rectTransform.sizeDelta = Vector2.zero;
            var handleArea = UiFactory.Rect("HandleArea", bg.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-20, 0));
            var handle = UiFactory.Panel("Handle", handleArea, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(24, 40), Color.white);
            var slider = bg.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
            slider.minValue = min; slider.maxValue = max;
            var value = UiFactory.Label("V" + row, page, "", 26, new Vector2(0.5f, 0.5f), Row(row) + new Vector2(390, 0), new Vector2(120, 50), TextAnchor.MiddleLeft, c);
            bool syncing = false;
            slider.onValueChanged.AddListener(v => { if (syncing) return; set(v); value.text = fmt(get()); });
            _refresh.Add(() => { syncing = true; slider.SetValueWithoutNotify(get()); value.text = fmt(get()); syncing = false; });
        }

        /// <summary>◀ value ▶ row (toggles and lists).</summary>
        void StepRow(Transform page, int row, string label, Color c, Func<string> show, Action<int> step)
        {
            Label(page, row, label, c);
            var value = UiFactory.Label("V" + row, page, "", 26, new Vector2(0.5f, 0.5f), Row(row) + new Vector2(130, 0), new Vector2(300, 50), TextAnchor.MiddleCenter, c);
            UiFactory.Button("Prev" + row, page, "◀", new Vector2(0.5f, 0.5f), Row(row) + new Vector2(-30, 0), new Vector2(56, 50), () => { step(-1); value.text = show(); });
            UiFactory.Button("Next" + row, page, "▶", new Vector2(0.5f, 0.5f), Row(row) + new Vector2(290, 0), new Vector2(56, 50), () => { step(1); value.text = show(); });
            _refresh.Add(() => value.text = show());
        }

        void BuildScreen(Transform page, Color c)
        {
            SliderRow(page, 0, "밝기", c, -2f, 2f, () => S.Brightness, v => S.Brightness = v, v => (v >= 0 ? "+" : "") + v.ToString("0.0"));
            SliderRow(page, 1, "시야각 (FOV)", c, 60f, 110f, () => S.Fov, v => S.Fov = Mathf.Round(v), v => v.ToString("0") + "°");
            StepRow(page, 2, "그래픽 품질", c, () => QualitySettings.names.Length > 0 ? QualitySettings.names[S.Quality] : "-", d => S.Quality = (S.Quality + d + QualitySettings.names.Length) % Mathf.Max(1, QualitySettings.names.Length));
            StepRow(page, 3, "해상도", c, ResolutionText, d =>
            {
                var res = Screen.resolutions;
                if (res.Length == 0) return;
                int cur = S.ResolutionIndex < 0 ? Array.FindLastIndex(res, r => r.width == Screen.width && r.height == Screen.height) : S.ResolutionIndex;
                S.ResolutionIndex = Mathf.Clamp((cur < 0 ? res.Length - 1 : cur) + d, 0, res.Length - 1);
            });
            StepRow(page, 4, "전체 화면", c, () => S.Fullscreen ? "켜짐" : "꺼짐", _ => S.Fullscreen = !S.Fullscreen);
            StepRow(page, 5, "수직 동기화", c, () => S.VSync ? "켜짐" : "꺼짐", _ => S.VSync = !S.VSync);
            SliderRow(page, 6, "UI 크기", c, 0.6f, 1.3f, () => S.UiScale, v => S.UiScale = Mathf.Round(v * 20f) / 20f, v => Mathf.RoundToInt(v * 100f) + "%"); // M4.6 D2
        }

        string ResolutionText()
        {
            var res = Screen.resolutions;
            int i = S.ResolutionIndex;
            if (i >= 0 && i < res.Length) return $"{res[i].width} × {res[i].height}";
            return $"{Screen.width} × {Screen.height}";
        }

        void BuildSound(Transform page, Color c)
        {
            Func<float, string> pct = v => Mathf.RoundToInt(v * 100f) + "%";
            SliderRow(page, 0, "전체 볼륨", c, 0f, 1f, () => S.MasterVolume, v => S.MasterVolume = v, pct);
            SliderRow(page, 1, "음악", c, 0f, 1f, () => S.MusicVolume, v => S.MusicVolume = v, pct);
            SliderRow(page, 2, "효과음", c, 0f, 1f, () => S.SfxVolume, v => S.SfxVolume = v, pct);
            SliderRow(page, 3, "음성", c, 0f, 1f, () => S.VoiceVolume, v => S.VoiceVolume = v, pct);
        }

        /// <summary>M4.6 D1: 도움말 — goal, controls, tools, critters; restart the tutorial.</summary>
        void BuildHelp(Transform page, Color c)
        {
            const string text =
                "<b>목표</b>  손님 차를 씻어 돈을 벌고, 하루 끝 정산에서 임대료를 내요. 청결도가 높고 빠를수록 더 받아요.\n" +
                "<b>하루 흐름</b>  대기실 [R] 준비 → 사무실 일정판 [E] 영업 시작 → 차가 베이에 들어오면 세차 → 베이 버튼 [E] 정산.\n" +
                "<b>조작</b>  WASD 이동 · 마우스 시점 · Shift 달리기 · Space 점프 · Ctrl 앉기 · E 줍기/상호작용 · F 잡기(꾹: 던지기) · G 내려놓기 · 1-4 슬롯 · 휠 클릭 핑 · F3 연결 정보\n" +
                "<b>도구</b>  고압 세척기(왼쪽 분사, 오른쪽 노즐 넓게/좁게, 호스 8 m) · 거품 대포(기름때는 거품 먼저) · 수건(마우스를 좌우로 문질러 물자국 제거) · 뜰채(기절한 동물 담기)\n" +
                "<b>돌발 상황</b>  차에서 동물이 튀어나와요. 물을 뿌려 기절시키고 [E]나 뜰채로 잡아 동물 보호소 우리에 넣으면 보너스. 차 안 쓰레기는 주워서 버려요.\n" +
                "<b>친구와</b>  호스트가 받은 6자리 참가 코드를 친구가 메인 메뉴에 입력. 쓰러진 친구는 F로 들어 옮길 수 있어요.";
            var t = UiFactory.Label("HelpText", page, text, 25, new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(900, 470), TextAnchor.UpperLeft, c);
            t.supportRichText = true; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.lineSpacing = 1.1f;
            UiFactory.Button("Tutorial", page, "튜토리얼 다시 하기", new Vector2(0.5f, 0.5f), new Vector2(0, -255), new Vector2(320, 56), () => { TutorialPanel.Instance?.Restart(); SetOpen(false); });
        }

        void BuildControls(Transform page, Color c)
        {
            SliderRow(page, 0, "마우스 감도", c, 0.1f, 5f, () => S.Sensitivity, v => S.Sensitivity = v, v => v.ToString("0.00"));
            StepRow(page, 1, "Y축 반전", c, () => S.InvertY ? "켜짐" : "꺼짐", _ => S.InvertY = !S.InvertY);
            SliderRow(page, 2, "화면 흔들림", c, 0f, 1f, () => S.ShakeStrength, v => S.ShakeStrength = v, v => v <= 0.001f ? "끔" : Mathf.RoundToInt(v * 100f) + "%");
            StepRow(page, 3, "머리 흔들림", c, () => S.HeadBob ? "켜짐" : "꺼짐", _ => S.HeadBob = !S.HeadBob);
            UiFactory.Button("ResetAll", page, "모든 설정 초기화", new Vector2(0.5f, 0.5f), Row(5), new Vector2(360, 56), () => { S.ResetAll(); PlayerInputReader.RaiseBindingsChanged(); RefreshAll(); _status.text = "기본값으로 되돌렸어요"; });
        }

        // ---------------------------------------------------------------- keys
        /// <summary>(label, action, composite part or null)</summary>
        static readonly (string label, string action, string part)[] Keys =
        {
            ("앞으로", "Move", "up"), ("뒤로", "Move", "down"), ("왼쪽", "Move", "left"), ("오른쪽", "Move", "right"),
            ("점프", "Jump", null), ("달리기", "Sprint", null), ("웅크리기", "Crouch", null), ("상호작용", "Interact", null),
            ("잡기 / 던지기", "Grab", null), ("내려놓기", "Drop", null), ("핑", "Ping", null),
        };

        InputActionAsset LiveAsset => PlayerLook.Local != null ? PlayerLook.Local.GetComponent<PlayerInputReader>().Asset : null;

        public static int BindingIndex(InputAction action, string part)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var b = action.bindings[i];
                if (part != null) { if (b.isPartOfComposite && b.name == part) return i; continue; }
                if (!b.isComposite && !b.isPartOfComposite && (b.path.StartsWith("<Keyboard>") || b.path.StartsWith("<Mouse>"))) return i;
            }
            return -1;
        }

        public string BindingText(string action, string part)
        {
            var a = LiveAsset?.FindAction(action);
            if (a == null) return "-";
            int i = BindingIndex(a, part);
            return i < 0 ? "-" : a.GetBindingDisplayString(i);
        }

        void BuildKeys(Transform page, Color c)
        {
            for (int k = 0; k < Keys.Length; k++)
            {
                var (label, action, part) = Keys[k];
                int col = k / 6, row = k % 6;
                var pos = new Vector2(-230 + col * 460, 200 - row * 68);
                UiFactory.Label("K" + k, page, label, 24, new Vector2(0.5f, 0.5f), pos + new Vector2(-60, 0), new Vector2(200, 46), TextAnchor.MiddleLeft, c);
                Text btnText = null;
                var btn = UiFactory.Button("B" + k, page, "", new Vector2(0.5f, 0.5f), pos + new Vector2(120, 0), new Vector2(150, 50), () => StartRebind(action, part));
                btnText = btn.GetComponentInChildren<Text>();
                _refresh.Add(() => btnText.text = BindingText(action, part));
            }
            UiFactory.Button("KeysReset", page, "키 기본값으로", new Vector2(0.5f, 0.5f), new Vector2(0, -230), new Vector2(300, 56), () =>
            {
                LiveAsset?.RemoveAllBindingOverrides();
                S.Bindings = "";
                PlayerInputReader.RaiseBindingsChanged();
                RefreshAll();
                _status.text = "키 설정을 기본값으로 되돌렸어요";
            });
        }

        void StartRebind(string actionName, string part)
        {
            var asset = LiveAsset;
            var action = asset?.FindAction(actionName);
            if (action == null) return;
            int index = BindingIndex(action, part);
            if (index < 0) return;
            CancelRebind();
            _status.text = "바꿀 키를 누르세요 (Esc = 취소)";
            var map = action.actionMap;
            map.Disable();
            _rebind = action.PerformInteractiveRebinding(index)
                .WithControlsExcluding("<Mouse>/delta").WithControlsExcluding("<Mouse>/position")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.05f)
                .OnComplete(op => FinishRebind(map, true))
                .OnCancel(op => FinishRebind(map, false))
                .Start();
        }

        /// <summary>Apply a binding without the interactive wait (automated checks).</summary>
        public bool DevRebind(string actionName, string part, string path)
        {
            var action = LiveAsset?.FindAction(actionName);
            if (action == null) return false;
            int i = BindingIndex(action, part);
            if (i < 0) return false;
            action.ApplyBindingOverride(i, path);
            SaveBindings();
            return true;
        }

        void FinishRebind(InputActionMap map, bool done)
        {
            _rebind?.Dispose(); _rebind = null;
            map.Enable();
            if (done) SaveBindings();
            _status.text = done ? "키를 바꿨어요" : "취소했어요";
            RefreshAll();
        }

        void SaveBindings()
        {
            var asset = LiveAsset;
            if (asset == null) return;
            S.Bindings = asset.SaveBindingOverridesAsJson();
        }

        void CancelRebind() { if (_rebind != null) { _rebind.Cancel(); _rebind?.Dispose(); _rebind = null; } }
    }
}
