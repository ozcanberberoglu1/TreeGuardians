using TMPro;
using TreeGuardians.Battle;
using TreeGuardians.UI;
using UnityEngine;
using UnityEngine.UI;
using F = TreeGuardians.Editor.UIFactory;

namespace TreeGuardians.Editor.SceneBuild
{
    /// 04_Results: outcome title, tree summaries, stars, stats, reward capsules, buttons, unlock panel.
    public static class ResultsSceneBuilder
    {
        public static void Build()
        {
            var scene = SceneBuildUtility.NewScene("04_Results");
            F.Root("Results_Root");
            F.Camera2D("ResultsCamera", 5.4f, new Color(0.1f, 0.14f, 0.2f));
            var canvas = F.Canvas("ResultsCanvas");
            var bg = F.Image("Background", canvas.transform, F.Ui("white"), new Color(0.1f, 0.14f, 0.2f), false, false);
            F.Stretch((RectTransform)bg.transform, -50f, -50f, -50f, -50f);
            var safe = F.SafeArea(canvas.transform);
            var controller = F.Root("ResultsController").AddComponent<ResultsController>();

            var title = F.OutlinedText("Title", safe, null, 84f, F.Honey, TextAlignmentOptions.Center, "VICTORY");
            F.AnchorStretchX((RectTransform)title.transform, -30f, 110f, 200f, 200f, 1f, 1f);
            var arena = F.Text("Arena", safe, null, 30f, F.TextLight, TextAlignmentOptions.Center, false, "");
            F.AnchorStretchX((RectTransform)arena.transform, -140f, 40f, 200f, 200f, 1f, 1f);

            var starsRow = F.HorizontalGroup("Stars", safe, 20f, TextAnchor.MiddleCenter);
            F.Anchor(starsRow, new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(400f, 110f), new Vector2(0.5f, 1f));
            var stars = new Object[3];
            for (int i = 0; i < 3; i++)
            {
                var s = F.Image("Star_" + i, starsRow, F.Icon("star"), new Color(1f, 1f, 1f, 0.2f), false, false);
                ((RectTransform)s.transform).sizeDelta = new Vector2(i == 1 ? 110f : 90f, i == 1 ? 110f : 90f);
                stars[i] = s;
            }

            var summary = F.Rect("TreeSummary", safe);
            F.Anchor(summary, new Vector2(0.5f, 1f), new Vector2(0f, -320f), new Vector2(1400f, 80f), new Vector2(0.5f, 1f));
            var pBar = F.FillBar("PlayerCore", summary, F.PanelMid, F.Green, out var pFill);
            F.Anchor((RectTransform)pBar.transform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(600f, 44f), new Vector2(0f, 0.5f));
            var pText = F.OutlinedText("Text", pBar.transform, null, 24f, F.TextLight, TextAlignmentOptions.Center, ""); F.Stretch((RectTransform)pText.transform);
            var eBar = F.FillBar("EnemyCore", summary, F.PanelMid, F.Red, out var eFill);
            F.Anchor((RectTransform)eBar.transform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(600f, 44f), new Vector2(1f, 0.5f));
            var eText = F.OutlinedText("Text", eBar.transform, null, 24f, F.TextLight, TextAlignmentOptions.Center, ""); F.Stretch((RectTransform)eText.transform);
            var vs = F.OutlinedText("VS", summary, null, 34f, F.Honey, TextAlignmentOptions.Center, "VS");
            F.Anchor((RectTransform)vs.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 50f));

            var stats = F.VerticalGroup("Stats", safe, 8f, TextAnchor.UpperLeft);
            F.Anchor(stats, new Vector2(0f, 0.5f), new Vector2(120f, -40f), new Vector2(700f, 260f), new Vector2(0f, 0.5f));
            var damage = Line("Damage", stats); var coreLeft = Line("CoreLeft", stats); var alive = Line("Alive", stats); var accuracy = Line("Accuracy", stats);

            var trophyPill = F.Image("TrophyPill", safe, F.Ui("pill"), F.PanelDark, true, false);
            F.Anchor((RectTransform)trophyPill.transform, new Vector2(0.5f, 1f), new Vector2(0f, -420f), new Vector2(260f, 70f), new Vector2(0.5f, 1f));
            var tIcon = F.Image("Icon", trophyPill.transform, F.Icon("trophy"), F.Honey, false, false);
            F.Anchor((RectTransform)tIcon.transform, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(50f, 50f), new Vector2(0f, 0.5f));
            var trophy = F.OutlinedText("Value", trophyPill.transform, null, 32f, F.TextLight, TextAlignmentOptions.Center, "+0");
            F.Stretch((RectTransform)trophy.transform, 60f, 0f, 16f, 0f);

            var rewardsLabel = F.Text("RewardsLabel", safe, "results_rewards", 30f, F.Honey, TextAlignmentOptions.MidlineLeft, true);
            F.Anchor((RectTransform)rewardsLabel.transform, new Vector2(1f, 0.5f), new Vector2(-120f, 150f), new Vector2(900f, 40f), new Vector2(1f, 0.5f));
            var tilesRow = F.HorizontalGroup("RewardTiles", safe, 12f, TextAnchor.MiddleLeft);
            F.Anchor(tilesRow, new Vector2(1f, 0.5f), new Vector2(-120f, 0f), new Vector2(900f, 230f), new Vector2(1f, 0.5f));
            var tiles = new Object[6];
            for (int i = 0; i < 6; i++)
            {
                var t = RewardTile("Tile_" + i, tilesRow);
                t.gameObject.SetActive(false);
                tiles[i] = t;
            }
            var chestNote = F.Text("ChestNote", safe, null, 22f, F.Red, TextAlignmentOptions.MidlineLeft, false, "");
            F.Anchor((RectTransform)chestNote.transform, new Vector2(1f, 0.5f), new Vector2(-120f, -150f), new Vector2(900f, 34f), new Vector2(1f, 0.5f));
            var tutHint = F.OutlinedText("TutorialHint", safe, null, 28f, F.Honey, TextAlignmentOptions.Center, "");
            F.Anchor((RectTransform)tutHint.transform, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1200f, 40f), new Vector2(0.5f, 0f));
            tutHint.gameObject.SetActive(false);
            F.Set(controller, "tutorialHintText", tutHint);

            var unlock = F.Image("UnlockPanel", safe, F.Ui("panel"), F.PanelMid, true, false);
            F.Anchor((RectTransform)unlock.transform, new Vector2(0f, 0.5f), new Vector2(120f, -220f), new Vector2(700f, 110f), new Vector2(0f, 0.5f));
            var uIcon = F.Image("Icon", unlock.transform, null, Color.white, false, false);
            F.Anchor((RectTransform)uIcon.transform, new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(84f, 84f), new Vector2(0f, 0.5f));
            var uText = F.OutlinedText("Text", unlock.transform, null, 28f, F.Honey, TextAlignmentOptions.MidlineLeft, "");
            F.Stretch((RectTransform)uText.transform, 112f, 0f, 16f, 0f);
            unlock.gameObject.SetActive(false);

            var buttons = F.HorizontalGroup("Buttons", safe, 24f, TextAnchor.MiddleCenter);
            F.AnchorStretchX(buttons, 30f, 110f, 200f, 200f, 0f, 0f);
            var home = F.Button("HomeButton", buttons, F.PanelMid, "ui_home", 30f); ((RectTransform)home.transform).sizeDelta = new Vector2(300f, 96f);
            var retry = F.Button("RetryButton", buttons, F.Blue, "ui_retry", 30f); ((RectTransform)retry.transform).sizeDelta = new Vector2(300f, 96f);
            var cont = F.Button("ContinueButton", buttons, F.Honey, "ui_continue", 34f); ((RectTransform)cont.transform).sizeDelta = new Vector2(420f, 96f);

            var overlay = F.Image("TransitionOverlay", canvas.transform, F.Ui("white"), Color.black, false, true);
            F.Stretch((RectTransform)overlay.transform, -100f, -100f, -100f, -100f);
            F.Group(overlay.gameObject, 1f);
            var transition = overlay.gameObject.AddComponent<TransitionOverlay>();

            F.Set(controller, "titleText", title); F.Set(controller, "arenaText", arena); F.SetArray(controller, "stars", stars);
            F.Set(controller, "playerCoreFill", pFill); F.Set(controller, "enemyCoreFill", eFill); F.Set(controller, "playerCoreText", pText); F.Set(controller, "enemyCoreText", eText);
            F.Set(controller, "damageText", damage); F.Set(controller, "coreLeftText", coreLeft); F.Set(controller, "aliveText", alive); F.Set(controller, "accuracyText", accuracy); F.Set(controller, "trophyText", trophy);
            F.SetArray(controller, "tiles", tiles); F.Set(controller, "chestNoteText", chestNote);
            F.Set(controller, "unlockPanel", unlock.gameObject); F.Set(controller, "unlockText", uText); F.Set(controller, "unlockIcon", uIcon);
            F.Set(controller, "continueButton", cont); F.Set(controller, "retryButton", retry); F.Set(controller, "homeButton", home); F.Set(controller, "transition", transition);
            WireRewardSprites(controller);

            F.EventSystem();
            SceneBuildUtility.Save(scene, "04_Results");
        }

        static TMP_Text Line(string name, Transform parent)
        {
            var t = F.Text(name, parent, null, 28f, F.TextLight, TextAlignmentOptions.MidlineLeft, false, "");
            ((RectTransform)t.transform).sizeDelta = new Vector2(700f, 40f);
            return t;
        }

        static RewardItemView RewardTile(string name, Transform parent)
        {
            var bg = F.Image(name, parent, F.Ui("card"), F.PanelMid, true, false);
            ((RectTransform)bg.transform).sizeDelta = new Vector2(140f, 200f);
            F.Group(bg.gameObject, 1f);
            var icon = F.Image("Icon", bg.transform, null, Color.white, false, false);
            F.Anchor((RectTransform)icon.transform, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(90f, 90f), new Vector2(0.5f, 1f));
            var label = F.OutlinedText("Label", bg.transform, null, 24f, F.TextLight, TextAlignmentOptions.Center, "");
            F.AnchorStretchX((RectTransform)label.transform, 36f, 34f, 4f, 4f, 0f, 0f);
            var sub = F.Text("Sub", bg.transform, null, 18f, new Color(0.85f, 0.88f, 0.9f), TextAlignmentOptions.Center, false, "");
            F.AnchorStretchX((RectTransform)sub.transform, 8f, 28f, 4f, 4f, 0f, 0f);
            var newBadge = F.Image("NewBadge", bg.transform, F.Ui("pill"), F.Honey, true, false);
            F.Anchor((RectTransform)newBadge.transform, new Vector2(0.5f, 1f), new Vector2(0f, 10f), new Vector2(80f, 30f), new Vector2(0.5f, 1f));
            var nt = F.OutlinedText("Text", newBadge.transform, "ui_new", 18f, F.TextDark); F.Stretch((RectTransform)nt.transform);
            var view = bg.gameObject.AddComponent<RewardItemView>();
            F.Set(view, "icon", icon); F.Set(view, "label", label); F.Set(view, "sub", sub); F.Set(view, "newBadge", newBadge.gameObject); F.Set(view, "group", bg.GetComponent<CanvasGroup>());
            return view;
        }

        static void WireRewardSprites(Component c)
        {
            var so = new UnityEditor.SerializedObject(c);
            var p = so.FindProperty("sprites");
            if (p == null) return;
            p.FindPropertyRelative("coin").objectReferenceValue = F.Icon("coin");
            p.FindPropertyRelative("sap").objectReferenceValue = F.Icon("sap");
            p.FindPropertyRelative("gem").objectReferenceValue = F.Icon("gem");
            p.FindPropertyRelative("trophy").objectReferenceValue = F.Icon("trophy");
            p.FindPropertyRelative("card").objectReferenceValue = F.Icon("cards");
            p.FindPropertyRelative("chest").objectReferenceValue = F.Sprite(ArtPaths.Chest("twig", false));
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
