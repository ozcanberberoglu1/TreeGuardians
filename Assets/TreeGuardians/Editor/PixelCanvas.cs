using System.IO;
using UnityEditor;
using UnityEngine;

namespace TreeGuardians.Editor
{
    /// Tiny anti-aliased SDF rasterizer for generating clean placeholder sprites.
    public sealed class PixelCanvas
    {
        public readonly int Width;
        public readonly int Height;
        readonly Color[] px;

        public PixelCanvas(int width, int height) : this(width, height, Color.clear) { }

        public PixelCanvas(int width, int height, Color background)
        {
            Width = width;
            Height = height;
            px = new Color[width * height];
            for (int i = 0; i < px.Length; i++) px[i] = background;
        }

        static float Smooth(float d, float aa) => 1f - Mathf.Clamp01((d + aa) / (2f * aa));

        void Blend(int x, int y, Color c, float a)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return;
            a *= c.a;
            if (a <= 0f) return;
            int i = y * Width + x;
            var d = px[i];
            float outA = a + d.a * (1f - a);
            if (outA <= 0f) { px[i] = Color.clear; return; }
            float r = (c.r * a + d.r * d.a * (1f - a)) / outA;
            float g = (c.g * a + d.g * d.a * (1f - a)) / outA;
            float b = (c.b * a + d.b * d.a * (1f - a)) / outA;
            px[i] = new Color(r, g, b, outA);
        }

        public void VerticalGradient(Color top, Color bottom)
        {
            for (int y = 0; y < Height; y++)
            {
                float t = Height <= 1 ? 0f : (float)y / (Height - 1);
                var c = Color.Lerp(bottom, top, t);
                for (int x = 0; x < Width; x++) px[y * Width + x] = c;
            }
        }

        public void RadialGlow(float cx, float cy, float radius, Color color, float power = 2f)
        {
            int x0 = Mathf.Max(0, (int)(cx - radius - 1)), x1 = Mathf.Min(Width - 1, (int)(cx + radius + 1));
            int y0 = Mathf.Max(0, (int)(cy - radius - 1)), y1 = Mathf.Min(Height - 1, (int)(cy + radius + 1));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)) / radius;
                    if (d >= 1f) continue;
                    Blend(x, y, color, Mathf.Pow(1f - d, power));
                }
        }

        public void Circle(float cx, float cy, float r, Color color, float aa = 1f)
        {
            int x0 = Mathf.Max(0, (int)(cx - r - 2)), x1 = Mathf.Min(Width - 1, (int)(cx + r + 2));
            int y0 = Mathf.Max(0, (int)(cy - r - 2)), y1 = Mathf.Min(Height - 1, (int)(cy + r + 2));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)) - r;
                    float a = Smooth(d, aa);
                    if (a > 0f) Blend(x, y, color, a);
                }
        }

        public void CircleOutlined(float cx, float cy, float r, Color fill, Color outline, float thickness)
        {
            Circle(cx, cy, r + thickness, outline);
            Circle(cx, cy, r, fill);
        }

        /// Multiplies alpha down to zero inside a circle (anti-aliased), for rings and cut-outs.
        public void EraseCircle(float cx, float cy, float r, float aa = 1f)
        {
            int x0 = Mathf.Max(0, (int)(cx - r - 2)), x1 = Mathf.Min(Width - 1, (int)(cx + r + 2));
            int y0 = Mathf.Max(0, (int)(cy - r - 2)), y1 = Mathf.Min(Height - 1, (int)(cy + r + 2));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)) - r;
                    float a = Smooth(d, aa);
                    if (a <= 0f) continue;
                    int i = y * Width + x;
                    var c = px[i];
                    c.a *= 1f - a;
                    px[i] = c;
                }
        }

        public void Ring(float cx, float cy, float outerR, float thickness, Color color)
        {
            Circle(cx, cy, outerR, color);
            EraseCircle(cx, cy, outerR - thickness);
        }

        public void RingOutlined(float cx, float cy, float outerR, float thickness, Color fill, Color outline, float outlineThickness)
        {
            Circle(cx, cy, outerR + outlineThickness, outline);
            EraseCircle(cx, cy, outerR - thickness - outlineThickness);
            Circle(cx, cy, outerR, fill);
            EraseCircle(cx, cy, outerR - thickness);
            Circle(cx, cy, outerR - thickness, outline);
            EraseCircle(cx, cy, outerR - thickness - outlineThickness);
        }

        public void Ellipse(float cx, float cy, float rx, float ry, Color color, float aa = 1f)
        {
            int x0 = Mathf.Max(0, (int)(cx - rx - 2)), x1 = Mathf.Min(Width - 1, (int)(cx + rx + 2));
            int y0 = Mathf.Max(0, (int)(cy - ry - 2)), y1 = Mathf.Min(Height - 1, (int)(cy + ry + 2));
            float k = Mathf.Min(rx, ry);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float nx = (x + 0.5f - cx) / rx, ny = (y + 0.5f - cy) / ry;
                    float d = (Mathf.Sqrt(nx * nx + ny * ny) - 1f) * k;
                    float a = Smooth(d, aa);
                    if (a > 0f) Blend(x, y, color, a);
                }
        }

        public void EllipseOutlined(float cx, float cy, float rx, float ry, Color fill, Color outline, float thickness)
        {
            Ellipse(cx, cy, rx + thickness, ry + thickness, outline);
            Ellipse(cx, cy, rx, ry, fill);
        }

        public void RoundedRect(float x, float y, float w, float h, float radius, Color color, float aa = 1f)
        {
            float cx = x + w * 0.5f, cy = y + h * 0.5f;
            float hx = w * 0.5f - radius, hy = h * 0.5f - radius;
            int x0 = Mathf.Max(0, (int)(x - 2)), x1 = Mathf.Min(Width - 1, (int)(x + w + 2));
            int y0 = Mathf.Max(0, (int)(y - 2)), y1 = Mathf.Min(Height - 1, (int)(y + h + 2));
            for (int py = y0; py <= y1; py++)
                for (int pxl = x0; pxl <= x1; pxl++)
                {
                    float qx = Mathf.Abs(pxl + 0.5f - cx) - hx;
                    float qy = Mathf.Abs(py + 0.5f - cy) - hy;
                    float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
                    float d = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    float a = Smooth(d, aa);
                    if (a > 0f) Blend(pxl, py, color, a);
                }
        }

        public void RoundedRectOutlined(float x, float y, float w, float h, float radius, Color fill, Color outline, float thickness)
        {
            RoundedRect(x - thickness, y - thickness, w + thickness * 2f, h + thickness * 2f, radius + thickness, outline);
            RoundedRect(x, y, w, h, radius, fill);
        }

        public void Capsule(Vector2 a, Vector2 b, float r, Color color, float aa = 1f)
        {
            int x0 = Mathf.Max(0, (int)(Mathf.Min(a.x, b.x) - r - 2)), x1 = Mathf.Min(Width - 1, (int)(Mathf.Max(a.x, b.x) + r + 2));
            int y0 = Mathf.Max(0, (int)(Mathf.Min(a.y, b.y) - r - 2)), y1 = Mathf.Min(Height - 1, (int)(Mathf.Max(a.y, b.y) + r + 2));
            Vector2 ba = b - a;
            float bb = Mathf.Max(0.0001f, Vector2.Dot(ba, ba));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    Vector2 pa = p - a;
                    float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / bb);
                    float d = (pa - ba * h).magnitude - r;
                    float al = Smooth(d, aa);
                    if (al > 0f) Blend(x, y, color, al);
                }
        }

        public void Polygon(Vector2[] pts, Color color)
        {
            if (pts == null || pts.Length < 3) return;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var p in pts) { minX = Mathf.Min(minX, p.x); minY = Mathf.Min(minY, p.y); maxX = Mathf.Max(maxX, p.x); maxY = Mathf.Max(maxY, p.y); }
            int x0 = Mathf.Max(0, (int)minX - 1), x1 = Mathf.Min(Width - 1, (int)maxX + 1);
            int y0 = Mathf.Max(0, (int)minY - 1), y1 = Mathf.Min(Height - 1, (int)maxY + 1);
            const int S = 4;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int inside = 0;
                    for (int sy = 0; sy < S; sy++)
                        for (int sx = 0; sx < S; sx++)
                            if (PointInPolygon(new Vector2(x + (sx + 0.5f) / S, y + (sy + 0.5f) / S), pts)) inside++;
                    if (inside > 0) Blend(x, y, color, inside / (float)(S * S));
                }
        }

        static bool PointInPolygon(Vector2 p, Vector2[] poly)
        {
            bool c = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    c = !c;
            }
            return c;
        }

        public void Triangle(Vector2 a, Vector2 b, Vector2 c, Color color) => Polygon(new[] { a, b, c }, color);

        public void Star(float cx, float cy, float outerR, float innerR, int points, Color color, float rotation = 0f)
        {
            var pts = new Vector2[points * 2];
            for (int i = 0; i < pts.Length; i++)
            {
                float ang = rotation + Mathf.PI * 2f * i / pts.Length - Mathf.PI / 2f;
                float r = (i % 2 == 0) ? outerR : innerR;
                pts[i] = new Vector2(cx + Mathf.Cos(ang) * r, cy + Mathf.Sin(ang) * r);
            }
            Polygon(pts, color);
        }

        public void Leaf(float cx, float cy, float length, float width, float angleDeg, Color color)
        {
            float ang = angleDeg * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            Vector2 nrm = new Vector2(-dir.y, dir.x);
            int segs = 14;
            var pts = new Vector2[segs * 2];
            for (int i = 0; i < segs; i++)
            {
                float t = i / (float)(segs - 1);
                float w = Mathf.Sin(t * Mathf.PI) * width * 0.5f;
                Vector2 c = new Vector2(cx, cy) + dir * (t - 0.5f) * length;
                pts[i] = c + nrm * w;
                pts[segs * 2 - 1 - i] = c - nrm * w;
            }
            Polygon(pts, color);
        }

        public Texture2D ToTexture()
        {
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply(false, false);
            return tex;
        }

        public Sprite SaveSprite(string assetPath, float pixelsPerUnit = 100f, Vector4? border = null, bool compress = false, Vector2? pivot = null)
        {
            var dir = Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var tex = ToTexture();
            File.WriteAllBytes(assetPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (ti != null)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = pixelsPerUnit;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.filterMode = FilterMode.Bilinear;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.textureCompression = compress ? TextureImporterCompression.Compressed : TextureImporterCompression.Uncompressed;
                ti.spriteBorder = border ?? Vector4.zero;
                var settings = new TextureImporterSettings();
                ti.ReadTextureSettings(settings);
                settings.spriteMeshType = border.HasValue ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
                if (pivot.HasValue)
                {
                    settings.spriteAlignment = (int)SpriteAlignment.Custom;
                    settings.spritePivot = pivot.Value;
                }
                ti.SetTextureSettings(settings);
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }
    }
}
