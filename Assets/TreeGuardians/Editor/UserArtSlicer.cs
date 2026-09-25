using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace TreeGuardians.Editor
{
    /// Slices the user's loose art sheets into named sprites (non-destructive: only import settings change).
    public static class UserArtSlicer
    {
        public const string PlatformsPath = "Assets/Art/Platforms-.png";
        public const string BackdropPath = "Assets/Art/bg.png";

        struct Slice { public string name; public RectInt rect; public Vector2 pivot; public Slice(string n, int x, int y, int w, int h, Vector2 p) { name = n; rect = new RectInt(x, y, w, h); pivot = p; } }

        // Pixel rects (bottom-left origin) measured from the sheet's alpha islands, padded by 4 px.
        static readonly Slice[] PlatformSlices =
        {
            new Slice("platform_big", 315, 486, 1229, 424, new Vector2(0.5f, 1f)),
            new Slice("platform_small", 1421, 955, 373, 156, new Vector2(0.5f, 1f)),
            new Slice("platform_mid", 629, 1061, 675, 137, new Vector2(0.5f, 1f)),
            new Slice("platform_long", 343, 1265, 1398, 137, new Vector2(0.5f, 1f)),
            new Slice("leaf_0", 60, 1532, 141, 144, new Vector2(0.5f, 0.5f)),
            new Slice("leaf_1", 351, 1627, 166, 178, new Vector2(0.5f, 0.5f)),
            new Slice("leaf_2", 314, 1827, 199, 183, new Vector2(0.5f, 0.5f)),
        };

        [MenuItem("Tree Guardians/Art/Slice User Art (platforms, leaves, backdrop)", priority = 6)]
        public static void SliceAll()
        {
            SliceSheet(PlatformsPath, PlatformSlices, 100f);
            ConfigureBackdrop();
            AssetDatabase.Refresh();
            Debug.Log("[TG] User art sliced: " + string.Join(", ", PlatformSlices.Select(s => s.name)) + "; backdrop configured.");
        }

        static void SliceSheet(string path, Slice[] slices, float ppu)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) { Debug.LogError("[TG] Missing " + path); return; }
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Multiple;
            ti.spritePixelsPerUnit = ppu;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Bilinear;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(ti);
            provider.InitSpriteEditorDataProvider();
            var existing = provider.GetSpriteRects().ToList();
            var rects = slices.Select(s =>
            {
                var old = existing.FirstOrDefault(r => r.name == s.name);
                return new SpriteRect
                {
                    name = s.name,
                    rect = new Rect(s.rect.x, s.rect.y, s.rect.width, s.rect.height),
                    alignment = SpriteAlignment.Custom,
                    pivot = s.pivot,
                    spriteID = old != null ? old.spriteID : GUID.Generate(),
                };
            }).ToArray();
            provider.SetSpriteRects(rects);
            var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (names != null) names.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            provider.Apply();
            ti.SaveAndReimport();
        }

        static void ConfigureBackdrop()
        {
            if (!(AssetImporter.GetAtPath(BackdropPath) is TextureImporter ti)) return;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 100f;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Bilinear;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
        }
    }
}
