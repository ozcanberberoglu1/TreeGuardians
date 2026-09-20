using TreeGuardians.Core;
using UnityEngine;

namespace TreeGuardians.Battle
{
    /// Pooled sprite effect: grows and fades, optional ballistic motion (debris).
    public sealed class VfxSprite : PooledBehaviour
    {
        [SerializeField] SpriteRenderer sprite;
        float age, life, startScale, endScale, gravity;
        Vector2 velocity;
        Color color;
        float spin;

        public void Play(Vector2 position, Color tint, float scaleFrom, float scaleTo, float duration, Vector2 vel, float grav, float spinSpeed)
        {
            transform.position = position;
            age = 0f;
            life = Mathf.Max(0.05f, duration);
            startScale = scaleFrom;
            endScale = scaleTo;
            velocity = vel;
            gravity = grav;
            color = tint;
            spin = spinSpeed;
            transform.localScale = Vector3.one * scaleFrom;
            if (sprite != null) sprite.color = tint;
        }

        public bool Step(float dt)
        {
            age += dt;
            float t = Mathf.Clamp01(age / life);
            velocity.y -= gravity * dt;
            transform.position += (Vector3)(velocity * dt);
            if (spin != 0f) transform.Rotate(0f, 0f, spin * dt);
            transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, TGTween.Evaluate(Ease.OutCubic, t));
            if (sprite != null) { var c = color; c.a = color.a * (1f - t * t); sprite.color = c; }
            return age < life;
        }
    }
}
