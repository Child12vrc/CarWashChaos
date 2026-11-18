using System;
using System.Linq;
using CarWash.Core;
using CarWash.Critters;
using CarWash.Day;
using CarWash.Economy;
using CarWash.Player;
using CarWash.Tools;
using CarWash.Vehicles;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CarWash.UI
{
    /// <summary>
    /// M4.6 D1: first-run step tutorial (move → pick up the washer → start the day → wash → payout), each step completes by
    /// doing it; [Backspace] skips; finished/skipped is remembered (Settings.TutorialDone; Esc → 도움말 → "튜토리얼 다시 하기").
    /// After it: contextual hints, one at a time, each at most 3 times ever (grease needs foam, spots need the towel, car
    /// ready to pay, stunned critter, hose at full length). A compact card on the left edge, never over the crosshair.
    /// </summary>
    public class TutorialPanel : MonoBehaviour
    {
        struct Step { public string title, text; public Func<bool> done; }

        Image _card;
        Text _title, _body, _foot;
        Step[] _steps;
        int _step = -1;
        Vector3 _start;
        float _sprayTime, _hintUntil, _nextHintCheck;
        int _money0 = -1;
        string _hintId;
        public static TutorialPanel Instance { get; private set; }

        /// <summary>Dev / tests: current tutorial step (-1 = off) and what the card says.</summary>
        public int StepIndex => _step;
        public string Text => _card != null && _card.gameObject.activeSelf ? _title.text + " | " + _body.text : null;

        Settings S => Services.Get<Settings>();
        static PlayerNet Me => PlayerLook.Local != null ? PlayerLook.Local.GetComponent<PlayerNet>() : null;

        public void Build(Transform root)
        {
            Instance = this;
            var c = UiFactory.Skin != null ? UiFactory.Skin.panelText : Color.white;
            _card = UiFactory.FramePanel("Tutorial", root, new Vector2(0f, 0.5f), new Vector2(210, -200), new Vector2(400, 190), new Color(0.08f, 0.12f, 0.24f, 0.88f));
            _title = UiFactory.Label("Title", _card.transform, "", 24, new Vector2(0.5f, 1f), new Vector2(0, -26), new Vector2(350, 34), TextAnchor.MiddleLeft, new Color(1f, 0.85f, 0.35f));
            _body = UiFactory.Label("Body", _card.transform, "", 19, new Vector2(0.5f, 0.5f), new Vector2(0, -4), new Vector2(370, 104), TextAnchor.UpperLeft, c);
            _foot = UiFactory.Label("Foot", _card.transform, "", 15, new Vector2(0.5f, 0f), new Vector2(0, 16), new Vector2(350, 22), TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.55f));
            _body.horizontalOverflow = HorizontalWrapMode.Wrap;
            _card.gameObject.SetActive(false);
            _steps = new[]
            {
                new Step { title = "1/5  움직여 보기", text = "W A S D 로 이동, 마우스로 둘러보기.\nShift 달리기 · Space 점프 · Ctrl 앉기", done = () => Me != null && Flat(Me.transform.position - _start) > 4f },
                new Step { title = "2/5  고압 세척기 집기", text = "베이 1 왼쪽 벽 가운데의 노란 세척기를 보고 [E].\n호스가 닿는 곳까지만 들고 다닐 수 있어요.", done = () => Me != null && Me.GetComponent<PlayerTools>().Equipped is PressureWasher },
                new Step { title = "3/5  영업 시작", text = "대기실에서 모두 [R] 준비 →\n사무실 벽 일정판을 보고 [E] 영업 시작.", done = () => DayManager.Instance != null && DayManager.Instance.State.Value == GameState.Day },
                new Step { title = "4/5  세차하기", text = "차가 베이에 들어오면 마우스 왼쪽으로 물을 뿌려요.\n오른쪽 클릭: 노즐 넓게/좁게. 베이 카드에 청결도 %.", done = () => _sprayTime > 3f },
                new Step { title = "5/5  정산하기", text = "다 씻었으면 베이 안쪽 벽 버튼을 보고 [E]\n\"세차 완료! 정산하기\" → 돈을 받아요.", done = () => EconomyState.Instance != null && _money0 >= 0 && EconomyState.Instance.Money.Value > _money0 },
            };
        }

        static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

        void Update()
        {
            var me = Me;
            if (me == null || _steps == null) return;
            if (_step < 0 && S != null && !S.TutorialDone && _hintId == null && !(DevFlags.Autotest && DevAutopilot.Requested)) Begin(me); // automated runs: no tutorial
            if (_step >= 0) TutorialTick(me);
            else Hints(me);
        }

        void Begin(PlayerNet me) { _step = 0; _start = me.transform.position; ShowStep(); }

        /// <summary>Dev checks: start the tutorial now (automated runs normally skip it).</summary>
        public void DevStart() { if (S != null) S.TutorialDone = false; _hintId = null; if (Me != null) Begin(Me); }

        /// <summary>Esc → 도움말 → 튜토리얼 다시 하기.</summary>
        public void Restart()
        {
            if (S != null) S.TutorialDone = false;
            _hintId = null; _step = -1;
        }

        void ShowStep()
        {
            _card.gameObject.SetActive(true);
            _title.text = _steps[_step].title;
            _body.text = _steps[_step].text;
            _foot.text = "[Backspace] 튜토리얼 건너뛰기   ·   Esc → 도움말";
            if (_step == 4 && EconomyState.Instance != null) _money0 = EconomyState.Instance.Money.Value;
        }

        void TutorialTick(PlayerNet me)
        {
            if (Keyboard.current != null && Keyboard.current.backspaceKey.wasPressedThisFrame && !SettingsMenu.IsOpen) { Finish(skipped: true); return; }
            var tool = me.GetComponent<PlayerTools>().Equipped;
            if (tool is SprayTool && tool.Using.Value) _sprayTime += Time.deltaTime;
            if (_step == 4 && _money0 < 0 && EconomyState.Instance != null) _money0 = EconomyState.Instance.Money.Value;
            if (!_steps[_step].done()) return;
            _step++;
            if (_step >= _steps.Length) { Finish(skipped: false); return; }
            ShowStep();
        }

        void Finish(bool skipped)
        {
            _step = -1;
            if (S != null) S.TutorialDone = true;
            ShowHint("tutorialDone", skipped ? "튜토리얼을 건너뛰었어요" : "튜토리얼 완료!", "언제든 Esc → 도움말에서 조작·목표·도구를 볼 수 있어요.", 5f, force: true);
        }

        // ---------------------------------------------------------------- contextual hints
        void ShowHint(string id, string title, string text, float seconds, bool force = false)
        {
            if (!force && S != null) { if (S.HintCount(id) >= 3) return; S.CountHint(id); }
            _hintId = id; _hintUntil = Time.time + seconds;
            _card.gameObject.SetActive(true);
            _title.text = title; _body.text = text; _foot.text = "";
        }

        void Hints(PlayerNet me)
        {
            if (_hintId != null)
            {
                if (Time.time < _hintUntil) return;
                _hintId = null; _card.gameObject.SetActive(false);
            }
            if (Time.time < _nextHintCheck) return;
            _nextHintCheck = Time.time + 1f;
            var tools = me.GetComponent<PlayerTools>();
            var tool = tools.Equipped;
            var car = NearestCar(me, 7f);
            if (car != null && car.Dirt != null && car.Dirt.Painter != null && car.Dirt.Painter.HasReadback)
            {
                var p = car.Dirt.Painter;
                if (tool is PressureWasher && p.GreasePct > 0.03f && p.MudPct < 0.05f) { ShowHint("grease", "기름때가 안 지워져요?", "검은 기름때는 거품 대포(분홍)로 먼저 덮은 뒤\n세척기로 쏘면 지워져요.", 7f); return; }
                if (p.SpotsPct > 0.03f && p.MudPct < 0.02f && !(tool is Towel)) { ShowHint("spots", "물자국이 남았어요", "물자국은 수건으로: 수건을 들고 차를 보며\n마우스를 좌우로 문질러요.", 7f); return; }
                if (car.State.Value == VehicleState.Washing && car.Dirt.CleanPct.Value >= 0.9f) { ShowHint("pay", "거의 다 됐어요!", "베이 안쪽 벽 버튼 [E] → 세차 완료! 정산하기.\n청결도가 높을수록 돈을 더 받아요.", 7f); return; }
            }
            foreach (var c in CritterBase.All)
                if (c != null && c.State.Value == CritterState.Stunned && (c.transform.position - me.transform.position).sqrMagnitude < 64f)
                { ShowHint("stunned", "동물이 기절했어요", "[E]로 붙잡거나 뜰채(마우스 왼쪽)로 담아서\n사무실 옆 '동물 보호소' 우리에 넣어요.", 7f); return; }
            var hose = tool != null ? tool.GetComponent<HoseTether>() : null;
            if (hose != null && Flat(me.transform.position - hose.Anchor.Value) > hose.Length - 0.3f)
            { ShowHint("hose", "호스 끝이에요", "세척기 호스는 8 m. 다른 도구로 바꾸려면 숫자키,\n빈 손은 빈 슬롯 숫자키.", 6f); return; }
        }

        static Vehicle NearestCar(PlayerNet me, float range)
        {
            Vehicle best = null; float bd = range * range;
            foreach (var v in FindObjectsByType<Vehicle>(FindObjectsSortMode.None))
            {
                if (v == null || !v.IsSpawned) continue;
                float d = (v.transform.position - me.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = v; }
            }
            return best;
        }
    }
}
