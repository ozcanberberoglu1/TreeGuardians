using System;
using System.Collections.Generic;
using UnityEngine;

namespace TreeGuardians.Data
{
    [Serializable]
    public sealed class TreeSectionSpec
    {
        [Tooltip("Ağaç içinde benzersiz bölüm kimliği (ör. bark_L1).")] public string sectionId = "section";
        public TreeSectionType type = TreeSectionType.BarkArmor;
        public float baseHealth = 200f;
        public float armor = 5f;
        [Tooltip("Zehir/Doğa/Işık direnci 0..1 (0 = yok).")] public float poisonResistance;
        [Tooltip("Bu bölümü taşıyan üst bölüm (boş = kök). Üst bölüm kırılırsa bu bölüm de etkilenir.")] public string parentSectionId = "";
        [Tooltip("Bu bölüm kırılınca açığa çıkan (tam hasar alabilen) bölümler.")] public string[] protectsSectionIds = Array.Empty<string>();
        [Tooltip("Bu dal hangi muhafız yuvasını taşır (-1 = yok).")] public int guardianSlotIndex = -1;
        [Tooltip("SceneBuilder için yerel konum (ağaç kökünden).")] public Vector2 localPosition;
        [Tooltip("SceneBuilder için görsel boyut.")] public Vector2 size = new Vector2(1.2f, 1.2f);
        [Tooltip("Başlangıçta hedeflenebilir mi.")] public bool initiallyTargetable = true;
        [Tooltip("Kırılınca üstteki muhafız davranışı (yalnız Branch).")] public BranchBreakBehavior breakBehavior = BranchBreakBehavior.FallToEmptySlot;
        [Tooltip("Kale parçasının sprite'ı (destructibleCastle ağaçlar için).")] public Sprite sprite;
    }

    [Serializable]
    public sealed class TreeVisualSet
    {
        public TreeVisualTier tier;
        public Sprite trunk;
        public Sprite branch;
        public Sprite bark;
        public Sprite canopy;
        public Sprite root;
        public Sprite heartwood;
        public Color leafColor = new Color(0.35f, 0.7f, 0.3f);
        public Color barkColor = new Color(0.45f, 0.3f, 0.18f);
    }

    [CreateAssetMenu(menuName = "Tree Guardians/Trees/Tree Definition", fileName = "Tree_New")]
    public sealed class TreeDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id = "tree_player";
        public string nameKey = "tree_player_name";
        [Tooltip("Parçalı kale: bölümler delinebilir duvarlardır, kalp özü yoktur; savaş sahnesi BattleSceneBuilder.BuildCastle ile kurulur.")] public bool destructibleCastle;

        [Header("Core")]
        public float heartwoodBaseHealth = 1200f;
        public float heartwoodArmor = 10f;
        [Tooltip("Kalp Özü korunurken alınan hasar oranı.")] [Range(0f, 1f)] public float protectedDamageMultiplier = 0.15f;

        [Header("Sections")]
        public List<TreeSectionSpec> sections = new List<TreeSectionSpec>();

        [Header("Slots (local positions)")]
        public Vector2[] guardianSlotPositions = new Vector2[8];
        public Vector2[] toolMountPositions = new Vector2[3];

        [Header("Visual Tiers")]
        public TreeVisualSet[] visuals = new TreeVisualSet[3];

        [Header("Upgrade Scaling")]
        [Tooltip("Kalp Özü seviyesi başına can artışı.")] public float heartwoodHealthPerLevel = 0.08f;
        [Tooltip("Kabuk kalınlığı seviyesi başına zırh.")] public float barkArmorPerLevel = 1.5f;
        [Tooltip("Dal dayanıklılığı seviyesi başına can artışı.")] public float branchHealthPerLevel = 0.08f;
        [Tooltip("Kök gücü seviyesi başına alan/sarsıntı hasarı azaltma.")] public float rootDamageReductionPerLevel = 0.03f;
        [Tooltip("Öz akışı seviyesi başına enerji dolum artışı.")] public float sapFlowEnergyPerLevel = 0.06f;

        public TreeSectionSpec FindSection(string sectionId)
        {
            for (int i = 0; i < sections.Count; i++)
                if (sections[i].sectionId == sectionId) return sections[i];
            return null;
        }

        public TreeVisualSet GetVisuals(TreeVisualTier tier)
        {
            if (visuals != null)
                for (int i = 0; i < visuals.Length; i++)
                    if (visuals[i] != null && visuals[i].tier == tier) return visuals[i];
            return visuals != null && visuals.Length > 0 ? visuals[0] : null;
        }

        void OnValidate()
        {
            if (guardianSlotPositions == null || guardianSlotPositions.Length != 8) guardianSlotPositions = new Vector2[8];
            if (toolMountPositions == null || toolMountPositions.Length != 3) toolMountPositions = new Vector2[3];
            var seen = new HashSet<string>();
            for (int i = 0; i < sections.Count; i++)
            {
                var s = sections[i];
                if (s == null) continue;
                if (string.IsNullOrWhiteSpace(s.sectionId)) Debug.LogWarning($"[TreeDefinition] {name}: section #{i} has empty id.", this);
                else if (!seen.Add(s.sectionId)) Debug.LogWarning($"[TreeDefinition] {name}: duplicate section id '{s.sectionId}'.", this);
                s.baseHealth = Mathf.Max(1f, s.baseHealth);
                s.armor = Mathf.Max(0f, s.armor);
            }
        }
    }
}
