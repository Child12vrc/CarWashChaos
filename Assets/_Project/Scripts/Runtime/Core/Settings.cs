using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CarWash.Core
{
    /// <summary>
    /// P2: local player settings in PlayerPrefs (key prefix "cw."). Setters clamp + save + raise Changed; Apply() pushes
    /// the engine-wide ones (brightness = URP ColorAdjustments.postExposure on a global volume, master volume, quality,
    /// resolution/fullscreen, vsync). Look/camera settings are read live by PlayerLook; key bindings by PlayerInputReader.
    /// </summary>
    public class Settings
    {
        public event Action Changed;

        static float F(string k, float d) => PlayerPrefs.GetFloat("cw." + k, d);
        static int I(string k, int d) => PlayerPrefs.GetInt("cw." + k, d);
        void SetF(string k, float v) { PlayerPrefs.SetFloat("cw." + k, v); Dirty(); }
        void SetI(string k, int v) { PlayerPrefs.SetInt("cw." + k, v); Dirty(); }
        void Dirty() { PlayerPrefs.Save(); Changed?.Invoke(); }

        // ---- screen
        /// <summary>Brightness as exposure in EV, -2..+2 (0 = neutral).</summary>
        public float Brightness { get => F("bright", 0f); set => SetF("bright", Mathf.Clamp(value, -2f, 2f)); }
        public float Fov { get => F("fov", 90f); set => SetF("fov", Mathf.Clamp(value, 60f, 110f)); }
        public int Quality { get => Mathf.Clamp(I("quality", QualitySettings.GetQualityLevel()), 0, Mathf.Max(0, QualitySettings.names.Length - 1)); set => SetI("quality", Mathf.Clamp(value, 0, Mathf.Max(0, QualitySettings.names.Length - 1))); }
        /// <summary>Index into Screen.resolutions (-1 = keep current).</summary>
        public int ResolutionIndex { get => I("res", -1); set => SetI("res", value); }
        public bool Fullscreen { get => I("full", Screen.fullScreen ? 1 : 0) == 1; set => SetI("full", value ? 1 : 0); }
        public bool VSync { get => I("vsync", 1) == 1; set => SetI("vsync", value ? 1 : 0); }

        // ---- sound (0..1)
        public float MasterVolume { get => F("vol.master", 0.8f); set => SetF("vol.master", Mathf.Clamp01(value)); }
        public float MusicVolume { get => F("vol.music", 0.7f); set => SetF("vol.music", Mathf.Clamp01(value)); }
        public float SfxVolume { get => F("vol.sfx", 0.9f); set => SetF("vol.sfx", Mathf.Clamp01(value)); }
        public float VoiceVolume { get => F("vol.voice", 1f); set => SetF("vol.voice", Mathf.Clamp01(value)); }

        // ---- controls / comfort
        public float Sensitivity { get => F("sens", 1f); set => SetF("sens", Mathf.Clamp(value, 0.1f, 5f)); }
        public bool InvertY { get => I("invertY", 0) == 1; set => SetI("invertY", value ? 1 : 0); }
        /// <summary>Screen shake strength 0..1 (0 = off).</summary>
        public float ShakeStrength { get => F("shake", 0.6f); set => SetF("shake", Mathf.Clamp01(value)); }
        public bool HeadBob { get => I("headBob", 1) == 1; set => SetI("headBob", value ? 1 : 0); }
        /// <summary>M4.6 D2: HUD size multiplier (0.6 small … 1.3 large). Default 0.8: the old HUD felt too big.</summary>
        public float UiScale { get => F("uiScale", 0.8f); set => SetF("uiScale", Mathf.Clamp(value, 0.6f, 1.3f)); }
        /// <summary>M4.6 D1: the first-run tutorial was finished or skipped.</summary>
        public bool TutorialDone { get => I("tutorialDone", 0) == 1; set => SetI("tutorialDone", value ? 1 : 0); }
        /// <summary>M4.6 D1: how often a contextual hint was shown (each one at most a few times).</summary>
        public int HintCount(string id) => I("hint." + id, 0);
        public void CountHint(string id) => SetI("hint." + id, HintCount(id) + 1);
        /// <summary>Input System binding overrides (JSON), "" = defaults.</summary>
        public string Bindings { get => PlayerPrefs.GetString("cw.bindings", ""); set { PlayerPrefs.SetString("cw.bindings", value ?? ""); Dirty(); } }

        // legacy accessor kept for older callers
        public float Volume { get => MasterVolume; set => MasterVolume = value; }

        public void Save() => PlayerPrefs.Save();

        public void ResetAll()
        {
            foreach (var k in new[] { "bright", "fov", "quality", "res", "full", "vsync", "vol.master", "vol.music", "vol.sfx", "vol.voice", "sens", "invertY", "shake", "headBob", "bindings", "uiScale" })
                PlayerPrefs.DeleteKey("cw." + k);
            Dirty();
        }

        // ---------------------------------------------------------------- engine side
        Volume _volume;
        ColorAdjustments _exposure;
        public float AppliedExposure => _exposure != null ? _exposure.postExposure.value : float.NaN;

        /// <summary>Push engine-wide settings (call at boot and on Changed).</summary>
        public void Apply()
        {
            EnsureVolume();
            _exposure.postExposure.Override(Brightness);
            AudioListener.volume = MasterVolume;
            if (QualitySettings.GetQualityLevel() != Quality) QualitySettings.SetQualityLevel(Quality, true);
            if (Application.targetFrameRate <= 0) QualitySettings.vSyncCount = VSync ? 1 : 0; // a dev -fps cap wins
            var res = Screen.resolutions;
            int ri = ResolutionIndex;
            if (!Application.isEditor && ri >= 0 && ri < res.Length)
            {
                var r = res[ri];
                if (Screen.width != r.width || Screen.height != r.height || Screen.fullScreen != Fullscreen)
                    Screen.SetResolution(r.width, r.height, Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            }
            else if (!Application.isEditor && Screen.fullScreen != Fullscreen) Screen.fullScreen = Fullscreen;
        }

        void EnsureVolume()
        {
            if (_volume != null) return;
            var go = new GameObject("SettingsVolume (brightness)");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 100f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _exposure = profile.Add<ColorAdjustments>(true);
            _volume.sharedProfile = profile;
        }
    }
}
