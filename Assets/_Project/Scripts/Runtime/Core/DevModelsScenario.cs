using System.Collections;
using System.Linq;
using CarWash.Critters;
using CarWash.Player;
using CarWash.Tools;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// GLB props / tools / critters check, host side (-autotest -autotest-models on the hosting dev exe). Watches the remote
    /// client from the host's own peer: the third-person pose of the tool it holds (grip near its hand, nozzle ahead), critter
    /// code animation computed locally from replicated state only (flip / speed), and the netted critter hanging at the
    /// host's copy of the net's Socket_Capture. Screenshots autotest_models_host_*.png. Logged as [HOSTSCN] models:.
    /// </summary>
    public static class DevModelsScenario
    {
        static void Log(string m) => Debug.Log("[HOSTSCN] models: " + m);

        public static IEnumerator Run()
        {
            var nm = NetworkManager.Singleton;
            while (nm == null || nm.ConnectedClientsIds.Count < 2) yield return null;
            yield return new WaitForSeconds(3f);
            var me = PlayerLook.Local != null ? PlayerLook.Local.GetComponent<PlayerNet>() : null;
            string lastTool = null;
            bool shotNet = false, shotPossum = false, shotStun = false;
            float end = Time.time + 600f, nextLog = 0f;
            while (Time.time < end)
            {
                var client = Object.FindObjectsByType<PlayerNet>(FindObjectsSortMode.None).FirstOrDefault(p => !p.IsOwner);
                if (client == null) { yield return null; continue; }
                var tool = ToolBase.All.FirstOrDefault(t => t != null && t.HolderPlayer == client && !t.Stowed);
                string id = tool != null ? tool.ToolId : "-";
                if (id != lastTool)
                {
                    lastTool = id;
                    if (tool != null)
                    {
                        yield return new WaitForSeconds(0.5f);
                        var head = client.Look.Head;
                        Log($"remote holds {id}: grip(head)={head.InverseTransformPoint(tool.transform.position).ToString("F2")} nozzle(head)={head.InverseTransformPoint(tool.Nozzle.position).ToString("F2")} glb={tool.transform.Find("Model") != null}");
                        yield return Watch(me, client.transform.position + Vector3.up * 1.2f, client.transform.right * 2.2f + client.transform.forward * 1.2f);
                        ScreenCapture.CaptureScreenshot($"autotest_models_host_{id}.png");
                    }
                }
                foreach (var c in CritterBase.All)
                {
                    var a = c != null ? c.GetComponent<CritterAnimator>() : null;
                    if (a == null) continue;
                    if (!shotPossum && c is Possum && c.State.Value == CritterState.Act && a.Flip > 0.95f)
                    {
                        shotPossum = true;
                        Log($"host view possum playing dead: flip={a.Flip:0.00}");
                        yield return Watch(me, c.transform.position, new Vector3(1.4f, 1.1f, -1.4f));
                        ScreenCapture.CaptureScreenshot("autotest_models_host_possum.png");
                    }
                    if (!shotStun && c is Raccoon && c.State.Value == CritterState.Stunned && a.Flip > 0.95f)
                    {
                        shotStun = true;
                        Log($"host view raccoon stunned: flip={a.Flip:0.00}");
                    }
                    if (!shotNet && c.IsCarried && tool is NetPole net && net.Capture != null && a.CarrySocket != null)
                    {
                        yield return new WaitForSeconds(0.6f);
                        if (c == null || !c.IsCarried) continue;
                        shotNet = true;
                        Log($"host view netted {c.CritterId}: Socket_Carry→Socket_Capture {(a.CarrySocket.position - net.Capture.position).magnitude:0.000} m (root {(c.transform.position - net.Capture.position).magnitude:0.00} m away)");
                        yield return Watch(me, net.Capture.position, client.transform.right * 2.4f + Vector3.up * 0.4f);
                        ScreenCapture.CaptureScreenshot("autotest_models_host_netted.png");
                        break;
                    }
                }
                if (Time.time >= nextLog)
                {
                    nextLog = Time.time + 4f;
                    Log("critters (host): " + string.Join(" | ", CritterBase.All.Where(c => c != null && c.State.Value != CritterState.Hidden).Select(c =>
                    {
                        var a = c.GetComponent<CritterAnimator>();
                        return $"{c.CritterId} {c.State.Value} speed {(a != null ? a.Speed : 0f):0.0} flip {(a != null ? a.Flip : 0f):0.00}";
                    })));
                }
                yield return null;
            }
        }

        /// <summary>Park the host's own player at `target + offset` looking at the target (host camera = screenshot).</summary>
        static IEnumerator Watch(PlayerNet me, Vector3 target, Vector3 offset)
        {
            if (me == null) yield break;
            var p = target + offset; p.y = 0.05f;
            me.Motor.Teleport(p);
            yield return new WaitForSeconds(0.3f);
            me.Look.LookAt(target);
            yield return new WaitForSeconds(0.3f);
        }
    }
}
