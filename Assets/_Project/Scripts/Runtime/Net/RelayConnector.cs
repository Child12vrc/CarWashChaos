using System;
using System.Threading.Tasks;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace CarWash.Net
{
    /// <summary>
    /// P7: Unity Relay "참가 코드" play over the internet (no port forwarding, no firewall rule: both peers only make
    /// outbound connections to the Relay server). Needs the Unity project linked to a UGS cloud project with Relay
    /// enabled (docs/TASKS.md Manual steps); without it every call fails with a readable Korean message and the
    /// IP mode (고급) still works.
    /// </summary>
    public static class RelayConnector
    {
        public const string Protocol = "dtls";
        public const int CodeLength = 6;

        /// <summary>Thrown with a message the menu can show as-is.</summary>
        public class RelayError : Exception { public RelayError(string m, Exception inner = null) : base(m, inner) { } }

        public static bool ProjectLinked => !string.IsNullOrEmpty(Application.cloudProjectId);

        static async Task SignIn()
        {
            if (!ProjectLinked)
                throw new RelayError("이 빌드는 Unity 온라인 서비스(Relay)에 연결되어 있지 않습니다. 고급 → IP로 플레이를 사용해 주세요.");
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    // a fresh profile per process: two copies on one PC must be two different anonymous players
                    var opts = new InitializationOptions().SetProfile("p" + UnityEngine.Random.Range(100000, 999999));
                    await UnityServices.InitializeAsync(opts);
                }
                if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[NET] UGS sign-in failed: " + e);
                throw new RelayError("온라인 서비스에 로그인하지 못했습니다. 인터넷 연결을 확인해 주세요.", e);
            }
        }

        /// <summary>Host: allocate a Relay slot for (maxPlayers - 1) guests, point the transport at it, return the join code.</summary>
        public static async Task<string> PrepareHost(UnityTransport utp, int maxPlayers)
        {
            await SignIn();
            try
            {
                var alloc = await Retry(() => RelayService.Instance.CreateAllocationAsync(maxPlayers - 1), "allocation");
                string code = await Retry(() => RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId), "join code");
                utp.SetRelayServerData(alloc.ToRelayServerData(Protocol));
                Debug.Log($"[NET] relay host allocation {alloc.AllocationId} region {alloc.Region}, join code {code}");
                return code;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[NET] relay host failed: " + e);
                throw new RelayError("코드를 발급하지 못했습니다. Relay가 켜져 있는지, 인터넷 연결을 확인해 주세요.", e);
            }
        }

        /// <summary>M4.7: Relay's HTTP API sometimes answers 5xx / 429 / times out (seen in the 4-player test: 504 Gateway Timeout
        /// on join). Those are retried twice (1 s, 2 s); a real "no such code" (4xx) fails at once.</summary>
        public static async Task<T> Retry<T>(Func<Task<T>> call, string what)
        {
            for (int attempt = 1; ; attempt++)
            {
                try { return await call(); }
                catch (Exception e) when (attempt < 3 && IsTransient(e))
                {
                    Debug.LogWarning($"[NET] relay {what}: transient error (attempt {attempt}), retrying: {e.Message}");
                    await Task.Delay(1000 * attempt);
                }
            }
        }

        public static bool IsTransient(Exception e)
        {
            string m = e.ToString();
            foreach (var s in new[] { "(500)", "(502)", "(503)", "(504)", "(429)", "Timeout", "timed out" }) if (m.Contains(s)) return true;
            return false;
        }

        /// <summary>Guest: resolve the join code and point the transport at the host's Relay server.</summary>
        public static async Task PrepareJoin(UnityTransport utp, string code)
        {
            code = NormalizeCode(code);
            if (!IsValidCode(code)) throw new RelayError($"참가 코드는 영문·숫자 {CodeLength}자리입니다.");
            await SignIn();
            try
            {
                var join = await Retry(() => RelayService.Instance.JoinAllocationAsync(code), "join");
                utp.SetRelayServerData(join.ToRelayServerData(Protocol));
                Debug.Log($"[NET] relay join {code}: allocation {join.AllocationId} region {join.Region}");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[NET] relay join failed: " + e);
                throw new RelayError("코드에 해당하는 방을 찾지 못했습니다. 코드를 다시 확인해 주세요.", e);
            }
        }

        /// <summary>"  ab c12 3 " → "ABC123" (EditMode tested).</summary>
        public static string NormalizeCode(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder();
            foreach (char ch in s) if (char.IsLetterOrDigit(ch)) sb.Append(char.ToUpperInvariant(ch));
            return sb.ToString();
        }

        public static bool IsValidCode(string code)
        {
            if (code == null || code.Length != CodeLength) return false;
            foreach (char ch in code) if (!(ch >= 'A' && ch <= 'Z') && !(ch >= '0' && ch <= '9')) return false;
            return true;
        }
    }
}
