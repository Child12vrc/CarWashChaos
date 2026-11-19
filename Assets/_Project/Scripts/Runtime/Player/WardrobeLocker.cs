using UnityEngine;

namespace CarWash.Player
{
    /// <summary>M4.9 G3: a staff locker — [E] opens the wardrobe (local UI: preview + look / colour). Scene object, no networking.
    /// With the Blender locker model (Lot/Locker.glb, `Door` origin = hinge on its −X edge) the door swings open while the
    /// wardrobe is open from this locker, and closes again after.</summary>
    public class WardrobeLocker : MonoBehaviour, ILocalInteractable
    {
        [SerializeField] private Transform _door;
        static WardrobeLocker _opened;
        Quaternion _shut; float _angle;

        public Transform Door => _door;
        public float DoorAngle => _angle;
        public string Prompt => "옷장 열기 (외형·색 바꾸기)";
        public bool CanInteract(PlayerNet player) => !UI.WardrobeUI.IsOpen;
        public void Interact(PlayerNet player) { }   // never sent to the server
        public void InteractLocal(PlayerNet me) { _opened = this; UI.WardrobeUI.Open(); }

        void Awake() { if (_door != null) _shut = _door.localRotation; }

        void Update()
        {
            if (_door == null) return;
            float target = _opened == this && UI.WardrobeUI.IsOpen ? 100f : 0f; // +Y swings the free (+X) edge towards the front (−Z)
            if (target == 0f && _opened == this && _angle <= 0.5f) _opened = null;
            if (Mathf.Approximately(_angle, target)) return;
            _angle = Mathf.MoveTowards(_angle, target, Time.deltaTime * 320f);
            _door.localRotation = _shut * Quaternion.Euler(0f, _angle, 0f);
        }
    }
}
