using TMPro;
using TreeGuardians.Core;
using UnityEngine;

namespace TreeGuardians.Battle
{
    /// Pooled world-space damage number.
    public sealed class DamageText : PooledBehaviour
    {
        [SerializeField] TMP_Text text;
        float age, life;
        Vector3 velocity;
        Color color;

        public void Show(Vector2 position, string value, Color tint, float size, float duration)
        {
            transform.position = new Vector3(position.x, position.y, -1f);
            age = 0f;
            life = duration;
            velocity = new Vector3(Random.Range(-0.4f, 0.4f), 1.6f, 0f);
            color = tint;
            if (text != null) { text.text = value; text.color = tint; text.fontSize = size; }
        }

        public bool Step(float dt)
        {
            age += dt;
            float t = Mathf.Clamp01(age / life);
            velocity.y -= 2.2f * dt;
            transform.position += velocity * dt;
            if (text != null) { var c = color; c.a = 1f - t * t; text.color = c; }
            return age < life;
        }
    }
}
