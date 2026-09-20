using System.Collections.Generic;
using TMPro;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Localization;
using TreeGuardians.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.UI.Menu
{
    /// Collection screen: 8 equipped slots on top, filterable/sortable owned & locked grid below.
    public sealed class GuardiansPanel : UIPanel
    {
        enum Filter { All, Owned, Locked }
        enum Sort { Power, Level, Rarity, New }

        [Header("Equipped")]
        [SerializeField] GuardianCardView[] equippedSlots = new GuardianCardView[8];
        [SerializeField] TMP_Text equippedCountText;
        [SerializeField] TMP_Text hintText;

        [Header("Collection")]
        [SerializeField] RectTransform gridContent;
        [SerializeField] GuardianCardView cardTemplate;
        [SerializeField] Button filterAllButton;
        [SerializeField] Button filterOwnedButton;
        [SerializeField] Button filterLockedButton;
        [SerializeField] Button sortPowerButton;
        [SerializeField] Button sortLevelButton;
        [SerializeField] Button sortRarityButton;
        [SerializeField] Button sortNewButton;
        [SerializeField] Button closeButton;

        readonly List<GuardianCardView> cards = new List<GuardianCardView>(16);
        readonly List<GuardianDefinition> working = new List<GuardianDefinition>(16);
        Filter filter = Filter.All;
        Sort sort = Sort.Power;
        int selectedSlot = -1;
        PlayerProgressService progress;
        RarityPalette palette;

        protected override void Awake()
        {
            base.Awake();
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (filterAllButton != null) filterAllButton.onClick.AddListener(() => SetFilter(Filter.All));
            if (filterOwnedButton != null) filterOwnedButton.onClick.AddListener(() => SetFilter(Filter.Owned));
            if (filterLockedButton != null) filterLockedButton.onClick.AddListener(() => SetFilter(Filter.Locked));
            if (sortPowerButton != null) sortPowerButton.onClick.AddListener(() => SetSort(Sort.Power));
            if (sortLevelButton != null) sortLevelButton.onClick.AddListener(() => SetSort(Sort.Level));
            if (sortRarityButton != null) sortRarityButton.onClick.AddListener(() => SetSort(Sort.Rarity));
            if (sortNewButton != null) sortNewButton.onClick.AddListener(() => SetSort(Sort.New));
            for (int i = 0; i < equippedSlots.Length; i++)
            {
                if (equippedSlots[i] == null) continue;
                equippedSlots[i].SlotIndex = i;
                equippedSlots[i].SetClickHandler(OnEquippedSlotClicked);
            }
            if (cardTemplate != null) cardTemplate.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            GameEventBus.Subscribe<LoadoutChangedEvent>(OnLoadoutChanged);
            GameEventBus.Subscribe<GuardianUpgradedEvent>(OnGuardianChanged);
            GameEventBus.Subscribe<GuardianUnlockedEvent>(OnGuardianUnlocked);
            GameEventBus.Subscribe<GuardianCardsChangedEvent>(OnCardsChanged);
            GameEventBus.Subscribe<CurrencyChangedEvent>(OnCurrencyChanged);
            GameEventBus.Subscribe<LanguageChangedEvent>(OnLanguageChanged);
        }

        void OnDisable()
        {
            GameEventBus.Unsubscribe<LoadoutChangedEvent>(OnLoadoutChanged);
            GameEventBus.Unsubscribe<GuardianUpgradedEvent>(OnGuardianChanged);
            GameEventBus.Unsubscribe<GuardianUnlockedEvent>(OnGuardianUnlocked);
            GameEventBus.Unsubscribe<GuardianCardsChangedEvent>(OnCardsChanged);
            GameEventBus.Unsubscribe<CurrencyChangedEvent>(OnCurrencyChanged);
            GameEventBus.Unsubscribe<LanguageChangedEvent>(OnLanguageChanged);
        }

        void OnLoadoutChanged(LoadoutChangedEvent e) => Refresh();
        void OnGuardianChanged(GuardianUpgradedEvent e) => Refresh();
        void OnGuardianUnlocked(GuardianUnlockedEvent e) => Refresh();
        void OnCardsChanged(GuardianCardsChangedEvent e) => Refresh();
        void OnCurrencyChanged(CurrencyChangedEvent e) { if (e.type == CurrencyType.Coins) Refresh(); }
        void OnLanguageChanged(LanguageChangedEvent e) => Refresh();

        protected override void OnOpen()
        {
            selectedSlot = -1;
            Refresh();
        }

        void SetFilter(Filter f) { filter = f; Refresh(); }
        void SetSort(Sort s) { sort = s; Refresh(); }

        public void Refresh()
        {
            if (!IsOpen) return;
            progress = Services.Get<PlayerProgressService>();
            if (progress == null) return;
            palette = progress.Database.rarityPalette;

            for (int i = 0; i < equippedSlots.Length; i++)
            {
                var view = equippedSlots[i];
                if (view == null) continue;
                var id = progress.GetEquippedGuardianId(i);
                var def = progress.Database.GetGuardian(id);
                if (def == null) view.BindEmpty(); else view.Bind(def, progress, palette);
                view.SetSelected(i == selectedSlot);
            }
            if (equippedCountText != null) equippedCountText.text = $"{progress.EquippedGuardianCount}/{progress.Balance.guardianSlotCount}";
            if (hintText != null) hintText.text = selectedSlot >= 0 ? LocalizationService.Tr("guardian_equip") : "";

            working.Clear();
            var all = progress.Database.guardians;
            for (int i = 0; i < all.Count; i++)
            {
                var g = all[i];
                if (g == null) continue;
                bool unlocked = progress.IsGuardianUnlocked(g.id);
                if (filter == Filter.Owned && !unlocked) continue;
                if (filter == Filter.Locked && unlocked) continue;
                working.Add(g);
            }
            SortWorking();

            EnsureCards(working.Count);
            for (int i = 0; i < cards.Count; i++)
            {
                if (i < working.Count)
                {
                    cards[i].Bind(working[i], progress, palette);
                    cards[i].SetSelected(false);
                }
                else cards[i].gameObject.SetActive(false);
            }
            UpdateFilterButtons();
        }

        void SortWorking()
        {
            switch (sort)
            {
                case Sort.Level: working.Sort((a, b) => progress.GetGuardianLevel(b.id).CompareTo(progress.GetGuardianLevel(a.id))); break;
                case Sort.Rarity: working.Sort((a, b) => ((int)b.rarity).CompareTo((int)a.rarity)); break;
                case Sort.New: working.Sort((a, b) => progress.GetGuardianState(b.id).isNew.CompareTo(progress.GetGuardianState(a.id).isNew)); break;
                default: working.Sort((a, b) => CompareOwnedThenPower(a, b)); break;
            }
        }

        int CompareOwnedThenPower(GuardianDefinition a, GuardianDefinition b)
        {
            bool ua = progress.IsGuardianUnlocked(a.id), ub = progress.IsGuardianUnlocked(b.id);
            if (ua != ub) return ub.CompareTo(ua);
            return progress.GetGuardianPower(b.id).CompareTo(progress.GetGuardianPower(a.id));
        }

        void EnsureCards(int count)
        {
            if (cardTemplate == null || gridContent == null) return;
            while (cards.Count < count)
            {
                var c = Instantiate(cardTemplate, gridContent);
                c.name = "GuardianCard_" + cards.Count;
                c.SetClickHandler(OnCollectionCardClicked);
                cards.Add(c);
            }
        }

        void UpdateFilterButtons()
        {
            Highlight(filterAllButton, filter == Filter.All);
            Highlight(filterOwnedButton, filter == Filter.Owned);
            Highlight(filterLockedButton, filter == Filter.Locked);
            Highlight(sortPowerButton, sort == Sort.Power);
            Highlight(sortLevelButton, sort == Sort.Level);
            Highlight(sortRarityButton, sort == Sort.Rarity);
            Highlight(sortNewButton, sort == Sort.New);
        }

        static void Highlight(Button b, bool on)
        {
            if (b == null) return;
            var fb = b.GetComponent<UIButtonFeedback>();
            if (fb != null) fb.SetSelected(on);
        }

        void OnEquippedSlotClicked(GuardianCardView view)
        {
            if (view.IsEmpty)
            {
                selectedSlot = selectedSlot == view.SlotIndex ? -1 : view.SlotIndex;
                Refresh();
                return;
            }
            if (selectedSlot >= 0 && selectedSlot != view.SlotIndex)
            {
                progress.SwapEquipped(selectedSlot, view.SlotIndex);
                selectedSlot = -1;
                return;
            }
            selectedSlot = -1;
            MenuUIController.Instance?.OpenGuardianDetail(view.GuardianId, view.SlotIndex);
        }

        void OnCollectionCardClicked(GuardianCardView view)
        {
            if (!progress.IsGuardianUnlocked(view.GuardianId))
            {
                MenuUIController.Instance?.OpenGuardianDetail(view.GuardianId, -1);
                return;
            }
            if (selectedSlot >= 0)
            {
                progress.EquipGuardian(view.GuardianId, selectedSlot);
                selectedSlot = -1;
                progress.ClearNewFlag(view.GuardianId);
                return;
            }
            progress.ClearNewFlag(view.GuardianId);
            MenuUIController.Instance?.OpenGuardianDetail(view.GuardianId, progress.FindEquippedSlot(view.GuardianId));
        }
    }
}
