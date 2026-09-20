using System.Collections.Generic;
using TMPro;
using TreeGuardians.Audio;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Localization;
using TreeGuardians.Meta;
using TreeGuardians.Rewards;
using TreeGuardians.SceneFlow;
using TreeGuardians.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.Battle
{
    /// 04_Results: shows outcome, stars, stats and rewards (already applied by the battle) with staged reveals.
    public sealed class ResultsController : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text arenaText;
        [SerializeField] Image[] stars = new Image[3];
        [SerializeField] Color starOn = new Color(0.98f, 0.8f, 0.3f);
        [SerializeField] Color starOff = new Color(1f, 1f, 1f, 0.2f);

        [Header("Tree summary")]
        [SerializeField] Image playerCoreFill;
        [SerializeField] Image enemyCoreFill;
        [SerializeField] TMP_Text playerCoreText;
        [SerializeField] TMP_Text enemyCoreText;

        [Header("Stats")]
        [SerializeField] TMP_Text damageText;
        [SerializeField] TMP_Text coreLeftText;
        [SerializeField] TMP_Text aliveText;
        [SerializeField] TMP_Text accuracyText;
        [SerializeField] TMP_Text trophyText;

        [Header("Rewards")]
        [SerializeField] RewardItemView[] tiles = new RewardItemView[6];
        [SerializeField] TMP_Text chestNoteText;
        [SerializeField] RewardSprites sprites = new RewardSprites();
        [SerializeField] float revealDelay = 0.18f;

        [Header("Unlock")]
        [SerializeField] GameObject unlockPanel;
        [SerializeField] TMP_Text unlockText;
        [SerializeField] Image unlockIcon;

        [Header("Tutorial")]
        [SerializeField] TMP_Text tutorialHintText;

        [Header("Buttons")]
        [SerializeField] Button continueButton;
        [SerializeField] Button retryButton;
        [SerializeField] Button homeButton;
        [SerializeField] TransitionOverlay transition;

        readonly List<RewardItem> items = new List<RewardItem>(12);
        BattleResultData result;

        void Awake()
        {
            if (continueButton != null) continueButton.onClick.AddListener(() => Leave(false));
            if (homeButton != null) homeButton.onClick.AddListener(() => Leave(false));
            if (retryButton != null) retryButton.onClick.AddListener(() => Leave(true));
            if (unlockPanel != null) unlockPanel.SetActive(false);
        }

        void Start()
        {
            BootAwaiter.WhenReady(Init);
        }

        void Init()
        {
            if (this == null) return;
            var flow = Services.Get<SceneFlowService>();
            var progress = Services.Get<PlayerProgressService>();
            result = flow != null ? flow.LastBattleResult : null;
            if (result == null)
            {
                result = new BattleResultData { outcome = BattleOutcome.Draw, reason = BattleEndReason.TimeUp, rewardsApplied = true };
                TGLog.Warn("ResultsController: no battle result found; showing placeholder.");
            }
            if (!result.rewardsApplied && progress != null)
            {
                var applied = Services.Get<RewardService>()?.Apply(result.rewards, "battle");
                result.rewardsApplied = true;
                if (applied != null) { result.unlockedGuardianIds.AddRange(applied.unlockedGuardianIds); result.unlockedToolIds.AddRange(applied.unlockedToolIds); }
            }

            if (tutorialHintText != null)
            {
                bool tutorial = flow != null && flow.PendingBattleSetup != null && flow.PendingBattleSetup.isTutorial && progress != null && !progress.Data.tutorial.skipped;
                tutorialHintText.gameObject.SetActive(tutorial);
                if (tutorial) tutorialHintText.text = LocalizationService.Tr("tut_step_7");
            }

            var audio = Services.Get<AudioService>();
            audio?.PlayMusic(MusicTrackId.Results);
            audio?.PlaySfx(result.outcome == BattleOutcome.Victory ? AudioEventId.Victory : AudioEventId.Defeat);

            if (titleText != null)
                titleText.text = LocalizationService.Tr(result.outcome == BattleOutcome.Victory ? "results_title_victory" : result.outcome == BattleOutcome.Defeat ? "results_title_defeat" : "results_title_draw");
            var arena = progress != null ? progress.Database.GetArena(result.arenaId) : null;
            if (arenaText != null) arenaText.text = arena != null ? LocalizationService.Tr(arena.nameKey) : "";

            if (playerCoreFill != null) playerCoreFill.fillAmount = Mathf.Clamp01(result.playerCorePercent);
            if (enemyCoreFill != null) enemyCoreFill.fillAmount = Mathf.Clamp01(result.enemyCorePercent);
            if (playerCoreText != null) playerCoreText.text = $"{LocalizationService.Tr("battle_you")}  {Mathf.RoundToInt(result.playerCorePercent * 100f)}%";
            if (enemyCoreText != null) enemyCoreText.text = $"{LocalizationService.Tr("battle_bot")}  {Mathf.RoundToInt(result.enemyCorePercent * 100f)}%";

            if (damageText != null) damageText.text = $"{LocalizationService.Tr("results_damage")}: {Mathf.RoundToInt(result.damageDealt):N0}";
            if (coreLeftText != null) coreLeftText.text = $"{LocalizationService.Tr("results_core_left")}: {Mathf.RoundToInt(result.playerCorePercent * 100f)}%";
            if (aliveText != null) aliveText.text = $"{LocalizationService.Tr("results_guardians_alive")}: {result.playerGuardiansAlive}";
            if (accuracyText != null) accuracyText.text = $"{LocalizationService.Tr("results_accuracy")}: {Mathf.RoundToInt(result.Accuracy * 100f)}%";
            if (trophyText != null) trophyText.text = (result.trophyDelta >= 0 ? "+" : "") + result.trophyDelta;

            for (int i = 0; i < stars.Length; i++)
            {
                if (stars[i] == null) continue;
                stars[i].color = starOff;
                if (i < result.stars)
                {
                    int idx = i;
                    TGTween.Delay(0.4f + i * 0.25f, () => { if (stars[idx] != null) { stars[idx].color = starOn; TGTween.PunchScale(stars[idx].transform, 0.35f, 0.35f); Services.Get<AudioService>()?.PlayUi(AudioEventId.RewardPop); } });
                }
            }

            RewardPresenter.Build(result.rewards, progress != null ? progress.Database : null, sprites, items);
            int shown = Mathf.Min(items.Count, tiles.Length);
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] == null) continue;
                if (i < shown) { tiles[i].Bind(items[i]); tiles[i].Pop(1.0f + i * revealDelay); }
                else tiles[i].Hide();
            }
            if (chestNoteText != null)
            {
                bool chestFull = result.outcome == BattleOutcome.Victory && string.IsNullOrEmpty(result.chestId) && result.rewards.chestIds.Count == 0;
                chestNoteText.gameObject.SetActive(chestFull);
                if (chestFull) chestNoteText.text = LocalizationService.Tr("results_chest_full");
            }

            if (unlockPanel != null)
            {
                bool any = result.unlockedGuardianIds.Count > 0 || result.unlockedToolIds.Count > 0 || !string.IsNullOrEmpty(result.unlockedArenaId);
                if (any)
                {
                    TGTween.Delay(1.2f + shown * revealDelay, () =>
                    {
                        if (unlockPanel == null) return;
                        unlockPanel.SetActive(true);
                        TGTween.PunchScale(unlockPanel.transform, 0.15f, 0.35f);
                        if (progress == null) return;
                        if (result.unlockedGuardianIds.Count > 0)
                        {
                            var def = progress.Database.GetGuardian(result.unlockedGuardianIds[0]);
                            if (unlockText != null) unlockText.text = LocalizationService.Tr("results_unlocked") + " " + (def != null ? LocalizationService.Tr(def.nameKey) : "");
                            if (unlockIcon != null && def != null) unlockIcon.sprite = def.portrait;
                        }
                        else if (result.unlockedToolIds.Count > 0)
                        {
                            var def = progress.Database.GetTool(result.unlockedToolIds[0]);
                            if (unlockText != null) unlockText.text = LocalizationService.Tr("results_unlocked") + " " + (def != null ? LocalizationService.Tr(def.nameKey) : "");
                            if (unlockIcon != null && def != null) unlockIcon.sprite = def.icon;
                        }
                        else
                        {
                            var def = progress.Database.GetArena(result.unlockedArenaId);
                            if (unlockText != null) unlockText.text = LocalizationService.Tr("results_new_arena") + ": " + (def != null ? LocalizationService.Tr(def.nameKey) : "");
                            if (unlockIcon != null && def != null) unlockIcon.sprite = def.badge;
                        }
                    });
                }
            }
        }

        void Leave(bool retry)
        {
            var flow = Services.Get<SceneFlowService>();
            if (flow == null) return;
            void Go() { if (retry) flow.RetryLastBattle(); else flow.GoToMainMenu(); }
            if (transition != null) transition.Cover(Go); else Go();
        }
    }
}
