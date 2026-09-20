using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TreeGuardians.Editor
{
    /// Generates clean, original placeholder sprites (white/gray masks tinted at runtime, dark outlines).
    public static class PlaceholderArtGenerator
    {
        public static readonly Color Outline = new Color(0.11f, 0.13f, 0.18f, 1f);
        static readonly Color White = Color.white;
        static readonly Color Light = new Color(0.86f, 0.86f, 0.86f, 1f);
        static readonly Color Mid = new Color(0.72f, 0.72f, 0.72f, 1f);
        static readonly Color Amber = new Color(1f, 0.76f, 0.3f, 1f);

        static int step;
        static int total;

        [MenuItem("Tree Guardians/1. Generate Placeholder Art", priority = 1)]
        public static void GenerateAll()
        {
            step = 0;
            total = 130;
            try
            {
                GenerateUI();
                GenerateIcons();
                GenerateRarity();
                GenerateChests();
                GenerateTrees();
                GenerateArenas();
                GenerateProjectiles();
                GenerateTools();
                GenerateLogos();
                PlaceholderCharacterArt.GenerateAll(Progress);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[TG] Placeholder art generated.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public static void Progress(string label)
        {
            step++;
            EditorUtility.DisplayProgressBar("Tree Guardians", "Generating " + label, Mathf.Clamp01(step / (float)total));
        }

        public static void PolygonOutlined(PixelCanvas c, Vector2[] pts, Color fill, Color outline, float thickness)
        {
            Vector2 centroid = Vector2.zero;
            foreach (var p in pts) centroid += p;
            centroid /= pts.Length;
            float avg = 0f;
            foreach (var p in pts) avg += (p - centroid).magnitude;
            avg /= pts.Length;
            var expanded = new Vector2[pts.Length];
            float k = 1f + thickness / Mathf.Max(1f, avg);
            for (int i = 0; i < pts.Length; i++) expanded[i] = centroid + (pts[i] - centroid) * k;
            c.Polygon(expanded, outline);
            c.Polygon(pts, fill);
        }

        static Vector2[] P(params float[] xy)
        {
            var arr = new Vector2[xy.Length / 2];
            for (int i = 0; i < arr.Length; i++) arr[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            return arr;
        }

        static void Cracks(PixelCanvas c, int seed, int count, float minLen, float maxLen, float thickness)
        {
            var rng = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                float x = (float)rng.NextDouble() * c.Width * 0.7f + c.Width * 0.15f;
                float y = (float)rng.NextDouble() * c.Height * 0.7f + c.Height * 0.15f;
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float len = Mathf.Lerp(minLen, maxLen, (float)rng.NextDouble());
                Vector2 a = new Vector2(x, y);
                Vector2 b = a + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * len;
                Vector2 m = (a + b) * 0.5f + new Vector2(-Mathf.Sin(ang), Mathf.Cos(ang)) * len * 0.18f;
                c.Capsule(a, m, thickness, Outline);
                c.Capsule(m, b, thickness * 0.8f, Outline);
            }
        }

        // ------------------------------------------------------------------ UI
        static void GenerateUI()
        {
            Progress("UI"); var c = new PixelCanvas(64, 64);
            c.RoundedRectOutlined(4, 4, 56, 56, 13, White, Outline, 3);
            c.SaveSprite(ArtPaths.Ui("panel"), 100, new Vector4(20, 20, 20, 20));

            Progress("UI"); c = new PixelCanvas(64, 64);
            c.RoundedRectOutlined(4, 4, 56, 56, 22, White, Outline, 3);
            c.SaveSprite(ArtPaths.Ui("button"), 100, new Vector4(28, 28, 28, 28));

            Progress("UI"); c = new PixelCanvas(64, 64);
            c.RoundedRect(0, 0, 64, 64, 24, White);
            c.SaveSprite(ArtPaths.Ui("button_flat"), 100, new Vector4(28, 28, 28, 28));

            Progress("UI"); c = new PixelCanvas(96, 96);
            c.RoundedRectOutlined(5, 5, 86, 86, 14, White, Outline, 4);
            c.SaveSprite(ArtPaths.Ui("card"), 100, new Vector4(26, 26, 26, 26));

            Progress("UI"); c = new PixelCanvas(64, 64);
            c.RoundedRect(0, 0, 64, 64, 16, White);
            c.SaveSprite(ArtPaths.Ui("soft"), 100, new Vector4(20, 20, 20, 20));

            Progress("UI"); c = new PixelCanvas(64, 32);
            c.RoundedRectOutlined(3, 3, 58, 26, 12, White, Outline, 2);
            c.SaveSprite(ArtPaths.Ui("bar_back"), 100, new Vector4(18, 14, 18, 14));

            Progress("UI"); c = new PixelCanvas(64, 32);
            c.RoundedRect(2, 2, 60, 28, 12, White);
            c.SaveSprite(ArtPaths.Ui("bar_fill"), 100, new Vector4(16, 14, 16, 14));

            Progress("UI"); c = new PixelCanvas(128, 128);
            c.CircleOutlined(64, 64, 58, White, Outline, 4);
            c.SaveSprite(ArtPaths.Ui("circle"));

            Progress("UI"); c = new PixelCanvas(128, 128);
            c.Circle(64, 64, 62, White);
            c.SaveSprite(ArtPaths.Ui("circle_flat"));

            Progress("UI"); c = new PixelCanvas(128, 128);
            c.RadialGlow(64, 64, 64, White, 2.2f);
            c.SaveSprite(ArtPaths.Ui("glow"));

            Progress("UI"); c = new PixelCanvas(128, 128);
            c.RingOutlined(64, 64, 58, 12, White, Outline, 3);
            c.SaveSprite(ArtPaths.Ui("ring"));

            Progress("UI"); c = new PixelCanvas(128, 128);
            c.RoundedRectOutlined(6, 6, 116, 116, 18, Mid, Outline, 4);
            c.RoundedRect(18, 18, 92, 92, 12, new Color(0.55f, 0.55f, 0.55f, 0.6f));
            c.SaveSprite(ArtPaths.Ui("slot"), 100, new Vector4(30, 30, 30, 30));

            Progress("UI"); c = new PixelCanvas(64, 48);
            c.RoundedRectOutlined(3, -20, 58, 65, 14, White, Outline, 3);
            c.SaveSprite(ArtPaths.Ui("tab"), 100, new Vector4(18, 8, 18, 18));

            Progress("UI"); c = new PixelCanvas(64, 64);
            c.RoundedRect(6, 2, 52, 52, 16, new Color(0f, 0f, 0f, 0.35f));
            c.SaveSprite(ArtPaths.Ui("shadow"), 100, new Vector4(22, 22, 22, 22));

            Progress("UI"); c = new PixelCanvas(4, 4);
            c.RoundedRect(0, 0, 4, 4, 0, White);
            c.SaveSprite(ArtPaths.Ui("white"));

            Progress("UI"); c = new PixelCanvas(32, 32);
            c.CircleOutlined(16, 16, 12, White, Outline, 2);
            c.SaveSprite(ArtPaths.Ui("dot"));

            Progress("UI"); c = new PixelCanvas(128, 40);
            c.Capsule(new Vector2(20, 20), new Vector2(108, 20), 12, Outline);
            c.Capsule(new Vector2(20, 20), new Vector2(108, 20), 9, White);
            c.SaveSprite(ArtPaths.Ui("pill"), 100, new Vector4(24, 0, 24, 0));
        }

        // ------------------------------------------------------------------ Icons
        static void Icon(string name, System.Action<PixelCanvas> draw, int size = 128)
        {
            Progress("icon " + name);
            var c = new PixelCanvas(size, size);
            draw(c);
            c.SaveSprite(ArtPaths.Icon(name));
        }

        static void Shield(PixelCanvas c, float cx, float cy, float s, Color fill)
        {
            PolygonOutlined(c, P(cx - s, cy + s * 0.8f, cx + s, cy + s * 0.8f, cx + s, cy - s * 0.1f, cx, cy - s, cx - s, cy - s * 0.1f), fill, Outline, 5);
        }

        static void GenerateIcons()
        {
            Icon("coin", c => { c.CircleOutlined(64, 64, 52, White, Outline, 5); c.Circle(64, 64, 38, Outline); c.Circle(64, 64, 32, White); c.Capsule(new Vector2(64, 46), new Vector2(64, 82), 6, Outline); });
            Icon("sap", c => { c.Triangle(new Vector2(20, 66), new Vector2(108, 66), new Vector2(64, 122), Outline); c.Circle(64, 56, 46, Outline); c.Triangle(new Vector2(27, 66), new Vector2(101, 66), new Vector2(64, 112), White); c.Circle(64, 56, 40, White); c.Circle(48, 70, 8, Light); });
            Icon("gem", c => PolygonOutlined(c, P(64, 116, 20, 66, 38, 28, 90, 28, 108, 66), White, Outline, 5));
            Icon("trophy", c => { c.Ring(28, 78, 22, 9, Outline); c.Ring(100, 78, 22, 9, Outline); c.RoundedRectOutlined(34, 52, 60, 62, 12, White, Outline, 5); c.Capsule(new Vector2(64, 50), new Vector2(64, 28), 8, Outline); c.RoundedRectOutlined(34, 10, 60, 18, 6, White, Outline, 4); });
            Icon("settings", c => { for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4f; c.Capsule(new Vector2(64, 64), new Vector2(64 + Mathf.Cos(a) * 52, 64 + Mathf.Sin(a) * 52), 13, Outline); } c.Circle(64, 64, 42, Outline); for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4f; c.Capsule(new Vector2(64, 64), new Vector2(64 + Mathf.Cos(a) * 48, 64 + Mathf.Sin(a) * 48), 9, White); } c.Circle(64, 64, 36, White); c.Circle(64, 64, 16, Outline); c.Circle(64, 64, 11, Light); });
            Icon("leaf", c => { c.Leaf(64, 64, 112, 62, 45, Outline); c.Leaf(64, 64, 100, 50, 45, White); c.Capsule(new Vector2(30, 30), new Vector2(98, 98), 3, Outline); });
            Icon("star", c => { c.Star(64, 64, 58, 26, 5, Outline); c.Star(64, 64, 48, 20, 5, White); });
            Icon("lock", c => { c.Ring(64, 78, 30, 10, Outline); c.RoundedRectOutlined(24, 12, 80, 62, 12, White, Outline, 5); c.Circle(64, 44, 9, Outline); });
            Icon("arrow_up", c => PolygonOutlined(c, P(64, 116, 16, 60, 42, 60, 42, 12, 86, 12, 86, 60, 112, 60), White, Outline, 5));
            Icon("battle", c => { c.Capsule(new Vector2(24, 24), new Vector2(104, 104), 11, Outline); c.Capsule(new Vector2(104, 24), new Vector2(24, 104), 11, Outline); c.Capsule(new Vector2(30, 30), new Vector2(98, 98), 7, White); c.Capsule(new Vector2(98, 30), new Vector2(30, 98), 7, White); c.Circle(64, 64, 14, Outline); c.Circle(64, 64, 9, Amber); });
            Icon("rank", c => { c.RoundedRectOutlined(8, 12, 36, 52, 6, White, Outline, 4); c.RoundedRectOutlined(46, 12, 36, 84, 6, White, Outline, 4); c.RoundedRectOutlined(84, 12, 36, 36, 6, White, Outline, 4); c.Star(64, 74, 12, 5, 5, Outline); });
            Icon("quest", c => { c.RoundedRectOutlined(24, 12, 80, 104, 10, White, Outline, 5); for (int i = 0; i < 4; i++) c.Capsule(new Vector2(40, 96 - i * 20), new Vector2(88, 96 - i * 20), 4, Outline); });
            Icon("shop", c => { c.Ring(64, 90, 22, 8, Outline); c.RoundedRectOutlined(20, 12, 88, 76, 12, White, Outline, 5); c.Capsule(new Vector2(20, 76), new Vector2(108, 76), 3, Outline); });
            Icon("season", c => { c.RoundedRectOutlined(14, 10, 100, 100, 12, White, Outline, 5); c.RoundedRect(14, 84, 100, 26, 8, Outline); c.RoundedRect(14, 84, 100, 12, 0, Outline); for (int i = 0; i < 3; i++) for (int j = 0; j < 2; j++) c.Circle(36 + i * 28, 38 + j * 26, 7, Outline); });
            Icon("events", c => { c.Polygon(P(24, 40, 32, 80, 44, 104, 84, 104, 96, 80, 104, 40), Outline); c.Polygon(P(31, 44, 38, 78, 48, 98, 80, 98, 90, 78, 97, 44), White); c.Capsule(new Vector2(20, 40), new Vector2(108, 40), 6, Outline); c.Circle(64, 26, 12, Outline); c.Circle(64, 26, 7, White); c.Capsule(new Vector2(64, 104), new Vector2(64, 116), 6, Outline); });
            Icon("inbox", c => { c.RoundedRectOutlined(12, 24, 104, 76, 10, White, Outline, 5); c.Capsule(new Vector2(14, 96), new Vector2(64, 58), 4, Outline); c.Capsule(new Vector2(114, 96), new Vector2(64, 58), 4, Outline); });
            Icon("profile", c => { c.Ellipse(64, 30, 46, 30, Outline); c.Ellipse(64, 30, 40, 24, White); c.CircleOutlined(64, 84, 28, White, Outline, 5); });
            Icon("tree", c => { c.RoundedRectOutlined(52, 8, 24, 60, 6, Mid, Outline, 4); c.Circle(64, 84, 40, Outline); c.Circle(38, 66, 26, Outline); c.Circle(90, 66, 26, Outline); c.Circle(64, 84, 34, White); c.Circle(38, 66, 20, White); c.Circle(90, 66, 20, White); });
            Icon("guardians", c => { Shield(c, 64, 62, 50, White); c.Leaf(64, 62, 56, 30, 60, Outline); c.Leaf(64, 62, 46, 22, 60, Light); });
            Icon("tools", c => { c.Capsule(new Vector2(30, 30), new Vector2(84, 84), 13, Outline); c.Capsule(new Vector2(34, 34), new Vector2(80, 80), 8, White); c.Circle(94, 94, 26, Outline); c.Circle(94, 94, 20, White); c.EraseCircle(104, 104, 12); c.Circle(104, 104, 11, Outline); c.EraseCircle(104, 104, 8); });
            Icon("home", c => { PolygonOutlined(c, P(12, 62, 64, 112, 116, 62), White, Outline, 5); c.RoundedRectOutlined(26, 12, 76, 56, 6, White, Outline, 5); c.RoundedRect(54, 12, 20, 30, 4, Outline); });
            Icon("pause", c => { c.RoundedRectOutlined(24, 18, 28, 92, 8, White, Outline, 5); c.RoundedRectOutlined(76, 18, 28, 92, 8, White, Outline, 5); });
            Icon("play", c => PolygonOutlined(c, P(30, 14, 30, 114, 110, 64), White, Outline, 6));
            Icon("retry", c => { c.Ring(64, 64, 46, 14, Outline); c.Ring(64, 64, 42, 6, White); c.Polygon(P(64, 14, 64, 44, 100, 29), Outline); c.EraseCircle(88, 64, 22); c.Polygon(P(66, 10, 66, 48, 106, 29), Outline); c.Polygon(P(70, 17, 70, 41, 96, 29), White); });
            Icon("close", c => { c.Capsule(new Vector2(28, 28), new Vector2(100, 100), 13, Outline); c.Capsule(new Vector2(100, 28), new Vector2(28, 100), 13, Outline); c.Capsule(new Vector2(32, 32), new Vector2(96, 96), 8, White); c.Capsule(new Vector2(96, 32), new Vector2(32, 96), 8, White); });
            Icon("check", c => { c.Capsule(new Vector2(22, 66), new Vector2(52, 34), 14, Outline); c.Capsule(new Vector2(52, 34), new Vector2(108, 94), 14, Outline); c.Capsule(new Vector2(26, 66), new Vector2(52, 40), 8, White); c.Capsule(new Vector2(52, 40), new Vector2(104, 92), 8, White); });
            Icon("info", c => { c.CircleOutlined(64, 64, 54, White, Outline, 5); c.Circle(64, 88, 9, Outline); c.RoundedRect(56, 26, 16, 46, 6, Outline); });
            Icon("plus", c => { c.RoundedRectOutlined(50, 14, 28, 100, 8, White, Outline, 5); c.RoundedRectOutlined(14, 50, 100, 28, 8, White, Outline, 5); c.RoundedRect(50, 50, 28, 28, 0, White); });
            Icon("cards", c => { c.RoundedRectOutlined(14, 22, 62, 84, 10, Light, Outline, 5); c.RoundedRectOutlined(46, 12, 66, 92, 10, White, Outline, 5); c.Star(79, 58, 16, 7, 5, Outline); });
            Icon("sword", c => { c.Capsule(new Vector2(34, 34), new Vector2(102, 102), 11, Outline); c.Capsule(new Vector2(40, 40), new Vector2(98, 98), 6, White); c.Capsule(new Vector2(20, 54), new Vector2(54, 20), 9, Outline); c.Circle(22, 22, 12, Outline); c.Circle(22, 22, 7, Amber); });
            Icon("heart", c => { c.Circle(42, 78, 30, Outline); c.Circle(86, 78, 30, Outline); c.Triangle(new Vector2(14, 68), new Vector2(114, 68), new Vector2(64, 12), Outline); c.Circle(42, 78, 24, White); c.Circle(86, 78, 24, White); c.Triangle(new Vector2(22, 68), new Vector2(106, 68), new Vector2(64, 22), White); });
            Icon("shield", c => Shield(c, 64, 62, 52, White));
            Icon("bolt", c => PolygonOutlined(c, P(72, 118, 30, 62, 58, 62, 50, 10, 98, 70, 68, 70), White, Outline, 5));
            Icon("target", c => { c.RingOutlined(64, 64, 56, 12, White, Outline, 3); c.RingOutlined(64, 64, 32, 10, White, Outline, 3); c.Circle(64, 64, 10, Outline); });
            Icon("clock", c => { c.CircleOutlined(64, 64, 54, White, Outline, 5); c.Capsule(new Vector2(64, 64), new Vector2(64, 96), 5, Outline); c.Capsule(new Vector2(64, 64), new Vector2(86, 52), 5, Outline); c.Circle(64, 64, 7, Outline); });
            Icon("language", c => { c.CircleOutlined(64, 64, 54, White, Outline, 5); c.EllipseOutlined(64, 64, 22, 52, White, Outline, 4); c.Capsule(new Vector2(12, 64), new Vector2(116, 64), 3, Outline); c.Capsule(new Vector2(20, 40), new Vector2(108, 40), 3, Outline); c.Capsule(new Vector2(20, 88), new Vector2(108, 88), 3, Outline); });
            Icon("music", c => { c.CircleOutlined(40, 34, 22, White, Outline, 5); c.CircleOutlined(90, 44, 22, White, Outline, 5); c.Capsule(new Vector2(58, 36), new Vector2(58, 106), 6, Outline); c.Capsule(new Vector2(108, 46), new Vector2(108, 116), 6, Outline); c.Capsule(new Vector2(58, 106), new Vector2(108, 116), 7, Outline); });
            Icon("sfx", c => { PolygonOutlined(c, P(18, 46, 44, 46, 76, 18, 76, 110, 44, 82, 18, 82), White, Outline, 5); c.Ring(76, 64, 30, 6, Outline); c.EraseCircle(56, 64, 30); c.Ring(76, 64, 48, 6, Outline); c.EraseCircle(50, 64, 42); });
            Icon("vibration", c => { c.RoundedRectOutlined(40, 10, 48, 108, 12, White, Outline, 5); c.Capsule(new Vector2(22, 44), new Vector2(22, 84), 5, Outline); c.Capsule(new Vector2(106, 44), new Vector2(106, 84), 5, Outline); c.Capsule(new Vector2(8, 52), new Vector2(8, 76), 4, Outline); c.Capsule(new Vector2(120, 52), new Vector2(120, 76), 4, Outline); });
            Icon("motion", c => { for (int i = 0; i < 3; i++) { float y = 32 + i * 30; c.Capsule(new Vector2(20, y), new Vector2(50, y + 14), 6, Outline); c.Capsule(new Vector2(50, y + 14), new Vector2(80, y), 6, Outline); c.Capsule(new Vector2(80, y), new Vector2(108, y + 14), 6, Outline); } });
            Icon("flag", c => { c.Capsule(new Vector2(30, 10), new Vector2(30, 118), 7, Outline); PolygonOutlined(c, P(36, 116, 108, 92, 36, 68), White, Outline, 5); });
            Icon("key", c => { c.RingOutlined(42, 84, 30, 12, White, Outline, 4); c.Capsule(new Vector2(62, 66), new Vector2(112, 16), 10, Outline); c.Capsule(new Vector2(64, 64), new Vector2(108, 20), 5, White); c.Capsule(new Vector2(98, 30), new Vector2(112, 44), 7, Outline); c.Capsule(new Vector2(86, 42), new Vector2(98, 54), 7, Outline); });
            Icon("level", c => { c.Star(64, 64, 60, 30, 8, Outline); c.Star(64, 64, 50, 25, 8, White); c.Circle(64, 64, 26, Outline); c.Circle(64, 64, 21, Amber); });
            Icon("energy", c => { c.CircleOutlined(64, 64, 54, White, Outline, 5); PolygonOutlined(c, P(70, 108, 36, 66, 60, 66, 54, 20, 94, 70, 70, 70), Amber, Outline, 4); });
            Icon("swap", c => { c.Capsule(new Vector2(24, 44), new Vector2(96, 44), 8, Outline); c.Polygon(P(90, 24, 116, 44, 90, 64), Outline); c.Capsule(new Vector2(104, 84), new Vector2(32, 84), 8, Outline); c.Polygon(P(38, 64, 12, 84, 38, 104), Outline); });
            Icon("branch", c => { c.Capsule(new Vector2(14, 40), new Vector2(114, 70), 12, Outline); c.Capsule(new Vector2(18, 40), new Vector2(110, 70), 7, Light); c.Capsule(new Vector2(70, 58), new Vector2(96, 100), 9, Outline); c.Capsule(new Vector2(72, 60), new Vector2(94, 96), 5, Light); c.Leaf(104, 104, 30, 16, 50, Outline); c.Leaf(104, 104, 24, 12, 50, White); });
            Icon("armor", c => { Shield(c, 64, 62, 52, White); c.Capsule(new Vector2(64, 24), new Vector2(64, 96), 5, Outline); c.Capsule(new Vector2(28, 62), new Vector2(100, 62), 5, Outline); });
            Icon("root", c => { c.Capsule(new Vector2(64, 118), new Vector2(64, 60), 12, Outline); c.Capsule(new Vector2(64, 70), new Vector2(24, 22), 9, Outline); c.Capsule(new Vector2(64, 70), new Vector2(104, 22), 9, Outline); c.Capsule(new Vector2(64, 46), new Vector2(40, 12), 7, Outline); c.Capsule(new Vector2(64, 46), new Vector2(88, 12), 7, Outline); c.Capsule(new Vector2(64, 116), new Vector2(64, 62), 7, Light); c.Capsule(new Vector2(64, 70), new Vector2(26, 24), 4, Light); c.Capsule(new Vector2(64, 70), new Vector2(102, 24), 4, Light); });
            Icon("canopy", c => { c.Circle(64, 70, 46, Outline); c.Circle(34, 52, 30, Outline); c.Circle(94, 52, 30, Outline); c.Circle(64, 70, 40, White); c.Circle(34, 52, 24, White); c.Circle(94, 52, 24, White); c.RoundedRect(56, 8, 16, 34, 5, Outline); });
            Icon("sapflow", c => { c.Circle(64, 56, 34, Outline); c.Triangle(new Vector2(30, 60), new Vector2(98, 60), new Vector2(64, 112), Outline); c.Circle(64, 56, 28, White); c.Triangle(new Vector2(36, 60), new Vector2(92, 60), new Vector2(64, 104), White); c.Capsule(new Vector2(20, 22), new Vector2(108, 22), 5, Outline); c.Capsule(new Vector2(30, 10), new Vector2(98, 10), 4, Outline); });
        }

        // ------------------------------------------------------------------ Rarity
        static void GenerateRarity()
        {
            Progress("rarity"); var c = new PixelCanvas(64, 64);
            c.CircleOutlined(32, 32, 24, White, Outline, 4);
            c.SaveSprite(ArtPaths.RarityIcon("common"));

            Progress("rarity"); c = new PixelCanvas(64, 64);
            PolygonOutlined(c, P(32, 6, 58, 32, 32, 58, 6, 32), White, Outline, 4);
            c.SaveSprite(ArtPaths.RarityIcon("rare"));

            Progress("rarity"); c = new PixelCanvas(64, 64);
            c.Star(32, 32, 30, 13, 5, Outline); c.Star(32, 32, 23, 10, 5, White);
            c.SaveSprite(ArtPaths.RarityIcon("epic"));

            Progress("rarity"); c = new PixelCanvas(64, 64);
            PolygonOutlined(c, P(8, 8, 8, 44, 20, 30, 32, 56, 44, 30, 56, 44, 56, 8), White, Outline, 4);
            c.SaveSprite(ArtPaths.RarityIcon("legendary"));
        }

        // ------------------------------------------------------------------ Chests
        static void GenerateChests()
        {
            var ids = new[] { "twig", "grove", "ancient", "moon" };
            var colors = new[] { new Color(0.62f, 0.44f, 0.26f), new Color(0.36f, 0.62f, 0.34f), new Color(0.54f, 0.36f, 0.76f), new Color(0.3f, 0.45f, 0.8f) };
            var glows = new[] { new Color(1f, 0.85f, 0.4f), new Color(0.6f, 1f, 0.5f), new Color(0.85f, 0.6f, 1f), new Color(0.6f, 0.8f, 1f) };
            for (int i = 0; i < ids.Length; i++)
            {
                var col = colors[i];
                var dark = col * 0.75f; dark.a = 1f;
                var light = Color.Lerp(col, Color.white, 0.25f);

                Progress("chest"); var c = new PixelCanvas(192, 160);
                c.RoundedRectOutlined(26, 16, 140, 84, 14, col, Outline, 5);
                c.RoundedRect(26, 16, 140, 26, 8, dark);
                c.RoundedRectOutlined(18, 90, 156, 46, 18, light, Outline, 5);
                c.Capsule(new Vector2(96, 20), new Vector2(96, 130), 5, Outline);
                c.Capsule(new Vector2(96, 22), new Vector2(96, 128), 2, Amber);
                c.CircleOutlined(96, 80, 16, Amber, Outline, 4);
                c.SaveSprite(ArtPaths.Chest(ids[i], false));

                Progress("chest"); c = new PixelCanvas(192, 160);
                c.RadialGlow(96, 110, 80, glows[i], 1.6f);
                c.RoundedRectOutlined(26, 8, 140, 76, 14, col, Outline, 5);
                c.RoundedRect(26, 8, 140, 26, 8, dark);
                c.RoundedRectOutlined(30, 60, 132, 22, 8, Amber, Outline, 4);
                c.RoundedRectOutlined(18, 116, 156, 38, 16, light, Outline, 5);
                c.Star(60, 100, 10, 4, 4, Color.white); c.Star(130, 106, 8, 3, 4, Color.white);
                c.SaveSprite(ArtPaths.Chest(ids[i], true));
            }
        }

        // ------------------------------------------------------------------ Trees
        static void GenerateTrees()
        {
            for (int state = 0; state < 3; state++)
            {
                Progress("trunk"); var c = new PixelCanvas(256, 512);
                c.RoundedRectOutlined(44, 0, 168, 512, 44, Light, Outline, 6);
                c.Capsule(new Vector2(78, 40), new Vector2(78, 470), 12, White);
                if (state >= 1) Cracks(c, 11 + state, 4 + state * 3, 40, 110, 3.5f);
                if (state == 2) { c.EraseCircle(212, 300, 40); c.EraseCircle(44, 140, 30); }
                c.SaveSprite(ArtPaths.Tree("trunk", state), 100, null, false, new Vector2(0.5f, 0f));

                Progress("branch"); c = new PixelCanvas(384, 96);
                c.Capsule(new Vector2(40, 46), new Vector2(344, 50), 38, Outline);
                c.Capsule(new Vector2(40, 46), new Vector2(344, 50), 32, Light);
                c.Capsule(new Vector2(60, 60), new Vector2(320, 64), 6, White);
                if (state >= 1) Cracks(c, 21 + state, 3 + state * 2, 30, 70, 3f);
                if (state == 2) c.EraseCircle(200, 12, 26);
                c.SaveSprite(ArtPaths.Tree("branch", state), 100, null, false, new Vector2(0f, 0.5f));

                Progress("bark"); c = new PixelCanvas(192, 224);
                c.RoundedRectOutlined(16, 16, 160, 192, 40, Light, Outline, 6);
                c.RoundedRect(34, 34, 124, 156, 30, Mid);
                c.RoundedRect(48, 48, 96, 128, 24, Light);
                if (state >= 1) Cracks(c, 31 + state, 4 + state * 4, 30, 90, 3f);
                if (state == 2) { c.EraseCircle(176, 60, 34); c.EraseCircle(20, 180, 28); }
                c.SaveSprite(ArtPaths.Tree("bark", state));
            }

            Progress("canopy"); var cc = new PixelCanvas(384, 256);
            var blobs = new[] { (192f, 140f, 96f), (100f, 110f, 70f), (284f, 110f, 70f), (150f, 190f, 62f), (240f, 190f, 62f), (60f, 160f, 48f), (324f, 160f, 48f) };
            foreach (var b in blobs) cc.Circle(b.Item1, b.Item2, b.Item3 + 6, Outline);
            foreach (var b in blobs) cc.Circle(b.Item1, b.Item2, b.Item3, Light);
            cc.Circle(180, 170, 40, White); cc.Circle(120, 130, 24, White);
            cc.SaveSprite(ArtPaths.TreePart("canopy"), 100, null, false, new Vector2(0.5f, 0.2f));

            Progress("heartwood"); var h = new PixelCanvas(160, 160);
            h.RadialGlow(80, 80, 80, Amber, 1.8f);
            h.CircleOutlined(80, 80, 52, Amber, Outline, 5);
            h.Ring(80, 80, 40, 6, new Color(1f, 0.92f, 0.6f));
            h.Circle(80, 80, 22, new Color(1f, 0.95f, 0.75f));
            h.SaveSprite(ArtPaths.TreePart("heartwood"));

            Progress("root"); var r = new PixelCanvas(320, 128);
            r.Ellipse(160, 40, 150, 36, Outline); r.Ellipse(70, 62, 70, 30, Outline); r.Ellipse(250, 62, 70, 30, Outline); r.Ellipse(160, 84, 60, 40, Outline);
            r.Ellipse(160, 40, 144, 30, Mid); r.Ellipse(70, 62, 64, 24, Mid); r.Ellipse(250, 62, 64, 24, Mid); r.Ellipse(160, 84, 54, 34, Mid);
            r.SaveSprite(ArtPaths.TreePart("root"), 100, null, false, new Vector2(0.5f, 0.15f));

            Progress("platform"); var p = new PixelCanvas(176, 56);
            p.RoundedRectOutlined(8, 8, 160, 40, 16, Mid, Outline, 5);
            p.RoundedRect(20, 30, 136, 10, 5, Light);
            p.SaveSprite(ArtPaths.TreePart("platform"));

            Progress("canopy_shield"); var cs = new PixelCanvas(320, 160);
            for (int i = 0; i < 7; i++) { float x = 40 + i * 40; float y = 70 + Mathf.Sin(i / 6f * Mathf.PI) * 50; cs.Circle(x, y, 34, Outline); }
            for (int i = 0; i < 7; i++) { float x = 40 + i * 40; float y = 70 + Mathf.Sin(i / 6f * Mathf.PI) * 50; cs.Circle(x, y, 28, Light); }
            cs.SaveSprite(ArtPaths.TreePart("canopy_shield"));

            Progress("tuft"); var t = new PixelCanvas(96, 96);
            t.Leaf(48, 48, 60, 30, 60, Outline); t.Leaf(48, 48, 60, 30, 120, Outline); t.Leaf(48, 48, 60, 30, 90, Outline);
            t.Leaf(48, 48, 50, 22, 60, Light); t.Leaf(48, 48, 50, 22, 120, Light); t.Leaf(48, 48, 50, 22, 90, White);
            t.SaveSprite(ArtPaths.TreePart("tuft"));
        }

        // ------------------------------------------------------------------ Arenas (white masks tinted at runtime)
        static void GenerateArenas()
        {
            Progress("clouds"); var c = new PixelCanvas(512, 160);
            var rng = new System.Random(5);
            for (int k = 0; k < 4; k++)
            {
                float bx = 40 + k * 130 + (float)rng.NextDouble() * 30, by = 50 + (float)rng.NextDouble() * 40;
                c.Ellipse(bx, by, 70, 30, new Color(1, 1, 1, 0.95f)); c.Ellipse(bx - 30, by + 10, 40, 28, new Color(1, 1, 1, 0.95f)); c.Ellipse(bx + 34, by + 14, 44, 30, new Color(1, 1, 1, 0.95f)); c.Ellipse(bx + 6, by + 24, 36, 26, new Color(1, 1, 1, 0.95f));
            }
            c.SaveSprite(ArtPaths.Arena("clouds"), 100, null, true);

            Progress("mountains"); c = new PixelCanvas(512, 256);
            var pts = new List<Vector2> { new Vector2(0, 0), new Vector2(0, 90) };
            rng = new System.Random(9);
            for (int x = 0; x <= 512; x += 32) pts.Add(new Vector2(x, 70 + Mathf.PerlinNoise(x * 0.013f, 0.7f) * 150 + (float)rng.NextDouble() * 20));
            pts.Add(new Vector2(512, 90)); pts.Add(new Vector2(512, 0));
            c.Polygon(pts.ToArray(), Color.white);
            c.SaveSprite(ArtPaths.Arena("mountains"), 100, null, true);

            Progress("forest_back"); c = new PixelCanvas(512, 224);
            c.RoundedRect(0, -40, 512, 90, 0, Color.white);
            for (int i = 0; i < 14; i++) { float x = i * 38 + 10; float hgt = 90 + Mathf.PerlinNoise(i * 0.7f, 2f) * 110; c.Triangle(new Vector2(x - 30, 40), new Vector2(x + 30, 40), new Vector2(x, hgt), Color.white); c.Circle(x, hgt - 10, 14, Color.white); }
            c.SaveSprite(ArtPaths.Arena("forest_back"), 100, null, true);

            Progress("forest_mid"); c = new PixelCanvas(512, 288);
            c.RoundedRect(0, -40, 512, 70, 0, Color.white);
            for (int i = 0; i < 9; i++) { float x = i * 60 + 24; float hgt = 140 + Mathf.PerlinNoise(i * 0.9f, 4f) * 130; c.RoundedRect(x - 8, 20, 16, hgt * 0.5f, 4, Color.white); c.Circle(x, hgt - 30, 44, Color.white); c.Circle(x - 30, hgt - 60, 30, Color.white); c.Circle(x + 30, hgt - 60, 30, Color.white); }
            c.SaveSprite(ArtPaths.Arena("forest_mid"), 100, null, true);

            Progress("ground"); c = new PixelCanvas(512, 160);
            c.RoundedRect(-20, -60, 552, 200, 48, Color.white);
            c.SaveSprite(ArtPaths.Arena("ground"), 100, null, true, new Vector2(0.5f, 1f));

            Progress("foreground"); c = new PixelCanvas(512, 160);
            rng = new System.Random(3);
            for (int i = 0; i < 26; i++) { float x = (float)rng.NextDouble() * 512; float len = 50 + (float)rng.NextDouble() * 80; float ang = 70 + (float)rng.NextDouble() * 40; c.Leaf(x, len * 0.4f, len, 22, ang, Color.white); }
            c.SaveSprite(ArtPaths.Arena("foreground"), 100, null, true, new Vector2(0.5f, 0f));

            Progress("mist"); c = new PixelCanvas(512, 128);
            c.VerticalGradient(new Color(1, 1, 1, 0f), new Color(1, 1, 1, 0.7f));
            c.SaveSprite(ArtPaths.Arena("mist"), 100, null, true);

            Progress("sky"); c = new PixelCanvas(8, 8);
            c.VerticalGradient(Color.white, Color.white);
            c.SaveSprite(ArtPaths.Arena("sky"), 100, null, false);

            Progress("particle_leaf"); c = new PixelCanvas(32, 32);
            c.Leaf(16, 16, 26, 14, 45, Color.white);
            c.SaveSprite(ArtPaths.Vfx("leaf"));

            Progress("particle_glow"); c = new PixelCanvas(32, 32);
            c.RadialGlow(16, 16, 16, Color.white, 1.6f);
            c.SaveSprite(ArtPaths.Vfx("glow"));

            Progress("particle_bark"); c = new PixelCanvas(24, 24);
            c.Polygon(P(4, 4, 20, 8, 18, 20, 6, 18), Color.white);
            c.SaveSprite(ArtPaths.Vfx("bark_shard"));
        }

        // ------------------------------------------------------------------ Projectiles & VFX
        static void GenerateProjectiles()
        {
            Progress("proj"); var c = new PixelCanvas(64, 64);
            PolygonOutlined(c, P(6, 26, 6, 38, 58, 32), White, Outline, 3);
            c.SaveSprite(ArtPaths.Projectile("thorn"));

            Progress("proj"); c = new PixelCanvas(64, 64);
            c.EllipseOutlined(32, 32, 22, 16, White, Outline, 3); c.Capsule(new Vector2(20, 32), new Vector2(44, 32), 2, Outline);
            c.SaveSprite(ArtPaths.Projectile("seed"));

            Progress("proj"); c = new PixelCanvas(64, 64);
            c.RadialGlow(32, 32, 32, White, 1.6f); c.CircleOutlined(32, 32, 14, White, Outline, 3);
            c.SaveSprite(ArtPaths.Projectile("lightseed"));

            Progress("proj"); c = new PixelCanvas(64, 64);
            c.EllipseOutlined(32, 32, 18, 24, White, Outline, 3); for (int y = 0; y < 3; y++) for (int x = 0; x < 2; x++) c.Circle(26 + x * 12, 20 + y * 12, 3, Outline);
            c.SaveSprite(ArtPaths.Projectile("cone"));

            Progress("proj"); c = new PixelCanvas(64, 64);
            c.Capsule(new Vector2(6, 32), new Vector2(58, 32), 7, Outline); c.Capsule(new Vector2(8, 32), new Vector2(56, 32), 4, White); c.Triangle(new Vector2(46, 22), new Vector2(46, 42), new Vector2(62, 32), Outline); c.Leaf(18, 40, 18, 9, 40, Outline); c.Leaf(18, 40, 14, 6, 40, White);
            c.SaveSprite(ArtPaths.Projectile("vinespear"));

            Progress("proj"); c = new PixelCanvas(64, 64);
            c.CircleOutlined(32, 32, 22, White, Outline, 3); c.Circle(24, 36, 4, Outline); c.Circle(38, 26, 5, Outline); c.Circle(36, 40, 3, Outline);
            c.SaveSprite(ArtPaths.Projectile("sporebomb"));

            Progress("proj"); c = new PixelCanvas(64, 64);
            c.Ellipse(24, 44, 12, 8, new Color(1, 1, 1, 0.8f)); c.Ellipse(40, 44, 12, 8, new Color(1, 1, 1, 0.8f)); c.EllipseOutlined(32, 30, 20, 13, White, Outline, 3); c.Capsule(new Vector2(24, 20), new Vector2(24, 40), 3, Outline); c.Capsule(new Vector2(34, 18), new Vector2(34, 42), 3, Outline);
            c.SaveSprite(ArtPaths.Projectile("bee"));

            Progress("proj"); c = new PixelCanvas(64, 64);
            PolygonOutlined(c, P(12, 22, 20, 10, 40, 8, 54, 20, 56, 40, 44, 56, 22, 54, 10, 40), White, Outline, 3);
            c.SaveSprite(ArtPaths.Projectile("rock"));

            Progress("proj"); c = new PixelCanvas(64, 64);
            c.CircleOutlined(32, 32, 16, White, Outline, 3);
            c.SaveSprite(ArtPaths.Projectile("generic"));

            Progress("vfx"); c = new PixelCanvas(128, 128);
            c.RadialGlow(64, 64, 64, White, 2f); c.Star(64, 64, 60, 22, 8, new Color(1, 1, 1, 0.9f));
            c.SaveSprite(ArtPaths.Vfx("burst"));

            Progress("vfx"); c = new PixelCanvas(96, 96);
            c.Star(48, 48, 44, 18, 6, White);
            c.SaveSprite(ArtPaths.Vfx("hit"));

            Progress("vfx"); c = new PixelCanvas(96, 96);
            c.RadialGlow(48, 48, 48, White, 1.5f); c.RoundedRect(40, 16, 16, 64, 6, White); c.RoundedRect(16, 40, 64, 16, 6, White);
            c.SaveSprite(ArtPaths.Vfx("heal"));

            Progress("vfx"); c = new PixelCanvas(128, 128);
            c.Ring(64, 64, 60, 10, new Color(1, 1, 1, 0.9f)); c.RadialGlow(64, 64, 56, new Color(1, 1, 1, 0.5f), 1.2f);
            c.SaveSprite(ArtPaths.Vfx("shield"));

            Progress("vfx"); c = new PixelCanvas(64, 64);
            c.RadialGlow(32, 32, 32, White, 1.3f);
            c.SaveSprite(ArtPaths.Vfx("poison"));

            Progress("vfx"); c = new PixelCanvas(96, 24);
            c.Capsule(new Vector2(12, 12), new Vector2(84, 12), 8, new Color(1, 1, 1, 0.9f));
            c.SaveSprite(ArtPaths.Vfx("trail"));

            Progress("aim"); c = new PixelCanvas(24, 24);
            c.CircleOutlined(12, 12, 8, White, Outline, 2);
            c.SaveSprite(ArtPaths.Vfx("aim_dot"));

            Progress("aim"); c = new PixelCanvas(96, 96);
            c.RingOutlined(48, 48, 44, 8, White, Outline, 2); c.Capsule(new Vector2(48, 4), new Vector2(48, 24), 3, Outline); c.Capsule(new Vector2(48, 72), new Vector2(48, 92), 3, Outline); c.Capsule(new Vector2(4, 48), new Vector2(24, 48), 3, Outline); c.Capsule(new Vector2(72, 48), new Vector2(92, 48), 3, Outline);
            c.SaveSprite(ArtPaths.Vfx("aim_reticle"));
        }

        // ------------------------------------------------------------------ Tools
        static void GenerateTools()
        {
            Progress("tool"); var c = new PixelCanvas(128, 128);
            c.RoundedRectOutlined(14, 14, 100, 30, 10, Mid, Outline, 5); c.Capsule(new Vector2(40, 40), new Vector2(92, 104), 9, Outline); c.Capsule(new Vector2(44, 42), new Vector2(90, 100), 5, Light); c.CircleOutlined(100, 106, 16, White, Outline, 4); c.Circle(30, 60, 12, Outline);
            c.SaveSprite(ArtPaths.Tool("catapult"));

            Progress("tool"); c = new PixelCanvas(128, 128);
            c.RoundedRectOutlined(40, 10, 48, 70, 12, White, Outline, 5); c.RoundedRectOutlined(48, 76, 32, 26, 6, Mid, Outline, 4); c.Capsule(new Vector2(88, 96), new Vector2(112, 110), 6, Outline); c.Circle(112, 118, 8, Outline); c.Circle(112, 118, 5, Light); c.Circle(30, 100, 7, Outline); c.Circle(30, 100, 4, Light); c.Circle(22, 84, 5, Outline);
            c.SaveSprite(ArtPaths.Tool("dew"));

            Progress("tool"); c = new PixelCanvas(128, 128);
            c.EllipseOutlined(64, 30, 46, 16, Amber, Outline, 5); c.EllipseOutlined(64, 56, 52, 18, Amber, Outline, 5); c.EllipseOutlined(64, 82, 44, 16, Amber, Outline, 5); c.EllipseOutlined(64, 104, 30, 12, Amber, Outline, 5); c.Circle(64, 56, 9, Outline);
            c.SaveSprite(ArtPaths.Tool("hive"));

            Progress("tool"); c = new PixelCanvas(128, 128);
            c.Polygon(P(28, 36, 34, 80, 48, 104, 80, 104, 94, 80, 100, 36), Outline); c.Polygon(P(35, 40, 40, 78, 52, 98, 76, 98, 88, 78, 93, 40), White); c.Capsule(new Vector2(22, 36), new Vector2(106, 36), 6, Outline); c.Circle(64, 112, 12, Outline); c.Circle(64, 112, 7, Light); c.Capsule(new Vector2(64, 104), new Vector2(64, 118), 5, Outline); c.Capsule(new Vector2(104, 60), new Vector2(120, 52), 4, Outline); c.Capsule(new Vector2(104, 76), new Vector2(122, 72), 4, Outline);
            c.SaveSprite(ArtPaths.Tool("bell"));

            Progress("tool"); c = new PixelCanvas(128, 128);
            for (int i = 0; i < 4; i++) { float o = 20 + i * 29; c.Capsule(new Vector2(o, 14), new Vector2(o, 114), 5, Outline); c.Capsule(new Vector2(14, o), new Vector2(114, o), 5, Outline); }
            for (int i = 0; i < 4; i++) { float o = 20 + i * 29; c.Capsule(new Vector2(o, 16), new Vector2(o, 112), 2, Light); c.Capsule(new Vector2(16, o), new Vector2(112, o), 2, Light); }
            c.Leaf(96, 96, 30, 16, 45, Outline); c.Leaf(96, 96, 24, 12, 45, White);
            c.SaveSprite(ArtPaths.Tool("net"));

            Progress("tool"); c = new PixelCanvas(128, 128);
            Shield(c, 64, 62, 52, White); c.EllipseOutlined(64, 60, 18, 26, Light, Outline, 4); c.Capsule(new Vector2(64, 40), new Vector2(64, 80), 2, Outline);
            c.SaveSprite(ArtPaths.Tool("seedshield"));
        }

        // ------------------------------------------------------------------ Logos
        static void GenerateLogos()
        {
            Progress("logo"); var c = new PixelCanvas(256, 256);
            c.CircleOutlined(128, 128, 110, White, Outline, 8);
            c.Leaf(128, 128, 150, 80, 50, Outline); c.Leaf(128, 128, 134, 66, 50, new Color(0.45f, 0.78f, 0.4f)); c.Capsule(new Vector2(80, 80), new Vector2(176, 176), 4, Outline);
            c.SaveSprite(ArtPaths.Logo("studio"));

            Progress("logo"); c = new PixelCanvas(512, 256);
            c.RadialGlow(256, 128, 220, new Color(1f, 0.92f, 0.6f, 0.9f), 2.5f);
            c.Circle(256, 120, 92, Outline); c.Circle(200, 96, 62, Outline); c.Circle(312, 96, 62, Outline);
            c.Circle(256, 120, 84, new Color(0.45f, 0.78f, 0.4f)); c.Circle(200, 96, 54, new Color(0.45f, 0.78f, 0.4f)); c.Circle(312, 96, 54, new Color(0.45f, 0.78f, 0.4f));
            c.Circle(240, 140, 34, new Color(0.6f, 0.88f, 0.5f));
            c.RoundedRectOutlined(240, 8, 32, 80, 10, new Color(0.5f, 0.35f, 0.2f), Outline, 6);
            c.CircleOutlined(256, 128, 22, Amber, Outline, 5);
            c.SaveSprite(ArtPaths.Logo("game"));
        }
    }
}
