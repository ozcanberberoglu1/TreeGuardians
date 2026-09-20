using TreeGuardians.Core;
using UnityEditor;
using UnityEngine;

namespace TreeGuardians.Editor.SceneBuild
{
    public static class SceneBuildMenu
    {
        [MenuItem("Tree Guardians/3. Build Core Scenes (Boot, Loading, MainMenu, Results)", priority = 10)]
        public static void BuildCoreScenes()
        {
            BootSceneBuilder.Build();
            LoadingSceneBuilder.Build();
            ResultsSceneBuilder.Build();
            MainMenuSceneBuilder.Build();
            ApplySettings();
        }

        [MenuItem("Tree Guardians/3a. Build Boot Scene", priority = 11)]
        public static void BuildBoot() { BootSceneBuilder.Build(); ApplySettings(); }

        [MenuItem("Tree Guardians/3b. Build Loading Scene", priority = 12)]
        public static void BuildLoading() { LoadingSceneBuilder.Build(); ApplySettings(); }

        [MenuItem("Tree Guardians/3c. Build MainMenu Scene", priority = 13)]
        public static void BuildMainMenu() { MainMenuSceneBuilder.Build(); ApplySettings(); }

        [MenuItem("Tree Guardians/3d. Build Results Scene", priority = 14)]
        public static void BuildResults() { ResultsSceneBuilder.Build(); ApplySettings(); }

        [MenuItem("Tree Guardians/4. Build Battle && Sandbox Scenes", priority = 15)]
        public static void BuildBattleScenes()
        {
            BattleSceneBuilder.Build();
            BattleSceneBuilder.BuildSandbox();
            ApplySettings();
        }

        [MenuItem("Tree Guardians/4a. Build Battle Scene", priority = 16)]
        public static void BuildBattle() { BattleSceneBuilder.Build(); ApplySettings(); }

        [MenuItem("Tree Guardians/4b. Build Sandbox Scene", priority = 17)]
        public static void BuildSandbox() { BattleSceneBuilder.BuildSandbox(); ApplySettings(); }

        [MenuItem("Tree Guardians/9. Build Everything (Art, Content, Scenes)", priority = 20)]
        public static void BuildEverything()
        {
            PlaceholderArtGenerator.GenerateAll();
            FontBuilder.BuildFont();
            ContentBuilder.BuildAll();
            BootSceneBuilder.Build();
            LoadingSceneBuilder.Build();
            ResultsSceneBuilder.Build();
            BattleSceneBuilder.Build();
            BattleSceneBuilder.BuildSandbox();
            MainMenuSceneBuilder.Build();
            ContentBuilder.BuildAll();
            ApplySettings();
        }

        [MenuItem("Tree Guardians/Apply Build && Player Settings", priority = 30)]
        public static void ApplySettings()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ContentBuilder.GameConfigPath);
            SceneBuildUtility.ApplyBuildSettings(config != null && config.includeSandboxInBuild);
            SceneBuildUtility.ApplyPlayerSettings();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tree Guardians/Open MainMenu Scene", priority = 31)]
        public static void OpenMainMenu() => SceneBuildUtility.OpenScene("02_MainMenu");

        [MenuItem("Tree Guardians/Open Boot Scene", priority = 32)]
        public static void OpenBoot() => SceneBuildUtility.OpenScene("00_Boot");
    }
}
