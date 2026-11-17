using System.Collections;
using System.Collections.Generic;
using System.IO;
using CarWash.Player;
using CarWash.Vehicles;
using UnityEngine;
using UnityEngine.AI;

namespace CarWash.Core
{
    /// <summary>T2.1 automated check (runs on the client): lot paths, route clearance, NavMesh agents, replicated bay shutters.</summary>
    public static class DevChecksLot
    {
        public static IEnumerator Run(PlayerNet me)
        {
            var paths = LotPaths.Instance;
            if (paths == null) { DevAutopilot.Log("LotPaths missing"); yield break; }
            yield return new WaitForSeconds(1.5f); // shutters animate

            // routes
            int blocked = 0, segs = 0;
            var routes = new List<List<Vector3>> { paths.RouteToQueueSlot(3), paths.RouteToQueueSlot(0), paths.RouteQueueToExit(2) };
            for (int b = 0; b < paths.Bays.Count; b++) { routes.Add(paths.RouteHeadToBay(b)); routes.Add(paths.RouteBayToExit(b)); }
            foreach (var r in routes)
                for (int i = 0; i + 1 < r.Count; i++)
                {
                    segs++;
                    var a = r[i] + Vector3.up * 0.8f; var c = r[i + 1] + Vector3.up * 0.8f;
                    if (Physics.SphereCast(a, 0.9f, (c - a).normalized, out var hit, Vector3.Distance(a, c), 1 << Layers.Default, QueryTriggerInteraction.Ignore))
                    { blocked++; DevAutopilot.Log($"route segment blocked by {hit.collider.name} at {hit.point:F1}"); }
                }
            DevAutopilot.Log($"lot: queue slots={paths.QueueSlots.Count}, bays={paths.Bays.Count}, route segments={segs}, blocked={blocked} (car width sweep 1.8 m)");

            // navmesh for both agent types
            foreach (var agent in new[] { "Small", "Large" })
            {
                int id = AgentId(agent);
                var filter = new NavMeshQueryFilter { agentTypeID = id, areaMask = NavMesh.AllAreas };
                var from = new Vector3(0f, 0f, 16f);           // back alley
                var to = agent == "Small" ? new Vector3(0.8f, 0f, -10.5f) : new Vector3(-20f, 0f, 0f); // cage / queue lane
                bool okA = NavMesh.SamplePosition(from, out var ha, 1.5f, filter);
                bool okB = NavMesh.SamplePosition(to, out var hb, 2.5f, filter);
                var path = new NavMeshPath();
                bool calc = okA && okB && NavMesh.CalculatePath(ha.position, hb.position, filter, path);
                float len = 0f; for (int i = 0; i + 1 < path.corners.Length; i++) len += Vector3.Distance(path.corners[i], path.corners[i + 1]);
                DevAutopilot.Log($"navmesh {agent} (id {id}): sample {okA}/{okB}, path {path.status} length {len:0.0} m");
            }

            // replicated bay state (host opened all bays for the dev test)
            var lot = LotState.Instance;
            string bays = "";
            foreach (var b in paths.Bays)
            {
                var shutter = b.transform.Find("ShutterS");
                bays += $"Bay{b.Index + 1}:{(b.IsOpen ? "open" : "closed")}(shutterY {shutter.localPosition.y:0.0}) ";
            }
            DevAutopilot.Log($"bay state mask={(lot != null ? lot.OpenBayMask.Value : -1)} {bays}");
            CaptureOverview();
        }

        static int AgentId(string name)
        {
            for (int i = 0; i < NavMesh.GetSettingsCount(); i++)
            {
                var s = NavMesh.GetSettingsByIndex(i);
                if (NavMesh.GetSettingsNameFromID(s.agentTypeID) == name) return s.agentTypeID;
            }
            return -1;
        }

        static void CaptureOverview()
        {
            var go = new GameObject("OverviewShot");
            var cam = go.AddComponent<Camera>();
            cam.transform.SetPositionAndRotation(new Vector3(0, 38f, -30f), Quaternion.Euler(55f, 0, 0));
            cam.fieldOfView = 60f;
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(Application.dataPath, "autotest_lot.png"), tex.EncodeToPNG());
            Object.Destroy(tex); rt.Release(); Object.Destroy(go);
        }
    }
}
