using TMPro;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Localization;
using TreeGuardians.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.UI.Menu
{
    /// Arena, recommended power, both loadouts and difficulty before starting an offline bot match.
    public sealed class BattlePrepPanel : UIPanel
    {
        [SerializeField] TMP_Text arenaNameText;
        [SerializeField] Image arenaBadge;
        [SerializeField] TMP_Text recommendedPowerText;
        [SerializeField] TMP_Text yourPowerText;
        [SerializeField] Image[] playerPortraits = new Image[8];
        [SerializeField] Image[] enemyPortraits = new Image[8];
        [SerializeField] Button easyButton;
        [SerializeField] Button normalButton;
        [SerializeField] Button hardButton;
        [SerializeField] Button startButton;
        [SerializeField] Button closeButton;
        [SerializeField] TMP_Text offlineNoteText;
        [SerializeField] Color emptyPortrait = new Color(1f, 1f, 1f, 0.15f);

        BotDifficulty difficulty = BotDifficulty.Normal;

        protected override void Awake()
        {
            base.Awake();
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (easyButton != null) easyButton.onClick.AddListener(() => SetDifficulty(BotDifficulty.Easy));
            if (normalButton != null) normalButton.onClick.AddListener(() => SetDifficulty(BotDifficulty.Normal));
            if (hardButton != null) hardButton.onClick.AddListener(() => SetDifficulty(BotDifficulty.Hard));
            if (startButton != null) startButton.onClick.AddListener(() => MenuUIController.Instance?.StartBattle(difficulty));
        }

        protected override void OnOpen()
        {
            var progress = Services.Get<PlayerProgressService>();
            if (progress != null && progress.CurrentArena != null) difficulty = progress.CurrentArena.botDifficulty;
            Refresh();
        }

        void SetDifficulty(BotDifficulty d)
        {
            difficulty = d;
            Refresh();
        }

        void Refresh()
        {
            var progress = Services.Get<PlayerProgressService>();
            if (progress == null) return;
            var arena = progress.CurrentArena;
            if (arenaNameText != null) arenaNameText.text = arena != null ? LocalizationService.Tr(arena.nameKey) : "";
            if (arenaBadge != null && arena != null) arenaBadge.sprite = arena.badge;
            if (recommendedPowerText != null) recommendedPowerText.text = LocalizationService.Tr("arena_recommended_power") + ": " + (arena != null ? arena.recommendedPower : 0);
            if (yourPowerText != null) yourPowerText.text = LocalizationService.Tr("menu_tree_power") + ": " + progress.GetTreePower();
            for (int i = 0; i < playerPortraits.Length; i++)
            {
                if (playerPortraits[i] == null) continue;
                var def = progress.Database.GetGuardian(progress.GetEquippedGuardianId(i));
                playerPortraits[i].sprite = def != null ? def.portrait : null; playerPortraits[i].preserveAspect = true;
                playerPortraits[i].color = def != null ? Color.white : emptyPortrait;
            }
            for (int i = 0; i < enemyPortraits.Length; i++)
            {
                if (enemyPortraits[i] == null) continue;
                var def = arena != null && i < arena.botGuardianIds.Length ? progress.Database.GetGuardian(arena.botGuardianIds[i]) : null;
                enemyPortraits[i].sprite = def != null ? def.portrait : null; enemyPortraits[i].preserveAspect = true;
                enemyPortraits[i].color = def != null ? Color.white : emptyPortrait;
            }
            Highlight(easyButton, difficulty == BotDifficulty.Easy);
            Highlight(normalButton, difficulty == BotDifficulty.Normal);
            Highlight(hardButton, difficulty == BotDifficulty.Hard);
            if (offlineNoteText != null) offlineNoteText.text = LocalizationService.Tr("prep_offline_bot");
        }

        static void Highlight(Button b, bool on)
        {
            if (b == null) return;
            var fb = b.GetComponent<UIButtonFeedback>();
            if (fb != null) fb.SetSelected(on);
        }
    }
}
