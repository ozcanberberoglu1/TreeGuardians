using TMPro;
using TreeGuardians.Core;
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
        [SerializeField] TMP_Text timerText;
        [SerializeField] TMP_Text arenaText;
        [SerializeField] GameObject offlineBadge;

        [Header("Action bars")]
        [SerializeField] GuardianActionButton[] guardianButtons = new GuardianActionButton[8];
        [SerializeField] ToolActionButton[] toolButtons = new ToolActionButton[3];
        [SerializeField] TMP_Text hintText;

        [Header("Message")]
        [SerializeField] CanvasGroup messageGroup;
        [SerializeField] TMP_Text messageText;

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
        float messageUntil;

        public TransitionOverlay Transition => transition;

        public void Initialize(BattleContext context, BattleManager battleManager, PlayerBattleCommander playerCommander)
        {
            ctx = context;
            manager = battleManager;
            commander = playerCommander;
            if (playerNameText != null) playerNameText.text = LocalizationService.Tr("battle_you");
            if (enemyNameText != null) enemyNameText.text = LocalizationService.Tr("battle_bot");
            var arena = ctx.database.GetArena(ctx.setup.arenaId);
            if (arenaText != null) arenaText.text = arena != null ? LocalizationService.Tr(arena.nameKey) : "";
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
            if (hintText != null) hintText.text = "";
            lastSecond = -1;
        }

        void OnGuardianButton(int i) => commander?.SelectSlot(i);
        void OnToolButton(int i) => commander?.SelectTool(i);

        public void Tick()
        {
            if (ctx == null) return;
            if (playerCoreFill != null && ctx.playerTree != null) playerCoreFill.fillAmount = ctx.playerTree.CorePercent;
            if (enemyCoreFill != null && ctx.enemyTree != null) enemyCoreFill.fillAmount = ctx.enemyTree.CorePercent;
            int sec = Mathf.CeilToInt(Mathf.Max(0f, ctx.timeRemaining));
            if (sec != lastSecond && timerText != null)
            {
                lastSecond = sec;
                timerText.SetText("{0}:{1:00}", sec / 60, sec % 60);
                if (sec <= 10) timerText.color = new Color(1f, 0.45f, 0.4f);
            }
            int selected = commander != null ? commander.SelectedSlot : -1;
            bool armed = commander != null && commander.SpecialArmed;
            for (int i = 0; i < guardianButtons.Length; i++)
            {
                var g = ctx.playerRoster != null ? ctx.playerRoster.Get(i) : null;
                if (g != null && g.IsActive) guardianButtons[i]?.Refresh(g, i == selected, armed);
            }
            int pendingTool = commander != null ? commander.PendingTool : -1;
            for (int i = 0; i < toolButtons.Length; i++)
                if (ctx.playerTools != null) toolButtons[i]?.Refresh(ctx.playerTools.Get(i), i == pendingTool);
            if (hintText != null)
            {
                string hint = pendingTool >= 0 ? LocalizationService.Tr("tut_step_5") : armed ? LocalizationService.Tr("battle_special_ready") : "";
                if (hintText.text != hint) hintText.text = hint;
            }
            if (messageGroup != null && messageUntil > 0f && Time.unscaledTime > messageUntil)
            {
                messageUntil = 0f;
                TGTween.FadeCanvasGroup(messageGroup, 0f, 0.2f);
            }
        }

        public void ShowMessage(string key, float duration = 1.2f)
        {
            if (messageText == null || messageGroup == null) return;
            messageText.text = LocalizationService.Tr(key);
            messageGroup.alpha = 1f;
            messageUntil = Time.unscaledTime + duration;
            messageText.transform.localScale = Vector3.one * 0.6f;
            TGTween.ScaleTo(messageText.transform, Vector3.one, 0.3f, Ease.OutBack);
        }

        public void ShowLiteral(string text, float duration = 1f)
        {
            if (messageText == null || messageGroup == null) return;
            messageText.text = text;
            messageGroup.alpha = 1f;
            messageUntil = Time.unscaledTime + duration;
            TGTween.PunchScale(messageText.transform, 0.25f, 0.3f);
        }

        public void ShowPause(bool show)
        {
            if (pausePopup == null) return;
            if (show) pausePopup.Open(); else pausePopup.Close();
        }
    }
}
