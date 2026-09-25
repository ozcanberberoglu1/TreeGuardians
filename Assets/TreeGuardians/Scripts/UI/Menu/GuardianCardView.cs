using System;
using TMPro;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Localization;
using TreeGuardians.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.UI.Menu
{
    /// Guardian card: portrait, level, rarity frame + corner icon, shard progress, upgrade arrow, NEW badge, locked overlay.
    public sealed class GuardianCardView : MonoBehaviour
    {
        [SerializeField] Image frame;
        [SerializeField] Image portrait;
        [SerializeField] Image cornerIcon;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text levelText;
        [SerializeField] Image shardsFill;
        [SerializeField] TMP_Text shardsText;
        [SerializeField] GameObject upgradeArrow;
        [SerializeField] GameObject newBadge;
        [SerializeField] GameObject lockedOverlay;
        [SerializeField] TMP_Text lockedText;
        [SerializeField] GameObject emptyState;
        [SerializeField] Button button;
        [SerializeField] UIButtonFeedback feedback;
        [SerializeField] Image selectedHighlight;

        public string GuardianId { get; private set; }
        public int SlotIndex { get; set; } = -1;
        public bool IsEmpty => string.IsNullOrEmpty(GuardianId);

        Action<GuardianCardView> onClick;

        void Awake()
        {
            if (button == null) button = GetComponent<Button>();
            if (feedback == null) feedback = GetComponent<UIButtonFeedback>();
            if (button != null) button.onClick.AddListener(() => onClick?.Invoke(this));
        }

        public void SetClickHandler(Action<GuardianCardView> handler) => onClick = handler;

        public void BindEmpty()
        {
            GuardianId = "";
            if (emptyState != null) emptyState.SetActive(true);
            if (portrait != null) portrait.enabled = false;
            if (cornerIcon != null) cornerIcon.enabled = false;
            if (nameText != null) nameText.text = "";
            if (levelText != null) levelText.text = "";
            if (shardsFill != null) shardsFill.transform.parent.gameObject.SetActive(false);
            if (upgradeArrow != null) upgradeArrow.SetActive(false);
            if (newBadge != null) newBadge.SetActive(false);
            if (lockedOverlay != null) lockedOverlay.SetActive(false);
            if (frame != null) frame.color = new Color(0.4f, 0.45f, 0.5f);
            gameObject.SetActive(true);
        }

        public void Bind(GuardianDefinition def, PlayerProgressService progress, RarityPalette palette)
        {
            GuardianId = def.id;
            var state = progress.GetGuardianState(def.id);
            bool unlocked = state.unlocked;
            if (emptyState != null) emptyState.SetActive(false);
            if (portrait != null) { portrait.enabled = true; portrait.sprite = def.portrait; portrait.preserveAspect = true; portrait.color = unlocked ? Color.white : new Color(0.45f, 0.45f, 0.5f); }
            if (frame != null) frame.color = palette != null ? palette.GetColor(def.rarity) : Color.white;
            if (cornerIcon != null) { cornerIcon.enabled = true; cornerIcon.sprite = palette != null ? palette.GetIcon(def.rarity) : null; cornerIcon.color = Color.white; }
            if (nameText != null) nameText.text = LocalizationService.Tr(def.nameKey);
            if (levelText != null) levelText.text = unlocked ? string.Format(LocalizationService.Tr("ui_level_short"), state.level) : "";

            int needed = unlocked
                ? (state.level >= progress.Balance.guardianMaxLevel ? 0 : progress.Balance.GetCardsRequired(def.rarity, state.level))
                : Mathf.Max(1, progress.Balance.GetUpgradeCost(def.rarity).cardsToUnlock);
            if (shardsFill != null)
            {
                shardsFill.transform.parent.gameObject.SetActive(needed > 0);
                shardsFill.fillAmount = needed > 0 ? Mathf.Clamp01(state.shards / (float)needed) : 1f;
            }
            if (shardsText != null) shardsText.text = needed > 0 ? $"{state.shards}/{needed}" : LocalizationService.Tr("ui_max");

            bool canUpgrade = unlocked && progress.CanUpgradeGuardian(def.id, out _, out _, out _);
            if (upgradeArrow != null) upgradeArrow.SetActive(canUpgrade);
            if (newBadge != null) newBadge.SetActive(unlocked && state.isNew);
            if (lockedOverlay != null) lockedOverlay.SetActive(!unlocked);
            if (lockedText != null && !unlocked)
                lockedText.text = def.arenaUnlockIndex > progress.Data.highestArenaIndex
                    ? string.Format(LocalizationService.Tr("ui_unlock_at"), def.arenaUnlockIndex + 1)
                    : string.Format(LocalizationService.Tr("guardian_unlock_hint"), needed);
            gameObject.SetActive(true);
        }

        public void SetSelected(bool value)
        {
            if (selectedHighlight != null) selectedHighlight.enabled = value;
            feedback?.SetSelected(value);
        }

        public void ShakeInvalid() => feedback?.ShakeInvalid();
    }
}
