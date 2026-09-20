using System;
using TMPro;
using TreeGuardians.Chests;
using TreeGuardians.Core;
using TreeGuardians.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.UI.Menu
{
    /// One of the four chest slots in the bottom bar.
    public sealed class ChestSlotView : MonoBehaviour
    {
        [SerializeField] int slotIndex;
        [SerializeField] Image chestIcon;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text timerText;
        [SerializeField] GameObject emptyState;
        [SerializeField] GameObject readyGlow;
        [SerializeField] Button button;

        Action onClick;
        float nextTick;

        public int SlotIndex => slotIndex;

        void Awake()
        {
            if (button == null) button = GetComponent<Button>();
            if (button != null) button.onClick.AddListener(() => onClick?.Invoke());
        }

        void OnEnable()
        {
            GameEventBus.Subscribe<ChestSlotsChangedEvent>(OnChestsChanged);
            GameEventBus.Subscribe<LanguageChangedEvent>(OnLanguageChanged);
            BootAwaiter.WhenReady(Refresh);
        }

        void OnDisable()
        {
            GameEventBus.Unsubscribe<ChestSlotsChangedEvent>(OnChestsChanged);
            GameEventBus.Unsubscribe<LanguageChangedEvent>(OnLanguageChanged);
        }

        void OnChestsChanged(ChestSlotsChangedEvent e) => Refresh();
        void OnLanguageChanged(LanguageChangedEvent e) => Refresh();

        public void SetClickHandler(Action handler) => onClick = handler;

        void Update()
        {
            if (!Services.IsBootstrapped || Time.unscaledTime < nextTick) return;
            nextTick = Time.unscaledTime + 0.5f;
            var chests = Services.Get<ChestService>();
            if (chests == null) return;
            var def = chests.GetDefinition(slotIndex);
            if (def == null) return;
            var slot = chests.Slots[slotIndex];
            if (slot.unlocking) UpdateTimer(chests, def.unlockSeconds);
        }

        public void Refresh()
        {
            if (!Services.IsBootstrapped) return;
            var chests = Services.Get<ChestService>();
            if (chests == null || this == null) return;
            var def = chests.GetDefinition(slotIndex);
            bool empty = def == null;
            if (emptyState != null) emptyState.SetActive(empty);
            if (chestIcon != null) chestIcon.enabled = !empty;
            if (readyGlow != null) readyGlow.SetActive(!empty && chests.IsReady(slotIndex));
            if (empty)
            {
                if (nameText != null) nameText.text = LocalizationService.Tr("chest_empty_slot");
                if (timerText != null) timerText.text = "";
                return;
            }
            var slot = chests.Slots[slotIndex];
            if (chestIcon != null) chestIcon.sprite = chests.IsReady(slotIndex) ? def.iconOpen : def.iconClosed;
            if (nameText != null) nameText.text = LocalizationService.Tr(def.nameKey);
            UpdateTimer(chests, def.unlockSeconds);
        }

        void UpdateTimer(ChestService chests, float unlockSeconds)
        {
            if (timerText == null) return;
            if (chests.IsReady(slotIndex))
            {
                timerText.text = LocalizationService.Tr("chest_ready");
                if (readyGlow != null && !readyGlow.activeSelf) readyGlow.SetActive(true);
                if (chestIcon != null) chestIcon.sprite = chests.GetDefinition(slotIndex)?.iconOpen;
                return;
            }
            var slot = chests.Slots[slotIndex];
            double rem = chests.GetRemainingSeconds(slotIndex);
            timerText.text = slot.unlocking ? FormatTime(rem) : FormatTime(unlockSeconds);
        }

        public static string FormatTime(double seconds)
        {
            if (seconds < 0) seconds = 0;
            var ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes:00}m";
            if (ts.TotalMinutes >= 1) return $"{ts.Minutes}m {ts.Seconds:00}s";
            return $"{ts.Seconds}s";
        }
    }
}
