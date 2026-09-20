namespace TreeGuardians.Editor
{
    /// Central sprite path registry shared by the art generator, content builder and scene builder.
    public static class ArtPaths
    {
        public const string Root = "Assets/TreeGuardians/Art";
        public const string UI = Root + "/UI";
        public const string Icons = Root + "/UI/Icons";
        public const string Rarity = Root + "/UI/Rarity";
        public const string Chests = Root + "/UI/Chests";
        public const string Characters = Root + "/Characters";
        public const string Trees = Root + "/Trees";
        public const string Arenas = Root + "/Arenas";
        public const string Projectiles = Root + "/VFX/Projectiles";
        public const string VFX = Root + "/VFX";
        public const string Tools = Root + "/UI/Tools";
        public const string Logos = Root + "/UI/Logos";

        public static string Ui(string name) => $"{UI}/ui_{name}.png";
        public static string Icon(string name) => $"{Icons}/icon_{name}.png";
        public static string RarityIcon(string name) => $"{Rarity}/rarity_{name}.png";
        public static string Chest(string id, bool open) => $"{Chests}/chest_{id}_{(open ? "open" : "closed")}.png";
        public static string Portrait(string id) => $"{Characters}/{id}_portrait.png";
        public static string WorldSprite(string id) => $"{Characters}/{id}_world.png";
        public static string Tree(string part, int state) => $"{Trees}/tree_{part}_{state}.png";
        public static string TreePart(string part) => $"{Trees}/tree_{part}.png";
        public static string Arena(string layer) => $"{Arenas}/arena_{layer}.png";
        public static string Projectile(string id) => $"{Projectiles}/proj_{id}.png";
        public static string Vfx(string name) => $"{VFX}/vfx_{name}.png";
        public static string Tool(string id) => $"{Tools}/tool_{id}.png";
        public static string Logo(string name) => $"{Logos}/logo_{name}.png";
    }
}
