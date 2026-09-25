using System.IO;
using UnityEditor;
using UnityEngine;

namespace TreeGuardians.Editor
{
    /// Soft, tintable effect sprites (rain, smoke, debris, sparks, rings, confetti) drawn procedurally at import quality.
    /// Menu: Tree Guardians/Art/Generate VFX Sprites. Output: Assets/TreeGuardians/Art/VFX/fx_*.png
    public static class VfxArtGenerator
    {
        public const string Folder = "Assets/TreeGuardians/Art/VFX";
        public static string PathOf(string name) => $"{Folder}/fx_{name}.png";

        [MenuItem("Tree Guardians/Art/Generate VFX Sprites", priority = 5)]
        public static void GenerateAll()
        {
            RainStreak();
            RainSplash();
            Puff("soft_puff", 128, 7, 11, 0.95f);
            Puff("smoke_puff", 128, 11, 23, 0.8f);
            Dust();
            for (int i = 0; i < 4; i++) Splinter(i);
            for (int i = 0; i < 3; i++) Chip(i);
            Spark();
            Ring();
            Flash();
            Confetti();
            SpeedLine();
            AssetDatabase.Refresh();
            Debug.Log("[TG] VFX sprites generated in " + Folder);
        }

        // ------------------------------------------------------------------ helpers
        static Color[] Blank(int w, int h) { var c = new Color[w * h]; for (int i = 0; i < c.Length; i++) c[i] = new Color(1f, 1f, 1f, 0f); return c; }

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1442695041;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0x7fffffff) / (float)int.MaxValue;
            }
        }

        static float ValueNoise(float x, float y, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float tx = x - xi, ty = y - yi;
            tx = tx * tx * (3f - 2f * tx); ty = ty * ty * (3f - 2f * ty);
            float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed), c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        static float Fbm(float x, float y, int seed)
        {
            float v = 0f, amp = 0.5f, f = 1f;
            for (int o = 0; o < 4; o++) { v += ValueNoise(x * f, y * f, seed + o * 17) * amp; f *= 2f; amp *= 0.5f; }
            return v;
        }

        static void Save(string name, int w, int h, Color[] px, float ppu = 100f, Vector2? pivot = null)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply();
            var path = PathOf(name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is TextureImporter ti)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = ppu;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.filterMode = FilterMode.Bilinear;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                var s = new TextureImporterSettings();
                ti.ReadTextureSettings(s);
                s.spriteMeshType = SpriteMeshType.FullRect;
                if (pivot.HasValue) { s.spriteAlignment = (int)SpriteAlignment.Custom; s.spritePivot = pivot.Value; }
                ti.SetTextureSettings(s);
                ti.SaveAndReimport();
            }
        }

        static void Put(Color[] px, int w, int h, int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int i = y * w + x;
            var d = px[i];
            float a = c.a + d.a * (1f - c.a);
            if (a <= 0f) return;
            px[i] = new Color((c.r * c.a + d.r * d.a * (1f - c.a)) / a, (c.g * c.a + d.g * d.a * (1f - c.a)) / a, (c.b * c.a + d.b * d.a * (1f - c.a)) / a, a);
        }

        // ------------------------------------------------------------------ sprites
        /// Horizontal streak: stretched-billboard particles map the texture's U axis along the direction of motion.
        static void RainStreak()
        {
            int w = 128, h = 8; var px = Blank(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float t = x / (float)(w - 1);               // 0 tail .. 1 head
                    float across = 1f - Mathf.Abs((y + 0.5f) - h * 0.5f) / (h * 0.5f);
                    float along = Mathf.SmoothStep(0f, 1f, t / 0.8f) * Mathf.SmoothStep(0f, 1f, (1f - t) / 0.08f);
                    px[y * w + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Pow(across, 1.3f) * along));
                }
            Save("rain_streak", w, h, px);
        }

        static void RainSplash()
        {
            int w = 64, h = 32; var px = Blank(w, h);
            // three droplets arcing out of a thin ring
            for (int k = -1; k <= 1; k++)
            {
                float cx = w * 0.5f + k * 16f, cy = 10f + (k == 0 ? 12f : 6f);
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)) / 3.2f;
                    if (d < 1f) Put(px, w, h, x, y, new Color(1f, 1f, 1f, (1f - d * d) * 0.95f));
                }
            }
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f - w * 0.5f) / (w * 0.42f), dy = (y + 0.5f - 4f) / 3.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.85f) / 0.18f);
                if (a > 0f) Put(px, w, h, x, y, new Color(1f, 1f, 1f, a * 0.7f));
            }
            Save("rain_splash", w, h, px, 100f, new Vector2(0.5f, 0.1f));
        }

        static void Puff(string name, int size, int blobs, int seed, float density)
        {
            var px = Blank(size, size);
            var rnd = new System.Random(seed);
            var centers = new Vector3[blobs];
            for (int i = 0; i < blobs; i++)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f, rad = (float)rnd.NextDouble() * size * 0.18f;
                centers[i] = new Vector3(size * 0.5f + Mathf.Cos(ang) * rad, size * 0.5f + Mathf.Sin(ang) * rad, size * (0.18f + (float)rnd.NextDouble() * 0.12f));
            }
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float field = 0f;
                for (int i = 0; i < blobs; i++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(centers[i].x, centers[i].y)) / centers[i].z;
                    field += Mathf.Exp(-d * d * 1.6f);
                }
                float n = Fbm(x / (size * 0.18f), y / (size * 0.18f), seed);
                float a = Mathf.Clamp01((field - 0.35f) * 1.4f) * Mathf.Lerp(0.7f, 1.1f, n) * density;
                float edge = 1f - Mathf.Clamp01((Vector2.Distance(new Vector2(x, y), new Vector2(size * 0.5f, size * 0.5f)) - size * 0.40f) / (size * 0.1f));
                a *= edge;
                float shade = Mathf.Lerp(0.82f, 1f, Mathf.Clamp01((y / (float)size) * 0.6f + n * 0.5f));
                px[y * size + x] = new Color(shade, shade, shade, Mathf.Clamp01(a));
            }
            Save(name, size, size, px);
        }

        static void Dust()
        {
            int w = 128, h = 56; var px = Blank(w, h);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f - w * 0.5f) / (w * 0.5f), dy = (y + 0.5f - h * 0.35f) / (h * 0.6f);
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float n = Fbm(x / 14f, y / 10f, 5);
                float a = Mathf.Clamp01(1f - r) * Mathf.Lerp(0.5f, 1f, n);
                px[y * w + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a * 1.3f));
            }
            Save("dust", w, h, px);
        }

        static bool InPoly(Vector2 p, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) inside = !inside;
            return inside;
        }

        static float PolyDist(Vector2 p, Vector2[] poly)
        {
            float best = float.MaxValue;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[j], b = poly[i], ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return InPoly(p, poly) ? -best : best;
        }

        static readonly Color WoodLight = new Color(0.86f, 0.62f, 0.36f), WoodMid = new Color(0.66f, 0.43f, 0.22f), WoodDark = new Color(0.27f, 0.16f, 0.08f);

        static void Splinter(int variant)
        {
            int w = 80, h = 24; var px = Blank(w, h);
            var rnd = new System.Random(100 + variant);
            float len = w - 8f, top = h - 5f, bot = 5f;
            var pts = new System.Collections.Generic.List<Vector2> { new Vector2(4, bot + 2), new Vector2(4 + len * 0.3f, bot) };
            float x = 4 + len * 0.55f;
            while (x < 4 + len) { pts.Add(new Vector2(x, bot + (float)rnd.NextDouble() * 4f)); x += 3f + (float)rnd.NextDouble() * 5f; }
            pts.Add(new Vector2(4 + len, h * 0.5f + ((float)rnd.NextDouble() - 0.5f) * 6f));
            x = 4 + len;
            while (x > 4 + len * 0.5f) { x -= 3f + (float)rnd.NextDouble() * 5f; pts.Add(new Vector2(x, top - (float)rnd.NextDouble() * 4f)); }
            pts.Add(new Vector2(4 + len * 0.2f, top)); pts.Add(new Vector2(4, top - 2));
            var poly = pts.ToArray();
            for (int y = 0; y < h; y++) for (int xx = 0; xx < w; xx++)
            {
                var p = new Vector2(xx + 0.5f, y + 0.5f);
                float d = PolyDist(p, poly);
                if (d > 1.2f) continue;
                float grain = Mathf.Sin((y + ValueNoise(xx / 9f, y / 3f, variant) * 3f) * 1.9f) * 0.5f + 0.5f;
                var fill = Color.Lerp(WoodMid, WoodLight, Mathf.Clamp01(y / (float)h * 0.9f + grain * 0.25f));
                var c = d > -1.6f ? WoodDark : fill;
                float a = Mathf.Clamp01(1.2f - d);
                Put(px, w, h, xx, y, new Color(c.r, c.g, c.b, a));
            }
            Save("splinter_" + variant, w, h, px);
        }

        static void Chip(int variant)
        {
            int s = 32; var px = Blank(s, s);
            var rnd = new System.Random(200 + variant);
            int n = 6 + variant;
            var poly = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f, r = s * (0.28f + (float)rnd.NextDouble() * 0.16f);
                poly[i] = new Vector2(s * 0.5f + Mathf.Cos(a) * r, s * 0.5f + Mathf.Sin(a) * r * 0.75f);
            }
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            {
                float d = PolyDist(new Vector2(x + 0.5f, y + 0.5f), poly);
                if (d > 1.2f) continue;
                var fill = Color.Lerp(WoodMid, WoodLight, y / (float)s);
                var c = d > -1.4f ? WoodDark : fill;
                Put(px, s, s, x, y, new Color(c.r, c.g, c.b, Mathf.Clamp01(1.2f - d)));
            }
            Save("chip_" + variant, s, s, px);
        }

        static void Spark()
        {
            int s = 64; var px = Blank(s, s);
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            {
                float dx = (x + 0.5f - s * 0.5f) / (s * 0.5f), dy = (y + 0.5f - s * 0.5f) / (s * 0.5f);
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float star = Mathf.Max(Mathf.Clamp01(1f - Mathf.Abs(dx) * 9f) * Mathf.Clamp01(1f - Mathf.Abs(dy)), Mathf.Clamp01(1f - Mathf.Abs(dy) * 9f) * Mathf.Clamp01(1f - Mathf.Abs(dx)));
                float core = Mathf.Pow(Mathf.Clamp01(1f - r), 3f);
                px[y * s + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(star * 0.9f + core));
            }
            Save("spark", s, s, px);
        }

        static void Ring()
        {
            int s = 128; var px = Blank(s, s);
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            {
                float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(s * 0.5f, s * 0.5f)) / (s * 0.5f);
                float a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.86f) / 0.1f);
                a = a * a * (3f - 2f * a);
                px[y * s + x] = new Color(1f, 1f, 1f, a);
            }
            Save("ring", s, s, px);
        }

        static void Flash()
        {
            int s = 128; var px = Blank(s, s);
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            {
                float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(s * 0.5f, s * 0.5f)) / (s * 0.5f);
                float a = Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f) + Mathf.Pow(Mathf.Clamp01(1f - r * 2.2f), 2f) * 0.6f;
                px[y * s + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }
            Save("flash", s, s, px);
        }

        static void Confetti()
        {
            int w = 20, h = 12; var px = Blank(w, h);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Abs(x + 0.5f - w * 0.5f) - (w * 0.5f - 3f)), dy = Mathf.Max(0f, Mathf.Abs(y + 0.5f - h * 0.5f) - (h * 0.5f - 3f));
                float d = Mathf.Sqrt(dx * dx + dy * dy) - 2.2f;
                float shade = 0.85f + 0.15f * (y / (float)h);
                px[y * w + x] = new Color(shade, shade, shade, Mathf.Clamp01(0.8f - d));
            }
            Save("confetti", w, h, px);
        }

        static void SpeedLine()
        {
            int w = 96, h = 12; var px = Blank(w, h);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float t = x / (float)(w - 1);
                float across = 1f - Mathf.Abs(y + 0.5f - h * 0.5f) / (h * 0.5f);
                px[y * w + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Pow(across, 1.5f) * Mathf.SmoothStep(0f, 1f, t) * Mathf.SmoothStep(1f, 0.85f, t) * 1.2f));
            }
            Save("speed_line", w, h, px, 100f, new Vector2(1f, 0.5f));
        }
    }
}
