using System;
using TreeGuardians.Battle;
using TreeGuardians.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TreeGuardians.SceneFlow
{
    /// Boot -> Loading -> MainMenu -> (Loading) -> Battle -> Results -> MainMenu. All scene names come from GameConfig.
    public sealed class SceneFlowService : MonoBehaviour
    {
        GameConfig config;

        public string NextSceneName { get; private set; }
        public bool IsTransitioning { get; private set; }
        AsyncOperation pendingLoad;
        public BattleSetup PendingBattleSetup { get; set; }
        public BattleResultData LastBattleResult { get; set; }

        public string BootScene => config != null ? config.bootScene : "00_Boot";
        public string LoadingScene => config != null ? config.loadingScene : "01_Loading";
        public string MainMenuScene => config != null ? config.mainMenuScene : "02_MainMenu";
        public string BattleScene => config != null ? config.battleScene : "03_Battle";
        public string ResultsScene => config != null ? config.resultsScene : "04_Results";
        public string SandboxScene => config != null ? config.sandboxScene : "05_Sandbox";

        public event Action<string> OnSceneLoadRequested;

        void Awake()
        {
            Services.Register(this);
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        void OnDestroy()
        {
            Services.Unregister(this);
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        public void Initialize(GameConfig gameConfig)
        {
            config = gameConfig;
        }

        void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != LoadingScene)
            {
                IsTransitioning = false;
                if (pendingLoad != null && pendingLoad.isDone) pendingLoad = null;
            }
        }

        /// A held-back async load must be released before any other scene can load (Unity serializes scene loads).
        void ReleasePendingLoad()
        {
            if (pendingLoad == null) return;
            if (!pendingLoad.isDone) pendingLoad.allowSceneActivation = true;
            pendingLoad = null;
            IsTransitioning = false;
        }

        public void LoadViaLoading(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return;
            if (pendingLoad != null && !pendingLoad.isDone) ReleasePendingLoad();
            if (IsTransitioning) return;
            NextSceneName = sceneName;
            IsTransitioning = true;
            OnSceneLoadRequested?.Invoke(sceneName);
            SceneManager.LoadScene(LoadingScene);
        }

        public void LoadDirect(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return;
            if (pendingLoad != null && !pendingLoad.isDone) ReleasePendingLoad();
            if (IsTransitioning) return;
            NextSceneName = sceneName;
            IsTransitioning = true;
            OnSceneLoadRequested?.Invoke(sceneName);
            SceneManager.LoadScene(sceneName);
        }

        /// Called by the Loading scene. Returns the async op with activation held back.
        public AsyncOperation BeginLoadNext()
        {
            var target = string.IsNullOrEmpty(NextSceneName) ? MainMenuScene : NextSceneName;
            var op = SceneManager.LoadSceneAsync(target);
            if (op != null) op.allowSceneActivation = false;
            pendingLoad = op;
            IsTransitioning = true;
            return op;
        }

        /// Loading scene finished its presentation; let the target scene activate.
        public void ActivatePendingLoad()
        {
            if (pendingLoad != null) pendingLoad.allowSceneActivation = true;
        }

        public void GoToMainMenu() => LoadViaLoading(MainMenuScene);

        public void GoToBattle(BattleSetup setup)
        {
            PendingBattleSetup = setup;
            LoadViaLoading(BattleScene);
        }

        public void GoToResults(BattleResultData result)
        {
            LastBattleResult = result;
            LoadDirect(ResultsScene);
        }

        public void RetryLastBattle()
        {
            if (PendingBattleSetup == null) { GoToMainMenu(); return; }
            LoadViaLoading(BattleScene);
        }

        public void GoToSandbox() => LoadDirect(SandboxScene);
    }
}
