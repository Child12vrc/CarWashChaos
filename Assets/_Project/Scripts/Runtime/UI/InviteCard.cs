using CarWash.Core;
using CarWash.Day;
using CarWash.Net;
using UnityEngine;
using UnityEngine.UI;

namespace CarWash.UI
{
    /// <summary>
    /// P7: top-right card with what friends need to join — the Relay join code (big, 복사 button) or, in IP mode, this
    /// host's private IP:port. Shown in the lobby and whenever the Esc menu is open (the cursor is free there, so the copy
    /// button can be clicked); hidden during play.
    /// </summary>
    public class InviteCard : MonoBehaviour
    {
        Image _panel;
        Text _title, _value, _hint;
        float _copiedUntil;

        public string Text => _panel != null && _panel.gameObject.activeSelf ? _title.text + " " + _value.text : null;

        public void Build(Transform root)
        {
            var tr = new Vector2(1f, 1f);
            _panel = UiFactory.FramePanel("InviteCard", root, tr, new Vector2(-250, -235), new Vector2(460, 220), new Color(0.1f, 0.15f, 0.3f, 0.92f));
            var p = _panel.transform;
            var txt = UiFactory.Skin != null ? UiFactory.Skin.panelText : Color.white;
            _title = UiFactory.Label("Title", p, "", 24, new Vector2(0.5f, 1f), new Vector2(0, -34), new Vector2(420, 32), color: txt);
            _value = UiFactory.Label("Value", p, "", 64, new Vector2(0.5f, 0.5f), new Vector2(-60, 8), new Vector2(320, 84), color: new Color(1f, 0.85f, 0.25f));
            _value.fontStyle = FontStyle.Bold;
            _value.resizeTextForBestFit = true; _value.resizeTextMinSize = 24; _value.resizeTextMaxSize = 64;
            _value.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.7f);
            UiFactory.Button("Copy", p, "복사", new Vector2(0.5f, 0.5f), new Vector2(160, 8), new Vector2(110, 64), Copy);
            _hint = UiFactory.Label("Hint", p, "", 18, new Vector2(0.5f, 0f), new Vector2(0, 34), new Vector2(430, 44), color: new Color(1f, 1f, 1f, 0.75f));
            _panel.gameObject.SetActive(false);
        }

        public string ValueToCopy => NetSession.Mode == NetSession.ConnectMode.Relay ? NetSession.JoinCode : NetSession.IpAddress;

        public void Copy()
        {
            if (string.IsNullOrEmpty(ValueToCopy)) return;
            GUIUtility.systemCopyBuffer = ValueToCopy;
            _copiedUntil = Time.unscaledTime + 2f;
        }

        void Update()
        {
            var dm = DayManager.Instance;
            bool inLobby = dm != null && dm.State.Value == GameState.Lobby;
            bool show = NetSession.Mode != NetSession.ConnectMode.None && !string.IsNullOrEmpty(ValueToCopy) && (inLobby || SettingsMenu.IsOpen);
            if (_panel.gameObject.activeSelf != show) _panel.gameObject.SetActive(show);
            if (!show) return;
            bool relay = NetSession.Mode == NetSession.ConnectMode.Relay;
            bool host = Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsServer;
            _title.text = relay ? "참가 코드 (친구에게 알려 주세요)" : host ? "내 IP : 포트 (같은 공유기)" : "접속한 주소";
            _value.text = ValueToCopy;
            _hint.text = Time.unscaledTime < _copiedUntil ? "복사했습니다!"
                : SettingsMenu.IsOpen ? "[복사]를 눌러 클립보드에 복사"
                : relay ? "Esc를 누르면 [복사] 버튼을 누를 수 있어요" : "인터넷 너머 친구는 '참가 코드' 방식을 쓰세요";
        }
    }
}
