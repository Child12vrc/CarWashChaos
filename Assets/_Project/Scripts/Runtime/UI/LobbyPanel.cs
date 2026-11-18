using System.Collections.Generic;
using System.Linq;
using CarWash.Core;
using CarWash.Day;
using CarWash.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CarWash.UI
{
    /// <summary>
    /// T4.1 lobby (in the lot, GameState.Lobby): everyone who joined, their colour and ready state. R = ready toggle,
    /// C = next free colour (server-validated). The day board starts the day once every player is ready.
    /// </summary>
    public class LobbyPanel : MonoBehaviour
    {
        Image _panel;
        Text _title, _hint;
        readonly List<(Image swatch, Text label)> _rows = new List<(Image, Text)>();

        public string Text => _panel != null && _panel.gameObject.activeSelf
            ? _title.text + " | " + string.Join(" | ", _rows.Where(r => r.label.gameObject.activeSelf).Select(r => r.label.text)) : null;

        public void Build(Transform root)
        {
            _panel = UiFactory.FramePanel("Lobby", root, new Vector2(0f, 0.5f), new Vector2(230, 60), new Vector2(420, 330), new Color(0.1f, 0.15f, 0.3f, 0.9f));
            var skin = UiFactory.Skin;
            var txt = skin != null ? skin.panelText : Color.white;
            _title = UiFactory.Label("Title", _panel.transform, "대기실", 30, new Vector2(0.5f, 1f), new Vector2(0, -40), new Vector2(380, 40), color: txt);
            for (int i = 0; i < 4; i++)
            {
                var sw = UiFactory.Panel("Swatch" + i, _panel.transform, new Vector2(0f, 1f), new Vector2(46, -92 - i * 48), new Vector2(28, 28), Color.gray);
                var lb = UiFactory.Label("Row" + i, _panel.transform, "", 24, new Vector2(0f, 1f), new Vector2(230, -92 - i * 48), new Vector2(330, 36), TextAnchor.MiddleLeft, txt);
                lb.resizeTextForBestFit = true; lb.resizeTextMinSize = 14; lb.resizeTextMaxSize = 24;
                _rows.Add((sw, lb));
            }
            _hint = UiFactory.Label("Hint", _panel.transform, "[R] 준비 · 옷 갈아입기: 사물함 [E] / Esc → 옷장\n모두 준비되면 일정판 [E] 영업 시작", 20, new Vector2(0.5f, 0f), new Vector2(0, 42), new Vector2(390, 56), color: new Color(1f, 0.85f, 0.3f));
            _panel.gameObject.SetActive(false);
        }

        void Update()
        {
            var dm = DayManager.Instance;
            bool show = dm != null && dm.State.Value == GameState.Lobby;
            _panel.gameObject.SetActive(show);
            if (!show) return;
            var me = PlayerLook.Local != null ? PlayerLook.Local.GetComponent<PlayerNet>() : null;
            var players = FindObjectsByType<PlayerNet>(FindObjectsSortMode.None).Where(p => p.IsSpawned).OrderBy(p => p.OwnerClientId).ToArray();
            _title.text = $"대기실 ({players.Length}/4) — 준비 {players.Count(p => p.Ready.Value)}/{players.Length}";
            for (int i = 0; i < _rows.Count; i++)
            {
                bool has = i < players.Length;
                _rows[i].swatch.gameObject.SetActive(has);
                _rows[i].label.gameObject.SetActive(has);
                if (!has) continue;
                var p = players[i];
                int ci = Mathf.Max(0, p.ColorIndex.Value) % PlayerNet.Colors.Length;
                _rows[i].swatch.color = PlayerNet.Colors[ci];
                _rows[i].label.text = $"{p.DisplayName}{(p == me ? " (나)" : "")} — {(p.Ready.Value ? "준비 완료" : "준비 중")}";
                _rows[i].label.color = p.Ready.Value ? new Color(0.55f, 1f, 0.55f) : new Color(1f, 1f, 1f, 0.8f);
            }
            if (me != null && Keyboard.current != null && Cursor.lockState == CursorLockMode.Locked)
            {
                if (Keyboard.current.rKey.wasPressedThisFrame) me.RequestToggleReady();
            }
        }
    }
}
