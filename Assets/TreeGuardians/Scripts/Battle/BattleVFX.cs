using System.Collections.Generic;
using TreeGuardians.Core;
using UnityEngine;

namespace TreeGuardians.Battle
{
    /// Owns the effect pools (bursts, hits, heals, poison clouds, debris, damage numbers). Nothing is instantiated per hit.
    public sealed class BattleVFX : MonoBehaviour
    {
        [SerializeField] Transform vfxRoot;
        [SerializeField] Transform textRoot;
        [SerializeField] VfxSprite burstPrefab;
        [SerializeField] VfxSprite hitPrefab;
        [SerializeField] VfxSprite healPrefab;
        [SerializeField] VfxSprite poisonPrefab;
        [SerializeField] VfxSprite shardPrefab;
        [SerializeField] DamageText damageTextPrefab;
        [SerializeField] int prewarm = 8;
        [SerializeField] bool showDamageNumbers = true;

        ObjectPool<VfxSprite> bursts, hitsPool, heals, poisons, shards;
        ObjectPool<DamageText> texts;
        readonly List<VfxSprite> active = new List<VfxSprite>(64);
        readonly List<DamageText> activeTexts = new List<DamageText>(32);
        float lastTextTime;

        public void Initialize()
        {
            if (vfxRoot == null) vfxRoot = transform;
            if (textRoot == null) textRoot = transform;
            int budget = Mathf.Max(2, prewarm * Mathf.Max(1, QualityApplier.ParticleBudgetMultiplier + 1) / 2);
            bursts = Make(burstPrefab, budget);
            hitsPool = Make(hitPrefab, budget * 2);
            heals = Make(healPrefab, budget);
            poisons = Make(poisonPrefab, 2);
            shards = Make(shardPrefab, budget * 3);
            if (damageTextPrefab != null) texts = new ObjectPool<DamageText>(damageTextPrefab, textRoot, budget * 2, 64);
        }

        ObjectPool<VfxSprite> Make(VfxSprite prefab, int count) => prefab != null ? new ObjectPool<VfxSprite>(prefab, vfxRoot, count, 96) : null;

        public void Tick(float dt)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var v = active[i];
                if (v == null || !v.Step(dt)) { active.RemoveAt(i); v?.ReleaseToPool(); }
            }
            for (int i = activeTexts.Count - 1; i >= 0; i--)
            {
                var t = activeTexts[i];
                if (t == null || !t.Step(dt)) { activeTexts.RemoveAt(i); t?.ReleaseToPool(); }
            }
        }

        void Spawn(ObjectPool<VfxSprite> pool, Vector2 pos, Color tint, float from, float to, float dur, Vector2 vel, float grav, float spin)
        {
            if (pool == null) return;
            var v = pool.Get();
            v.Play(pos, tint, from, to, dur, vel, grav, spin);
            active.Add(v);
        }

        public void Burst(Vector2 pos, Color tint, float scale = 1f) => Spawn(bursts, pos, tint, 0.2f * scale, 1.6f * scale, 0.35f, Vector2.zero, 0f, 90f);
        public void Hit(Vector2 pos, Color tint) => Spawn(hitsPool, pos, tint, 0.15f, 0.7f, 0.22f, Vector2.zero, 0f, 0f);
        public void Heal(Vector2 pos) => Spawn(heals, pos + Vector2.up * 0.3f, new Color(0.6f, 1f, 0.6f), 0.3f, 1.1f, 0.5f, Vector2.up * 0.8f, 0f, 0f);
        public void Poison(Vector2 pos, float radius) => Spawn(poisons, pos, new Color(0.85f, 0.45f, 0.8f, 0.6f), radius * 0.6f, radius * 1.3f, 2.5f, Vector2.zero, 0f, 20f);

        public void Shards(Vector2 pos, int count, Color tint)
        {
            if (shards == null) return;
            int n = Mathf.Min(count, 4 + QualityApplier.ParticleBudgetMultiplier * 6);
            for (int i = 0; i < n; i++)
            {
                float ang = Random.Range(20f, 160f) * Mathf.Deg2Rad;
                float spd = Random.Range(2f, 5f);
                Spawn(shards, pos, tint, Random.Range(0.35f, 0.7f), 0.1f, Random.Range(0.6f, 1.1f), new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spd, 14f, Random.Range(-360f, 360f));
            }
        }

        public void DamageNumber(Vector2 pos, float amount, Color tint, bool crit)
        {
            if (!showDamageNumbers || texts == null || amount < 0.5f) return;
            if (Time.time - lastTextTime < 0.02f) return;
            lastTextTime = Time.time;
            var t = texts.Get();
            t.Show(pos + new Vector2(Random.Range(-0.2f, 0.2f), 0.3f), Mathf.RoundToInt(amount).ToString(), tint, crit ? 5f : 3.6f, crit ? 0.9f : 0.7f);
            activeTexts.Add(t);
        }

        public void ReleaseAll()
        {
            for (int i = 0; i < active.Count; i++) active[i]?.ReleaseToPool();
            active.Clear();
            for (int i = 0; i < activeTexts.Count; i++) activeTexts[i]?.ReleaseToPool();
            activeTexts.Clear();
        }
    }
}
