using UnityEngine;

namespace TreeGuardians.Data
{
    [CreateAssetMenu(menuName = "Tree Guardians/Chests/Chest Definition", fileName = "Chest_New")]
    public sealed class ChestDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id = "chest_new";
        public string nameKey = "chest_new_name";
        public ChestTier tier = ChestTier.Twig;
        public Sprite iconClosed;
        public Sprite iconOpen;
        public Color glowColor = new Color(1f, 0.85f, 0.3f);

        [Header("Contents")]
        public int coinsMin = 40;
        public int coinsMax = 80;
        public int sapMin = 2;
        public int sapMax = 6;
        public int gemsMin = 0;
        public int gemsMax = 0;
        [Tooltip("Çekilecek kart sayısı.")] public int cardDraws = 3;
        [Tooltip("Common, Rare, Epic, Legendary ağırlıkları.")] public float[] rarityWeights = { 70f, 25f, 4.5f, 0.5f };
        [Tooltip("Bu nadirlikten en az bir kart garanti edilir (None = garanti yok).")] public bool guaranteeEpic;
        [Tooltip("Pity: bu kadar sandıkta Epic çıkmazsa garanti edilir (0 = kapalı).")] public int epicPityThreshold = 0;

        [Header("Timing")]
        [Tooltip("Açılma süresi (saniye).")] public float unlockSeconds = 900f;
        [Tooltip("Arena indeksi başına ödül çarpanı artışı.")] public float arenaTierBonusPerIndex = 0.15f;
        [Tooltip("Süreyi atlamak için gem maliyeti (saat başına).")] public int gemSkipCostPerHour = 6;

        public float GetRarityWeight(Rarity r)
        {
            int i = (int)r;
            return rarityWeights != null && i < rarityWeights.Length ? Mathf.Max(0f, rarityWeights[i]) : 0f;
        }

        public int GetSkipGemCost(float remainingSeconds)
        {
            if (remainingSeconds <= 0f) return 0;
            return Mathf.Max(1, Mathf.CeilToInt(remainingSeconds / 3600f * gemSkipCostPerHour));
        }

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id)) Debug.LogWarning($"[ChestDefinition] {name} has an empty id.", this);
            coinsMin = Mathf.Max(0, coinsMin); coinsMax = Mathf.Max(coinsMin, coinsMax);
            sapMin = Mathf.Max(0, sapMin); sapMax = Mathf.Max(sapMin, sapMax);
            gemsMin = Mathf.Max(0, gemsMin); gemsMax = Mathf.Max(gemsMin, gemsMax);
            cardDraws = Mathf.Max(0, cardDraws);
            unlockSeconds = Mathf.Max(0f, unlockSeconds);
            if (rarityWeights == null || rarityWeights.Length != 4) rarityWeights = new[] { 70f, 25f, 4.5f, 0.5f };
        }
    }
}
