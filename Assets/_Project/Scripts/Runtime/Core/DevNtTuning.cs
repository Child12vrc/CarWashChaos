using System;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// M4.7 N2 (dev builds only): command-line overrides for interpolation experiments, so variants can be measured with the
    /// netload scenario without rebuilding. Every peer must get the same -tick.
    ///   -tick N            NetworkConfig.TickRate
    ///   -nt-buffer N       NetworkTransform.InterpolationBufferTickOffset
    ///   -nt-interp legacy|lerp|smooth   position + rotation interpolation type
    ///   -nt-lerpsmooth 0|1 second-phase lerp smoothing
    ///   -nt-maxinterp S    Position/RotationMaxInterpolationTime
    /// </summary>
    public static class DevNtTuning
    {
        static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }

        public static bool Active => DevFlags.DevContent && (Arg("-tick") != null || Arg("-nt-buffer") != null || Arg("-nt-interp") != null || Arg("-nt-lerpsmooth") != null || Arg("-nt-maxinterp") != null);

        public static string Describe() => !Active ? "default" :
            $"tick {Arg("-tick") ?? "-"} buffer {Arg("-nt-buffer") ?? "-"} interp {Arg("-nt-interp") ?? "-"} lerpsmooth {Arg("-nt-lerpsmooth") ?? "-"} maxinterp {Arg("-nt-maxinterp") ?? "-"}";

        public static void ApplyGlobal(NetworkManager nm)
        {
            if (!DevFlags.DevContent) return;
            if (int.TryParse(Arg("-tick"), out int tick) && nm != null) nm.NetworkConfig.TickRate = (uint)tick;
            if (int.TryParse(Arg("-nt-buffer"), out int b)) NetworkTransform.InterpolationBufferTickOffset = b;
        }

        public static void Apply(NetworkTransform nt)
        {
            if (!DevFlags.DevContent || nt == null) return;
            switch (Arg("-nt-interp"))
            {
                case "legacy": nt.PositionInterpolationType = nt.RotationInterpolationType = NetworkTransform.InterpolationTypes.LegacyLerp; break;
                case "lerp": nt.PositionInterpolationType = nt.RotationInterpolationType = NetworkTransform.InterpolationTypes.Lerp; break;
                case "smooth": nt.PositionInterpolationType = nt.RotationInterpolationType = NetworkTransform.InterpolationTypes.SmoothDampening; break;
            }
            var ls = Arg("-nt-lerpsmooth");
            if (ls != null) nt.PositionLerpSmoothing = nt.RotationLerpSmoothing = ls == "1";
            if (float.TryParse(Arg("-nt-maxinterp"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float mi))
                nt.PositionMaxInterpolationTime = nt.RotationMaxInterpolationTime = mi;
        }
    }
}
