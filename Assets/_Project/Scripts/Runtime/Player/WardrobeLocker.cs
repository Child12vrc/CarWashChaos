using UnityEngine;

namespace CarWash.Player
{
    /// <summary>M4.9 G3: a staff locker — [E] opens the wardrobe (local UI: preview + look / colour). Scene object, no networking.</summary>
    public class WardrobeLocker : MonoBehaviour, ILocalInteractable
    {
        public string Prompt => "옷장 열기 (외형·색 바꾸기)";
        public bool CanInteract(PlayerNet player) => !UI.WardrobeUI.IsOpen;
        public void Interact(PlayerNet player) { }   // never sent to the server
        public void InteractLocal(PlayerNet me) => UI.WardrobeUI.Open();
    }
}
