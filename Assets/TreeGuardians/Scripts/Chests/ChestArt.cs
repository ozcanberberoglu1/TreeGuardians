using TreeGuardians.Data;
using UnityEngine;

namespace TreeGuardians.Chests
{
    /// Single lookup for chest sprites: the scene-authored provider (ChestSlotsView) wins, the ChestDefinition icons are the fallback.
    public static class ChestArt
    {
        public static IChestArtProvider Provider { get; set; }

        public static Sprite Closed(ChestDefinition chest) => Resolve(chest, false);
        public static Sprite Open(ChestDefinition chest) => Resolve(chest, true);

        public static Sprite Resolve(ChestDefinition chest, bool open)
        {
            if (chest == null) return null;
            var provider = Provider;
            if (provider != null)
            {
                var provided = provider.GetChestSprite(chest, open);
                if (provided != null) return provided;
            }
            return open ? chest.iconOpen : chest.iconClosed;
        }
    }
}
