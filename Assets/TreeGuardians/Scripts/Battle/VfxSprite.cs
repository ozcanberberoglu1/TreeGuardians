using TreeGuardians.Core;
using UnityEngine;

namespace TreeGuardians.Battle
{
    /// Everything one pooled effect sprite needs for its short life. Filled per spawn (struct, no allocation).
    public struct VfxParams
    {
        public Vector2 position, velocity;
        public Color tint, endTint;
        public bool useEndTint;
        public float scaleFrom, scaleTo, life;
        [Tooltip("0..1: alpha stays full until this fraction of the life, then fades out.")] public float fadeStart;
        public float gravity, drag, spin, rotation;
        public float groundY, restitution;
        public int maxBounces;
        public bool alignToVelocity;
        public Vector2 aspect;       // non-uniform scale multiplier (1,1 = uniform)
        public Sprite sprite;        // null = keep the prefab sprite
        public int sortingOrder;     // 0 = keep the prefab order
        public Ease scaleEase;

        public static VfxParams At(Vector2 pos, Sprite sprite, Color tint, float scaleFrom, float scaleTo, float life)
            => new VfxParams { position = pos, sprite = sprite, tint = tint, scaleFrom = scaleFrom, scaleTo = scaleTo, life = life, fadeStart = 0f, aspect = Vector2.one, groundY = float.NegativeInfinity, scaleEase = Ease.OutCubic };
    }

    /// Pooled sprite effect: scale/alpha/colour over life, ballistic motion with drag and ground bounces (debris).
    public sealed class VfxSprite : PooledBehaviour
    {
        [SerializeField] SpriteRenderer sprite;

        VfxParams p;
        Sprite defaultSprite;
        int defaultOrder;
        float age;
        int bounces;
        float angle;
        bool resting;

        void Awake()
        {
            if (sprite == null) sprite = GetComponent<SpriteRenderer>();
            if (sprite != null) { defaultSprite = sprite.sprite; defaultOrder = sprite.sortingOrder; }
        }

        /// Legacy signature kept for older call sites.
        public void Play(Vector2 position, Color tint, float scaleFrom, float scaleTo, float duration, Vector2 vel, float grav, float spinSpeed)
        {
            var q = VfxParams.At(position, null, tint, scaleFrom, scaleTo, duration);
            q.velocity = vel; q.gravity = grav; q.spin = spinSpeed;
            Play(q);
        }

        public void Play(in VfxParams param)
        {
            p = param;
            if (p.aspect == Vector2.zero) p.aspect = Vector2.one;
            p.life = Mathf.Max(0.05f, p.life);
            age = 0f;
            bounces = 0;
            resting = false;
            angle = p.rotation;
            transform.position = new Vector3(p.position.x, p.position.y, 0f);
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            ApplyScale(p.scaleFrom);
            if (sprite != null)
            {
                sprite.sprite = p.sprite != null ? p.sprite : defaultSprite;
                sprite.sortingOrder = p.sortingOrder != 0 ? p.sortingOrder : defaultOrder;
                sprite.color = p.tint;
            }
        }

        void ApplyScale(float s) => transform.localScale = new Vector3(s * p.aspect.x, s * p.aspect.y, 1f);

        public bool Step(float dt)
        {
            age += dt;
            float t = Mathf.Clamp01(age / p.life);
            if (!resting)
            {
                if (p.drag > 0f) p.velocity *= 1f / (1f + p.drag * dt);
                p.velocity.y -= p.gravity * dt;
                p.position += p.velocity * dt;
                if (p.position.y < p.groundY && p.velocity.y < 0f)
                {
                    p.position.y = p.groundY;
                    if (bounces < p.maxBounces)
                    {
                        p.velocity.y = -p.velocity.y * p.restitution;
                        p.velocity.x *= 0.55f;
                        p.spin *= 0.5f;
                        bounces++;
                    }
                    else { p.velocity = Vector2.zero; p.spin = 0f; resting = true; }
                }
                angle = p.alignToVelocity && p.velocity.sqrMagnitude > 0.01f ? Mathf.Atan2(p.velocity.y, p.velocity.x) * Mathf.Rad2Deg : angle + p.spin * dt;
                transform.SetPositionAndRotation(new Vector3(p.position.x, p.position.y, 0f), Quaternion.Euler(0f, 0f, angle));
            }
            ApplyScale(Mathf.LerpUnclamped(p.scaleFrom, p.scaleTo, TGTween.Evaluate(p.scaleEase, t)));
            if (sprite != null)
            {
                var c = p.useEndTint ? Color.Lerp(p.tint, p.endTint, t) : p.tint;
                float fade = t <= p.fadeStart ? 1f : 1f - Mathf.Pow((t - p.fadeStart) / Mathf.Max(0.0001f, 1f - p.fadeStart), 2f);
                c.a *= fade;
                sprite.color = c;
            }
            return age < p.life;
        }
    }
}
