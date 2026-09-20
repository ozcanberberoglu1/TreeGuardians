using System;
using UnityEngine;

namespace TreeGuardians.Data
{
    [Serializable]
    public struct StatusEffectSpec
    {
        public StatusEffectType type;
        [Tooltip("Etki süresi (saniye).")] public float duration;
        [Tooltip("Etki büyüklüğü: zehir için saniyelik hasar, yavaşlatma için oran, kalkan için can.")] public float magnitude;
    }

    [Serializable]
    public struct ShakeProfile
    {
        [Tooltip("Sarsıntı genliği (dünya birimi).")] public float amplitude;
        [Tooltip("Sarsıntı süresi (saniye).")] public float duration;
    }

    [CreateAssetMenu(menuName = "Tree Guardians/Battle/Projectile Definition", fileName = "Projectile_New")]
    public sealed class ProjectileDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id = "projectile_new";
        public string nameKey = "projectile_new";
        public Sprite icon;

        [Header("Prefab & Visual")]
        [Tooltip("ProjectileController içeren prefab. Boşsa varsayılan mermi prefabı kullanılır.")]
        public GameObject prefab;
        public Color tint = Color.white;
        public float visualScale = 1f;

        [Header("Motion")]
        public ProjectileMotion motion = ProjectileMotion.Straight;
        public float speed = 18f;
        [Tooltip("Ballistic modda yerçekimi çarpanı.")] public float gravityScale = 1f;
        public float lifetime = 5f;
        [Tooltip("Homing modda hedefe dönüş gücü.")] public float homingStrength = 3f;
        public int bounceCount = 0;
        public int pierceCount = 0;
        [Tooltip("Çarpışma yarıçapı.")] public float collisionRadius = 0.25f;

        [Header("Damage")]
        public float baseDamage = 30f;
        [Tooltip("Ağaç parçalarına hasar çarpanı.")] public float structureMultiplier = 1f;
        [Tooltip("Muhafızlara hasar çarpanı.")] public float guardianMultiplier = 1f;
        [Tooltip("Alan hasarı yarıçapı (0 = yok).")] public float splashRadius = 0f;
        [Range(0f, 1f)] [Tooltip("Yarıçap kenarında kalan hasar oranı.")] public float splashFalloff = 0.4f;
        public bool canHitOwnSide;
        public StatusEffectSpec statusEffect;

        [Header("Feedback")]
        public GameObject impactVfxPrefab;
        public AudioEventId launchSfx = AudioEventId.ProjectileLaunch;
        public AudioEventId impactSfx = AudioEventId.BarkHit;
        public ShakeProfile shake = new ShakeProfile { amplitude = 0.05f, duration = 0.1f };

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id)) Debug.LogWarning($"[ProjectileDefinition] {name} has an empty id.", this);
            speed = Mathf.Max(0.1f, speed);
            lifetime = Mathf.Max(0.1f, lifetime);
            baseDamage = Mathf.Max(0f, baseDamage);
            bounceCount = Mathf.Max(0, bounceCount);
            pierceCount = Mathf.Max(0, pierceCount);
            collisionRadius = Mathf.Max(0.02f, collisionRadius);
        }
    }
}
