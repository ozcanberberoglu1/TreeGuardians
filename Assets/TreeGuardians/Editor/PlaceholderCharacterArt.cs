using System;
using UnityEngine;

namespace TreeGuardians.Editor
{
    /// Twelve original mascot-style placeholder guardians: portrait (with backdrop) and world sprite (transparent).
    public static class PlaceholderCharacterArt
    {
        public struct CharacterSpec
        {
            public string id;
            public Color body;
            public Color accent;
            public Action<PixelCanvas, float, float, float> feature;
        }

        static readonly Color Outline = PlaceholderArtGenerator.Outline;
        static readonly Color Eye = Color.white;
        static readonly Color Pupil = new Color(0.1f, 0.1f, 0.14f);

        public static readonly CharacterSpec[] Characters =
        {
            new CharacterSpec { id = "thorn_archer", body = new Color(0.45f, 0.7f, 0.38f), accent = new Color(0.6f, 0.4f, 0.22f), feature = ThornArcher },
            new CharacterSpec { id = "cone_bomber", body = new Color(0.62f, 0.42f, 0.24f), accent = new Color(0.85f, 0.6f, 0.3f), feature = ConeBomber },
            new CharacterSpec { id = "bark_knight", body = new Color(0.5f, 0.36f, 0.24f), accent = new Color(0.72f, 0.72f, 0.75f), feature = BarkKnight },
            new CharacterSpec { id = "dew_fairy", body = new Color(0.55f, 0.82f, 0.95f), accent = new Color(0.85f, 0.95f, 1f), feature = DewFairy },
            new CharacterSpec { id = "vine_master", body = new Color(0.3f, 0.6f, 0.35f), accent = new Color(0.55f, 0.85f, 0.45f), feature = VineMaster },
            new CharacterSpec { id = "spore_alchemist", body = new Color(0.85f, 0.8f, 0.7f), accent = new Color(0.75f, 0.35f, 0.55f), feature = SporeAlchemist },
            new CharacterSpec { id = "owl_scout", body = new Color(0.6f, 0.5f, 0.4f), accent = new Color(0.95f, 0.8f, 0.3f), feature = OwlScout },
            new CharacterSpec { id = "oak_warden", body = new Color(0.42f, 0.32f, 0.22f), accent = new Color(0.4f, 0.68f, 0.32f), feature = OakWarden },
            new CharacterSpec { id = "firefly_mage", body = new Color(0.25f, 0.25f, 0.4f), accent = new Color(1f, 0.9f, 0.35f), feature = FireflyMage },
            new CharacterSpec { id = "beaver_engineer", body = new Color(0.55f, 0.38f, 0.25f), accent = new Color(0.95f, 0.92f, 0.85f), feature = BeaverEngineer },
            new CharacterSpec { id = "hedgehog_sniper", body = new Color(0.72f, 0.55f, 0.4f), accent = new Color(0.35f, 0.25f, 0.2f), feature = HedgehogSniper },
            new CharacterSpec { id = "ancient_sprout", body = new Color(0.55f, 0.85f, 0.6f), accent = new Color(1f, 0.95f, 0.6f), feature = AncientSprout },
        };

        public static void GenerateAll(Action<string> progress)
        {
            foreach (var ch in Characters)
            {
                progress?.Invoke("character " + ch.id);
                var p = new PixelCanvas(256, 256);
                p.RoundedRect(0, 0, 256, 256, 28, new Color(0.16f, 0.2f, 0.28f));
                p.RadialGlow(128, 120, 130, Color.Lerp(ch.body, Color.white, 0.3f) * new Color(1, 1, 1, 0.55f), 1.8f);
                Draw(p, ch, 128, 116, 1f);
                p.SaveSprite(ArtPaths.Portrait(ch.id));

                var w = new PixelCanvas(256, 256);
                Draw(w, ch, 128, 128, 1f);
                w.SaveSprite(ArtPaths.WorldSprite(ch.id), 100, null, false, new Vector2(0.5f, 0.12f));
            }
        }

        static void Draw(PixelCanvas c, CharacterSpec ch, float cx, float cy, float s)
        {
            ch.feature(c, cx, cy, s);
        }

        static void Body(PixelCanvas c, float cx, float cy, float r, Color body)
        {
            c.CircleOutlined(cx, cy, r, body, Outline, 6);
            c.Circle(cx - r * 0.3f, cy + r * 0.3f, r * 0.28f, Color.Lerp(body, Color.white, 0.35f));
        }

        static void Eyes(PixelCanvas c, float cx, float cy, float spread, float size, float lookX = 0f)
        {
            c.CircleOutlined(cx - spread, cy, size, Eye, Outline, 4);
            c.CircleOutlined(cx + spread, cy, size, Eye, Outline, 4);
            c.Circle(cx - spread + lookX + size * 0.15f, cy - size * 0.1f, size * 0.5f, Pupil);
            c.Circle(cx + spread + lookX + size * 0.15f, cy - size * 0.1f, size * 0.5f, Pupil);
            c.Circle(cx - spread + lookX + size * 0.3f, cy + size * 0.15f, size * 0.16f, Eye);
            c.Circle(cx + spread + lookX + size * 0.3f, cy + size * 0.15f, size * 0.16f, Eye);
        }

        static void Smile(PixelCanvas c, float cx, float cy, float w)
        {
            c.Capsule(new Vector2(cx - w, cy + 4), new Vector2(cx, cy - 4), 4, Outline);
            c.Capsule(new Vector2(cx, cy - 4), new Vector2(cx + w, cy + 4), 4, Outline);
        }

        static void Ear(PixelCanvas c, float bx, float by, float tipX, float tipY, float width, Color fill)
        {
            PlaceholderArtGenerator.PolygonOutlined(c, new[] { new Vector2(bx - width, by), new Vector2(bx + width, by), new Vector2(tipX, tipY) }, fill, Outline, 6);
        }

        static void ThornArcher(PixelCanvas c, float cx, float cy, float s)
        {
            var body = Characters[0].body; var acc = Characters[0].accent;
            Ear(c, cx - 50, cy + 40, cx - 80, cy + 110, 22, body);
            Ear(c, cx + 50, cy + 40, cx + 80, cy + 110, 22, body);
            Body(c, cx, cy, 74, body);
            c.Capsule(new Vector2(cx + 60, cy - 60), new Vector2(cx + 100, cy + 30), 7, Outline);
            c.Capsule(new Vector2(cx + 62, cy - 58), new Vector2(cx + 98, cy + 28), 4, acc);
            c.Capsule(new Vector2(cx + 62, cy - 58), new Vector2(cx + 98, cy + 28), 1.5f, Outline);
            Eyes(c, cx, cy + 10, 26, 16);
            Smile(c, cx, cy - 26, 18);
            c.Leaf(cx - 10, cy + 84, 40, 18, 100, Outline); c.Leaf(cx - 10, cy + 84, 32, 12, 100, acc);
        }

        static void ConeBomber(PixelCanvas c, float cx, float cy, float s)
        {
            var body = Characters[1].body; var acc = Characters[1].accent;
            c.EllipseOutlined(cx, cy - 10, 80, 92, body, Outline, 6);
            for (int y = 0; y < 4; y++) for (int x = 0; x < 3; x++) c.Ellipse(cx - 40 + x * 40 + (y % 2) * 20, cy - 80 + y * 26, 16, 10, acc);
            for (int y = 0; y < 4; y++) for (int x = 0; x < 3; x++) c.Ellipse(cx - 40 + x * 40 + (y % 2) * 20, cy - 80 + y * 26, 16, 10, new Color(0, 0, 0, 0.12f));
            Eyes(c, cx, cy + 30, 28, 17);
            Smile(c, cx, cy - 4, 16);
            c.Capsule(new Vector2(cx, cy + 82), new Vector2(cx + 14, cy + 112), 6, Outline);
            c.Circle(cx + 18, cy + 118, 10, new Color(1f, 0.5f, 0.2f));
            c.Circle(cx + 18, cy + 118, 5, new Color(1f, 0.9f, 0.4f));
        }

        static void BarkKnight(PixelCanvas c, float cx, float cy, float s)
        {
            var body = Characters[2].body; var acc = Characters[2].accent;
            Body(c, cx, cy, 72, body);
            c.RoundedRectOutlined(cx - 84, cy + 30, 168, 46, 18, acc, Outline, 6);
            c.RoundedRect(cx - 40, cy + 38, 80, 30, 12, Outline);
            Eyes(c, cx, cy + 52, 24, 12);
            PlaceholderArtGenerator.PolygonOutlined(c, new[] { new Vector2(cx - 40, cy - 30), new Vector2(cx + 40, cy - 30), new Vector2(cx + 40, cy - 90), new Vector2(cx, cy - 112), new Vector2(cx - 40, cy - 90) }, acc, Outline, 6);
            c.Capsule(new Vector2(cx, cy - 40), new Vector2(cx, cy - 100), 4, Outline);
            c.Capsule(new Vector2(cx - 26, cy - 62), new Vector2(cx + 26, cy - 62), 4, Outline);
        }

        static void DewFairy(PixelCanvas c, float cx, float cy, float s)
        {
            var body = Characters[3].body; var acc = Characters[3].accent;
            c.EllipseOutlined(cx - 70, cy + 20, 44, 62, acc * new Color(1, 1, 1, 0.9f), Outline, 5);
            c.EllipseOutlined(cx + 70, cy + 20, 44, 62, acc * new Color(1, 1, 1, 0.9f), Outline, 5);
            c.Ellipse(cx - 70, cy + 20, 26, 40, new Color(1, 1, 1, 0.5f));
            c.Ellipse(cx + 70, cy + 20, 26, 40, new Color(1, 1, 1, 0.5f));
            Body(c, cx, cy, 64, body);
            Eyes(c, cx, cy + 8, 24, 15);
            Smile(c, cx, cy - 24, 14);
            c.Circle(cx, cy + 80, 16, Outline); c.Circle(cx, cy + 80, 11, new Color(0.7f, 0.95f, 1f)); c.Circle(cx - 4, cy + 84, 4, Color.white);
        }

        static void VineMaster(PixelCanvas c, float cx, float cy, float s)
        {
            var body = Characters[4].body; var acc = Characters[4].accent;
            for (int i = 0; i < 5; i++)
            {
                float a = -0.4f + i * 0.5f;
                Vector2 p0 = new Vector2(cx + Mathf.Cos(a) * 60, cy + Mathf.Sin(a) * 60);
                Vector2 p1 = new Vector2(cx + Mathf.Cos(a + 0.4f) * 104, cy + Mathf.Sin(a + 0.4f) * 104);
                c.Capsule(p0, p1, 11, Outline); c.Capsule(p0, p1, 7, acc);
                c.Leaf(p1.x, p1.y, 30, 14, a * Mathf.Rad2Deg + 60, Outline); c.Leaf(p1.x, p1.y, 24, 10, a * Mathf.Rad2Deg + 60, acc);
            }
            Body(c, cx, cy, 70, body);
            Eyes(c, cx, cy + 8, 26, 15);
            Smile(c, cx, cy - 26, 18);
        }

        static void SporeAlchemist(PixelCanvas c, float cx, float cy, float s)
        {
            var body = Characters[5].body; var acc = Characters[5].accent;
            Body(c, cx, cy - 10, 62, body);
            c.EllipseOutlined(cx, cy + 46, 96, 44, acc, Outline, 6);
            c.RoundedRect(cx - 96, cy + 0, 192, 46, 0, Color.clear);
            c.Ellipse(cx, cy + 46, 96, 44, acc);
            c.Ellipse(cx, cy + 40, 100, 14, Outline);
            c.Ellipse(cx, cy + 42, 96, 10, Color.Lerp(acc, Outline, 0.5f));
            c.Circle(cx - 40, cy + 60, 12, Color.white); c.Circle(cx + 30, cy + 70, 15, Color.white); c.Circle(cx + 60, cy + 50, 8, Color.white);
            Eyes(c, cx, cy, 24, 15);
            Smile(c, cx, cy - 30, 14);
            c.Circle(cx + 80, cy - 40, 14, Outline); c.Circle(cx + 80, cy - 40, 9, new Color(0.7f, 0.9f, 0.4f));
        }

        static void OwlScout(PixelCanvas c, float cx, float cy, float s)
        {
            var body = Characters[6].body; var acc = Characters[6].accent;
            Ear(c, cx - 44, cy + 50, cx - 62, cy + 104, 18, body);
            Ear(c, cx + 44, cy + 50, cx + 62, cy + 104, 18, body);
            c.EllipseOutlined(cx, cy, 78, 84, body, Outline, 6);
            c.Ellipse(cx, cy - 20, 44, 50, Color.Lerp(body, Color.white, 0.4f));
            c.CircleOutlined(cx - 30, cy + 22, 26, Eye, Outline, 5);
            c.CircleOutlined(cx + 30, cy + 22, 26, Eye, Outline, 5);
            c.Circle(cx - 28, cy + 22, 14, acc); c.Circle(cx + 32, cy + 22, 14, acc);
            c.Circle(cx - 28, cy + 22, 7, Pupil); c.Circle(cx + 32, cy + 22, 7, Pupil);
            c.Circle(cx - 24, cy + 26, 3, Eye); c.Circle(cx + 36, cy + 26, 3, Eye);
            PlaceholderArtGenerator.PolygonOutlined(c, new[] { new Vector2(cx - 10, cy - 4), new Vector2(cx + 10, cy - 4), new Vector2(cx, cy - 22) }, acc, Outline, 4);
        }

        static void OakWarden(PixelCanvas c, float cx, float cy, float s)
        {
            var body = Characters[7].body; var acc = Characters[7].accent;
            c.RoundedRectOutlined(cx - 76, cy - 80, 152, 160, 34, body, Outline, 7);
            c.RoundedRect(cx - 60, cy - 66, 22, 130, 10, Color.Lerp(body, Color.white, 0.2f));
            c.Circle(cx - 20, cy + 96, 30, Outline); c.Circle(cx + 24, cy + 100, 26, Outline); c.Circle(cx + 2, cy + 116, 22, Outline);
            c.Circle(cx - 20, cy + 96, 24, acc); c.Circle(cx + 24, cy + 100, 20, acc); c.Circle(cx + 2, cy + 116, 16, Color.Lerp(acc, Color.white, 0.3f));
            Eyes(c, cx, cy + 14, 28, 14);
            c.Capsule(new Vector2(cx - 20, cy - 30), new Vector2(cx + 20, cy - 30), 4, Outline);
            c.Capsule(new Vector2(cx - 40, cy - 60), new Vector2(cx + 40, cy - 60), 5, Outline);
        }

        static void FireflyMage(PixelCanvas c, float cx, float cy, float s)
        {
            var body = Characters[8].body; var acc = Characters[8].accent;
            c.RadialGlow(cx, cy - 46, 70, acc * new Color(1, 1, 1, 0.8f), 1.6f);
            c.EllipseOutlined(cx, cy - 46, 40, 34, acc, Outline, 5);
            c.Ellipse(cx - 60, cy + 30, 34, 50, new Color(0.8f, 0.9f, 1f, 0.75f)); c.Ellipse(cx + 60, cy + 30, 34, 50, new Color(0.8f, 0.9f, 1f, 0.75f));
            Body(c, cx, cy + 10, 58, body);
            c.Capsule(new Vector2(cx - 20, cy + 62), new Vector2(cx - 34, cy + 100), 4, Outline);
            c.Capsule(new Vector2(cx + 20, cy + 62), new Vector2(cx + 34, cy + 100), 4, Outline);
            c.Circle(cx - 34, cy + 100, 7, acc); c.Circle(cx + 34, cy + 100, 7, acc);
            Eyes(c, cx, cy + 18, 22, 14);
            Smile(c, cx, cy - 12, 14);
        }

        static void BeaverEngineer(PixelCanvas c, float cx, float cy, float s)
        {
            var body = Characters[9].body; var acc = Characters[9].accent;
            c.EllipseOutlined(cx + 70, cy - 40, 56, 30, new Color(0.35f, 0.24f, 0.16f), Outline, 6);
            for (int i = 0; i < 3; i++) c.Capsule(new Vector2(cx + 40 + i * 20, cy - 60), new Vector2(cx + 50 + i * 20, cy - 20), 2, Outline);
            Body(c, cx, cy, 70, body);
            c.CircleOutlined(cx - 46, cy + 56, 18, body, Outline, 5); c.CircleOutlined(cx + 46, cy + 56, 18, body, Outline, 5);
            Eyes(c, cx, cy + 12, 26, 14);
            c.RoundedRectOutlined(cx - 20, cy - 52, 18, 26, 4, acc, Outline, 4);
            c.RoundedRectOutlined(cx + 2, cy - 52, 18, 26, 4, acc, Outline, 4);
            c.Ellipse(cx, cy - 14, 18, 10, Outline);
            c.RoundedRectOutlined(cx - 44, cy + 62, 88, 26, 8, new Color(1f, 0.8f, 0.2f), Outline, 5);
        }

        static void HedgehogSniper(PixelCanvas c, float cx, float cy, float s)
        {
            var body = Characters[10].body; var acc = Characters[10].accent;
            for (int i = 0; i < 9; i++)
            {
                float a = Mathf.PI * 0.15f + i * (Mathf.PI * 0.7f / 8f);
                Vector2 b = new Vector2(cx + Mathf.Cos(a) * 60, cy + Mathf.Sin(a) * 60);
                Vector2 t = new Vector2(cx + Mathf.Cos(a) * 112, cy + Mathf.Sin(a) * 112);
                Vector2 n = new Vector2(-Mathf.Sin(a), Mathf.Cos(a)) * 12;
                PlaceholderArtGenerator.PolygonOutlined(c, new[] { b - n, b + n, t }, acc, Outline, 5);
            }
            Body(c, cx, cy, 72, body);
            Eyes(c, cx, cy + 6, 26, 14, 4f);
            c.Circle(cx + 36, cy - 30, 10, Outline);
            c.Capsule(new Vector2(cx + 60, cy + 10), new Vector2(cx + 118, cy + 22), 8, Outline);
            c.Capsule(new Vector2(cx + 62, cy + 10), new Vector2(cx + 116, cy + 22), 4, new Color(0.5f, 0.5f, 0.55f));
        }

        static void AncientSprout(PixelCanvas c, float cx, float cy, float s)
        {
            var body = Characters[11].body; var acc = Characters[11].accent;
            c.RadialGlow(cx, cy, 120, acc * new Color(1, 1, 1, 0.7f), 2f);
            Body(c, cx, cy - 6, 70, body);
            c.Capsule(new Vector2(cx, cy + 60), new Vector2(cx, cy + 100), 6, Outline);
            c.Capsule(new Vector2(cx, cy + 62), new Vector2(cx, cy + 98), 3, new Color(0.4f, 0.7f, 0.35f));
            c.Leaf(cx - 24, cy + 106, 46, 24, 140, Outline); c.Leaf(cx + 24, cy + 106, 46, 24, 40, Outline);
            c.Leaf(cx - 24, cy + 106, 38, 18, 140, new Color(0.5f, 0.85f, 0.45f)); c.Leaf(cx + 24, cy + 106, 38, 18, 40, new Color(0.5f, 0.85f, 0.45f));
            Eyes(c, cx, cy + 6, 26, 15);
            Smile(c, cx, cy - 30, 18);
            c.Circle(cx - 90, cy + 40, 6, acc); c.Circle(cx + 96, cy + 20, 5, acc); c.Circle(cx + 80, cy - 70, 7, acc);
        }
    }
}
