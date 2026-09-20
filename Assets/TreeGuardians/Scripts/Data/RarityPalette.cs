using UnityEngine;

namespace TreeGuardians.Data
{
    [CreateAssetMenu(menuName = "Tree Guardians/UI/Rarity Palette", fileName = "RarityPalette")]
    public sealed class RarityPalette : ScriptableObject
    {
        [Tooltip("Common, Rare, Epic, Legendary çerçeve renkleri.")]
        public Color[] frameColors =
        {
            new Color(0.62f, 0.66f, 0.62f),
            new Color(0.27f, 0.6f, 0.95f),
            new Color(0.63f, 0.36f, 0.9f),
            new Color(0.98f, 0.72f, 0.2f)
        };
        [Tooltip("Common, Rare, Epic, Legendary köşe simgeleri (renk tek gösterge olmasın).")]
        public Sprite[] cornerIcons = new Sprite[4];
        public string[] nameKeys = { "rarity_common", "rarity_rare", "rarity_epic", "rarity_legendary" };

        public Color GetColor(Rarity r)
        {
            int i = (int)r;
            return frameColors != null && i < frameColors.Length ? frameColors[i] : Color.white;
        }

        public Sprite GetIcon(Rarity r)
        {
            int i = (int)r;
            return cornerIcons != null && i < cornerIcons.Length ? cornerIcons[i] : null;
        }

        public string GetNameKey(Rarity r)
        {
            int i = (int)r;
            return nameKeys != null && i < nameKeys.Length ? nameKeys[i] : "rarity_common";
        }
    }
}
