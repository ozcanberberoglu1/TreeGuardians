using System;
using System.Collections.Generic;
using UnityEngine;

namespace TreeGuardians.Data
{
    [Serializable]
    public sealed class LocalizationEntry
    {
        public string key;
        [TextArea(1, 3)] public string en;
        [TextArea(1, 3)] public string tr;
    }

    [CreateAssetMenu(menuName = "Tree Guardians/Localization/Localization Table", fileName = "LocalizationTable")]
    public sealed class LocalizationTable : ScriptableObject
    {
        public List<LocalizationEntry> entries = new List<LocalizationEntry>();
        [Tooltip("Loading ekranında dönüşümlü gösterilen ipucu anahtarları.")]
        public List<string> loadingTipKeys = new List<string>();

        public void BuildInto(Dictionary<string, string> en, Dictionary<string, string> tr)
        {
            en.Clear();
            tr.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null || string.IsNullOrEmpty(e.key)) continue;
                en[e.key] = e.en ?? "";
                tr[e.key] = string.IsNullOrEmpty(e.tr) ? (e.en ?? "") : e.tr;
            }
        }

        void OnValidate()
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null || string.IsNullOrEmpty(e.key)) continue;
                if (!seen.Add(e.key)) Debug.LogWarning($"[LocalizationTable] duplicate key '{e.key}'.", this);
            }
        }
    }
}
