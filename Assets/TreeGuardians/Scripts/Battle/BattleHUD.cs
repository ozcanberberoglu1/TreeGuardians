using TMPro;
using TreeGuardians.Audio;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Localization;
using TreeGuardians.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.Battle
{
    /// Pre-authored battle HUD; refreshes cheap fills every frame and texts only on change.
    public sealed class BattleHUD : MonoBehaviour
    {
        [Header("Top")]
        [SerializeField] TMP_Text playerNameText;
        [SerializeField] TMP_Text enemyNameText;
        [SerializeField] Image playerCoreFill;
        [SerializeField] Image enemyCoreFill;
        [SerializeField] UIFill playerCoreBar;
        [SerializeField] UIFill enemyCoreBar;
        [SerializeField] TMP_Text playerCorePercent;
        [SerializeField] TMP_Text enemyCorePercent;
        [SerializeField] TMP_Text timerText;
        [SerializeField] Image timerPlate;
        [SerializeField] TMP_Text arenaText;
        [SerializeField] GameObject offlineBadge;
        [SerializeField] Color barHealthy = new Color(0.47f, 0.82f, 0.29f);
        [SerializeField] Color barWarning = new Color(1f, 0.74f, 0.24f);
        [SerializeField] Color barCritical = new Color(0.94f, 0.33f, 0.27f);
        [SerializeField] Color timerNormal = Color.white;
        [SerializeField] Color timerUrgent = new Color(1f, 0.45f, 0.4f);

        [Header("Action bars")]
        [SerializeField] GuardianActionButton[] guardianButtons = new GuardianActionButton[8];
        [SerializeField] ToolActionButton[] toolButtons = new ToolActionButton[3];
        [SerializeField] CanvasGroup combatGroup;
        [SerializeField] TMP_Text hintText;
        [SerializeField] CanvasGroup hintGroup;

        [Header("Turn (castle duel)")]
        [SerializeField] GameObject turnGroup;
        [SerializeField] TMP_Text turnText;
        [SerializeField] TMP_Text turnCountText;
        [SerializeField] Image turnPill;
        [SerializeField] Image turnTimerFill;
        [SerializeField] Color turnPlayerColor = new Color(0.36f, 0.75f, 0.35f);
        [SerializeField] Color turnEnemyColor = new Color(0.9f, 0.35f, 0.3f);
        [SerializeField] Color turnUrgentColor = new Color(1f, 0.55f, 0.2f);

        [Header("Turn banner")]
        [SerializeField] CanvasGroup turnBanner;
        [SerializeField] TMP_Text turnBannerText;
        [SerializeField] Image turnBannerRibbon;
        [SerializeField] float turnBannerSeconds = 1.05f;
        [SerializeField] Color bannerPlayerTint = Color.white;
        [SerializeField] Color bannerEnemyTint = new Color(1f, 0.58f, 0.52f);

        [Header("Message")]
        [SerializeField] CanvasGroup messageGroup;
        [SerializeField] TMP_Text messageText;

        [Header("End banner")]
        [SerializeField] CanvasGroup endBanner;
        [SerializeField] TMP_Text endBannerText;
        [SerializeField] Image endBannerRibbon;
        [SerializeField] Color victoryColor = new Color(0.98f, 0.76f, 0.2f);
        [SerializeField] Color defeatColor = new Color(0.62f, 0.3f, 0.3f);
        [SerializeField] Color drawColor = new Color(0.55f, 0.6f, 0.7f);

        [Header("Pause")]
        [SerializeField] Button pauseButton;
        [SerializeField] UIPanel pausePopup;
        [SerializeField] Button resumeButton;
        [SerializeField] Button forfeitButton;
        [SerializeField] TransitionOverlay transition;

        BattleContext ctx;
        BattleManager manager;
        PlayerBattleCommander commander;
        int lastSecond = -1;
        int lastTurnSecond = -1;
        int lastPlayerPct = -1, lastEnemyPct = -1;
        float messageUntil;
        float bannerUntil;
        Coroutine messageFade, bannerFade;
        bool ended;
        string lastTurnLabel;
        string lastHint;

        public TransitionOverlay Transition => transition;

        public void Initialize(BattleContext context, BattleManager battleManager, PlayerBattleCommander playerCommander)
        {
            ctx = context;
            manager = battleManager;
            if (commander != null) commander.OnFireRejected -= OnFireRejected;
            commander = playerCommander;
            if (commander != null) commander.OnFireRejected += OnFireRejected;
            ended = false;
            if (playerNameText != null) playerNameText.text = LocalizationService.Tr("battle_you");
            if (enemyNameText != null) enemyNameText.text = LocalizationService.Tr("battle_bot");
            var arena = ctx.database.GetArena(ctx.setup.arenaId);
            if (arenaText != null)
            {
                string arenaName = arena != null ? LocalizationService.Tr(arena.nameKey) : "";
                // Without a separate badge the chip reads "<arena>  •  OFFLINE BOT".
                arenaText.text = offlineBadge != null ? arenaName : arenaName + "  •  " + LocalizationService.Tr("battle_offline");
            }
            if (offlineBadge != null) offlineBadge.SetActive(true);
            for (int i = 0; i < guardianButtons.Length; i++)
            {
                var g = ctx.playerRoster != null ? ctx.playerRoster.Get(i) : null;
                guardianButtons[i]?.Bind(i, g, OnGuardianButton);
            }
            for (int i = 0; i < toolButtons.Length; i++)
                toolButtons[i]?.Bind(i, ctx.playerTools != null ? ctx.playerTools.Get(i) : default, OnToolButton);
            if (pauseButton != null) { pauseButton.onClick.RemoveAllListeners(); pauseButton.onClick.AddListener(() => manager.TogglePause()); }
            if (resumeButton != null) { resumeButton.onClick.RemoveAllListeners(); resumeButton.onClick.AddListener(() => manager.Resume()); }
            if (forfeitButton != null) { forfeitButton.onClick.RemoveAllListeners(); forfeitButton.onClick.AddListener(() => manager.Forfeit()); }
            pausePopup?.HideImmediate();
            if (messageGroup != null) messageGroup.alpha = 0f;
            if (turnBanner != null) { turnBanner.alpha = 0f; turnBanner.gameObject.SetActive(false); }
            if (endBanner != null) { endBanner.alpha = 0f; endBanner.gameObject.SetActive(false); }
            if (combatGroup != null) { combatGroup.alpha = 1f; combatGroup.interactable = true; combatGroup.blocksRaycasts = true; }
            SetHint("");
            if (turnGroup != null) turnGroup.SetActive(false); // shown by TickTurn once the first turn starts
            if (timerText != null) timerText.color = timerNormal;
            lastSecond = -1;
            lastTurnSecond = -1;
            lastPlayerPct = lastEnemyPct = -1;
            lastTurnLabel = null;
            playerCoreBar?.SetValue(1f, true);
            enemyCoreBar?.SetValue(1f, true);
        }

        void OnDestroy()
        {
            if (commander != null) commander.OnFireRejected -= OnFireRejected;
        }

        void OnFireRejected()
        {
            ShowMessage("battle_guardian_cannot_fire", 0.9f);
            Services.Get<AudioService>()?.PlayUi(AudioEventId.UiError);
        }

        void TickTurn()
        {
            var turns = ctx.turns;
            if (turns == null || turnGroup == null) return;
            // Intro / countdown: no side is acting yet, so the pill stays hidden instead of showing "enemy turn".
            bool started = turns.Phase != TurnPhase.None && !ended;
            if (turnGroup.activeSelf != started) turnGroup.SetActive(started);
            if (!started) return;
            string label;
            float fill;
            Color color;
            int count = -1;
            switch (turns.Phase)
            {
                case TurnPhase.PlayerAct:
                    label = LocalizationService.Tr("battle_your_turn");
                    count = Mathf.CeilToInt(turns.TimeLeft);
                    fill = ctx.balance.turnSeconds > 0f ? turns.TimeLeft / ctx.balance.turnSeconds : 0f;
                    color = turns.TimeLeft <= 3f ? turnUrgentColor : turnPlayerColor;
                    break;
                case TurnPhase.PlayerResolve:
                    label = LocalizationService.Tr("battle_your_turn");
                    fill = 0f; color = turnPlayerColor;
                    break;
                default:
                    label = LocalizationService.Tr("battle_enemy_turn");
                    fill = 1f; color = turnEnemyColor;
                    break;
            }
            if (turnCountText == null && count >= 0) label = label + "  " + count;
            if (turnText != null && label != lastTurnLabel) { turnText.text = label; lastTurnLabel = label; }
            if (turnCountText != null && count != lastTurnSecond)
            {
                bool urgentTick = count >= 1 && count <= 3 && lastTurnSecond > count;
                lastTurnSecond = count;
                turnCountText.gameObject.SetActive(count >= 0);
                if (count >= 0) turnCountText.SetText("{0}", count);
                if (urgentTick)
                {
                    TGTween.PunchScale(turnCountText.transform, 0.3f, 0.25f);
                    Services.Get<AudioService>()?.PlayUi(AudioEventId.TimerTick, 1f, 1f + (3 - count) * 0.09f);
                    Services.Get<HapticService>()?.Light();
                }
            }
            if (turnTimerFill != null) { turnTimerFill.fillAmount = fill; turnTimerFill.color = color; }
            if (turnPill != null)
            {
                // The badge only exists while a countdown runs (player act); other phases show the label alone.
                bool counting = count >= 0 || turnCountText == null;
                if (turnPill.gameObject.activeSelf != counting) turnPill.gameObject.SetActive(counting);
                var tint = Color.Lerp(color, Color.white, 0.12f);
                if (turnPill.color != tint) turnPill.color = tint;
            }
        }

        void OnGuardianButton(int i) => commander?.SelectSlot(i);
        void OnToolButton(int i) => commander?.SelectTool(i);

        public void Tick()
        {
            if (ctx == null) return;
            if (ctx.playerTree != null) UpdateCore(ctx.playerTree.CorePercent, playerCoreFill, playerCoreBar, playerCorePercent, ref lastPlayerPct);
            if (ctx.enemyTree != null) UpdateCore(ctx.enemyTree.CorePercent, enemyCoreFill, enemyCoreBar, enemyCorePercent, ref lastEnemyPct);
            int sec = Mathf.CeilToInt(Mathf.Max(0f, ctx.timeRemaining));
            if (sec != lastSecond && timerText != null)
            {
                lastSecond = sec;
                timerText.SetText("{0}:{1:00}", sec / 60, sec % 60);
                bool urgent = sec <= 10;
                timerText.color = urgent ? timerUrgent : timerNormal;
                if (urgent && sec > 0 && ctx.isPlaying) TGTween.PunchScale(timerText.transform, 0.15f, 0.2f);
            }
            if (ended) { TickMessage(); return; }
            bool canAct = ctx.isPlaying && (ctx.turns == null || ctx.turns.IsPlayerActing);
            int selected = commander != null ? commander.SelectedSlot : -1;
            bool armed = commander != null && commander.SpecialArmed;
            for (int i = 0; i < guardianButtons.Length; i++)
            {
                var g = ctx.playerRoster != null ? ctx.playerRoster.Get(i) : null;
                if (g != null && g.IsActive) guardianButtons[i]?.Refresh(g, i == selected, armed, canAct, ctx.turnBased);
            }
            int pendingTool = commander != null ? commander.PendingTool : -1;
            for (int i = 0; i < toolButtons.Length; i++)
                if (ctx.playerTools != null) toolButtons[i]?.Refresh(ctx.playerTools.Get(i), i == pendingTool, canAct);
            string hint;
            if (!canAct) hint = "";
            else if (pendingTool >= 0) hint = LocalizationService.Tr("battle_tool_tap_target");
            else if (armed) hint = LocalizationService.Tr("battle_special_ready");
            else hint = LocalizationService.Tr(selected >= 0 ? "battle_tap_to_fire" : "battle_select_guardian");
            SetHint(hint);
            TickTurn();
            TickMessage();
        }

        void UpdateCore(float pct, Image legacyFill, UIFill bar, TMP_Text label, ref int last)
        {
            pct = Mathf.Clamp01(pct);
            Color c = pct > 0.5f ? barHealthy : pct > 0.25f ? barWarning : barCritical;
            if (bar != null) { bar.SetValue(pct); bar.Color = c; }
            else if (legacyFill != null) legacyFill.fillAmount = pct;
            int p = Mathf.CeilToInt(pct * 100f);
            if (label != null && p != last)
            {
                if (last >= 0 && p < last) TGTween.PunchScale(label.transform, 0.18f, 0.2f);
                last = p;
                label.text = LocalizationService.Tr("fmt_percent", p);
            }
        }

        void SetHint(string hint)
        {
            if (hintText == null || hint == lastHint) return;
            lastHint = hint;
            bool show = !string.IsNullOrEmpty(hint);
            if (show) hintText.text = hint;
            if (hintGroup != null)
            {
                TGTween.FadeCanvasGroup(hintGroup, show ? 1f : 0f, 0.15f);
            }
            else if (!show) hintText.text = "";
        }

        void TickMessage()
        {
            if (messageGroup != null && messageUntil > 0f && Time.unscaledTime > messageUntil)
            {
                messageUntil = 0f;
                messageFade = TGTween.FadeCanvasGroup(messageGroup, 0f, 0.2f);
            }
            if (turnBanner != null && bannerUntil > 0f && Time.unscaledTime > bannerUntil)
            {
                bannerUntil = 0f;
                var banner = turnBanner;
                bannerFade = TGTween.FadeCanvasGroup(banner, 0f, 0.2f, true, () => { if (banner != null) banner.gameObject.SetActive(false); });
            }
        }

        public void ShowMessage(string key, float duration = 1.2f) => ShowLiteralInternal(LocalizationService.Tr(key), duration, true);

        public void ShowLiteral(string text, float duration = 1f) => ShowLiteralInternal(text, duration, false);

        void ShowLiteralInternal(string text, float duration, bool pop)
        {
            if (messageText == null || messageGroup == null) return;
            TGTween.Stop(messageFade); // an older fade must not hide the new message
            messageFade = null;
            messageText.text = text;
            messageGroup.alpha = 1f;
            messageUntil = Time.unscaledTime + duration;
            if (pop)
            {
                messageText.transform.localScale = Vector3.one * 0.6f;
                TGTween.ScaleTo(messageText.transform, Vector3.one, 0.3f, Ease.OutBack);
            }
            else TGTween.PunchScale(messageText.transform, 0.25f, 0.3f);
        }

        /// Big ribbon that slides in at every turn change ("YOUR TURN" / "ENEMY TURN").
        public void ShowTurnBanner(bool player)
        {
            if (ended) return;
            if (turnBanner == null) { ShowMessage(player ? "battle_your_turn" : "battle_enemy_turn", 1f); return; }
            TGTween.Stop(bannerFade);
            bannerFade = null;
            turnBanner.gameObject.SetActive(true);
            if (turnBannerText != null) turnBannerText.text = LocalizationService.Tr(player ? "battle_your_turn" : "battle_enemy_turn");
            if (turnBannerRibbon != null) turnBannerRibbon.color = player ? bannerPlayerTint : bannerEnemyTint;
            turnBanner.alpha = 0f;
            TGTween.FadeCanvasGroup(turnBanner, 1f, 0.12f);
            var t = turnBanner.transform;
            t.localScale = new Vector3(0.4f, 0.85f, 1f);
            TGTween.ScaleTo(t, Vector3.one, QualityApplier.ReduceMotion ? 0f : 0.32f, Ease.OutBack);
            bannerUntil = Time.unscaledTime + turnBannerSeconds;
        }

        /// Battle is over (or resolving the last hit): hide combat controls and show the result ribbon.
        public void OnBattleEnded(BattleOutcome outcome)
        {
            ended = true;
            if (commander != null) commander.OnFireRejected -= OnFireRejected;
            SetHint("");
            if (combatGroup != null)
            {
                combatGroup.interactable = false;
                combatGroup.blocksRaycasts = false;
                TGTween.FadeCanvasGroup(combatGroup, 0f, 0.25f);
            }
            if (turnGroup != null) turnGroup.SetActive(false);
            if (pauseButton != null) pauseButton.interactable = false;
            if (turnBanner != null) { bannerUntil = 0f; turnBanner.alpha = 0f; turnBanner.gameObject.SetActive(false); }
            if (outcome == BattleOutcome.None) return;
            string key = outcome == BattleOutcome.Victory ? "battle_victory" : outcome == BattleOutcome.Defeat ? "battle_defeat" : "battle_draw";
            if (endBanner == null) { ShowMessage(key, 3f); return; }
            messageUntil = 0f;
            if (messageGroup != null) messageGroup.alpha = 0f;
            endBanner.gameObject.SetActive(true);
            if (endBannerText != null) endBannerText.text = LocalizationService.Tr(key);
            if (endBannerRibbon != null) endBannerRibbon.color = outcome == BattleOutcome.Victory ? victoryColor : outcome == BattleOutcome.Defeat ? defeatColor : drawColor;
            endBanner.alpha = 0f;
            TGTween.FadeCanvasGroup(endBanner, 1f, 0.2f);
            endBanner.transform.localScale = Vector3.one * 0.3f;
            TGTween.ScaleTo(endBanner.transform, Vector3.one, QualityApplier.ReduceMotion ? 0f : 0.45f, Ease.OutBack);
        }

        public void ShowPause(bool show)
        {
            if (pausePopup == null) return;
            if (show) pausePopup.Open(); else pausePopup.Close();
        }
    }
}
