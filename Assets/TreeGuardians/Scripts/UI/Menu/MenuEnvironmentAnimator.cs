using TreeGuardians.Core;
using UnityEngine;

namespace TreeGuardians.UI.Menu
{
    /// Drifting clouds and floating leaves behind the menu. Ground and tree stay put.
    public sealed class MenuEnvironmentAnimator : MonoBehaviour
    {
        [SerializeField] Transform[] cloudLayers = new Transform[0];
        [SerializeField] float[] cloudSpeeds = new float[0];
        [SerializeField] float cloudWrapWidth = 30f;
        [SerializeField] Transform[] leaves = new Transform[0];
        [SerializeField] float leafFallSpeed = 0.6f;
        [SerializeField] float leafSway = 0.6f;
        [SerializeField] Vector2 leafArea = new Vector2(16f, 10f);

        Vector3[] leafBase;
        float[] leafPhase;

        void Awake()
        {
            leafBase = new Vector3[leaves.Length];
            leafPhase = new float[leaves.Length];
            for (int i = 0; i < leaves.Length; i++)
            {
                if (leaves[i] == null) continue;
                leafBase[i] = leaves[i].localPosition;
                leafPhase[i] = i * 1.7f;
            }
        }

        void Update()
        {
            if (QualityApplier.ReduceMotion) return;
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < cloudLayers.Length; i++)
            {
                var c = cloudLayers[i];
                if (c == null) continue;
                float speed = i < cloudSpeeds.Length ? cloudSpeeds[i] : 0.3f;
                var p = c.localPosition;
                p.x += speed * dt;
                if (p.x > cloudWrapWidth * 0.5f) p.x -= cloudWrapWidth;
                c.localPosition = p;
            }
            float t = Time.unscaledTime;
            for (int i = 0; i < leaves.Length; i++)
            {
                var l = leaves[i];
                if (l == null) continue;
                var p = l.localPosition;
                p.y -= leafFallSpeed * dt * (0.7f + 0.3f * Mathf.Sin(leafPhase[i]));
                p.x += Mathf.Sin(t * 1.3f + leafPhase[i]) * leafSway * dt;
                if (p.y < -leafArea.y * 0.5f) { p.y = leafArea.y * 0.5f; p.x = Random.Range(-leafArea.x * 0.5f, leafArea.x * 0.5f); }
                l.localPosition = p;
                l.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 2f + leafPhase[i]) * 25f);
            }
        }
    }
}
