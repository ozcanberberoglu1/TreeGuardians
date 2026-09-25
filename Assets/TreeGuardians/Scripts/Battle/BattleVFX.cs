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
        [SerializeField] VfxSprite smokePrefab;
        [SerializeField] int maxSmokeSources = 10;
        [SerializeField] Color smokeColor = new Color(0.2f, 0.18f, 0.17f, 0.85f);
        [Tooltip("Kaynak başına puf aralığı (saniye).")] [SerializeField] Vector2 smokePuffInterval = new Vector2(0.05f, 0.1f);
        [Tooltip("Puf ölçeği (başlangıç, bitiş) dünya birimi cinsinden sprite çarpanı.")] [SerializeField] Vector2 smokePuffScale = new Vector2(0.6f, 2.2f);
        [SerializeField] int smokePuffsPerTick = 2;
        [SerializeField] DamageText damageTextPrefab;
        [SerializeField] int prewarm = 8;
        [SerializeField] bool showDamageNumbers = true;

        ObjectPool<VfxSprite> bursts, hitsPool, heals, poisons, shards, smokes;
        struct SmokeSource { public Vector2 pos; public float until; public float next; }
        readonly List<SmokeSource> smokeSources = new List<SmokeSource>(8);
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
            smokes = Make(smokePrefab, budget * 5);
            smokeSources.Clear();
            if (damageTextPrefab != null) texts = new ObjectPool<DamageText>(damageTextPrefab, textRoot, budget * 2, 64);
        }

        ObjectPool<VfxSprite> Make(VfxSprite prefab, int count) => prefab != null ? new ObjectPool<VfxSprite>(prefab, vfxRoot, count, 96) : null;

        /// A hole that keeps smoking for a while (puffs rise from the point).
        public void AddSmokeSource(Vector2 pos, float seconds)
        {
            if (smokes == null || QualityApplier.ReduceMotion) return;
            if (smokeSources.Count >= maxSmokeSources) smokeSources.RemoveAt(0);
            smokeSources.Add(new SmokeSource { pos = pos, until = Time.time + seconds, next = Time.time });
        }

        void TickSmoke()
        {
            for (int i = smokeSources.Count - 1; i >= 0; i--)
            {
                var s = smokeSources[i];
                if (Time.time >= s.until) { smokeSources.RemoveAt(i); continue; }
                if (Time.time < s.next) continue;
                s.next = Time.time + Random.Range(smokePuffInterval.x, smokePuffInterval.y);
                smokeSources[i] = s;
                for (int k = 0; k < smokePuffsPerTick; k++)
                {
                    var pos = s.pos + new Vector2(Random.Range(-0.25f, 0.25f), Random.Range(-0.1f, 0.15f));
                    var vel = new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.55f, 1f));
                    var tint = smokeColor; tint.a *= Random.Range(0.75f, 1f);
                    Spawn(smokes, pos, tint, smokePuffScale.x * Random.Range(0.8f, 1.2f), smokePuffScale.y * Random.Range(0.8f, 1.2f), Random.Range(2f, 3f), vel, -0.2f, Random.Range(-40f, 40f));
                }
            }
        }

        public void Tick(float dt)
        {
            TickSmoke();
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
            smokeSources.Clear();
            for (int i = 0; i < active.Count; i++) active[i]?.ReleaseToPool();
            active.Clear();
            for (int i = 0; i < activeTexts.Count; i++) activeTexts[i]?.ReleaseToPool();
            activeTexts.Clear();
        }
    }
}
