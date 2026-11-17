using CarWash.Player;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Day
{
    /// <summary>
    /// Day board on the office wall (GDD §11). E: start the day from the lobby, or continue after a summary / game over /
    /// week end (same as Enter on the summary panel). Any player may use it (co-op; DESIGN-GAP: no host-only gate).
    /// </summary>
    public class DayBoard : NetworkBehaviour, IInteractable
    {
        public string Prompt => DayManager.Instance != null ? DayManager.Instance.AdvancePrompt ?? "" : "";

        public bool CanInteract(PlayerNet player) => DayManager.Instance != null && DayManager.Instance.AdvancePrompt != null;

        public void Interact(PlayerNet player)
        {
            Debug.Log($"[DAY] day board used by client {player.OwnerClientId} in {DayManager.Instance?.State.Value}");
            DayManager.Instance?.ServerAdvance();
        }
    }
}
