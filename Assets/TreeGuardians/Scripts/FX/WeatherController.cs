using TreeGuardians.Audio;
using TreeGuardians.Core;
using TreeGuardians.Data;
using UnityEngine;

namespace TreeGuardians.FX
{
    /// Scene weather for the menu and the battle: occasional rain showers (streaks + ground splashes + darkening + rain
    /// ambience + rare lightning) and wind gusts that scatter leaves. All particle systems and the overlay are
    /// pre-authored children; this component only drives rates, colors and audio.
    /// Battle arenas call Configure (rain chance, lightning) and SetLeafTint; quality tier and Reduce Motion are
    /// re-applied whenever the settings change.
    public sealed class WeatherController : MonoBehaviour
    {
        [Header("Pre-authored parts")]
        [SerializeField] ParticleSystem rain;
        [SerializeField] ParticleSystem splashes;
        [Tooltip("Ek sıçrama yüzeyleri (ör. her adanın üstü). Oran ana sıçramalarla aynı.")] [SerializeField] ParticleSystem[] extraSplashes = new ParticleSystem[0];
        [SerializeField] ParticleSystem leaves;
        [SerializeField] SpriteRenderer overlay;
        [Tooltip("Rüzgâr yönünü yapraklara/yağmura uygulamak için takip edilen kamera (overlay onu takip eder).")] [SerializeField] Camera followCamera;

        [Header("Rain schedule (seconds)")]
        [SerializeField] Vector2 clearDuration = new Vector2(35f, 80f);
        [SerializeField] Vector2 rainDuration = new Vector2(20f, 40f);
        [SerializeField] float firstRainDelayMin = 12f;
        [Tooltip("Sahne açılır açılmaz yağmur ihtimali (0-1).")] [SerializeField, Range(0f, 1f)] float startRainingChance = 0.2f;
        [Tooltip("Açık hava bittiğinde sağanak gelme ihtimali (0-1). Arenalarda Configure ile ayarlanır.")] [SerializeField, Range(0f, 1f)] float showerChance = 1f;
        [SerializeField] float rampSeconds = 3.5f;
        [SerializeField] float maxRainRate = 260f;
        [SerializeField] float maxSplashRate = 45f;
        [SerializeField] Color overlayColor = new Color(0.1f, 0.14f, 0.22f, 1f);
        [SerializeField, Range(0f, 0.6f)] float overlayMaxAlpha = 0.24f;

        [Header("Lightning")]
        [SerializeField, Range(0f, 1f)] float lightningChancePerShower = 0.55f;
        [Tooltip("Configure(…, lightning: true) çağrıldığında sağanak başına şimşek ihtimali.")] [SerializeField, Range(0f, 1f)] float configuredLightningChance = 0.7f;
        [SerializeField] Vector2 lightningInterval = new Vector2(9f, 20f);
        [SerializeField] Color flashColor = new Color(0.92f, 0.95f, 1f, 1f);
        [Tooltip("Gök gürültüsü ses seviyesi aralığı.")] [SerializeField] Vector2 thunderVolume = new Vector2(0.6f, 1f);
        [Tooltip("Gök gürültüsünde müzik/ambiyans bu seviyeye iner.")] [SerializeField, Range(0f, 1f)] float thunderDuckTo = 0.6f;
        [SerializeField] float thunderDuckSeconds = 1.5f;

        [Header("Wind & leaves")]
        [SerializeField] Vector2 gustInterval = new Vector2(7f, 16f);
        [SerializeField] float gustSeconds = 2.6f;
        [SerializeField] float baseLeafRate = 1.2f;
        [SerializeField] float gustLeafBurst = 9f;
        [SerializeField] float baseWind = 0.6f;
        [SerializeField] float gustWind = 3.4f;
        [SerializeField] bool playAmbience = true;
        [Tooltip("Rüzgâr esintisinde ses çal (menüde kapalı tutulabilir).")] [SerializeField] bool gustSound = true;
        [Tooltip("Yağmur sırasında orman ambiyansı: 1 - bu değer × yağmur yoğunluğu.")] [SerializeField, Range(0f, 1f)] float forestDuckWhileRaining = 0.6f;

        [Header("Scene tint while raining")]
        [Tooltip("Yağmur yoğunluğuyla kararan sahne sprite'ları (ör. savaş arka planı).")] [SerializeField] SpriteRenderer[] tintTargets = new SpriteRenderer[0];
        [SerializeField] Color rainTint = new Color(0.72f, 0.78f, 0.88f, 1f);

        [Header("Quality")]
        [Tooltip("Düşük kalite: yağmur/sıçrama/şimşek yok, karartma bu oranda.")] [SerializeField, Range(0f, 1f)] float lowOverlayScale = 0.5f;
        [SerializeField, Range(0f, 1f)] float mediumRainScale = 0.55f;
        [SerializeField, Range(0f, 1f)] float mediumSplashScale = 0.5f;
        [SerializeField] int mediumMaxRainParticles = 160;
        [SerializeField] int highMaxRainParticles = 300;
        [Tooltip("Hareketi Azalt açıkken yağmur ve sıçrama oranı çarpanı.")] [SerializeField, Range(0f, 1f)] float reduceMotionRainScale = 0.4f;

        public float RainIntensity { get; private set; }
        public float Wind { get; private set; }
        public bool IsRaining => targetRain > 0f;

        float targetRain;
        float stateTimer;
        float gustTimer;
        float gustTime = -1f;
        float lightningTimer;
        bool lightningThisShower;
        float flash;
        float thunderAt = -1f;
        bool ambienceDirty;
        Color[] tintBase;
        float appliedTint = -1f;
        System.Random rng;

        void Awake()
        {
            EnsureRng();
            if (followCamera == null) followCamera = Camera.main;
            CaptureTintBases();
            ApplyQuality();
            SetRates(0f, 0f);
            if (overlay != null) overlay.color = new Color(overlayColor.r, overlayColor.g, overlayColor.b, 0f);
        }

        void OnEnable()
        {
            EnsureRng();
            bool rainy = Range(0f, 1f) < startRainingChance;
            if (rainy) { StartShower(); RainIntensity = targetRain; }
            else { targetRain = 0f; stateTimer = Mathf.Max(firstRainDelayMin, Range(clearDuration.x, clearDuration.y)); }
            gustTimer = Range(2f, gustInterval.y);
            if (leaves != null && !leaves.isPlaying) leaves.Play();
            GameEventBus.Subscribe<SettingsChangedEvent>(OnSettingsChanged);
            if (playAmbience)
                BootAwaiter.WhenReady(() =>
                {
                    if (this == null || !isActiveAndEnabled) return;
                    Services.Get<AudioService>()?.SetAmbience(0, AmbienceTrackId.Forest, ForestLevel, 2f);
                    ambienceDirty = true;
                });
        }

        void OnDisable()
        {
            GameEventBus.Unsubscribe<SettingsChangedEvent>(OnSettingsChanged);
            if (playAmbience) Services.Get<AudioService>()?.SetAmbience(1, AmbienceTrackId.None, 0f, 1f);
        }

        void OnSettingsChanged(SettingsChangedEvent e) => ApplyQuality();

        void EnsureRng()
        {
            if (rng == null) rng = new System.Random(GetInstanceID() ^ System.Environment.TickCount);
        }

        float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        float ForestLevel => 1f - forestDuckWhileRaining * RainIntensity;

        /// Arena weather: chance of rain at the start and after every clear spell, and whether showers bring lightning.
        /// Re-rolls the opening weather and re-captures the tint targets' colours (call after the arena colours are set).
        public void Configure(float rainChance, bool lightning)
        {
            EnsureRng();
            rainChance = Mathf.Clamp01(rainChance);
            startRainingChance = rainChance;
            showerChance = rainChance;
            lightningChancePerShower = lightning ? configuredLightningChance : 0f;
            if (Range(0f, 1f) < rainChance) { StartShower(); RainIntensity = targetRain; }
            else
            {
                targetRain = 0f;
                RainIntensity = 0f;
                lightningThisShower = false;
                stateTimer = Mathf.Max(firstRainDelayMin, Range(clearDuration.x, clearDuration.y));
                if (rain != null) rain.Clear();
                if (splashes != null) splashes.Clear();
                foreach (var x in extraSplashes) if (x != null) x.Clear();
            }
            ambienceDirty = true;
            CaptureTintBases();
        }

        /// Colour range of the wind-blown leaves (white/white = the original leaf art).
        public void SetLeafTint(Color a, Color b)
        {
            if (leaves == null) return;
            var main = leaves.main;
            main.startColor = a == b ? new ParticleSystem.MinMaxGradient(a) : new ParticleSystem.MinMaxGradient(a, b);
        }

        /// Stores the current colours of the tint targets as their dry-weather colours.
        public void CaptureTintBases()
        {
            int n = tintTargets != null ? tintTargets.Length : 0;
            if (tintBase == null || tintBase.Length != n) tintBase = new Color[n];
            for (int i = 0; i < n; i++) tintBase[i] = tintTargets[i] != null ? tintTargets[i].color : Color.white;
            appliedTint = -1f;
        }

        /// Particle caps per quality tier (rates are scaled every frame in SetRates).
        void ApplyQuality()
        {
            if (rain != null)
            {
                var m = rain.main;
                m.maxParticles = QualityApplier.CurrentTier == QualityTier.High ? highMaxRainParticles : mediumMaxRainParticles;
            }
            if (QualityApplier.CurrentTier == QualityTier.Low) { lightningThisShower = false; thunderAt = -1f; }
            if (QualityApplier.ReduceMotion || QualityApplier.CurrentTier == QualityTier.Low) flash = 0f;
        }

        void StartShower()
        {
            targetRain = Range(0.65f, 1f);
            stateTimer = Range(rainDuration.x, rainDuration.y);
            lightningThisShower = QualityApplier.CurrentTier != QualityTier.Low && Range(0f, 1f) < lightningChancePerShower;
            lightningTimer = Range(4f, lightningInterval.y);
            if (rain != null && !rain.isPlaying) rain.Play();
            if (splashes != null && !splashes.isPlaying) splashes.Play();
            foreach (var x in extraSplashes) if (x != null && !x.isPlaying) x.Play();
        }

        /// Forces weather (debug / tutorial / arenas that are always rainy).
        public void SetRaining(bool on)
        {
            if (on) StartShower();
            else { targetRain = 0f; stateTimer = Range(clearDuration.x, clearDuration.y); }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            stateTimer -= dt;
            if (stateTimer <= 0f)
            {
                if (targetRain > 0f) { targetRain = 0f; stateTimer = Range(clearDuration.x, clearDuration.y); }
                else if (Range(0f, 1f) < showerChance) StartShower();
                else stateTimer = Range(clearDuration.x, clearDuration.y);
            }
            float previous = RainIntensity;
            RainIntensity = Mathf.MoveTowards(RainIntensity, targetRain, dt / Mathf.Max(0.1f, rampSeconds));

            // wind: slow base sway + occasional gust
            gustTimer -= dt;
            if (gustTimer <= 0f)
            {
                gustTimer = Range(gustInterval.x, gustInterval.y);
                gustTime = 0f;
                if (leaves != null && !QualityApplier.ReduceMotion) leaves.Emit(Mathf.RoundToInt(gustLeafBurst * (1 + QualityApplier.ParticleBudgetMultiplier) * 0.5f));
                if (gustSound) Services.Get<AudioService>()?.PlaySfx(AudioEventId.WindGust, 0.7f + 0.3f * RainIntensity);
            }
            float gust = 0f;
            if (gustTime >= 0f)
            {
                gustTime += dt;
                float t = gustTime / gustSeconds;
                gust = t < 1f ? Mathf.Sin(t * Mathf.PI) : 0f;
                if (t >= 1f) gustTime = -1f;
            }
            Wind = baseWind + Mathf.Sin(Time.time * 0.35f) * 0.25f + gust * gustWind + RainIntensity * 0.8f;

            // lightning: Reduce Motion keeps the thunder but drops the flash; Low quality has neither.
            if (lightningThisShower && RainIntensity > 0.6f && QualityApplier.CurrentTier != QualityTier.Low)
            {
                lightningTimer -= dt;
                if (lightningTimer <= 0f)
                {
                    lightningTimer = Range(lightningInterval.x, lightningInterval.y);
                    if (!QualityApplier.ReduceMotion) flash = 1f;
                    thunderAt = Time.time + Range(0.25f, 1.1f);
                }
            }
            if (thunderAt > 0f && Time.time >= thunderAt)
            {
                thunderAt = -1f;
                var audio = Services.Get<AudioService>();
                if (audio != null)
                {
                    audio.Duck(thunderDuckTo, thunderDuckSeconds);
                    audio.PlaySfx(AudioEventId.Thunder, Range(thunderVolume.x, thunderVolume.y));
                }
            }
            flash = Mathf.MoveTowards(flash, 0f, dt * 3.2f);

            SetRates(RainIntensity, Wind);
            ApplyTint();
            if (overlay != null)
            {
                float a = overlayMaxAlpha * RainIntensity * (QualityApplier.CurrentTier == QualityTier.Low ? lowOverlayScale : 1f);
                float f = QualityApplier.ReduceMotion ? 0f : flash;
                var c = Color.Lerp(overlayColor, flashColor, f > 0.5f ? 1f : f * 2f);
                c.a = Mathf.Max(a, f * 0.55f);
                overlay.color = c;
                if (followCamera != null)
                {
                    var p = followCamera.transform.position;
                    overlay.transform.position = new Vector3(p.x, p.y, overlay.transform.position.z);
                }
            }
            bool changed = Mathf.Abs(RainIntensity - previous) > 0.0001f && (RainIntensity == 0f || RainIntensity == targetRain || Time.frameCount % 15 == 0);
            if (playAmbience && (changed || ambienceDirty))
            {
                var audio = Services.Get<AudioService>();
                if (audio != null)
                {
                    ambienceDirty = false;
                    audio.SetAmbience(1, RainIntensity > 0.001f ? AmbienceTrackId.Rain : AmbienceTrackId.None, RainIntensity, 0.5f);
                    audio.SetAmbience(0, AmbienceTrackId.Forest, ForestLevel, 0.5f);
                }
            }
        }

        /// Darkens the tint targets with the rain (only when the intensity moved).
        void ApplyTint()
        {
            if (tintTargets == null || tintTargets.Length == 0 || tintBase == null) return;
            if (Mathf.Abs(RainIntensity - appliedTint) < 0.002f) return;
            appliedTint = RainIntensity;
            var k = Color.Lerp(Color.white, rainTint, RainIntensity);
            for (int i = 0; i < tintTargets.Length && i < tintBase.Length; i++)
            {
                var r = tintTargets[i];
                if (r == null) continue;
                var b = tintBase[i];
                r.color = new Color(b.r * k.r, b.g * k.g, b.b * k.b, b.a);
            }
        }

        void SetRates(float rainIntensity, float wind)
        {
            var tier = QualityApplier.CurrentTier;
            float rainScale = tier == QualityTier.Low ? 0f : tier == QualityTier.Medium ? mediumRainScale : 1f;
            float splashScale = tier == QualityTier.Low ? 0f : tier == QualityTier.Medium ? mediumSplashScale : 1f;
            if (QualityApplier.ReduceMotion) { rainScale *= reduceMotionRainScale; splashScale *= reduceMotionRainScale; }
            float leafBudget = tier == QualityTier.Low ? 0.45f : tier == QualityTier.High ? 1.2f : 1f;
            if (rain != null)
            {
                var em = rain.emission; em.rateOverTime = maxRainRate * rainIntensity * rainScale;
                var v = rain.velocityOverLifetime; v.x = new ParticleSystem.MinMaxCurve(-wind * 1.4f - 0.5f, -wind * 1.4f + 0.3f);
                if (rainIntensity <= 0f && rain.isPlaying && rain.particleCount == 0) rain.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            if (splashes != null)
            {
                var em = splashes.emission; em.rateOverTime = maxSplashRate * rainIntensity * splashScale;
            }
            foreach (var x in extraSplashes)
            {
                if (x == null) continue;
                var em = x.emission; em.rateOverTime = maxSplashRate * rainIntensity * splashScale;
            }
            if (leaves != null)
            {
                var em = leaves.emission; em.rateOverTime = QualityApplier.ReduceMotion ? 0f : baseLeafRate * leafBudget * (1f + wind * 0.3f);
                var v = leaves.velocityOverLifetime; v.x = new ParticleSystem.MinMaxCurve(-wind * 1.1f - 0.4f, -wind * 0.6f + 0.2f);
            }
        }
    }
}
