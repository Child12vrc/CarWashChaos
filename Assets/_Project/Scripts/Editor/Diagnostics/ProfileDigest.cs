using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace CarWash.Editor
{
    /// <summary>
    /// M4.7 N1: reads a player profiler capture (.raw from NetStatsRecorder -profilecapture) and sums, over all frames, each
    /// marker's SELF main-thread time and SELF GC allocation (its own minus its children's). Logs the top N of each as
    /// [PROFDIGEST] lines, plus the slowest frames' top markers — to tell rendering / scripts / GC / network apart.
    /// </summary>
    public static class ProfileDigest
    {
        public static string Run(string rawPath, int top = 20)
        {
            if (!ProfilerDriver.LoadProfile(rawPath, false)) return "[PROFDIGEST] could not load " + rawPath;
            int first = ProfilerDriver.firstFrameIndex, last = ProfilerDriver.lastFrameIndex;
            var selfMs = new Dictionary<string, double>();
            var selfGc = new Dictionary<string, double>();
            var frameMs = new List<(int f, float ms)>();
            var slowTop = new Dictionary<int, string>();
            int frames = 0;
            for (int f = first; f <= last; f++)
            {
                using (var v = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnSelfTime, false))
                {
                    if (v == null || !v.valid) continue;
                    frames++;
                    frameMs.Add((f, v.frameTimeMs));
                    var children = new List<int>();
                    var stack = new Stack<(int id, string parent)>();
                    stack.Push((v.GetRootItemID(), ""));
                    var perFrame = new Dictionary<string, double>();
                    while (stack.Count > 0)
                    {
                        var (id, parent) = stack.Pop();
                        v.GetItemChildren(id, children);
                        float gc = v.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnGcMemory);
                        float childGc = 0f;
                        string name = id == v.GetRootItemID() ? "" : v.GetItemName(id);
                        foreach (int c in children) { childGc += v.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnGcMemory); stack.Push((c, name)); }
                        if (id == v.GetRootItemID()) continue;
                        float self = v.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnSelfTime);
                        selfMs.TryGetValue(name, out var a); selfMs[name] = a + self;
                        perFrame.TryGetValue(name, out var pf); perFrame[name] = pf + self;
                        float sgc = Mathf.Max(0f, gc - childGc);
                        if (sgc > 0f) { string who = name == "GC.Alloc" ? parent : name; selfGc.TryGetValue(who, out var g); selfGc[who] = g + sgc; } // GC.Alloc samples belong to their caller
                    }
                    slowTop[f] = string.Join(", ", perFrame.OrderByDescending(k => k.Value).Take(4).Select(k => $"{k.Key} {k.Value:0.0}"));
                }
            }
            var sb = new StringBuilder();
            sb.Append($"[PROFDIGEST] {System.IO.Path.GetFileName(rawPath)}: {frames} frames, avg {frameMs.Average(x => x.ms):0.0} ms, max {frameMs.Max(x => x.ms):0.0} ms\n");
            sb.Append("[PROFDIGEST] -- self main-thread ms/frame --\n");
            foreach (var kv in selfMs.OrderByDescending(k => k.Value).Take(top)) sb.Append($"[PROFDIGEST]   {kv.Value / frames:0.000} ms  {kv.Key}\n");
            sb.Append("[PROFDIGEST] -- self GC alloc KB/frame --\n");
            foreach (var kv in selfGc.OrderByDescending(k => k.Value).Take(top)) sb.Append($"[PROFDIGEST]   {kv.Value / 1024.0 / frames:0.00} KB  {kv.Key}\n");
            sb.Append("[PROFDIGEST] -- 5 slowest frames --\n");
            foreach (var x in frameMs.OrderByDescending(x => x.ms).Take(5)) sb.Append($"[PROFDIGEST]   frame {x.f}: {x.ms:0.0} ms — {slowTop[x.f]}\n");
            var s = sb.ToString();
            Debug.Log(s);
            return s;
        }
    }
}
