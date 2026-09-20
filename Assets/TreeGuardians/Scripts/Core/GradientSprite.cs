using UnityEngine;

namespace TreeGuardians.Core
{
    /// Vertical two-color gradient on a SpriteRenderer (tiny runtime texture, cached per instance).
    [RequireComponent(typeof(SpriteRenderer))]
    [ExecuteAlways]
    public sealed class GradientSprite : MonoBehaviour
    {
        [SerializeField] Color top = new Color(0.42f, 0.72f, 0.98f);
        [SerializeField] Color bottom = new Color(0.85f, 0.95f, 1f);
        [SerializeField] Vector2 worldSize = new Vector2(40f, 14f);

        Texture2D texture;
        Sprite sprite;
        Color lastTop, lastBottom;

        void OnEnable() => Rebuild();

        void OnValidate() => Rebuild();

        public void SetColors(Color newTop, Color newBottom)
        {
            top = newTop;
            bottom = newBottom;
            Rebuild();
        }

        void Rebuild()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null) return;
            if (texture == null)
            {
                texture = new Texture2D(2, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            }
            if (sprite == null || lastTop != top || lastBottom != bottom)
            {
                for (int y = 0; y < 64; y++)
                {
                    var c = Color.Lerp(bottom, top, y / 63f);
                    texture.SetPixel(0, y, c);
                    texture.SetPixel(1, y, c);
                }
                texture.Apply(false);
                lastTop = top;
                lastBottom = bottom;
                sprite = Sprite.Create(texture, new Rect(0, 0, 2, 64), new Vector2(0.5f, 0.5f), 1f);
                sprite.hideFlags = HideFlags.HideAndDontSave;
                sr.sprite = sprite;
            }
            sr.color = Color.white;
            transform.localScale = new Vector3(worldSize.x / 2f, worldSize.y / 64f, 1f);
        }

        void OnDisable()
        {
            if (Application.isPlaying) return;
        }

        void OnDestroy()
        {
            if (sprite != null) DestroyImmediate(sprite);
            if (texture != null) DestroyImmediate(texture);
        }
    }
}
