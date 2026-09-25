using System;
using UnityEngine;

namespace TreeGuardians.UI.Menu
{
    /// One compartment of the main-menu tree: the part sprite that turns into a silhouette and the spawn point inside it.
    [Serializable]
    public sealed class TreePartSlot
    {
        [Tooltip("Bölmenin sprite'ı (silüet moduna girer).")] public SpriteRenderer part;
        [Tooltip("Bölmedeki muhafız yerleşim noktası.")] public GuardianSpawnPoint spawnPoint;
    }
}
