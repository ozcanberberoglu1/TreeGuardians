using System;
using TreeGuardians.Data;
using UnityEngine;

namespace TreeGuardians.UI.Menu
{
    /// One chest type's artwork, assigned by hand on ChestSlotsView (MainMenu > BottomBar > ChestSlots).
    [Serializable]
    public sealed class ChestVisualEntry
    {
        [Tooltip("Sandık tanımı (ScriptableObjects/Chests).")] public ChestDefinition chest;
        [Tooltip("Kapalı / varsayılan görsel.")] public Sprite closedSprite;
        [Tooltip("Açılmış görsel (sandık hazır olduğunda ve açılış popup'ında).")] public Sprite openSprite;
    }
}
