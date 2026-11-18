using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// P7: what a build may run besides the game.
    ///  DevContent — dev yard, dev command-line tweaks (-fps, -netsim, -autoreconnect): editor + Development builds only.
    ///  Autotest   — DevAutopilot (-autotest ...): DevContent, or a release-verification build (define CARWASH_VERIFY:
    ///               same non-development player as the shipped release, dev yard still off, only the autopilot on).
    /// The shipped release has neither: it is the plain game.
    /// </summary>
    public static class DevFlags
    {
#if CARWASH_VERIFY
        public const bool VerifyBuild = true;
#else
        public const bool VerifyBuild = false;
#endif
        public static bool DevContent => Application.isEditor || Debug.isDebugBuild;
        public static bool Autotest => DevContent || VerifyBuild;
    }
}
