using System.Collections.Generic;
using UnityEngine;

namespace TreeGuardians.Data
{
    /// Central registry of all content definitions. Assigned on the Boot scene's GameConfigProvider.
    [CreateAssetMenu(menuName = "Tree Guardians/Core/Game Database", fileName = "GameDatabase")]
    public sealed class GameDatabase : ScriptableObject
    {
        public List<GuardianDefinition> guardians = new List<GuardianDefinition>();
        public List<ProjectileDefinition> projectiles = new List<ProjectileDefinition>();
        public List<ToolDefinition> tools = new List<ToolDefinition>();
        public List<ArenaDefinition> arenas = new List<ArenaDefinition>();
        public List<ChestDefinition> chests = new List<ChestDefinition>();
        public List<QuestDefinition> quests = new List<QuestDefinition>();
        public List<AchievementDefinition> achievements = new List<AchievementDefinition>();
        public DailyRewardDefinition dailyRewards;
        public TreeDefinition playerTree;
        public TreeDefinition enemyTree;
        public RarityPalette rarityPalette;
        public AudioLibrary audioLibrary;
        public LocalizationTable localization;
        [Tooltip("ProjectileDefinition.prefab boşsa kullanılan varsayılan mermi prefabı.")] public GameObject defaultProjectilePrefab;
        [Tooltip("GuardianDefinition.worldPrefab boşsa kullanılan varsayılan muhafız prefabı.")] public GameObject defaultGuardianPrefab;

        Dictionary<string, GuardianDefinition> guardianMap;
        Dictionary<string, ProjectileDefinition> projectileMap;
        Dictionary<string, ToolDefinition> toolMap;
        Dictionary<string, ArenaDefinition> arenaMap;
        Dictionary<string, ChestDefinition> chestMap;
        Dictionary<string, QuestDefinition> questMap;
        Dictionary<string, AchievementDefinition> achievementMap;

        public void Build()
        {
            guardianMap = BuildMap(guardians, g => g.id);
            projectileMap = BuildMap(projectiles, p => p.id);
            toolMap = BuildMap(tools, t => t.id);
            arenaMap = BuildMap(arenas, a => a.id);
            chestMap = BuildMap(chests, c => c.id);
            questMap = BuildMap(quests, q => q.id);
            achievementMap = BuildMap(achievements, a => a.id);
        }

        static Dictionary<string, T> BuildMap<T>(List<T> list, System.Func<T, string> key) where T : Object
        {
            var map = new Dictionary<string, T>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (item == null) continue;
                var k = key(item);
                if (string.IsNullOrEmpty(k)) continue;
                if (map.ContainsKey(k)) Debug.LogWarning($"[GameDatabase] duplicate id '{k}' in {typeof(T).Name} list.");
                map[k] = item;
            }
            return map;
        }

        public GuardianDefinition GetGuardian(string id) { if (guardianMap == null) Build(); return id != null && guardianMap.TryGetValue(id, out var v) ? v : null; }
        public ProjectileDefinition GetProjectile(string id) { if (projectileMap == null) Build(); return id != null && projectileMap.TryGetValue(id, out var v) ? v : null; }
        public ToolDefinition GetTool(string id) { if (toolMap == null) Build(); return id != null && toolMap.TryGetValue(id, out var v) ? v : null; }
        public ArenaDefinition GetArena(string id) { if (arenaMap == null) Build(); return id != null && arenaMap.TryGetValue(id, out var v) ? v : null; }
        public ChestDefinition GetChest(string id) { if (chestMap == null) Build(); return id != null && chestMap.TryGetValue(id, out var v) ? v : null; }
        public QuestDefinition GetQuest(string id) { if (questMap == null) Build(); return id != null && questMap.TryGetValue(id, out var v) ? v : null; }
        public AchievementDefinition GetAchievement(string id) { if (achievementMap == null) Build(); return id != null && achievementMap.TryGetValue(id, out var v) ? v : null; }

        public ArenaDefinition GetArenaByIndex(int index)
        {
            if (arenaMap == null) Build();
            for (int i = 0; i < arenas.Count; i++)
                if (arenas[i] != null && arenas[i].arenaIndex == index) return arenas[i];
            return arenas.Count > 0 ? arenas[0] : null;
        }

        public ArenaDefinition GetHighestUnlockedArena(int trophies)
        {
            if (arenaMap == null) Build();
            ArenaDefinition best = null;
            for (int i = 0; i < arenas.Count; i++)
            {
                var a = arenas[i];
                if (a == null) continue;
                if (a.unlockTrophies <= trophies && (best == null || a.arenaIndex > best.arenaIndex)) best = a;
            }
            return best ?? (arenas.Count > 0 ? arenas[0] : null);
        }

        public ArenaDefinition GetNextArena(int currentIndex)
        {
            if (arenaMap == null) Build();
            for (int i = 0; i < arenas.Count; i++)
                if (arenas[i] != null && arenas[i].arenaIndex == currentIndex + 1) return arenas[i];
            return null;
        }

        void OnValidate()
        {
            guardianMap = null; projectileMap = null; toolMap = null; arenaMap = null; chestMap = null; questMap = null; achievementMap = null;
            CheckDuplicates(guardians, g => g.id, "guardian");
            CheckDuplicates(tools, t => t.id, "tool");
            CheckDuplicates(arenas, a => a.id, "arena");
            CheckDuplicates(chests, c => c.id, "chest");
        }

        void CheckDuplicates<T>(List<T> list, System.Func<T, string> key, string label) where T : Object
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null) { Debug.LogWarning($"[GameDatabase] null {label} at index {i}.", this); continue; }
                var k = key(list[i]);
                if (!string.IsNullOrEmpty(k) && !seen.Add(k)) Debug.LogWarning($"[GameDatabase] duplicate {label} id '{k}'.", this);
            }
        }
    }
}
