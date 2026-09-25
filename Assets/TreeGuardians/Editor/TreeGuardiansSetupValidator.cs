using System.Collections.Generic;
using System.IO;
using System.Text;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Editor.SceneBuild;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TreeGuardians.Editor
{
    /// Reports missing scenes, build settings, EventSystems, cameras, configs, duplicate ids and missing references.
    public static class TreeGuardiansSetupValidator
    {
        static readonly Dictionary<string, string[]> RequiredRoots = new Dictionary<string, string[]>
        {
            { "00_Boot", new[] { "Boot_Root", "Main Camera", "BootCanvas", "EventSystem" } },
            { "01_Loading", new[] { "Loading_Root", "Main Camera", "LoadingCanvas", "LoadingController", "EventSystem" } },
            { "02_MainMenu", new[] { "MainMenu_Root", "Main Camera", "Environment", "MenuCanvas", "MenuUIController", "MenuEnvironmentAnimator", "EventSystem" } },
            { "03_Battle", new[] { "Battle_Root", "BattleCamera", "BattleEnvironment", "ArenaBounds", "PlayerSide", "EnemySide", "ProjectilePoolRoot", "VFXPoolRoot", "DamageTextPoolRoot", "BattleSystems", "BattleCanvas", "EventSystem" } },
            { "04_Results", new[] { "Results_Root", "ResultsCamera", "ResultsCanvas", "ResultsController", "EventSystem" } },
            { "05_Sandbox", new[] { "Battle_Root", "BattleCamera", "BattleSystems", "BattleCanvas", "SandboxCanvas", "EventSystem" } },
        };

        [MenuItem("Tree Guardians/Validate Setup", priority = 50)]
        public static void ValidateMenu()
        {
            // Scene switching inside a menu/command callback can upset the caller's iteration; run on the next editor tick.
            EditorApplication.delayCall += Validate;
        }

        public static void Validate()
        {
            var report = new StringBuilder();
            int errors = 0, warnings = 0;
            void Err(string m) { errors++; report.AppendLine("ERROR: " + m); }
            void Warn(string m) { warnings++; report.AppendLine("WARN:  " + m); }

            // Assets
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ContentBuilder.GameConfigPath);
            var balance = AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(ContentBuilder.BalancePath);
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(ContentBuilder.DatabasePath);
            if (config == null) Err("GameConfig missing at " + ContentBuilder.GameConfigPath);
            if (balance == null) Err("GameBalanceConfig missing at " + ContentBuilder.BalancePath);
            if (db == null) Err("GameDatabase missing at " + ContentBuilder.DatabasePath);
            if (!File.Exists(UIFactory.FontPath)) Warn("TG font asset missing: " + UIFactory.FontPath);
            if (!File.Exists(BootSceneBuilder.BootRootPrefabPath)) Err("Boot root prefab missing (ServicesFallback): " + BootSceneBuilder.BootRootPrefabPath);

            if (db != null)
            {
                db.Build();
                CheckIds(db.guardians, g => g.id, "guardian", Err);
                CheckIds(db.projectiles, p => p.id, "projectile", Err);
                CheckIds(db.tools, t => t.id, "tool", Err);
                CheckIds(db.arenas, a => a.id, "arena", Err);
                CheckIds(db.chests, c => c.id, "chest", Err);
                CheckIds(db.quests, q => q.id, "quest", Err);
                var keys = new HashSet<string>();
                if (db.localization != null)
                {
                    foreach (var e in db.localization.entries)
                        if (e != null && !string.IsNullOrEmpty(e.key)) keys.Add(e.key);
                }
                else Err("GameDatabase.localization is not assigned.");
                foreach (var g in db.guardians)
                {
                    if (g == null) continue;
                    if (g.portrait == null) Warn($"Guardian '{g.id}' has no portrait.");
                    if (g.worldSprite == null && g.worldPrefab == null) Warn($"Guardian '{g.id}' has no world sprite/prefab.");
                    if (g.normalProjectile == null) Err($"Guardian '{g.id}' has no normal projectile.");
                    if (!keys.Contains(g.nameKey)) Warn($"Guardian '{g.id}' nameKey '{g.nameKey}' missing in localization.");
                    if (!keys.Contains(g.descriptionKey)) Warn($"Guardian '{g.id}' descriptionKey missing in localization.");
                }
                foreach (var c in db.chests)
                {
                    if (c == null) continue;
                    if (c.iconClosed == null || c.iconOpen == null) Warn($"Chest '{c.id}' has no closed/open art. Assign it on MainMenu > BottomBar > ChestSlots (ChestSlotsView).");
                    if (!keys.Contains(c.nameKey)) Warn($"Chest '{c.id}' nameKey '{c.nameKey}' missing in localization.");
                }
                foreach (var t in db.tools)
                {
                    if (t == null) continue;
                    if ((t.effect == ToolEffectType.Projectile || t.effect == ToolEffectType.HomingSwarm) && t.projectile == null) Err($"Tool '{t.id}' needs a projectile.");
                    if (!keys.Contains(t.nameKey)) Warn($"Tool '{t.id}' nameKey missing in localization.");
                }
                foreach (var a in db.arenas)
                {
                    if (a == null) continue;
                    foreach (var id in a.botGuardianIds) if (db.GetGuardian(id) == null) Err($"Arena '{a.id}' references unknown guardian '{id}'.");
                    foreach (var id in a.botToolIds) if (db.GetTool(id) == null) Err($"Arena '{a.id}' references unknown tool '{id}'.");
                    foreach (var d in a.chestDrops) if (db.GetChest(d.chestId) == null) Err($"Arena '{a.id}' references unknown chest '{d.chestId}'.");
                    if (!keys.Contains(a.nameKey)) Warn($"Arena '{a.id}' nameKey missing in localization.");
                }
                if (db.playerTree == null || db.enemyTree == null) Err("GameDatabase trees are not assigned.");
                if (db.dailyRewards == null) Warn("GameDatabase.dailyRewards not assigned.");
                if (db.defaultProjectilePrefab == null) Err("GameDatabase.defaultProjectilePrefab not assigned (build the Battle scene).");
                if (balance != null)
                {
                    foreach (var id in balance.starterGuardianIds) if (db.GetGuardian(id) == null) Err($"Balance starter guardian '{id}' unknown.");
                    foreach (var id in balance.starterToolIds) if (db.GetTool(id) == null) Err($"Balance starter tool '{id}' unknown.");
                    if (!string.IsNullOrEmpty(balance.starterChestId) && db.GetChest(balance.starterChestId) == null) Err("Balance starter chest unknown.");
                }
            }

            // Build settings
            var buildScenes = EditorBuildSettings.scenes;
            int expectedSections = (db != null && db.playerTree != null ? db.playerTree.sections.Count : 17) + (db != null && db.enemyTree != null ? db.enemyTree.sections.Count : 17);
            for (int i = 0; i < SceneBuildUtility.SceneNames.Length; i++)
            {
                var path = SceneBuildUtility.ScenePath(SceneBuildUtility.SceneNames[i]);
                if (!File.Exists(path)) { Err("Scene missing: " + path); continue; }
                int idx = System.Array.FindIndex(buildScenes, s => s.path == path);
                if (idx < 0) Err("Scene not in Build Settings: " + path);
                else if (idx != i) Warn($"Scene order: {path} is at index {idx}, expected {i}.");
                else if (!buildScenes[idx].enabled && SceneBuildUtility.SceneNames[i] != "05_Sandbox") Err("Scene disabled in Build Settings: " + path);
            }

            // Scene contents
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (var name in SceneBuildUtility.SceneNames)
                {
                    var path = SceneBuildUtility.ScenePath(name);
                    if (!File.Exists(path)) continue;
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var roots = scene.GetRootGameObjects();
                    var rootNames = new HashSet<string>();
                    int eventSystems = 0, cameras = 0, listeners = 0, missingRefs = 0, missingScripts = 0;
                    foreach (var r in roots)
                    {
                        rootNames.Add(r.name);
                        eventSystems += r.GetComponentsInChildren<EventSystem>(true).Length;
                        cameras += r.GetComponentsInChildren<Camera>(true).Length;
                        listeners += r.GetComponentsInChildren<AudioListener>(true).Length;
                        foreach (var mb in r.GetComponentsInChildren<MonoBehaviour>(true))
                        {
                            if (mb == null) { missingScripts++; continue; }
                            var so = new SerializedObject(mb);
                            var it = so.GetIterator();
                            while (it.NextVisible(true))
                                if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue == null && it.objectReferenceInstanceIDValue != 0)
                                    missingRefs++;
                        }
                    }
                    if (RequiredRoots.TryGetValue(name, out var req))
                        foreach (var rn in req) if (!rootNames.Contains(rn)) Err($"{name}: required root object '{rn}' missing.");
                    if (eventSystems != 1) Err($"{name}: expected exactly one EventSystem, found {eventSystems}.");
                    if (cameras < 1) Err($"{name}: no camera.");
                    if (listeners > 1) Warn($"{name}: {listeners} AudioListeners.");
                    if (missingRefs > 0) Err($"{name}: {missingRefs} missing (destroyed) object references.");
                    if (missingScripts > 0) Err($"{name}: {missingScripts} missing scripts.");
                    if (name == "03_Battle" || name == "05_Sandbox")
                    {
                        int sections = 0; foreach (var r in roots) sections += r.GetComponentsInChildren<Trees.TreeSection>(true).Length;
                        if (sections < expectedSections) Warn($"{name}: only {sections} TreeSections (expected {expectedSections} from the tree definitions).");
                    }
                }
            }
            finally
            {
                if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }

            string header = $"[TG] Setup validation: {errors} error(s), {warnings} warning(s).";
            if (errors > 0) Debug.LogError(header + "\n" + report);
            else if (warnings > 0) Debug.LogWarning(header + "\n" + report);
            else Debug.Log(header + " Everything looks good.");
        }

        static void CheckIds<T>(List<T> list, System.Func<T, string> key, string label, System.Action<string> err) where T : Object
        {
            var seen = new HashSet<string>();
            if (list == null || list.Count == 0) { err($"No {label}s in GameDatabase."); return; }
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null) { err($"Null {label} at index {i}."); continue; }
                var k = key(list[i]);
                if (string.IsNullOrWhiteSpace(k)) err($"{label} at index {i} has empty id.");
                else if (!seen.Add(k)) err($"Duplicate {label} id '{k}'.");
            }
        }
    }
}
