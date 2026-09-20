using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace TreeGuardians.Editor
{
    /// Imports TMP essentials (if missing) and builds a dynamic SDF font asset that includes Turkish glyphs.
    public static class FontBuilder
    {
        const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        const string LiberationTtf = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";
        const string TurkishChars = "ABCÇDEFGĞHIİJKLMNOÖPRSŞTUÜVYZQWXabcçdefgğhıijklmnoöprsştuüvyzqwx0123456789 .,:;!?%()[]{}-+*/=_'\"<>@#&€$₺ışİÇĞÖŞÜ";

        [MenuItem("Tree Guardians/0a. Import TMP Essentials", priority = 0)]
        public static void ImportMenu() => ImportEssentials();

        [MenuItem("Tree Guardians/0b. Build TG Font", priority = 1)]
        public static void BuildFontMenu() => BuildFont();

        public static bool EssentialsImported => File.Exists(TmpSettingsPath);

        public static void ImportEssentials()
        {
            if (EssentialsImported) { Debug.Log("[TG] TMP essentials already imported."); return; }
            string packageFolder = Path.GetFullPath("Packages/com.unity.ugui");
            string pkg = Path.Combine(packageFolder, "Package Resources", "TMP Essential Resources.unitypackage");
            if (!File.Exists(pkg))
            {
                packageFolder = Path.GetFullPath("Packages/com.unity.textmeshpro");
                pkg = Path.Combine(packageFolder, "Package Resources", "TMP Essential Resources.unitypackage");
            }
            if (!File.Exists(pkg))
            {
                Debug.LogError("[TG] TMP Essential Resources package not found at " + pkg);
                return;
            }
            AssetDatabase.ImportPackage(pkg, false);
            AssetDatabase.Refresh();
            Debug.Log("[TG] TMP essentials imported.");
        }

        public static TMP_FontAsset BuildFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UIFactory.FontPath);
            if (existing != null)
            {
                existing.TryAddCharacters(TurkishChars);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                Debug.Log("[TG] Font asset already exists; ensured Turkish glyphs.");
                return existing;
            }

            var ttf = AssetDatabase.LoadAssetAtPath<Font>(LiberationTtf);
            if (ttf == null)
            {
                Debug.LogError("[TG] LiberationSans.ttf not found; import TMP essentials first.");
                return null;
            }

            var fontAsset = TMP_FontAsset.CreateFontAsset(ttf, 72, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (fontAsset == null)
            {
                Debug.LogError("[TG] Could not create font asset.");
                return null;
            }
            fontAsset.name = "TG_Main SDF";
            ContentBuilder.EnsureFolder(Path.GetDirectoryName(UIFactory.FontPath).Replace('\\', '/'));
            AssetDatabase.CreateAsset(fontAsset, UIFactory.FontPath);
            if (fontAsset.atlasTextures != null)
                for (int i = 0; i < fontAsset.atlasTextures.Length; i++)
                {
                    var tex = fontAsset.atlasTextures[i];
                    if (tex == null) continue;
                    tex.name = "TG_Main Atlas " + i;
                    AssetDatabase.AddObjectToAsset(tex, fontAsset);
                }
            if (fontAsset.material != null)
            {
                fontAsset.material.name = "TG_Main Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }
            fontAsset.TryAddCharacters(TurkishChars);
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(UIFactory.FontPath);
            Debug.Log("[TG] Font asset built: " + UIFactory.FontPath);
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UIFactory.FontPath);
        }
    }
}
