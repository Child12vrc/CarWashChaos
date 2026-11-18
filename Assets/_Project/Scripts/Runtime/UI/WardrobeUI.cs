using System.Collections.Generic;
using CarWash.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CarWash.UI
{
    /// <summary>
    /// M4.9 G3: the wardrobe — opened from a staff locker ([E]) or Esc → 옷장. Left: a live preview of my character in a small
    /// "studio" far above the lot (its own camera + lights, a copy of the look model, slowly turning, drag to rotate). Right:
    /// the 12 looks and the 4 player colours (taken colours greyed). Choices go to the server (look / colour NetworkVariables,
    /// so everyone sees them) and are remembered in PlayerPrefs. Other HUD canvases are hidden while it is open.
    /// </summary>
    public class WardrobeUI : MonoBehaviour
    {
        public static WardrobeUI Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance._open;

        static readonly Vector3 StudioPos = new Vector3(0f, 500f, 0f);
        const int StudioLayer = 30;

        bool _open;
        Canvas _canvas;
        Transform _studio, _pivot;
        Camera _cam;
        GameObject _clone;
        int _cloneLook = -1, _cloneColor = -1, _shownLook = -1;
        readonly List<Button> _lookButtons = new List<Button>();
        readonly List<Text> _lookLabels = new List<Text>();
        readonly List<Button> _colorButtons = new List<Button>();
        readonly List<Text> _colorLabels = new List<Text>();
        readonly List<Canvas> _hidden = new List<Canvas>();
        Text _status, _title;
        float _spin;
        MaterialPropertyBlock _mpb;

        static PlayerNet Me => PlayerLook.Local != null ? PlayerLook.Local.GetComponent<PlayerNet>() : null;
        static PlayerAppearance MyLooks => PlayerLook.Local != null ? PlayerLook.Local.GetComponent<PlayerAppearance>() : null;
        public int PreviewLook => _cloneLook;
        public string StatusText => _status != null ? _status.text : "";

        public static void Open()
        {
            if (Me == null || MyLooks == null) return;
            if (Instance == null) new GameObject("WardrobeUI").AddComponent<WardrobeUI>();
            Instance.SetOpen(true);
        }
        public static void Close() { if (Instance != null) Instance.SetOpen(false); }

        void Awake() { Instance = this; Build(); }
        void OnDestroy() { if (Instance == this) Instance = null; if (_studio != null) Destroy(_studio.gameObject); }

        void Build()
        {
            _canvas = UiFactory.Canvas("WardrobeCanvas", 60, transform);
            var root = _canvas.transform;
            var right = new Vector2(1f, 0.5f);
            var panel = UiFactory.FramePanel("WardrobePanel", root, right, new Vector2(-380, 0), new Vector2(680, 960), new Color(0.1f, 0.14f, 0.26f, 0.97f)).transform;
            var txt = UiFactory.Skin != null ? UiFactory.Skin.panelText : Color.white;
            var top = new Vector2(0.5f, 1f);
            _title = UiFactory.Label("Title", panel, "옷장", 44, top, new Vector2(0, -52), new Vector2(600, 60), color: txt);
            UiFactory.Label("LooksHead", panel, "외형 (12가지)", 26, top, new Vector2(0, -112), new Vector2(600, 36), color: new Color(0.75f, 0.85f, 1f));
            var looks = MyLooks;
            for (int i = 0; i < looks.Count; i++)
            {
                int li = i;
                var pos = new Vector2(-150 + (i % 2) * 300, -170 - (i / 2) * 66);
                var b = UiFactory.Button("Look" + i, panel, PlayerAppearance.NameOf(looks, i), top, pos, new Vector2(286, 56), () => PickLook(li));
                _lookButtons.Add(b); _lookLabels.Add(b.GetComponentInChildren<Text>());
            }
            float cy = -170 - ((looks.Count + 1) / 2) * 66 - 30;
            UiFactory.Label("ColorHead", panel, "내 색 (다른 사람에게 보이는 외곽선·이름표 색)", 24, top, new Vector2(0, cy), new Vector2(640, 34), color: new Color(0.75f, 0.85f, 1f));
            for (int c = 0; c < PlayerNet.Colors.Length; c++)
            {
                int ci = c;
                var b = UiFactory.Button("Color" + c, panel, PlayerNet.ColorNames[c], top, new Vector2(-234 + c * 156, cy - 56), new Vector2(150, 56), () => PickColor(ci));
                b.GetComponentInChildren<Text>().fontSize = 20;
                b.GetComponent<Image>().color = PlayerNet.Colors[c];
                _colorButtons.Add(b); _colorLabels.Add(b.GetComponentInChildren<Text>());
            }
            _status = UiFactory.Label("Status", panel, "", 22, top, new Vector2(0, cy - 112), new Vector2(640, 30), color: new Color(1f, 0.85f, 0.5f));
            UiFactory.Button("Close", panel, "닫기 [Esc]", new Vector2(0.5f, 0f), new Vector2(0, 60), new Vector2(300, 64), Close);
            UiFactory.Label("DragHint", root, "마우스로 끌어서 돌려 보기", 22, new Vector2(0.32f, 0.06f), Vector2.zero, new Vector2(600, 34), color: new Color(1f, 1f, 1f, 0.75f));
            BuildStudio();
            _canvas.gameObject.SetActive(false);
        }

        void BuildStudio()
        {
            _studio = new GameObject("WardrobeStudio").transform;
            _studio.position = StudioPos;
            _pivot = new GameObject("Turntable").transform; _pivot.SetParent(_studio, false);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(floor.GetComponent<Collider>());
            floor.name = "Floor"; floor.layer = StudioLayer;
            floor.transform.SetParent(_studio, false);
            floor.transform.localPosition = new Vector3(0f, -0.03f, 0f); floor.transform.localScale = new Vector3(1.6f, 0.03f, 1.6f);
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh != null) { var m = new Material(sh); m.SetColor("_BaseColor", new Color(0.32f, 0.36f, 0.45f)); floor.GetComponent<MeshRenderer>().sharedMaterial = m; }
            Light L(string n, Vector3 p, LightType t, float intensity, float range, Color c)
            {
                var go = new GameObject(n) { layer = StudioLayer }; go.transform.SetParent(_studio, false); go.transform.localPosition = p;
                go.transform.LookAt(_studio.position + Vector3.up * 0.9f);
                var l = go.AddComponent<Light>(); l.type = t; l.intensity = intensity; l.range = range; l.color = c; l.spotAngle = 60f; l.shadows = LightShadows.None;
                return l;
            }
            L("Key", new Vector3(1.4f, 2.4f, 2.2f), LightType.Spot, 18f, 8f, new Color(1f, 0.96f, 0.9f));
            L("Fill", new Vector3(-1.8f, 1.4f, 1.6f), LightType.Point, 3f, 6f, new Color(0.75f, 0.85f, 1f));
            L("Rim", new Vector3(0f, 2.0f, -1.8f), LightType.Point, 4f, 5f, new Color(1f, 0.9f, 0.8f));
            var camGo = new GameObject("WardrobeCamera") { layer = StudioLayer };
            camGo.transform.SetParent(_studio, false);
            camGo.transform.localPosition = new Vector3(0f, 1.15f, 4.3f);
            camGo.transform.LookAt(_studio.position + Vector3.up * 0.9f);
            _cam = camGo.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor; _cam.backgroundColor = new Color(0.13f, 0.17f, 0.26f);
            _cam.fieldOfView = 34f; _cam.nearClipPlane = 0.1f; _cam.farClipPlane = 20f;
            _cam.cullingMask = 1 << StudioLayer;
            _cam.depth = 50f;
            _cam.rect = new Rect(0f, 0f, 0.62f, 1f);
            var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(_cam);
            data.renderPostProcessing = true;
            _studio.gameObject.SetActive(false);
        }

        void SetOpen(bool open)
        {
            if (_open == open) return;
            _open = open;
            _canvas.gameObject.SetActive(open);
            _studio.gameObject.SetActive(open);
            if (open)
            {
                if (SettingsMenu.IsOpen) SettingsMenu.Toggle();
                _hidden.Clear();
                foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    if (c != _canvas && c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay && c.enabled) { c.enabled = false; _hidden.Add(c); }
                _status.text = "";
                _shownLook = -1; _cloneLook = -1; _cloneColor = -1;
                _spin = 200f;
                Refresh();
            }
            else
            {
                foreach (var c in _hidden) if (c != null) c.enabled = true;
                _hidden.Clear();
                if (_clone != null) { Destroy(_clone); _clone = null; }
            }
            PlayerLook.SetCursorLocked(!open);
        }

        /// <summary>Dev checks: same as clicking.</summary>
        public void DevPickLook(int i) => PickLook(i);
        public void DevPickColor(int c) => PickColor(c);

        void PickLook(int i)
        {
            var a = MyLooks; if (a == null) return;
            var rag = a.GetComponent<PlayerRagdoll>();
            if (rag != null && rag.ControlsBody) { _status.text = "넘어져 있을 때는 갈아입을 수 없어요"; return; }
            a.RequestSet(i);
            _shownLook = i;          // preview right away; the server confirms through the NetworkVariable
            _status.text = $"'{PlayerAppearance.NameOf(a, i)}'(으)로 갈아입었어요";
            Refresh();
        }

        void PickColor(int c)
        {
            var me = Me; if (me == null) return;
            if (me.ColorTaken(c)) { _status.text = $"{PlayerNet.ColorNames[c]}은(는) 다른 사람이 쓰고 있어요"; return; }
            me.RequestColor(c);
            _status.text = $"내 색: {PlayerNet.ColorNames[c]}";
        }

        void Update()
        {
            if (!_open) return;
            if (Me == null) { SetOpen(false); return; }
            Refresh();
            // turntable: slow spin, mouse drag on the preview side rotates
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed && mouse.position.ReadValue().x < Screen.width * 0.62f) _spin -= mouse.delta.ReadValue().x * 0.4f;
            else _spin += Time.unscaledDeltaTime * 22f;
            _pivot.localRotation = Quaternion.Euler(0f, _spin, 0f);
        }

        void Refresh()
        {
            var me = Me; var a = MyLooks;
            if (me == null || a == null) return;
            int look = a.Look.Value;
            if (_shownLook >= 0 && look == _shownLook) _shownLook = -1; // confirmed
            int want = _shownLook >= 0 ? _shownLook : look;
            if (want != _cloneLook) BuildClone(a, want);
            int col = Mathf.Max(0, me.ColorIndex.Value) % PlayerNet.Colors.Length;
            if (col != _cloneColor) TintClone(col);
            for (int i = 0; i < _lookLabels.Count; i++)
            {
                _lookLabels[i].text = (i == want ? "✔ " : "") + PlayerAppearance.NameOf(a, i);
                _lookLabels[i].color = i == want ? new Color(1f, 0.88f, 0.3f) : Color.white;
            }
            for (int c = 0; c < _colorLabels.Count; c++)
            {
                bool taken = me.ColorTaken(c);
                _colorLabels[c].text = (c == col ? "✔ " : "") + PlayerNet.ColorNames[c] + (taken ? " · 사용 중" : "");
                var img = _colorButtons[c].GetComponent<Image>();
                var baseC = PlayerNet.Colors[c];
                img.color = taken ? Color.Lerp(baseC, Color.gray, 0.7f) : baseC;
            }
            _title.text = $"옷장 — {me.DisplayName}";
        }

        void BuildClone(PlayerAppearance a, int index)
        {
            if (_clone != null) Destroy(_clone);
            _clone = null; _cloneLook = index; _cloneColor = -1;
            var e = a.EntryAt(index);
            if (e == null || e.root == null) return;
            _clone = Instantiate(e.root, _pivot);
            _clone.name = "Preview_" + e.id;
            _clone.SetActive(true);
            _clone.transform.localPosition = Vector3.zero;
            _clone.transform.localRotation = Quaternion.identity;
            _clone.transform.localScale = e.root.transform.lossyScale;
            // a picture, not a body: no joints / physics / scripts (the Animator keeps the idle pose going)
            foreach (var j in _clone.GetComponentsInChildren<Joint>(true)) DestroyImmediate(j);
            foreach (var rb in _clone.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(rb);
            foreach (var c in _clone.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            foreach (var mb in _clone.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(mb);
            foreach (var t in _clone.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = StudioLayer;
            foreach (var r in _clone.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = true;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            }
        }

        void TintClone(int col)
        {
            _cloneColor = col;
            if (_clone == null) return;
            _mpb ??= new MaterialPropertyBlock();
            var c = PlayerNet.Colors[col];
            foreach (var r in _clone.GetComponentsInChildren<Renderer>(true))
            {
                bool outline = r.sharedMaterial != null && r.sharedMaterial.name.Contains("Outline");
                if (!outline) continue;
                for (int m = 0; m < r.sharedMaterials.Length; m++)
                {
                    r.GetPropertyBlock(_mpb, m);
                    _mpb.SetColor("_BaseColor", c); _mpb.SetColor("_Color", c);
                    r.SetPropertyBlock(_mpb, m);
                }
            }
        }
    }
}
