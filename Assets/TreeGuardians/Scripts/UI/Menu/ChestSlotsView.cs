using System.Collections.Generic;
using TreeGuardians.Chests;
using TreeGuardians.Data;
using UnityEngine;

namespace TreeGuardians.UI.Menu
{
    /// Lives on MainMenu > BottomBar > ChestSlots. Holds the chest artwork assigned by hand in the Inspector (one row per
    /// chest type: closed + open sprite) and the ordered slot views under it. While enabled it is the chest art provider
    /// for every chest image in the menu (slots, chest panel, open popup, reward tiles).
    public sealed class ChestSlotsView : MonoBehaviour, IChestArtProvider
    {
        [Header("Chest Artwork (one row per chest type)")]
        [Tooltip("Her sandık tipi için kapalı ve açık görsel. Sandık tanımı boş bırakılan satırlar yok sayılır.")]
        [SerializeField] ChestVisualEntry[] chestVisuals = new ChestVisualEntry[0];

        [Header("Slots (array order = slot index)")]
        [Tooltip("Alt objelerdeki ChestSlotView bileşenleri. Sağ tık > 'Collect Slots From Children' ile doldurulur ve slot indeksleri yazılır.")]
        [SerializeField] ChestSlotView[] slots = new ChestSlotView[0];

        readonly Dictionary<ChestDefinition, ChestVisualEntry> lookup = new Dictionary<ChestDefinition, ChestVisualEntry>(8);
        bool lookupBuilt;

        public IReadOnlyList<ChestSlotView> Slots => slots;
        public int SlotCount => slots.Length;
        public IReadOnlyList<ChestVisualEntry> ChestVisuals => chestVisuals;

        void OnEnable()
        {
            lookupBuilt = false;
            ChestArt.Provider = this;
        }

        void OnDisable()
        {
            if (ReferenceEquals(ChestArt.Provider, this)) ChestArt.Provider = null;
        }

        public ChestSlotView GetSlot(int index) => index >= 0 && index < slots.Length ? slots[index] : null;

        public bool HasArt(ChestDefinition chest)
        {
            if (chest == null) return false;
            if (!lookupBuilt) BuildLookup();
            return lookup.TryGetValue(chest, out var e) && e.closedSprite != null && e.openSprite != null;
        }

        public Sprite GetChestSprite(ChestDefinition chest, bool open)
        {
            if (chest == null) return null;
            if (!lookupBuilt) BuildLookup();
            if (!lookup.TryGetValue(chest, out var e)) return null;
            return open ? e.openSprite : e.closedSprite;
        }

        void BuildLookup()
        {
            lookup.Clear();
            for (int i = 0; i < chestVisuals.Length; i++)
            {
                var e = chestVisuals[i];
                if (e == null || e.chest == null) continue;
                if (lookup.ContainsKey(e.chest))
                {
                    Debug.LogWarning($"[ChestSlotsView] Chest '{e.chest.id}' is listed twice; the first row wins.", this);
                    continue;
                }
                lookup.Add(e.chest, e);
            }
            lookupBuilt = true;
        }

#if UNITY_EDITOR
        bool syncScheduled;

        void OnValidate()
        {
            lookupBuilt = false;
            if (Application.isPlaying || syncScheduled || !NeedsSync()) return;
            syncScheduled = true;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                syncScheduled = false;
                if (this != null) ApplyArtToDefinitions();
            };
        }

        bool NeedsSync()
        {
            for (int i = 0; i < chestVisuals.Length; i++)
            {
                var e = chestVisuals[i];
                if (e == null || e.chest == null) continue;
                if (e.closedSprite != null && e.chest.iconClosed != e.closedSprite) return true;
                if (e.openSprite != null && e.chest.iconOpen != e.openSprite) return true;
            }
            return false;
        }

        /// Copies the sprites into the ChestDefinition assets so scenes without this view (battle results) show the same art.
        [ContextMenu("Apply Art To Chest Definitions")]
        public void ApplyArtToDefinitions()
        {
            int changed = 0;
            for (int i = 0; i < chestVisuals.Length; i++)
            {
                var e = chestVisuals[i];
                if (e == null || e.chest == null) continue;
                bool dirty = false;
                if (e.closedSprite != null && e.chest.iconClosed != e.closedSprite) { e.chest.iconClosed = e.closedSprite; dirty = true; }
                if (e.openSprite != null && e.chest.iconOpen != e.openSprite) { e.chest.iconOpen = e.openSprite; dirty = true; }
                if (dirty) { UnityEditor.EditorUtility.SetDirty(e.chest); changed++; }
            }
            if (changed > 0) Debug.Log($"[ChestSlotsView] Chest art applied to {changed} chest definition(s).", this);
        }

        /// Fills the slots array from the children in sibling order and writes matching slot indices.
        [ContextMenu("Collect Slots From Children")]
        public void CollectSlotsFromChildren()
        {
            var list = new List<ChestSlotView>(8);
            foreach (Transform child in transform)
            {
                var view = child.GetComponent<ChestSlotView>();
                if (view != null) list.Add(view);
            }
            slots = list.ToArray();
            for (int i = 0; i < slots.Length; i++) slots[i].EditorSetSlotIndex(i);
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
