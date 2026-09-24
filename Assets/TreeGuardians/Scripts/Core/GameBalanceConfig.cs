using System;
using TreeGuardians.Data;
using UnityEngine;

namespace TreeGuardians.Core
{
    [Serializable]
    public sealed class RarityUpgradeCost
    {
        public Rarity rarity;
        [Tooltip("Seviye 1→2 için gereken kart sayısı.")] public int baseCards = 2;
        [Tooltip("Her seviyede kart gereksiniminin çarpanı.")] public float cardsGrowth = 1.35f;
        [Tooltip("Seviye 1→2 için gereken coin.")] public int baseCoins = 50;
        [Tooltip("Her seviyede coin maliyetinin çarpanı.")] public float coinsGrowth = 1.42f;
        [Tooltip("Kilidi açmak için gereken kart sayısı (0 = ilk kart açar).")] public int cardsToUnlock = 1;
    }

    [Serializable]
    public sealed class TimeUpScoring
    {
        [Tooltip("Kalan Kalp Özü yüzdesinin puana katkısı.")] public float coreHealthWeight = 1.0f;
        [Tooltip("Yaşayan muhafız başına puan.")] public float aliveGuardianWeight = 8f;
        [Tooltip("Verilen yapısal hasarın puana katkısı (yüzde başına).")] public float structureDamageWeight = 0.5f;
    }

    [CreateAssetMenu(menuName = "Tree Guardians/Balance/Game Balance Config", fileName = "GameBalanceConfig")]
    public sealed class GameBalanceConfig : ScriptableObject
    {
        [Header("Starting Resources")]
        public int startingCoins = 1500;
        public int startingSap = 120;
        public int startingGems = 20;
        public int startingTrophies = 0;
        [Tooltip("Yeni kayıtta açık gelen muhafız id'leri.")] public string[] starterGuardianIds = Array.Empty<string>();
        [Tooltip("Yeni kayıtta açık gelen araç id'leri.")] public string[] starterToolIds = Array.Empty<string>();
        [Tooltip("Yeni kayıtta ilk slota konan ücretsiz sandık id'si (boş = yok).")] public string starterChestId = "";

        [Header("Guardians")]
        [Tooltip("Tüm muhafızlar için maksimum seviye.")] public int guardianMaxLevel = 30;
        [Tooltip("Seviye başına can artışı (0.06 = %6).")] public float healthPerLevel = 0.06f;
        [Tooltip("Seviye başına saldırı artışı.")] public float attackPerLevel = 0.06f;
        [Tooltip("Seviye başına yapı hasarı artışı.")] public float structureDamagePerLevel = 0.05f;
        public RarityUpgradeCost[] upgradeCosts =
        {
            new RarityUpgradeCost { rarity = Rarity.Common, baseCards = 2, cardsGrowth = 1.35f, baseCoins = 40, coinsGrowth = 1.4f, cardsToUnlock = 1 },
            new RarityUpgradeCost { rarity = Rarity.Rare, baseCards = 2, cardsGrowth = 1.4f, baseCoins = 80, coinsGrowth = 1.42f, cardsToUnlock = 2 },
            new RarityUpgradeCost { rarity = Rarity.Epic, baseCards = 1, cardsGrowth = 1.5f, baseCoins = 200, coinsGrowth = 1.45f, cardsToUnlock = 2 },
            new RarityUpgradeCost { rarity = Rarity.Legendary, baseCards = 1, cardsGrowth = 1.6f, baseCoins = 500, coinsGrowth = 1.5f, cardsToUnlock = 3 },
        };

        [Header("Tree")]
        [Tooltip("Ağaçtaki toplam muhafız yuvası.")] public int guardianSlotCount = 8;
        [Tooltip("Savaşta takılı araç sayısı.")] public int toolSlotCount = 3;
        [Tooltip("Ağaç geliştirme yollarının maksimum seviyesi.")] public int treeUpgradeMaxLevel = 10;
        [Tooltip("Ağaç geliştirme temel sap maliyeti.")] public int treeUpgradeBaseSap = 30;
        [Tooltip("Ağaç geliştirme maliyet çarpanı.")] public float treeUpgradeGrowth = 1.5f;
        [Tooltip("Toplam ağaç seviyesi bu değere ulaşınca 'Güçlü' görünüm.")] public int treeTierStrongThreshold = 10;
        [Tooltip("Toplam ağaç seviyesi bu değere ulaşınca 'Kadim' görünüm.")] public int treeTierAncientThreshold = 24;

        [Header("Battle Timing")]
        public float battleDurationSeconds = 150f;
        public float readyCountdownSeconds = 3f;
        public float introSeconds = 1.2f;
        public float resultRevealDelaySeconds = 1.5f;

        [Header("Battle Rules")]
        [Tooltip("Otomatik (hafif) atışların hasar çarpanı; manuel nişan tam hasar verir.")]
        [Range(0.1f, 1f)] public float autoAttackDamageMultiplier = 0.5f;
        [Tooltip("Otomatik atışların bekleme süresi çarpanı.")]
        [Range(1f, 4f)] public float autoAttackCooldownMultiplier = 1.6f;
        [Tooltip("Dal kırılınca muhafızın alacağı hasar (maks canın yüzdesi).")]
        [Range(0f, 1f)] public float branchBreakDamagePercent = 0.35f;
        [Tooltip("Dal kırılınca sersemleme süresi.")] public float branchBreakStunSeconds = 1.5f;
        [Tooltip("Özel enerji dolum hızı (saniyede).")] public float specialEnergyPerSecond = 4f;
        [Tooltip("Vuruş başına kazanılan özel enerji.")] public float specialEnergyPerHit = 6f;
        public float specialEnergyMax = 100f;
        public TimeUpScoring timeUpScoring = new TimeUpScoring();

        [Header("Trophies")]
        public int trophiesOnWin = 30;
        public int trophiesOnLoss = 15;
        public int trophiesOnDraw = 5;

        [Header("Battle Rewards")]
        public int winCoinsBase = 120;
        public int loseCoinsBase = 30;
        public int winSapBase = 12;
        public int loseSapBase = 3;
        [Tooltip("Zaferde verilen rastgele kart sayısı.")] public int winCardDraws = 2;
        [Tooltip("Zaferde boş slot varsa sandık verme olasılığı.")] [Range(0f, 1f)] public float winChestChance = 1f;

        [Header("Chests")]
        [Tooltip("Ana menüdeki sandık yuvası sayısı; MainMenu > BottomBar > ChestSlots altındaki slot sayısıyla eşleşmeli.")] public int chestSlotCount = 6;
        [Tooltip("Sandık anahtarı sistemi aktif mi (prototipte kapalı).")] public bool chestKeysEnabled;

        [Header("Daily Reward")]
        [Tooltip("İki günlük ödül arasında beklenmesi gereken saat.")] public float dailyRewardCooldownHours = 20f;

        public RarityUpgradeCost GetUpgradeCost(Rarity rarity)
        {
            for (int i = 0; i < upgradeCosts.Length; i++)
                if (upgradeCosts[i].rarity == rarity) return upgradeCosts[i];
            return upgradeCosts.Length > 0 ? upgradeCosts[0] : new RarityUpgradeCost();
        }

        public int GetCardsRequired(Rarity rarity, int currentLevel)
        {
            var c = GetUpgradeCost(rarity);
            return Mathf.Max(1, Mathf.RoundToInt(c.baseCards * Mathf.Pow(c.cardsGrowth, Mathf.Max(0, currentLevel - 1))));
        }

        public int GetCoinsRequired(Rarity rarity, int currentLevel)
        {
            var c = GetUpgradeCost(rarity);
            return Mathf.Max(1, Mathf.RoundToInt(c.baseCoins * Mathf.Pow(c.coinsGrowth, Mathf.Max(0, currentLevel - 1))));
        }

        public int GetTreeUpgradeSapCost(int currentLevel)
        {
            return Mathf.Max(1, Mathf.RoundToInt(treeUpgradeBaseSap * Mathf.Pow(treeUpgradeGrowth, Mathf.Max(0, currentLevel))));
        }

        void OnValidate()
        {
            guardianMaxLevel = Mathf.Max(1, guardianMaxLevel);
            guardianSlotCount = Mathf.Clamp(guardianSlotCount, 1, 8);
            toolSlotCount = Mathf.Clamp(toolSlotCount, 1, 3);
            chestSlotCount = Mathf.Clamp(chestSlotCount, 1, 8);
            battleDurationSeconds = Mathf.Max(10f, battleDurationSeconds);
        }
    }
}
