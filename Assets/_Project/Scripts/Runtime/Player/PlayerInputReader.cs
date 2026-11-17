using UnityEngine.InputSystem;
using CarWash.Core;
using UnityEngine;

namespace CarWash.Player
{
    /// <summary>
    /// Owner-only input snapshot read once per frame from Controls. Scripted mode (tests / autopilot) overrides values.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PlayerInputReader : MonoBehaviour
    {
        public Vector2 Move, Look;
        public bool Sprint, Crouch, JumpPressed, UseHeld, AltPressed, InteractPressed, GrabHeld, GrabPressed, GrabReleased, DropPressed;
        public int SlotPressed = -1;
        public bool PausePressed, DebugPressed, PingPressed;

        [Header("Scripted (tests / autopilot)")]
        public bool scripted;
        public Vector2 sMove, sLook;
        public bool sSprint, sCrouch, sJump, sUse, sGrab, sInteract, sAlt, sDrop;
        public int sSlot = -1;

        Controls _c;
        bool _prevGrab;

        public void Enable()
        {
            if (_c != null) return;
            _c = new Controls();
            ApplyBindings();
            BindingsChanged += ApplyBindings;
            _c.Player.Enable();
        }

        // ---- P2 key rebinding: Settings.Bindings holds the Input System override JSON
        public static event System.Action BindingsChanged;
        public static void RaiseBindingsChanged() => BindingsChanged?.Invoke();
        public UnityEngine.InputSystem.InputActionAsset Asset => _c?.asset;

        void ApplyBindings()
        {
            if (_c == null) return;
            _c.asset.RemoveAllBindingOverrides();
            var s = Services.Get<Settings>();
            if (s != null && !string.IsNullOrEmpty(s.Bindings)) _c.asset.LoadBindingOverridesFromJson(s.Bindings);
        }

        void OnDestroy()
        {
            BindingsChanged -= ApplyBindings;
            _c?.Dispose();
            _c = null;
        }

        void Update()
        {
            if (scripted)
            {
                Move = Vector2.ClampMagnitude(sMove, 1f); Look = sLook; Sprint = sSprint; Crouch = sCrouch;
                JumpPressed = sJump; sJump = false; UseHeld = sUse;
                GrabPressed = sGrab && !_prevGrab; GrabReleased = !sGrab && _prevGrab; GrabHeld = sGrab; _prevGrab = sGrab;
                InteractPressed = sInteract; sInteract = false;
                AltPressed = sAlt; sAlt = false;
                DropPressed = sDrop; sDrop = false;
                SlotPressed = sSlot; sSlot = -1;
                PausePressed = DebugPressed = PingPressed = false;
                return;
            }
            if (_c == null) return;
            var p = _c.Player;
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            Move = Vector2.ClampMagnitude(p.Move.ReadValue<Vector2>(), 1f);
            Look = locked ? p.Look.ReadValue<Vector2>() : Vector2.zero;
            Sprint = p.Sprint.IsPressed();
            Crouch = p.Crouch.IsPressed();
            JumpPressed = p.Jump.WasPressedThisFrame();
            UseHeld = locked && p.UsePrimary.IsPressed();
            AltPressed = locked && p.AltUse.WasPressedThisFrame();
            InteractPressed = p.Interact.WasPressedThisFrame();
            GrabHeld = p.Grab.IsPressed(); GrabPressed = p.Grab.WasPressedThisFrame(); GrabReleased = p.Grab.WasReleasedThisFrame();
            DropPressed = p.Drop.WasPressedThisFrame();
            SlotPressed = p.Slot1.WasPressedThisFrame() ? 0 : p.Slot2.WasPressedThisFrame() ? 1 : p.Slot3.WasPressedThisFrame() ? 2 : p.Slot4.WasPressedThisFrame() ? 3 : -1;
            PausePressed = p.Pause.WasPressedThisFrame();
            DebugPressed = p.DebugOverlay.WasPressedThisFrame();
            PingPressed = p.Ping.WasPressedThisFrame();
        }
    }
}
