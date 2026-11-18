using System.Collections.Generic;
using System.Linq;
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
        Text _money, _clock, _info, _income, _rent, _repNum;
        readonly List<GameObject> _captions = new List<GameObject>();
        int _lastMoney = int.MinValue;
        public string StatusText => _money == null ? null : $"{_money.text} | {_income.text} | {_rent.text} | {_repNum.text} | {_clock.text}";
        Image _clockIcon;
        readonly Image[] _stars = new Image[5];
        Image _summary;
        Text _summaryTitle, _summaryBody, _summaryHint, _summaryCrew;
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
            // M4.9 G5: ONE status bar along the top — money · today's income · rent (what is still missing / reached) ·
            // reputation stars + number · clock (G1 hour) + time left. Captions under the icons during week 1.
            var top = new Vector2(0.5f, 1f);
            var bar = UiFactory.FramePanel("StatusBar", root, top, new Vector2(0, -40), new Vector2(1600, 68), new Color(0, 0, 0, 0.5f));
            var bt = bar.transform; var mid = new Vector2(0.5f, 0.5f);
            Text L(string n, string s, int size, float x, float w)
            {
                var t = UiFactory.Label(n, bt, s, size, mid, new Vector2(x, 0), new Vector2(w, 50), TextAnchor.MiddleLeft);
                t.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.7f);
                return t;
            }
            Icon("MoneyIcon", bt, "ico_money", mid, new Vector2(-735, 0), 44);
            _money = L("Money", "$0", 34, -615, 170);
            _income = L("Income", "", 26, -400, 230);
            _rent = L("Rent", "", 24, -110, 330);
            for (int i = 0; i < 5; i++) _stars[i] = Icon("Star" + i, bt, "ico_rep_star", mid, new Vector2(120 + i * 32, 0), 30);
            _repNum = L("RepNum", "", 26, 305, 70);
            _clockIcon = Icon("TimerIcon", bt, "ico_timer", mid, new Vector2(370, 0), 40);
            _clock = L("ClockText", "", 24, 600, 410);
            // week-1 captions under the bar ("what is this icon")
            void Cap(string s, float x)
            {
                var c = UiFactory.Label("Cap_" + s, bt, s, 17, mid, new Vector2(x, -46), new Vector2(220, 24), color: new Color(1f, 1f, 1f, 0.85f));
                c.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
                _captions.Add(c.gameObject);
            }
            Cap("보유 돈", -660); Cap("오늘 번 돈", -400); Cap("임대료 (하루 끝에 냄)", -110); Cap("평판", 200); Cap("시각 · 남은 영업 시간", 520);
            _info = UiFactory.Label("DayInfo", root, "", 24, top, new Vector2(0, -112), new Vector2(900, 34), color: new Color(1f, 0.95f, 0.75f));
            _info.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);

            // summary panel (centre)
            _summary = UiFactory.FramePanel("Summary", root, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(720, 560), new Color(0.1f, 0.15f, 0.3f, 0.95f));
            var txt = _skin != null ? _skin.panelText : Color.white;
            _summaryTitle = UiFactory.Label("Title", _summary.transform, "", 44, new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(640, 60), color: txt);
            _summaryBody = UiFactory.Label("Body", _summary.transform, "", 28, new Vector2(0.5f, 0.5f), new Vector2(0, -18), new Vector2(600, 330), TextAnchor.MiddleLeft, txt);
            _summaryBody.lineSpacing = 1.15f;
            _summaryCrew = UiFactory.Label("Crew", _summary.transform, "", 22, new Vector2(0.5f, 1f), new Vector2(0, -108), new Vector2(660, 30), color: new Color(0.75f, 0.85f, 1f)); // M4.9 G4
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
            if (n.angry) WorldPopup.Show(n.position + Vector3.up * 2.2f, "떠남! -평판", new Color(1f, 0.45f, 0.4f)); // M4.9 G6
            else if (n.refused) WorldPopup.Show(n.position + Vector3.up * 2.2f, "결제 거절! -평판", new Color(1f, 0.45f, 0.4f));
            else WorldPopup.Show(n.position + Vector3.up * 2.2f, "+$" + n.amount, new Color(0.5f, 1f, 0.55f));
            if (n.angry) Toast($"{name}: 기다리다 화나서 떠났어요!", new Color(1f, 0.55f, 0.5f));
            else if (n.refused) Toast($"{name}: 결제 거절! 청결 {Mathf.FloorToInt(n.clean * 100f)}% (평판 하락)", new Color(1f, 0.55f, 0.5f));
            else if (n.dryTip > 0) Toast($"{name}: +${n.amount}  (청결 {Mathf.FloorToInt(n.clean * 100f)}%) 물기까지 완벽! 팁 +${n.dryTip}", new Color(0.55f, 0.95f, 1f));
            else Toast($"{name}: +${n.amount}  (청결 {Mathf.FloorToInt(n.clean * 100f)}%)", new Color(0.6f, 1f, 0.6f));
        }

        // ---- M4.9 G5: +$ / -$ next to the money (rises + fades in 1.4 s)
        readonly List<(Text t, float t0)> _moneyPops = new List<(Text, float)>();
        public string LastMoneyPopup { get; private set; }

        void MoneyPopup(int delta)
        {
            if (_money == null || delta == 0) return;
            var t = UiFactory.Label("MoneyPop", _money.transform.parent, (delta > 0 ? "+$" : "-$") + Mathf.Abs(delta), 30,
                new Vector2(0.5f, 0.5f), new Vector2(-560, -40), new Vector2(200, 40), TextAnchor.MiddleLeft, delta > 0 ? new Color(0.5f, 1f, 0.55f) : new Color(1f, 0.5f, 0.45f));
            t.fontStyle = FontStyle.Bold;
            t.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.85f);
            _moneyPops.Add((t, Time.time));
            LastMoneyPopup = t.text;
        }

        void AnimateMoneyPops()
        {
            for (int i = _moneyPops.Count - 1; i >= 0; i--)
            {
                var (t, t0) = _moneyPops[i];
                float k = (Time.time - t0) / 1.4f;
                if (k >= 1f || t == null) { if (t != null) Destroy(t.gameObject); _moneyPops.RemoveAt(i); continue; }
                t.rectTransform.anchoredPosition = new Vector2(-560, -40 - k * 30f - i * 4f);
                var c = t.color; c.a = k < 0.65f ? 1f : 1f - (k - 0.65f) / 0.35f; t.color = c;
            }
        }

        long _hudKey = long.MinValue;
        static string Mmss(float s) { int i = Mathf.CeilToInt(s); return $"{i / 60}:{i % 60:00}"; }

        void Update()
        {
            var dm = DayManager.Instance;
            AnimateMoneyPops();
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
            _info.gameObject.SetActive(show);
            if (!show) { _summary.gameObject.SetActive(false); return; }

            // money popups (+$ / -$) next to the money, from the replicated value — every peer, every source (pay, rent, bonus)
            int money = eco.Money.Value;
            if (_lastMoney != int.MinValue && money != _lastMoney) MoneyPopup(money - _lastMoney);
            _lastMoney = money;

            // M4.7: strings are rebuilt only when what they show changes (the HUD was the game's biggest per-frame GC source)
            var st = dm.State.Value;
            float left = st == GameState.Day ? (dm.Phase.Value == DayPhase.Open ? dm.TimeLeft : dm.OvertimeLeft) : 0f;
            int tenMin = Mathf.FloorToInt(Visual.DayNightCycle.Hour * 6f);
            long key = ((long)money << 32) ^ ((long)dm.CurrentRent << 20) ^ ((long)(int)st << 16) ^ ((long)dm.Week.Value << 12)
                       ^ ((long)dm.DayNum.Value << 8) ^ ((long)(int)dm.Phase.Value << 6) ^ ((long)Mathf.CeilToInt(left) << 40) ^ ((long)Mathf.RoundToInt(eco.Rep.Value * 10f) << 54)
                       ^ ((long)dm.EarnedToday.Value << 24) ^ ((long)tenMin << 48);
            if (key == _hudKey && !(st == GameState.Summary || st == GameState.GameOver || st == GameState.WeekEnd)) return;
            _hudKey = key;

            int rent = dm.CurrentRent;
            _money.text = "$" + money;
            _money.color = money < rent ? new Color(1f, 0.6f, 0.5f) : Color.white;
            _income.text = $"오늘 +${dm.EarnedToday.Value}";
            int missing = rent - money;
            _rent.text = missing > 0 ? $"임대료 ${rent} · ${missing} 더 필요" : $"임대료 ${rent} · 낼 수 있어요 ✔";
            _rent.color = missing > 0 ? new Color(1f, 0.75f, 0.4f) : new Color(0.55f, 1f, 0.6f);
            float rep = eco.Rep.Value;
            for (int i = 0; i < 5; i++)
            {
                bool full = rep >= i + 0.5f;
                var sp = _skin != null ? _skin.Icon(full ? "ico_rep_star" : "ico_rep_star_empty") : null;
                if (sp != null) _stars[i].sprite = sp; else _stars[i].color = full ? new Color(1f, 0.85f, 0.3f) : new Color(1, 1, 1, 0.25f);
            }
            _repNum.text = rep.ToString("0.0");
            foreach (var c in _captions) c.SetActive(dm.Week.Value <= 1); // labels on the icons during the first week

            string head = $"{dm.Week.Value}주 {dm.DayNum.Value}일"; // short: the bar has room for one line
            int hh = Mathf.FloorToInt(Visual.DayNightCycle.Hour), mm = (tenMin % 6) * 10;
            string clock = $"{hh:00}:{mm:00}";
            switch (st)
            {
                case GameState.Day:
                    if (dm.Phase.Value == DayPhase.Open) { _clock.text = $"{head} · {clock} · 남은 {Mmss(dm.TimeLeft)}"; _clock.color = dm.TimeLeft < 30f ? new Color(1f, 0.8f, 0.4f) : Color.white; }
                    else { _clock.text = $"{head} · {clock} · 마감 {Mmss(dm.OvertimeLeft)}"; _clock.color = new Color(1f, 0.6f, 0.5f); }
                    _info.text = dm.Phase.Value == DayPhase.Open ? "" : "영업 종료 — 남은 차만 마무리하세요";
                    break;
                case GameState.Lobby:
                    _clock.text = $"{head} · {clock} · 준비 중"; _clock.color = Color.white;
                    _info.text = "사무실 일정판에서 [E] 영업 시작";
                    break;
                default:
                    _clock.text = $"{head} · {clock} · 영업 종료"; _clock.color = Color.white;
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
            _summaryCrew.text = "함께한 사람  " + string.Join(" · ", Player.PlayerNet.All.Where(p => p != null).OrderBy(p => p.OwnerClientId).Select(p => p.DisplayName));
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
            Player.PlayerNet.Joined += OnPlayerJoined;
            Player.PlayerNet.Left += OnPlayerLeft;
        }

        void OnDisable()
        {
            Critters.CritterBase.Revealed -= OnCritterRevealed;
            Critters.CritterBase.Caged -= OnCritterCaged;
            Player.PlayerNet.Joined -= OnPlayerJoined;
            Player.PlayerNet.Left -= OnPlayerLeft;
        }

        // M4.9 G4: join / leave notices with the nickname
        void OnPlayerJoined(Player.PlayerNet p) => Toast($"{p.DisplayName} 님이 들어왔어요", new Color(0.6f, 0.9f, 1f));
        void OnPlayerLeft(string name) => Toast($"{name} 님이 나갔어요", new Color(1f, 0.75f, 0.6f));

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
