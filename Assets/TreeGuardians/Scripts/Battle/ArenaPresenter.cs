using TreeGuardians.Core;
using TreeGuardians.Data;
using UnityEngine;

namespace TreeGuardians.Battle
{
    /// Applies an ArenaDefinition to the pre-authored environment layers and drifts them very slowly.
    public sealed class ArenaPresenter : MonoBehaviour
    {
        [SerializeField] GradientSprite sky;
        [SerializeField] SpriteRenderer[] clouds = new SpriteRenderer[0];
        [SerializeField] SpriteRenderer mountains;
        [SerializeField] SpriteRenderer forestBack;
        [SerializeField] SpriteRenderer forestMid;
        [SerializeField] SpriteRenderer ground;
        [SerializeField] SpriteRenderer foreground;
        [SerializeField] SpriteRenderer mist;
        [SerializeField] ParticleSystem leaves;
        [SerializeField] ParticleSystem fireflies;
        [SerializeField] float cloudWrapWidth = 34f;
        [SerializeField] float farDriftWidth = 2f;

        ArenaDefinition arena;
        Vector3 mountainsBase, forestBackBase, forestMidBase;
        float t;

        public void Apply(ArenaDefinition def)
        {
            arena = def;
            if (def == null) return;
            if (sky != null) sky.SetColors(def.skyTop, def.skyBottom);
            foreach (var c in clouds) if (c != null) { c.color = def.ambientTint * new Color(1f, 1f, 1f, 0.9f); if (def.cloudsSprite != null) c.sprite = def.cloudsSprite; }
            Tint(mountains, def.mountainsSprite, Color.Lerp(def.skyBottom, def.groundColor, 0.45f) * def.ambientTint);
            Tint(forestBack, def.forestBackSprite, Color.Lerp(def.groundColor, def.skyBottom, 0.35f) * def.ambientTint);
            Tint(forestMid, def.forestMidSprite, Color.Lerp(def.groundColor, Color.black, 0.15f) * def.ambientTint);
            Tint(ground, def.groundSprite, def.groundColor);
            Tint(foreground, def.foregroundSprite, Color.Lerp(def.groundColor, Color.black, 0.3f));
            if (mist != null) { mist.enabled = def.mist; mist.color = def.fogColor.a > 0f ? def.fogColor : new Color(1f, 1f, 1f, 0.3f); }
            if (leaves != null) { if (def.leafParticles && QualityApplier.CurrentTier != QualityTier.Low) leaves.Play(); else leaves.Stop(); }
            if (fireflies != null) { if (def.fireflyParticles && QualityApplier.CurrentTier != QualityTier.Low) fireflies.Play(); else fireflies.Stop(); }
            if (mountains != null) mountainsBase = mountains.transform.localPosition;
            if (forestBack != null) forestBackBase = forestBack.transform.localPosition;
            if (forestMid != null) forestMidBase = forestMid.transform.localPosition;
        }

        static void Tint(SpriteRenderer r, Sprite s, Color c)
        {
            if (r == null) return;
            if (s != null) r.sprite = s;
            c.a = 1f;
            r.color = c;
        }

        void Update()
        {
            if (arena == null || QualityApplier.ReduceMotion) return;
            float dt = Time.deltaTime;
            t += dt;
            for (int i = 0; i < clouds.Length; i++)
            {
                var c = clouds[i];
                if (c == null) continue;
                var p = c.transform.localPosition;
                p.x += arena.cloudSpeed * (0.8f + 0.2f * i) * dt;
                if (p.x > cloudWrapWidth * 0.5f) p.x -= cloudWrapWidth;
                c.transform.localPosition = p;
            }
            if (mountains != null) mountains.transform.localPosition = mountainsBase + new Vector3(Mathf.Sin(t * 0.05f) * farDriftWidth * arena.mountainParallax * 10f, 0f, 0f);
            if (forestBack != null) forestBack.transform.localPosition = forestBackBase + new Vector3(-Mathf.Sin(t * 0.07f) * farDriftWidth * arena.forestBackParallax * 10f, 0f, 0f);
            if (forestMid != null) forestMid.transform.localPosition = forestMidBase + new Vector3(Mathf.Sin(t * 0.09f) * farDriftWidth * arena.forestMidParallax * 10f, 0f, 0f);
        }
    }
}
