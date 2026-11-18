using CarWash.Player;
using UnityEngine;
using UnityEngine.UI;

namespace CarWash.UI
{
    /// <summary>
    /// Local HUD (TECH S12), built in code: crosshair, interaction prompt, throw-charge bar, status line.
    /// Extended by later tasks (slots, money/rep/timer, bay cards).
    /// </summary>
    public class Hud : MonoBehaviour
    {
        public static Hud Instance { get; private set; }
        public Transform Root { get; private set; }

        Text _prompt, _status;
        Image _chargeBg, _chargeFill;

        void Awake()
        {
            Instance = this;
            UiFactory.EnsureEventSystem();
            Root = UiFactory.Canvas("HudCanvas", 10, transform).transform;
            var c = new Vector2(0.5f, 0.5f);
            _skin = UiFactory.Skin;
            if (_skin != null && _skin.crosshair != null)
            {
                _crosshair = UiFactory.Panel("Crosshair", Root, c, Vector2.zero, new Vector2(64, 64), Color.white); // native 64px
                _crosshair.sprite = _skin.crosshair;
                _crosshair.raycastTarget = false;
            }
            else
            {
                UiFactory.Panel("CrossH", Root, c, Vector2.zero, new Vector2(18, 2), new Color(1, 1, 1, 0.85f));
                UiFactory.Panel("CrossV", Root, c, Vector2.zero, new Vector2(2, 18), new Color(1, 1, 1, 0.85f));
            }
            _prompt = UiFactory.Label("Prompt", Root, "", 30, c, new Vector2(0, -90), new Vector2(1200, 50));
            var outline = _prompt.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(0, 0, 0, 0.8f);
            _chargeBg = UiFactory.Panel("ChargeBg", Root, c, new Vector2(0, -140), new Vector2(300, 14), new Color(0, 0, 0, 0.5f));
            _chargeFill = UiFactory.Panel("ChargeFill", _chargeBg.transform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(300, 14), new Color(1f, 0.75f, 0.2f));
            _chargeFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            _status = UiFactory.Label("Status", Root, "", 24, new Vector2(0.5f, 0f), new Vector2(0, 150), new Vector2(1200, 40), color: new Color(1f, 0.6f, 0.5f));
            for (int i = 0; i < _slots.Length; i++)
            {
                bool skinned = _skin != null && _skin.slot != null;
                _slots[i] = UiFactory.Panel("Slot" + (i + 1), Root, new Vector2(0.5f, 0f), new Vector2((i - 1.5f) * 96f, 62f), new Vector2(84, 84), skinned ? Color.white : new Color(0, 0, 0, 0.45f));
                if (skinned) { _slots[i].sprite = _skin.slot; _slots[i].type = Image.Type.Sliced; }
                _slotIcons[i] = UiFactory.Panel("Icon", _slots[i].transform, new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(60, 60), Color.white);
                _slotIcons[i].preserveAspect = true; _slotIcons[i].raycastTarget = false;
                _slotKeys[i] = UiFactory.Label("Key", _slots[i].transform, (i + 1).ToString(), 20, new Vector2(0f, 1f), new Vector2(16, -16), new Vector2(28, 28));
                _slotKeys[i].gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
                // tool name under the slot (active slot, or fallback when the icon is missing)
                _slotLabels[i] = UiFactory.Label("Label", _slots[i].transform, "", 18, new Vector2(0.5f, 0f), new Vector2(0, -14), new Vector2(118, 26));
                _slotLabels[i].gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
            }
            Day = gameObject.AddComponent<DayHud>();
            Day.Build(Root);
            Bays = gameObject.AddComponent<BayCards>();
            Bays.Build(Root);
            Lobby = gameObject.AddComponent<LobbyPanel>();
            Lobby.Build(Root);
            Invite = gameObject.AddComponent<InviteCard>(); // P7: join code / IP
            Invite.Build(Root);
            Pings = gameObject.AddComponent<PingMarkers>();
            Pings.Build(Root);
            DebugView = gameObject.AddComponent<DebugOverlay>();
            DebugView.Build(Root);
            Menu = gameObject.AddComponent<SettingsMenu>();
            Menu.Build(Root);
            Tutorial = gameObject.AddComponent<TutorialPanel>(); // M4.6 D1
            Tutorial.Build(Root);
            // M4.6 D2: one size knob for the whole HUD (Esc → 화면 → UI 크기); the scaler's reference resolution shrinks / grows it
            _scaler = Root.GetComponent<CanvasScaler>();
            var settings = Core.Services.Get<Core.Settings>();
            if (settings != null) { settings.Changed += ApplyUiScale; ApplyUiScale(); }
        }

        public LobbyPanel Lobby { get; private set; }
        public InviteCard Invite { get; private set; }
        public PingMarkers Pings { get; private set; }
        public DebugOverlay DebugView { get; private set; }
        public SettingsMenu Menu { get; private set; }
        public TutorialPanel Tutorial { get; private set; }
        CanvasScaler _scaler;

        void ApplyUiScale()
        {
            var s = Core.Services.Get<Core.Settings>();
            if (_scaler == null || s == null) return;
            _scaler.referenceResolution = new Vector2(1920f, 1080f) / Mathf.Max(0.3f, s.UiScale);
        }


        public DayHud Day { get; private set; }
        public BayCards Bays { get; private set; }

        UiSkin _skin;
        Image _crosshair;
        readonly RaycastHit[] _aimHits = new RaycastHit[8];

        /// <summary>True when the equipped cleaning tool's aim ray hits a dirtable vehicle within range (ui_crosshair_hit).</summary>
        public bool CrosshairOnTarget { get; private set; }

        void UpdateCrosshair(PlayerLook look)
        {
            bool onTarget = false;
            var tool = look != null ? look.GetComponent<PlayerTools>()?.Equipped : null;
            if (tool != null && !tool.Stowed)
            {
                float range = tool is Tools.SprayTool spray ? spray.Range : (tool.Data != null ? tool.Data.range + 0.8f : 1.8f);
                var ray = look.AimRay;
                int n = Physics.RaycastNonAlloc(ray, _aimHits, range, Core.Layers.SprayMask, QueryTriggerInteraction.Ignore);
                float best = float.MaxValue; Collider bestCol = null;
                for (int i = 0; i < n; i++)
                    if (_aimHits[i].distance < best && !_aimHits[i].collider.transform.IsChildOf(look.transform) && !Vehicles.VehicleHull.Is(_aimHits[i].collider)) { best = _aimHits[i].distance; bestCol = _aimHits[i].collider; }
                onTarget = bestCol != null && bestCol.GetComponentInParent<Dirt.DirtNetSync>() != null;
            }
            CrosshairOnTarget = onTarget;
            if (_crosshair != null && _skin != null) _crosshair.sprite = onTarget && _skin.crosshairHit != null ? _skin.crosshairHit : _skin.crosshair;
            // M4.8 F5: hit marker — the crosshair flashes red and pops for 0.18 s
            if (_crosshair != null)
            {
                _hitT = Mathf.Max(0f, _hitT - Time.deltaTime);
                float k = _hitT / 0.18f;
                _crosshair.color = Color.Lerp(Color.white, new Color(1f, 0.25f, 0.2f), k);
                _crosshair.rectTransform.localScale = Vector3.one * (1f + 0.45f * k);
            }
        }

        float _hitT;
        /// <summary>M4.8 F5: the local player's swing connected.</summary>
        public void HitMarker() => _hitT = 0.18f;

        readonly Image[] _slots = new Image[4];
        readonly Text[] _slotLabels = new Text[4];
        readonly Text[] _slotKeys = new Text[4];
        readonly Image[] _slotIcons = new Image[4];

        /// <summary>Sprite currently shown in slot i (null = empty); for dev checks.</summary>
        public Sprite SlotIcon(int i) => _slotIcons[i] != null && _slotIcons[i].enabled ? _slotIcons[i].sprite : null;

        void UpdateSlots(PlayerLook look)
        {
            var inv = look != null ? look.GetComponent<PlayerInventory>() : null;
            for (int i = 0; i < _slots.Length; i++)
            {
                var t = inv != null ? inv.Get(i) : null;
                bool active = inv != null && inv.ActiveSlot.Value == i;
                string name = t != null ? (t.Data != null ? t.Data.displayName : t.ToolId) : "";
                var icon = t != null && _skin != null ? _skin.ToolIcon(t.ToolId) : null;
                _slotIcons[i].sprite = icon;
                _slotIcons[i].enabled = icon != null;
                _slotLabels[i].text = active || icon == null ? name : "";
                if (_skin != null && _skin.slot != null)
                    _slots[i].sprite = active && _skin.slotSelected != null ? _skin.slotSelected : _skin.slot;
                else
                    _slots[i].color = active ? new Color(1f, 0.75f, 0.2f, 0.6f) : new Color(0, 0, 0, 0.45f);
                _slotKeys[i].color = active ? new Color(1f, 0.85f, 0.3f) : Color.white;
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            var s = Core.Services.Get<Core.Settings>(); if (s != null) s.Changed -= ApplyUiScale;
        }

        void LateUpdate()
        {
            var look = PlayerLook.Local;
            UpdateSlots(look);
            UpdateCrosshair(look);
            if (look == null) { _prompt.text = ""; _chargeBg.gameObject.SetActive(false); return; }
            var grabber = look.GetComponent<PlayerGrabber>();
            var inter = look.GetComponent<PlayerInteractor>();
            string text = grabber != null ? grabber.PromptText() : null;
            if (string.IsNullOrEmpty(text) && inter != null) text = inter.PromptText();
            _prompt.text = text ?? "";

            bool charging = grabber != null && grabber.Charging;
            _chargeBg.gameObject.SetActive(charging);
            if (charging) _chargeFill.rectTransform.sizeDelta = new Vector2(300 * grabber.Charge, 14);

            var st = look.GetComponent<PlayerStatus>();
            string s = "";
            if (st.Has(StatusFlags.Knocked)) s += "넘어짐!  ";
            if (st.Has(StatusFlags.Soaked)) s += "흠뻑 젖음  ";
            if (st.Has(StatusFlags.Stung)) s += "벌에 쏘임  ";
            if (st.Has(StatusFlags.InFoam)) s += "거품 위 (미끄러움)";
            var rag = look.GetComponent<PlayerRagdoll>();
            if (rag != null && rag.IsHeld) s += "  누가 들고 있어요! [Space] 연타해서 빠져나오기";
            else if (rag != null && rag.IsDown) s += "  쓰러졌어요 — 동료가 [F]로 옮길 수 있어요";
            _status.text = s;
        }
    }
}
