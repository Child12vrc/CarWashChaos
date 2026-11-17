using System.Collections.Generic;
using CarWash.Player;
using UnityEngine;
using UnityEngine.UI;

namespace CarWash.UI
{
    /// <summary>T4.4: draws live pings (ico_ping in the pinger's colour + label + distance), clamped to the screen edge.</summary>
    public class PingMarkers : MonoBehaviour
    {
        class Marker { public RectTransform root; public Image icon; public Text label; public PlayerPing.Ping ping; }

        RectTransform _root;
        readonly Dictionary<ulong, Marker> _byClient = new Dictionary<ulong, Marker>();
        public int Active { get; private set; }
        public string LastLabel { get; private set; }

        public void Build(Transform root) => _root = (RectTransform)root;

        void OnEnable() => PlayerPing.Pinged += OnPing;
        void OnDisable() => PlayerPing.Pinged -= OnPing;

        void OnPing(PlayerPing.Ping p)
        {
            if (!_byClient.TryGetValue(p.client, out var m))
            {
                var rt = UiFactory.Rect("Ping" + p.client, _root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 64));
                var icon = UiFactory.Panel("Icon", rt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(52, 52), Color.white);
                icon.preserveAspect = true; icon.raycastTarget = false;
                var sp = UiFactory.Skin != null ? UiFactory.Skin.Icon("ico_ping") : null;
                if (sp != null) icon.sprite = sp;
                var label = UiFactory.Label("Label", rt, "", 22, new Vector2(0.5f, 0f), new Vector2(0, -14), new Vector2(260, 30));
                label.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.85f);
                m = new Marker { root = rt, icon = icon, label = label };
                _byClient[p.client] = m;
            }
            m.ping = p;
            m.icon.color = PlayerNet.Colors[p.color % PlayerNet.Colors.Length];
            LastLabel = p.label;
        }

        void LateUpdate()
        {
            var cam = PlayerLook.Local != null ? PlayerLook.Local.Camera : Camera.main;
            int n = 0;
            foreach (var m in _byClient.Values)
            {
                bool live = Time.time < m.ping.until && cam != null;
                m.root.gameObject.SetActive(live);
                if (!live) continue;
                n++;
                var sp = cam.WorldToScreenPoint(m.ping.pos + Vector3.up * 0.3f);
                if (sp.z < 0f) { sp.x = Screen.width - sp.x; sp.y = 0f; } // behind → bottom edge
                sp.x = Mathf.Clamp(sp.x, 40f, Screen.width - 40f);
                sp.y = Mathf.Clamp(sp.y, 60f, Screen.height - 40f);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, sp, null, out var local);
                m.root.anchoredPosition = local;
                float d = Vector3.Distance(cam.transform.position, m.ping.pos);
                m.label.text = $"{m.ping.label} {d:0}m";
                float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 8f);
                m.icon.rectTransform.localScale = Vector3.one * pulse;
            }
            Active = n;
        }
    }
}
