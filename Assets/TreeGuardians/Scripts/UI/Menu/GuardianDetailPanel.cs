using TMPro;
using TreeGuardians.Audio;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Localization;
using TreeGuardians.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.UI.Menu
{
    /// Guardian info, old→new stat comparison, upgrade transaction and equip/unequip.
    public sealed class GuardianDetailPanel : UIPanel
    {
        [Header("Header")]
        [SerializeField] Image portrait;
        [SerializeField] Image frame;
        [SerializeField] Image cornerIcon;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text rarityText;
        [SerializeField] TMP_Text levelText;
        [SerializeField] TMP_Text descriptionText;
        [SerializeField] TMP_Text powerText;

        [Header("Stats (current → next)")]
        [SerializeField] TMP_Text healthText;
        [SerializeField] TMP_Text attackText;
        [SerializeField] TMP_Text structureText;
        [SerializeField] TMP_Text armorText;
        [SerializeField] TMP_Text critText;
        [SerializeField] TMP_Text cooldownText;
        [SerializeField] TMP_Text specialText;
        [SerializeField] TMP_Text passiveText;

        [Header("Upgrade")]
        [SerializeField] Button upgradeButton;
        [SerializeField] TMP_Text upgradeLabel;
        [SerializeField] TMP_Text cardsCostText;
        [SerializeField] TMP_Text coinsCostText;
        [SerializeField] Image cardsFill;
        [SerializeField] Color okColor = new Color(0.9f, 0.95f, 0.9f);
        [SerializeField] Color missingColor = new Color(1f, 0.4f, 0.35f);

        [Header("Loadout")]
        [SerializeField] Button equipButton;
        [SerializeField] TMP_Text equipLabel;
        [SerializeField] Button closeButton;
        [SerializeField] RectTransform punchTarget;
        [Tooltip("Açıksa arka planı popup karartıcısıyla karartır. Yalnızca panel PopupLayer altına taşındıysa açın (Panels altındayken karartıcı panelin üstünde kalır).")]
        [SerializeField] bool dimBackground;
        [Tooltip("Çıkarma sesinin perde çarpanı: aynı Equip sesi bu çarpanla çalar (1'in altı = daha pes).")]
        [SerializeField] float unequipPitch = 0.85f;

        bool dimRetained;

        string guardianId;
        int slotIndex = -1;
        PlayerProgressService progress;

        protected override void Awake()
        {
            base.Awake();
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (upgradeButton != null) upgradeButton.onClick.AddListener(OnUpgradeClicked);
            if (equipButton != null) equipButton.onClick.AddListener(OnEquipClicked);
        }

        void OnEnable()
        {
            GameEventBus.Subscribe<CurrencyChangedEvent>(OnCurrency);
            GameEventBus.Subscribe<LanguageChangedEvent>(OnLanguage);
        }

        void OnDisable()
        {
            GameEventBus.Unsubscribe<CurrencyChangedEvent>(OnCurrency);
            GameEventBus.Unsubscribe<LanguageChangedEvent>(OnLanguage);
        }

        void OnCurrency(CurrencyChangedEvent e) { if (IsOpen) Refresh(); }
        void OnLanguage(LanguageChangedEvent e) { if (IsOpen) Refresh(); }

        protected override void OnOpen()
        {
            // Opened on top of the Guardians panel (outside MenuUIController.current): always draw above the other panels.
            transform.SetAsLastSibling();
            if (dimBackground && !dimRetained && PopupDimmer.Instance != null) { PopupDimmer.Instance.Retain(); dimRetained = true; }
        }

        protected override void OnClose()
        {
            if (dimRetained) { PopupDimmer.Instance?.Release(); dimRetained = false; }
        }

        public void Show(string id, int equippedSlot)
        {
            guardianId = id;
            slotIndex = equippedSlot;
            Open();
            Refresh();
        }

        public void Refresh()
        {
            progress = Services.Get<PlayerProgressService>();
            if (progress == null) return;
            var def = progress.Database.GetGuardian(guardianId);
            if (def == null) { Close(); return; }
            var palette = progress.Database.rarityPalette;
            var balance = progress.Balance;
            var state = progress.GetGuardianState(def.id);
            bool unlocked = state.unlocked;
            int level = state.level;
            int next = Mathf.Min(balance.guardianMaxLevel, level + 1);
            bool atMax = level >= balance.guardianMaxLevel;

            if (portrait != null) { portrait.sprite = def.portrait; portrait.preserveAspect = true; }
            if (frame != null) frame.color = palette != null ? palette.GetColor(def.rarity) : Color.white;
            if (cornerIcon != null) cornerIcon.sprite = palette != null ? palette.GetIcon(def.rarity) : null;
            if (nameText != null) nameText.text = LocalizationService.Tr(def.nameKey);
            if (rarityText != null)
            {
                rarityText.text = palette != null ? LocalizationService.Tr(palette.GetNameKey(def.rarity)) : def.rarity.ToString();
                if (palette != null) rarityText.color = palette.GetColor(def.rarity);
            }
            if (levelText != null) levelText.text = unlocked ? string.Format(LocalizationService.Tr("menu_level"), level) : LocalizationService.Tr("guardian_locked_label");
            if (descriptionText != null) descriptionText.text = LocalizationService.Tr(def.descriptionKey);
            if (powerText != null) powerText.text = LocalizationService.Tr("guardian_power") + ": " + LocalizationService.Number(def.GetPower(level, balance));

            SetStat(healthText, "guardian_health", def.GetHealth(level, balance), def.GetHealth(next, balance), atMax || !unlocked);
            SetStat(attackText, "guardian_attack", def.GetAttack(level, balance), def.GetAttack(next, balance), atMax || !unlocked);
            SetStat(structureText, "guardian_structure", def.GetStructureDamage(level, balance), def.GetStructureDamage(next, balance), atMax || !unlocked);
            SetStat(armorText, "guardian_armor", def.GetArmor(level), def.GetArmor(next), atMax || !unlocked);
            if (critText != null) critText.text = LocalizationService.Tr("guardian_crit") + ": " + LocalizationService.Tr("fmt_percent", Mathf.RoundToInt(def.critChance * 100f));
            if (cooldownText != null) cooldownText.text = LocalizationService.Tr("guardian_cooldown") + ": " + LocalizationService.Tr("fmt_seconds", def.attackCooldown)
                + "   " + LocalizationService.Tr("guardian_range") + ": " + LocalizationService.Number(Mathf.RoundToInt(def.range));
            if (specialText != null) specialText.text = $"{LocalizationService.Tr("guardian_special")}: {LocalizationService.Tr(def.specialNameKey)}";
            if (passiveText != null) passiveText.text = $"{LocalizationService.Tr("guardian_passive")}: {PassiveDescription(def)}";

            bool canUpgrade = progress.CanUpgradeGuardian(def.id, out int cards, out int coins, out string reason);
            if (!unlocked)
            {
                int need = Mathf.Max(1, balance.GetUpgradeCost(def.rarity).cardsToUnlock);
                if (cardsCostText != null) { cardsCostText.text = $"{state.shards}/{need}"; cardsCostText.color = state.shards >= need ? okColor : missingColor; }
                FillBar.Set(cardsFill, Mathf.Clamp01(state.shards / (float)need));
                if (coinsCostText != null) coinsCostText.text = "";
                if (upgradeButton != null) upgradeButton.interactable = false;
                if (upgradeLabel != null) upgradeLabel.text = LocalizationService.Tr("guardian_locked_label");
            }
            else if (atMax)
            {
                if (cardsCostText != null) { cardsCostText.text = LocalizationService.Tr("ui_max"); cardsCostText.color = okColor; }
                FillBar.Set(cardsFill, 1f);
                if (coinsCostText != null) coinsCostText.text = "";
                if (upgradeButton != null) upgradeButton.interactable = false;
                if (upgradeLabel != null) upgradeLabel.text = LocalizationService.Tr("ui_max");
            }
            else
            {
                if (cardsCostText != null) { cardsCostText.text = $"{state.shards}/{cards}"; cardsCostText.color = state.shards >= cards ? okColor : missingColor; }
                FillBar.Set(cardsFill, Mathf.Clamp01(state.shards / (float)Mathf.Max(1, cards)));
                if (coinsCostText != null) { coinsCostText.text = LocalizationService.Number(coins); coinsCostText.color = progress.Wallet.CanAfford(CurrencyType.Coins, coins) ? okColor : missingColor; }
                if (upgradeButton != null) upgradeButton.interactable = canUpgrade;
                if (upgradeLabel != null) upgradeLabel.text = LocalizationService.Tr("guardian_upgrade");
            }

            slotIndex = progress.FindEquippedSlot(def.id);
            if (equipButton != null) equipButton.interactable = unlocked;
            if (equipLabel != null) equipLabel.text = LocalizationService.Tr(slotIndex >= 0 ? "ui_unequip" : "ui_equip");
        }

        static void SetStat(TMP_Text t, string key, float current, float next, bool hideNext)
        {
            if (t == null) return;
            string c = LocalizationService.Number(Mathf.RoundToInt(current));
            t.text = hideNext || Mathf.Approximately(current, next)
                ? $"{LocalizationService.Tr(key)}: {c}"
                : $"{LocalizationService.Tr(key)}: {c} <color=#7CE07C>→ {LocalizationService.Number(Mathf.RoundToInt(next))}</color>";
        }

        static string PassiveDescription(GuardianDefinition def)
        {
            switch (def.passiveKind)
            {
                case GuardianPassiveKind.CritBoost: return string.Format(LocalizationService.Tr("passive_crit"), Mathf.RoundToInt(def.passiveMagnitude * 100f));
                case GuardianPassiveKind.ShieldOnHit: return LocalizationService.Tr("passive_shield_on_hit");
                case GuardianPassiveKind.HealOverTime: return LocalizationService.Tr("passive_heal");
                case GuardianPassiveKind.ArmorAura: return LocalizationService.Tr("passive_armor_aura");
                case GuardianPassiveKind.ToolCooldownReduction: return string.Format(LocalizationService.Tr("passive_tool_cd"), Mathf.RoundToInt(def.passiveMagnitude * 100f));
                case GuardianPassiveKind.ExtraStructureDamage: return string.Format(LocalizationService.Tr("passive_structure"), Mathf.RoundToInt(def.passiveMagnitude * 100f));
                case GuardianPassiveKind.Rebirth: return LocalizationService.Tr("passive_rebirth");
                default: return LocalizationService.Tr("passive_none");
            }
        }

        void OnUpgradeClicked()
        {
            if (progress == null) return;
            if (!progress.CanUpgradeGuardian(guardianId, out _, out int coins, out string reason))
            {
                if (reason == "insufficient_coins") MenuUIController.Instance?.ShowInsufficient(CurrencyType.Coins);
                else MenuUIController.Instance?.Toast(reason);
                upgradeButton?.GetComponent<UIButtonFeedback>()?.ShakeInvalid();
                return;
            }
            if (progress.TryUpgradeGuardian(guardianId))
            {
                Services.Get<AudioService>()?.PlayUi(AudioEventId.Upgrade);
                Services.Get<HapticService>()?.Medium();
                if (punchTarget != null) TGTween.PunchScale(punchTarget, 0.15f, 0.35f);
                Refresh();
            }
        }

        void OnEquipClicked()
        {
            if (progress == null) return;
            if (slotIndex >= 0)
            {
                if (progress.UnequipGuardian(slotIndex)) Services.Get<AudioService>()?.PlayUi(AudioEventId.Equip, 1f, unequipPitch);
            }
            else
            {
                int free = progress.FirstFreeGuardianSlot();
                if (free < 0)
                {
                    MenuUIController.Instance?.Toast("loadout_full");
                    equipButton?.GetComponent<UIButtonFeedback>()?.ShakeInvalid();
                    return;
                }
                if (progress.EquipGuardian(guardianId, free)) Services.Get<AudioService>()?.PlayUi(AudioEventId.Equip);
            }
            Refresh();
        }
    }
}
