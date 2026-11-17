using CarWash.Core;
using CarWash.Data;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Player
{
    /// <summary>
    /// First-person look (TECH S02). Owner: yaw on body, pitch on head; camera + listener exist only for the owner.
    /// Pitch is replicated so other players see where you look.
    /// </summary>
    public class PlayerLook : NetworkBehaviour
    {
        [SerializeField] private DataRegistry _data;
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private PlayerMotor _motor;
        [SerializeField] private Transform _head;
        [SerializeField] private Camera _camera;
        [SerializeField] private float _maxPitch = 85f;

        readonly NetworkVariable<float> _pitch = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        float _yaw, _localPitch;
        public Camera Camera => _camera;
        public Transform Head => _head;
        public float Pitch => IsOwner ? _localPitch : _pitch.Value;
        public static PlayerLook Local { get; private set; }

        public override void OnNetworkSpawn()
        {
            bool own = IsOwner;
            _camera.enabled = own;
            var listener = _camera.GetComponent<AudioListener>();
            if (listener) listener.enabled = own;
            if (!own) return;

            Local = this;
            _input.Enable();
            _yaw = transform.eulerAngles.y;
            var settings = Services.Get<Settings>();
            _camera.fieldOfView = settings != null ? settings.Fov : _data.balance.player.fov;
            // P2: brightness (exposure) lives in a post-processing volume → the player camera must render post
            var urp = _camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (urp != null) urp.renderPostProcessing = true;
            SetCursorLocked(true);
            foreach (var other in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (other != _camera && other.CompareTag("MainCamera")) other.gameObject.SetActive(false);
        }

        public override void OnNetworkDespawn()
        {
            if (Local == this) Local = null;
        }

        public void SetYaw(float yaw)
        {
            _yaw = yaw;
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        public void SetPitch(float pitch) => _localPitch = Mathf.Clamp(pitch, -_maxPitch, _maxPitch);

        /// <summary>Owner: turn to look at a world point (tests / autopilot).</summary>
        public void LookAt(Vector3 point)
        {
            Vector3 d = point - _head.position;
            SetYaw(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
            SetPitch(-Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg);
        }

        public static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Update()
        {
            if (IsSpawned && !IsOwner)
            {
                _head.localRotation = Quaternion.Euler(_pitch.Value, 0f, 0f);
                return;
            }
            if (!IsSpawned) return;

            if (_input.PausePressed) UI.SettingsMenu.Toggle();   // P2: Esc = settings / pause menu (unlocks the cursor)
            else if (!UI.SettingsMenu.IsOpen && Cursor.lockState != CursorLockMode.Locked && Input_ClickedToRelock()) SetCursorLocked(true);

            var settings = Services.Get<Settings>();
            float sens = _data.balance.player.lookSensitivity * (settings != null ? settings.Sensitivity : 1f);
            _yaw += _input.Look.x * sens;
            float invert = settings != null && settings.InvertY ? -1f : 1f;
            _localPitch = Mathf.Clamp(_localPitch - _input.Look.y * sens * invert, -_maxPitch, _maxPitch);
            if (settings != null && !Mathf.Approximately(_camera.fieldOfView, settings.Fov)) _camera.fieldOfView = settings.Fov;

            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            _head.localPosition = new Vector3(0f, Mathf.Lerp(_head.localPosition.y, _motor.EyeHeight, 1f - Mathf.Exp(-12f * Time.deltaTime)), 0f);
            _head.localRotation = Quaternion.Euler(_localPitch, 0f, 0f);
            if (Mathf.Abs(_pitch.Value - _localPitch) > 0.5f) _pitch.Value = _localPitch;
            UpdateCameraFx(settings);
        }

        // ---------------- P2 camera comfort: head bob + screen shake (both scaled/disabled by settings) ----------------
        float _bobT, _trauma;
        Vector3 _lastPos;
        public float Trauma => _trauma;

        /// <summary>Owner: add screen shake (0..1 trauma; stings, knockdowns). Scaled by Settings.ShakeStrength, 0 = off.</summary>
        public void AddShake(float trauma) => _trauma = Mathf.Clamp01(_trauma + trauma);

        void UpdateCameraFx(Settings settings)
        {
            float dt = Time.deltaTime;
            var cam = _camera.transform;
            // head bob: only when walking on the ground; amplitude follows speed
            var v = (transform.position - _lastPos) / Mathf.Max(1e-4f, dt); v.y = 0f;
            _lastPos = transform.position;
            float speed = v.magnitude;
            bool bob = settings == null || settings.HeadBob;
            Vector3 bobOffset = Vector3.zero;
            if (bob && _motor != null && _motor.Grounded && speed > 0.5f && speed < 12f)
            {
                _bobT += dt * Mathf.Lerp(6f, 11f, Mathf.InverseLerp(2f, 7f, speed));
                float a = Mathf.Lerp(0.015f, 0.035f, Mathf.InverseLerp(2f, 7f, speed));
                bobOffset = new Vector3(Mathf.Cos(_bobT) * a * 0.5f, Mathf.Abs(Mathf.Sin(_bobT)) * a, 0f);
            }
            cam.localPosition = Vector3.Lerp(cam.localPosition, bobOffset, 1f - Mathf.Exp(-15f * dt));
            // shake: trauma² noise, decays ~1.2/s
            float strength = settings != null ? settings.ShakeStrength : 0.6f;
            _trauma = Mathf.Max(0f, _trauma - dt * 1.2f);
            float s = _trauma * _trauma * strength;
            float t = Time.time * 25f;
            cam.localRotation = s > 0.0001f
                ? Quaternion.Euler((Mathf.PerlinNoise(t, 0.1f) - 0.5f) * 6f * s, (Mathf.PerlinNoise(0.3f, t) - 0.5f) * 6f * s, (Mathf.PerlinNoise(t, t) - 0.5f) * 8f * s)
                : Quaternion.identity;
        }

        static bool Input_ClickedToRelock()
        {
            var m = UnityEngine.InputSystem.Mouse.current;
            return m != null && m.leftButton.wasPressedThisFrame;
        }

        /// <summary>Aim ray from the eye (owner only).</summary>
        public Ray AimRay => new Ray(_camera.transform.position, _camera.transform.forward);
    }
}
