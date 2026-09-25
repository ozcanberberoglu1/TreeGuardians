using TMPro;
using TreeGuardians.Core;
using UnityEngine;

namespace TreeGuardians.Battle
{
    /// Pooled world-space damage number / short label with a pop-in, rise and fade. No string building for numbers.
    public sealed class DamageText : PooledBehaviour
    {
        [SerializeField] TMP_Text text;
        float age, life, baseSize;
        Vector3 velocity;
        Color color;

        public void Show(Vector2 position, int value, string prefix, Color tint, float size, float duration)
        {
            Begin(position, tint, size, duration);
            if (text != null) text.SetText(prefix == "+" ? "+{0}" : "-{0}", value);
        }

        public void ShowLabel(Vector2 position, string label, Color tint, float size, float duration)
        {
            Begin(position, tint, size, duration);
            if (text != null) text.SetText(label);
            velocity = new Vector3(0f, 0.9f, 0f);
        }

        /// Legacy signature.
        public void Show(Vector2 position, string value, Color tint, float size, float duration)
        {
            Begin(position, tint, size, duration);
            if (text != null) text.SetText(value);
        }

        void Begin(Vector2 position, Color tint, float size, float duration)
        {
            transform.position = new Vector3(position.x, position.y, -1f);
            transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-6f, 6f));
            age = 0f;
            life = Mathf.Max(0.3f, duration);
            velocity = new Vector3(Random.Range(-0.25f, 0.25f), 1.5f, 0f);
            color = tint;
            baseSize = size;
            if (text != null) { text.color = tint; text.fontSize = size; }
            transform.localScale = Vector3.one * 0.4f;
        }

        public bool Step(float dt)
        {
            age += dt;
            float t = Mathf.Clamp01(age / life);
            velocity.y = Mathf.MoveTowards(velocity.y, 0.25f, 3f * dt);
            transform.position += velocity * dt;
            float pop = age < 0.18f ? TGTween.Evaluate(Ease.OutBack, age / 0.18f) : 1f;
            transform.localScale = Vector3.one * Mathf.LerpUnclamped(0.4f, 1f, pop);
            if (text != null) { var c = color; c.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f; text.color = c; }
            return age < life;
        }
    }
}
