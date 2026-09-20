using System.Collections.Generic;
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
    /// Three equipped tool slots + collection, with an inline detail box for upgrade/equip.
    public sealed class ToolsPanel : UIPanel
    {
        [SerializeField] ToolCardView[] equippedSlots = new ToolCardView[3];
        [SerializeField] RectTransform gridContent;
        [SerializeField] ToolCardView cardTemplate;
        [Header("Detail")]
        [SerializeField] Image detailIcon;
        [SerializeField] TMP_Text detailName;
        [SerializeField] TMP_Text detailDesc;
        [SerializeField] TMP_Text detailStats;
        [SerializeField] TMP_Text detailCost;
        [SerializeField] Button upgradeButton;
        [SerializeField] Button equipButton;
        [SerializeField] TMP_Text equipLabel;
        [SerializeField] Button closeButton;
        [SerializeField] Color okColor = new Color(0.9f, 0.95f, 0.9f);
        [SerializeField] Color missingColor = new Color(1f, 0.4f, 0.35f);

        readonly List<ToolCardView> cards = new List<ToolCardView>(8);
        string selectedId;
        int selectedSlot = -1;
        PlayerProgressService progress;

        protected override void Awake()
        {
            base.Awake();
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (upgradeButton != null) upgradeButton.onClick.AddListener(OnUpgrade);
            if (equipButton != null) equipButton.onClick.AddListener(OnEquip);
            for (int i = 0; i < equippedSlots.Length; i++)
            {
                if (equippedSlots[i] == null) continue;
                equippedSlots[i].SlotIndex = i;
                equippedSlots[i].SetClickHandler(OnSlotClicked);
            }
            if (cardTemplate != null) cardTemplate.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            GameEventBus.Subscribe<ToolsChangedEvent>(OnTools);
            GameEventBus.Subscribe<CurrencyChangedEvent>(OnCurrency);
            GameEventBus.Subscribe<LanguageChangedEvent>(OnLanguage);
        }

        void OnDisable()
        {
            GameEventBus.Unsubscribe<ToolsChangedEvent>(OnTools);
            GameEventBus.Unsubscribe<CurrencyChangedEvent>(OnCurrency);
            GameEventBus.Unsubscribe<LanguageChangedEvent>(OnLanguage);
        }

        void OnTools(ToolsChangedEvent e) { if (IsOpen) Refresh(); }
        void OnCurrency(CurrencyChangedEvent e) { if (IsOpen && e.type == CurrencyType.Sap) Refresh(); }
        void OnLanguage(LanguageChangedEvent e) { if (IsOpen) Refresh(); }

        protected override void OnOpen()
        {
            selectedSlot = -1;
            selectedId = null;
            Refresh();
        }

        void Refresh()
        {
            progress = Services.Get<PlayerProgressService>();
            if (progress == null) return;
            for (int i = 0; i < equippedSlots.Length; i++)
            {
                var v = equippedSlots[i];
                if (v == null) continue;
                var def = progress.Database.GetTool(progress.GetEquippedToolId(i));
                if (def == null) v.BindEmpty(); else v.Bind(def, progress);
                v.SetSelected(i == selectedSlot);
            }
            var tools = progress.Database.tools;
            int n = 0;
            for (int i = 0; i < tools.Count; i++)
            {
                if (tools[i] == null) continue;
                var card = Card(n++);
                card.Bind(tools[i], progress);
                card.SetSelected(tools[i].id == selectedId);
            }
            for (int i = n; i < cards.Count; i++) cards[i].gameObject.SetActive(false);
            RefreshDetail();
        }

        ToolCardView Card(int i)
        {
            while (cards.Count <= i)
            {
                var c = Instantiate(cardTemplate, gridContent);
                c.name = "ToolCard_" + cards.Count;
                c.SetClickHandler(OnCardClicked);
                cards.Add(c);
            }
            return cards[i];
        }

        void RefreshDetail()
        {
            var def = progress.Database.GetTool(selectedId);
            bool has = def != null;
            if (detailIcon != null) { detailIcon.enabled = has; if (has) { detailIcon.sprite = def.icon; detailIcon.color = def.accentColor; } }
            if (detailName != null) detailName.text = has ? LocalizationService.Tr(def.nameKey) : "";
            if (detailDesc != null) detailDesc.text = has ? LocalizationService.Tr(def.descriptionKey) : "";
            if (!has)
            {
                if (detailStats != null) detailStats.text = "";
                if (detailCost != null) detailCost.text = "";
                if (upgradeButton != null) upgradeButton.interactable = false;
                if (equipButton != null) equipButton.interactable = false;
                return;
            }
            int level = progress.GetToolLevel(def.id);
            bool unlocked = progress.IsToolUnlocked(def.id);
            if (detailStats != null) detailStats.text = $"{LocalizationService.Tr("ui_level_short").Replace("{0}", level.ToString())}   {LocalizationService.Tr("guardian_cooldown")}: {def.GetCooldown(level):0}s   {LocalizationService.Tr("guardian_power")}: {def.GetMagnitude(level):0}";
            bool can = progress.CanUpgradeTool(def.id, out int cost, out string reason);
            if (detailCost != null)
            {
                if (!unlocked) { detailCost.text = string.Format(LocalizationService.Tr("ui_unlock_at"), def.arenaUnlockIndex + 1); detailCost.color = missingColor; }
                else if (level >= def.maxLevel) { detailCost.text = LocalizationService.Tr("ui_max"); detailCost.color = okColor; }
                else { detailCost.text = cost + " " + LocalizationService.Tr("currency_sap"); detailCost.color = progress.Wallet.CanAfford(CurrencyType.Sap, cost) ? okColor : missingColor; }
            }
            if (upgradeButton != null) upgradeButton.interactable = can;
            int equippedAt = progress.FindEquippedToolSlot(def.id);
            if (equipButton != null) equipButton.interactable = unlocked;
            if (equipLabel != null) equipLabel.text = LocalizationService.Tr(equippedAt >= 0 ? "ui_unequip" : "ui_equip");
        }

        void OnSlotClicked(ToolCardView v)
        {
            if (string.IsNullOrEmpty(v.ToolId)) { selectedSlot = selectedSlot == v.SlotIndex ? -1 : v.SlotIndex; Refresh(); return; }
            selectedId = v.ToolId;
            selectedSlot = -1;
            Refresh();
        }

        void OnCardClicked(ToolCardView v)
        {
            if (selectedSlot >= 0 && progress.IsToolUnlocked(v.ToolId))
            {
                progress.EquipTool(v.ToolId, selectedSlot);
                selectedSlot = -1;
            }
            selectedId = v.ToolId;
            Refresh();
        }

        void OnUpgrade()
        {
            if (progress == null || string.IsNullOrEmpty(selectedId)) return;
            if (!progress.CanUpgradeTool(selectedId, out _, out string reason))
            {
                if (reason == "insufficient_sap") MenuUIController.Instance?.ShowInsufficient(CurrencyType.Sap);
                else MenuUIController.Instance?.Toast(reason);
                return;
            }
            if (progress.TryUpgradeTool(selectedId))
            {
                Services.Get<AudioService>()?.PlayUi(AudioEventId.Upgrade);
                Refresh();
            }
        }

        void OnEquip()
        {
            if (progress == null || string.IsNullOrEmpty(selectedId)) return;
            int at = progress.FindEquippedToolSlot(selectedId);
            if (at >= 0) { progress.UnequipTool(at); Refresh(); return; }
            int free = -1;
            for (int i = 0; i < progress.Balance.toolSlotCount; i++) if (string.IsNullOrEmpty(progress.GetEquippedToolId(i))) { free = i; break; }
            if (free < 0) { MenuUIController.Instance?.Toast("tools_equipped"); return; }
            progress.EquipTool(selectedId, free);
            Refresh();
        }
    }
}
