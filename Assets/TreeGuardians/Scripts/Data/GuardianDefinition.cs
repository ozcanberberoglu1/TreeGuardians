using TreeGuardians.Core;
using UnityEngine;

namespace TreeGuardians.Data
{
    public enum GuardianSpecialKind
    {
        None = 0,
        PowerShot = 1,
        HealAllies = 2,
        RootTarget = 3,
        PoisonArea = 4,
        ChainEnergy = 5,
        ShieldSelf = 6,
        ArmorAllies = 7,
        ReduceToolCooldowns = 8,
        Salvo = 9,
        TeamBuff = 10
    }

    public enum GuardianPassiveKind
    {
        None = 0,
        CritBoost = 1,
        ShieldOnHit = 2,
        HealOverTime = 3,
        ArmorAura = 4,
        ToolCooldownReduction = 5,
        ExtraStructureDamage = 6,
        Rebirth = 7
    }

    [CreateAssetMenu(menuName = "Tree Guardians/Guardians/Guardian Definition", fileName = "Guardian_New")]
    public sealed class GuardianDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Benzersiz, değişmez kimlik. Save dosyasında bu kullanılır.")] public string id = "guardian_new";
        public string nameKey = "guardian_new_name";
        public string descriptionKey = "guardian_new_desc";
        public Rarity rarity = Rarity.Common;

        [Header("Visuals")]
        public Sprite portrait;
        public Sprite cardArt;
        [Tooltip("Sahnede kullanılan sprite (worldPrefab boşsa).")] public Sprite worldSprite;
        [Tooltip("GuardianView içeren prefab; boşsa varsayılan görünüm kullanılır.")] public GameObject worldPrefab;
        public Color tintColor = Color.white;
        [Tooltip("Ana menü ağacındaki görsel ölçek çarpanı (GuardianSpawnPoint.visualScale ile çarpılır).")] public float menuVisualScale = 1f;
        [Tooltip("Savaşta worldPrefab ölçeği (prefab'ın kendi ölçeğiyle çarpılır).")] public float battleVisualScale = 0.55f;

        [Header("Base Stats (Level 1)")]
        public float baseHealth = 300f;
        public float baseAttack = 40f;
        public float baseStructureDamage = 60f;
        public float baseArmor = 5f;
        [Range(0f, 1f)] public float critChance = 0.05f;
        public float critMultiplier = 1.5f;

        [Header("Combat")]
        public ProjectileDefinition normalProjectile;
        [Tooltip("Normal atışlar arası süre.")] public float attackCooldown = 2.5f;
        [Tooltip("Menzil (dünya birimi).")] public float range = 16f;
        public TargetPreference preferredTarget = TargetPreference.Nearest;
        [Tooltip("Uçan muhafızlar dal kırılınca kısa süre havada kalır.")] public bool isFlying;
        [Tooltip("Otomatik hafif hedef takibi yapar mı.")] public bool autoAttack = true;
        [Tooltip("Oyuncu bu muhafızla manuel nişan alabilir mi.")] public bool playerAimable = true;

        [Header("Special Ability")]
        public GuardianSpecialKind specialKind = GuardianSpecialKind.PowerShot;
        public string specialNameKey = "special_power_shot";
        [Tooltip("Özel yetenekte kullanılan mermi (PowerShot/Salvo/PoisonArea vb.).")] public ProjectileDefinition specialProjectile;
        [Tooltip("Gereken özel enerji.")] public float specialEnergyCost = 100f;
        [Tooltip("Yetenek büyüklüğü: iyileştirme miktarı, kalkan canı, zincir sayısı vb.")] public float specialMagnitude = 100f;
        public float specialDuration = 3f;

        [Header("Passive")]
        public GuardianPassiveKind passiveKind = GuardianPassiveKind.None;
        public float passiveMagnitude = 0.1f;

        [Header("Progression")]
        [Tooltip("Bu arena indeksinden itibaren açılabilir.")] public int arenaUnlockIndex = 0;
        [Tooltip("Prototipte tam oynanabilir mi (false = yalnız data/kart placeholder).")] public bool playableInPrototype = true;

        [Header("Audio")]
        public AudioEventId attackSfx = AudioEventId.ProjectileLaunch;
        public AudioEventId hurtSfx = AudioEventId.GuardianHurt;

        public float GetHealth(int level, GameBalanceConfig balance) =>
            baseHealth * (1f + balance.healthPerLevel * Mathf.Max(0, level - 1));

        public float GetAttack(int level, GameBalanceConfig balance) =>
            baseAttack * (1f + balance.attackPerLevel * Mathf.Max(0, level - 1));

        public float GetStructureDamage(int level, GameBalanceConfig balance) =>
            baseStructureDamage * (1f + balance.structureDamagePerLevel * Mathf.Max(0, level - 1));

        public float GetArmor(int level) => baseArmor + Mathf.Max(0, level - 1) * 0.5f;

        public int GetPower(int level, GameBalanceConfig balance)
        {
            float p = GetHealth(level, balance) * 0.1f + GetAttack(level, balance) + GetStructureDamage(level, balance) * 0.5f + GetArmor(level) * 2f;
            return Mathf.RoundToInt(p);
        }

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id)) Debug.LogWarning($"[GuardianDefinition] {name} has an empty id.", this);
            baseHealth = Mathf.Max(1f, baseHealth);
            baseAttack = Mathf.Max(0f, baseAttack);
            baseStructureDamage = Mathf.Max(0f, baseStructureDamage);
            baseArmor = Mathf.Max(0f, baseArmor);
            attackCooldown = Mathf.Max(0.2f, attackCooldown);
            range = Mathf.Max(1f, range);
            specialEnergyCost = Mathf.Max(1f, specialEnergyCost);
            if (playableInPrototype && normalProjectile == null)
                Debug.LogWarning($"[GuardianDefinition] {name} is playable but has no normal projectile.", this);
        }
    }
}
