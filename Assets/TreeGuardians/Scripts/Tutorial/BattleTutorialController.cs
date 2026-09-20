using TMPro;
using TreeGuardians.Battle;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Localization;
using TreeGuardians.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.Tutorial
{
    /// Data-driven first-battle tutorial. Never blocks input; steps advance from gameplay events. Skippable, replayable from Settings.
    public sealed class BattleTutorialController : MonoBehaviour
    {
        [SerializeField] BattleManager manager;
        [SerializeField] PlayerBattleCommander commander;
        [SerializeField] GameObject overlayRoot;
        [SerializeField] CanvasGroup overlayGroup;
        [SerializeField] TMP_Text stepText;
        [SerializeField] Button skipButton;
        [SerializeField] RectTransform spotlight;
        [SerializeField] RectTransform guardianBarAnchor;
        [SerializeField] RectTransform toolBarAnchor;
        [SerializeField] string[] stepKeys = { "tut_step_1", "tut_step_2", "tut_step_3", "tut_step_4", "tut_step_5", "tut_step_6" };

        int step = -1;
        bool running;
        bool waitingForContext = true;
        bool shown;
        PlayerProgressService progress;
        float pulse;

        void Awake()
        {
            if (skipButton != null) skipButton.onClick.AddListener(Skip);
            if (overlayRoot != null) overlayRoot.SetActive(false);
        }

        void OnDestroy()
        {
            Unsubscribe();
        }

        void Update()
        {
            if (waitingForContext)
            {
                if (!Services.IsBootstrapped || manager == null || manager.Context == null) return;
                waitingForContext = false;
                Begin();
                return;
            }
            if (!running) return;
            if (!shown && manager.StateMachine.State == BattleState.Playing) Show(step);
            if (spotlight != null && spotlight.gameObject.activeSelf && !QualityApplier.ReduceMotion)
            {
                pulse += Time.unscaledDeltaTime * 3f;
                spotlight.localScale = Vector3.one * (1f + Mathf.Sin(pulse) * 0.08f);
            }
        }

        void Begin()
        {
            progress = Services.Get<PlayerProgressService>();
            var ctx = manager.Context;
            if (ctx == null || ctx.setup == null || !ctx.setup.isTutorial || progress == null || progress.Data.tutorial.skipped || progress.Data.tutorial.battleTutorialDone)
            {
                enabled = false;
                return;
            }
            step = Mathf.Clamp(progress.Data.tutorial.lastStep, 0, stepKeys.Length - 1);
            running = true;
            GameEventBus.Subscribe<ProjectileFiredEvent>(OnFired);
            GameEventBus.Subscribe<SectionDamagedEvent>(OnSectionDamaged);
            GameEventBus.Subscribe<SectionDestroyedEvent>(OnSectionDestroyed);
            GameEventBus.Subscribe<ToolUsedEvent>(OnToolUsed);
            GameEventBus.Subscribe<BattleFinishedEvent>(OnFinished);
            if (commander != null) commander.OnSelectionChanged += OnSelectionChanged;
        }

        void Unsubscribe()
        {
            GameEventBus.Unsubscribe<ProjectileFiredEvent>(OnFired);
            GameEventBus.Unsubscribe<SectionDamagedEvent>(OnSectionDamaged);
            GameEventBus.Unsubscribe<SectionDestroyedEvent>(OnSectionDestroyed);
            GameEventBus.Unsubscribe<ToolUsedEvent>(OnToolUsed);
            GameEventBus.Unsubscribe<BattleFinishedEvent>(OnFinished);
            if (commander != null) commander.OnSelectionChanged -= OnSelectionChanged;
        }

        void Show(int index)
        {
            shown = true;
            if (overlayRoot != null) overlayRoot.SetActive(true);
            if (overlayGroup != null) { overlayGroup.alpha = 0f; TGTween.FadeCanvasGroup(overlayGroup, 1f, 0.2f); }
            if (stepText != null) stepText.text = LocalizationService.Tr(stepKeys[index]);
            if (spotlight != null)
            {
                RectTransform target = index <= 1 ? guardianBarAnchor : index == 4 ? toolBarAnchor : null;
                spotlight.gameObject.SetActive(target != null);
                if (target != null)
                {
                    spotlight.position = target.position;
                    spotlight.sizeDelta = new Vector2(target.rect.width + 40f, target.rect.height + 40f);
                }
            }
            if (index == 4 && manager.Context.playerTools != null)
            {
                bool anyTool = false;
                for (int i = 0; i < manager.Context.playerTools.Count; i++) if (manager.Context.playerTools.Get(i).def != null) anyTool = true;
                if (!anyTool) Advance();
            }
        }

        void Advance()
        {
            if (!running) return;
            step++;
            if (progress != null) progress.Data.tutorial.lastStep = step;
            if (step >= stepKeys.Length) { Complete(); return; }
            Show(step);
        }

        void Complete()
        {
            running = false;
            if (progress != null)
            {
                progress.Data.tutorial.battleTutorialDone = true;
                progress.Data.tutorial.lastStep = 0;
                progress.Save();
            }
            Hide();
            Unsubscribe();
        }

        void Skip()
        {
            if (progress != null)
            {
                progress.Data.tutorial.skipped = true;
                progress.Data.tutorial.battleTutorialDone = true;
                progress.Save();
            }
            running = false;
            Hide();
            Unsubscribe();
        }

        void Hide()
        {
            if (overlayGroup != null) TGTween.FadeCanvasGroup(overlayGroup, 0f, 0.2f, true, () => { if (overlayRoot != null) overlayRoot.SetActive(false); });
            else if (overlayRoot != null) overlayRoot.SetActive(false);
        }

        void OnSelectionChanged() { if (running && shown && step == 0) Advance(); }
        void OnFired(ProjectileFiredEvent e) { if (running && e.side == BattleSide.Player && step == 1) Advance(); }
        void OnSectionDamaged(SectionDamagedEvent e) { if (running && e.side == BattleSide.Enemy && step == 2) Advance(); }
        void OnSectionDestroyed(SectionDestroyedEvent e) { if (running && e.side == BattleSide.Enemy && step == 3) Advance(); }
        void OnToolUsed(ToolUsedEvent e) { if (running && e.side == BattleSide.Player && step == 4) Advance(); }
        void OnFinished(BattleFinishedEvent e) { if (running) Complete(); }
    }
}
