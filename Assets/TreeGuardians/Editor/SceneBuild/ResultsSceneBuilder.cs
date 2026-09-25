using TMPro;
using TreeGuardians.Battle;
using TreeGuardians.Localization;
using TreeGuardians.UI;
using UnityEngine;
using UnityEngine.UI;
using F = TreeGuardians.Editor.UIFactory;

namespace TreeGuardians.Editor.SceneBuild
{
    /// 04_Results in the main-menu art style (canvas 2340x1080):
    ///   backdrop  : menu background (bg.jpg, aspect-correct cover) + outcome-tinted dim + edge vignette
    ///   header    : yellow kit ribbon with the outcome title, arena pill, three socketed stars with glows
    ///   strip     : player / enemy castle integrity bars (UIFill) with shield icons and crossed swords
    ///   panels    : stats (kit icons, label + value rows) and rewards (menu currency icons, chest tier art)
    ///   bottom    : unlock banner, tutorial hint, Retry (blue kit) + Continue (yellow kit)
    /// ResultsController only binds and animates what is authored here.
    public static class ResultsSceneBuilder
    {
        const string BackgroundPath = "Assets/TreeGuardians/Art/UI/Panel/bg.jpg";
        /// Fraction of the background kept below the screen's bottom edge (puts the grass line where the menu has it).
        const float BackgroundPivotY = 0.14f;

        const int KitAcorn = 0, KitLog = 1, KitGem = 2, KitTrophy = 4, KitShield = 5, KitTarget = 9, KitHelmet = 16;
        const int KitStarburst = 36, KitSwords = 40, KitDark = 41, KitYellowButton = 42, KitBlueButton = 43;
        const int KitDarkPill = 45, KitRibbon = 46, KitUnlockPlate = 51;
        /// Closed chest art by ChestTier (Twig, Grove, Ancient, Moon, Sun), same as the menu's chest slots.
        static readonly int[] KitChestByTier = { 24, 25, 17, 26, 29 };

        const int TileCount = 7;
        const float TileW = 140f, TileH = 196f;

        static readonly Color PanelTint = new Color(0.8f, 0.83f, 0.9f, 0.97f);
        static readonly Color StarSocket = new Color(0.08f, 0.1f, 0.16f, 1f);
        static readonly Color StarOff = new Color(0.25f, 0.28f, 0.36f, 1f);
        static readonly Color StarGlow = new Color(1f, 0.8f, 0.3f, 0f);
        static readonly Color SubText = new Color(0.8f, 0.85f, 0.92f);
        static readonly Color TitleBrown = new Color(0.36f, 0.2f, 0.06f);
        static readonly Color BarBack = new Color(0.07f, 0.09f, 0.14f, 0.92f);
        static readonly Color DimVictory = new Color(0.04f, 0.06f, 0.12f, 0.55f);
        static readonly Color Vignette = new Color(0f, 0f, 0f, 0.6f);
        static readonly Color NoteRed = new Color(1f, 0.52f, 0.46f);
        static readonly Color StripeColor = new Color(1f, 1f, 1f, 0.06f);

        public static void Build()
        {
            var scene = SceneBuildUtility.NewScene("04_Results");
            F.Root("Results_Root");
            F.Camera2D("ResultsCamera", 5.4f, new Color(0.04f, 0.06f, 0.1f));
            var canvas = F.Canvas("ResultsCanvas");
            var controller = F.Root("ResultsController").AddComponent<ResultsController>();

            var dim = BuildBackdrop(canvas.transform);
            var safe = F.SafeArea(canvas.transform);

            // ---------------------------------------------------------------- header: ribbon, arena, stars
            var header = F.Rect("Header", safe);
            F.Anchor(header, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1400f, 330f), new Vector2(0.5f, 1f));
            var headerGroup = F.Group(header.gameObject, 1f);

            var victoryFx = F.Rect("VictoryFx", header);
            F.Stretch(victoryFx);
            var halo = F.Image("Halo", victoryFx, F.Ui("glow"), new Color(0.98f, 0.74f, 0.22f, 0.35f), true, false);
            F.Anchor((RectTransform)halo.transform, new Vector2(0.5f, 1f), new Vector2(0f, -236f), new Vector2(720f, 320f), new Vector2(0.5f, 0.5f));
            Burst("Burst_L", victoryFx, new Vector2(-430f, -72f), 14f);
            Burst("Burst_R", victoryFx, new Vector2(430f, -72f), -14f);

            var ribbon = F.Image("Ribbon", header, F.Kit(KitRibbon), Color.white, true, false);
            F.Anchor((RectTransform)ribbon.transform, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(860f, 120f), new Vector2(0.5f, 1f));
            ribbon.pixelsPerUnitMultiplier = 154f / 120f;
            var title = F.Text("Title", ribbon.transform, "results_title_victory", 72f, TitleBrown, TextAlignmentOptions.Center, true);
            F.Stretch((RectTransform)title.transform, 48f, 14f, 48f, 6f);
            AutoSize(title, 44f, 72f);

            var arenaPill = F.Image("ArenaPill", header, F.Kit(KitDarkPill), Color.white, true, false);
            F.Anchor((RectTransform)arenaPill.transform, new Vector2(0.5f, 1f), new Vector2(0f, -132f), new Vector2(440f, 44f), new Vector2(0.5f, 0.5f));
            arenaPill.pixelsPerUnitMultiplier = 106f / 44f;
            var arena = F.Text("Arena", arenaPill.transform, null, 24f, F.TextLight, TextAlignmentOptions.Center, true, "");
            F.Stretch((RectTransform)arena.transform, 24f, 2f, 24f, 2f);
            AutoSize(arena, 16f, 24f);

            var starsRow = F.Rect("Stars", header);
            F.Anchor(starsRow, new Vector2(0.5f, 1f), new Vector2(0f, -232f), new Vector2(440f, 140f), new Vector2(0.5f, 0.5f));
            var stars = new Object[3];
            var glows = new Object[3];
            for (int i = 0; i < 3; i++)
            {
                bool middle = i == 1;
                var pos = new Vector2((i - 1) * 140f, middle ? 6f : -10f);
                stars[i] = StarSlot("Star_" + i, starsRow, pos, middle ? 124f : 96f, out var glow);
                glows[i] = glow;
            }

            // ---------------------------------------------------------------- integrity strip (UIFill bars)
            var strip = F.Rect("Integrity", safe);
            F.Anchor(strip, new Vector2(0.5f, 1f), new Vector2(0f, -330f), new Vector2(1180f, 52f), new Vector2(0.5f, 0.5f));
            var stripGroup = F.Group(strip.gameObject, 1f);
            KitIcon("PlayerIcon", strip, KitShield, new Vector2(-560f, 0f), 48f);
            var pBar = IntegrityBar("PlayerBar", strip, new Vector2(-295f, 0f), F.Green, out var pText);
            KitIcon("Versus", strip, KitSwords, Vector2.zero, 52f);
            var eBar = IntegrityBar("EnemyBar", strip, new Vector2(295f, 0f), F.Red, out var eText);
            KitIcon("EnemyIcon", strip, KitShield, new Vector2(560f, 0f), 48f);

            // ---------------------------------------------------------------- stats panel
            var stats = Panel("StatsPanel", safe, new Vector2(-565f, -15f), new Vector2(570f, 350f), "results_stats");
            var statsGroup = F.Group(stats.gameObject, 1f);
            var rows = F.VerticalGroup("Rows", stats.transform, 6f, TextAnchor.UpperCenter);
            F.Anchor(rows, new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(530f, 250f), new Vector2(0.5f, 1f));
            var damage = StatRow("Row_Damage", rows, KitSwords, "results_damage", out _, out _);
            var coreLeft = StatRow("Row_Integrity", rows, KitShield, "results_castle_left", out var coreLeftLabel, out _);
            var alive = StatRow("Row_Alive", rows, KitHelmet, "results_guardians_alive", out _, out _);
            var accuracy = StatRow("Row_Accuracy", rows, KitTarget, "results_accuracy", out _, out var accuracyRow);

            // ---------------------------------------------------------------- rewards panel
            var rewards = Panel("RewardsPanel", safe, new Vector2(305f, -15f), new Vector2(1090f, 350f), "results_rewards");
            var rewardsGroup = F.Group(rewards.gameObject, 1f);
            var tilesRow = F.HorizontalGroup("RewardTiles", rewards.transform, 10f, TextAnchor.MiddleCenter);
            F.Anchor(tilesRow, new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(1050f, TileH), new Vector2(0.5f, 1f));
            var tiles = new Object[TileCount];
            for (int i = 0; i < TileCount; i++)
            {
                var t = RewardTile("Tile_" + i, tilesRow);
                t.gameObject.SetActive(false);
                tiles[i] = t;
            }
            var chestNote = F.Text("ChestNote", rewards.transform, null, 22f, NoteRed, TextAlignmentOptions.Center, true, "");
            F.AnchorStretchX((RectTransform)chestNote.transform, 14f, 34f, 30f, 30f, 0f, 0f);
            chestNote.gameObject.SetActive(false);

            // ---------------------------------------------------------------- unlock banner + tutorial hint
            var unlock = F.Image("UnlockPanel", safe, F.Kit(KitUnlockPlate), Color.white, true, false);
            F.Anchor((RectTransform)unlock.transform, new Vector2(0.5f, 0f), new Vector2(0f, 214f), new Vector2(700f, 104f), new Vector2(0.5f, 0f));
            unlock.pixelsPerUnitMultiplier = 132f / 104f;
            var uIcon = F.Image("Icon", unlock.transform, null, Color.white, false, false);
            F.Anchor((RectTransform)uIcon.transform, new Vector2(0f, 0.5f), new Vector2(54f, 0f), new Vector2(70f, 70f), new Vector2(0.5f, 0.5f));
            var uText = F.OutlinedText("Text", unlock.transform, null, 28f, F.Honey, TextAlignmentOptions.MidlineLeft, "");
            F.Stretch((RectTransform)uText.transform, 118f, 6f, 24f, 6f);
            AutoSize(uText, 18f, 28f);
            unlock.gameObject.SetActive(false);

            var tutHint = F.OutlinedText("TutorialHint", safe, null, 28f, F.Honey, TextAlignmentOptions.Center, "");
            F.Anchor((RectTransform)tutHint.transform, new Vector2(0.5f, 0f), new Vector2(0f, 170f), new Vector2(1200f, 44f), new Vector2(0.5f, 0f));
            tutHint.gameObject.SetActive(false);

            // ---------------------------------------------------------------- buttons (Retry blue, Continue yellow)
            var buttons = F.HorizontalGroup("Buttons", safe, 28f, TextAnchor.MiddleCenter);
            F.Anchor(buttons, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1000f, 128f), new Vector2(0.5f, 0f));
            var buttonsGroup = F.Group(buttons.gameObject, 1f);
            var retry = KitButton("RetryButton", buttons, KitBlueButton, "ui_retry", 40f, new Vector2(360f, 128f), 152f);
            var cont = KitButton("ContinueButton", buttons, KitYellowButton, "ui_continue", 44f, new Vector2(440f, 128f), 153f);

            var overlay = F.Image("TransitionOverlay", canvas.transform, F.Ui("white"), Color.black, false, true);
            overlay.preserveAspect = false;
            F.Stretch((RectTransform)overlay.transform, -100f, -100f, -100f, -100f);
            F.Group(overlay.gameObject, 1f);
            var transition = overlay.gameObject.AddComponent<TransitionOverlay>();

            // ---------------------------------------------------------------- wiring
            F.Set(controller, "dimOverlay", dim);
            F.Set(controller, "victoryFx", victoryFx.gameObject);
            F.Set(controller, "titleRibbon", ribbon);
            F.Set(controller, "ribbonVictory", F.Kit(KitRibbon));
            F.Set(controller, "ribbonDefeat", F.Kit(KitDark));
            F.Set(controller, "ribbonDraw", F.Kit(KitDark));
            F.Set(controller, "titleText", title);
            F.Set(controller, "arenaText", arena);
            F.Set(controller, "arenaPill", arenaPill.gameObject);
            F.SetArray(controller, "stars", stars);
            F.SetArray(controller, "starGlows", glows);
            F.SetColor(controller, "starOff", StarOff);
            F.Set(controller, "playerCoreBar", pBar);
            F.Set(controller, "enemyCoreBar", eBar);
            F.Set(controller, "playerCoreText", pText);
            F.Set(controller, "enemyCoreText", eText);
            F.Set(controller, "damageText", damage);
            F.Set(controller, "coreLeftLabel", coreLeftLabel);
            F.Set(controller, "coreLeftText", coreLeft);
            F.Set(controller, "aliveText", alive);
            F.Set(controller, "accuracyText", accuracy);
            F.Set(controller, "accuracyRow", accuracyRow);
            F.SetArray(controller, "tiles", tiles);
            F.Set(controller, "chestNoteText", chestNote);
            F.Set(controller, "unlockPanel", unlock.gameObject);
            F.Set(controller, "unlockText", uText);
            F.Set(controller, "unlockIcon", uIcon);
            F.Set(controller, "tutorialHintText", tutHint);
            F.SetArray(controller, "introGroups", new Object[] { headerGroup, stripGroup, statsGroup, rewardsGroup, buttonsGroup });
            F.Set(controller, "continueButton", cont);
            F.Set(controller, "retryButton", retry);
            F.Set(controller, "transition", transition);
            WireRewardSprites(controller);

            F.EventSystem();
            SceneBuildUtility.Save(scene, "04_Results");
        }

        // ------------------------------------------------------------------ backdrop

        /// Menu background (aspect-correct, width-driven cover anchored to the bottom), dim overlay and edge vignette.
        /// Returns the dim image (tinted per outcome at runtime).
        static Image BuildBackdrop(Transform canvas)
        {
            var root = F.Rect("Backdrop", canvas);
            F.Stretch(root);

            var sprite = F.Sprite(BackgroundPath);
            if (sprite == null) Debug.LogWarning("[TG] Results background missing: " + BackgroundPath);
            var bg = F.Image("Background", root, sprite, sprite != null ? Color.white : new Color(0.1f, 0.14f, 0.2f), true, false);
            var rt = (RectTransform)bg.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, BackgroundPivotY);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(80f, 2420f);
            var fitter = bg.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
            fitter.aspectRatio = sprite != null ? sprite.rect.width / Mathf.Max(1f, sprite.rect.height) : 1f;

            var dim = F.Image("Dim", root, F.Ui("white"), DimVictory, true, false);
            F.Stretch((RectTransform)dim.transform, -20f, -20f, -20f, -20f);

            var vignette = F.Rect("Vignette", root);
            F.Stretch(vignette);
            var glow = F.Ui("glow");
            Blob("Left", vignette, glow, new Vector2(0f, 0.5f), new Vector2(1000f, 1900f));
            Blob("Right", vignette, glow, new Vector2(1f, 0.5f), new Vector2(1000f, 1900f));
            Blob("Top", vignette, glow, new Vector2(0.5f, 1f), new Vector2(3400f, 620f));
            Blob("Bottom", vignette, glow, new Vector2(0.5f, 0f), new Vector2(3400f, 620f));
            return dim;
        }

        static void Blob(string name, Transform parent, Sprite sprite, Vector2 anchor, Vector2 size)
        {
            var img = F.Image(name, parent, sprite, Vignette, true, false);
            F.Anchor((RectTransform)img.transform, anchor, Vector2.zero, size, new Vector2(0.5f, 0.5f));
        }

        // ------------------------------------------------------------------ header pieces

        static void Burst(string name, Transform parent, Vector2 pos, float angle)
        {
            var img = F.Image(name, parent, F.Kit(KitStarburst), Color.white, false, false);
            var rt = F.Anchor((RectTransform)img.transform, new Vector2(0.5f, 1f), pos, new Vector2(104f, 92f), new Vector2(0.5f, 0.5f));
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        /// Star slot: honey glow (alpha 0 until earned) → dark socket (1.1x) → the star itself (starOff until earned).
        static Image StarSlot(string name, Transform parent, Vector2 pos, float size, out Image glow)
        {
            var slot = F.Rect(name, parent);
            F.Anchor(slot, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            glow = F.Image("Glow", slot, F.Ui("glow"), StarGlow, true, false);
            F.Anchor((RectTransform)glow.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 2f, size * 2f));
            var socket = F.Image("Socket", slot, F.Icon("star"), StarSocket, false, false);
            F.Anchor((RectTransform)socket.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 1.1f, size * 1.1f));
            var fill = F.Image("Fill", slot, F.Icon("star"), StarOff, false, false);
            F.Stretch((RectTransform)fill.transform);
            return fill;
        }

        static Image KitIcon(string name, Transform parent, int kit, Vector2 pos, float size)
        {
            var img = F.Image(name, parent, F.Kit(kit), Color.white, false, false);
            F.Anchor((RectTransform)img.transform, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            return img;
        }

        static UIFill IntegrityBar(string name, Transform parent, Vector2 pos, Color fill, out TMP_Text text)
        {
            var bar = F.SlicedBar(name, parent, BarBack, fill, 5f, true);
            F.Anchor((RectTransform)bar.transform, new Vector2(0.5f, 0.5f), pos, new Vector2(450f, 40f));
            text = F.OutlinedText("Text", bar.transform, null, 24f, F.TextLight, TextAlignmentOptions.Center, "");
            F.Stretch((RectTransform)text.transform, 12f, 0f, 12f, 0f);
            return bar;
        }

        // ------------------------------------------------------------------ panels

        /// Kit dark plate with a honey header and a thin divider.
        static Image Panel(string name, Transform parent, Vector2 pos, Vector2 size, string headerKey)
        {
            var panel = F.Image(name, parent, F.Kit(KitDark), PanelTint, true, false);
            F.Anchor((RectTransform)panel.transform, new Vector2(0.5f, 0.5f), pos, size);
            var head = F.OutlinedText("Header", panel.transform, headerKey, 32f, F.Honey, TextAlignmentOptions.Center);
            F.AnchorStretchX((RectTransform)head.transform, -14f, 44f, 24f, 24f, 1f, 1f);
            AutoSize(head, 22f, 32f);
            var divider = F.Image("Divider", panel.transform, F.Ui("white"), new Color(1f, 1f, 1f, 0.08f), true, false);
            F.AnchorStretchX((RectTransform)divider.transform, -66f, 3f, 30f, 30f, 1f, 1f);
            return panel;
        }

        /// Stats row: stripe, kit icon, localized label (left), honey value (right). Returns the value text.
        static TMP_Text StatRow(string name, Transform parent, int kitIcon, string labelKey, out LocalizedTMPText labelLoc, out GameObject row)
        {
            var bg = F.Image(name, parent, F.Ui("soft"), StripeColor, true, false);
            ((RectTransform)bg.transform).sizeDelta = new Vector2(530f, 58f);
            var icon = F.Image("Icon", bg.transform, F.Kit(kitIcon), Color.white, false, false);
            F.Anchor((RectTransform)icon.transform, new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(48f, 48f), new Vector2(0f, 0.5f));
            var label = F.Text("Label", bg.transform, labelKey, 26f, F.TextLight, TextAlignmentOptions.MidlineLeft, false);
            F.Stretch((RectTransform)label.transform, 72f, 0f, 190f, 0f);
            AutoSize(label, 18f, 26f);
            labelLoc = label.GetComponent<LocalizedTMPText>();
            var value = F.OutlinedText("Value", bg.transform, null, 30f, F.Honey, TextAlignmentOptions.MidlineRight, "0");
            F.Stretch((RectTransform)value.transform, 346f, 0f, 16f, 0f);
            AutoSize(value, 20f, 30f);
            row = bg.gameObject;
            return value;
        }

        static RewardItemView RewardTile(string name, Transform parent)
        {
            var bg = F.Image(name, parent, F.Kit(KitDark), Color.white, true, false);
            bg.pixelsPerUnitMultiplier = 1.35f;
            ((RectTransform)bg.transform).sizeDelta = new Vector2(TileW, TileH);
            F.Group(bg.gameObject, 1f);
            var shine = F.Image("Shine", bg.transform, F.Ui("glow"), new Color(1f, 1f, 1f, 0.08f), true, false);
            F.Anchor((RectTransform)shine.transform, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(130f, 130f), new Vector2(0.5f, 0.5f));
            var icon = F.Image("Icon", bg.transform, null, Color.white, false, false);
            F.Anchor((RectTransform)icon.transform, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(92f, 92f), new Vector2(0.5f, 1f));
            var label = F.OutlinedText("Label", bg.transform, null, 32f, F.TextLight, TextAlignmentOptions.Center, "");
            F.AnchorStretchX((RectTransform)label.transform, 40f, 40f, 6f, 6f, 0f, 0f);
            AutoSize(label, 20f, 32f);
            var sub = F.Text("Sub", bg.transform, null, 19f, SubText, TextAlignmentOptions.Center, false, "");
            F.AnchorStretchX((RectTransform)sub.transform, 12f, 28f, 6f, 6f, 0f, 0f);
            AutoSize(sub, 13f, 19f);
            var newBadge = F.Image("NewBadge", bg.transform, F.Ui("pill"), F.Honey, true, false);
            F.Anchor((RectTransform)newBadge.transform, new Vector2(0.5f, 1f), new Vector2(0f, 10f), new Vector2(80f, 30f), new Vector2(0.5f, 1f));
            var nt = F.OutlinedText("Text", newBadge.transform, "ui_new", 18f, F.TextDark);
            F.Stretch((RectTransform)nt.transform);
            newBadge.gameObject.SetActive(false);
            var view = bg.gameObject.AddComponent<RewardItemView>();
            F.Set(view, "icon", icon);
            F.Set(view, "label", label);
            F.Set(view, "sub", sub);
            F.Set(view, "newBadge", newBadge.gameObject);
            F.Set(view, "group", bg.GetComponent<CanvasGroup>());
            return view;
        }

        // ------------------------------------------------------------------ buttons

        /// Kit button with uniformly scaled caps and the label centred on the coloured part (above the dark band).
        static Button KitButton(string name, Transform parent, int kit, string labelKey, float fontSize, Vector2 size, float sourceHeight)
        {
            var btn = F.KitButton(name, parent, kit, labelKey, fontSize);
            ((RectTransform)btn.transform).sizeDelta = size;
            if (btn.targetGraphic is Image img) img.pixelsPerUnitMultiplier = sourceHeight / size.y;
            var label = btn.transform.Find("Label") as RectTransform;
            if (label != null) F.Stretch(label, 20f, 36f, 20f, 6f);
            return btn;
        }

        // ------------------------------------------------------------------ helpers

        static void AutoSize(TMP_Text t, float min, float max)
        {
            t.enableAutoSizing = true;
            t.fontSizeMin = min;
            t.fontSizeMax = max;
        }

        /// Reward icons from the menu kit (acorn coins, log sap, gem, trophy) at full colour; chest art by tier as fallback.
        static void WireRewardSprites(Component c)
        {
            var so = new UnityEditor.SerializedObject(c);
            var p = so.FindProperty("sprites");
            if (p == null) return;
            p.FindPropertyRelative("coin").objectReferenceValue = F.Kit(KitAcorn);
            p.FindPropertyRelative("sap").objectReferenceValue = F.Kit(KitLog);
            p.FindPropertyRelative("gem").objectReferenceValue = F.Kit(KitGem);
            p.FindPropertyRelative("trophy").objectReferenceValue = F.Kit(KitTrophy);
            p.FindPropertyRelative("card").objectReferenceValue = F.Icon("cards");
            p.FindPropertyRelative("chest").objectReferenceValue = F.Kit(KitChestByTier[0]);
            var byTier = p.FindPropertyRelative("chestByTier");
            if (byTier != null)
            {
                byTier.arraySize = KitChestByTier.Length;
                for (int i = 0; i < KitChestByTier.Length; i++) byTier.GetArrayElementAtIndex(i).objectReferenceValue = F.Kit(KitChestByTier[i]);
            }
            foreach (var tint in new[] { "coinTint", "sapTint", "gemTint", "trophyTint" })
            {
                var t = p.FindPropertyRelative(tint);
                if (t != null) t.colorValue = Color.white;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
