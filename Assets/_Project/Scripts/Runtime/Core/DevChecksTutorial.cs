using System.Collections;
using CarWash.Player;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// M4.6 D1/D2 check (client: -autotest -autotest-release -autotest-tutorial). Starts the first-run tutorial, then the normal
    /// release loop plays the game (ready → day → washer → wash → payout) while this watcher logs every tutorial step as it
    /// completes (screenshot each); afterwards: Esc → 도움말 tab screenshot, HUD at UI scale 60 % / 120 %.
    /// </summary>
    public static class DevChecksTutorial
    {
        public static bool Requested => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-autotest-tutorial") >= 0;
        static void Log(string m) => DevAutopilot.Log("tutorial: " + m);

        public static IEnumerator Watch()
        {
            var tut = UI.TutorialPanel.Instance;
            if (tut == null) { Log("no TutorialPanel"); yield break; }
            tut.DevStart();
            int last = -2; float t0 = Time.time;
            while (Time.time - t0 < 240f)
            {
                if (tut.StepIndex != last)
                {
                    Log($"step {tut.StepIndex + 1} at {Time.time - t0:0}s: '{tut.Text}'");
                    ScreenCapture.CaptureScreenshot($"autotest_tutorial_step{tut.StepIndex + 1}.png");
                    if (tut.StepIndex < 0 && last >= 0) { Log($"finished, TutorialDone={Services.Get<Settings>().TutorialDone}"); yield break; }
                    last = tut.StepIndex;
                }
                yield return null;
            }
            Log($"timed out at step {tut.StepIndex + 1}");
        }

        public static IEnumerator HelpAndScale()
        {
            var s = Services.Get<Settings>();
            UI.SettingsMenu.Instance.SetOpen(true);
            UI.SettingsMenu.Instance.ShowTab(4);
            yield return new WaitForSeconds(0.5f);
            ScreenCapture.CaptureScreenshot("autotest_help.png");
            yield return null;
            UI.SettingsMenu.Instance.ShowTab(0);
            yield return new WaitForSeconds(0.3f);
            ScreenCapture.CaptureScreenshot("autotest_settings_screen.png");
            yield return null;
            UI.SettingsMenu.Instance.SetOpen(false);
            float keep = s.UiScale;
            foreach (float k in new[] { 0.6f, 1.2f })
            {
                s.UiScale = k;
                yield return new WaitForSeconds(0.5f);
                var scaler = UI.Hud.Instance.Root.GetComponent<UnityEngine.UI.CanvasScaler>();
                Log($"UI scale {k:0.0}: canvas reference {scaler.referenceResolution}");
                ScreenCapture.CaptureScreenshot($"autotest_uiscale_{Mathf.RoundToInt(k * 100)}.png");
                yield return null;
            }
            s.UiScale = keep;
        }
    }
}
