using UnityEngine;

namespace TreeGuardians.Trees
{
    /// World-space destruction mask shared by all parts of one castle: an R8 texture covering the castle's bounds,
    /// 255 = intact, 0 = hole. Carving paints soft circles on the CPU buffer and uploads once per hit.
    public sealed class DestructibleMask
    {
        public Texture2D Texture { get; private set; }
        public Rect WorldRect { get; }
        public int Width { get; }
        public int Height { get; }

        readonly byte[] data;
        /// Logical openings (destroyed parts). Kept out of the rendered texture so neighbouring parts that overlap
        /// a destroyed part's bounds stay painted; hit tests still treat the area as open.
        readonly byte[] opened;
        bool dirty;

        public DestructibleMask(Rect worldRect, float pixelsPerUnit)
        {
            WorldRect = worldRect;
            Width = Mathf.Clamp(Mathf.CeilToInt(worldRect.width * pixelsPerUnit), 8, 1024);
            Height = Mathf.Clamp(Mathf.CeilToInt(worldRect.height * pixelsPerUnit), 8, 1024);
            data = new byte[Width * Height];
            opened = new byte[Width * Height];
            Texture = new Texture2D(Width, Height, TextureFormat.R8, false, true)
            {
                name = "DestructibleMask",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            Reset();
        }

        /// (xmin, ymin, 1/width, 1/height) for the shader.
        public Vector4 ShaderRect => new Vector4(WorldRect.xMin, WorldRect.yMin, 1f / Mathf.Max(0.0001f, WorldRect.width), 1f / Mathf.Max(0.0001f, WorldRect.height));

        public void Reset()
        {
            for (int i = 0; i < data.Length; i++) data[i] = 255;
            System.Array.Clear(opened, 0, opened.Length);
            dirty = true;
            Apply();
        }

        /// Paints a hole: fully open inside radius * (1 - softness), fading to intact at radius.
        public void Carve(Vector2 world, float radius, float softness = 0.35f)
        {
            if (radius <= 0f) return;
            float px = (world.x - WorldRect.xMin) / WorldRect.width * Width;
            float py = (world.y - WorldRect.yMin) / WorldRect.height * Height;
            float rx = radius / WorldRect.width * Width;
            float ry = radius / WorldRect.height * Height;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(px - rx)), x1 = Mathf.Min(Width - 1, Mathf.CeilToInt(px + rx));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(py - ry)), y1 = Mathf.Min(Height - 1, Mathf.CeilToInt(py + ry));
            if (x0 > x1 || y0 > y1) return;
            float inner = Mathf.Clamp01(1f - softness);
            for (int y = y0; y <= y1; y++)
            {
                float dy = (y + 0.5f - py) / ry;
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x + 0.5f - px) / rx;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d >= 1f) continue;
                    float v = inner >= 1f ? 0f : Mathf.Clamp01((d - inner) / (1f - inner));
                    byte b = (byte)Mathf.RoundToInt(v * 255f);
                    int i = y * Width + x;
                    if (b < data[i]) data[i] = b;
                }
            }
            dirty = true;
        }

        /// Opens the whole rectangle (a part that lost all its health).
        public void ClearRect(Rect world)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt((world.xMin - WorldRect.xMin) / WorldRect.width * Width), 0, Width - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((world.xMax - WorldRect.xMin) / WorldRect.width * Width), 0, Width - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt((world.yMin - WorldRect.yMin) / WorldRect.height * Height), 0, Height - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt((world.yMax - WorldRect.yMin) / WorldRect.height * Height), 0, Height - 1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++) opened[y * Width + x] = 1;
        }

        /// 0 = hole, 1 = intact. Points outside the mask count as intact.
        public float SampleAt(Vector2 world)
        {
            if (!WorldRect.Contains(world)) return 1f;
            int x = Mathf.Clamp((int)((world.x - WorldRect.xMin) / WorldRect.width * Width), 0, Width - 1);
            int y = Mathf.Clamp((int)((world.y - WorldRect.yMin) / WorldRect.height * Height), 0, Height - 1);
            int i = y * Width + x;
            return opened[i] != 0 ? 0f : data[i] / 255f;
        }

        public bool IsDestroyedAt(Vector2 world) => SampleAt(world) < 0.5f;

        /// Fraction of the rectangle that is open (0..1).
        public float DestroyedFraction(Rect world)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt((world.xMin - WorldRect.xMin) / WorldRect.width * Width), 0, Width - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((world.xMax - WorldRect.xMin) / WorldRect.width * Width), 0, Width - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt((world.yMin - WorldRect.yMin) / WorldRect.height * Height), 0, Height - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt((world.yMax - WorldRect.yMin) / WorldRect.height * Height), 0, Height - 1);
            int total = 0, open = 0;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++) { total++; int i = y * Width + x; if (opened[i] != 0 || data[i] < 128) open++; }
            return total > 0 ? open / (float)total : 0f;
        }

        /// Uploads pending changes (call once per hit, not per pixel).
        public void Apply()
        {
            if (!dirty || Texture == null) return;
            Texture.SetPixelData(data, 0);
            Texture.Apply(false, false);
            dirty = false;
        }

        public void Dispose()
        {
            if (Texture != null)
            {
                if (Application.isPlaying) Object.Destroy(Texture);
                else Object.DestroyImmediate(Texture);
            }
            Texture = null;
        }
    }
}
