using System.Collections;
using System.Linq;
using CarWash.Critters;
using CarWash.Economy;
using CarWash.Player;
using CarWash.Tools;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// T3.1 client check on the dev-yard critters: spray the raccoon until stunned (wetness replicated, stun stars),
    /// net it (T06) → carry to the Animal Control cage → +$15; grab the possum by hand (no stun needed) → cage.
    /// </summary>
    public static class DevChecksCritters
    {
        static readonly Vector3 CagePos = new Vector3(-6.6f, 0f, -10.2f);

        public static IEnumerator Run(PlayerNet me)
        {
            var input = me.GetComponent<PlayerInputReader>();
            var tools = me.GetComponent<PlayerTools>();
            input.scripted = true;
            var raccoon = CritterBase.All.FirstOrDefault(c => c.CritterId == "C01");
            var possum = CritterBase.All.FirstOrDefault(c => c.CritterId == "C05");
            DevAutopilot.Log($"critters: raccoon={(raccoon != null ? raccoon.State.Value.ToString() : "none")} possum={(possum != null ? possum.State.Value.ToString() : "none")} cage={(AnimalCage.Instance != null)}");
            if (raccoon == null) yield break;
            // dev-yard critters start hidden (like surprise critters inside a car) → pop them out first
            raccoon.DevRequestReveal();
            if (possum != null) possum.DevRequestReveal();
            yield return new WaitForSeconds(1.2f);

            // 0) pick up the dev-yard net first so switching to it after the stun is just a slot key
            yield return DevChecksFoam.Equip<NetPole>(me);
            // 1) washer → stun the raccoon (stand by the dev-yard reel first so Equip picks that washer, not bay 1's)
            if (!(tools.Equipped is PressureWasher) && !Enumerable.Range(0, PlayerInventory.SlotCount).Any(i => tools.Inventory.Get(i) is PressureWasher))
            {
                me.Motor.Teleport(new Vector3(DevYard.WasherReel.x + 1.5f, 0.05f, DevYard.WasherReel.z));
                yield return new WaitForSeconds(0.3f);
            }
            yield return DevChecksFoam.Equip<PressureWasher>(me);
            float t0 = Time.time, maxWet = 0f;
            bool shot = false;
            while (Time.time - t0 < 15f && raccoon.State.Value != CritterState.Stunned)
            {
                var c = raccoon.transform.position;
                var reel = DevYard.WasherReel; reel.y = 0f;
                var toReel = (reel - c); toReel.y = 0f;
                me.Motor.Teleport(c + toReel.normalized * 2.8f + new Vector3(0, 0.05f, 0));
                me.Look.LookAt(c + Vector3.up * 0.3f);
                input.sUse = true;
                maxWet = Mathf.Max(maxWet, raccoon.Wetness.Value);
                yield return null;
            }
            input.sUse = false;
            float stunAt = Time.time - t0;
            yield return new WaitForSeconds(0.3f);
            if (!shot) { ScreenCapture.CaptureScreenshot("autotest_stun.png"); shot = true; }
            DevAutopilot.Log($"raccoon: state={raccoon.State.Value} after {stunAt:0.0}s, wetness {raccoon.Wetness.Value:0}/{raccoon.MaxWetness.Value:0} (max seen {maxWet:0}); prompt='{me.GetComponent<PlayerInteractor>().PromptText()}'");

            // 2) net it
            yield return DevChecksFoam.Equip<NetPole>(me);
            float tn = Time.time;
            while (Time.time - tn < 4f && raccoon != null && !raccoon.IsCarried)
            {
                var c = raccoon.transform.position;
                me.Motor.Teleport(c + new Vector3(0f, 0.05f, -1.8f));
                me.Look.LookAt(c + Vector3.up * 0.3f);
                input.sUse = true;
                yield return null;
            }
            input.sUse = false;
            if (raccoon == null) { DevAutopilot.Log("raccoon: gone before the net (despawned by the host scenario) — net check skipped"); yield break; }
            bool netted = raccoon.IsCarried && raccoon.CarrierClient.Value == me.OwnerClientId;
            DevAutopilot.Log($"net: carried={netted} state={raccoon.State.Value} (stun left when netted ≈ {Mathf.Max(0f, 6f * 1.25f - (Time.time - t0 - stunAt)):0.0}s)");

            // 3) to the cage
            int money0 = EconomyState.Instance != null ? EconomyState.Instance.Money.Value : 0;
            yield return CarryToCage(me, raccoon, 10f);
            int money1 = EconomyState.Instance != null ? EconomyState.Instance.Money.Value : 0;
            DevAutopilot.Log($"cage: raccoon gone={raccoon == null || !raccoon.IsSpawned} money {money0}→{money1}; toast='{UI.Hud.Instance?.Day?.LastToast}'");

            // 4) possum: no stun needed → grab with E (hands, not secure)
            if (possum != null)
            {
                // select an empty slot so the net isn't in the hands
                var inv = tools.Inventory;
                for (int i = 0; i < PlayerInventory.SlotCount; i++) if (inv.Get(i) == null) { input.sSlot = i; break; }
                yield return new WaitForSeconds(0.4f);
                var p = possum.transform.position;
                me.Motor.Teleport(p + new Vector3(0f, 0.05f, -1.4f));
                me.Look.LookAt(p + Vector3.up * 0.2f);
                yield return new WaitForSeconds(0.3f);
                string prompt = me.GetComponent<PlayerInteractor>().PromptText();
                input.sInteract = true;
                { float tw = Time.time; while (!possum.IsCarried && Time.time - tw < 4f) yield return null; }
                DevAutopilot.Log($"possum: prompt='{prompt}' carried={possum.IsCarried} by me={possum.CarrierClient.Value == me.OwnerClientId}");
                int m0 = EconomyState.Instance.Money.Value;
                yield return CarryToCage(me, possum, 6f);
                DevAutopilot.Log($"cage: possum gone={possum == null || !possum.IsSpawned} money {m0}→{EconomyState.Instance.Money.Value}");
            }
            me.Motor.Teleport(new Vector3(-3.5f, 0.05f, -10f));
            input.scripted = false;
        }

        static IEnumerator CarryToCage(PlayerNet me, CritterBase c, float timeout)
        {
            // stand north of the open cage side, face south into it
            me.Motor.Teleport(CagePos + new Vector3(0f, 0.05f, 2.6f));
            me.Look.SetYaw(180f);
            me.Look.SetPitch(0f);
            float t = Time.time;
            while (c != null && c.IsSpawned && Time.time - t < timeout) yield return null;
            yield return new WaitForSeconds(0.5f);
        }
    }
}
