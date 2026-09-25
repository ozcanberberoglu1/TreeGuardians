using System.IO;
using UnityEditor;
using UnityEngine;

namespace TreeGuardians.Editor
{
    /// Tileable value-noise texture that roughens the castle hole rims (TG_SpriteDestructible _RimNoiseTex).
    /// Grain is slightly stretched vertically so edges read as splintered planks. Linear, Repeat, mipmapped.
    /// Menu: Tree Guardians/Art/Generate Rim Noise. Output: Assets/TreeGuardians/Art/Textures/fx_rim_noise.png
    public static class RimNoiseGenerator
    {
        public const string Folder = "Assets/TreeGuardians/Art/Textures";
        public const string TexturePath = Folder + "/fx_rim_noise.png";
        const int Size = 128;
        const int Seed = 7331;

        /// Returns the rim noise texture, generating it first when it does not exist yet.
        public static Texture2D Ensure()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            return tex != null ? tex : Generate();
        }

        [MenuItem("Tree Guardians/Art/Generate Rim Noise", priority = 6)]
        public static void GenerateMenu()
        {
            var tex = Generate();
            Debug.Log(tex != null ? "[TG] Rim noise generated: " + TexturePath : "[TG] Rim noise generation failed.");
        }

        public static Texture2D Generate()
        {
            var px = new Color32[Size * Size];
            // Octaves as (cells across, cells down); every period divides the tile, so the result wraps seamlessly.
            var octaves = new[] { new Vector2Int(6, 3), new Vector2Int(12, 6), new Vector2Int(24, 12), new Vector2Int(48, 24) };
            var weights = new[] { 0.5f, 0.27f, 0.15f, 0.08f };
            float min = float.MaxValue, max = float.MinValue;
            var values = new float[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float v = 0f;
                    for (int o = 0; o < octaves.Length; o++)
                    {
                        var p = octaves[o];
                        v += TileNoise(x * p.x / (float)Size, y * p.y / (float)Size, p.x, p.y, Seed + o * 31) * weights[o];
                    }
                    values[y * Size + x] = v;
                    if (v < min) min = v;
                    if (v > max) max = v;
                }
            }
            float range = Mathf.Max(0.0001f, max - min);
            for (int i = 0; i < values.Length; i++)
            {
                float n = (values[i] - min) / range;              // normalise to 0..1
                n = Mathf.Clamp01((n - 0.5f) * 1.35f + 0.5f);     // a little more contrast: sharper splinters
                byte b = (byte)Mathf.RoundToInt(n * 255f);
                px[i] = new Color32(b, b, b, 255);
            }

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true);
            tex.SetPixels32(px);
            tex.Apply();
            ContentBuilder.EnsureFolder(Folder);
            File.WriteAllBytes(TexturePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(TexturePath) is TextureImporter ti)
            {
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = false;
                ti.alphaSource = TextureImporterAlphaSource.None;
                ti.mipmapEnabled = true;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.filterMode = FilterMode.Bilinear;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.isReadable = false;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }

        /// Smooth value noise on a lattice that wraps every (periodX, periodY) cells.
        static float TileNoise(float x, float y, int periodX, int periodY, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float tx = x - xi, ty = y - yi;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            int x0 = Wrap(xi, periodX), x1 = Wrap(xi + 1, periodX);
            int y0 = Wrap(yi, periodY), y1 = Wrap(yi + 1, periodY);
            float a = Hash(x0, y0, seed), b = Hash(x1, y0, seed), c = Hash(x0, y1, seed), d = Hash(x1, y1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        static int Wrap(int v, int period) => ((v % period) + period) % period;

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1442695041;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0x7fffffff) / (float)int.MaxValue;
            }
        }
    }
}
