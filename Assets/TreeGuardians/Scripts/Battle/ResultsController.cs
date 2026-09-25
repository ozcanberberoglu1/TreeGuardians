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
    /// 04_Results: binds the finished battle to the layout pre-authored by ResultsSceneBuilder and plays the staged reveal:
    /// blocks fade in, integrity bars drain, stats count up, stars pop on a C-E-G ladder, reward tiles pop and count,
    /// unlock banner, then a punch on Continue. The victory/defeat stinger is played by the battle (BattleManager.End),
    /// so this screen only starts the results music after a short delay.
    public sealed class ResultsController : MonoBehaviour
    {
        [Header("Backdrop")]
        [SerializeField] Image dimOverlay;
        [SerializeField] Color dimVictory = new Color(0.04f, 0.06f, 0.12f, 0.55f);
        [SerializeField] Color dimDefeat = new Color(0.25f, 0.05f, 0.05f, 0.55f);
        [SerializeField] Color dimDraw = new Color(0.05f, 0.07f, 0.13f, 0.62f);
        [Tooltip("Halo + bursts around the title; only shown on victory.")]
        [SerializeField] GameObject victoryFx;

        [Header("Header")]
        [SerializeField] Image titleRibbon;
        [SerializeField] Sprite ribbonVictory;
        [SerializeField] Sprite ribbonDefeat;
        [SerializeField] Sprite ribbonDraw;
        [SerializeField] Color ribbonVictoryTint = Color.white;
        [SerializeField] Color ribbonDefeatTint = new Color(1f, 0.72f, 0.72f, 1f);
        [SerializeField] Color ribbonDrawTint = Color.white;
        [SerializeField] TMP_Text titleText;
        [SerializeField] Color titleVictoryColor = new Color(0.36f, 0.2f, 0.06f, 1f);
        [SerializeField] Color titleDefeatColor = new Color(1f, 0.56f, 0.5f, 1f);
        [SerializeField] Color titleDrawColor = new Color(0.78f, 0.87f, 1f, 1f);
        [SerializeField] TMP_Text arenaText;
        [Tooltip("Plate behind the arena name; hidden when the arena is unknown.")]
        [SerializeField] GameObject arenaPill;

        [Header("Stars")]
        [SerializeField] Image[] stars = new Image[3];
        [SerializeField] Image[] starGlows = new Image[3];
        [SerializeField] Color starOn = new Color(0.98f, 0.8f, 0.3f);
        [SerializeField] Color starOff = new Color(0.25f, 0.28f, 0.36f, 1f);
        [SerializeField, Range(0f, 1f)] float starGlowAlpha = 0.6f;
        [SerializeField] float starStartDelay = 0.6f;
        [SerializeField] float starInterval = 0.3f;
        [Tooltip("Star pop sound. Falls back to RewardPop while the audio library has no clip for it.")]
        [SerializeField] AudioEventId starSfx = AudioEventId.StarPop;
        [SerializeField, Range(0f, 1f)] float starVolume = 0.9f;
        [Tooltip("Pitch per earned star (C-E-G major triad).")]
        [SerializeField] float[] starPitches = { 1f, 1.26f, 1.5f };

        [Header("Castle integrity")]
        [SerializeField] UIFill playerCoreBar;
        [SerializeField] UIFill enemyCoreBar;
        [SerializeField] TMP_Text playerCoreText;
        [SerializeField] TMP_Text enemyCoreText;
        [SerializeField] float barDelay = 0.3f;
        [SerializeField] float barDuration = 0.9f;

        [Header("Stats")]
        [SerializeField] TMP_Text damageText;
        [Tooltip("Label of the integrity row; its key switches between castle and heartwood wording.")]
        [SerializeField] LocalizedTMPText coreLeftLabel;
        [SerializeField] TMP_Text coreLeftText;
        [SerializeField] TMP_Text aliveText;
        [SerializeField] TMP_Text accuracyText;
        [Tooltip("Hidden when no shot was fired.")]
        [SerializeField] GameObject accuracyRow;
        [SerializeField] float statsDelay = 0.45f;
        [SerializeField] float statCountDuration = 0.8f;
        [SerializeField] string castleLeftKey = "results_castle_left";
        [SerializeField] string heartwoodLeftKey = "results_core_left";

        [Header("Rewards")]
        [SerializeField] RewardItemView[] tiles = new RewardItemView[7];
        [SerializeField] TMP_Text chestNoteText;
        [SerializeField] RewardSprites sprites = new RewardSprites();
        [SerializeField] float rewardsDelay = 1.5f;
        [SerializeField] float revealDelay = 0.16f;

        [Header("Unlock")]
        [SerializeField] GameObject unlockPanel;
        [SerializeField] TMP_Text unlockText;
        [SerializeField] Image unlockIcon;

        [Header("Tutorial")]
        [SerializeField] TMP_Text tutorialHintText;

        [Header("Intro")]
        [Tooltip("Blocks faded/scaled in one after another when the screen opens.")]
        [SerializeField] CanvasGroup[] introGroups = new CanvasGroup[0];
        [SerializeField] float introStagger = 0.08f;
        [SerializeField] float introDuration = 0.3f;
        [SerializeField] float introFromScale = 0.92f;

        [Header("Buttons")]
        [SerializeField] Button continueButton;
        [SerializeField] Button retryButton;
        [SerializeField] TransitionOverlay transition;

        [Header("Audio")]
        [Tooltip("Results music starts after this delay so it does not collide with the battle's end stinger.")]
        [SerializeField] float musicDelay = 0.6f;

        readonly List<RewardItem> items = new List<RewardItem>(12);
        BattleResultData result;
        bool leaving;

        void Awake()
        {
            if (continueButton != null) continueButton.onClick.AddListener(() => Leave(false));
            if (retryButton != null) retryButton.onClick.AddListener(() => Leave(true));
            if (unlockPanel != null) unlockPanel.SetActive(false);
            for (int i = 0; i < introGroups.Length; i++) if (introGroups[i] != null) introGroups[i].alpha = 0f;
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
            var db = progress != null ? progress.Database : null;
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

            bool reduce = QualityApplier.ReduceMotion;
            var setup = flow != null ? flow.PendingBattleSetup : null;

            // No stinger here: BattleManager.End already played it. Music fades in after a short gap.
            TGTween.Delay(musicDelay, () => { if (this != null && !leaving) Services.Get<AudioService>()?.PlayMusic(MusicTrackId.Results); });

            PlayIntro(reduce);
            BindHeader(db);
            BindTutorial(setup, progress);
            BindIntegrity(reduce);
            BindStats(setup, db, reduce);
            BindStars(db, reduce);
            float revealEnd = BindRewards(db);
            revealEnd = BindUnlock(db, revealEnd);

            TGTween.Delay(revealEnd + 0.25f, () =>
            {
                if (this == null || leaving || continueButton == null || QualityApplier.ReduceMotion) return;
                TGTween.PunchScale(continueButton.transform, 0.12f, 0.35f);
            });
        }

        // ------------------------------------------------------------------ intro + header

        void PlayIntro(bool reduce)
        {
            for (int i = 0; i < introGroups.Length; i++)
            {
                var g = introGroups[i];
                if (g == null) continue;
                if (reduce) { g.alpha = 1f; continue; }
                g.alpha = 0f;
                g.transform.localScale = Vector3.one * introFromScale;
                var group = g;
                TGTween.Delay(i * introStagger, () =>
                {
                    if (group == null) return;
                    TGTween.FadeCanvasGroup(group, 1f, introDuration * 0.7f);
                    TGTween.ScaleTo(group.transform, Vector3.one, introDuration, Ease.OutBack);
                });
            }
        }

        void BindHeader(GameDatabase db)
        {
            var outcome = result.outcome;
            bool victory = outcome == BattleOutcome.Victory;
            bool defeat = outcome == BattleOutcome.Defeat;

            if (dimOverlay != null) dimOverlay.color = victory ? dimVictory : defeat ? dimDefeat : dimDraw;
            if (victoryFx != null) victoryFx.SetActive(victory);

            if (titleRibbon != null)
            {
                var sprite = victory ? ribbonVictory : defeat ? ribbonDefeat : ribbonDraw;
                if (sprite != null) titleRibbon.sprite = sprite;
                titleRibbon.color = victory ? ribbonVictoryTint : defeat ? ribbonDefeatTint : ribbonDrawTint;
            }
            if (titleText != null)
            {
                SetLocalized(titleText, victory ? "results_title_victory" : defeat ? "results_title_defeat" : "results_title_draw");
                titleText.color = victory ? titleVictoryColor : defeat ? titleDefeatColor : titleDrawColor;
            }

            var arena = db != null ? db.GetArena(result.arenaId) : null;
            if (arenaText != null) arenaText.text = arena != null ? LocalizationService.Tr(arena.nameKey) : "";
            if (arenaPill != null) arenaPill.SetActive(arena != null);
        }

        void BindTutorial(BattleSetup setup, PlayerProgressService progress)
        {
            bool tutorial = setup != null && setup.isTutorial && progress != null && !progress.Data.tutorial.skipped;
            if (tutorialHintText != null)
            {
                tutorialHintText.gameObject.SetActive(tutorial);
                if (tutorial) tutorialHintText.text = LocalizationService.Tr("tut_step_7");
            }
            // The tutorial battle is not replayable: only Continue is offered.
            if (retryButton != null) retryButton.gameObject.SetActive(!tutorial);
        }

        // ------------------------------------------------------------------ integrity bars

        void BindIntegrity(bool reduce)
        {
            float p = Mathf.Clamp01(result.playerCorePercent);
            float e = Mathf.Clamp01(result.enemyCorePercent);
            string you = LocalizationService.Tr("battle_you");
            string bot = LocalizationService.Tr("battle_bot");
            string pct = PercentFormat();

            if (playerCoreBar != null) playerCoreBar.SetValue(reduce ? p : 1f, true);
            if (enemyCoreBar != null) enemyCoreBar.SetValue(reduce ? e : 1f, true);
            SetBarText(playerCoreText, you, pct, reduce ? p : 1f);
            SetBarText(enemyCoreText, bot, pct, reduce ? e : 1f);
            if (reduce) return;

            TGTween.Delay(barDelay, () =>
            {
                if (this == null) return;
                TGTween.FloatTo(1f, p, barDuration, v =>
                {
                    if (playerCoreBar != null) playerCoreBar.SetValue(v);
                    SetBarText(playerCoreText, you, pct, v);
                }, Ease.OutCubic);
                TGTween.FloatTo(1f, e, barDuration, v =>
                {
                    if (enemyCoreBar != null) enemyCoreBar.SetValue(v);
                    SetBarText(enemyCoreText, bot, pct, v);
                }, Ease.OutCubic);
            });
        }

        static void SetBarText(TMP_Text text, string who, string pctFormat, float value)
        {
            if (text == null) return;
            text.SetText(who + "  " + Percent(pctFormat, Mathf.RoundToInt(value * 100f)));
        }

        // ------------------------------------------------------------------ stats

        void BindStats(BattleSetup setup, GameDatabase db, bool reduce)
        {
            bool castle = db == null || db.playerTree == null || db.playerTree.destructibleCastle;
            if (coreLeftLabel != null) coreLeftLabel.SetKey(castle ? castleLeftKey : heartwoodLeftKey);

            string pct = PercentFormat();
            int damage = Mathf.Max(0, Mathf.RoundToInt(result.damageDealt));
            int coreLeft = Mathf.RoundToInt(Mathf.Clamp01(result.playerCorePercent) * 100f);
            bool hasShots = result.shotsFired > 0;
            int accuracy = Mathf.RoundToInt(result.Accuracy * 100f);
            if (accuracyRow != null) accuracyRow.SetActive(hasShots);

            if (aliveText != null)
            {
                int total = GuardianCount(setup);
                aliveText.text = total > 0 && result.playerGuardiansAlive <= total
                    ? LocalizationService.Number(result.playerGuardiansAlive) + "/" + LocalizationService.Number(total)
                    : LocalizationService.Number(result.playerGuardiansAlive);
            }

            if (damageText != null) damageText.SetText(string.Format(TGTween.NumberCulture, "{0:N0}", reduce ? damage : 0));
            if (coreLeftText != null) coreLeftText.SetText(Percent(pct, reduce ? coreLeft : 0));
            if (accuracyText != null && hasShots) accuracyText.SetText(Percent(pct, reduce ? accuracy : 0));
            if (reduce) return;

            TGTween.Delay(statsDelay, () =>
            {
                if (this == null) return;
                TGTween.CountTo(damageText, 0, damage, statCountDuration, "{0:N0}");
                TGTween.CountTo(coreLeftText, 0, coreLeft, statCountDuration, pct);
                if (hasShots) TGTween.CountTo(accuracyText, 0, accuracy, statCountDuration, pct);
            });
        }

        static int GuardianCount(BattleSetup setup)
        {
            if (setup == null || setup.playerGuardianIds == null) return 0;
            int slots = setup.activeSlots > 0 ? Mathf.Min(setup.activeSlots, setup.playerGuardianIds.Length) : setup.playerGuardianIds.Length;
            int n = 0;
            for (int i = 0; i < slots; i++) if (!string.IsNullOrEmpty(setup.playerGuardianIds[i])) n++;
            return n;
        }

        // ------------------------------------------------------------------ stars

        void BindStars(GameDatabase db, bool reduce)
        {
            var sfx = HasClip(db, starSfx) ? starSfx : AudioEventId.RewardPop;
            for (int i = 0; i < stars.Length; i++)
            {
                var star = stars[i];
                var glow = starGlows != null && i < starGlows.Length ? starGlows[i] : null;
                if (glow != null) { var c = glow.color; c.a = 0f; glow.color = c; }
                if (star == null) continue;
                star.color = starOff;
                if (i >= result.stars) continue;
                int idx = i;
                TGTween.Delay(starStartDelay + i * starInterval, () =>
                {
                    if (this == null || leaving || star == null) return;
                    star.color = starOn;
                    if (!QualityApplier.ReduceMotion)
                    {
                        star.transform.localScale = Vector3.zero;
                        TGTween.ScaleTo(star.transform, Vector3.one, 0.32f, Ease.OutBack);
                    }
                    if (glow != null)
                    {
                        TGTween.FloatTo(0f, starGlowAlpha, QualityApplier.ReduceMotion ? 0f : 0.35f, a =>
                        {
                            if (glow == null) return;
                            var gc = glow.color; gc.a = a; glow.color = gc;
                        });
                    }
                    float pitch = starPitches != null && starPitches.Length > 0 ? starPitches[Mathf.Min(idx, starPitches.Length - 1)] : 1f;
                    Services.Get<AudioService>()?.PlaySfx(sfx, starVolume, pitch, 0f);
                    if (idx == 2) Services.Get<HapticService>()?.Light();
                });
            }
        }

        static bool HasClip(GameDatabase db, AudioEventId id)
        {
            var lib = db != null ? db.audioLibrary : null;
            var e = lib != null ? lib.GetSfx(id) : null;
            if (e == null || e.clips == null) return false;
            for (int i = 0; i < e.clips.Length; i++) if (e.clips[i] != null) return true;
            return false;
        }

        // ------------------------------------------------------------------ rewards + unlock

        /// Binds and schedules the reward tiles; returns the time the last tile has popped.
        float BindRewards(GameDatabase db)
        {
            RewardPresenter.Build(result.rewards, db, sprites, items);
            int shown = Mathf.Min(items.Count, tiles.Length);
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] == null) continue;
                if (i < shown) { tiles[i].Bind(items[i]); tiles[i].Pop(rewardsDelay + i * revealDelay); }
                else tiles[i].Hide();
            }
            if (chestNoteText != null)
            {
                bool chestFull = result.outcome == BattleOutcome.Victory && string.IsNullOrEmpty(result.chestId) && result.rewards.chestIds.Count == 0
                                 && Services.Get<TreeGuardians.Chests.ChestService>() is { HasFreeSlot: false };
                chestNoteText.gameObject.SetActive(chestFull);
                if (chestFull) chestNoteText.text = LocalizationService.Tr("results_chest_full");
            }
            return rewardsDelay + Mathf.Max(0, shown - 1) * revealDelay + 0.3f;
        }

        float BindUnlock(GameDatabase db, float after)
        {
            if (unlockPanel == null) return after;
            bool any = result.unlockedGuardianIds.Count > 0 || result.unlockedToolIds.Count > 0 || !string.IsNullOrEmpty(result.unlockedArenaId);
            if (!any) return after;
            float at = after + 0.2f;
            TGTween.Delay(at, () =>
            {
                if (this == null || leaving || unlockPanel == null) return;
                string title, name = "";
                Sprite icon = null;
                if (result.unlockedGuardianIds.Count > 0)
                {
                    var def = db != null ? db.GetGuardian(result.unlockedGuardianIds[0]) : null;
                    title = LocalizationService.Tr("results_unlocked");
                    if (def != null) { name = LocalizationService.Tr(def.nameKey); icon = def.portrait; }
                }
                else if (result.unlockedToolIds.Count > 0)
                {
                    var def = db != null ? db.GetTool(result.unlockedToolIds[0]) : null;
                    title = LocalizationService.Tr("results_unlocked");
                    if (def != null) { name = LocalizationService.Tr(def.nameKey); icon = def.icon; }
                }
                else
                {
                    var def = db != null ? db.GetArena(result.unlockedArenaId) : null;
                    title = LocalizationService.Tr("results_new_arena");
                    if (def != null) { name = LocalizationService.Tr(def.nameKey); icon = def.badge; }
                }
                if (unlockText != null) unlockText.text = string.IsNullOrEmpty(name) ? title : "<size=70%>" + title + "</size>\n" + name;
                if (unlockIcon != null)
                {
                    unlockIcon.sprite = icon;
                    unlockIcon.enabled = icon != null;
                    unlockIcon.preserveAspect = true;
                }
                unlockPanel.SetActive(true);
                if (!QualityApplier.ReduceMotion)
                {
                    unlockPanel.transform.localScale = Vector3.one * 0.6f;
                    TGTween.ScaleTo(unlockPanel.transform, Vector3.one, 0.35f, Ease.OutBack);
                }
                Services.Get<AudioService>()?.PlaySfx(AudioEventId.Unlock, 0.9f);
            });
            return at + 0.4f;
        }

        // ------------------------------------------------------------------ helpers

        static void SetLocalized(TMP_Text text, string key)
        {
            var loc = text.GetComponent<LocalizedTMPText>();
            if (loc != null) loc.SetKey(key);
            else text.text = LocalizationService.Tr(key);
        }

        /// Localized percent pattern ("{0}%" EN / "%{0}" TR) from key fmt_percent, with a safe fallback.
        static string PercentFormat()
        {
            var loc = Services.Get<LocalizationService>();
            return loc != null && loc.Has("fmt_percent") ? loc.Get("fmt_percent") : "{0}%";
        }

        static string Percent(string format, int value) => string.Format(TGTween.NumberCulture, format, value);

        void Leave(bool retry)
        {
            if (leaving) return;
            var flow = Services.Get<SceneFlowService>();
            if (flow == null) return;
            leaving = true;
            if (continueButton != null) continueButton.interactable = false;
            if (retryButton != null) retryButton.interactable = false;
            void Go() { if (retry) flow.RetryLastBattle(); else flow.GoToMainMenu(); }
            if (transition != null) transition.Cover(Go); else Go();
        }
    }
}
