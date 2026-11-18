using System.Collections.Generic;
using CarWash.Core;
using CarWash.Day;
using CarWash.Economy;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CarWash.UI
{
    /// <summary>
    /// Day HUD (T2.4): money (ico_money), reputation stars (ico_rep_star / _empty), week/day + clock (ico_timer),
    /// result toasts (ui_toast) and the end-of-day summary / bankrupt / week-end panel (ui_panel, Enter = continue).
    /// Reads DayManager / EconomyState NetworkVariables, so it is identical on every peer.
    /// </summary>
    public class DayHud : MonoBehaviour
    {
        Transform _root;
        UiSkin _skin;
        Text _money, _clock, _info;
        Image _clockIcon;
        readonly Image[] _stars = new Image[5];
        Image _summary;
        Text _summaryTitle, _summaryBody, _summaryHint;
        readonly List<(Image img, float until)> _toasts = new List<(Image, float)>();
        DayManager _bound;

        public string SummaryText => _summary != null && _summary.gameObject.activeSelf ? _summaryTitle.text + " | " + _summaryBody.text.Replace("\n", " / ") : null;
        public string ClockText => _clock != null ? _clock.text : null;
        public string MoneyText => _money != null ? _money.text : null;
        public int ToastCount => _toasts.Count;
        public string LastToast { get; private set; }
        public readonly List<string> ToastLog = new List<string>();

        public void Build(Transform root)
        {
            _root = root;
            _skin = UiFactory.Skin;
            var tl = new Vector2(0f, 1f);
            // money + reputation (top-left)
            var bar = UiFactory.FramePanel("MoneyRep", root, tl, new Vector2(190, -62), new Vector2(340, 96), new Color(0, 0, 0, 0.45f));
            Icon("MoneyIcon", bar.transform, "ico_money", new Vector2(0f, 0.5f), new Vector2(38, 14), 44);
            _money = UiFactory.Label("Money", bar.transform, "$0", 34, new Vector2(0f, 0.5f), new Vector2(172, 14), new Vector2(200, 44), TextAnchor.MiddleLeft);
            _money.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.7f);
            for (int i = 0; i < 5; i++)
            {
                _stars[i] = Icon("Star" + i, bar.transform, "ico_rep_star", new Vector2(0f, 0.5f), new Vector2(36 + i * 34, -24), 32);
            }
            // week/day + clock (top-centre)
            var top = new Vector2(0.5f, 1f);
            var clockPanel = UiFactory.FramePanel("Clock", root, top, new Vector2(0, -50), new Vector2(460, 72), new Color(0, 0, 0, 0.45f));
            _clockIcon = Icon("TimerIcon", clockPanel.transform, "ico_timer", new Vector2(0f, 0.5f), new Vector2(42, 0), 44);
            _clock = UiFactory.Label("ClockText", clockPanel.transform, "", 30, new Vector2(0.5f, 0.5f), new Vector2(24, 0), new Vector2(380, 60));
            _clock.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.7f);
            _info = UiFactory.Label("DayInfo", root, "", 24, top, new Vector2(0, -108), new Vector2(900, 34), color: new Color(1f, 0.95f, 0.75f));
            _info.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);

            // summary panel (centre)
            _summary = UiFactory.FramePanel("Summary", root, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(720, 560), new Color(0.1f, 0.15f, 0.3f, 0.95f));
            var txt = _skin != null ? _skin.panelText : Color.white;
            _summaryTitle = UiFactory.Label("Title", _summary.transform, "", 44, new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(640, 60), color: txt);
            _summaryBody = UiFactory.Label("Body", _summary.transform, "", 28, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(600, 340), TextAnchor.MiddleLeft, txt);
            _summaryBody.lineSpacing = 1.15f;
            _summaryHint = UiFactory.Label("Hint", _summary.transform, "", 28, new Vector2(0.5f, 0f), new Vector2(0, 52), new Vector2(640, 40), color: new Color(1f, 0.85f, 0.3f));
            _summary.gameObject.SetActive(false);
        }

        Image Icon(string name, Transform parent, string icon, Vector2 anchor, Vector2 pos, float size)
        {
            var sp = _skin != null ? _skin.Icon(icon) : null;
            var img = UiFactory.Panel(name, parent, anchor, pos, new Vector2(size, size), sp != null ? Color.white : new Color(1f, 0.85f, 0.3f));
            img.sprite = sp; img.preserveAspect = true; img.raycastTarget = false;
            return img;
        }

        void Toast(string text, Color color)
        {
            LastToast = text;
            ToastLog.Add(text);
            var img = _skin != null && _skin.toast != null
                ? UiFactory.Panel("Toast", _root, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(620, 64), Color.white)
                : UiFactory.Panel("Toast", _root, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(620, 64), new Color(0, 0, 0, 0.6f));
            if (_skin != null && _skin.toast != null) { img.sprite = _skin.toast; img.type = Image.Type.Sliced; }
            var t = UiFactory.Label("Text", img.transform, text, 26, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(580, 56), color: color);
            t.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.6f);
            _toasts.Add((img, Time.time + 3f));
            while (_toasts.Count > 3) { Destroy(_toasts[0].img.gameObject); _toasts.RemoveAt(0); }
        }

        void OnPaid(PayNotice n)
        {
            var reg = Services.Get<Data.DataRegistry>();
            var d = reg != null ? reg.Get<Data.VehicleData>(n.vehicleId) : null;
            string name = d != null ? d.displayName : n.vehicleId;
            if (n.angry) Toast($"{name}: 기다리다 화나서 떠났어요! (평판 하락)", new Color(1f, 0.55f, 0.5f));
            else if (n.refused) Toast($"{name}: 결제 거절! 청결 {Mathf.FloorToInt(n.clean * 100f)}% (평판 하락)", new Color(1f, 0.55f, 0.5f));
            else if (n.dryTip > 0) Toast($"{name}: +${n.amount}  (청결 {Mathf.FloorToInt(n.clean * 100f)}%) 물기까지 완벽! 팁 +${n.dryTip}", new Color(0.55f, 0.95f, 1f));
            else Toast($"{name}: +${n.amount}  (청결 {Mathf.FloorToInt(n.clean * 100f)}%)", new Color(0.6f, 1f, 0.6f));
        }

        long _hudKey = long.MinValue;
        static string Mmss(float s) { int i = Mathf.CeilToInt(s); return $"{i / 60}:{i % 60:00}"; }

        void Update()
        {
            var dm = DayManager.Instance;
            if (dm != _bound)
            {
                if (_bound != null) { _bound.Paid -= OnPaid; _bound.BossArrived -= OnBoss; }
                _bound = dm;
                if (dm != null) { dm.Paid += OnPaid; dm.BossArrived += OnBoss; }
            }
            // toasts stack downward under the clock
            for (int i = _toasts.Count - 1; i >= 0; i--)
                if (Time.time > _toasts[i].until) { Destroy(_toasts[i].img.gameObject); _toasts.RemoveAt(i); }
            for (int i = 0; i < _toasts.Count; i++) _toasts[i].img.rectTransform.anchoredPosition = new Vector2(0, -170 - i * 70);

            var eco = EconomyState.Instance;
            bool show = dm != null && eco != null;
            _money.transform.parent.gameObject.SetActive(show);
            _clock.transform.parent.gameObject.SetActive(show);
            _info.gameObject.SetActive(show);
            if (!show) { _summary.gameObject.SetActive(false); return; }

            // M4.7: strings are rebuilt only when what they show changes (the HUD was the game's biggest per-frame GC source)
            var st = dm.State.Value;
            float left = st == GameState.Day ? (dm.Phase.Value == DayPhase.Open ? dm.TimeLeft : dm.OvertimeLeft) : 0f;
            long key = ((long)eco.Money.Value << 32) ^ ((long)dm.CurrentRent << 20) ^ ((long)(int)st << 16) ^ ((long)dm.Week.Value << 12)
                       ^ ((long)dm.DayNum.Value << 8) ^ ((long)(int)dm.Phase.Value << 6) ^ ((long)Mathf.CeilToInt(left) << 40) ^ ((long)Mathf.RoundToInt(eco.Rep.Value * 2f) << 56);
            if (key == _hudKey && !(st == GameState.Summary || st == GameState.GameOver || st == GameState.WeekEnd)) return;
            _hudKey = key;

            _money.text = "$" + eco.Money.Value;
            _money.color = eco.Money.Value < dm.CurrentRent ? new Color(1f, 0.6f, 0.5f) : Color.white;
            float rep = eco.Rep.Value;
            for (int i = 0; i < 5; i++)
            {
                bool full = rep >= i + 0.5f;
                var sp = _skin != null ? _skin.Icon(full ? "ico_rep_star" : "ico_rep_star_empty") : null;
                if (sp != null) _stars[i].sprite = sp; else _stars[i].color = full ? new Color(1f, 0.85f, 0.3f) : new Color(1, 1, 1, 0.25f);
            }

            string head = $"{dm.Week.Value}주차 {dm.DayNum.Value}일차";
            switch (st)
            {
                case GameState.Day:
                    if (dm.Phase.Value == DayPhase.Open) { _clock.text = $"{head}  {Mmss(dm.TimeLeft)}"; _clock.color = dm.TimeLeft < 30f ? new Color(1f, 0.8f, 0.4f) : Color.white; }
                    else { _clock.text = $"{head}  마감 {Mmss(dm.OvertimeLeft)}"; _clock.color = new Color(1f, 0.6f, 0.5f); }
                    _info.text = dm.Phase.Value == DayPhase.Open ? $"오늘 임대료 ${dm.CurrentRent}  ·  세차가 끝나면 차고지 버튼 [E] 정산" : "영업 종료 — 남은 차만 마무리하세요";
                    break;
                case GameState.Lobby:
                    _clock.text = head + "  준비 중"; _clock.color = Color.white;
                    _info.text = "사무실 일정판에서 [E] 영업 시작";
                    break;
                default:
                    _clock.text = head + "  영업 종료"; _clock.color = Color.white;
                    _info.text = "";
                    break;
            }

            bool panel = st == GameState.Summary || st == GameState.GameOver || st == GameState.WeekEnd;
            _summary.gameObject.SetActive(panel);
            if (panel) FillSummary(dm, st);
            if (panel && Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
                dm.RequestAdvance();
        }

        void FillSummary(DayManager dm, GameState st)
        {
            var s = dm.Summary.Value;
            if (st == GameState.GameOver)
            {
                _summaryTitle.text = "파산!";
                _summaryBody.text = $"임대료 ${s.rent}를 낼 돈이 없습니다 (보유 ${s.moneyEnd}).\n\n{s.week}주차 1일차부터 다시 시작합니다.\n돈과 평판은 처음으로 돌아가고,\n열어 둔 차고지는 그대로 유지됩니다.";
                _summaryHint.text = "[Enter] 1일차부터 다시 도전";
                return;
            }
            if (st == GameState.WeekEnd)
            {
                _summaryTitle.text = $"{s.week}주차 완료!";
                _summaryBody.text = $"한 주를 버텨냈습니다.\n\n보유 금액  ${s.moneyEnd}\n평판  {s.repEnd:0.0} / 5";
                _summaryHint.text = $"[Enter] {s.week + 1}주차 시작";
                return;
            }
            _summaryTitle.text = $"{s.week}주차 {s.day}일차 결산";
            _summaryBody.text =
                $"세차 완료  {s.served}대\n" +
                $"결제 거절  {s.refused}대\n" +
                $"화나서 떠남  {s.angry}대\n" +
                (s.sentAway > 0 ? $"영업 종료로 돌려보냄  {s.sentAway}대\n" : "") +
                (s.captured > 0 ? $"동물 포획  {s.captured}마리 (+${s.bonus})\n" : "") +
                $"\n수입  +${s.earned}" + (s.tips > 0 ? $"  (팁 ${s.tips})" : "") + "\n" +
                (s.bankrupt ? $"임대료  ${s.rent}  — 낼 수 없음!\n" : $"임대료  -${s.rent}\n") +
                $"보유 금액  ${s.moneyStart} → ${s.moneyEnd}\n" +
                $"평판  {s.repStart:0.0} → {s.repEnd:0.0}";
            _summaryHint.text = "[Enter] " + (dm.AdvancePrompt ?? "계속");
        }

        void OnEnable()
        {
            Critters.CritterBase.Revealed += OnCritterRevealed;
            Critters.CritterBase.Caged += OnCritterCaged;
        }

        void OnDisable()
        {
            Critters.CritterBase.Revealed -= OnCritterRevealed;
            Critters.CritterBase.Caged -= OnCritterCaged;
        }

        // DESIGN-GAP: the reveal "sting" is visual only (pop + toast) until SFX exist (T6.x audio)
        void OnCritterRevealed(Critters.CritterBase c) => Toast($"앗! {c.DisplayName} 등장!", new Color(1f, 0.8f, 0.4f));
        void OnCritterCaged(Critters.CritterBase c)
        {
            var reg = Services.Get<Data.DataRegistry>();
            int bonus = reg != null ? reg.balance.critter.captureBonus : 15;
            Toast($"{c.DisplayName} 포획! +${bonus}", new Color(0.6f, 1f, 0.6f));
        }

        void OnBoss(string vehicleId)
        {
            var reg = Services.Get<Data.DataRegistry>();
            var d = reg != null ? reg.Get<Data.VehicleData>(vehicleId) : null;
            string name = d != null ? d.displayName.Replace("특별 손님: ", "") : vehicleId;
            Toast($"특별 손님 등장! {name} (기본 ${(d != null ? d.basePay : 0)})", new Color(1f, 0.6f, 0.9f));
        }

        void OnDestroy() { if (_bound != null) { _bound.Paid -= OnPaid; _bound.BossArrived -= OnBoss; } }
    }
}
