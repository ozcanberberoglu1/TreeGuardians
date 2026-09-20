using TMPro;
using TreeGuardians.SceneFlow;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.Editor.SceneBuild
{
    /// 01_Loading: real async progress bar, rotating tips, leaf spinner, parallax leaves.
    public static class LoadingSceneBuilder
    {
        public static void Build()
        {
            var scene = SceneBuildUtility.NewScene("01_Loading");
            UIFactory.Root("Loading_Root");
            UIFactory.Camera2D("Main Camera", 5.4f, new Color(0.1f, 0.16f, 0.14f));

            var canvas = UIFactory.Canvas("LoadingCanvas");
            var rootGroup = UIFactory.Group(canvas.gameObject, 1f);
            var bg = UIFactory.Image("Background", canvas.transform, UIFactory.Ui("white"), new Color(0.12f, 0.2f, 0.17f), false, false);
            UIFactory.Stretch((RectTransform)bg.transform, -50f, -50f, -50f, -50f);

            var leavesLayer = UIFactory.Rect("ParallaxLeaves_Back", canvas.transform);
            UIFactory.Stretch(leavesLayer);
            var rng = new System.Random(7);
            for (int i = 0; i < 14; i++)
            {
                var leaf = UIFactory.Image("Leaf_" + i, leavesLayer, UIFactory.Sprite(ArtPaths.Vfx("leaf")), new Color(0.35f, 0.6f, 0.35f, 0.35f), false, false);
                float size = 40f + (float)rng.NextDouble() * 60f;
                UIFactory.Anchor((RectTransform)leaf.transform, new Vector2((float)rng.NextDouble(), (float)rng.NextDouble()), Vector2.zero, new Vector2(size, size));
                leaf.transform.localRotation = Quaternion.Euler(0f, 0f, (float)rng.NextDouble() * 360f);
            }

            var silhouette = UIFactory.Image("TreeSilhouette", canvas.transform, UIFactory.Sprite(ArtPaths.Logo("game")), new Color(0.2f, 0.3f, 0.25f, 0.5f), false, false);
            UIFactory.Anchor((RectTransform)silhouette.transform, new Vector2(0.5f, 0.6f), Vector2.zero, new Vector2(900f, 450f));

            var safe = UIFactory.SafeArea(canvas.transform);
            var logo = UIFactory.Image("GameLogo", safe, UIFactory.Sprite(ArtPaths.Logo("game")), Color.white, false, false);
            UIFactory.Anchor((RectTransform)logo.transform, new Vector2(0.5f, 0.66f), Vector2.zero, new Vector2(640f, 320f));
            var title = UIFactory.OutlinedText("GameTitle", safe, null, 72f, UIFactory.Honey, TextAlignmentOptions.Center, "TREE GUARDIANS");
            UIFactory.Anchor((RectTransform)title.transform, new Vector2(0.5f, 0.4f), Vector2.zero, new Vector2(1200f, 100f));

            var bar = UIFactory.FillBar("LoadingBar_Back", safe, UIFactory.PanelMid, UIFactory.Green, out var fill);
            fill.name = "LoadingBar_Fill";
            UIFactory.Anchor((RectTransform)bar.transform, new Vector2(0.5f, 0.24f), Vector2.zero, new Vector2(1100f, 48f));
            var percent = UIFactory.Text("LoadingPercentText", safe, null, 30f, UIFactory.TextLight, TextAlignmentOptions.Center, true, "0%");
            UIFactory.Anchor((RectTransform)percent.transform, new Vector2(0.5f, 0.19f), Vector2.zero, new Vector2(300f, 44f));
            var tip = UIFactory.Text("TipText", safe, null, 30f, new Color(0.85f, 0.9f, 0.85f), TextAlignmentOptions.Center, false, "");
            UIFactory.Anchor((RectTransform)tip.transform, new Vector2(0.5f, 0.1f), Vector2.zero, new Vector2(1600f, 60f));
            var spinner = UIFactory.Image("Spinner_Leaf", safe, UIFactory.Icon("leaf"), UIFactory.Green, false, false);
            UIFactory.Anchor((RectTransform)spinner.transform, new Vector2(1f, 0f), new Vector2(-60f, 60f), new Vector2(90f, 90f), new Vector2(1f, 0f));

            var controller = UIFactory.Root("LoadingController").AddComponent<LoadingController>();
            UIFactory.Set(controller, "barFill", fill);
            UIFactory.Set(controller, "percentText", percent);
            UIFactory.Set(controller, "tipText", tip);
            UIFactory.Set(controller, "spinner", spinner.transform);
            UIFactory.Set(controller, "rootGroup", rootGroup);
            UIFactory.Set(controller, "leavesLayer", leavesLayer);

            UIFactory.EventSystem();
            SceneBuildUtility.Save(scene, "01_Loading");
        }
    }
}
