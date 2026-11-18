using System.Collections.Generic;
using CarWash.Core;
using CarWash.Data;
using CarWash.Day;
using CarWash.Player;
using CarWash.Vehicles;
using UnityEngine;
using UnityEngine.UI;

namespace CarWash.UI
{
    /// <summary>
    /// HUD bay cards (TECH S12, ui_card) on the right edge: per bay its state, the car's icon/name, a clean% bar
    /// (after the −2%/trash penalty; red = would be refused, yellow = ×0.6, green = full/bonus) and a wash-time bar
    /// (green = still in the ×1.2 fast window, yellow = on time, red = overtime). Plus queue patience bars projected
    /// above queued cars (ico_patience). All from replicated vehicle state, so every peer shows the same.
    /// </summary>
    public class BayCards : MonoBehaviour
    {
        class Card
        {
            public Image root, icon, cleanFill, timeFill;
            public RectTransform info;
            public Text title, status, carName, cleanText, timeText;
        }

        class PBar { public RectTransform root; public Image fill; }

        readonly List<Card> _cards = new List<Card>();
        readonly List<PBar> _pbars = new List<PBar>();
        RectTransform _root;
        UiSkin _skin;
        DataRegistry _data;
        Vehicle[] _vehicles = new Vehicle[0];
        float _nextScan;

        /// <summary>Dev check: text of card i ("베이 1 세차 중 | 세단 | 77% | 0:42").</summary>
        public string CardText(int i) => i < _cards.Count && _cards[i].root.gameObject.activeSelf
            ? $"{_cards[i].title.text} {_cards[i].status.text} | {_cards[i].carName.text} | {_cards[i].cleanText.text} | {_cards[i].timeText.text}" : null;
        public int VisiblePatienceBars { get; private set; }

        public void Build(Transform root)
        {
            _root = (RectTransform)root;
            _skin = UiFactory.Skin;
            _data = Services.Get<DataRegistry>();
            for (int i = 0; i < 3; i++) _cards.Add(MakeCard(i));
        }

        Image Icon(Transform parent, string name, Vector2 anchor, Vector2 pos, float size)
        {
            var img = UiFactory.Panel(name, parent, anchor, pos, new Vector2(size, size), Color.white);
            img.preserveAspect = true; img.raycastTarget = false;
            var sp = _skin != null ? _skin.Icon(name) : null;
            if (sp != null) img.sprite = sp; else img.color = new Color(1, 1, 1, 0.3f);
            return img;
        }

        Card MakeCard(int i)
        {
            var c = new Card();
            var anchor = new Vector2(1f, 0.5f);
            c.root = UiFactory.Panel("BayCard" + (i + 1), _root, anchor, new Vector2(-180, 190 - i * 170), new Vector2(330, 156), new Color(0.1f, 0.15f, 0.3f, 0.85f));
            if (_skin != null && _skin.card != null) { c.root.sprite = _skin.card; c.root.type = Image.Type.Sliced; c.root.color = Color.white; }
            c.root.raycastTarget = false;
            var tl = new Vector2(0f, 1f);
            // ui_card is a light card → dark text (same navy as button labels); flat fallback stays white-on-dark
            bool light = _skin != null && _skin.card != null;
            var txt = light ? _skin.buttonText : Color.white;
            c.title = UiFactory.Label("Title", c.root.transform, $"베이 {i + 1}", 24, tl, new Vector2(70, -26), new Vector2(110, 30), TextAnchor.MiddleLeft, txt);
            c.status = UiFactory.Label("Status", c.root.transform, "", 22, new Vector2(1f, 1f), new Vector2(-90, -26), new Vector2(160, 30), TextAnchor.MiddleRight,
                light ? new Color(0.85f, 0.4f, 0.05f) : new Color(1f, 0.85f, 0.3f));
            // everything below the title only shows while a car is in the bay
            c.info = UiFactory.Rect("Info", c.root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var t = c.info;
            c.icon = UiFactory.Panel("Car", t, tl, new Vector2(52, -76), new Vector2(64, 64), Color.white);
            c.icon.preserveAspect = true; c.icon.raycastTarget = false;
            c.carName = UiFactory.Label("CarName", t, "", 20, tl, new Vector2(190, -56), new Vector2(200, 26), TextAnchor.MiddleLeft, txt);
            Icon(t, "ico_clean", tl, new Vector2(106, -88), 26);
            c.cleanFill = UiFactory.Bar("CleanBar", t, tl, new Vector2(186, -88), new Vector2(120, 20));
            c.cleanText = UiFactory.Label("CleanPct", t, "", 20, tl, new Vector2(282, -88), new Vector2(66, 26), TextAnchor.MiddleLeft, txt);
            Icon(t, "ico_timer", tl, new Vector2(106, -120), 26);
            c.timeFill = UiFactory.Bar("TimeBar", t, tl, new Vector2(186, -120), new Vector2(120, 20));
            c.timeText = UiFactory.Label("TimeText", t, "", 20, tl, new Vector2(282, -120), new Vector2(66, 26), TextAnchor.MiddleLeft, txt);
            return c;
        }

        PBar MakePBar()
        {
            var rt = UiFactory.Rect("PatienceBar", _root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 30));
            Icon(rt, "ico_patience", new Vector2(0f, 0.5f), new Vector2(12, 0), 28);
            var fill = UiFactory.Bar("Bar", rt, new Vector2(0.5f, 0.5f), new Vector2(14, 0), new Vector2(112, 16));
            var pb = new PBar { root = rt, fill = fill };
            _pbars.Add(pb);
            return pb;
        }

        static string Mmss(float s) { int i = Mathf.Max(0, Mathf.CeilToInt(s)); return $"{i / 60}:{i % 60:00}"; }

        // ---- critter wetness bars (T3.1): blue = wetness, yellow "기절!" while stunned, "잡힘" while carried
        class CBar { public RectTransform root; public Image icon, fill; public Text label; }
        readonly List<CBar> _cbars = new List<CBar>();
        static readonly Color WetBlue = new Color32(0x5A, 0xC8, 0xFA, 0xFF);
        public int VisibleCritterBars { get; private set; }

        CBar MakeCBar()
        {
            var rt = UiFactory.Rect("CritterBar", _root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 30));
            var icon = UiFactory.Panel("Icon", rt, new Vector2(0f, 0.5f), new Vector2(12, 0), new Vector2(30, 30), Color.white);
            icon.preserveAspect = true; icon.raycastTarget = false;
            var fill = UiFactory.Bar("Bar", rt, new Vector2(0.5f, 0.5f), new Vector2(14, 0), new Vector2(112, 14));
            var label = UiFactory.Label("State", rt, "", 20, new Vector2(0.5f, 1f), new Vector2(14, 12), new Vector2(160, 26), color: new Color(1f, 0.9f, 0.3f));
            label.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
            var cb = new CBar { root = rt, icon = icon, fill = fill, label = label };
            _cbars.Add(cb);
            return cb;
        }

        void UpdateCritterBars(Camera cam)
        {
            int n = 0;
            if (cam != null)
                foreach (var c in Critters.CritterBase.All)
                {
                    if (c == null || !c.IsSpawned) continue;
                    var st = c.State.Value;
                    if (st == Critters.CritterState.Hidden || st == Critters.CritterState.Captured || st == Critters.CritterState.Escaped) continue;
                    var sp = cam.WorldToScreenPoint(c.transform.position + Vector3.up * 1.3f);
                    if (sp.z <= 0.3f || sp.z > 40f) continue;
                    if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, sp, null, out var local)) continue;
                    var cb = n < _cbars.Count ? _cbars[n] : MakeCBar();
                    n++;
                    cb.root.gameObject.SetActive(true);
                    cb.root.anchoredPosition = local;
                    var icon = _skin != null ? _skin.CritterIcon(c.CritterId) : null;
                    cb.icon.sprite = icon; cb.icon.enabled = icon != null;
                    bool stunned = st == Critters.CritterState.Stunned, carried = st == Critters.CritterState.Carried;
                    float f = c.MaxWetness.Value > 0f ? c.Wetness.Value / c.MaxWetness.Value : 0f;
                    UiFactory.SetBar(cb.fill, stunned ? 1f : f, stunned ? UiFactory.BarYellow : WetBlue);
                    cb.label.text = stunned ? "기절! [E] 잡기" : carried ? "잡힘 — 우리로!" : "";
                }
            for (int i = n; i < _cbars.Count; i++) _cbars[i].root.gameObject.SetActive(false);
            VisibleCritterBars = n;
        }

        void LateUpdate()
        {
            UpdateCritterBars(PlayerLook.Local != null ? PlayerLook.Local.Camera : Camera.main);
            var dm = DayManager.Instance;
            // M4.6 D2: only while the lot is open for business (not in the lobby), only open bays, stacked from the top
            bool show = dm != null && dm.State.Value == GameState.Day;
            int shown = 0;
            if (Time.time >= _nextScan) { _nextScan = Time.time + 0.25f; _vehicles = FindObjectsByType<Vehicle>(FindObjectsSortMode.None); }
            float trashPenalty = _data != null ? _data.balance.dirt.trashPenalty : 0.02f;

            for (int i = 0; i < _cards.Count; i++)
            {
                var c = _cards[i];
                bool open = LotState.Instance == null || LotState.Instance.IsOpen(i);
                c.root.gameObject.SetActive(show && open);
                if (!show || !open) continue;
                c.root.rectTransform.anchoredPosition = new Vector2(c.root.rectTransform.anchoredPosition.x, 190 - shown++ * 170);
                Vehicle car = null;
                foreach (var v in _vehicles)
                    if (v != null && v.IsSpawned && v.BayIndex.Value == i &&
                        (v.State.Value == VehicleState.Parking || v.State.Value == VehicleState.Washing || v.State.Value == VehicleState.Inspecting)) { car = v; break; }
                c.root.color = open ? Color.white : new Color(1, 1, 1, 0.45f);
                c.status.text = !open ? "닫힘" : car == null ? "비어 있음" :
                    car.State.Value == VehicleState.Parking ? "주차 중" : car.State.Value == VehicleState.Washing ? "세차 중" : "검사 중";
                bool hasCar = car != null;
                c.info.gameObject.SetActive(hasCar);
                c.cleanText.text = c.timeText.text = c.carName.text = "";
                if (!hasCar) continue;
                var d = car.Data;
                c.icon.sprite = _skin != null ? _skin.VehicleIcon(d) : null;
                c.carName.text = d != null ? d.displayName : car.DataId;
                float clean = Mathf.Clamp01(car.Dirt.CleanPct.Value - car.TrashRemaining * trashPenalty);
                var cc = clean < 0.6f ? UiFactory.BarRed : clean < 0.8f ? UiFactory.BarYellow : UiFactory.BarGreen;
                UiFactory.SetBar(c.cleanFill, clean, cc);
                c.cleanText.text = Mathf.FloorToInt(clean * 100f) + "%";
                float limit = d != null ? d.timeLimit : 0f;
                float el = car.State.Value == VehicleState.Washing ? car.PhaseElapsed : 0f;
                if (limit > 0f)
                {
                    float left = limit - el;
                    var tc = el <= limit * 0.5f ? UiFactory.BarGreen : el <= limit ? UiFactory.BarYellow : UiFactory.BarRed;
                    UiFactory.SetBar(c.timeFill, left > 0f ? left / limit : 1f, tc);
                    c.timeText.text = left >= 0f ? Mmss(left) : "+" + Mmss(-left);
                }
            }

            // patience bars over queued cars
            var cam = PlayerLook.Local != null ? PlayerLook.Local.Camera : Camera.main;
            float patience = dm != null ? Mathf.Max(1f, dm.PatienceNet.Value) : 45f;
            int n = 0;
            if (show && cam != null)
                foreach (var v in _vehicles)
                {
                    if (v == null || !v.IsSpawned || v.State.Value != VehicleState.Queued) continue;
                    var sp = cam.WorldToScreenPoint(v.transform.position + Vector3.up * 2.8f);
                    if (sp.z <= 0.5f || sp.z > 60f) continue;
                    if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, sp, null, out var local)) continue;
                    var pb = n < _pbars.Count ? _pbars[n] : MakePBar();
                    n++;
                    pb.root.gameObject.SetActive(true);
                    pb.root.anchoredPosition = local;
                    float f = 1f - v.PhaseElapsed / patience;
                    UiFactory.SetBar(pb.fill, f, f > 0.5f ? UiFactory.BarGreen : f > 0.25f ? UiFactory.BarYellow : UiFactory.BarRed);
                }
            for (int i = n; i < _pbars.Count; i++) _pbars[i].root.gameObject.SetActive(false);
            VisiblePatienceBars = n;
        }
    }
}
