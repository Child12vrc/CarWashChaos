using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>Host save (TECH S14). JSON in persistentDataPath. Full use in T5.2; stub API now.</summary>
    public class SaveSystem
    {
        [Serializable]
        public class SaveData
        {
            public int version = 1;
            public int weekUnlocked = 1;
            public List<string> upgradeIds = new List<string>();
            public List<int> upgradeLevels = new List<int>();
            public List<string> cosmetics = new List<string>();
        }

        public SaveData Current { get; private set; } = new SaveData();
        readonly string _path;

        public SaveSystem(string profileId = "local")
        {
            _path = Path.Combine(Application.persistentDataPath, $"save_{profileId}.json");
        }

        public bool HasSave => File.Exists(_path);

        public void Save() => File.WriteAllText(_path, JsonUtility.ToJson(Current, true));

        public bool Load()
        {
            if (!HasSave) return false;
            try { Current = JsonUtility.FromJson<SaveData>(File.ReadAllText(_path)) ?? new SaveData(); return true; }
            catch (Exception e) { Debug.LogWarning("[Save] corrupt save, ignoring: " + e.Message); Current = new SaveData(); return false; }
        }
    }
}
