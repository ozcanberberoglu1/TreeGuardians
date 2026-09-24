using TMPro;
using TreeGuardians.Chests;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.UI.Menu
{
    /// Details for one chest slot: start timer, skip with gems, or open when ready.
    public sealed class ChestsPanel : UIPanel
    {
        [SerializeField] Image chestImage;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text timerText;
        [SerializeField] TMP_Text infoText;
        [SerializeField] Button startButton;
        [SerializeField] Button skipButton;
        [SerializeField] TMP_Text skipLabel;
        [SerializeField] Button openButton;
        [SerializeField] Button closeButton;
        [SerializeField] Button prevButton;
        [SerializeField] Button nextButton;
        [SerializeField] TMP_Text slotIndexText;

        int slotIndex;
        float nextTick;

        protected override void Awake()
        {
            base.Awake();
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (startButton != null) startButton.onClick.AddListener(OnStart);
            if (skipButton != null) skipButton.onClick.AddListener(OnSkip);
            if (openButton != null) openButton.onClick.AddListener(OnOpenChest);
            if (prevButton != null) prevButton.onClick.AddListener(() => ShowSlot(slotIndex - 1));
            if (nextButton != null) nextButton.onClick.AddListener(() => ShowSlot(slotIndex + 1));
        }

        void OnEnable()
        {
            GameEventBus.Subscribe<ChestSlotsChangedEvent>(OnChanged);
        }

        void OnDisable()
        {
            GameEventBus.Unsubscribe<ChestSlotsChangedEvent>(OnChanged);
        }

        void OnChanged(ChestSlotsChangedEvent e) { if (IsOpen) Refresh(); }

        public void ShowSlot(int index)
        {
            var chests = Services.Get<ChestService>();
            if (chests == null || chests.SlotCount <= 0) return;
            int count = chests.SlotCount;
            slotIndex = ((index % count) + count) % count;
            if (!IsOpen) Open();
            Refresh();
        }

        void Update()
        {
            if (!IsOpen || !Services.IsBootstrapped || Time.unscaledTime < nextTick) return;
            nextTick = Time.unscaledTime + 0.5f;
            Refresh();
        }

        void Refresh()
        {
            var chests = Services.Get<ChestService>();
            if (chests == null) return;
            var def = chests.GetDefinition(slotIndex);
            if (slotIndexText != null) slotIndexText.text = $"{slotIndex + 1}/{chests.SlotCount}";
            bool empty = def == null;
            if (chestImage != null) { chestImage.enabled = !empty; if (!empty) chestImage.sprite = chests.IsReady(slotIndex) ? ChestArt.Open(def) : ChestArt.Closed(def); }
            if (nameText != null) nameText.text = empty ? LocalizationService.Tr("chest_empty_slot") : LocalizationService.Tr(def.nameKey);
            if (startButton != null) startButton.gameObject.SetActive(!empty && chests.CanStartUnlock(slotIndex));
            if (openButton != null) openButton.gameObject.SetActive(!empty && chests.CanOpen(slotIndex));
            bool canSkip = !empty && !chests.IsReady(slotIndex);
            if (skipButton != null) skipButton.gameObject.SetActive(canSkip);
            if (canSkip && skipLabel != null) skipLabel.text = string.Format(LocalizationService.Tr("chest_skip"), chests.GetSkipGemCost(slotIndex));
            if (timerText != null)
            {
                if (empty) timerText.text = "";
                else if (chests.IsReady(slotIndex)) timerText.text = LocalizationService.Tr("chest_ready");
                else timerText.text = ChestSlotView.FormatTime(chests.GetRemainingSeconds(slotIndex));
            }
            if (infoText != null)
            {
                if (empty) infoText.text = "";
                else if (!chests.Slots[slotIndex].unlocking && chests.IsAnyUnlocking && !chests.IsReady(slotIndex)) infoText.text = LocalizationService.Tr("chest_busy");
                else if (chests.Slots[slotIndex].unlocking && !chests.IsReady(slotIndex)) infoText.text = LocalizationService.Tr("chest_unlocking");
                else infoText.text = $"{def.cardDraws} {LocalizationService.Tr("guardian_cards")} · {def.coinsMin}-{def.coinsMax} {LocalizationService.Tr("currency_coins")}";
            }
        }

        void OnStart()
        {
            var chests = Services.Get<ChestService>();
            if (chests != null && chests.StartUnlock(slotIndex)) Refresh();
            else MenuUIController.Instance?.Toast("chest_busy");
        }

        void OnSkip()
        {
            var chests = Services.Get<ChestService>();
            if (chests == null) return;
            if (!chests.TrySkipWithGems(slotIndex)) MenuUIController.Instance?.ShowInsufficient(CurrencyType.Gems);
            else Refresh();
        }

        void OnOpenChest()
        {
            var chests = Services.Get<ChestService>();
            if (chests == null || !chests.CanOpen(slotIndex)) return;
            Close();
            MenuUIController.Instance?.OpenChestPopup(slotIndex);
        }
    }
}
