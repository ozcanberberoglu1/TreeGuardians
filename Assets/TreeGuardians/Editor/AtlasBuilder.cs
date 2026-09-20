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
            ok += Build("Atlas_UI", new[] { ArtPaths.UI }) ? 1 : 0;
            ok += Build("Atlas_Characters", new[] { ArtPaths.Characters }) ? 1 : 0;
            ok += Build("Atlas_Trees", new[] { ArtPaths.Trees }) ? 1 : 0;
            ok += Build("Atlas_Arenas", new[] { ArtPaths.Arenas }) ? 1 : 0;
            ok += Build("Atlas_VFX", new[] { ArtPaths.VFX }) ? 1 : 0;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[TG] Sprite atlases built: {ok}/5 (packer mode: {EditorSettings.spritePackerMode}).");
        }

        static bool Build(string name, string[] folders)
        {
            try
            {
                var objects = new UnityEngine.Object[folders.Length];
                for (int i = 0; i < folders.Length; i++)
                {
                    objects[i] = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folders[i]);
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
                    packing.padding = 4;
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
