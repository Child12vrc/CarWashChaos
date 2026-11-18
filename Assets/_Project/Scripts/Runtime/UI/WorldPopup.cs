using System.Collections.Generic;
using CarWash.Player;
using UnityEngine;
using UnityEngine.UI;

namespace CarWash.UI
{
    /// <summary>
    /// M4.9 G5/G6: floating text at a world point (screen-space, follows the point, rises and fades over ~1.8 s):
    /// '+$56', '-$60', '떠남! -평판'… Local only (every peer raises its own from the replicated events).
    /// </summary>
    public class WorldPopup : MonoBehaviour
    {
        class P { public Text text; public Vector3 world; public float t, life; public Color color; }

        static WorldPopup _i;
        readonly List<P> _live = new List<P>();
        readonly Stack<Text> _pool = new Stack<Text>();
        RectTransform _root;
        public static int Shown { get; private set; }
        public static string LastText { get; private set; }

        static WorldPopup I
        {
            get
            {
                if (_i != null) return _i;
                var go = new GameObject("WorldPopups");
                _i = go.AddComponent<WorldPopup>();
                _i._root = (RectTransform)UiFactory.Canvas("WorldPopupCanvas", 15, go.transform).transform;
                return _i;
            }
        }

        /// <summary>Shows <paramref name="text"/> above <paramref name="world"/> (big = money / leaving, small = info).</summary>
        public static void Show(Vector3 world, string text, Color color, bool big = true)
        {
            var w = I;
            var t = w._pool.Count > 0 ? w._pool.Pop() : w.Make();
            t.gameObject.SetActive(true);
            t.text = text; t.fontSize = big ? 40 : 28; t.color = color;
            w._live.Add(new P { text = t, world = world, t = 0f, life = 1.8f, color = color });
            Shown++; LastText = text;
        }

        Text Make()
        {
            var t = UiFactory.Label("Popup", _root, "", 40, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420, 60));
            t.fontStyle = FontStyle.Bold; t.raycastTarget = false;
            var o = t.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0f, 0f, 0f, 0.85f); o.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        void OnDestroy() { if (_i == this) _i = null; }

        void LateUpdate()
        {
            var cam = PlayerLook.Local != null ? PlayerLook.Local.Camera : Camera.main;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var p = _live[i];
                p.t += Time.deltaTime;
                if (p.t >= p.life || cam == null) { p.text.gameObject.SetActive(false); _pool.Push(p.text); _live.RemoveAt(i); continue; }
                var sp = cam.WorldToScreenPoint(p.world + Vector3.up * (p.t * 0.7f));
                Vector2 local = default;
                bool vis = sp.z > 0.3f && RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, sp, null, out local);
                p.text.enabled = vis;
                if (!vis) continue;
                p.text.rectTransform.anchoredPosition = local;
                float k = p.t / p.life;
                var c = p.color; c.a = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f;
                p.text.color = c;
                float s = p.t < 0.15f ? Mathf.Lerp(0.6f, 1.15f, p.t / 0.15f) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((p.t - 0.15f) / 0.2f));
                p.text.rectTransform.localScale = Vector3.one * s;
            }
        }
    }
}
