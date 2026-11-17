using System.Collections;
using CarWash.Player;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>P6 client check (-autotest-scenery): screenshots from fixed viewpoints (lot, bays + signs, road + skyline, office).</summary>
    public static class DevChecksScenery
    {
        static readonly (string name, Vector3 pos, Vector3 lookAt)[] Views =
        {
            ("lot", new Vector3(-14f, 0.05f, -12.5f), new Vector3(6f, 1.5f, 6f)),
            ("bays", new Vector3(0f, 0.05f, 15f), new Vector3(0f, 2.5f, 4f)),
            ("road", new Vector3(-20f, 0.05f, -9f), new Vector3(-27f, 4.5f, -19f)),
            ("skyline", new Vector3(10f, 0.05f, -12f), new Vector3(60f, 8f, -60f)),
        };

        public static IEnumerator Run(PlayerNet me)
        {
            var input = me.GetComponent<PlayerInputReader>();
            input.scripted = true;
            yield return new WaitForSeconds(1f);
            foreach (var v in Views)
            {
                me.Motor.Teleport(v.pos);
                yield return new WaitForSeconds(0.4f);
                me.Look.LookAt(v.lookAt);
                yield return new WaitForSeconds(0.8f);
                ScreenCapture.CaptureScreenshot($"autotest_scenery_{v.name}.png");
                yield return new WaitForSeconds(0.3f);
            }
            DevAutopilot.Log($"P6 scenery: {Views.Length} screenshots, fog={RenderSettings.fog} skybox={(RenderSettings.skybox != null ? RenderSettings.skybox.name : "none")}");
            input.scripted = false;
        }
    }
}
