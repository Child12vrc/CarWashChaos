using System.Collections;
using System.Linq;
using CarWash.Player;
using CarWash.UI;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// M4.9 G3 check (client: -autotest -autotest-release -autotest-wardrobe; host -autotest-release). Walks to a staff locker,
    /// [E] opens the wardrobe (preview screenshot), picks another look (server confirms through the NetworkVariable), tries the
    /// host's colour (refused) and a free one, closes, reopens from the Esc menu, then restores the saved look / colour prefs.
    /// Logged as [AUTOTEST] wardrobe:.
    /// </summary>
    public static class DevChecksWardrobe
    {
        public static bool Requested => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-autotest-wardrobe") >= 0;
        static void Log(string m) => DevAutopilot.Log("wardrobe: " + m);

        public static IEnumerator Run(PlayerNet me)
        {
            var input = me.GetComponent<PlayerInputReader>();
            var app = me.GetComponent<PlayerAppearance>();
            input.scripted = true;
            yield return new WaitForSeconds(3f);
            var locker = Object.FindObjectsByType<WardrobeLocker>(FindObjectsSortMode.None).OrderBy(l => (l.transform.position - me.transform.position).sqrMagnitude).FirstOrDefault();
            if (locker == null) { Log("FAIL no locker"); yield break; }
            var face = locker.transform.position; var n = locker.transform.parent.forward;
            me.Motor.Teleport(new Vector3(face.x, 0.05f, face.z) + n * 1.2f);
            yield return new WaitForSeconds(0.5f);
            me.Look.LookAt(face);
            yield return new WaitForSeconds(0.4f);
            string prompt = me.GetComponent<PlayerInteractor>().PromptText();
            input.sInteract = true;
            float t0 = Time.time; while (!WardrobeUI.IsOpen && Time.time - t0 < 2f) yield return null;
            Log($"locker prompt '{prompt}' → open={WardrobeUI.IsOpen}, cursor {Cursor.lockState} → {(WardrobeUI.IsOpen ? "OK" : "FAIL")}");
            if (!WardrobeUI.IsOpen) { input.scripted = false; yield break; }
            yield return new WaitForSeconds(1.2f);
            ScreenCapture.CaptureScreenshot("autotest_wardrobe_open.png");
            Log($"locker door: model door={(locker.Door != null)} angle {locker.DoorAngle:0} deg while open → {(locker.Door == null || locker.DoorAngle > 90f ? "OK" : "FAIL")}");
            yield return new WaitForSeconds(0.3f);
            var w = WardrobeUI.Instance;
            int before = app.Look.Value, target = (before + 5) % app.Count;
            w.DevPickLook(target);
            t0 = Time.time; while (app.Look.Value != target && Time.time - t0 < 3f) yield return null;
            yield return new WaitForSeconds(1.2f);
            Log($"look {PlayerAppearance.NameOf(app, before)} → {PlayerAppearance.NameOf(app, target)}: server value {app.Look.Value} in {Time.time - t0 - 1.2f:0.00}s, preview {w.PreviewLook}, saved '{PlayerPrefs.GetString(PlayerAppearance.SavedLookKey, "")}' → {(app.Look.Value == target && w.PreviewLook == target ? "OK" : "FAIL")}");
            ScreenCapture.CaptureScreenshot("autotest_wardrobe_changed.png");
            yield return new WaitForSeconds(0.3f);
            int taken = PlayerNet.All.Where(p => p != me).Select(p => p.ColorIndex.Value).FirstOrDefault();
            w.DevPickColor(taken);
            yield return new WaitForSeconds(0.6f);
            Log($"taken colour {PlayerNet.ColorNames[taken]}: '{w.StatusText}', mine still {PlayerNet.ColorNames[me.ColorIndex.Value]} → {(me.ColorIndex.Value != taken ? "OK" : "FAIL")}");
            int free = Enumerable.Range(0, PlayerNet.Colors.Length).First(c => c != me.ColorIndex.Value && !me.ColorTaken(c));
            w.DevPickColor(free);
            t0 = Time.time; while (me.ColorIndex.Value != free && Time.time - t0 < 3f) yield return null;
            Log($"free colour → {PlayerNet.ColorNames[me.ColorIndex.Value]} → {(me.ColorIndex.Value == free ? "OK" : "FAIL")}");
            yield return new WaitForSeconds(0.8f);
            ScreenCapture.CaptureScreenshot("autotest_wardrobe_colour.png");
            yield return new WaitForSeconds(0.3f);
            WardrobeUI.Close();
            yield return null;
            yield return new WaitForSeconds(0.6f);
            Log($"closed: open={WardrobeUI.IsOpen}, cursor {Cursor.lockState}, my body look active={app.Current?.root.activeSelf}");
            Log($"locker door after close: angle {locker.DoorAngle:0} deg");
            // from the Esc menu
            UI.SettingsMenu.Toggle();
            yield return new WaitForSeconds(0.3f);
            bool menu = UI.SettingsMenu.IsOpen;
            WardrobeUI.Open(); // = the menu's 옷장 button
            yield return new WaitForSeconds(0.8f);
            Log($"Esc menu open={menu} → wardrobe open={WardrobeUI.IsOpen}, settings closed={!UI.SettingsMenu.IsOpen} → {(menu && WardrobeUI.IsOpen && !UI.SettingsMenu.IsOpen ? "OK" : "FAIL")}");
            ScreenCapture.CaptureScreenshot("autotest_wardrobe_from_menu.png");
            yield return new WaitForSeconds(0.3f);
            WardrobeUI.Close();
            // do not leave test choices in the shared prefs
            PlayerPrefs.DeleteKey(PlayerAppearance.SavedLookKey); PlayerPrefs.DeleteKey(PlayerNet.SavedColorKey); PlayerPrefs.Save();
            input.scripted = false;
        }
    }
}
