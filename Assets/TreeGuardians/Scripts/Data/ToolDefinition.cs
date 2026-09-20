using UnityEngine;

namespace TreeGuardians.Data
{
    [CreateAssetMenu(menuName = "Tree Guardians/Tools/Tool Definition", fileName = "Tool_New")]
    public sealed class ToolDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id = "tool_new";
        public string nameKey = "tool_new_name";
        public string descriptionKey = "tool_new_desc";
        public Sprite icon;
        public Color accentColor = Color.white;

        [Header("Effect")]
        public ToolEffectType effect = ToolEffectType.Projectile;
        [Tooltip("Projectile/HomingSwarm etkilerinde kullanılan mermi.")] public ProjectileDefinition projectile;
        [Tooltip("Etki büyüklüğü: hasar, iyileştirme, kalkan canı, yavaşlatma oranı.")] public float effectMagnitude = 100f;
        public float effectDuration = 3f;
        public float effectRadius = 2f;
        [Tooltip("HomingSwarm için mermi sayısı.")] public int projectileCount = 1;
        public float cooldownSeconds = 20f;
        [Tooltip("Oyuncunun hedef seçmesi gerekir mi (false = otomatik hedef).")] public bool requiresAim = true;

        [Header("Progression")]
        public int maxLevel = 10;
        public int baseUpgradeSap = 20;
        public float upgradeGrowth = 1.4f;
        [Tooltip("Seviye başına etki artışı.")] public float magnitudePerLevel = 0.08f;
        [Tooltip("Seviye başına cooldown azalması (oran).")] public float cooldownReductionPerLevel = 0.02f;
        public int arenaUnlockIndex = 0;
        public bool playableInPrototype = true;

        [Header("Audio")]
        public AudioEventId useSfx = AudioEventId.ToolUse;

        public float GetMagnitude(int level) => effectMagnitude * (1f + magnitudePerLevel * Mathf.Max(0, level - 1));
        public float GetCooldown(int level) => cooldownSeconds * Mathf.Max(0.4f, 1f - cooldownReductionPerLevel * Mathf.Max(0, level - 1));
        public int GetUpgradeSapCost(int level) => Mathf.Max(1, Mathf.RoundToInt(baseUpgradeSap * Mathf.Pow(upgradeGrowth, Mathf.Max(0, level - 1))));

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id)) Debug.LogWarning($"[ToolDefinition] {name} has an empty id.", this);
            cooldownSeconds = Mathf.Max(1f, cooldownSeconds);
            maxLevel = Mathf.Max(1, maxLevel);
            projectileCount = Mathf.Max(1, projectileCount);
        }
    }
}
