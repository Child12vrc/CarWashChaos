using System.Linq;
using CarWash.Core;
using CarWash.Day;
using CarWash.Economy;
using CarWash.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace CarWash.UI
{
    /// <summary>
    /// T4.5 DebugOverlay (F3): fps, role, RTT, players, state/day/clock, money/rep, the player-count scaling in effect
    /// (arrival interval, critter HP multiplier), live vehicles/critters/tools and the nearest car's dirt sync.
    /// </summary>
    public class DebugOverlay : MonoBehaviour
    {
        Image _panel;
        Text _text;
        float _fps, _nextText;
        public bool Visible => _panel != null && _panel.gameObject.activeSelf;
        public string Text => _text != null ? _text.text : null;

        public void Build(Transform root)
        {
            _panel = UiFactory.Panel("DebugOverlay", root, new Vector2(0f, 1f), new Vector2(240, -330), new Vector2(460, 400), new Color(0, 0, 0, 0.65f));
            _panel.raycastTarget = false;
            _text = UiFactory.Label("Text", _panel.transform, "", 20, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(430, 380), TextAnchor.UpperLeft, new Color(0.7f, 1f, 0.7f));
            _panel.gameObject.SetActive(false);
        }

        public void Toggle() => _panel.gameObject.SetActive(!_panel.gameObject.activeSelf);

        void Update()
        {
            var look = PlayerLook.Local;
            var input = look != null ? look.GetComponent<PlayerInputReader>() : null;
            if (input != null && input.DebugPressed) Toggle();
            _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime), 0.05f);
            if (!Visible || Time.unscaledTime < _nextText) return;
            _nextText = Time.unscaledTime + 0.25f;

            var nm = NetworkManager.Singleton;
            var dm = DayManager.Instance;
            var eco = EconomyState.Instance;
            int players = Object.FindObjectsByType<PlayerNet>(FindObjectsSortMode.None).Length;
            string role = nm == null ? "오프라인" : nm.IsHost ? "호스트" : nm.IsServer ? "서버" : "클라이언트";
            ulong rtt = 0;
            if (nm != null && nm.IsClient && !nm.IsServer && nm.NetworkConfig.NetworkTransport != null)
                rtt = nm.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.ServerClientId);
            var s = new System.Text.StringBuilder();
            s.AppendLine($"[F3] FPS {_fps:0}   {role}   RTT {rtt} ms");
            s.AppendLine($"플레이어 {players}명   상태 {(dm != null ? dm.State.Value.ToString() : "-")}");
            if (dm != null) s.AppendLine($"W{dm.Week.Value} D{dm.DayNum.Value}  {dm.Phase.Value}  남은 {dm.TimeLeft:0}s  임대료 ${dm.CurrentRent}");
            if (eco != null)
            {
                var e = eco.Tuning;
                s.AppendLine($"돈 ${eco.Money.Value}   평판 {eco.Rep.Value:0.00}");
                s.AppendLine($"인원 보정: 손님 간격 {EconomyCalc.ArrivalInterval(eco.Rep.Value, players, e):0.0}s (x{EconomyCalc.PlayerScale(players, e):0.00})");
            }
            var reg = Services.Get<Data.DataRegistry>();
            if (reg != null) s.AppendLine($"동물 HP x{Critters.CritterBrain.HpScale(players, reg.balance.critter.hpScalePerPlayer):0.00}");
            s.AppendLine($"차량 {Object.FindObjectsByType<Vehicles.Vehicle>(FindObjectsSortMode.None).Length}   동물 {Critters.CritterBase.All.Count}   도구 {Object.FindObjectsByType<Tools.ToolBase>(FindObjectsSortMode.None).Length}");
            if (look != null)
            {
                var near = Object.FindObjectsByType<Dirt.DirtNetSync>(FindObjectsSortMode.None).OrderBy(d => (d.transform.position - look.transform.position).sqrMagnitude).FirstOrDefault();
                if (near != null)
                    s.AppendLine($"가까운 차 {near.name.Replace("(Clone)", "")}: 서버 {near.CleanPct.Value * 100f:0.0}% / 로컬 {near.Painter.CleanPct * 100f:0.0}%  기록 {near.HistoryCount}");
                s.AppendLine($"위치 {look.transform.position:F1}");
            }
            _text.text = s.ToString();
        }
    }
}
