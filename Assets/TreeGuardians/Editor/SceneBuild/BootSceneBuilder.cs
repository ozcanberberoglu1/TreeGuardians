using TMPro;
using TreeGuardians.Audio;
using TreeGuardians.Chests;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Localization;
using TreeGuardians.Meta;
using TreeGuardians.Quests;
using TreeGuardians.Rewards;
using TreeGuardians.Save;
using TreeGuardians.SceneFlow;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.Editor.SceneBuild
{
    /// 00_Boot: persistent Boot_Root (services) + scene-local boot canvas. Boot_Root is also saved as Resources/TG_BootRoot.prefab.
    public static class BootSceneBuilder
    {
        public const string BootRootPrefabPath = "Assets/TreeGuardians/Resources/TG_BootRoot.prefab";

        public static void Build()
        {
            var scene = SceneBuildUtility.NewScene("00_Boot");

            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ContentBuilder.GameConfigPath);
            var balance = AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(ContentBuilder.BalancePath);
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(ContentBuilder.DatabasePath);

            var root = UIFactory.Root("Boot_Root");
            var bootstrapper = UIFactory.Child("Bootstrapper", root.transform).AddComponent<GameBootstrapper>();
            var provider = UIFactory.Child("GameConfigProvider", root.transform).AddComponent<GameConfigProvider>();
            var save = UIFactory.Child("SaveService", root.transform).AddComponent<SaveService>();
            var progress = UIFactory.Child("PlayerProgressService", root.transform).AddComponent<PlayerProgressService>();
            var loc = UIFactory.Child("LocalizationService", root.transform).AddComponent<LocalizationService>();
            var audioGo = UIFactory.Child("AudioService", root.transform);
            var audio = audioGo.AddComponent<AudioService>();
            var haptic = UIFactory.Child("HapticService", root.transform).AddComponent<HapticService>();
            var input = UIFactory.Child("InputService", root.transform).AddComponent<InputService>();
            var tween = UIFactory.Child("TweenRunner", root.transform).AddComponent<TweenRunner>();
            var flow = UIFactory.Child("SceneFlowService", root.transform).AddComponent<SceneFlowService>();
            var rewards = UIFactory.Child("RewardService", root.transform).AddComponent<RewardService>();
            var chests = UIFactory.Child("ChestService", root.transform).AddComponent<ChestService>();
            var quests = UIFactory.Child("QuestService", root.transform).AddComponent<QuestService>();

            UIFactory.Set(provider, "config", config);
            UIFactory.Set(provider, "balance", balance);
            UIFactory.Set(provider, "database", db);

            // Audio voices pre-created so nothing is spawned at runtime.
            var musicA = CreateVoice("Music_A", audioGo.transform, true);
            var musicB = CreateVoice("Music_B", audioGo.transform, true);
            var sfx = new Object[8];
            for (int i = 0; i < 8; i++) sfx[i] = CreateVoice("Sfx_" + i, audioGo.transform, false);
            var ui = new Object[3];
            for (int i = 0; i < 3; i++) ui[i] = CreateVoice("Ui_" + i, audioGo.transform, false);
            UIFactory.Set(audio, "musicSourceA", musicA);
            UIFactory.Set(audio, "musicSourceB", musicB);
            UIFactory.SetArray(audio, "sfxSources", sfx);
            UIFactory.SetArray(audio, "uiSources", ui);

            UIFactory.Set(bootstrapper, "configProvider", provider);
            UIFactory.Set(bootstrapper, "saveService", save);
            UIFactory.Set(bootstrapper, "progressService", progress);
            UIFactory.Set(bootstrapper, "localizationService", loc);
            UIFactory.Set(bootstrapper, "audioService", audio);
            UIFactory.Set(bootstrapper, "hapticService", haptic);
            UIFactory.Set(bootstrapper, "inputService", input);
            UIFactory.Set(bootstrapper, "tweenRunner", tween);
            UIFactory.Set(bootstrapper, "sceneFlowService", flow);
            UIFactory.Set(bootstrapper, "rewardService", rewards);
            UIFactory.Set(bootstrapper, "chestService", chests);
            UIFactory.Set(bootstrapper, "questService", quests);

            // Save prefab (used by ServicesFallback when a scene is played directly), then link the scene instance to it.
            ContentBuilder.EnsureFolder("Assets/TreeGuardians/Resources");
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, BootRootPrefabPath, InteractionMode.AutomatedAction);

            // Scene-local camera and boot UI.
            UIFactory.Camera2D("Main Camera", 5.4f, new Color(0.09f, 0.12f, 0.16f));
            var canvas = UIFactory.Canvas("BootCanvas");
            var safe = UIFactory.SafeArea(canvas.transform);
            var bg = UIFactory.Image("Background", safe, UIFactory.Ui("white"), new Color(0.09f, 0.12f, 0.16f), false, false);
            UIFactory.Stretch((RectTransform)bg.transform, -50f, -50f, -50f, -50f);
            var logo = UIFactory.Image("StudioLogo", safe, UIFactory.Sprite(ArtPaths.Logo("studio")), Color.white, false, false);
            UIFactory.Anchor((RectTransform)logo.transform, new Vector2(0.5f, 0.56f), Vector2.zero, new Vector2(260f, 260f));
            var logoGroup = UIFactory.Group(logo.gameObject, 0f);
            var studio = UIFactory.Text("StudioText", safe, null, 44f, UIFactory.TextLight, TextAlignmentOptions.Center, true, "Leke Games");
            UIFactory.Anchor((RectTransform)studio.transform, new Vector2(0.5f, 0.36f), Vector2.zero, new Vector2(800f, 70f));
            var status = UIFactory.Text("StatusText", safe, null, 28f, new Color(0.7f, 0.75f, 0.8f), TextAlignmentOptions.Center, false, "...");
            UIFactory.Anchor((RectTransform)status.transform, new Vector2(0.5f, 0.22f), Vector2.zero, new Vector2(900f, 50f));
            var version = UIFactory.Text("VersionText", safe, null, 24f, new Color(0.5f, 0.55f, 0.6f), TextAlignmentOptions.Right, false, "v0.1.0");
            UIFactory.Anchor((RectTransform)version.transform, new Vector2(1f, 0f), new Vector2(-30f, 20f), new Vector2(500f, 40f), new Vector2(1f, 0f));

            var sceneBootstrapper = root.GetComponentInChildren<GameBootstrapper>();
            UIFactory.Set(sceneBootstrapper, "statusText", status);
            UIFactory.Set(sceneBootstrapper, "versionText", version);
            UIFactory.Set(sceneBootstrapper, "studioText", studio);
            UIFactory.Set(sceneBootstrapper, "logoGroup", logoGroup);

            UIFactory.EventSystem();
            SceneBuildUtility.Save(scene, "00_Boot");
        }

        static AudioSource CreateVoice(string name, Transform parent, bool loop)
        {
            var go = UIFactory.Child(name, parent);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.ignoreListenerPause = true;
            return s;
        }
    }
}
