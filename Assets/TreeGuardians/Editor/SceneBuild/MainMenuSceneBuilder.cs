using TMPro;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.UI;
using TreeGuardians.UI.Menu;
using TreeGuardians.UI.Popups;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using F = TreeGuardians.Editor.UIFactory;

namespace TreeGuardians.Editor.SceneBuild
{
    /// 02_MainMenu: world environment + center tree, one canvas with every panel and popup pre-authored.
    public static class MainMenuSceneBuilder
    {
        static readonly Vector2 PanelSize = new Vector2(1900f, 940f);

        public static void Build()
        {
            var scene = SceneBuildUtility.NewScene("02_MainMenu");
            var tree = AssetDatabase.LoadAssetAtPath<TreeDefinition>("Assets/TreeGuardians/ScriptableObjects/Trees/Tree_Player.asset");

            F.Root("MainMenu_Root");
            var menuCam = F.Camera2D("Main Camera", 5.4f, new Color(0.55f, 0.8f, 0.98f));
            var fitter = menuCam.gameObject.AddComponent<CameraFitter>();
            fitter.halfWidth = 10.5f;
            fitter.minOrthoSize = 5.4f;
            var envAnimator = BuildEnvironment(tree, out var centerTree);

            var canvas = F.Canvas("MenuCanvas");
            var safe = F.SafeArea(canvas.transform);
            var controller = F.Root("MenuUIController").AddComponent<MenuUIController>();

            BuildTopBar(safe, controller);
            BuildRails(safe, controller);
            BuildCenterPanel(safe, controller, centerTree);
            BuildBottomBar(safe, controller);

            var panels = F.Rect("Panels", safe);
            F.Stretch(panels);
            var guardians = BuildGuardiansPanel(panels);
            var detail = BuildGuardianDetailPanel(panels);
            var treePanel = BuildTreePanel(panels);
            var tools = BuildToolsPanel(panels);
            var prep = BuildBattlePrepPanel(panels);
            var chests = BuildChestsPanel(panels);
            var shop = BuildShopPanel(panels);
            var quests = BuildQuestsPanel(panels, out var dailyRefHolder);
            var ranking = BuildRankingPanel(panels);
            var profile = BuildProfilePanel(panels);
            var settings = BuildSettingsPanel(panels);
            var debug = BuildDebugPanel(panels);

            var popupLayer = F.Rect("PopupLayer", safe);
            F.Stretch(popupLayer);
            var dimmer = F.Image("Dimmer", popupLayer, F.Ui("white"), new Color(0f, 0f, 0f, 1f), false, true);
            F.Stretch((RectTransform)dimmer.transform, -100f, -100f, -100f, -100f);
            F.Group(dimmer.gameObject, 0f);
            dimmer.gameObject.AddComponent<PopupDimmer>();
            var confirm = BuildConfirmPopup(popupLayer);
            var reward = BuildRewardPopup(popupLayer);
            var insufficient = BuildInsufficientPopup(popupLayer);
            var unlock = BuildUnlockPopup(popupLayer);
            var daily = BuildDailyRewardPopup(popupLayer);
            var chestOpen = BuildChestOpenPopup(popupLayer);
            var connection = BuildConnectionPopup(popupLayer);
            F.Set(quests, "dailyPopupRef", daily);

            var toastLayer = F.Rect("ToastLayer", safe);
            F.Stretch(toastLayer);
            var toast = BuildToast(toastLayer);

            var overlay = F.Image("TransitionOverlay", canvas.transform, F.Ui("white"), Color.black, false, true);
            F.Stretch((RectTransform)overlay.transform, -100f, -100f, -100f, -100f);
            F.Group(overlay.gameObject, 1f);
            var transition = overlay.gameObject.AddComponent<TransitionOverlay>();

            F.Set(controller, "guardiansPanel", guardians);
            F.Set(controller, "guardianDetailPanel", detail);
            F.Set(controller, "treeUpgradePanel", treePanel);
            F.Set(controller, "toolsPanel", tools);
            F.Set(controller, "battlePrepPanel", prep);
            F.Set(controller, "chestsPanel", chests);
            F.Set(controller, "shopPanel", shop);
            F.Set(controller, "questsPanel", quests);
            F.Set(controller, "rankingPanel", ranking);
            F.Set(controller, "profilePanel", profile);
            F.Set(controller, "settingsPanel", settings);
            F.Set(controller, "debugPanel", debug);
            F.Set(controller, "confirmPopup", confirm);
            F.Set(controller, "rewardPopup", reward);
            F.Set(controller, "insufficientPopup", insufficient);
            F.Set(controller, "unlockPopup", unlock);
            F.Set(controller, "dailyRewardPopup", daily);
            F.Set(controller, "chestOpenPopup", chestOpen);
            F.Set(controller, "connectionPopup", connection);
            F.Set(controller, "toast", toast);
            F.Set(controller, "transition", transition);

            F.EventSystem();
            SceneBuildUtility.Save(scene, "02_MainMenu");
        }

        // ------------------------------------------------------------------ environment
        static MenuEnvironmentAnimator BuildEnvironment(TreeDefinition tree, out CenterTreeDisplay centerTree)
        {
            var env = F.Root("Environment");
            var sky = F.Child("SkyGradient", env.transform, new Vector3(0f, 0f, 5f));
            sky.AddComponent<SpriteRenderer>().sortingOrder = -100;
            var grad = sky.AddComponent<GradientSprite>();
            F.SetColor(grad, "top", new Color(0.42f, 0.72f, 0.98f));
            F.SetColor(grad, "bottom", new Color(0.85f, 0.95f, 1f));

            var cloudsRoot = F.Child("CloudLayer_Far", env.transform);
            var clouds = new Object[3];
            for (int i = 0; i < 3; i++)
            {
                var c = F.WorldSprite("Cloud_" + i, cloudsRoot.transform, F.Sprite(ArtPaths.Arena("clouds")), new Color(1f, 1f, 1f, 0.9f), -90, new Vector3(-10f + i * 10f, 2.6f + (i % 2) * 0.8f, 4f));
                c.transform.localScale = new Vector3(2.2f, 2.2f, 1f);
                clouds[i] = c.transform;
            }
            var mountains = F.WorldSprite("MountainLayer", env.transform, F.Sprite(ArtPaths.Arena("mountains")), new Color(0.56f, 0.66f, 0.82f), -80, new Vector3(0f, -0.4f, 3f));
            mountains.transform.localScale = new Vector3(5f, 2.2f, 1f);
            var forestBack = F.WorldSprite("ForestLayer_Back", env.transform, F.Sprite(ArtPaths.Arena("forest_back")), new Color(0.28f, 0.5f, 0.38f), -70, new Vector3(0f, -1.7f, 2f));
            forestBack.transform.localScale = new Vector3(5f, 2.2f, 1f);
            var forestMid = F.WorldSprite("ForestLayer_Mid", env.transform, F.Sprite(ArtPaths.Arena("forest_mid")), new Color(0.32f, 0.58f, 0.36f), -60, new Vector3(0f, -2.6f, 1f));
            forestMid.transform.localScale = new Vector3(5f, 2.4f, 1f);

            var leavesRoot = F.Child("FloatingLeaves", env.transform);
            var leaves = new Object[8];
            var rng = new System.Random(3);
            for (int i = 0; i < 8; i++)
            {
                var l = F.WorldSprite("Leaf_" + i, leavesRoot.transform, F.Sprite(ArtPaths.Vfx("leaf")), new Color(0.4f + (float)rng.NextDouble() * 0.3f, 0.7f, 0.35f, 0.85f), 50, new Vector3(-8f + (float)rng.NextDouble() * 16f, -4f + (float)rng.NextDouble() * 9f, 0f));
                l.transform.localScale = Vector3.one * (0.8f + (float)rng.NextDouble() * 0.8f);
                leaves[i] = l.transform;
            }
            var ground = F.WorldSprite("StaticGround", env.transform, F.Sprite(ArtPaths.Arena("ground")), new Color(0.42f, 0.66f, 0.32f), -50, new Vector3(0f, -3.2f, 0f));
            ground.transform.localScale = new Vector3(5f, 2.5f, 1f);

            var treeRoot = F.Child("CenterTree_Display", env.transform, new Vector3(0f, -3.3f, 0f));
            centerTree = treeRoot.AddComponent<CenterTreeDisplay>();
            var visuals = tree != null ? tree.GetVisuals(TreeVisualTier.Sprouting) : null;
            var barkColor = visuals != null ? visuals.barkColor : new Color(0.6f, 0.45f, 0.3f);
            var leafColor = visuals != null ? visuals.leafColor : new Color(0.5f, 0.8f, 0.45f);
            var root = F.WorldSprite("Root", treeRoot.transform, F.Sprite(ArtPaths.TreePart("root")), barkColor * 0.85f, 1, new Vector3(0f, 0f, 0f));
            root.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
            var trunk = F.WorldSprite("Trunk", treeRoot.transform, F.Sprite(ArtPaths.Tree("trunk", 0)), barkColor, 2, new Vector3(0f, 0.1f, 0f));
            trunk.transform.localScale = new Vector3(0.6f, 1.0f, 1f);
            var branches = new Object[8];
            var guardians = new Object[8];
            for (int i = 0; i < 8; i++)
            {
                Vector2 slot = tree != null ? tree.guardianSlotPositions[i] * 0.6f : new Vector2(i < 4 ? 1.5f : -1.5f, 1f + (i % 4) * 1f);
                bool back = slot.x < 0f;
                var b = F.WorldSprite("Branch_" + i, treeRoot.transform, F.Sprite(ArtPaths.Tree("branch", 0)), barkColor, 3, new Vector3(back ? slot.x + 0.4f : slot.x - 0.4f, slot.y - 0.35f, 0f));
                b.transform.localScale = new Vector3(back ? -0.42f : 0.42f, 0.42f, 1f);
                branches[i] = b;
                var g = F.WorldSprite("Guardian_" + i, treeRoot.transform, null, Color.white, 6, new Vector3(slot.x, slot.y - 0.15f, 0f));
                g.transform.localScale = new Vector3(back ? -0.34f : 0.34f, 0.34f, 1f);
                g.enabled = false;
                guardians[i] = g;
            }
            var canopy = F.WorldSprite("Canopy", treeRoot.transform, F.Sprite(ArtPaths.TreePart("canopy")), leafColor, 4, new Vector3(0f, 4.9f, 0f));
            canopy.transform.localScale = new Vector3(1.0f, 1.0f, 1f);

            F.Set(centerTree, "trunk", trunk);
            F.Set(centerTree, "canopy", canopy);
            F.Set(centerTree, "root", root);
            F.SetArray(centerTree, "branches", branches);
            F.SetArray(centerTree, "guardianSprites", guardians);
            F.Set(centerTree, "breathTarget", treeRoot.transform);

            var animator = F.Root("MenuEnvironmentAnimator").AddComponent<MenuEnvironmentAnimator>();
            F.SetArray(animator, "cloudLayers", clouds);
            SetFloatArray(animator, "cloudSpeeds", new[] { 0.25f, 0.18f, 0.32f });
            F.SetArray(animator, "leaves", leaves);
            return animator;
        }

        static void SetFloatArray(Component c, string field, float[] values)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(field);
            if (p == null) return;
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).floatValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetNested(Component c, string parent, string field, Object value)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(parent);
            var f = p != null ? p.FindPropertyRelative(field) : null;
            if (f == null) { Debug.LogWarning($"[MenuBuilder] {c.GetType().Name}.{parent}.{field} not found"); return; }
            f.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireRewardSprites(Component c)
        {
            SetNested(c, "sprites", "coin", F.Icon("coin"));
            SetNested(c, "sprites", "sap", F.Icon("sap"));
            SetNested(c, "sprites", "gem", F.Icon("gem"));
            SetNested(c, "sprites", "trophy", F.Icon("trophy"));
            SetNested(c, "sprites", "card", F.Icon("cards"));
            SetNested(c, "sprites", "chest", F.Sprite(ArtPaths.Chest("twig", false)));
        }

        // ------------------------------------------------------------------ top bar
        static void BuildTopBar(RectTransform safe, MenuUIController controller)
        {
            var bar = F.Rect("TopBar", safe);
            F.AnchorStretchX(bar, -14f, 100f, 20f, 20f, 1f, 1f);

            var level = F.Image("PlayerLevel", bar, F.Ui("pill"), F.PanelDark, true, false);
            F.Anchor((RectTransform)level.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(230f, 80f), new Vector2(0f, 0.5f));
            var levelIcon = F.Image("Icon", level.transform, F.Icon("level"), F.Honey, false, false);
            F.Anchor((RectTransform)levelIcon.transform, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(64f, 64f), new Vector2(0f, 0.5f));
            var levelText = F.OutlinedText("Value", level.transform, null, 36f, F.TextLight, TextAlignmentOptions.Center, "1");
            F.Stretch((RectTransform)levelText.transform, 76f, 0f, 16f, 0f);
            F.Set(controller, "playerLevelText", levelText);

            var counters = F.HorizontalGroup("Counters", bar, 16f, TextAnchor.MiddleCenter);
            F.Anchor(counters, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 90f));
            F.Set(controller, "coinsButton", Counter("CoinsCounter", counters, CurrencyType.Coins, F.Icon("coin"), F.Honey));
            F.Set(controller, "sapButton", Counter("SapCounter", counters, CurrencyType.Sap, F.Icon("sap"), F.Green));
            F.Set(controller, "gemsButton", Counter("GemsCounter", counters, CurrencyType.Gems, F.Icon("gem"), F.Purple));

            var settings = F.IconButton("SettingsButton", bar, F.Blue, F.Icon("settings"), F.TextLight);
            F.Anchor((RectTransform)settings.transform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(90f, 90f), new Vector2(1f, 0.5f));
            F.Set(controller, "settingsButton", settings);
        }

        static Button Counter(string name, Transform parent, CurrencyType type, Sprite icon, Color tint)
        {
            var btn = F.Button(name, parent, F.PanelDark, null, 30f, F.Ui("pill"));
            ((RectTransform)btn.transform).sizeDelta = new Vector2(300f, 80f);
            var ic = F.Image("Icon", btn.transform, icon, tint, false, false);
            F.Anchor((RectTransform)ic.transform, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(64f, 64f), new Vector2(0f, 0.5f));
            var value = F.OutlinedText("Value", btn.transform, null, 34f, F.TextLight, TextAlignmentOptions.MidlineRight, "0");
            F.Stretch((RectTransform)value.transform, 80f, 0f, 24f, 0f);
            var view = btn.gameObject.AddComponent<CurrencyCounterView>();
            F.Set(view, "currency", (float)(int)type);
            F.Set(view, "valueText", value);
            F.Set(view, "icon", ic);
            F.Set(view, "punchTarget", ic.transform);
            return btn;
        }

        // ------------------------------------------------------------------ rails
        static void BuildRails(RectTransform safe, MenuUIController controller)
        {
            var left = F.VerticalGroup("LeftRail", safe, 18f, TextAnchor.MiddleCenter);
            F.Anchor(left, new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(150f, 700f), new Vector2(0f, 0.5f));
            F.Set(controller, "shopButton", Rail("ShopButton", left, "menu_shop", F.Icon("shop"), F.Blue));
            F.Set(controller, "seasonButton", Rail("SeasonButton", left, "menu_season", F.Icon("season"), F.Blue));
            F.Set(controller, "eventsButton", Rail("EventsButton", left, "menu_events", F.Icon("events"), F.Blue));
            F.Set(controller, "inboxButton", Rail("InboxButton", left, "menu_inbox", F.Icon("inbox"), F.Blue));

            var right = F.VerticalGroup("RightRail", safe, 18f, TextAnchor.MiddleCenter);
            F.Anchor(right, new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(150f, 700f), new Vector2(1f, 0.5f));
            F.Set(controller, "treeButton", Rail("TreeButton", right, "menu_tree", F.Icon("tree"), F.Green));
            F.Set(controller, "guardiansButton", Rail("GuardiansButton", right, "menu_guardians", F.Icon("guardians"), F.Green));
            F.Set(controller, "toolsButton", Rail("ToolsButton", right, "menu_tools", F.Icon("tools"), F.Green));
            F.Set(controller, "profileButton", Rail("ProfileButton", right, "menu_profile", F.Icon("profile"), F.Blue));
        }

        static Button Rail(string name, Transform parent, string labelKey, Sprite icon, Color color)
        {
            var btn = F.IconButton(name, parent, color, icon, F.TextLight, labelKey, 20f);
            ((RectTransform)btn.transform).sizeDelta = new Vector2(150f, 150f);
            var label = btn.transform.Find("Label");
            if (label != null) F.AnchorStretchX((RectTransform)label, 6f, 34f, -14f, -14f, 0f, 0f);
            return btn;
        }

        // ------------------------------------------------------------------ center
        static void BuildCenterPanel(RectTransform safe, MenuUIController controller, CenterTreeDisplay centerTree)
        {
            var center = F.Rect("CenterTreePanel", safe);
            F.Anchor(center, new Vector2(0.5f, 0f), new Vector2(0f, 230f), new Vector2(900f, 150f), new Vector2(0.5f, 0f));

            var power = F.Image("TreePowerLabel", center, F.Ui("pill"), F.PanelDark, true, false);
            F.Anchor((RectTransform)power.transform, new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(300f, 70f), new Vector2(0f, 1f));
            var powerIcon = F.Image("Icon", power.transform, F.Icon("bolt"), F.Honey, false, false);
            F.Anchor((RectTransform)powerIcon.transform, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(54f, 54f), new Vector2(0f, 0.5f));
            var powerText = F.OutlinedText("Value", power.transform, null, 32f, F.TextLight, TextAlignmentOptions.MidlineRight, "0");
            F.Stretch((RectTransform)powerText.transform, 70f, 0f, 24f, 0f);
            F.Set(controller, "treePowerText", powerText);

            var arena = F.Image("ArenaBadge", center, F.Ui("pill"), F.PanelDark, true, false);
            F.Anchor((RectTransform)arena.transform, new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(420f, 70f), new Vector2(1f, 1f));
            var arenaIcon = F.Image("Icon", arena.transform, F.Icon("leaf"), F.Green, false, false);
            F.Anchor((RectTransform)arenaIcon.transform, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(54f, 54f), new Vector2(0f, 0.5f));
            var arenaName = F.OutlinedText("Name", arena.transform, null, 30f, F.TextLight, TextAlignmentOptions.MidlineLeft, "Arena");
            F.Stretch((RectTransform)arenaName.transform, 72f, 0f, 16f, 0f);
            F.Set(controller, "arenaNameText", arenaName);
            F.Set(controller, "arenaBadge", arenaIcon);

            var progress = F.FillBar("ArenaProgress", center, F.PanelMid, F.Honey, out var fill);
            F.Anchor((RectTransform)progress.transform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(560f, 34f), new Vector2(0.5f, 0f));
            var progressText = F.OutlinedText("Text", progress.transform, null, 22f, F.TextLight, TextAlignmentOptions.Center, "");
            F.Stretch((RectTransform)progressText.transform);
            F.Set(controller, "arenaProgressFill", fill);
            F.Set(controller, "arenaProgressText", progressText);

            var edit = F.Button("EditLoadoutButton", center, F.Green, "menu_edit_loadout", 26f);
            F.Anchor((RectTransform)edit.transform, new Vector2(0.5f, 0f), new Vector2(0f, 48f), new Vector2(320f, 60f), new Vector2(0.5f, 0f));
            F.Set(controller, "editLoadoutButton", edit);
            F.Set(controller, "centerTree", centerTree);
        }

        // ------------------------------------------------------------------ bottom bar
        static void BuildBottomBar(RectTransform safe, MenuUIController controller)
        {
            var bar = F.Rect("BottomBar", safe);
            F.AnchorStretchX(bar, 14f, 200f, 200f, 200f, 0f, 0f);

            var rank = F.IconButton("RankButton", bar, F.Blue, F.Icon("rank"), F.TextLight, "menu_rank", 24f);
            F.Anchor((RectTransform)rank.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(150f, 150f), new Vector2(0f, 0.5f));
            F.Set(controller, "rankButton", rank);

            var quests = F.IconButton("QuestsButton", bar, F.Blue, F.Icon("quest"), F.TextLight, "menu_quests", 24f);
            F.Anchor((RectTransform)quests.transform, new Vector2(0f, 0.5f), new Vector2(170f, 0f), new Vector2(150f, 150f), new Vector2(0f, 0.5f));
            F.Set(controller, "questsButton", quests);
            var badge = F.Image("Badge", quests.transform, F.Ui("circle"), F.Red, false, false);
            F.Anchor((RectTransform)badge.transform, new Vector2(1f, 1f), new Vector2(10f, 10f), new Vector2(52f, 52f), new Vector2(1f, 1f));
            var badgeText = F.OutlinedText("Count", badge.transform, null, 26f, F.TextLight, TextAlignmentOptions.Center, "0");
            F.Stretch((RectTransform)badgeText.transform);
            F.Set(controller, "questsBadge", badge.gameObject);
            F.Set(controller, "questsBadgeText", badgeText);

            var slots = F.HorizontalGroup("ChestSlots", bar, 12f, TextAnchor.MiddleLeft);
            F.Anchor(slots, new Vector2(0f, 0.5f), new Vector2(336f, 0f), new Vector2(720f, 180f), new Vector2(0f, 0.5f));
            var slotViews = new Object[4];
            for (int i = 0; i < 4; i++) slotViews[i] = ChestSlot("ChestSlot_" + i, slots, i);
            F.SetArray(controller, "chestSlots", slotViews);

            var battle = F.Button("BattleButton", bar, F.Honey, "menu_battle", 56f);
            F.Anchor((RectTransform)battle.transform, new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(420f, 150f), new Vector2(1f, 0.5f));
            var swords = F.Image("Icon", battle.transform, F.Icon("battle"), F.TextLight, false, false);
            F.Anchor((RectTransform)swords.transform, new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(90f, 90f), new Vector2(0f, 0.5f));
            var label = battle.transform.Find("Label");
            if (label != null) F.Stretch((RectTransform)label, 110f, 6f, 20f, 6f);
            F.Set(controller, "battleButton", battle);

            var debugBtn = F.Button("DebugButton", safe, F.Red, null, 22f, null, null, "DEBUG");
            F.Anchor((RectTransform)debugBtn.transform, new Vector2(0f, 1f), new Vector2(260f, -14f), new Vector2(130f, 50f), new Vector2(0f, 1f));
            F.Set(controller, "debugButton", debugBtn);
        }

        static ChestSlotView ChestSlot(string name, Transform parent, int index)
        {
            var btn = F.Button(name, parent, F.PanelDark, null, 24f, F.Ui("panel"));
            ((RectTransform)btn.transform).sizeDelta = new Vector2(170f, 176f);
            var glow = F.Image("ReadyGlow", btn.transform, F.Ui("glow"), F.Honey, false, false);
            F.Anchor((RectTransform)glow.transform, new Vector2(0.5f, 0.6f), Vector2.zero, new Vector2(220f, 200f));
            glow.gameObject.SetActive(false);
            var icon = F.Image("ChestIcon", btn.transform, F.Sprite(ArtPaths.Chest("twig", false)), Color.white, false, false);
            F.Anchor((RectTransform)icon.transform, new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(150f, 110f));
            var empty = F.Image("EmptyState", btn.transform, F.Icon("plus"), new Color(1f, 1f, 1f, 0.25f), false, false);
            F.Anchor((RectTransform)empty.transform, new Vector2(0.5f, 0.6f), Vector2.zero, new Vector2(70f, 70f));
            var nameText = F.Text("Name", btn.transform, null, 22f, F.TextLight, TextAlignmentOptions.Center, true, "");
            F.AnchorStretchX((RectTransform)nameText.transform, 40f, 30f, 6f, 6f, 0f, 0f);
            var timer = F.Text("Timer", btn.transform, null, 22f, F.Honey, TextAlignmentOptions.Center, false, "");
            F.AnchorStretchX((RectTransform)timer.transform, 10f, 30f, 6f, 6f, 0f, 0f);
            var view = btn.gameObject.AddComponent<ChestSlotView>();
            F.Set(view, "slotIndex", (float)index);
            F.Set(view, "chestIcon", icon);
            F.Set(view, "nameText", nameText);
            F.Set(view, "timerText", timer);
            F.Set(view, "emptyState", empty.gameObject);
            F.Set(view, "readyGlow", glow.gameObject);
            F.Set(view, "button", btn);
            return view;
        }

        // ------------------------------------------------------------------ panel frame
        static T MakePanel<T>(string name, Transform parent, string titleKey, out Button close, Vector2? size = null) where T : UIPanel
        {
            var img = F.Image(name, parent, F.Ui("panel"), F.PanelDark, true, true);
            F.Anchor((RectTransform)img.transform, new Vector2(0.5f, 0.5f), Vector2.zero, size ?? PanelSize);
            F.Group(img.gameObject, 1f);
            var responsive = img.gameObject.AddComponent<ResponsiveWidth>();
            F.Set(responsive, "preferredWidth", (size ?? PanelSize).x);
            F.Set(responsive, "sideMargin", 24f);
            var comp = img.gameObject.AddComponent<T>();
            var title = F.OutlinedText("Title", img.transform, titleKey, 46f, F.Honey);
            F.AnchorStretchX((RectTransform)title.transform, -16f, 70f, 120f, 120f, 1f, 1f);
            close = F.IconButton("CloseButton", img.transform, F.Red, F.Icon("close"), F.TextLight);
            F.Anchor((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-14f, -14f), new Vector2(84f, 84f), new Vector2(1f, 1f));
            F.Set(comp, "closeButton", close);
            return comp;
        }

        static void FinishPanel(UIPanel panel)
        {
            var group = panel.GetComponent<CanvasGroup>();
            if (group != null) { group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false; }
            panel.gameObject.SetActive(false);
        }

        static RectTransform Content(Transform panel, float top = 90f, float bottom = 30f, float side = 40f)
        {
            var rt = F.Rect("Content", panel);
            F.Stretch(rt, side, bottom, side, top);
            return rt;
        }

        // ------------------------------------------------------------------ guardian card
        static GuardianCardView GuardianCard(string name, Transform parent, Vector2 size)
        {
            float s = size.x / 260f;
            var frame = F.Image(name, parent, F.Ui("card"), Color.white, true, true);
            ((RectTransform)frame.transform).sizeDelta = size;
            var btn = frame.gameObject.AddComponent<Button>();
            btn.targetGraphic = frame;
            var feedback = frame.gameObject.AddComponent<UIButtonFeedback>();
            var inner = F.Image("Inner", frame.transform, F.Ui("soft"), new Color(0.16f, 0.2f, 0.27f), true, false);
            F.Stretch((RectTransform)inner.transform, 10f * s, 10f * s, 10f * s, 10f * s);
            var portrait = F.Image("Portrait", frame.transform, null, Color.white, false, false);
            F.Stretch((RectTransform)portrait.transform, 16f * s, 90f * s, 16f * s, 16f * s);
            var corner = F.Image("CornerIcon", frame.transform, F.Sprite(ArtPaths.RarityIcon("common")), Color.white, false, false);
            F.Anchor((RectTransform)corner.transform, new Vector2(0f, 1f), new Vector2(10f * s, -10f * s), new Vector2(44f * s, 44f * s), new Vector2(0f, 1f));
            var levelPill = F.Image("LevelPill", frame.transform, F.Ui("pill"), F.PanelDark, true, false);
            F.Anchor((RectTransform)levelPill.transform, new Vector2(1f, 1f), new Vector2(-8f * s, -8f * s), new Vector2(96f * s, 40f * s), new Vector2(1f, 1f));
            var level = F.OutlinedText("Level", levelPill.transform, null, 22f * s, F.TextLight, TextAlignmentOptions.Center, "");
            F.Stretch((RectTransform)level.transform);
            var nameText = F.OutlinedText("Name", frame.transform, null, 22f * s, F.TextLight, TextAlignmentOptions.Center, "");
            F.AnchorStretchX((RectTransform)nameText.transform, 52f * s, 32f * s, 8f * s, 8f * s, 0f, 0f);
            var bar = F.FillBar("Shards", frame.transform, F.PanelMid, F.Green, out var fill);
            F.AnchorStretchX((RectTransform)bar.transform, 16f * s, 28f * s, 16f * s, 16f * s, 0f, 0f);
            var shardsText = F.OutlinedText("ShardsText", bar.transform, null, 18f * s, F.TextLight, TextAlignmentOptions.Center, "");
            F.Stretch((RectTransform)shardsText.transform);
            var arrow = F.Image("UpgradeArrow", frame.transform, F.Icon("arrow_up"), F.Green, false, false);
            F.Anchor((RectTransform)arrow.transform, new Vector2(1f, 0f), new Vector2(-6f * s, 44f * s), new Vector2(44f * s, 44f * s), new Vector2(1f, 0f));
            var newBadge = F.Image("NewBadge", frame.transform, F.Ui("pill"), F.Honey, true, false);
            F.Anchor((RectTransform)newBadge.transform, new Vector2(0.5f, 1f), new Vector2(0f, 14f * s), new Vector2(90f * s, 36f * s), new Vector2(0.5f, 1f));
            var newText = F.OutlinedText("Text", newBadge.transform, "ui_new", 20f * s, F.TextDark);
            F.Stretch((RectTransform)newText.transform);
            var locked = F.Image("LockedOverlay", frame.transform, F.Ui("soft"), new Color(0f, 0f, 0f, 0.55f), true, false);
            F.Stretch((RectTransform)locked.transform, 10f * s, 10f * s, 10f * s, 10f * s);
            var lockIcon = F.Image("Lock", locked.transform, F.Icon("lock"), F.TextLight, false, false);
            F.Anchor((RectTransform)lockIcon.transform, new Vector2(0.5f, 0.6f), Vector2.zero, new Vector2(64f * s, 64f * s));
            var lockedText = F.Text("LockedText", locked.transform, null, 18f * s, F.TextLight, TextAlignmentOptions.Center, false, "");
            F.AnchorStretchX((RectTransform)lockedText.transform, 60f * s, 50f * s, 6f * s, 6f * s, 0f, 0f);
            var emptyState = F.Image("EmptyState", frame.transform, F.Icon("plus"), new Color(1f, 1f, 1f, 0.3f), false, false);
            F.Anchor((RectTransform)emptyState.transform, new Vector2(0.5f, 0.55f), Vector2.zero, new Vector2(70f * s, 70f * s));
            var highlight = F.Image("SelectedHighlight", frame.transform, F.Ui("card"), F.Honey, true, false);
            F.Stretch((RectTransform)highlight.transform, -8f * s, -8f * s, -8f * s, -8f * s);
            highlight.transform.SetAsFirstSibling();
            highlight.enabled = false;

            var view = frame.gameObject.AddComponent<GuardianCardView>();
            F.Set(view, "frame", frame);
            F.Set(view, "portrait", portrait);
            F.Set(view, "cornerIcon", corner);
            F.Set(view, "nameText", nameText);
            F.Set(view, "levelText", level);
            F.Set(view, "shardsFill", fill);
            F.Set(view, "shardsText", shardsText);
            F.Set(view, "upgradeArrow", arrow.gameObject);
            F.Set(view, "newBadge", newBadge.gameObject);
            F.Set(view, "lockedOverlay", locked.gameObject);
            F.Set(view, "lockedText", lockedText);
            F.Set(view, "emptyState", emptyState.gameObject);
            F.Set(view, "button", btn);
            F.Set(view, "feedback", feedback);
            F.Set(view, "selectedHighlight", highlight);
            return view;
        }

        static GuardiansPanel BuildGuardiansPanel(Transform parent)
        {
            var panel = MakePanel<GuardiansPanel>("GuardiansPanel", parent, "menu_guardians", out _);
            var content = Content(panel.transform);

            var equippedLabel = F.Text("EquippedLabel", content, "guardian_equipped_slots", 30f, F.TextLight, TextAlignmentOptions.MidlineLeft, true);
            F.Anchor((RectTransform)equippedLabel.transform, new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(400f, 40f), new Vector2(0f, 1f));
            var countText = F.Text("EquippedCount", content, null, 30f, F.Honey, TextAlignmentOptions.MidlineRight, true, "0/8");
            F.Anchor((RectTransform)countText.transform, new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(200f, 40f), new Vector2(1f, 1f));
            var hint = F.Text("Hint", content, null, 26f, F.Honey, TextAlignmentOptions.Center, false, "");
            F.Anchor((RectTransform)hint.transform, new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(800f, 40f), new Vector2(0.5f, 1f));

            var equippedRow = F.HorizontalGroup("Equipped", content, 12f, TextAnchor.MiddleCenter);
            F.AnchorStretchX(equippedRow, -50f, 250f, 0f, 0f, 1f, 1f);
            equippedRow.gameObject.AddComponent<FitRowScale>();
            var slots = new Object[8];
            for (int i = 0; i < 8; i++)
            {
                var card = GuardianCard("EquippedSlot_" + i, equippedRow, new Vector2(190f, 248f));
                slots[i] = card;
            }
            F.SetArray(panel, "equippedSlots", slots);
            F.Set(panel, "equippedCountText", countText);
            F.Set(panel, "hintText", hint);

            var filters = F.HorizontalGroup("Filters", content, 10f, TextAnchor.MiddleLeft);
            F.AnchorStretchX(filters, -316f, 56f, 0f, 0f, 1f, 1f);
            F.Set(panel, "filterAllButton", Chip("FilterAll", filters, "ui_all"));
            F.Set(panel, "filterOwnedButton", Chip("FilterOwned", filters, "ui_owned"));
            F.Set(panel, "filterLockedButton", Chip("FilterLocked", filters, "ui_locked"));
            var sortLabel = F.Text("SortLabel", filters, "ui_sort", 26f, F.TextLight, TextAlignmentOptions.Center, false);
            ((RectTransform)sortLabel.transform).sizeDelta = new Vector2(120f, 56f);
            F.Set(panel, "sortPowerButton", Chip("SortPower", filters, "sort_power"));
            F.Set(panel, "sortLevelButton", Chip("SortLevel", filters, "sort_level"));
            F.Set(panel, "sortRarityButton", Chip("SortRarity", filters, "sort_rarity"));
            F.Set(panel, "sortNewButton", Chip("SortNew", filters, "sort_new"));

            var scroll = F.ScrollView("Collection", content, out var gridContent);
            F.Stretch((RectTransform)scroll.transform, 0f, 0f, 0f, 380f);
            var grid = gridContent.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(230f, 300f);
            grid.spacing = new Vector2(14f, 14f);
            grid.padding = new RectOffset(12, 12, 12, 12);
            grid.constraint = GridLayoutGroup.Constraint.Flexible;
            var fitter = gridContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var template = GuardianCard("CardTemplate", gridContent, new Vector2(230f, 300f));
            template.gameObject.SetActive(false);
            F.Set(panel, "gridContent", gridContent);
            F.Set(panel, "cardTemplate", template);

            FinishPanel(panel);
            return panel;
        }

        static Button Chip(string name, Transform parent, string key)
        {
            var b = F.Button(name, parent, F.PanelMid, key, 24f, F.Ui("pill"));
            ((RectTransform)b.transform).sizeDelta = new Vector2(170f, 54f);
            var fb = b.GetComponent<UIButtonFeedback>();
            F.Set(fb, "selectedGraphic", b.GetComponent<Image>());
            return b;
        }

        static GuardianDetailPanel BuildGuardianDetailPanel(Transform parent)
        {
            var panel = MakePanel<GuardianDetailPanel>("GuardianDetailPanel", parent, "ui_info", out _, new Vector2(1500f, 900f));
            var content = Content(panel.transform);

            var frame = F.Image("Frame", content, F.Ui("card"), Color.white, true, false);
            F.Anchor((RectTransform)frame.transform, new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(380f, 470f), new Vector2(0f, 1f));
            var inner = F.Image("Inner", frame.transform, F.Ui("soft"), new Color(0.16f, 0.2f, 0.27f), true, false);
            F.Stretch((RectTransform)inner.transform, 10f, 10f, 10f, 10f);
            var portrait = F.Image("Portrait", frame.transform, null, Color.white, false, false);
            F.Stretch((RectTransform)portrait.transform, 18f, 18f, 18f, 18f);
            var corner = F.Image("CornerIcon", frame.transform, null, Color.white, false, false);
            F.Anchor((RectTransform)corner.transform, new Vector2(0f, 1f), new Vector2(12f, -12f), new Vector2(60f, 60f), new Vector2(0f, 1f));

            var nameText = F.OutlinedText("Name", content, null, 44f, F.TextLight, TextAlignmentOptions.MidlineLeft, "");
            F.Anchor((RectTransform)nameText.transform, new Vector2(0f, 1f), new Vector2(410f, 0f), new Vector2(700f, 56f), new Vector2(0f, 1f));
            var rarity = F.Text("Rarity", content, null, 28f, F.Honey, TextAlignmentOptions.MidlineLeft, true, "");
            F.Anchor((RectTransform)rarity.transform, new Vector2(0f, 1f), new Vector2(410f, -60f), new Vector2(400f, 36f), new Vector2(0f, 1f));
            var level = F.Text("Level", content, null, 28f, F.TextLight, TextAlignmentOptions.MidlineRight, true, "");
            F.Anchor((RectTransform)level.transform, new Vector2(1f, 1f), new Vector2(0f, -60f), new Vector2(300f, 36f), new Vector2(1f, 1f));
            var desc = F.Text("Description", content, null, 24f, new Color(0.85f, 0.88f, 0.9f), TextAlignmentOptions.TopLeft, false, "");
            F.Anchor((RectTransform)desc.transform, new Vector2(0f, 1f), new Vector2(410f, -104f), new Vector2(1000f, 70f), new Vector2(0f, 1f));
            var power = F.Text("Power", content, null, 26f, F.Honey, TextAlignmentOptions.MidlineLeft, true, "");
            F.Anchor((RectTransform)power.transform, new Vector2(0f, 1f), new Vector2(410f, -180f), new Vector2(500f, 34f), new Vector2(0f, 1f));

            var stats = F.VerticalGroup("Stats", content, 6f, TextAnchor.UpperLeft);
            F.Anchor(stats, new Vector2(0f, 1f), new Vector2(410f, -222f), new Vector2(1000f, 300f), new Vector2(0f, 1f));
            var health = StatLine("Health", stats); var attack = StatLine("Attack", stats); var structure = StatLine("Structure", stats); var armor = StatLine("Armor", stats);
            var crit = StatLine("Crit", stats); var cooldown = StatLine("Cooldown", stats); var special = StatLine("Special", stats); var passive = StatLine("Passive", stats);

            var costRow = F.Rect("CostRow", content);
            F.Anchor(costRow, new Vector2(0f, 0f), new Vector2(0f, 90f), new Vector2(380f, 60f), new Vector2(0f, 0f));
            var cardsBar = F.FillBar("CardsBar", costRow, F.PanelMid, F.Green, out var cardsFill);
            F.Stretch((RectTransform)cardsBar.transform);
            var cardsText = F.OutlinedText("CardsText", cardsBar.transform, null, 24f, F.TextLight, TextAlignmentOptions.Center, "");
            F.Stretch((RectTransform)cardsText.transform);

            var upgrade = F.Button("UpgradeButton", content, F.Green, "guardian_upgrade", 30f);
            F.Anchor((RectTransform)upgrade.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(380f, 80f), new Vector2(0f, 0f));
            var coinIcon = F.Image("CoinIcon", upgrade.transform, F.Icon("coin"), F.Honey, false, false);
            F.Anchor((RectTransform)coinIcon.transform, new Vector2(1f, 0.5f), new Vector2(-96f, 0f), new Vector2(40f, 40f), new Vector2(1f, 0.5f));
            var coinsText = F.OutlinedText("CoinsCost", upgrade.transform, null, 24f, F.TextLight, TextAlignmentOptions.MidlineRight, "");
            F.Anchor((RectTransform)coinsText.transform, new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(84f, 40f), new Vector2(1f, 0.5f));
            var upgradeLabel = upgrade.transform.Find("Label");
            if (upgradeLabel != null) F.Stretch((RectTransform)upgradeLabel, 12f, 6f, 150f, 6f);

            var equip = F.Button("EquipButton", content, F.Blue, "ui_equip", 30f);
            F.Anchor((RectTransform)equip.transform, new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(360f, 80f), new Vector2(1f, 0f));

            F.Set(panel, "portrait", portrait); F.Set(panel, "frame", frame); F.Set(panel, "cornerIcon", corner);
            F.Set(panel, "nameText", nameText); F.Set(panel, "rarityText", rarity); F.Set(panel, "levelText", level);
            F.Set(panel, "descriptionText", desc); F.Set(panel, "powerText", power);
            F.Set(panel, "healthText", health); F.Set(panel, "attackText", attack); F.Set(panel, "structureText", structure); F.Set(panel, "armorText", armor);
            F.Set(panel, "critText", crit); F.Set(panel, "cooldownText", cooldown); F.Set(panel, "specialText", special); F.Set(panel, "passiveText", passive);
            F.Set(panel, "upgradeButton", upgrade); F.Set(panel, "upgradeLabel", upgradeLabel != null ? upgradeLabel.GetComponent<TMP_Text>() : null);
            F.Set(panel, "cardsCostText", cardsText); F.Set(panel, "coinsCostText", coinsText); F.Set(panel, "cardsFill", cardsFill);
            F.Set(panel, "equipButton", equip); F.Set(panel, "equipLabel", equip.transform.Find("Label")?.GetComponent<TMP_Text>());
            F.Set(panel, "punchTarget", frame.transform);
            FinishPanel(panel);
            return panel;
        }

        static TMP_Text StatLine(string name, Transform parent)
        {
            var t = F.Text(name, parent, null, 26f, F.TextLight, TextAlignmentOptions.MidlineLeft, false, "");
            ((RectTransform)t.transform).sizeDelta = new Vector2(1000f, 32f);
            return t;
        }

        // ------------------------------------------------------------------ tree panel
        static TreeUpgradePanel BuildTreePanel(Transform parent)
        {
            var panel = MakePanel<TreeUpgradePanel>("TreeUpgradePanel", parent, "tree_panel_title", out _);
            var content = Content(panel.transform);
            var tier = F.Text("TierText", content, null, 34f, F.Honey, TextAlignmentOptions.MidlineLeft, true, "");
            F.Anchor((RectTransform)tier.transform, new Vector2(0f, 1f), Vector2.zero, new Vector2(600f, 44f), new Vector2(0f, 1f));
            var total = F.Text("TotalLevelText", content, null, 30f, F.TextLight, TextAlignmentOptions.MidlineRight, true, "");
            F.Anchor((RectTransform)total.transform, new Vector2(1f, 1f), Vector2.zero, new Vector2(400f, 44f), new Vector2(1f, 1f));

            var grid = F.Rect("Rows", content);
            F.Stretch(grid, 0f, 0f, 0f, 60f);
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(880f, 230f);
            gl.spacing = new Vector2(30f, 20f);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = 2;
            string[] icons = { "heart", "armor", "branch", "root", "sapflow", "canopy" };
            var so = new SerializedObject(panel);
            var rows = so.FindProperty("rows");
            rows.arraySize = 6;
            for (int i = 0; i < 6; i++)
            {
                var row = F.Image("Row_" + i, grid, F.Ui("panel"), F.PanelMid, true, false);
                var icon = F.Image("Icon", row.transform, F.Icon(icons[i]), F.Green, false, false);
                F.Anchor((RectTransform)icon.transform, new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(110f, 110f), new Vector2(0f, 0.5f));
                var nameText = F.Text("Name", row.transform, null, 30f, F.TextLight, TextAlignmentOptions.MidlineLeft, true, "");
                F.Anchor((RectTransform)nameText.transform, new Vector2(0f, 1f), new Vector2(150f, -18f), new Vector2(500f, 40f), new Vector2(0f, 1f));
                var descText = F.Text("Desc", row.transform, null, 22f, new Color(0.85f, 0.88f, 0.9f), TextAlignmentOptions.TopLeft, false, "");
                F.Anchor((RectTransform)descText.transform, new Vector2(0f, 1f), new Vector2(150f, -62f), new Vector2(480f, 70f), new Vector2(0f, 1f));
                var levelText = F.Text("Level", row.transform, null, 26f, F.Honey, TextAlignmentOptions.MidlineLeft, true, "");
                F.Anchor((RectTransform)levelText.transform, new Vector2(0f, 0f), new Vector2(150f, 22f), new Vector2(200f, 36f), new Vector2(0f, 0f));
                var btn = F.Button("UpgradeButton", row.transform, F.Green, "ui_upgrade", 26f);
                F.Anchor((RectTransform)btn.transform, new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(210f, 100f), new Vector2(1f, 0.5f));
                var lbl = btn.transform.Find("Label"); if (lbl != null) F.AnchorStretchX((RectTransform)lbl, -12f, 40f, 8f, 8f, 1f, 1f);
                var costText = F.OutlinedText("Cost", btn.transform, null, 24f, F.TextLight, TextAlignmentOptions.Center, "");
                F.AnchorStretchX((RectTransform)costText.transform, 10f, 36f, 8f, 8f, 0f, 0f);
                var sapIcon = F.Image("SapIcon", costText.transform, F.Icon("sap"), F.Green, false, false);
                F.Anchor((RectTransform)sapIcon.transform, new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(30f, 30f), new Vector2(0f, 0.5f));
                var el = rows.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("path").enumValueIndex = i;
                el.FindPropertyRelative("icon").objectReferenceValue = icon;
                el.FindPropertyRelative("nameText").objectReferenceValue = nameText;
                el.FindPropertyRelative("descText").objectReferenceValue = descText;
                el.FindPropertyRelative("levelText").objectReferenceValue = levelText;
                el.FindPropertyRelative("costText").objectReferenceValue = costText;
                el.FindPropertyRelative("upgradeButton").objectReferenceValue = btn;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            F.Set(panel, "tierText", tier);
            F.Set(panel, "totalLevelText", total);
            FinishPanel(panel);
            return panel;
        }

        // ------------------------------------------------------------------ tools panel
        static ToolCardView ToolCard(string name, Transform parent, Vector2 size)
        {
            var frame = F.Image(name, parent, F.Ui("card"), Color.white, true, true);
            ((RectTransform)frame.transform).sizeDelta = size;
            var btn = frame.gameObject.AddComponent<Button>();
            btn.targetGraphic = frame;
            var feedback = frame.gameObject.AddComponent<UIButtonFeedback>();
            F.Set(feedback, "selectedGraphic", frame);
            var inner = F.Image("Inner", frame.transform, F.Ui("soft"), new Color(0.16f, 0.2f, 0.27f), true, false);
            F.Stretch((RectTransform)inner.transform, 10f, 10f, 10f, 10f);
            var icon = F.Image("Icon", frame.transform, null, Color.white, false, false);
            F.Stretch((RectTransform)icon.transform, 30f, 80f, 30f, 24f);
            var nameText = F.OutlinedText("Name", frame.transform, null, 20f, F.TextLight, TextAlignmentOptions.Center, "");
            F.AnchorStretchX((RectTransform)nameText.transform, 44f, 30f, 6f, 6f, 0f, 0f);
            var level = F.Text("Level", frame.transform, null, 20f, F.Honey, TextAlignmentOptions.Center, true, "");
            F.AnchorStretchX((RectTransform)level.transform, 14f, 28f, 6f, 6f, 0f, 0f);
            var locked = F.Image("LockedOverlay", frame.transform, F.Ui("soft"), new Color(0f, 0f, 0f, 0.55f), true, false);
            F.Stretch((RectTransform)locked.transform, 10f, 10f, 10f, 10f);
            var lockIcon = F.Image("Lock", locked.transform, F.Icon("lock"), F.TextLight, false, false);
            F.Anchor((RectTransform)lockIcon.transform, new Vector2(0.5f, 0.6f), Vector2.zero, new Vector2(56f, 56f));
            var empty = F.Image("EmptyState", frame.transform, F.Icon("plus"), new Color(1f, 1f, 1f, 0.3f), false, false);
            F.Anchor((RectTransform)empty.transform, new Vector2(0.5f, 0.55f), Vector2.zero, new Vector2(60f, 60f));
            var view = frame.gameObject.AddComponent<ToolCardView>();
            F.Set(view, "icon", icon); F.Set(view, "frame", frame); F.Set(view, "nameText", nameText); F.Set(view, "levelText", level);
            F.Set(view, "lockedOverlay", locked.gameObject); F.Set(view, "emptyState", empty.gameObject); F.Set(view, "button", btn); F.Set(view, "feedback", feedback);
            return view;
        }

        static ToolsPanel BuildToolsPanel(Transform parent)
        {
            var panel = MakePanel<ToolsPanel>("ToolsPanel", parent, "menu_tools", out _);
            var content = Content(panel.transform);
            var eqLabel = F.Text("EquippedLabel", content, "tools_equipped", 30f, F.TextLight, TextAlignmentOptions.MidlineLeft, true);
            F.Anchor((RectTransform)eqLabel.transform, new Vector2(0f, 1f), Vector2.zero, new Vector2(500f, 40f), new Vector2(0f, 1f));
            var eqRow = F.HorizontalGroup("Equipped", content, 16f, TextAnchor.MiddleLeft);
            F.Anchor(eqRow, new Vector2(0f, 1f), new Vector2(0f, -50f), new Vector2(700f, 250f), new Vector2(0f, 1f));
            var slots = new Object[3];
            for (int i = 0; i < 3; i++) slots[i] = ToolCard("EquippedTool_" + i, eqRow, new Vector2(200f, 240f));
            F.SetArray(panel, "equippedSlots", slots);

            var colLabel = F.Text("CollectionLabel", content, "tools_collection", 30f, F.TextLight, TextAlignmentOptions.MidlineLeft, true);
            F.Anchor((RectTransform)colLabel.transform, new Vector2(0f, 1f), new Vector2(0f, -320f), new Vector2(500f, 40f), new Vector2(0f, 1f));
            var scroll = F.ScrollView("Collection", content, out var gridContent);
            F.Anchor((RectTransform)scroll.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(1100f, 440f), new Vector2(0f, 0f));
            var grid = gridContent.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(200f, 240f); grid.spacing = new Vector2(14f, 14f); grid.padding = new RectOffset(12, 12, 12, 12);
            gridContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var template = ToolCard("ToolTemplate", gridContent, new Vector2(200f, 240f));
            template.gameObject.SetActive(false);
            F.Set(panel, "gridContent", gridContent); F.Set(panel, "cardTemplate", template);

            var detail = F.Image("Detail", content, F.Ui("panel"), F.PanelMid, true, false);
            F.Anchor((RectTransform)detail.transform, new Vector2(1f, 0.5f), new Vector2(0f, -40f), new Vector2(640f, 720f), new Vector2(1f, 0.5f));
            var dIcon = F.Image("Icon", detail.transform, null, Color.white, false, false);
            F.Anchor((RectTransform)dIcon.transform, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(180f, 180f), new Vector2(0.5f, 1f));
            var dName = F.OutlinedText("Name", detail.transform, null, 36f, F.TextLight, TextAlignmentOptions.Center, "");
            F.AnchorStretchX((RectTransform)dName.transform, -220f, 46f, 20f, 20f, 1f, 1f);
            var dDesc = F.Text("Desc", detail.transform, null, 24f, new Color(0.85f, 0.88f, 0.9f), TextAlignmentOptions.Top, false, "");
            F.AnchorStretchX((RectTransform)dDesc.transform, -276f, 110f, 30f, 30f, 1f, 1f);
            var dStats = F.Text("Stats", detail.transform, null, 24f, F.Honey, TextAlignmentOptions.Center, false, "");
            F.AnchorStretchX((RectTransform)dStats.transform, -390f, 40f, 20f, 20f, 1f, 1f);
            var dCost = F.Text("Cost", detail.transform, null, 26f, F.TextLight, TextAlignmentOptions.Center, true, "");
            F.AnchorStretchX((RectTransform)dCost.transform, -440f, 40f, 20f, 20f, 1f, 1f);
            var upgrade = F.Button("UpgradeButton", detail.transform, F.Green, "ui_upgrade", 28f);
            F.Anchor((RectTransform)upgrade.transform, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(400f, 76f), new Vector2(0.5f, 0f));
            var equip = F.Button("EquipButton", detail.transform, F.Blue, "ui_equip", 28f);
            F.Anchor((RectTransform)equip.transform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(400f, 76f), new Vector2(0.5f, 0f));
            F.Set(panel, "detailIcon", dIcon); F.Set(panel, "detailName", dName); F.Set(panel, "detailDesc", dDesc); F.Set(panel, "detailStats", dStats); F.Set(panel, "detailCost", dCost);
            F.Set(panel, "upgradeButton", upgrade); F.Set(panel, "equipButton", equip); F.Set(panel, "equipLabel", equip.transform.Find("Label")?.GetComponent<TMP_Text>());
            FinishPanel(panel);
            return panel;
        }

        // ------------------------------------------------------------------ battle prep
        static BattlePrepPanel BuildBattlePrepPanel(Transform parent)
        {
            var panel = MakePanel<BattlePrepPanel>("BattlePrepPanel", parent, "prep_title", out _, new Vector2(1700f, 900f));
            var content = Content(panel.transform);
            var badge = F.Image("ArenaBadge", content, F.Icon("leaf"), F.Green, false, false);
            F.Anchor((RectTransform)badge.transform, new Vector2(0.5f, 1f), new Vector2(-300f, 0f), new Vector2(90f, 90f), new Vector2(0.5f, 1f));
            var arenaName = F.OutlinedText("ArenaName", content, null, 44f, F.TextLight, TextAlignmentOptions.MidlineLeft, "");
            F.Anchor((RectTransform)arenaName.transform, new Vector2(0.5f, 1f), new Vector2(-240f, -6f), new Vector2(700f, 60f), new Vector2(0f, 1f));
            var rec = F.Text("RecommendedPower", content, null, 26f, F.Honey, TextAlignmentOptions.MidlineLeft, false, "");
            F.Anchor((RectTransform)rec.transform, new Vector2(0.5f, 1f), new Vector2(-240f, -66f), new Vector2(500f, 34f), new Vector2(0f, 1f));
            var your = F.Text("YourPower", content, null, 26f, F.Green, TextAlignmentOptions.MidlineLeft, false, "");
            F.Anchor((RectTransform)your.transform, new Vector2(0.5f, 1f), new Vector2(120f, -66f), new Vector2(500f, 34f), new Vector2(0f, 1f));

            var yourLabel = F.Text("YourLoadoutLabel", content, "prep_your_loadout", 28f, F.TextLight, TextAlignmentOptions.MidlineLeft, true);
            F.Anchor((RectTransform)yourLabel.transform, new Vector2(0f, 1f), new Vector2(0f, -130f), new Vector2(600f, 40f), new Vector2(0f, 1f));
            var yourRow = F.HorizontalGroup("PlayerLoadout", content, 10f, TextAnchor.MiddleLeft);
            F.Anchor(yourRow, new Vector2(0f, 1f), new Vector2(0f, -176f), new Vector2(1500f, 150f), new Vector2(0f, 1f));
            yourRow.pivot = new Vector2(0f, 1f);
            yourRow.gameObject.AddComponent<FitRowScale>();
            var playerPortraits = new Object[8];
            for (int i = 0; i < 8; i++) playerPortraits[i] = Portrait("P_" + i, yourRow);
            var enemyLabel = F.Text("EnemyLoadoutLabel", content, "prep_enemy_loadout", 28f, F.TextLight, TextAlignmentOptions.MidlineLeft, true);
            F.Anchor((RectTransform)enemyLabel.transform, new Vector2(0f, 1f), new Vector2(0f, -350f), new Vector2(600f, 40f), new Vector2(0f, 1f));
            var enemyRow = F.HorizontalGroup("EnemyLoadout", content, 10f, TextAnchor.MiddleLeft);
            F.Anchor(enemyRow, new Vector2(0f, 1f), new Vector2(0f, -396f), new Vector2(1500f, 150f), new Vector2(0f, 1f));
            enemyRow.pivot = new Vector2(0f, 1f);
            enemyRow.gameObject.AddComponent<FitRowScale>();
            var enemyPortraits = new Object[8];
            for (int i = 0; i < 8; i++) enemyPortraits[i] = Portrait("E_" + i, enemyRow);
            F.SetArray(panel, "playerPortraits", playerPortraits);
            F.SetArray(panel, "enemyPortraits", enemyPortraits);

            var diffLabel = F.Text("DifficultyLabel", content, "prep_difficulty", 28f, F.TextLight, TextAlignmentOptions.MidlineLeft, true);
            F.Anchor((RectTransform)diffLabel.transform, new Vector2(0f, 0f), new Vector2(0f, 110f), new Vector2(300f, 40f), new Vector2(0f, 0f));
            var diffRow = F.HorizontalGroup("Difficulty", content, 12f, TextAnchor.MiddleLeft);
            F.Anchor(diffRow, new Vector2(0f, 0f), new Vector2(0f, 30f), new Vector2(700f, 70f), new Vector2(0f, 0f));
            F.Set(panel, "easyButton", Chip("Easy", diffRow, "difficulty_easy"));
            F.Set(panel, "normalButton", Chip("Normal", diffRow, "difficulty_normal"));
            F.Set(panel, "hardButton", Chip("Hard", diffRow, "difficulty_hard"));
            var note = F.Text("OfflineNote", content, null, 22f, new Color(0.75f, 0.8f, 0.85f), TextAlignmentOptions.MidlineLeft, false, "");
            F.Anchor((RectTransform)note.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(700f, 30f), new Vector2(0f, 0f));
            var start = F.Button("StartButton", content, F.Honey, "prep_start", 40f);
            F.Anchor((RectTransform)start.transform, new Vector2(1f, 0f), Vector2.zero, new Vector2(460f, 120f), new Vector2(1f, 0f));
            F.Set(panel, "startButton", start);
            F.Set(panel, "arenaNameText", arenaName); F.Set(panel, "arenaBadge", badge); F.Set(panel, "recommendedPowerText", rec); F.Set(panel, "yourPowerText", your); F.Set(panel, "offlineNoteText", note);
            FinishPanel(panel);
            return panel;
        }

        static Image Portrait(string name, Transform parent)
        {
            var frame = F.Image(name, parent, F.Ui("card"), F.PanelMid, true, false);
            ((RectTransform)frame.transform).sizeDelta = new Vector2(150f, 150f);
            var img = F.Image("Portrait", frame.transform, null, new Color(1f, 1f, 1f, 0.15f), false, false);
            F.Stretch((RectTransform)img.transform, 10f, 10f, 10f, 10f);
            return img;
        }

        // ------------------------------------------------------------------ chests panel
        static ChestsPanel BuildChestsPanel(Transform parent)
        {
            var panel = MakePanel<ChestsPanel>("ChestsPanel", parent, "chest_open", out _, new Vector2(1100f, 760f));
            var content = Content(panel.transform);
            var slotIdx = F.Text("SlotIndex", content, null, 26f, F.TextLight, TextAlignmentOptions.Center, false, "1/4");
            F.Anchor((RectTransform)slotIdx.transform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(200f, 36f), new Vector2(0.5f, 1f));
            var prev = F.IconButton("PrevButton", content, F.PanelMid, F.Icon("play"), F.TextLight);
            F.Anchor((RectTransform)prev.transform, new Vector2(0f, 0.55f), Vector2.zero, new Vector2(80f, 80f), new Vector2(0f, 0.5f));
            prev.transform.localScale = new Vector3(-1f, 1f, 1f);
            var next = F.IconButton("NextButton", content, F.PanelMid, F.Icon("play"), F.TextLight);
            F.Anchor((RectTransform)next.transform, new Vector2(1f, 0.55f), Vector2.zero, new Vector2(80f, 80f), new Vector2(1f, 0.5f));
            var img = F.Image("ChestImage", content, F.Sprite(ArtPaths.Chest("twig", false)), Color.white, false, false);
            F.Anchor((RectTransform)img.transform, new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(360f, 300f));
            var nameText = F.OutlinedText("Name", content, null, 36f, F.TextLight, TextAlignmentOptions.Center, "");
            F.Anchor((RectTransform)nameText.transform, new Vector2(0.5f, 0.35f), Vector2.zero, new Vector2(700f, 50f));
            var timer = F.Text("Timer", content, null, 32f, F.Honey, TextAlignmentOptions.Center, true, "");
            F.Anchor((RectTransform)timer.transform, new Vector2(0.5f, 0.27f), Vector2.zero, new Vector2(500f, 44f));
            var info = F.Text("Info", content, null, 22f, new Color(0.8f, 0.85f, 0.9f), TextAlignmentOptions.Center, false, "");
            F.Anchor((RectTransform)info.transform, new Vector2(0.5f, 0.2f), Vector2.zero, new Vector2(800f, 34f));
            var buttons = F.HorizontalGroup("Buttons", content, 16f, TextAnchor.MiddleCenter);
            F.Anchor(buttons, new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(900f, 90f), new Vector2(0.5f, 0f));
            var start = F.Button("StartButton", buttons, F.Green, "chest_start", 28f); ((RectTransform)start.transform).sizeDelta = new Vector2(260f, 84f);
            var skip = F.Button("SkipButton", buttons, F.Purple, null, 26f); ((RectTransform)skip.transform).sizeDelta = new Vector2(300f, 84f);
            var skipLabel = F.OutlinedText("Label", skip.transform, null, 26f, F.TextLight, TextAlignmentOptions.Center, ""); F.Stretch((RectTransform)skipLabel.transform, 12f, 6f, 12f, 6f);
            var open = F.Button("OpenButton", buttons, F.Honey, "chest_open", 30f); ((RectTransform)open.transform).sizeDelta = new Vector2(260f, 84f);
            F.Set(panel, "chestImage", img); F.Set(panel, "nameText", nameText); F.Set(panel, "timerText", timer); F.Set(panel, "infoText", info);
            F.Set(panel, "startButton", start); F.Set(panel, "skipButton", skip); F.Set(panel, "skipLabel", skipLabel); F.Set(panel, "openButton", open);
            F.Set(panel, "prevButton", prev); F.Set(panel, "nextButton", next); F.Set(panel, "slotIndexText", slotIdx);
            FinishPanel(panel);
            return panel;
        }

        // ------------------------------------------------------------------ shop
        static ShopPanel BuildShopPanel(Transform parent)
        {
            var panel = MakePanel<ShopPanel>("ShopPanel", parent, "shop_title", out _);
            var content = Content(panel.transform);
            var note = F.Text("Note", content, null, 24f, F.Honey, TextAlignmentOptions.Center, false, "");
            F.AnchorStretchX((RectTransform)note.transform, 0f, 34f, 0f, 0f, 1f, 1f);
            var grid = F.Rect("Items", content);
            F.Stretch(grid, 0f, 0f, 0f, 50f);
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(280f, 340f); gl.spacing = new Vector2(20f, 20f); gl.childAlignment = TextAnchor.MiddleCenter;
            var free = ShopItem("FreeChest", grid, "shop_free_chest", F.Sprite(ArtPaths.Chest("twig", true)), Color.white, out var freeLabel);
            F.Set(panel, "freeChestButton", free); F.Set(panel, "freeChestLabel", freeLabel);
            F.Set(panel, "coinPackButton", ShopItem("CoinPack", grid, "shop_coin_pack", F.Icon("coin"), F.Honey, out _));
            F.Set(panel, "sapPackButton", ShopItem("SapPack", grid, "shop_sap_pack", F.Icon("sap"), F.Green, out _));
            F.Set(panel, "starterPackButton", ShopItem("StarterPack", grid, "shop_starter_pack", F.Icon("star"), F.Honey, out _));
            F.Set(panel, "barkSkinButton", ShopItem("BarkSkin", grid, "shop_bark_skin", F.Icon("tree"), F.Green, out _));
            F.Set(panel, "watchAdButton", ShopItem("WatchAd", grid, "shop_watch_ad", F.Icon("play"), F.Blue, out _));
            F.Set(panel, "noteText", note);
            FinishPanel(panel);
            return panel;
        }

        static Button ShopItem(string name, Transform parent, string titleKey, Sprite icon, Color tint, out TMP_Text priceLabel)
        {
            var btn = F.Button(name, parent, F.PanelMid, null, 24f, F.Ui("card"));
            var ic = F.Image("Icon", btn.transform, icon, tint, false, false);
            F.Anchor((RectTransform)ic.transform, new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(150f, 150f), new Vector2(0.5f, 1f));
            var title = F.OutlinedText("Title", btn.transform, titleKey, 24f, F.TextLight);
            F.AnchorStretchX((RectTransform)title.transform, 86f, 60f, 10f, 10f, 0f, 0f);
            var price = F.Image("PricePill", btn.transform, F.Ui("pill"), F.Honey, true, false);
            F.Anchor((RectTransform)price.transform, new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(200f, 56f), new Vector2(0.5f, 0f));
            priceLabel = F.OutlinedText("Label", price.transform, "ui_test", 24f, F.TextDark);
            F.Stretch((RectTransform)priceLabel.transform);
            return btn;
        }

        // ------------------------------------------------------------------ quests
        static QuestsPanel BuildQuestsPanel(Transform parent, out Component dailyRefHolder)
        {
            var panel = MakePanel<QuestsPanel>("QuestsPanel", parent, "quests_title", out _);
            var content = Content(panel.transform);
            var tabs = F.HorizontalGroup("Tabs", content, 12f, TextAnchor.MiddleLeft);
            F.AnchorStretchX(tabs, 0f, 60f, 0f, 0f, 1f, 1f);
            F.Set(panel, "tabQuestsButton", Chip("TabQuests", tabs, "quests_tab_quests"));
            F.Set(panel, "tabAchievementsButton", Chip("TabAchievements", tabs, "quests_tab_achievements"));
            var daily = F.Button("DailyRewardButton", content, F.Honey, null, 24f, F.Ui("pill"));
            F.Anchor((RectTransform)daily.transform, new Vector2(1f, 1f), Vector2.zero, new Vector2(520f, 60f), new Vector2(1f, 1f));
            var dailyLabel = F.OutlinedText("Label", daily.transform, null, 24f, F.TextDark, TextAlignmentOptions.Center, "");
            F.Stretch((RectTransform)dailyLabel.transform, 12f, 4f, 12f, 4f);
            F.Set(panel, "dailyRewardButton", daily); F.Set(panel, "dailyRewardLabel", dailyLabel);

            var scroll = F.ScrollView("List", content, out var listContent);
            F.Stretch((RectTransform)scroll.transform, 0f, 0f, 0f, 76f);
            var vl = listContent.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 12f; vl.padding = new RectOffset(12, 12, 12, 12); vl.childControlWidth = true; vl.childControlHeight = false; vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            listContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var row = QuestRow("QuestRowTemplate", listContent);
            row.gameObject.SetActive(false);
            F.Set(panel, "listContent", listContent); F.Set(panel, "rowTemplate", row);
            dailyRefHolder = panel;
            FinishPanel(panel);
            return panel;
        }

        static QuestRowView QuestRow(string name, Transform parent)
        {
            var bg = F.Image(name, parent, F.Ui("panel"), F.PanelMid, true, false);
            var rt = (RectTransform)bg.transform; rt.sizeDelta = new Vector2(0f, 130f);
            bg.gameObject.AddComponent<LayoutElement>().preferredHeight = 130f;
            var icon = F.Image("Icon", bg.transform, F.Icon("quest"), F.Green, false, false);
            F.Anchor((RectTransform)icon.transform, new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(90f, 90f), new Vector2(0f, 0.5f));
            var dailyTag = F.Image("DailyTag", bg.transform, F.Ui("pill"), F.Blue, true, false);
            F.Anchor((RectTransform)dailyTag.transform, new Vector2(0f, 1f), new Vector2(126f, -8f), new Vector2(110f, 30f), new Vector2(0f, 1f));
            var dailyText = F.OutlinedText("Text", dailyTag.transform, "quests_daily", 18f, F.TextLight); F.Stretch((RectTransform)dailyText.transform);
            var title = F.Text("Title", bg.transform, null, 28f, F.TextLight, TextAlignmentOptions.MidlineLeft, true, "");
            F.Anchor((RectTransform)title.transform, new Vector2(0f, 1f), new Vector2(250f, -12f), new Vector2(700f, 36f), new Vector2(0f, 1f));
            var desc = F.Text("Desc", bg.transform, null, 22f, new Color(0.85f, 0.88f, 0.9f), TextAlignmentOptions.MidlineLeft, false, "");
            F.Anchor((RectTransform)desc.transform, new Vector2(0f, 1f), new Vector2(250f, -50f), new Vector2(800f, 30f), new Vector2(0f, 1f));
            var bar = F.FillBar("Progress", bg.transform, F.PanelDark, F.Green, out var fill);
            F.Anchor((RectTransform)bar.transform, new Vector2(0f, 0f), new Vector2(250f, 14f), new Vector2(500f, 30f), new Vector2(0f, 0f));
            var progressText = F.OutlinedText("Text", bar.transform, null, 20f, F.TextLight); F.Stretch((RectTransform)progressText.transform);
            var reward = F.Text("Reward", bg.transform, null, 22f, F.Honey, TextAlignmentOptions.MidlineRight, false, "");
            F.Anchor((RectTransform)reward.transform, new Vector2(1f, 0.5f), new Vector2(-250f, 0f), new Vector2(420f, 40f), new Vector2(1f, 0.5f));
            var claim = F.Button("ClaimButton", bg.transform, F.Honey, null, 24f);
            F.Anchor((RectTransform)claim.transform, new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(210f, 74f), new Vector2(1f, 0.5f));
            var claimLabel = F.OutlinedText("Label", claim.transform, null, 24f, F.TextDark, TextAlignmentOptions.Center, ""); F.Stretch((RectTransform)claimLabel.transform);
            var view = bg.gameObject.AddComponent<QuestRowView>();
            F.Set(view, "icon", icon); F.Set(view, "titleText", title); F.Set(view, "descText", desc); F.Set(view, "progressFill", fill); F.Set(view, "progressText", progressText);
            F.Set(view, "rewardText", reward); F.Set(view, "claimButton", claim); F.Set(view, "claimLabel", claimLabel); F.Set(view, "dailyTag", dailyTag.gameObject);
            return view;
        }

        // ------------------------------------------------------------------ ranking / profile / settings / debug
        static RankingPanel BuildRankingPanel(Transform parent)
        {
            var panel = MakePanel<RankingPanel>("RankingPanel", parent, "rank_title", out _, new Vector2(1300f, 860f));
            var content = Content(panel.transform);
            var trophies = F.OutlinedText("Trophies", content, null, 40f, F.Honey, TextAlignmentOptions.Center, "0");
            F.AnchorStretchX((RectTransform)trophies.transform, 0f, 50f, 0f, 0f, 1f, 1f);
            var tIcon = F.Image("Icon", trophies.transform, F.Icon("trophy"), F.Honey, false, false);
            F.Anchor((RectTransform)tIcon.transform, new Vector2(0.5f, 0.5f), new Vector2(-90f, 0f), new Vector2(50f, 50f));
            var scroll = F.ScrollView("List", content, out var listContent);
            F.Stretch((RectTransform)scroll.transform, 0f, 0f, 0f, 64f);
            var vl = listContent.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 12f; vl.padding = new RectOffset(12, 12, 12, 12); vl.childControlWidth = true; vl.childControlHeight = false; vl.childForceExpandWidth = true;
            listContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var row = F.Image("ArenaRowTemplate", listContent, F.Ui("panel"), F.PanelMid, true, false);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 120f;
            var badge = F.Image("Badge", row.transform, F.Icon("leaf"), F.Green, false, false);
            F.Anchor((RectTransform)badge.transform, new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(84f, 84f), new Vector2(0f, 0.5f));
            var nameText = F.Text("Name", row.transform, null, 30f, F.TextLight, TextAlignmentOptions.MidlineLeft, true, "");
            F.Anchor((RectTransform)nameText.transform, new Vector2(0f, 0.5f), new Vector2(120f, 0f), new Vector2(600f, 40f), new Vector2(0f, 0.5f));
            var tro = F.Text("Trophies", row.transform, null, 28f, F.Honey, TextAlignmentOptions.MidlineRight, true, "");
            F.Anchor((RectTransform)tro.transform, new Vector2(1f, 0.5f), new Vector2(-120f, 0f), new Vector2(200f, 40f), new Vector2(1f, 0.5f));
            var marker = F.Image("CurrentMarker", row.transform, F.Icon("check"), F.Green, false, false);
            F.Anchor((RectTransform)marker.transform, new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(60f, 60f), new Vector2(1f, 0.5f));
            var locked = F.Image("LockedOverlay", row.transform, F.Ui("soft"), new Color(0f, 0f, 0f, 0.45f), true, false);
            F.Stretch((RectTransform)locked.transform);
            var view = row.gameObject.AddComponent<ArenaRowView>();
            F.Set(view, "badge", badge); F.Set(view, "nameText", nameText); F.Set(view, "trophiesText", tro); F.Set(view, "currentMarker", marker.gameObject); F.Set(view, "lockedOverlay", locked.gameObject);
            row.gameObject.SetActive(false);
            F.Set(panel, "trophiesText", trophies); F.Set(panel, "listContent", listContent); F.Set(panel, "rowTemplate", view);
            FinishPanel(panel);
            return panel;
        }

        static ProfilePanel BuildProfilePanel(Transform parent)
        {
            var panel = MakePanel<ProfilePanel>("ProfilePanel", parent, "profile_title", out _, new Vector2(1100f, 760f));
            var content = Content(panel.transform);
            var col = F.VerticalGroup("Lines", content, 14f, TextAnchor.UpperLeft);
            F.Stretch(col, 30f, 20f, 30f, 20f);
            string[] names = { "nameText", "levelText", "trophiesText", "bestTrophiesText", "battlesText", "winsText", "winRateText", "collectionText" };
            foreach (var n in names)
            {
                var t = F.Text(n, col, null, 30f, F.TextLight, TextAlignmentOptions.MidlineLeft, n == "nameText", "");
                ((RectTransform)t.transform).sizeDelta = new Vector2(1000f, 44f);
                F.Set(panel, n, t);
            }
            FinishPanel(panel);
            return panel;
        }

        static SettingsPanel BuildSettingsPanel(Transform parent)
        {
            var panel = MakePanel<SettingsPanel>("SettingsPanel", parent, "settings_title", out _, new Vector2(1400f, 940f));
            var content = Content(panel.transform);
            var left = F.VerticalGroup("Left", content, 18f, TextAnchor.UpperLeft);
            F.Anchor(left, new Vector2(0f, 1f), Vector2.zero, new Vector2(640f, 800f), new Vector2(0f, 1f));
            var right = F.VerticalGroup("Right", content, 18f, TextAnchor.UpperLeft);
            F.Anchor(right, new Vector2(1f, 1f), Vector2.zero, new Vector2(640f, 800f), new Vector2(1f, 1f));

            Label(left, "settings_language");
            var langRow = F.HorizontalGroup("LanguageRow", left, 12f); langRow.sizeDelta = new Vector2(640f, 60f);
            F.Set(panel, "englishButton", Chip("English", langRow, "language_en"));
            F.Set(panel, "turkishButton", Chip("Turkish", langRow, "language_tr"));
            Label(left, "settings_music");
            var music = F.Slider("MusicSlider", left, F.Green); F.Set(panel, "musicSlider", music);
            Label(left, "settings_sfx");
            var sfx = F.Slider("SfxSlider", left, F.Green); F.Set(panel, "sfxSlider", sfx);
            F.Set(panel, "vibrationToggle", F.Toggle("VibrationToggle", left, "settings_vibration"));
            F.Set(panel, "reduceHapticsToggle", F.Toggle("ReduceHapticsToggle", left, "settings_reduce_haptics"));
            F.Set(panel, "reduceMotionToggle", F.Toggle("ReduceMotionToggle", left, "settings_reduce_motion"));

            Label(right, "settings_quality");
            var qRow = F.HorizontalGroup("QualityRow", right, 12f); qRow.sizeDelta = new Vector2(640f, 60f);
            F.Set(panel, "qualityLowButton", Chip("Low", qRow, "quality_low"));
            F.Set(panel, "qualityMediumButton", Chip("Medium", qRow, "quality_medium"));
            F.Set(panel, "qualityHighButton", Chip("High", qRow, "quality_high"));
            Label(right, "settings_aim_mode");
            var aRow = F.HorizontalGroup("AimRow", right, 12f); aRow.sizeDelta = new Vector2(640f, 60f);
            F.Set(panel, "aimPullButton", Chip("PullBack", aRow, "aim_pull_back"));
            F.Set(panel, "aimDirectButton", Chip("Direct", aRow, "aim_direct"));
            var replay = F.Button("ReplayTutorialButton", right, F.Blue, "settings_replay_tutorial", 26f); ((RectTransform)replay.transform).sizeDelta = new Vector2(500f, 70f);
            var reset = F.Button("ResetSaveButton", right, F.Red, "settings_reset_save", 26f); ((RectTransform)reset.transform).sizeDelta = new Vector2(500f, 70f);
            var version = F.Text("VersionText", right, null, 22f, new Color(0.6f, 0.65f, 0.7f), TextAlignmentOptions.MidlineLeft, false, ""); ((RectTransform)version.transform).sizeDelta = new Vector2(600f, 36f);
            F.Set(panel, "replayTutorialButton", replay); F.Set(panel, "resetSaveButton", reset); F.Set(panel, "versionText", version);
            FinishPanel(panel);
            return panel;
        }

        static void Label(Transform parent, string key)
        {
            var t = F.Text("Label_" + key, parent, key, 26f, F.Honey, TextAlignmentOptions.MidlineLeft, true);
            ((RectTransform)t.transform).sizeDelta = new Vector2(600f, 36f);
        }

        static DebugPanel BuildDebugPanel(Transform parent)
        {
            var panel = MakePanel<DebugPanel>("DebugPanel", parent, "debug_title", out _, new Vector2(900f, 760f));
            var content = Content(panel.transform);
            var col = F.VerticalGroup("Buttons", content, 12f, TextAnchor.UpperCenter);
            F.Stretch(col);
            string[][] items = { new[] { "addCoinsButton", "debug_add_coins" }, new[] { "addSapButton", "debug_add_sap" }, new[] { "addGemsButton", "debug_add_gems" }, new[] { "unlockAllButton", "debug_unlock_all" }, new[] { "finishChestsButton", "debug_finish_chests" }, new[] { "addChestButton", "chest_grove" }, new[] { "sandboxButton", "menu_sandbox" }, new[] { "resetButton", "debug_reset" } };
            foreach (var it in items)
            {
                var b = F.Button(it[0], col, it[0] == "resetButton" ? F.Red : F.PanelMid, it[1], 26f);
                ((RectTransform)b.transform).sizeDelta = new Vector2(600f, 64f);
                F.Set(panel, it[0], b);
            }
            FinishPanel(panel);
            return panel;
        }

        // ------------------------------------------------------------------ popups
        static T MakePopup<T>(string name, Transform parent, string titleKey, Vector2 size, out TMP_Text title) where T : UIPopup
        {
            var img = F.Image(name, parent, F.Ui("panel"), F.PanelDark, true, true);
            F.Anchor((RectTransform)img.transform, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            F.Group(img.gameObject, 1f);
            var responsive = img.gameObject.AddComponent<ResponsiveWidth>();
            F.Set(responsive, "preferredWidth", size.x);
            F.Set(responsive, "sideMargin", 40f);
            var comp = img.gameObject.AddComponent<T>();
            title = F.OutlinedText("Title", img.transform, titleKey, 42f, F.Honey);
            F.AnchorStretchX((RectTransform)title.transform, -16f, 60f, 40f, 40f, 1f, 1f);
            return comp;
        }

        static GenericConfirmPopup BuildConfirmPopup(Transform parent)
        {
            var p = MakePopup<GenericConfirmPopup>("GenericConfirmPopup", parent, null, new Vector2(900f, 460f), out var title);
            var body = F.Text("Body", p.transform, null, 28f, F.TextLight, TextAlignmentOptions.Center, false, "");
            F.AnchorStretchX((RectTransform)body.transform, -90f, 200f, 40f, 40f, 1f, 1f);
            var row = F.HorizontalGroup("Buttons", p.transform, 20f, TextAnchor.MiddleCenter);
            F.AnchorStretchX(row, 26f, 90f, 40f, 40f, 0f, 0f);
            var no = F.Button("NoButton", row, F.PanelMid, null, 28f); ((RectTransform)no.transform).sizeDelta = new Vector2(300f, 84f);
            var noLabel = F.OutlinedText("Label", no.transform, null, 28f, F.TextLight, TextAlignmentOptions.Center, ""); F.Stretch((RectTransform)noLabel.transform);
            var yes = F.Button("YesButton", row, F.Green, null, 28f); ((RectTransform)yes.transform).sizeDelta = new Vector2(300f, 84f);
            var yesLabel = F.OutlinedText("Label", yes.transform, null, 28f, F.TextLight, TextAlignmentOptions.Center, ""); F.Stretch((RectTransform)yesLabel.transform);
            F.Set(p, "titleText", title); F.Set(p, "bodyText", body); F.Set(p, "yesButton", yes); F.Set(p, "noButton", no); F.Set(p, "yesLabel", yesLabel); F.Set(p, "noLabel", noLabel);
            FinishPanel(p);
            return p;
        }

        static RewardItemView RewardTile(string name, Transform parent, Vector2 size)
        {
            var bg = F.Image(name, parent, F.Ui("card"), F.PanelMid, true, false);
            ((RectTransform)bg.transform).sizeDelta = size;
            F.Group(bg.gameObject, 1f);
            var icon = F.Image("Icon", bg.transform, null, Color.white, false, false);
            F.Anchor((RectTransform)icon.transform, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(size.x * 0.62f, size.x * 0.62f), new Vector2(0.5f, 1f));
            var label = F.OutlinedText("Label", bg.transform, null, 28f, F.TextLight, TextAlignmentOptions.Center, "");
            F.AnchorStretchX((RectTransform)label.transform, 40f, 36f, 6f, 6f, 0f, 0f);
            var sub = F.Text("Sub", bg.transform, null, 20f, new Color(0.85f, 0.88f, 0.9f), TextAlignmentOptions.Center, false, "");
            F.AnchorStretchX((RectTransform)sub.transform, 10f, 30f, 6f, 6f, 0f, 0f);
            var newBadge = F.Image("NewBadge", bg.transform, F.Ui("pill"), F.Honey, true, false);
            F.Anchor((RectTransform)newBadge.transform, new Vector2(0.5f, 1f), new Vector2(0f, 12f), new Vector2(90f, 34f), new Vector2(0.5f, 1f));
            var newText = F.OutlinedText("Text", newBadge.transform, "ui_new", 20f, F.TextDark); F.Stretch((RectTransform)newText.transform);
            var view = bg.gameObject.AddComponent<RewardItemView>();
            F.Set(view, "icon", icon); F.Set(view, "label", label); F.Set(view, "sub", sub); F.Set(view, "newBadge", newBadge.gameObject); F.Set(view, "group", bg.GetComponent<CanvasGroup>());
            return view;
        }

        static RewardPopup BuildRewardPopup(Transform parent)
        {
            var p = MakePopup<RewardPopup>("RewardPopup", parent, "popup_reward_title", new Vector2(1500f, 640f), out var title);
            var row = F.HorizontalGroup("Tiles", p.transform, 14f, TextAnchor.MiddleCenter);
            F.AnchorStretchX(row, -20f, 300f, 30f, 30f, 0.5f, 0.5f);
            row.gameObject.AddComponent<FitRowScale>();
            var tiles = new Object[8];
            for (int i = 0; i < 8; i++) { var t = RewardTile("Tile_" + i, row, new Vector2(170f, 230f)); t.gameObject.SetActive(false); tiles[i] = t; }
            var overflow = F.Text("Overflow", p.transform, null, 26f, F.Honey, TextAlignmentOptions.Center, true, "");
            F.AnchorStretchX((RectTransform)overflow.transform, 120f, 36f, 40f, 40f, 0f, 0f);
            var collect = F.Button("CollectButton", p.transform, F.Honey, "ui_collect_all", 32f);
            F.Anchor((RectTransform)collect.transform, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(420f, 90f), new Vector2(0.5f, 0f));
            F.Set(p, "titleText", title); F.SetArray(p, "tiles", tiles); F.Set(p, "overflowText", overflow); F.Set(p, "collectButton", collect);
            WireRewardSprites(p);
            FinishPanel(p);
            return p;
        }

        static InsufficientCurrencyPopup BuildInsufficientPopup(Transform parent)
        {
            var p = MakePopup<InsufficientCurrencyPopup>("InsufficientCurrencyPopup", parent, "insufficient_title", new Vector2(900f, 500f), out var title);
            var icon = F.Image("Icon", p.transform, F.Icon("coin"), F.Honey, false, false);
            F.Anchor((RectTransform)icon.transform, new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(120f, 120f));
            var body = F.Text("Body", p.transform, null, 28f, F.TextLight, TextAlignmentOptions.Center, false, "");
            F.AnchorStretchX((RectTransform)body.transform, -260f, 80f, 40f, 40f, 1f, 1f);
            var row = F.HorizontalGroup("Buttons", p.transform, 20f, TextAnchor.MiddleCenter);
            F.AnchorStretchX(row, 26f, 90f, 40f, 40f, 0f, 0f);
            var ok = F.Button("OkButton", row, F.PanelMid, "ui_ok", 28f); ((RectTransform)ok.transform).sizeDelta = new Vector2(280f, 84f);
            var shop = F.Button("ShopButton", row, F.Honey, "currency_go_shop", 28f); ((RectTransform)shop.transform).sizeDelta = new Vector2(320f, 84f);
            F.Set(p, "titleText", title); F.Set(p, "bodyText", body); F.Set(p, "icon", icon); F.Set(p, "okButton", ok); F.Set(p, "shopButton", shop);
            WireRewardSprites(p);
            FinishPanel(p);
            return p;
        }

        static UnlockPopup BuildUnlockPopup(Transform parent)
        {
            var p = MakePopup<UnlockPopup>("UnlockPopup", parent, "popup_unlock_title", new Vector2(1000f, 760f), out var title);
            var glow = F.Image("Glow", p.transform, F.Ui("glow"), F.Honey, false, false);
            F.Anchor((RectTransform)glow.transform, new Vector2(0.5f, 0.6f), Vector2.zero, new Vector2(520f, 520f));
            var frame = F.Image("Frame", p.transform, F.Ui("card"), Color.white, true, false);
            F.Anchor((RectTransform)frame.transform, new Vector2(0.5f, 0.6f), Vector2.zero, new Vector2(300f, 300f));
            var portrait = F.Image("Portrait", frame.transform, null, Color.white, false, false);
            F.Stretch((RectTransform)portrait.transform, 14f, 14f, 14f, 14f);
            var nameText = F.OutlinedText("Name", p.transform, null, 40f, F.TextLight, TextAlignmentOptions.Center, "");
            F.AnchorStretchX((RectTransform)nameText.transform, 190f, 50f, 40f, 40f, 0f, 0f);
            var rarity = F.Text("Rarity", p.transform, null, 26f, F.Honey, TextAlignmentOptions.Center, true, "");
            F.AnchorStretchX((RectTransform)rarity.transform, 156f, 34f, 40f, 40f, 0f, 0f);
            var desc = F.Text("Description", p.transform, null, 24f, new Color(0.85f, 0.88f, 0.9f), TextAlignmentOptions.Center, false, "");
            F.AnchorStretchX((RectTransform)desc.transform, 100f, 56f, 60f, 60f, 0f, 0f);
            var ok = F.Button("OkButton", p.transform, F.Honey, "ui_ok", 30f);
            F.Anchor((RectTransform)ok.transform, new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(320f, 76f), new Vector2(0.5f, 0f));
            F.Set(p, "titleText", title); F.Set(p, "portrait", portrait); F.Set(p, "frame", frame); F.Set(p, "nameText", nameText); F.Set(p, "rarityText", rarity); F.Set(p, "descriptionText", desc); F.Set(p, "okButton", ok); F.Set(p, "punchTarget", frame.transform);
            FinishPanel(p);
            return p;
        }

        static DailyRewardPopup BuildDailyRewardPopup(Transform parent)
        {
            var p = MakePopup<DailyRewardPopup>("DailyRewardPopup", parent, "daily_title", new Vector2(1600f, 640f), out var title);
            var row = F.HorizontalGroup("Days", p.transform, 12f, TextAnchor.MiddleCenter);
            F.AnchorStretchX(row, -10f, 300f, 30f, 30f, 0.5f, 0.5f);
            row.gameObject.AddComponent<FitRowScale>();
            var tiles = new Object[7]; var highlights = new Object[7]; var labels = new Object[7];
            for (int i = 0; i < 7; i++)
            {
                var holder = F.Rect("Day_" + i, row); holder.sizeDelta = new Vector2(200f, 290f);
                var hl = F.Image("Highlight", holder, F.Ui("card"), F.Honey, true, false); F.Stretch((RectTransform)hl.transform, -8f, -8f, -8f, -8f); hl.enabled = false;
                var tile = RewardTile("Tile", holder, new Vector2(200f, 240f)); F.Anchor((RectTransform)tile.transform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(200f, 240f), new Vector2(0.5f, 1f));
                var label = F.Text("DayLabel", holder, null, 22f, F.TextLight, TextAlignmentOptions.Center, true, ""); F.AnchorStretchX((RectTransform)label.transform, 0f, 40f, 0f, 0f, 0f, 0f);
                tiles[i] = tile; highlights[i] = hl; labels[i] = label;
            }
            var close = F.IconButton("CloseButton", p.transform, F.Red, F.Icon("close"), F.TextLight);
            F.Anchor((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-14f, -14f), new Vector2(76f, 76f), new Vector2(1f, 1f));
            var claim = F.Button("ClaimButton", p.transform, F.Honey, null, 30f);
            F.Anchor((RectTransform)claim.transform, new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(460f, 84f), new Vector2(0.5f, 0f));
            var claimLabel = F.OutlinedText("Label", claim.transform, null, 28f, F.TextDark, TextAlignmentOptions.Center, ""); F.Stretch((RectTransform)claimLabel.transform);
            F.Set(p, "titleText", title); F.SetArray(p, "dayTiles", tiles); F.SetArray(p, "dayHighlights", highlights); F.SetArray(p, "dayLabels", labels); F.Set(p, "claimButton", claim); F.Set(p, "claimLabel", claimLabel); F.Set(p, "closeButton", close);
            WireRewardSprites(p);
            FinishPanel(p);
            return p;
        }

        static ChestOpenPopup BuildChestOpenPopup(Transform parent)
        {
            var p = MakePopup<ChestOpenPopup>("ChestOpenPopup", parent, "chest_open", new Vector2(1600f, 800f), out var title);
            var glow = F.Image("Glow", p.transform, F.Ui("glow"), F.Honey, false, false);
            F.Anchor((RectTransform)glow.transform, new Vector2(0.5f, 0.66f), Vector2.zero, new Vector2(520f, 520f));
            var chestBtn = F.Button("ChestButton", p.transform, Color.white, null, 20f, F.Sprite(ArtPaths.Chest("twig", false)));
            F.Anchor((RectTransform)chestBtn.transform, new Vector2(0.5f, 0.66f), Vector2.zero, new Vector2(420f, 350f));
            var chestImg = chestBtn.GetComponent<Image>(); chestImg.type = Image.Type.Simple; chestImg.preserveAspect = true;
            var hint = F.Text("Hint", p.transform, null, 26f, F.Honey, TextAlignmentOptions.Center, false, "");
            F.AnchorStretchX((RectTransform)hint.transform, -400f, 40f, 40f, 40f, 1f, 1f);
            var row = F.HorizontalGroup("Tiles", p.transform, 12f, TextAnchor.MiddleCenter);
            F.AnchorStretchX(row, 120f, 220f, 30f, 30f, 0f, 0f);
            var tiles = new Object[8];
            for (int i = 0; i < 8; i++) { var t = RewardTile("Tile_" + i, row, new Vector2(160f, 210f)); t.gameObject.SetActive(false); tiles[i] = t; }
            var collect = F.Button("CollectButton", p.transform, F.Honey, "ui_collect_all", 30f);
            F.Anchor((RectTransform)collect.transform, new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(420f, 84f), new Vector2(0.5f, 0f));
            F.Set(p, "chestImage", chestImg); F.Set(p, "glow", glow); F.Set(p, "chestButton", chestBtn); F.Set(p, "hintText", hint); F.SetArray(p, "tiles", tiles); F.Set(p, "collectButton", collect);
            WireRewardSprites(p);
            FinishPanel(p);
            return p;
        }

        static ConnectionInfoPopup BuildConnectionPopup(Transform parent)
        {
            var p = MakePopup<ConnectionInfoPopup>("ConnectionInfoPopup", parent, "popup_connection_title", new Vector2(900f, 420f), out _);
            var body = F.Text("Body", p.transform, "popup_connection_body", 26f, F.TextLight, TextAlignmentOptions.Center, false);
            F.AnchorStretchX((RectTransform)body.transform, -90f, 180f, 40f, 40f, 1f, 1f);
            var ok = F.Button("OkButton", p.transform, F.Honey, "ui_ok", 28f);
            F.Anchor((RectTransform)ok.transform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(300f, 80f), new Vector2(0.5f, 0f));
            F.Set(p, "okButton", ok);
            FinishPanel(p);
            return p;
        }

        static UIToast BuildToast(Transform parent)
        {
            var pill = F.Image("Toast", parent, F.Ui("pill"), new Color(0.1f, 0.12f, 0.16f, 0.95f), true, false);
            F.Anchor((RectTransform)pill.transform, new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(900f, 70f), new Vector2(0.5f, 1f));
            F.Group(pill.gameObject, 0f);
            var label = F.Text("Label", pill.transform, null, 26f, F.TextLight, TextAlignmentOptions.Center, false, "");
            F.Stretch((RectTransform)label.transform, 24f, 4f, 24f, 4f);
            var toast = pill.gameObject.AddComponent<UIToast>();
            F.Set(toast, "label", label);
            return toast;
        }
    }
}
