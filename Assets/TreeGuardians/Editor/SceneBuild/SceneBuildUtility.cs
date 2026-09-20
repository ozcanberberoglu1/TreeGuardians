using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TreeGuardians.Editor.SceneBuild
{
    public static class SceneBuildUtility
    {
        public const string ScenesFolder = "Assets/TreeGuardians/Scenes";
        public static readonly string[] SceneNames = { "00_Boot", "01_Loading", "02_MainMenu", "03_Battle", "04_Results", "05_Sandbox" };

        public static string ScenePath(string sceneName) => $"{ScenesFolder}/{sceneName}.unity";

        /// Creates a fresh empty scene (replacing any existing file) and makes it active.
        public static Scene NewScene(string sceneName)
        {
            ContentBuilder.EnsureFolder(ScenesFolder);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = sceneName;
            return scene;
        }

        public static void Save(Scene scene, string sceneName)
        {
            var path = ScenePath(sceneName);
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log("[TG] Saved scene " + path);
        }

        public static void ApplyBuildSettings(bool includeSandbox)
        {
            var list = new List<EditorBuildSettingsScene>();
            foreach (var name in SceneNames)
            {
                var path = ScenePath(name);
                if (!File.Exists(path)) continue;
                bool enabled = name != "05_Sandbox" || includeSandbox;
                list.Add(new EditorBuildSettingsScene(path, enabled));
            }
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log("[TG] Build settings updated with " + list.Count + " scenes.");
        }

        public static void ApplyPlayerSettings()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.productName = "Tree Guardians";
            PlayerSettings.companyName = "Leke Games";
            PlayerSettings.runInBackground = true;
            Debug.Log("[TG] Player settings: landscape only.");
        }

        public static void OpenScene(string sceneName)
        {
            var path = ScenePath(sceneName);
            if (File.Exists(path)) EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }
    }
}
