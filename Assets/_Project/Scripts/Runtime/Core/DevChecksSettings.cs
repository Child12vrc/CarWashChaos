using UnityEngine.InputSystem;
using System.Collections;
using System.Linq;
using CarWash.Player;
using CarWash.Tools;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// P2 client check (-autotest-settings): Esc menu open/close + cursor, every setting changes the engine (exposure,
    /// FOV, invert Y, sensitivity, listener volume, shake off/on, head bob off/on, quality), persists in PlayerPrefs,
    /// key rebinding takes effect, unstuck (server-validated, cooldown, drops held tool). Restores defaults at the end.
    /// </summary>
    public static class DevChecksSettings
    {
        static void Log(string m) => DevAutopilot.Log("P2 " + m);

        public static IEnumerator Run(PlayerNet me)
        {
            var input = me.GetComponent<PlayerInputReader>();
            var look = me.Look;
            var s = Services.Get<Settings>();
            var menu = UI.Hud.Instance.Menu;
            input.scripted = true;
            yield return new WaitForSeconds(0.5f);

            // menu
            UI.SettingsMenu.Toggle();
            yield return null;
            Log($"menu open={UI.SettingsMenu.IsOpen} cursor={Cursor.lockState}");
            ScreenCapture.CaptureScreenshot("autotest_settings_screen.png");
            yield return new WaitForSeconds(0.3f);
            menu.ShowTab(3);
            yield return new WaitForSeconds(0.2f);
            ScreenCapture.CaptureScreenshot("autotest_settings_keys.png");
            yield return new WaitForSeconds(0.3f);
            menu.ShowTab(0);

            // screen
            s.Brightness = 1f; s.Fov = 75f;
            yield return null; yield return null;
            Log($"brightness 1 → exposure {s.AppliedExposure:0.0}; fov 75 → camera {look.Camera.fieldOfView:0}");
            int q0 = s.Quality; int qn = QualitySettings.names.Length;
            s.Quality = (q0 + 1) % qn;
            yield return null;
            Log($"quality {q0}→{s.Quality} applied={QualitySettings.GetQualityLevel() == s.Quality} ({QualitySettings.names[QualitySettings.GetQualityLevel()]})");
            s.Quality = q0;

            // sound
            s.MasterVolume = 0.3f;
            yield return null;
            Log($"master 0.3 → AudioListener.volume {AudioListener.volume:0.00}; sfx/music/voice stored {s.SfxVolume:0.0}/{s.MusicVolume:0.0}/{s.VoiceVolume:0.0}");

            // controls: invert Y + sensitivity (scripted mouse moves up)
            UI.SettingsMenu.Toggle(); // menu closed so look works normally
            look.SetPitch(0f);
            s.InvertY = false; s.Sensitivity = 1f;
            input.sLook = new Vector2(0f, 10f); yield return null; input.sLook = Vector2.zero; yield return null;
            float p1 = look.Pitch;
            look.SetPitch(0f);
            s.InvertY = true; s.Sensitivity = 2f;
            input.sLook = new Vector2(0f, 10f); yield return null; input.sLook = Vector2.zero; yield return null;
            float p2 = look.Pitch;
            Log($"mouse up: normal pitch {p1:0.0}°, inverted×2 sens pitch {p2:0.0}° → opposite={Mathf.Sign(p1) != Mathf.Sign(p2)}, ratio {Mathf.Abs(p2 / Mathf.Max(0.001f, Mathf.Abs(p1))):0.0}");
            s.InvertY = false; s.Sensitivity = 1f; look.SetPitch(0f);

            // shake
            s.ShakeStrength = 0f; look.AddShake(1f); yield return null; yield return null;
            float rotOff = Quaternion.Angle(look.Camera.transform.localRotation, Quaternion.identity);
            s.ShakeStrength = 1f; look.AddShake(1f); yield return null; yield return null;
            float rotOn = Quaternion.Angle(look.Camera.transform.localRotation, Quaternion.identity);
            Log($"shake strength 0 → {rotOff:0.00}°, strength 1 → {rotOn:0.00}°");
            yield return new WaitForSeconds(1.2f);

            // head bob while walking
            me.Motor.Teleport(new Vector3(0f, 0.05f, -9f)); look.SetYaw(90f);
            yield return new WaitForSeconds(0.3f);
            s.HeadBob = false; float bobOff = 0f;
            input.sMove = new Vector2(0f, 1f);
            for (float t = 0; t < 1f; t += Time.deltaTime) { bobOff = Mathf.Max(bobOff, look.Camera.transform.localPosition.magnitude); yield return null; }
            s.HeadBob = true; float bobOn = 0f;
            for (float t = 0; t < 1f; t += Time.deltaTime) { bobOn = Mathf.Max(bobOn, look.Camera.transform.localPosition.magnitude); yield return null; }
            input.sMove = Vector2.zero;
            Log($"head bob off → max cam offset {bobOff * 100f:0.0} cm, on → {bobOn * 100f:0.0} cm");

            // persistence
            Log($"PlayerPrefs: fov {PlayerPrefs.GetFloat("cw.fov", -1):0} bright {PlayerPrefs.GetFloat("cw.bright", -9):0.0} vol {PlayerPrefs.GetFloat("cw.vol.master", -1):0.0}");

            // key rebinding: Interact E → R
            bool ok = menu.DevRebind("Interact", null, "<Keyboard>/r");
            yield return null;
            var act = input.Asset.FindAction("Interact");
            int bi = UI.SettingsMenu.BindingIndex(act, null);
            Log($"rebind Interact → R ok={ok}: shows '{menu.BindingText("Interact", null)}', effective path {act.bindings[bi].effectivePath}, saved json has /r={s.Bindings.Contains("/r")}");

            // unstuck: stand inside the office block holding the office towel
            var towel = Object.FindObjectsByType<Towel>(FindObjectsSortMode.None).Where(t => !t.IsHeld).OrderBy(t => (t.transform.position - new Vector3(-7.4f, 1f, -8f)).sqrMagnitude).FirstOrDefault();
            if (towel != null)
            {
                act.ApplyBindingOverride(bi, "<Keyboard>/e"); // E again for the scripted interact
                me.Motor.Teleport(new Vector3(towel.transform.position.x, 0.05f, towel.transform.position.z) + new Vector3(0f, 0f, -1.2f));
                yield return new WaitForSeconds(0.3f);
                look.LookAt(towel.transform.position); yield return new WaitForSeconds(0.2f);
                input.sInteract = true;
                float tw = Time.time; while (towel.HolderPlayer != me && Time.time - tw < 3f) yield return null;
            }
            var inside = new Vector3(-12f, 0.05f, -10f); // middle of the office box
            me.Motor.Teleport(inside);
            yield return new WaitForSeconds(0.5f);
            bool stuckRoom = Unstuck.HasRoom(me.transform.position);
            me.RequestUnstuck();
            float tu = Time.time; while (string.IsNullOrEmpty(me.LastUnstuckResult) && Time.time - tu < 4f) yield return null;
            yield return new WaitForSeconds(0.3f);
            var after = me.transform.position;
            Log($"unstuck: inside office (room={stuckRoom}) → '{me.LastUnstuckResult}' moved {Vector3.Distance(inside, after):0.0} m to {after:F1} room={Unstuck.HasRoom(after)}; towel held={(towel != null && towel.HolderPlayer == me)}; cooldown {me.UnstuckCooldownLeft:0}s");
            me.RequestUnstuck();
            tu = Time.time; while (me.LastUnstuckResult == "" && Time.time - tu < 4f) yield return null;
            Log($"unstuck again → '{me.LastUnstuckResult}', position unchanged={Vector3.Distance(after, me.transform.position) < 0.2f}");

            // restore defaults (dev builds share PlayerPrefs with the player's copy)
            s.ResetAll();
            PlayerInputReader.RaiseBindingsChanged();
            yield return null;
            Log($"reset → fov {s.Fov:0}, exposure {s.AppliedExposure:0.0}, Interact '{menu.BindingText("Interact", null)}', menu open={UI.SettingsMenu.IsOpen}, cursor={Cursor.lockState}");
            input.scripted = false;
        }
    }
}
