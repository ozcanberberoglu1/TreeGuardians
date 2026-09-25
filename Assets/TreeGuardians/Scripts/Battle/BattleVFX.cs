using System.Collections.Generic;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Trees;
using UnityEngine;

namespace TreeGuardians.Battle
{
    /// Owns every battle effect: two pooled sprite pools (alpha-blended and additive), the damage text pool and
    /// pre-authored particle systems (hole smoke, confetti). Nothing is instantiated per hit.
    public sealed class BattleVFX : MonoBehaviour
    {
        [Header("Pools")]
        [SerializeField] Transform vfxRoot;
        [SerializeField] Transform textRoot;
        [Tooltip("Alfa karışımlı efekt sprite'ı (kıymık, toz, duman).")] [SerializeField] VfxSprite spritePrefab;
        [Tooltip("Additive efekt sprite'ı (flaş, kıvılcım, halka, parıltı).")] [SerializeField] VfxSprite additivePrefab;
        [SerializeField] DamageText damageTextPrefab;
        [SerializeField] int prewarm = 24;
        [SerializeField] int maxPoolSize = 220;
        [SerializeField] bool showDamageNumbers = true;
        [Tooltip("Kıymıkların sekeceği zemin (ada üstü). -100 = ProjectileService zemini kullanılır.")] [SerializeField] float debrisFloorY = -100f;

        [Header("Sprites")]
        [SerializeField] Sprite[] splinters = new Sprite[0];
        [SerializeField] Sprite[] chips = new Sprite[0];
        [SerializeField] Sprite[] leafBits = new Sprite[0];
        [SerializeField] Sprite puff;
        [SerializeField] Sprite dust;
        [SerializeField] Sprite spark;
        [SerializeField] Sprite ring;
        [SerializeField] Sprite flash;
        [SerializeField] Sprite star;
        [SerializeField] Sprite glow;
        [SerializeField] Sprite heal;

        [Header("Particle systems (pre-authored)")]
        [SerializeField] ParticleSystem holeSmoke;
        [SerializeField] ParticleSystem confetti;
        [SerializeField] int maxSmokeSources = 6;
        [SerializeField] Vector2 smokeRate = new Vector2(5f, 8f);
        [SerializeField] Color smokeColor = new Color(0.34f, 0.31f, 0.29f, 0.7f);

        [Header("Colours")]
        [SerializeField] Color dustColor = new Color(0.8f, 0.7f, 0.56f, 0.8f);
        [SerializeField] Color flashColor = new Color(1f, 0.93f, 0.75f, 0.95f);
        [SerializeField] Color sparkColor = new Color(1f, 0.82f, 0.4f, 1f);

        [Header("Damage numbers")]
        [SerializeField] Color structureNumber = new Color(1f, 0.96f, 0.88f);
        [SerializeField] Color guardianNumber = new Color(1f, 0.45f, 0.38f);
        [SerializeField] Color critNumber = new Color(1f, 0.82f, 0.2f);

        [Header("Castle collapse (battle end)")]
        [Tooltip("Yıkılan kalede art arda çöken bant sayısı.")] [SerializeField] int collapseBands = 3;
        [Tooltip("Çöküş bantları arası süre (saniye).")] [SerializeField] float collapseBandInterval = 0.15f;
        [Tooltip("Kale tabanı boyunca kalkan toz bulutu sayısı.")] [SerializeField] int collapseGroundPuffs = 6;
        [Tooltip("Toz bulutları arası süre (saniye).")] [SerializeField] float collapsePuffInterval = 0.06f;

        ObjectPool<VfxSprite> normal, additive;
        ObjectPool<DamageText> texts;
        readonly List<VfxSprite> active = new List<VfxSprite>(128);
        readonly List<DamageText> activeTexts = new List<DamageText>(32);
        struct SmokeSource { public Vector2 pos; public float until; public float next; }
        readonly List<SmokeSource> smokeSources = new List<SmokeSource>(8);
        ParticleSystem.EmitParams smokeEmit;
        float groundY = -100f;
        float lastTextTime;
        Vector2 lastTextPos;
        int textStack;
        string critLabel = "CRIT!";

        float Budget => QualityApplier.CurrentTier == Data.QualityTier.Low ? 0.5f : QualityApplier.CurrentTier == Data.QualityTier.High ? 1.3f : 1f;
        int N(int count) => Mathf.Max(1, Mathf.RoundToInt(count * Budget));

        public void Initialize(float ground = -100f)
        {
            if (vfxRoot == null) vfxRoot = transform;
            if (textRoot == null) textRoot = transform;
            groundY = debrisFloorY > -99f ? debrisFloorY : ground;
            int warm = Mathf.RoundToInt(prewarm * Budget);
            normal = spritePrefab != null ? new ObjectPool<VfxSprite>(spritePrefab, vfxRoot, warm, maxPoolSize) : null;
            additive = additivePrefab != null ? new ObjectPool<VfxSprite>(additivePrefab, vfxRoot, warm / 2, maxPoolSize / 2) : null;
            if (damageTextPrefab != null) texts = new ObjectPool<DamageText>(damageTextPrefab, textRoot, 8, 48);
            smokeSources.Clear();
            smokeEmit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            var loc = Services.Get<Localization.LocalizationService>();
            critLabel = loc != null ? loc.Get("battle_crit") : critLabel;
        }

        // ------------------------------------------------------------------ core
        void Spawn(bool add, in VfxParams p)
        {
            var pool = add ? additive : normal;
            if (pool == null || !pool.TryGet(out var v)) return;
            v.Play(p);
            active.Add(v);
        }

        static Sprite Pick(Sprite[] arr) => arr != null && arr.Length > 0 ? arr[Random.Range(0, arr.Length)] : null;
        static Vector2 Dir(float degrees) { float r = degrees * Mathf.Deg2Rad; return new Vector2(Mathf.Cos(r), Mathf.Sin(r)); }

        public void Tick(float dt)
        {
            TickSmoke(dt);
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

        // ------------------------------------------------------------------ smoke from holes
        public void AddSmokeSource(Vector2 pos, float seconds)
        {
            if (holeSmoke == null || QualityApplier.ReduceMotion) return;
            if (smokeSources.Count >= maxSmokeSources) smokeSources.RemoveAt(0);
            smokeSources.Add(new SmokeSource { pos = pos, until = Time.time + seconds, next = Time.time });
            if (!holeSmoke.isPlaying) holeSmoke.Play();
        }

        void TickSmoke(float dt)
        {
            if (holeSmoke == null) return;
            for (int i = smokeSources.Count - 1; i >= 0; i--)
            {
                var s = smokeSources[i];
                if (Time.time >= s.until) { smokeSources.RemoveAt(i); continue; }
                if (Time.time < s.next) continue;
                float left = Mathf.Clamp01((s.until - Time.time) / 2f);
                s.next = Time.time + 1f / Mathf.Max(0.5f, Random.Range(smokeRate.x, smokeRate.y) * Budget * (0.35f + 0.65f * left));
                smokeSources[i] = s;
                smokeEmit.position = new Vector3(s.pos.x + Random.Range(-0.18f, 0.18f), s.pos.y + Random.Range(-0.08f, 0.12f), 0f);
                smokeEmit.velocity = new Vector3(Random.Range(-0.35f, 0.15f), Random.Range(0.45f, 0.85f), 0f);
                var c = smokeColor; c.a *= Random.Range(0.7f, 1f) * (0.4f + 0.6f * left);
                smokeEmit.startColor = c;
                smokeEmit.startSize = Random.Range(0.35f, 0.55f);
                smokeEmit.rotation = Random.Range(0f, 360f);
                smokeEmit.startLifetime = Random.Range(1.6f, 2.3f);
                holeSmoke.Emit(smokeEmit, 1);
            }
        }

        // ------------------------------------------------------------------ impacts
        /// Wooden wall hit: flash, shock ring, dust, splinters and chips that bounce on the ground, sparks, a vine leaf or two.
        public void WallImpact(Vector2 point, Vector2 projectileDir, float radius, bool crit)
        {
            Vector2 back = projectileDir.sqrMagnitude > 0.001f ? -projectileDir.normalized : Vector2.up;
            float baseAngle = Mathf.Atan2(back.y, back.x) * Mathf.Rad2Deg;
            float r = Mathf.Max(0.4f, radius);

            var f = VfxParams.At(point, flash, flashColor, 0.25f * r, 1.25f * r * (crit ? 1.4f : 1f), 0.14f);
            Spawn(true, f);
            var rg = VfxParams.At(point, ring, new Color(1f, 0.92f, 0.78f, 0.55f), 0.2f * r, 1.6f * r, 0.28f);
            Spawn(true, rg);

            for (int i = 0, n = N(4); i < n; i++)
            {
                var d = VfxParams.At(point + Random.insideUnitCircle * 0.2f * r, dust, dustColor, 0.35f * r, Random.Range(1.2f, 1.8f) * r, Random.Range(0.7f, 1.0f));
                d.velocity = Dir(baseAngle + Random.Range(-60f, 60f)) * Random.Range(0.6f, 1.6f) + Vector2.up * 0.3f;
                d.drag = 2.5f; d.spin = Random.Range(-40f, 40f); d.rotation = Random.Range(0f, 360f); d.fadeStart = 0.25f;
                d.useEndTint = true; d.endTint = new Color(dustColor.r * 0.9f, dustColor.g * 0.9f, dustColor.b * 0.9f, 0f);
                Spawn(false, d);
            }
            for (int i = 0, n = N(crit ? 10 : 7); i < n; i++)
            {
                var s = VfxParams.At(point, Pick(splinters), Color.white, Random.Range(0.55f, 0.95f), Random.Range(0.45f, 0.8f), Random.Range(1.1f, 1.7f));
                s.velocity = Dir(baseAngle + Random.Range(-70f, 70f)) * Random.Range(3f, 7f) + Vector2.up * Random.Range(1.5f, 3.5f);
                s.gravity = 18f; s.spin = Random.Range(-540f, 540f); s.rotation = Random.Range(0f, 360f);
                s.groundY = groundY + Random.Range(0f, 0.12f); s.restitution = 0.35f; s.maxBounces = 2; s.fadeStart = 0.6f;
                Spawn(false, s);
            }
            for (int i = 0, n = N(5); i < n; i++)
            {
                var c = VfxParams.At(point, Pick(chips), Color.white, Random.Range(0.3f, 0.55f), Random.Range(0.25f, 0.45f), Random.Range(0.9f, 1.4f));
                c.velocity = Dir(baseAngle + Random.Range(-85f, 85f)) * Random.Range(2.5f, 6f) + Vector2.up * 2f;
                c.gravity = 20f; c.spin = Random.Range(-720f, 720f); c.rotation = Random.Range(0f, 360f);
                c.groundY = groundY; c.restitution = 0.3f; c.maxBounces = 1; c.fadeStart = 0.65f;
                Spawn(false, c);
            }
            for (int i = 0, n = N(crit ? 7 : 4); i < n; i++)
            {
                var k = VfxParams.At(point, spark, sparkColor, Random.Range(0.18f, 0.3f), 0.05f, Random.Range(0.18f, 0.32f));
                k.velocity = Dir(baseAngle + Random.Range(-75f, 75f)) * Random.Range(6f, 11f);
                k.gravity = 8f; k.drag = 2f; k.alignToVelocity = true; k.aspect = new Vector2(2.4f, 0.6f);
                Spawn(true, k);
            }
            if (leafBits != null && leafBits.Length > 0 && Random.value < 0.8f)
            {
                for (int i = 0, n = Random.Range(1, 3); i < n; i++)
                {
                    var l = VfxParams.At(point, Pick(leafBits), Color.white, 0.22f, 0.2f, Random.Range(1.6f, 2.4f));
                    l.velocity = Dir(baseAngle + Random.Range(-50f, 50f)) * Random.Range(1.5f, 3f) + Vector2.up * 1.5f;
                    l.gravity = 2.2f; l.drag = 1.2f; l.spin = Random.Range(-200f, 200f); l.rotation = Random.Range(0f, 360f); l.fadeStart = 0.7f;
                    Spawn(false, l);
                }
            }
        }

        /// One band of a collapsing wall part: chunks tumbling out, a plank and dust rolling at the bottom.
        public void CollapseBand(Rect band, float castleCenterX, bool first)
        {
            float side = Mathf.Sign(band.center.x - castleCenterX);
            if (side == 0f) side = Random.value < 0.5f ? -1f : 1f;
            for (int i = 0, n = N(5); i < n; i++)
            {
                var p0 = new Vector2(Random.Range(band.xMin, band.xMax), Random.Range(band.yMin, band.yMax));
                var s = VfxParams.At(p0, Random.value < 0.6f ? Pick(splinters) : Pick(chips), Color.white, Random.Range(0.7f, 1.2f), Random.Range(0.6f, 1f), Random.Range(1.6f, 2.3f));
                s.velocity = new Vector2(side * Random.Range(1.5f, 4.5f), Random.Range(1.5f, 4f));
                s.gravity = 18f; s.spin = Random.Range(-420f, 420f); s.rotation = Random.Range(0f, 360f);
                s.groundY = groundY + Random.Range(0f, 0.15f); s.restitution = 0.3f; s.maxBounces = 2; s.fadeStart = 0.7f;
                Spawn(false, s);
            }
            for (int i = 0, n = N(3); i < n; i++)
            {
                var d = VfxParams.At(new Vector2(Random.Range(band.xMin, band.xMax), band.yMin), dust, dustColor, 0.9f, Random.Range(2.4f, 3.2f), Random.Range(1.2f, 1.7f));
                d.velocity = new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(0.3f, 0.8f)); d.drag = 1.5f; d.rotation = Random.Range(0f, 360f); d.spin = Random.Range(-30f, 30f); d.fadeStart = 0.3f;
                Spawn(false, d);
            }
            if (first)
            {
                var rg = VfxParams.At(band.center, ring, new Color(1f, 0.9f, 0.75f, 0.5f), 0.5f, 4f, 0.4f);
                Spawn(true, rg);
            }
        }

        /// Battle end: the losing castle crumbles. Collapse bands at random heights inside its walls (staggered),
        /// then dust rolling along its base.
        public void CastleCollapse(TreeController tree)
        {
            if (tree == null || !CastleRects(tree, out var walls, out var whole)) return;
            float cx = tree.transform.position.x;
            float bandH = Mathf.Min(walls.height * 0.28f, 1.3f);
            for (int i = 0, n = Mathf.Max(1, collapseBands); i < n; i++)
            {
                float w = walls.width * Random.Range(0.45f, 0.7f);
                float x = Random.Range(walls.xMin, Mathf.Max(walls.xMin, walls.xMax - w));
                float y = Random.Range(walls.yMin + walls.height * 0.1f, Mathf.Max(walls.yMin + walls.height * 0.1f, walls.yMax - bandH));
                var band = new Rect(x, y, w, bandH);
                bool first = i == 0;
                if (first) CollapseBand(band, cx, true);
                else TGTween.Delay(collapseBandInterval * i, () => { if (this != null && normal != null) CollapseBand(band, cx, first); });
            }
            float baseY = tree.transform.position.y + 0.05f;
            for (int i = 0, n = Mathf.Max(0, collapseGroundPuffs); i < n; i++)
            {
                float t = n > 1 ? i / (float)(n - 1) : 0.5f;
                var pos = new Vector2(Mathf.Lerp(whole.xMin + 0.3f, whole.xMax - 0.3f, t) + Random.Range(-0.15f, 0.15f), baseY);
                float delay = 0.1f + collapsePuffInterval * i;
                TGTween.Delay(delay, () => { if (this != null && normal != null) GroundPuff(pos); });
            }
        }

        /// World rects of the castle: carvable walls only, and every part including the base.
        static bool CastleRects(TreeController tree, out Rect walls, out Rect whole)
        {
            walls = default; whole = default;
            bool anyWall = false, any = false;
            var sections = tree.Sections;
            for (int i = 0; i < sections.Count; i++)
            {
                var s = sections[i];
                if (s == null || !s.gameObject.activeSelf) continue;
                var b = s.WorldBounds;
                var r = Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y);
                whole = any ? Union(whole, r) : r;
                any = true;
                if (s.Type == TreeSectionType.RootStabilizer || s.Type == TreeSectionType.HeartwoodCore) continue;
                walls = anyWall ? Union(walls, r) : r;
                anyWall = true;
            }
            if (!anyWall) walls = whole;
            return any;
        }

        static Rect Union(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

        /// Bomb / splash: flash, shock ring, fiery puffs turning into smoke; poison variant rises as spores.
        public void Explosion(Vector2 point, float radius, Color tint, bool poison)
        {
            float r = Mathf.Max(0.6f, radius);
            Spawn(true, VfxParams.At(point, flash, poison ? new Color(0.9f, 0.6f, 1f, 0.9f) : flashColor, 0.3f * r, 1.4f * r, 0.16f));
            Spawn(true, VfxParams.At(point, ring, poison ? new Color(0.85f, 0.5f, 0.9f, 0.7f) : new Color(1f, 0.88f, 0.65f, 0.75f), 0.2f * r, 2.2f * r, 0.32f));
            // Warm, light puffs that fade fast: a bright pop, not a lingering dark smudge over the castle.
            var hot = poison ? new Color(0.85f, 0.5f, 0.85f, 0.7f) : Color.Lerp(new Color(1f, 0.86f, 0.62f, 0.72f), tint, 0.2f);
            var cold = poison ? new Color(0.95f, 0.75f, 0.95f, 0f) : new Color(0.78f, 0.74f, 0.7f, 0f);
            float puffScale = Mathf.Min(r, 1.6f);
            for (int i = 0, n = N(8); i < n; i++)
            {
                var d = VfxParams.At(point + Random.insideUnitCircle * 0.25f * puffScale, puff, hot, 0.3f * puffScale, Random.Range(0.8f, 1.15f) * puffScale, Random.Range(0.5f, 0.75f));
                d.velocity = Dir(Random.Range(0f, 360f)) * Random.Range(2f, 4f) * puffScale * 0.5f + Vector2.up * 0.5f;
                d.drag = 3.5f; d.rotation = Random.Range(0f, 360f); d.spin = Random.Range(-60f, 60f); d.useEndTint = true; d.endTint = cold; d.fadeStart = 0.2f;
                Spawn(false, d);
            }
            if (poison)
            {
                for (int i = 0, n = N(12); i < n; i++)
                {
                    var g = VfxParams.At(point + Random.insideUnitCircle * r * 0.6f, glow, new Color(0.8f, 1f, 0.5f, 0.9f), 0.12f, 0.2f, Random.Range(1.8f, 2.8f));
                    g.velocity = new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(0.3f, 0.6f)); g.fadeStart = 0.5f;
                    Spawn(true, g);
                }
            }
            else
            {
                for (int i = 0, n = N(8); i < n; i++)
                {
                    var k = VfxParams.At(point, spark, sparkColor, 0.25f, 0.05f, Random.Range(0.25f, 0.4f));
                    k.velocity = Dir(Random.Range(0f, 360f)) * Random.Range(6f, 12f); k.gravity = 10f; k.drag = 2f; k.alignToVelocity = true; k.aspect = new Vector2(2.4f, 0.6f);
                    Spawn(true, k);
                }
            }
        }

        /// Launch puff at the shooter, plus a charge ring for specials.
        public void Muzzle(Vector2 pos, Vector2 dir, Color tint, bool special)
        {
            var d = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector2.right;
            var perp = new Vector2(-d.y, d.x);
            for (int i = 0, n = N(3); i < n; i++)
            {
                var p = VfxParams.At(pos, puff, new Color(1f, 1f, 1f, 0.75f), 0.18f, Random.Range(0.5f, 0.7f), 0.32f);
                p.velocity = d * Random.Range(1.5f, 2.6f) + perp * Random.Range(-0.4f, 0.4f); p.drag = 6f; p.rotation = Random.Range(0f, 360f); p.fadeStart = 0.2f;
                Spawn(false, p);
            }
            Spawn(true, VfxParams.At(pos, glow, new Color(tint.r, tint.g, tint.b, 0.8f), 0.3f, 0.9f, 0.1f));
            if (special)
            {
                Spawn(true, VfxParams.At(pos, ring, new Color(tint.r, tint.g, tint.b, 0.9f), 0.2f, 1.4f, 0.28f));
                for (int i = 0, n = N(6); i < n; i++)
                {
                    var s = VfxParams.At(pos + Random.insideUnitCircle * 0.4f, star, new Color(1f, 0.95f, 0.6f, 1f), 0.25f, 0.05f, Random.Range(0.4f, 0.6f));
                    s.velocity = Vector2.up * Random.Range(0.8f, 1.6f); s.spin = 180f;
                    Spawn(true, s);
                }
            }
        }

        /// Guardian knocked out: radial puffs, a flash and a few stars.
        public void GuardianPoof(Vector2 pos, Color tint)
        {
            Spawn(true, VfxParams.At(pos, flash, new Color(1f, 1f, 1f, 0.8f), 0.3f, 1.3f, 0.15f));
            for (int i = 0, n = N(9); i < n; i++)
            {
                var p = VfxParams.At(pos, puff, new Color(0.95f, 0.95f, 0.95f, 0.85f), 0.25f, Random.Range(0.7f, 1f), Random.Range(0.5f, 0.75f));
                p.velocity = Dir(i * 40f + Random.Range(-10f, 10f)) * Random.Range(1.6f, 2.6f); p.drag = 4f; p.rotation = Random.Range(0f, 360f); p.fadeStart = 0.3f;
                Spawn(false, p);
            }
            for (int i = 0, n = N(5); i < n; i++)
            {
                var s = VfxParams.At(pos, star, new Color(1f, 0.9f, 0.4f, 1f), 0.3f, 0.1f, Random.Range(0.5f, 0.8f));
                s.velocity = Dir(Random.Range(40f, 140f)) * Random.Range(2f, 4f); s.gravity = 6f; s.spin = Random.Range(-360f, 360f);
                Spawn(true, s);
            }
        }

        /// Projectile landing in the grass: dust and a few clods.
        public void GroundPuff(Vector2 pos)
        {
            for (int i = 0, n = N(3); i < n; i++)
            {
                var d = VfxParams.At(pos + new Vector2(Random.Range(-0.2f, 0.2f), 0f), dust, new Color(0.62f, 0.56f, 0.4f, 0.75f), 0.3f, Random.Range(1f, 1.4f), Random.Range(0.6f, 0.9f));
                d.velocity = new Vector2(Random.Range(-1f, 1f), Random.Range(0.4f, 1f)); d.drag = 2f; d.rotation = Random.Range(0f, 360f); d.fadeStart = 0.3f;
                Spawn(false, d);
            }
            for (int i = 0, n = N(3); i < n; i++)
            {
                var c = VfxParams.At(pos, Pick(chips), new Color(0.45f, 0.36f, 0.24f, 1f), 0.25f, 0.2f, 0.9f);
                c.velocity = Dir(Random.Range(50f, 130f)) * Random.Range(2f, 4f); c.gravity = 18f; c.spin = Random.Range(-500f, 500f);
                c.groundY = pos.y; c.restitution = 0.25f; c.maxBounces = 1; c.fadeStart = 0.6f;
                Spawn(false, c);
            }
        }

        public void Burst(Vector2 pos, Color tint, float scale = 1f)
        {
            var p = VfxParams.At(pos, star, tint, 0.2f * scale, 1.6f * scale, 0.35f); p.spin = 90f;
            Spawn(true, p);
        }

        public void Hit(Vector2 pos, Color tint) => Spawn(true, VfxParams.At(pos, flash, new Color(tint.r, tint.g, tint.b, 0.9f), 0.15f, 0.8f, 0.16f));

        public void Heal(Vector2 pos)
        {
            for (int i = 0, n = N(5); i < n; i++)
            {
                var h = VfxParams.At(pos + new Vector2(Random.Range(-0.35f, 0.35f), Random.Range(0f, 0.4f)), heal, new Color(0.6f, 1f, 0.6f, 1f), 0.25f, 0.35f, Random.Range(0.6f, 0.9f));
                h.velocity = Vector2.up * Random.Range(0.8f, 1.3f); h.fadeStart = 0.4f;
                Spawn(true, h);
            }
        }

        public void Poison(Vector2 pos, float radius) => Explosion(pos, radius * 0.6f, Color.magenta, true);

        /// Legacy debris call (non-castle sections).
        public void Shards(Vector2 pos, int count, Color tint)
        {
            for (int i = 0, n = N(count); i < n; i++)
            {
                var s = VfxParams.At(pos, Pick(chips), tint, Random.Range(0.35f, 0.6f), 0.3f, Random.Range(0.8f, 1.2f));
                s.velocity = Dir(Random.Range(20f, 160f)) * Random.Range(2f, 5f); s.gravity = 14f; s.spin = Random.Range(-360f, 360f);
                s.groundY = groundY; s.restitution = 0.3f; s.maxBounces = 1; s.fadeStart = 0.6f;
                Spawn(false, s);
            }
        }

        public void PlayConfetti()
        {
            if (confetti == null || QualityApplier.ReduceMotion) return;
            confetti.Play(true);
        }

        // ------------------------------------------------------------------ numbers
        public enum NumberStyle { Structure, Guardian, Crit, Heal }

        public void DamageNumber(Vector2 pos, float amount, Color tint, bool crit) =>
            Number(pos, amount, crit ? NumberStyle.Crit : (tint.r > 0.95f && tint.g < 0.7f ? NumberStyle.Guardian : NumberStyle.Structure));

        public void Number(Vector2 pos, float amount, NumberStyle style)
        {
            if (!showDamageNumbers || texts == null || amount < 0.5f) return;
            if (Time.time - lastTextTime < 0.2f && (pos - lastTextPos).sqrMagnitude < 0.5f) textStack++; else textStack = 0;
            lastTextTime = Time.time;
            lastTextPos = pos;
            if (!texts.TryGet(out var t)) return;
            var color = style == NumberStyle.Crit ? critNumber : style == NumberStyle.Guardian ? guardianNumber : style == NumberStyle.Heal ? new Color(0.55f, 1f, 0.5f) : structureNumber;
            float size = style == NumberStyle.Crit ? 8.5f : 6f;
            var at = pos + new Vector2((textStack % 2 == 0 ? 0.3f : -0.3f), 0.35f + textStack * 0.4f);
            t.Show(at, Mathf.RoundToInt(amount), style == NumberStyle.Heal ? "+" : "-", color, size, style == NumberStyle.Crit ? 1f : 0.85f);
            activeTexts.Add(t);
            if (style == NumberStyle.Crit && texts.TryGet(out var label))
            {
                label.ShowLabel(at + new Vector2(0f, 0.75f), critLabel, critNumber, 6.5f, 1f);
                activeTexts.Add(label);
                Spawn(true, VfxParams.At(at, star, new Color(1f, 0.85f, 0.3f, 0.9f), 0.4f, 1.6f, 0.25f));
            }
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
