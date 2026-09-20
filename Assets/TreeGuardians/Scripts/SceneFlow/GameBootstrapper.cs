using System.Collections;
using TMPro;
using TreeGuardians.Audio;
using TreeGuardians.Chests;
using TreeGuardians.Core;
using TreeGuardians.Localization;
using TreeGuardians.Meta;
using TreeGuardians.Quests;
using TreeGuardians.Rewards;
using TreeGuardians.Save;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TreeGuardians.SceneFlow
{
    /// Idempotent boot: initializes every persistent service in order, then hands off to the Loading scene.
    public sealed class GameBootstrapper : MonoBehaviour
    {
        [Header("Services (children of Boot_Root)")]
        [SerializeField] GameConfigProvider configProvider;
        [SerializeField] SaveService saveService;
        [SerializeField] PlayerProgressService progressService;
        [SerializeField] LocalizationService localizationService;
        [SerializeField] AudioService audioService;
        [SerializeField] HapticService hapticService;
        [SerializeField] InputService inputService;
        [SerializeField] TweenRunner tweenRunner;
        [SerializeField] SceneFlowService sceneFlowService;
        [SerializeField] RewardService rewardService;
        [SerializeField] ChestService chestService;
        [SerializeField] QuestService questService;

        [Header("Boot UI (optional, scene-local)")]
        [SerializeField] TMP_Text statusText;
        [SerializeField] TMP_Text versionText;
        [SerializeField] TMP_Text studioText;
        [SerializeField] CanvasGroup logoGroup;
        [SerializeField] float minBootSeconds = 0.8f;

        static GameBootstrapper instance;

        public static bool IsBooted => Services.IsBootstrapped;

        void Awake()
        {
            if (instance != null && instance != this)
            {
                // Boot scene entered again while services already live: skip re-boot, continue the flow.
                if (Services.IsBootstrapped) instance.ContinueFromBootScene();
                Destroy(transform.root.gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(transform.root.gameObject);
            Application.runInBackground = true;
            Services.Register(this);
        }

        void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
                Services.Unregister(this);
            }
        }

        IEnumerator Start()
        {
            if (instance != this) yield break;
            if (Services.IsBootstrapped) yield break;

            float startTime = Time.realtimeSinceStartup;
            var cfg = configProvider != null ? configProvider.Config : null;
            if (cfg == null)
            {
                TGLog.Error("GameBootstrapper: GameConfigProvider/GameConfig missing. Cannot boot.");
                yield break;
            }

            if (versionText != null) versionText.text = cfg.gameName + " v" + cfg.version;
            if (studioText != null) studioText.text = cfg.studioName;
            if (logoGroup != null) { logoGroup.alpha = 0f; TGTween.FadeCanvasGroup(logoGroup, 1f, 0.4f); }

            SetStatus("Loading save...");
            configProvider.Database.Build();
            saveService.Initialize();
            yield return null;

            SetStatus("Preparing profile...");
            progressService.Initialize(saveService, configProvider);
            var data = saveService.Data;
            localizationService.Initialize(configProvider.Database.localization, data.settings, saveService);
            audioService.Initialize(configProvider.Database.audioLibrary, data.settings);
            hapticService.Initialize(data.settings);
            chestService.Initialize(progressService, saveService, saveService.IsNewSave);
            questService.Initialize(progressService, saveService);
            rewardService.Initialize(progressService, chestService, saveService);
            sceneFlowService.Initialize(cfg);
            QualityApplier.Apply(data.settings.qualityTier, data.settings.reduceMotion, cfg);
            yield return null;

            if (saveService.IsNewSave) saveService.SaveNow();

            Services.IsBootstrapped = true;
            GameEventBus.Publish(new ServicesReadyEvent());
            SetStatus(localizationService.Get("boot_ready"));
            TGLog.Info($"Boot complete. New save: {saveService.IsNewSave}, recovered: {saveService.RecoveredFromBackup}, failed: {saveService.LoadFailed}");

            while (Time.realtimeSinceStartup - startTime < minBootSeconds) yield return null;

            if (SceneManager.GetActiveScene().name == cfg.bootScene)
                sceneFlowService.LoadViaLoading(cfg.mainMenuScene);
        }

        void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }

        void ContinueFromBootScene()
        {
            var cfg = configProvider != null ? configProvider.Config : null;
            if (cfg == null || sceneFlowService == null) return;
            TGTween.Delay(0.1f, () => { if (sceneFlowService != null) sceneFlowService.LoadViaLoading(cfg.mainMenuScene); });
        }
    }
}
