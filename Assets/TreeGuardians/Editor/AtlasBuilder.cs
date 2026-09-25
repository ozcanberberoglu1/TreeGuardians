using System;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace TreeGuardians.Editor
{
    /// Creates sprite atlases per art folder so UI and arena layers batch on mobile.
    public static class AtlasBuilder
    {
        const string Folder = "Assets/TreeGuardians/Art/Atlases";

        [MenuItem("Tree Guardians/5. Build Sprite Atlases", priority = 18)]
        public static void BuildAll()
        {
            ContentBuilder.EnsureFolder(Folder);
            int ok = 0;
            ok += Build("Atlas_UI", UiPackables()) ? 1 : 0;
            ok += Build("Atlas_Characters", new[] { ArtPaths.Characters }) ? 1 : 0;
            ok += Build("Atlas_Trees", new[] { ArtPaths.Trees }) ? 1 : 0;
            ok += Build("Atlas_Arenas", new[] { ArtPaths.Arenas }) ? 1 : 0;
            ok += Build("Atlas_VFX", new[] { ArtPaths.VFX }) ? 1 : 0;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ReimportUnpackedSheets();
            Debug.Log($"[TG] Sprite atlases built: {ok}/5 (packer mode: {EditorSettings.spritePackerMode}).");
        }

        /// Textures that are no longer in any atlas (Art/UI/Panel) can keep a stale atlas binding from an earlier pack:
        /// in Play Mode their sprites then report texture == null and every UI Image using them draws white.
        /// A forced reimport + repack clears that binding.
        [MenuItem("Tree Guardians/5b. Repair White UI Sprites (reimport unpacked sheets)", priority = 20)]
        public static void ReimportUnpackedSheets()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtPaths.UI + "/Panel" }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            SpriteAtlasUtility.PackAllAtlases(EditorUserBuildSettings.activeBuildTarget, false);
            Debug.Log("[TG] Unpacked UI sheets reimported and atlases repacked.");
        }

        /// Everything under Art/UI except Art/UI/Panel: the user's hand-made 2048² sheets (menu atlas, backgrounds, castle)
        /// stay as their own textures instead of being re-packed (and downscaled/rotated) into the UI atlas.
        static string[] UiPackables()
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (var dir in System.IO.Directory.GetDirectories(ArtPaths.UI))
            {
                var p = dir.Replace('\\', '/');
                if (p.EndsWith("/Panel")) continue;
                list.Add(p);
            }
            foreach (var file in System.IO.Directory.GetFiles(ArtPaths.UI, "*.png")) list.Add(file.Replace('\\', '/'));
            return list.ToArray();
        }

        /// UI Images draw full quads: tight packing lets neighbouring sprites bleed in and UI cannot draw rotated sprites.
        static SpriteAtlasPackingSettings SafePacking => new SpriteAtlasPackingSettings { enableTightPacking = false, enableRotation = false, padding = 8, blockOffset = 1 };

        [MenuItem("Tree Guardians/5a. Fix Atlas Packing (no tight/rotation, pad 8)", priority = 19)]
        public static void FixPackingOnly()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:SpriteAtlas", new[] { Folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is SpriteAtlasImporter imp) { imp.packingSettings = SafePacking; imp.SaveAndReimport(); }
            }
            ReimportUnpackedSheets();
            Debug.Log("[TG] Atlas packing settings fixed.");
        }

        static bool Build(string name, string[] folders)
        {
            try
            {
                var objects = new UnityEngine.Object[folders.Length];
                for (int i = 0; i < folders.Length; i++)
                {
                    objects[i] = folders[i].EndsWith(".png") ? (UnityEngine.Object)AssetDatabase.LoadAssetAtPath<Texture2D>(folders[i]) : AssetDatabase.LoadAssetAtPath<DefaultAsset>(folders[i]);
                    if (objects[i] == null) { Debug.LogWarning("[TG] Atlas folder missing: " + folders[i]); return false; }
                }
                bool v2 = EditorSettings.spritePackerMode == SpritePackerMode.SpriteAtlasV2 || EditorSettings.spritePackerMode == SpritePackerMode.SpriteAtlasV2Build;
                if (v2)
                {
                    string path = $"{Folder}/{name}.spriteatlasv2";
                    var asset = new SpriteAtlasAsset();
                    asset.Add(objects);
                    SpriteAtlasAsset.Save(asset, path);
                    AssetDatabase.ImportAsset(path);
                    if (AssetImporter.GetAtPath(path) is SpriteAtlasImporter imp) { imp.packingSettings = SafePacking; imp.SaveAndReimport(); }
                }
                else
                {
                    string path = $"{Folder}/{name}.spriteatlas";
                    var existing = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
                    var atlas = existing != null ? existing : new SpriteAtlas();
                    if (existing == null) AssetDatabase.CreateAsset(atlas, path);
                    atlas.Remove(atlas.GetPackables());
                    atlas.Add(objects);
                    var packing = atlas.GetPackingSettings();
                    packing.enableTightPacking = false;
                    packing.enableRotation = false;
                    packing.padding = 8;
                    atlas.SetPackingSettings(packing);
                    EditorUtility.SetDirty(atlas);
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TG] Atlas '{name}' failed: {e.Message}");
                return false;
            }
        }
    }
}
