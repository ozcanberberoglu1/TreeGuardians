using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TreeGuardians.Editor
{
    /// Adds/selects fixed Game view resolutions so layouts can be checked on phone and tablet aspect ratios.
    public static class GameViewSizeUtility
    {
        public static bool Select(int width, int height, string label)
        {
            try
            {
                var asm = typeof(UnityEditor.Editor).Assembly;
                var sizesType = asm.GetType("UnityEditor.GameViewSizes");
                var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                var instance = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null, null);
                var group = sizesType.GetProperty("currentGroup", BindingFlags.Public | BindingFlags.Instance).GetValue(instance, null);
                var groupType = group.GetType();
                int total = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
                int found = -1;
                for (int i = 0; i < total; i++)
                {
                    var s = groupType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                    var name = (string)s.GetType().GetProperty("baseText").GetValue(s, null);
                    if (name == label) { found = i; break; }
                }
                if (found < 0)
                {
                    var gvsType = asm.GetType("UnityEditor.GameViewSize");
                    var enumType = asm.GetType("UnityEditor.GameViewSizeType");
                    var ctor = gvsType.GetConstructor(new[] { enumType, typeof(int), typeof(int), typeof(string) });
                    var size = ctor.Invoke(new object[] { Enum.Parse(enumType, "FixedResolution"), width, height, label });
                    groupType.GetMethod("AddCustomSize").Invoke(group, new[] { size });
                    found = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null) - 1;
                }
                var gameViewType = asm.GetType("UnityEditor.GameView");
                var gv = EditorWindow.GetWindow(gameViewType);
                var prop = gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                prop.SetValue(gv, found, null);
                gv.Repaint();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[TG] GameViewSizeUtility failed: " + e.Message);
                return false;
            }
        }

        [MenuItem("Tree Guardians/Game View/Phone 19.5:9 (2340x1080)", priority = 60)] static void Phone195() => Select(2340, 1080, "TG Phone 19.5:9");
        [MenuItem("Tree Guardians/Game View/Phone 16:9 (1920x1080)", priority = 61)] static void Phone169() => Select(1920, 1080, "TG Phone 16:9");
        [MenuItem("Tree Guardians/Game View/Tablet 4:3 (2048x1536)", priority = 62)] static void Tablet43() => Select(2048, 1536, "TG Tablet 4:3");
    }
}
