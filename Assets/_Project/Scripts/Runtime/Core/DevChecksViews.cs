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

        /// <summary>M4.10: close-ups of the clipping / text problem spots (feet position, look-at point).</summary>
        public static readonly (string name, Vector3 pos, Vector3 look)[] Closeups =
        {
            ("bay1_number", new Vector3(-8f, 0.05f, 12.5f), new Vector3(-8f, 0f, 7.4f)),
            ("slow_paint", new Vector3(-20f, 0.05f, -13.8f), new Vector3(-20f, 0f, -8f)),
            ("price_board", new Vector3(-23.2f, 0.05f, -8.8f), new Vector3(-27f, 2.2f, -8.8f)),
            ("fascia", new Vector3(0f, 0.05f, 15f), new Vector3(0f, 4.1f, 9.6f)),
            ("drain_boom", new Vector3(-8f, 0.05f, 0.6f), new Vector3(-8f, 0.3f, 4f)),
            ("brush_wall", new Vector3(-9.2f, 0.05f, 2.6f), new Vector3(-11f, 1f, 2.6f)),
            ("number_through_divider", new Vector3(0f, 0.05f, 6.5f), new Vector3(-8f, 0f, 7.4f)),
            ("vending", new Vector3(-13f, 0.05f, -3.8f), new Vector3(-13f, 1.1f, -6.6f)),
            ("pillar_divider", new Vector3(-4f, 0.05f, -3.2f), new Vector3(-4f, 1.2f, 1.5f)),
            ("vacuum_area", new Vector3(25f, 0.05f, -5f), new Vector3(25f, 1f, -9.5f)),
            ("pay_terminal", new Vector3(-6.9f, 0.05f, 6.8f), new Vector3(-5.2f, 1.0f, 7.6f)),
            ("boom_reel", new Vector3(-6.2f, 0.05f, 1.2f), new Vector3(-9.5f, 2.2f, 4f)),
            ("canopy_up", new Vector3(0f, 0.05f, 1.5f), new Vector3(0f, 4.3f, 7f)),
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
            foreach (var (name, pos, look) in Closeups)
            {
                me.Motor.Teleport(pos);
                yield return new WaitForSeconds(0.6f);
                me.Look.LookAt(look);
                yield return new WaitForSeconds(0.45f);
                ScreenCapture.CaptureScreenshot($"views_{Label}_close_{name}.png");
                yield return null;
            }
            DevAutopilot.Log($"views: {Spots.Length * 8} screenshots ({Label})");
            input.scripted = false;
        }
    }
}
