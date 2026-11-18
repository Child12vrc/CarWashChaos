using System.Collections;
using CarWash.Player;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// M4.7 E4: before / after environment screenshots (host alone: -autotest -autotest-views LABEL). Three spots at player eye
    /// height — the open lot, the north-east corner by the fence, the road by the exit — each in 8 directions (N, NE … NW),
    /// HUD hidden. Files: views_LABEL_spot_deg.png in the player's data folder.
    /// </summary>
    public static class DevChecksViews
    {
        public static string Label
        {
            get
            {
                var a = System.Environment.GetCommandLineArgs();
                int i = System.Array.IndexOf(a, "-autotest-views");
                return i < 0 ? null : i + 1 < a.Length && !a[i + 1].StartsWith("-") ? a[i + 1] : "now";
            }
        }

        public static readonly (string name, Vector3 pos)[] Spots =
        {
            ("lot", new Vector3(2f, 0.05f, -8f)),
            ("corner", new Vector3(13f, 0.05f, 10f)),
            ("road", new Vector3(24f, 0.05f, -13.5f)),
        };

        public static IEnumerator Run(PlayerNet me)
        {
            var input = me.GetComponent<PlayerInputReader>();
            input.scripted = true;
            yield return new WaitForSeconds(3f);
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in canvases) c.enabled = false;
            foreach (var (name, pos) in Spots)
            {
                me.Motor.Teleport(pos);
                yield return new WaitForSeconds(0.6f);
                for (int deg = 0; deg < 360; deg += 45)
                {
                    me.Look.SetYaw(deg);
                    me.Look.SetPitch(6f);
                    yield return new WaitForSeconds(0.35f);
                    ScreenCapture.CaptureScreenshot($"views_{Label}_{name}_{deg:000}.png");
                    yield return null;
                }
            }
            foreach (var c in canvases) if (c) c.enabled = true;
            DevAutopilot.Log($"views: {Spots.Length * 8} screenshots ({Label})");
            input.scripted = false;
        }
    }
}
