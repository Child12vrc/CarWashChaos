using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CarWash.Editor
{
    /// <summary>
    /// M4.11 / ART_REQUESTS Batch H: builds a LODGroup on `root` from the Blender naming rule — every LOD mesh is a sibling of
    /// its LOD0 node under the same parent, named `&lt;Node&gt;_LOD1` / `&lt;Node&gt;_LOD2`. A node without a variant for a
    /// level keeps its previous level's renderer; an EMPTY `_LODn` node (no mesh) drops the part from that level on.
    /// </summary>
    public static class LodKit
    {
        public static bool IsLodNode(string name) => LevelOf(name) > 0;

        public static int LevelOf(string name)
        {
            if (name.EndsWith("_LOD1")) return 1;
            if (name.EndsWith("_LOD2")) return 2;
            return 0;
        }

        static string BaseOf(string name) => LevelOf(name) > 0 ? name.Substring(0, name.Length - 5) : name;

        /// <summary>Returns the LODGroup, or null when the model has no `_LODn` nodes. `heights` = screen-relative transition
        /// heights for LOD0 / LOD1 / LOD2 (the last one is the cull height).</summary>
        public static LODGroup Build(Transform root, float[] heights, ICollection<Renderer> exclude = null, ICollection<Renderer> lod0Only = null)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            if (!all.Any(t => IsLodNode(t.name))) return null;
            var levels = new[] { new List<Renderer>(), new List<Renderer>(), new List<Renderer>() };
            foreach (var t in all)
            {
                if (IsLodNode(t.name)) continue;
                var r = t.GetComponent<Renderer>();
                if (r == null || (exclude != null && exclude.Contains(r))) continue;
                levels[0].Add(r);
                if (lod0Only != null && lod0Only.Contains(r)) continue; // e.g. a car's cabin: only drawn up close
                Renderer prev = r;
                bool dropped = false;
                for (int lv = 1; lv <= 2; lv++)
                {
                    var v = t.parent != null ? t.parent.Find($"{t.name}_LOD{lv}") : null;
                    if (v != null)
                    {
                        var vr = v.GetComponent<Renderer>();
                        if (vr == null) dropped = true; // empty node: gone from this level on
                        else { prev = vr; dropped = false; }
                    }
                    if (!dropped) levels[lv].Add(prev);
                }
            }
            var group = root.gameObject.GetComponent<LODGroup>();
            if (group == null) group = root.gameObject.AddComponent<LODGroup>(); // not ??: Unity's fake-null in the editor
            group.fadeMode = LODFadeMode.None;
            group.SetLODs(new[]
            {
                new LOD(heights[0], levels[0].ToArray()),
                new LOD(heights[1], levels[1].ToArray()),
                new LOD(heights[2], levels[2].ToArray()),
            });
            group.RecalculateBounds();
            return group;
        }
    }
}
