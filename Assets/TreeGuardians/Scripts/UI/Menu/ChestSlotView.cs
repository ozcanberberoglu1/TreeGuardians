using System;
using TMPro;
using TreeGuardians.Chests;
using TreeGuardians.Core;
using TreeGuardians.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.UI.Menu
{
    /// One chest slot in the bottom bar. Its index is the position under ChestSlotsView (MainMenu > BottomBar > ChestSlots).
    public sealed class ChestSlotView : MonoBehaviour
    {
        [SerializeField] int slotIndex;
        [SerializeField] Image chestIcon;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text timerText;
        [SerializeField] GameObject emptyState;
        [SerializeField] GameObject readyGlow;
        [SerializeField] Button button;
        [Tooltip("Kapalıysa sandık varken isim gizlenir (sandık görseli tipi belirtir; uzun isimler slot çerçevesinden taşmaz). Boş slotta 'Boş' yazısı her zaman görünür.")]
        [SerializeField] bool showNameWhenFilled;

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
                if (nameText != null) { nameText.gameObject.SetActive(true); nameText.text = LocalizationService.Tr("chest_empty_slot"); }
                if (timerText != null) timerText.text = "";
                return;
            }
            if (chestIcon != null) chestIcon.sprite = chests.IsReady(slotIndex) ? ChestArt.Open(def) : ChestArt.Closed(def);
            if (nameText != null)
            {
                nameText.gameObject.SetActive(showNameWhenFilled);
                if (showNameWhenFilled) nameText.text = LocalizationService.Tr(def.nameKey);
            }
            UpdateTimer(chests, def.unlockSeconds);
        }

        void UpdateTimer(ChestService chests, float unlockSeconds)
        {
            if (timerText == null) return;
            if (chests.IsReady(slotIndex))
            {
                timerText.text = LocalizationService.Tr("chest_ready");
                if (readyGlow != null && !readyGlow.activeSelf) readyGlow.SetActive(true);
                if (chestIcon != null) chestIcon.sprite = ChestArt.Open(chests.GetDefinition(slotIndex));
                return;
            }
            var slot = chests.Slots[slotIndex];
            double rem = chests.GetRemainingSeconds(slotIndex);
            timerText.text = slot.unlocking ? FormatTime(rem) : FormatTime(unlockSeconds);
        }

#if UNITY_EDITOR
        public void EditorSetSlotIndex(int index)
        {
            slotIndex = index;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        public static string FormatTime(double seconds) => LocalizationService.Duration(seconds);
    }
}
