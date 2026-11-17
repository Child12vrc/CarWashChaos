using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarWash.Data
{
    /// <summary>
    /// Pure JSON → ScriptableObject loading. Source files are {"items":[{...},{...}]};
    /// each item object is applied to a fresh instance with JsonUtility.FromJsonOverwrite.
    /// </summary>
    public static class DataJson
    {
        public static List<T> LoadItems<T>(string json) where T : ScriptableObject
        {
            var list = new List<T>();
            foreach (var item in SplitItems(json))
            {
                var so = ScriptableObject.CreateInstance<T>();
                JsonUtility.FromJsonOverwrite(item, so);
                list.Add(so);
            }
            return list;
        }

        public static T LoadSingle<T>(string json) where T : ScriptableObject
        {
            var so = ScriptableObject.CreateInstance<T>();
            JsonUtility.FromJsonOverwrite(json, so);
            return so;
        }

        /// <summary>Returns the raw JSON text of every object inside the top-level "items" array.</summary>
        public static List<string> SplitItems(string json)
        {
            var result = new List<string>();
            int key = json.IndexOf("\"items\"", StringComparison.Ordinal);
            if (key < 0) throw new FormatException("missing \"items\" array");
            int i = json.IndexOf('[', key);
            if (i < 0) throw new FormatException("\"items\" is not an array");
            int depth = 0, start = -1;
            bool inString = false, escape = false;
            for (i++; i < json.Length; i++)
            {
                char c = json[i];
                if (inString)
                {
                    if (escape) escape = false;
                    else if (c == '\\') escape = true;
                    else if (c == '"') inString = false;
                    continue;
                }
                if (c == '"') { inString = true; continue; }
                if (c == '{') { if (depth == 0) start = i; depth++; }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && start >= 0) { result.Add(json.Substring(start, i - start + 1)); start = -1; }
                    if (depth < 0) throw new FormatException("unbalanced braces");
                }
                else if (c == ']' && depth == 0) return result;
            }
            throw new FormatException("unterminated items array");
        }
    }
}
