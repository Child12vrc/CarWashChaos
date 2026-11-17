using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CarWash.UI
{
    /// <summary>
    /// Code-first uGUI helpers. Uses the legacy dynamic font (OS fallback renders Korean without a font asset).
    /// DESIGN-GAP: final OFL Korean font arrives in T6.5/T6.4; swap Font here.
    /// </summary>
    public static class UiFactory
    {
        static Font _font;
        public static Font Font => _font != null ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static Canvas Canvas(string name, int sortOrder = 0, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent) go.transform.SetParent(parent, false);
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = sortOrder;
            var s = go.GetComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920, 1080);
            s.matchWidthOrHeight = 0.5f;
            return c;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null || Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Object.DontDestroyOnLoad(go);
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        public static Image Panel(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var rt = Rect(name, parent, anchor, anchor, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text Label(string name, Transform parent, string text, int size, Vector2 anchor, Vector2 pos, Vector2 box,
            TextAnchor align = TextAnchor.MiddleCenter, Color? color = null)
        {
            var rt = Rect(name, parent, anchor, anchor, pos, box);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font; t.text = text; t.fontSize = size; t.alignment = align;
            t.color = color ?? Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>UI sprites (docs/ART_IMPORT.md), registered by Bootstrap. Null in standalone test scenes → flat colors.</summary>
        public static UiSkin Skin => Core.Services.Get<UiSkin>();

        public static readonly Color BarGreen = new Color32(0x4C, 0xD9, 0x64, 0xFF);
        public static readonly Color BarYellow = new Color32(0xFF, 0xC9, 0x3C, 0xFF);
        public static readonly Color BarRed = new Color32(0xFF, 0x5A, 0x4E, 0xFF);

        /// <summary>Progress bar: ui_bar_bg (sliced) + ui_bar_fill (Filled horizontal, tinted). Returns the fill image.</summary>
        public static Image Bar(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var skin = Skin;
            var bg = Panel(name, parent, anchor, pos, size, new Color(0, 0, 0, 0.5f));
            bg.raycastTarget = false;
            if (skin != null && skin.barBg != null)
            {
                bg.sprite = skin.barBg; bg.type = Image.Type.Sliced; bg.color = Color.white;
                bg.pixelsPerUnitMultiplier = 32f / size.y; // keep the 32 px art's caps proportional
            }
            // left-anchored fill whose width = fraction: white ui_bar_fill art (9-slice, 32 px tall) tinted per state;
            // falls back to the built-in rounded sprite, or plain colour without a skin.
            var fill = Panel("Fill", bg.transform, new Vector2(0f, 0.5f), new Vector2(2f, 0f), size - new Vector2(4, 4), BarGreen);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.raycastTarget = false;
            var fillSprite = skin != null ? (skin.barFill != null ? skin.barFill : skin.barFillWhite) : null;
            if (fillSprite != null)
            {
                fill.sprite = fillSprite; fill.type = Image.Type.Sliced;
                fill.pixelsPerUnitMultiplier = (fillSprite == skin.barFill ? 32f : 20f) / (size.y - 4f);
            }
            return fill;
        }

        public static void SetBar(Image fill, float fraction, Color color)
        {
            float max = ((RectTransform)fill.transform.parent).sizeDelta.x - 4f;
            float w = max * Mathf.Clamp01(fraction);
            var rt = fill.rectTransform;
            rt.sizeDelta = new Vector2(w, rt.sizeDelta.y);
            fill.enabled = w >= 3f;
            fill.color = color;
        }

        /// <summary>9-slice framed panel (ui_panel) — falls back to a flat color without a skin.</summary>
        public static Image FramePanel(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color fallback)
        {
            var img = Panel(name, parent, anchor, pos, size, fallback);
            var skin = Skin;
            if (skin != null && skin.panel != null) { img.sprite = skin.panel; img.type = Image.Type.Sliced; img.color = Color.white; }
            return img;
        }

        public static InputField Input(string name, Transform parent, string value, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var skin = Skin;
            var img = skin != null && skin.panel != null
                ? FramePanel(name, parent, anchor, pos, size, Color.white)
                : Panel(name, parent, anchor, pos, size, new Color(1f, 1f, 1f, 0.95f));
            var field = img.gameObject.AddComponent<InputField>();
            var txtColor = skin != null && skin.panel != null ? skin.panelText : Color.black;
            var txt = Label("Text", img.transform, value, 28, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(40, 0), TextAnchor.MiddleLeft, txtColor);
            txt.supportRichText = false;
            field.textComponent = txt;
            field.text = value;
            return field;
        }

        public static Button Button(string name, Transform parent, string text, Vector2 anchor, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var img = Panel(name, parent, anchor, pos, size, new Color(0.15f, 0.45f, 0.85f, 0.95f));
            var b = img.gameObject.AddComponent<Button>();
            var skin = Skin;
            var textColor = Color.white;
            if (skin != null && skin.btnPrimary != null)
            {
                // ART_IMPORT.md: Button transition = Sprite Swap (highlighted=hover, pressed, disabled)
                img.sprite = skin.btnPrimary; img.type = Image.Type.Sliced; img.color = Color.white;
                b.transition = Selectable.Transition.SpriteSwap;
                b.spriteState = new SpriteState { highlightedSprite = skin.btnHover, selectedSprite = skin.btnHover, pressedSprite = skin.btnPressed, disabledSprite = skin.btnDisabled };
                textColor = skin.buttonText;
            }
            else { var colors = b.colors; colors.highlightedColor = new Color(0.8f, 0.9f, 1f); b.colors = colors; }
            var label = Label("Text", img.transform, text, 28, new Vector2(0.5f, 0.5f), new Vector2(0, 2), size, color: textColor);
            label.fontStyle = FontStyle.Bold;
            if (onClick != null) b.onClick.AddListener(onClick);
            return b;
        }
    }
}
