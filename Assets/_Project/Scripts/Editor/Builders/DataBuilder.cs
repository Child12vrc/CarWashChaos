using System.Collections.Generic;
using System.IO;
using System.Linq;
using CarWash.Data;
using UnityEditor;
using UnityEngine;

namespace CarWash.Editor
{
    /// <summary>
    /// Data/Source/*.json → Data/Generated/*.asset + DataRegistry. Idempotent: existing assets are overwritten in place (GUIDs kept).
    /// CLI: -executeMethod CarWash.Editor.DataBuilder.BuildAll
    /// </summary>
    public static class DataBuilder
    {
        public const string SourceDir = "Assets/_Project/Data/Source";
        public const string GeneratedDir = "Assets/_Project/Data/Generated";
        public const string RegistryPath = GeneratedDir + "/DataRegistry.asset";

        public struct Loaded
        {
            public BalanceData balance;
            public List<ToolData> tools;
            public List<VehicleData> vehicles;
            public List<CritterData> critters;
            public List<UpgradeData> upgrades;
            public List<WeekData> weeks;

            public List<string> Validate() => DataValidator.Validate(balance, tools, vehicles, critters, upgrades, weeks);
        }

        /// <summary>Parses all source JSON into in-memory (non-asset) instances.</summary>
        public static Loaded LoadSources()
        {
            string Read(string f) => File.ReadAllText(Path.Combine(SourceDir, f));
            return new Loaded
            {
                balance = DataJson.LoadSingle<BalanceData>(Read("balance.json")),
                tools = DataJson.LoadItems<ToolData>(Read("tools.json")),
                vehicles = DataJson.LoadItems<VehicleData>(Read("vehicles.json")),
                critters = DataJson.LoadItems<CritterData>(Read("critters.json")),
                upgrades = DataJson.LoadItems<UpgradeData>(Read("upgrades.json")),
                weeks = DataJson.LoadItems<WeekData>(Read("weeks.json")),
            };
        }

        [MenuItem("CarWash/Build/Data")]
        public static void BuildAll()
        {
            var d = LoadSources();
            var errors = d.Validate();
            if (errors.Count > 0) throw new System.Exception("[DataBuilder] validation failed:\n" + string.Join("\n", errors));

            var reg = LoadOrCreate<DataRegistry>(RegistryPath);
            reg.balance = Save(d.balance, GeneratedDir + "/BalanceData.asset", "BalanceData");
            reg.tools = d.tools.Select(t => Save(t, $"{GeneratedDir}/Tools/{t.id}.asset", t.id)).ToArray();
            reg.vehicles = d.vehicles.Select(v => Save(v, $"{GeneratedDir}/Vehicles/{v.id}.asset", v.id)).ToArray();
            reg.critters = d.critters.Select(c => Save(c, $"{GeneratedDir}/Critters/{c.id}.asset", c.id)).ToArray();
            reg.upgrades = d.upgrades.Select(u => Save(u, $"{GeneratedDir}/Upgrades/{u.id}.asset", u.id)).ToArray();
            reg.weeks = d.weeks.Select(w => Save(w, $"{GeneratedDir}/Weeks/{w.id}.asset", w.id)).ToArray();
            EditorUtility.SetDirty(reg);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DataBuilder] OK: {reg.tools.Length} tools, {reg.vehicles.Length} vehicles, {reg.critters.Length} critters, {reg.upgrades.Length} upgrades, {reg.weeks.Length} weeks");
        }

        public static DataRegistry LoadRegistry() => AssetDatabase.LoadAssetAtPath<DataRegistry>(RegistryPath);

        static T Save<T>(T fresh, string path, string name) where T : ScriptableObject
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            fresh.name = name;
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(fresh, path);
                return fresh;
            }
            EditorUtility.CopySerialized(fresh, existing);
            existing.name = name;
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(fresh);
            return existing;
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }
    }
}
