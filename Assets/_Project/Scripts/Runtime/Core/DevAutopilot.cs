using System;
using System.Collections;
using System.Linq;
using CarWash.Player;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// Dev-only automated checks (command line -autotest). Drives the LOCAL player with scripted input and logs
    /// "[AUTOTEST]" lines; on the host it also logs "[NETOBS]" lines with every remote player's replicated position.
    /// Used to verify features with a second (standalone) client because MPPM can't be driven by the agent.
    /// </summary>
    public class DevAutopilot : MonoBehaviour
    {
        public static bool Requested => Array.IndexOf(Environment.GetCommandLineArgs(), "-autotest") >= 0;
        /// <summary>T2.4 day-loop test only (client: -autotest-day; editor host: set before adding the component).</summary>
        public static bool DayMode = Array.IndexOf(Environment.GetCommandLineArgs(), "-autotest-day") >= 0;
        /// <summary>T3.6 surprise test (client: -autotest-surprise; editor host: set before adding the component).</summary>
        public static bool SurpriseMode = Array.IndexOf(Environment.GetCommandLineArgs(), "-autotest-surprise") >= 0;
        /// <summary>M4 4-player test (client: -autotest-net; editor host: set before adding the component).</summary>
        public static bool NetMode = Array.IndexOf(Environment.GetCommandLineArgs(), "-autotest-net") >= 0;
        /// <summary>P1 network-simulator test (client: -autotest-p1; editor host: set before adding the component).</summary>
        public static bool P1Mode = Array.IndexOf(Environment.GetCommandLineArgs(), "-autotest-p1") >= 0;
        /// <summary>P7 release loop check (-autotest-release; the hosting exe observes and runs DevReleaseScenario).</summary>
        /// <summary>GLB vehicle check (-autotest-glb; the hosting exe observes and runs DevGlbScenario).</summary>
        public static bool GlbMode = Array.IndexOf(Environment.GetCommandLineArgs(), "-autotest-glb") >= 0;
        public static bool ReleaseMode = Array.IndexOf(Environment.GetCommandLineArgs(), "-autotest-release") >= 0;
        /// <summary>GLB props / tools / critters check (-autotest-models; the hosting exe observes and runs DevModelsScenario).</summary>
        public static bool ModelsMode = Array.IndexOf(Environment.GetCommandLineArgs(), "-autotest-models") >= 0;
        /// <summary>Player looks check (-autotest-looks; the hosting exe observes and runs DevLooksScenario, two clients run DevChecksLooks).</summary>
        public static bool LooksMode = Array.IndexOf(Environment.GetCommandLineArgs(), "-autotest-looks") >= 0;
        /// <summary>Play-area walls check (-autotest-bounds; the hosting exe only observes).</summary>
        public static bool BoundsMode = Array.IndexOf(Environment.GetCommandLineArgs(), "-autotest-bounds") >= 0;
        /// <summary>M4.6 B3 spawn check (-autotest-spawn on host + clients; everyone checks their own spawn, nobody observes).</summary>
        public static bool SpawnMode = Array.IndexOf(Environment.GetCommandLineArgs(), "-autotest-spawn") >= 0;
        public static event Func<PlayerNet, IEnumerator> ExtraChecks; // later tasks append their own checks

        [Tooltip("Host in the editor: only log remote players (the client build runs the checks)")]
        public bool observeOnly;

        IEnumerator Start()
        {
            if (transform.parent == null) DontDestroyOnLoad(gameObject);
            while (PlayerLook.Local == null) yield return null;
            yield return new WaitForSeconds(1.5f);
            var me = PlayerLook.Local.GetComponent<PlayerNet>();
            Log($"local player client={me.OwnerClientId} server={NetworkManager.Singleton.IsServer} color={me.ColorIndex.Value}");
            if (!DevChecksNetLoad.Requested) StartCoroutine(Observe()); // M4.7: no test log spam in the measurement
            if ((ReleaseMode || GlbMode || ModelsMode || LooksMode || BoundsMode) && NetworkManager.Singleton.IsServer) observeOnly = true; // P7: the hosting exe only observes
            // dev only: open all three bays so the vehicle test fills them in parallel (not in the release check)
            if (!ReleaseMode && !GlbMode && !ModelsMode && !LooksMode && !BoundsMode && !SpawnMode && NetworkManager.Singleton.IsServer && Vehicles.LotState.Instance != null) Vehicles.LotState.Instance.ServerSetOpenCount(3);
            if (NetworkManager.Singleton.IsServer && observeOnly) me.Ready.Value = true; // T4.1: the observing editor host is always ready
            if (observeOnly)
            {
                if (NetworkManager.Singleton.IsServer) StartCoroutine(DevChecksPatience.Requested ? DevChecksPatience.Host() : BoundsMode ? DevChecksBounds.HostIdle() : LooksMode ? DevLooksScenario.Run() : ModelsMode ? DevModelsScenario.Run() : GlbMode ? DevGlbScenario.Run() : ReleaseMode ? DevReleaseScenario.Run() : P1Mode ? DevP1Scenario.Run() : NetMode ? DevNetScenario.Run() : SurpriseMode ? DevSurpriseScenario.Run() : DayMode ? DevDayScenario.Run() : DevHostScenario.Run());
                if (NetworkManager.Singleton.IsServer && ReleaseMode && DevChecksSwing.Requested) StartCoroutine(DevChecksSwing.HostObserve()); // M4.6 D8
                yield break;
            }
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-autotest-foam") >= 0 && !NetworkManager.Singleton.IsServer) // M4.8 F1-F3
            {
                yield return DevChecksFoam.Run(me);
                Log("DONE");
                yield break;
            }
            if (DevChecksF5.Requested && !NetworkManager.Singleton.IsServer) // M4.8 F5/F6: swing / grab / throw players + critters
            {
                yield return DevChecksF5.Run(me);
                Log("DONE");
                yield break;
            }
            if (DevChecksViews.Label != null) // M4.7 E4: environment screenshots, 3 spots × 8 directions
            {
                yield return DevChecksViews.Run(me);
                Log("DONE");
                yield break;
            }
            if (DevChecksNetLoad.Requested) // M4.7 N1: every peer (host too) plays and records
            {
                yield return DevChecksNetLoad.Run(me);
                Log("DONE");
                yield break;
            }
            if (SpawnMode)
            {
                if (NetworkManager.Singleton.IsServer) StartCoroutine(DevChecksSpawn.Host(4));
                yield return DevChecksSpawn.Run(me);
                Log("DONE");
                yield break;
            }
            if (BoundsMode)
            {
                yield return DevChecksBounds.Run(me);
                Log("DONE");
                yield break;
            }
            if (LooksMode)
            {
                yield return DevChecksLooks.Run(me);
                Log("DONE");
                yield break;
            }
            if (ModelsMode)
            {
                yield return DevChecksModels.Run(me);
                Log("DONE");
                yield break;
            }
            if (GlbMode)
            {
                yield return DevChecksGlb.Run(me);
                Log("DONE");
                yield break;
            }
            if (ReleaseMode)
            {
                if (DevChecksSwing.Requested) { yield return DevChecksSwing.Run(me); Log("DONE"); yield break; } // M4.6 D8
                if (DevChecksWater.Requested) { yield return DevChecksWater.Run(me); Log("DONE"); yield break; } // M4.6 D6/D7
                if (DevChecksInterior.Requested) { yield return DevChecksInterior.Run(me); Log("DONE"); yield break; } // M4.6 D3
                if (DevChecksLights.Requested) { yield return DevChecksLights.Run(me); Log("DONE"); yield break; } // M4.9 G7
                if (DevChecksWardrobe.Requested) { yield return DevChecksWardrobe.Run(me); Log("DONE"); yield break; } // M4.9 G3
                if (DevChecksPatience.Requested) { yield return DevChecksPatience.Client(me); Log("DONE"); yield break; } // M4.9 G6
                if (DevChecksTutorial.Requested) StartCoroutine(DevChecksTutorial.Watch()); // M4.6 D1
                yield return DevChecksRelease.Run(me);
                if (DevChecksTutorial.Requested) yield return DevChecksTutorial.HelpAndScale(); // M4.6 D1/D2
                Log("DONE");
                yield break;
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotest-scenery") >= 0)
            {
                yield return DevChecksScenery.Run(me);
                Log("DONE");
                yield break;
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotest-ragdoll") >= 0)
            {
                yield return DevChecksRagdoll.Run(me);
                Log("DONE");
                yield break;
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotest-settings") >= 0)
            {
                yield return DevChecksSettings.Run(me);
                Log("DONE");
                yield break;
            }
            if (P1Mode)
            {
                yield return DevChecksP1.Run(me);
                Log("DONE");
                yield break;
            }
            if (NetMode)
            {
                yield return DevChecksNet.Run(me);
                Log("DONE");
                yield break;
            }
            if (SurpriseMode)
            {
                yield return DevChecksSurprise.Run(me);
                Log("DONE");
                yield break;
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotest-species") >= 0)
            {
                yield return DevChecksSpecies.Run(me);
                Log("DONE");
                yield break;
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotest-critters") >= 0)
            {
                yield return DevChecksCritters.Run(me);
                Log("DONE");
                yield break;
            }
            if (DayMode)
            {
                yield return DevChecksDay.Run(me);
                Log("DONE");
                yield break;
            }
            yield return DevChecksLot.Run(me);
            yield return DevChecksVehicles.Run(me);
            yield return MotorChecks(me);
            yield return GrabChecks(me);
            yield return DevChecksWasher.Run(me);
            yield return DevChecksFoam.Run(me);
            yield return DevChecksInventory.Run(me);
            yield return DevChecksCritters.Run(me);
            yield return DevChecksSpecies.Run(me);
            if (ExtraChecks != null)
                foreach (Func<PlayerNet, IEnumerator> f in ExtraChecks.GetInvocationList()) yield return f(me);
            Log("DONE");
        }

        public static void Log(string m) => Debug.Log("[AUTOTEST] " + m);

        IEnumerator MotorChecks(PlayerNet me)
        {
            var input = me.GetComponent<PlayerInputReader>();
            var motor = me.Motor;
            var look = me.Look;
            input.scripted = true;
            // offset per client so two players don't collide
            float lane = me.OwnerClientId * 2.2f;

            // 1) normal ground: sprint, release, stopping distance
            motor.Teleport(new Vector3(-1f + lane, 0.05f, -9f)); look.SetYaw(0f);
            yield return new WaitForSeconds(0.3f);
            yield return SprintAndRelease(input, motor, 1.0f, d => Log($"normal ground stop distance = {d:0.00} m"));

            // 2) foam: start before Bay1 foam zone, sprint into it, release inside
            motor.Teleport(DevYard.FoamZone + new Vector3(lane * 0.5f, 0.05f, -9.5f)); look.SetYaw(0f);
            yield return new WaitForSeconds(0.3f);
            yield return SprintAndRelease(input, motor, 1.0f, d => Log($"foam slide distance = {d:0.00} m (inFoam={motor.InFoam})"));

            // 3) knockback above knockdown speed → Knocked state
            motor.Teleport(new Vector3(3f + lane, 0.05f, -9f));
            yield return new WaitForSeconds(0.3f);
            var start = motor.transform.position;
            motor.AddKnockback(new Vector3(12f, 2f, 0f));
            yield return null; yield return null;
            bool knocked = me.Status.IsKnocked;
            yield return new WaitForSeconds(2f);
            Log($"knockback: knocked={knocked} pushed={(motor.transform.position - start).magnitude:0.00} m, recovered={!me.Status.IsKnocked}");

            // 4) jump
            float y0 = motor.transform.position.y, maxY = y0;
            input.sJump = true;
            for (float t = 0; t < 1.2f; t += Time.deltaTime) { maxY = Mathf.Max(maxY, motor.transform.position.y); yield return null; }
            Log($"jump height = {maxY - y0:0.00} m");
            input.scripted = false;
        }

        IEnumerator GrabChecks(PlayerNet me)
        {
            var input = me.GetComponent<PlayerInputReader>();
            var grabber = me.GetComponent<PlayerGrabber>();
            input.scripted = true;
            Grabbable Find(string prefix, float maxMass) => FindObjectsByType<Grabbable>(FindObjectsSortMode.None)
                .Where(g => g.name.StartsWith(prefix) && !g.IsHeld && g.Mass <= maxMass + 0.01f)
                .OrderBy(g => (g.transform.position - me.transform.position).sqrMagnitude).FirstOrDefault();

            // heavy crate must be rejected
            var heavy = Find("HeavyCrate", 999f);
            if (heavy != null)
            {
                me.Motor.Teleport(heavy.transform.position + new Vector3(0, -heavy.transform.position.y + 0.05f, -1.6f));
                yield return new WaitForSeconds(0.2f);
                me.Look.LookAt(heavy.transform.position);
                yield return new WaitForSeconds(0.2f);
                input.sGrab = true; yield return new WaitForSeconds(0.4f); input.sGrab = false; yield return new WaitForSeconds(0.2f);
                Log($"heavy crate ({heavy.Mass}kg) grab rejected = {grabber.Held == null}, prompt='{grabber.PromptText()}'");
            }

            var crate = Find("Crate", 40f);
            if (crate == null) { Log("no crate found"); yield break; }
            me.Motor.Teleport(crate.transform.position + new Vector3(0, -crate.transform.position.y + 0.05f, -1.6f));
            yield return new WaitForSeconds(0.2f);
            me.Look.LookAt(crate.transform.position);
            yield return new WaitForSeconds(0.2f);
            string prompt = grabber.PromptText();
            input.sGrab = true; yield return new WaitForSeconds(0.3f); input.sGrab = false;
            yield return new WaitForSeconds(0.5f);
            bool held = grabber.Held == crate;
            float lift = crate.transform.position.y;
            Log($"crate ({crate.Mass}kg) prompt='{prompt}' held={held} heldHeight={lift:0.00}");

            // charged throw: aim slightly up, hold 1s, release
            me.Look.SetPitch(-15f);
            yield return new WaitForSeconds(0.3f);
            var p0 = crate.transform.position;
            input.sGrab = true; yield return new WaitForSeconds(1.1f);
            float charge = grabber.Charge;
            input.sGrab = false;
            yield return new WaitForSeconds(2.5f);
            var d = crate.transform.position - p0; d.y = 0;
            Log($"charged throw: charge={charge:0.00} released={grabber.Held == null} distance={d.magnitude:0.00} m");
            input.scripted = false;
        }

        IEnumerator SprintAndRelease(PlayerInputReader input, PlayerMotor motor, float sprintTime, Action<float> report)
        {
            input.sMove = Vector2.up; input.sSprint = true;
            yield return new WaitForSeconds(sprintTime);
            input.sMove = Vector2.zero; input.sSprint = false;
            var p0 = motor.transform.position;
            float t = 0f;
            yield return null;
            while (t < 6f && new Vector2(motor.Velocity.x, motor.Velocity.z).magnitude > 0.05f) { t += Time.deltaTime; yield return null; }
            var d = motor.transform.position - p0; d.y = 0f;
            report(d.magnitude);
        }

        IEnumerator Observe()
        {
            var nm = NetworkManager.Singleton;
            while (true)
            {
                yield return new WaitForSeconds(1f);
                if (nm == null || !nm.IsServer) yield break;
                foreach (var p in FindObjectsByType<PlayerNet>(FindObjectsSortMode.None).Where(p => !p.IsOwner))
                    Debug.Log($"[NETOBS] remote client {p.OwnerClientId} pos={p.transform.position:F2} color={p.ColorIndex.Value} flags={p.Status.Flags}");
            }
        }
    }
}
