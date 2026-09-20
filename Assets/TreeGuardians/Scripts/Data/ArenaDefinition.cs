using System;
using UnityEngine;

namespace TreeGuardians.Data
{
    [Serializable]
    public struct ChestDrop
    {
        public string chestId;
        [Tooltip("Ağırlık; yüksek değer daha sık düşer.")] public float weight;
    }

    [CreateAssetMenu(menuName = "Tree Guardians/Arenas/Arena Definition", fileName = "Arena_New")]
    public sealed class ArenaDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id = "arena_new";
        public string nameKey = "arena_new_name";
        [Tooltip("Sıralama ve kilit açma için indeks (0 = ilk arena).")] public int arenaIndex = 0;
        public int unlockTrophies = 0;
        public int recommendedPower = 400;
        public Sprite badge;
        [Tooltip("Prototipte tam çalışır mı (false = data/visual placeholder).")] public bool fullyPlayable = true;

        [Header("Palette")]
        public Color skyTop = new Color(0.45f, 0.75f, 0.98f);
        public Color skyBottom = new Color(0.82f, 0.93f, 1f);
        public Color ambientTint = Color.white;
        public Color groundColor = new Color(0.36f, 0.6f, 0.3f);
        public Color fogColor = new Color(1f, 1f, 1f, 0f);

        [Header("Layer Sprites (optional; placeholder generated if empty)")]
        public Sprite cloudsSprite;
        public Sprite mountainsSprite;
        public Sprite forestBackSprite;
        public Sprite forestMidSprite;
        public Sprite groundSprite;
        public Sprite foregroundSprite;

        [Header("Parallax")]
        public float cloudSpeed = 0.15f;
        public float mountainParallax = 0.03f;
        public float forestBackParallax = 0.06f;
        public float forestMidParallax = 0.1f;
        public bool leafParticles;
        public bool fireflyParticles;
        public bool mist;

        [Header("Music")]
        public MusicTrackId music = MusicTrackId.BattleSunny;

        [Header("Battle Setup")]
        [Range(4, 8)] public int activeGuardianSlots = 6;
        public BotDifficulty botDifficulty = BotDifficulty.Normal;
        public string[] botGuardianIds = Array.Empty<string>();
        public int botGuardianLevel = 1;
        public string[] botToolIds = Array.Empty<string>();
        public TreeVisualTier botTreeTier = TreeVisualTier.Sprouting;
        [Tooltip("Bot ağacının geliştirme seviyesi (0..max).")] public int botTreeUpgradeLevel = 0;

        [Header("Rewards")]
        public float rewardMultiplier = 1f;
        public ChestDrop[] chestDrops = Array.Empty<ChestDrop>();

        public string PickChestId(System.Random rng)
        {
            if (chestDrops == null || chestDrops.Length == 0) return null;
            float total = 0f;
            for (int i = 0; i < chestDrops.Length; i++) total += Mathf.Max(0f, chestDrops[i].weight);
            if (total <= 0f) return chestDrops[0].chestId;
            float r = (float)rng.NextDouble() * total;
            for (int i = 0; i < chestDrops.Length; i++)
            {
                r -= Mathf.Max(0f, chestDrops[i].weight);
                if (r <= 0f) return chestDrops[i].chestId;
            }
            return chestDrops[chestDrops.Length - 1].chestId;
        }

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id)) Debug.LogWarning($"[ArenaDefinition] {name} has an empty id.", this);
            arenaIndex = Mathf.Max(0, arenaIndex);
            unlockTrophies = Mathf.Max(0, unlockTrophies);
            rewardMultiplier = Mathf.Max(0.1f, rewardMultiplier);
        }
    }
}
