using TreeGuardians.Data;
using UnityEngine;

namespace TreeGuardians.Chests
{
    /// Supplies chest artwork (closed / open) for a chest definition. Authored in the menu scene by ChestSlotsView.
    public interface IChestArtProvider
    {
        Sprite GetChestSprite(ChestDefinition chest, bool open);
    }
}
