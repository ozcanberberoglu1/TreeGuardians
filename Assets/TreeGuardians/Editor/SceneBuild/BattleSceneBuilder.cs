using System.Collections.Generic;
using TMPro;
using TreeGuardians.AI;
using TreeGuardians.Battle;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Guardians;
using TreeGuardians.Tools;
using TreeGuardians.Trees;
using TreeGuardians.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using F = TreeGuardians.Editor.UIFactory;

namespace TreeGuardians.Editor.SceneBuild
{
    /// 03_Battle: environment layers, two pre-authored trees (sections, slots, guardians, mounts), pools, systems and HUD.
    public static class BattleSceneBuilder
    {
        const float GroundY = -3.3f;
        const float TreeX = 7.6f;
        public const string ProjectilePrefabPath = "Assets/TreeGuardians/Prefabs/Projectiles/Projectile_Default.prefab";
        public const string VfxFolder = "Assets/TreeGuardians/Prefabs/VFX";

        static readonly Color BarBack = new Color(0.08f, 0.1f, 0.14f, 0.85f);
        static TreeGuardians.Tutorial.BattleTutorialController tutorialController;

        public static void Build() => BuildScene("03_Battle", false);

        public static void BuildSandbox() => BuildScene("05_Sandbox", true);

        static void BuildScene(string sceneName, bool sandbox)
        {
            var scene = SceneBuildUtility.NewScene(sceneName);
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(ContentBuilder.DatabasePath);
            var playerDef = db != null ? db.playerTree : null;
            var enemyDef = db != null ? db.enemyTree : null;

            F.Root("Battle_Root");
            var cam = F.Camera2D("BattleCamera", 6.2f, new Color(0.45f, 0.72f, 0.95f));
            cam.transform.position = new Vector3(0f, 1.9f, -10f);
            var camCtrl = cam.gameObject.AddComponent<BattleCameraController>();
            F.Set(camCtrl, "cam", cam);

            var arenaPresenter = BuildEnvironment();
            var bounds = F.Root("ArenaBounds");
            F.Child("GroundLine", bounds.transform, new Vector3(0f, GroundY, 0f));

            var projectilePrefab = BuildProjectilePrefab();
            BuildVfxPrefabs(out var burst, out var hit, out var heal, out var poison, out var shard, out var dmgText);

            var playerSide = F.Root("PlayerSide");
            var playerTree = BuildTree("PlayerTree", playerSide.transform, playerDef, BattleSide.Player, new Vector3(-TreeX, GroundY, 0f), false, out var playerRoster, out var playerTools);
            var enemySide = F.Root("EnemySide");
            var enemyTree = BuildTree("EnemyTree", enemySide.transform, enemyDef, BattleSide.Enemy, new Vector3(TreeX, GroundY, 0f), true, out var enemyRoster, out var enemyTools);

            var projectileRoot = F.Root("ProjectilePoolRoot");
            var vfxRoot = F.Root("VFXPoolRoot");
            var textRoot = F.Root("DamageTextPoolRoot");

            var systems = F.Root("BattleSystems");
            var manager = F.Child("BattleManager", systems.transform).AddComponent<BattleManager>();
            F.Child("BattleStateMachine", systems.transform);
            F.Child("TurnOrEnergyController", systems.transform);
            F.Child("TargetingController", systems.transform);
            var projectiles = F.Child("ProjectileService", systems.transform).AddComponent<ProjectileService>();
            F.Child("DamageResolver", systems.transform);
            F.Child("DestructionController", systems.transform);
            var bot = F.Child("BattleBotController", systems.transform).AddComponent<BotBattleCommander>();
            F.Child("BattleAudioController", systems.transform);
            F.Child("BattleCameraController", systems.transform);
            var playerCommander = F.Child("PlayerBattleCommander", systems.transform).AddComponent<PlayerBattleCommander>();
            var vfx = F.Child("BattleVFX", systems.transform).AddComponent<BattleVFX>();

            F.Set(projectiles, "poolRoot", projectileRoot.transform);
            F.Set(projectiles, "defaultPrefab", projectilePrefab);
            F.Set(projectiles, "groundY", GroundY);
            F.Set(vfx, "vfxRoot", vfxRoot.transform);
            F.Set(vfx, "textRoot", textRoot.transform);
            F.Set(vfx, "burstPrefab", burst); F.Set(vfx, "hitPrefab", hit); F.Set(vfx, "healPrefab", heal); F.Set(vfx, "poisonPrefab", poison); F.Set(vfx, "shardPrefab", shard); F.Set(vfx, "damageTextPrefab", dmgText);

            tutorialController = null;
            var hud = BuildHUD(cam, out var aimView);
            F.Set(playerCommander, "worldCamera", cam);
            F.Set(playerCommander, "aimView", aimView);
            if (tutorialController != null)
            {
                F.Set(tutorialController, "manager", manager);
                F.Set(tutorialController, "commander", playerCommander);
            }

            F.Set(manager, "playerTree", playerTree); F.Set(manager, "enemyTree", enemyTree);
            F.Set(manager, "playerRoster", playerRoster); F.Set(manager, "enemyRoster", enemyRoster);
            F.Set(manager, "playerTools", playerTools); F.Set(manager, "enemyTools", enemyTools);
            F.Set(manager, "projectiles", projectiles); F.Set(manager, "vfx", vfx); F.Set(manager, "hud", hud);
            F.Set(manager, "arenaPresenter", arenaPresenter); F.Set(manager, "playerCommander", playerCommander); F.Set(manager, "botCommander", bot);
            F.Set(manager, "cameraController", camCtrl); F.Set(manager, "battleCamera", cam);

            if (db != null)
            {
                var so = new SerializedObject(db);
                so.FindProperty("defaultProjectilePrefab").objectReferenceValue = projectilePrefab;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            if (sandbox) BuildSandboxUI(manager, db);

            F.EventSystem();
            SceneBuildUtility.Save(scene, sceneName);
        }

        // ------------------------------------------------------------------ sandbox
        static void BuildSandboxUI(BattleManager manager, GameDatabase db)
        {
            var canvas = F.Canvas("SandboxCanvas", 50);
            var safe = F.SafeArea(canvas.transform);
            var controller = F.Root("SandboxController").AddComponent<TreeGuardians.Debugging.SandboxController>();
            var toggle = F.Button("SandboxToggle", safe, F.Purple, null, 22f, null, null, "SANDBOX");
            F.Anchor((RectTransform)toggle.transform, new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(180f, 54f), new Vector2(0f, 1f));

            var panelImg = F.Image("SandboxPanel", safe, F.Ui("panel"), new Color(0.1f, 0.12f, 0.18f, 0.94f), true, true);
            F.Anchor((RectTransform)panelImg.transform, new Vector2(0f, 0.5f), new Vector2(20f, 40f), new Vector2(760f, 900f), new Vector2(0f, 0.5f));
            F.Group(panelImg.gameObject, 1f);
            var panel = panelImg.gameObject.AddComponent<UIPanel>();
            var info = F.Text("Info", panelImg.transform, null, 18f, F.Honey, TextAlignmentOptions.TopLeft, false, "");
            F.AnchorStretchX((RectTransform)info.transform, -12f, 60f, 16f, 16f, 1f, 1f);

            var scroll = F.ScrollView("Scroll", panelImg.transform, out var content);
            F.Stretch((RectTransform)scroll.transform, 12f, 12f, 12f, 76f);
            var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 6f; vl.padding = new RectOffset(8, 8, 8, 8); vl.childControlWidth = true; vl.childControlHeight = false; vl.childForceExpandWidth = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var gButtons = new List<Object>(); var gIds = new List<string>();
            SectionLabel(content, "Guardians (assign to next player slot)");
            var gGrid = Grid(content, 3, 240f, 46f);
            if (db != null) foreach (var g in db.guardians) { if (g == null) continue; gButtons.Add(SmallButton(gGrid, g.id)); gIds.Add(g.id); }
            var pButtons = new List<Object>(); var pIds = new List<string>();
            SectionLabel(content, "Projectiles (fire at enemy core)");
            var pGrid = Grid(content, 3, 240f, 46f);
            if (db != null) foreach (var p in db.projectiles) { if (p == null) continue; pButtons.Add(SmallButton(pGrid, p.id)); pIds.Add(p.id); }
            var tButtons = new List<Object>(); var tIds = new List<string>();
            SectionLabel(content, "Tools (equipped only)");
            var tGrid = Grid(content, 3, 240f, 46f);
            if (db != null) foreach (var t in db.tools) { if (t == null) continue; tButtons.Add(SmallButton(tGrid, t.id)); tIds.Add(t.id); }
            SectionLabel(content, "Actions");
            var aGrid = Grid(content, 2, 360f, 50f);
            var dmg = SmallButton(aGrid, "Damage enemy section");
            var brk = SmallButton(aGrid, "Break enemy branch");
            var heal = SmallButton(aGrid, "Heal all (player)");
            var energy = SmallButton(aGrid, "Fill special energy");
            var win = SmallButton(aGrid, "Force WIN");
            var lose = SmallButton(aGrid, "Force LOSE");
            var time = SmallButton(aGrid, "+60s");
            var reset = SmallButton(aGrid, "Reload sandbox");

            F.Set(controller, "manager", manager); F.Set(controller, "panel", panel); F.Set(controller, "toggleButton", toggle);
            F.SetArray(controller, "guardianButtons", gButtons.ToArray()); SetStrings(controller, "guardianIds", gIds);
            F.SetArray(controller, "projectileButtons", pButtons.ToArray()); SetStrings(controller, "projectileIds", pIds);
            F.SetArray(controller, "toolButtons", tButtons.ToArray()); SetStrings(controller, "toolIds", tIds);
            F.Set(controller, "damageSectionButton", dmg); F.Set(controller, "breakBranchButton", brk); F.Set(controller, "healAllButton", heal); F.Set(controller, "fillEnergyButton", energy);
            F.Set(controller, "winButton", win); F.Set(controller, "loseButton", lose); F.Set(controller, "addTimeButton", time); F.Set(controller, "resetButton", reset); F.Set(controller, "infoText", info);
            var grp = panelImg.GetComponent<CanvasGroup>(); grp.alpha = 0f; grp.blocksRaycasts = false; grp.interactable = false;
            panelImg.gameObject.SetActive(false);
        }

        static void SectionLabel(Transform parent, string text)
        {
            var t = F.Text("Label", parent, null, 20f, F.Honey, TextAlignmentOptions.MidlineLeft, true, text);
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
        }

        static RectTransform Grid(Transform parent, int columns, float cellW, float cellH)
        {
            var rt = F.Rect("Grid", parent);
            var g = rt.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(cellW, cellH); g.spacing = new Vector2(6f, 6f); g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = columns;
            rt.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rt;
        }

        static Button SmallButton(Transform parent, string label)
        {
            var b = F.Button(label, parent, F.PanelMid, null, 16f, null, null, label);
            return b;
        }

        static void SetStrings(Component c, string field, List<string> values)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(field);
            if (p == null) return;
            p.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) p.GetArrayElementAtIndex(i).stringValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ environment
        static ArenaPresenter BuildEnvironment()
        {
            var env = F.Root("BattleEnvironment");
            var presenter = env.AddComponent<ArenaPresenter>();
            var sky = F.Child("SkyGradient", env.transform, new Vector3(0f, 2f, 6f));
            sky.AddComponent<SpriteRenderer>().sortingOrder = -100;
            var grad = sky.AddComponent<GradientSprite>();
            var gso = new SerializedObject(grad);
            gso.FindProperty("worldSize").vector2Value = new Vector2(44f, 18f);
            gso.ApplyModifiedPropertiesWithoutUndo();

            var cloudsRoot = F.Child("CloudsFar", env.transform);
            var clouds = new Object[3];
            for (int i = 0; i < 3; i++)
            {
                var c = F.WorldSprite("Cloud_" + i, cloudsRoot.transform, F.Sprite(ArtPaths.Arena("clouds")), new Color(1f, 1f, 1f, 0.9f), -90, new Vector3(-12f + i * 12f, 5.2f + (i % 2) * 1.2f, 5f));
                c.transform.localScale = new Vector3(2.4f, 2.4f, 1f);
                clouds[i] = c;
            }
            var mountains = F.WorldSprite("MountainsFar", env.transform, F.Sprite(ArtPaths.Arena("mountains")), new Color(0.56f, 0.66f, 0.82f), -80, new Vector3(0f, -0.2f, 4f));
            mountains.transform.localScale = new Vector3(6f, 2.6f, 1f);
            var forestBack = F.WorldSprite("ForestBack", env.transform, F.Sprite(ArtPaths.Arena("forest_back")), new Color(0.28f, 0.5f, 0.38f), -70, new Vector3(0f, -1.6f, 3f));
            forestBack.transform.localScale = new Vector3(6f, 2.4f, 1f);
            var forestMid = F.WorldSprite("ForestMid", env.transform, F.Sprite(ArtPaths.Arena("forest_mid")), new Color(0.32f, 0.58f, 0.36f), -60, new Vector3(0f, -2.4f, 2f));
            forestMid.transform.localScale = new Vector3(6f, 2.6f, 1f);
            var mist = F.WorldSprite("Mist", env.transform, F.Sprite(ArtPaths.Arena("mist")), new Color(1f, 1f, 1f, 0.35f), -55, new Vector3(0f, -1.2f, 1.5f));
            mist.transform.localScale = new Vector3(6f, 2.5f, 1f);
            mist.enabled = false;

            var particles = F.Child("AtmosphereParticles", env.transform);
            var leaves = MakeParticles("Leaves", particles.transform, F.Sprite(ArtPaths.Vfx("leaf")), new Color(0.6f, 0.85f, 0.4f), 0.28f, 4f, new Vector2(-0.6f, 0.6f), new Vector2(-1.4f, -0.7f), 40, -45);
            var fireflies = MakeParticles("Fireflies", particles.transform, F.Sprite(ArtPaths.Vfx("glow")), new Color(1f, 0.95f, 0.5f), 0.14f, 3f, new Vector2(-0.4f, 0.4f), new Vector2(-0.2f, 0.3f), 36, -45);

            var ground = F.WorldSprite("StaticGround", env.transform, F.Sprite(ArtPaths.Arena("ground")), new Color(0.42f, 0.66f, 0.32f), -50, new Vector3(0f, GroundY + 0.15f, 0f));
            ground.transform.localScale = new Vector3(6f, 3f, 1f);
            var foreground = F.WorldSprite("ForegroundPlants", env.transform, F.Sprite(ArtPaths.Arena("foreground")), new Color(0.3f, 0.5f, 0.25f), 30, new Vector3(0f, GroundY - 0.9f, -1f));
            foreground.transform.localScale = new Vector3(6f, 1.6f, 1f);

            F.Set(presenter, "sky", grad);
            F.SetArray(presenter, "clouds", clouds);
            F.Set(presenter, "mountains", mountains); F.Set(presenter, "forestBack", forestBack); F.Set(presenter, "forestMid", forestMid);
            F.Set(presenter, "ground", ground); F.Set(presenter, "foreground", foreground); F.Set(presenter, "mist", mist);
            F.Set(presenter, "leaves", leaves); F.Set(presenter, "fireflies", fireflies);
            return presenter;
        }

        static ParticleSystem MakeParticles(string name, Transform parent, Sprite sprite, Color color, float size, float life, Vector2 vx, Vector2 vy, int max, int order)
        {
            var go = F.Child(name, parent, new Vector3(0f, 7.5f, 1f));
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = life;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.7f, size * 1.3f);
            main.startColor = color;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            var emission = ps.emission;
            emission.rateOverTime = max / life * 0.8f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(30f, 4f, 1f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(vx.x, vx.y);
            vel.y = new ParticleSystem.MinMaxCurve(vy.x, vy.y);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            renderer.sortingOrder = order;
            var tsa = ps.textureSheetAnimation;
            tsa.enabled = true;
            tsa.mode = ParticleSystemAnimationMode.Sprites;
            if (sprite != null) tsa.AddSprite(sprite);
            return ps;
        }

        // ------------------------------------------------------------------ prefabs
        static GameObject BuildProjectilePrefab()
        {
            ContentBuilder.EnsureFolder("Assets/TreeGuardians/Prefabs/Projectiles");
            var go = new GameObject("Projectile_Default");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = F.Sprite(ArtPaths.Projectile("generic"));
            sr.sortingOrder = 20;
            go.transform.localScale = Vector3.one * 0.7f;
            var pc = go.AddComponent<ProjectileController>();
            F.Set(pc, "sprite", sr);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, ProjectilePrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static void BuildVfxPrefabs(out VfxSprite burst, out VfxSprite hit, out VfxSprite heal, out VfxSprite poison, out VfxSprite shard, out DamageText dmgText)
        {
            ContentBuilder.EnsureFolder(VfxFolder);
            burst = VfxPrefab("Vfx_Burst", ArtPaths.Vfx("burst"), 25);
            hit = VfxPrefab("Vfx_Hit", ArtPaths.Vfx("hit"), 26);
            heal = VfxPrefab("Vfx_Heal", ArtPaths.Vfx("heal"), 26);
            poison = VfxPrefab("Vfx_Poison", ArtPaths.Vfx("poison"), 15);
            shard = VfxPrefab("Vfx_Shard", ArtPaths.Vfx("bark_shard"), 24);

            var go = new GameObject("DamageText");
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = F.Font;
            tmp.fontSize = 4f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.text = "12";
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(3f, 1f);
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 60;
            var dt = go.AddComponent<DamageText>();
            F.Set(dt, "text", tmp);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, VfxFolder + "/DamageText.prefab");
            Object.DestroyImmediate(go);
            dmgText = prefab.GetComponent<DamageText>();
        }

        static VfxSprite VfxPrefab(string name, string spritePath, int order)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = F.Sprite(spritePath);
            sr.sortingOrder = order;
            var v = go.AddComponent<VfxSprite>();
            F.Set(v, "sprite", sr);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{VfxFolder}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<VfxSprite>();
        }

        // ------------------------------------------------------------------ trees
        static TreeController BuildTree(string name, Transform parent, TreeDefinition def, BattleSide side, Vector3 position, bool mirrored, out GuardianRoster roster, out ToolController tools)
        {
            var root = F.Child(name, parent, position);
            if (mirrored) root.transform.localScale = new Vector3(-1f, 1f, 1f);
            var tree = root.AddComponent<TreeController>();
            roster = parent.gameObject.AddComponent<GuardianRoster>();
            tools = parent.gameObject.AddComponent<ToolController>();
            F.Set(roster, "side", (float)(int)side);
            F.Set(tools, "side", (float)(int)side);
            F.Set(tree, "definition", def);
            F.Set(tree, "side", (float)(int)side);

            var visuals = def != null ? def.GetVisuals(TreeVisualTier.Sprouting) : null;
            var barkColor = visuals != null ? visuals.barkColor : new Color(0.6f, 0.45f, 0.3f);
            var leafColor = visuals != null ? visuals.leafColor : new Color(0.5f, 0.8f, 0.45f);

            var trunkRoot = F.Child("TrunkSections", root.transform);
            var branchRoot = F.Child("BranchSections", root.transform);
            var barkRoot = F.Child("BarkArmorSections", root.transform);
            var coreRoot = F.Child("HeartwoodCore", root.transform);
            var otherRoot = F.Child("SupportSections", root.transform);
            F.Child("DamageVFXAnchors", root.transform);

            var sections = new List<Object>();
            var barkRenderers = new List<Object>();
            var leafRenderers = new List<Object>();
            TreeSection core = null;
            SpriteRenderer heartGlow = null;

            if (def != null)
            {
                foreach (var spec in def.sections)
                {
                    Transform p = spec.type switch
                    {
                        TreeSectionType.Trunk => trunkRoot.transform,
                        TreeSectionType.Branch => branchRoot.transform,
                        TreeSectionType.BarkArmor => barkRoot.transform,
                        TreeSectionType.HeartwoodCore => coreRoot.transform,
                        _ => otherRoot.transform
                    };
                    var section = BuildSection(spec, p, barkColor, leafColor, out var sr, out var glow);
                    sections.Add(section);
                    if (spec.type == TreeSectionType.HeartwoodCore) { core = section; heartGlow = glow; }
                    else if (spec.type == TreeSectionType.CanopyShield) leafRenderers.Add(sr);
                    else barkRenderers.Add(sr);
                }
            }

            var slotsRoot = F.Child("GuardianSlots_01_08", root.transform);
            var slots = new Object[8];
            var guardians = new Object[8];
            for (int i = 0; i < 8; i++)
            {
                Vector2 pos = def != null ? def.guardianSlotPositions[i] : new Vector2(2f, 2f + i);
                var slot = F.Child($"Slot_{i + 1:00}", slotsRoot.transform, new Vector3(pos.x, pos.y, 0f));
                slots[i] = slot.transform;
                guardians[i] = BuildGuardianView($"Guardian_{i + 1:00}", slot.transform);
            }
            var mountsRoot = F.Child("ToolMounts_01_03", root.transform);
            var mounts = new Object[3];
            for (int i = 0; i < 3; i++)
            {
                Vector2 pos = def != null ? def.toolMountPositions[i] : new Vector2(1f, 0.5f);
                var m = F.Child($"Mount_{i + 1:00}", mountsRoot.transform, new Vector3(pos.x, pos.y, 0f));
                var ms = F.WorldSprite("Marker", m.transform, F.Sprite(ArtPaths.TreePart("platform")), barkColor * 0.9f, 5, Vector3.zero);
                ms.transform.localScale = new Vector3(0.45f, 0.45f, 1f);
                mounts[i] = m.transform;
            }

            var barRoot = F.Child("TreeHealthWorldBar", root.transform, new Vector3(0f, 9.6f, 0f));
            var barBack = F.WorldSprite("Back", barRoot.transform, F.Ui("white"), BarBack, 40, Vector3.zero);
            barBack.transform.localScale = new Vector3(90f, 8f, 1f);
            var fillPivot = F.Child("FillPivot", barRoot.transform, new Vector3(-1.72f, 0f, 0f));
            var barFill = F.WorldSprite("Fill", fillPivot.transform, F.Ui("white"), F.Green, 41, new Vector3(1.72f, 0f, 0f));
            barFill.transform.localScale = new Vector3(86f, 5.5f, 1f);
            var heartIcon = F.WorldSprite("Icon", barRoot.transform, F.Icon("heart"), F.Red, 42, new Vector3(-2.15f, 0f, 0f));
            heartIcon.transform.localScale = new Vector3(0.3f, 0.3f, 1f);

            F.SetArray(tree, "sections", sections.ToArray());
            F.Set(tree, "core", core);
            F.SetArray(tree, "guardianSlots", slots);
            F.SetArray(tree, "toolMounts", mounts);
            F.SetArray(tree, "barkRenderers", barkRenderers.ToArray());
            F.SetArray(tree, "leafRenderers", leafRenderers.ToArray());
            F.Set(tree, "heartwoodGlow", heartGlow);
            F.Set(tree, "healthBarFill", fillPivot.transform);
            F.Set(tree, "healthBarFillRenderer", barFill);
            SetGradient(tree, "healthGradient");
            F.SetArray(roster, "slots", guardians);
            F.SetArray(tools, "mounts", mounts);
            return tree;
        }

        static TreeSection BuildSection(TreeSectionSpec spec, Transform parent, Color barkColor, Color leafColor, out SpriteRenderer renderer, out SpriteRenderer glow)
        {
            glow = null;
            var go = F.Child(spec.sectionId, parent, new Vector3(spec.localPosition.x, spec.localPosition.y, 0f));
            var section = go.AddComponent<TreeSection>();
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = spec.size;

            Sprite s0, s1, s2;
            Color color;
            int order;
            switch (spec.type)
            {
                case TreeSectionType.Trunk: s0 = F.Sprite(ArtPaths.Tree("trunk", 0)); s1 = F.Sprite(ArtPaths.Tree("trunk", 1)); s2 = F.Sprite(ArtPaths.Tree("trunk", 2)); color = barkColor; order = 2; break;
                case TreeSectionType.Branch: s0 = F.Sprite(ArtPaths.Tree("branch", 0)); s1 = F.Sprite(ArtPaths.Tree("branch", 1)); s2 = F.Sprite(ArtPaths.Tree("branch", 2)); color = barkColor; order = 3; break;
                case TreeSectionType.BarkArmor: s0 = F.Sprite(ArtPaths.Tree("bark", 0)); s1 = F.Sprite(ArtPaths.Tree("bark", 1)); s2 = F.Sprite(ArtPaths.Tree("bark", 2)); color = Color.Lerp(barkColor, Color.white, 0.12f); order = 4; break;
                case TreeSectionType.CanopyShield: s0 = F.Sprite(ArtPaths.TreePart("canopy")); s1 = s0; s2 = s0; color = leafColor; order = 7; break;
                case TreeSectionType.RootStabilizer: s0 = F.Sprite(ArtPaths.TreePart("root")); s1 = s0; s2 = s0; color = barkColor * 0.85f; order = 1; break;
                default: s0 = F.Sprite(ArtPaths.TreePart("heartwood")); s1 = s0; s2 = s0; color = Color.white; order = 6; break;
            }
            if (spec.type == TreeSectionType.HeartwoodCore)
            {
                glow = F.WorldSprite("Glow", go.transform, F.Ui("glow"), new Color(1f, 0.8f, 0.3f, 0.35f), 5, Vector3.zero);
                glow.transform.localScale = new Vector3(spec.size.x * 1.8f / 1.28f, spec.size.y * 1.8f / 1.28f, 1f);
            }
            var visual = F.Child("Visual", go.transform);
            renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = s0;
            renderer.color = color;
            renderer.sortingOrder = order;
            if (s0 != null)
            {
                var b = s0.bounds.size;
                bool mirrorBranch = spec.type == TreeSectionType.Branch && spec.localPosition.x < 0f;
                visual.transform.localScale = new Vector3((mirrorBranch ? -1f : 1f) * spec.size.x / Mathf.Max(0.001f, b.x), spec.size.y / Mathf.Max(0.001f, b.y), 1f);
                if (spec.type == TreeSectionType.Branch)
                    visual.transform.localPosition = new Vector3(mirrorBranch ? spec.size.x * 0.5f : -spec.size.x * 0.5f, 0f, 0f);
                else if (spec.type == TreeSectionType.Trunk || spec.type == TreeSectionType.RootStabilizer)
                    visual.transform.localPosition = new Vector3(0f, -spec.size.y * 0.5f + (spec.type == TreeSectionType.RootStabilizer ? spec.size.y * 0.15f : 0f), 0f);
            }
            F.Set(section, "sectionId", spec.sectionId);
            F.Set(section, "type", (float)(int)spec.type);
            F.Set(section, "guardianSlotIndex", (float)spec.guardianSlotIndex);
            F.SetArray(section, "renderers", new Object[] { renderer });
            F.SetArray(section, "damageStateSprites", new Object[] { s0, s1, s2 });
            F.Set(section, "sectionCollider", col);
            F.Set(section, "damageVfxAnchor", go.transform);
            return section;
        }

        static GuardianController BuildGuardianView(string name, Transform slot)
        {
            var go = F.Child(name, slot.transform);
            var ctrl = go.AddComponent<GuardianController>();
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.85f, 1.05f);
            col.offset = new Vector2(0f, 0.55f);
            var platform = F.WorldSprite("Platform", go.transform, F.Sprite(ArtPaths.TreePart("platform")), new Color(0.5f, 0.38f, 0.26f), 7, new Vector3(0f, -0.04f, 0f));
            platform.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
            var visualRoot = F.Child("VisualRoot", go.transform);
            var sprite = F.WorldSprite("Sprite", visualRoot.transform, F.Sprite(ArtPaths.WorldSprite("thorn_archer")), Color.white, 8, Vector3.zero);
            sprite.transform.localScale = new Vector3(0.42f, 0.42f, 1f);
            var muzzle = F.Child("Muzzle", go.transform, new Vector3(0.4f, 0.62f, 0f));
            var ring = F.WorldSprite("SelectionRing", go.transform, F.Ui("ring"), F.Honey, 6, new Vector3(0f, 0.05f, 0f));
            ring.transform.localScale = new Vector3(0.7f, 0.35f, 1f);
            ring.enabled = false;
            var shield = F.WorldSprite("Shield", go.transform, F.Sprite(ArtPaths.Vfx("shield")), new Color(0.6f, 0.9f, 1f, 0.6f), 9, new Vector3(0f, 0.55f, 0f));
            shield.transform.localScale = new Vector3(0.95f, 0.95f, 1f);
            shield.enabled = false;
            var status = F.WorldSprite("StatusIcon", go.transform, F.Icon("bolt"), new Color(1f, 0.85f, 0.3f), 10, new Vector3(0f, 1.25f, 0f));
            status.transform.localScale = new Vector3(0.2f, 0.2f, 1f);
            status.enabled = false;
            var bars = F.Child("Bars", go.transform, new Vector3(0f, 1.12f, 0f));
            var hpBack = F.WorldSprite("HealthBack", bars.transform, F.Ui("white"), BarBack, 10, Vector3.zero);
            hpBack.transform.localScale = new Vector3(24f, 3.2f, 1f);
            var hpPivot = F.Child("HealthFillPivot", bars.transform, new Vector3(-0.45f, 0f, 0f));
            var hpFill = F.WorldSprite("HealthFill", hpPivot.transform, F.Ui("white"), F.Green, 11, new Vector3(0.45f, 0f, 0f));
            hpFill.transform.localScale = new Vector3(22f, 2f, 1f);
            var enBack = F.WorldSprite("EnergyBack", bars.transform, F.Ui("white"), BarBack, 10, new Vector3(0f, -0.09f, 0f));
            enBack.transform.localScale = new Vector3(24f, 2f, 1f);
            var enPivot = F.Child("EnergyFillPivot", bars.transform, new Vector3(-0.45f, -0.09f, 0f));
            var enFill = F.WorldSprite("EnergyFill", enPivot.transform, F.Ui("white"), F.Honey, 11, new Vector3(0.45f, 0f, 0f));
            enFill.transform.localScale = new Vector3(22f, 1.2f, 1f);

            F.Set(ctrl, "sprite", sprite); F.Set(ctrl, "visualRoot", visualRoot.transform); F.Set(ctrl, "muzzle", muzzle.transform);
            F.Set(ctrl, "healthBarFill", hpPivot.transform); F.Set(ctrl, "healthBarFillRenderer", hpFill); F.Set(ctrl, "energyBarFill", enPivot.transform);
            F.Set(ctrl, "selectionRing", ring); F.Set(ctrl, "shieldVisual", shield); F.Set(ctrl, "statusIcon", status); F.Set(ctrl, "bodyCollider", col); F.Set(ctrl, "platform", platform);
            SetGradient(ctrl, "healthGradient");
            go.SetActive(false);
            return ctrl;
        }

        static void SetGradient(Component c, string field)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(field);
            if (p == null) return;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(0.9f, 0.25f, 0.2f), 0f), new GradientColorKey(new Color(0.95f, 0.75f, 0.2f), 0.5f), new GradientColorKey(new Color(0.36f, 0.75f, 0.35f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            p.gradientValue = g;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ HUD
        static BattleHUD BuildHUD(Camera cam, out AimView aimView)
        {
            var canvas = F.Canvas("BattleCanvas");
            var hud = canvas.gameObject.AddComponent<BattleHUD>();
            var safe = F.SafeArea(canvas.transform);

            // Top HUD
            var top = F.Rect("TopHUD", safe);
            F.AnchorStretchX(top, -10f, 120f, 20f, 20f, 1f, 1f);
            var pName = F.OutlinedText("PlayerName", top, null, 28f, F.TextLight, TextAlignmentOptions.MidlineLeft, "You");
            F.Anchor((RectTransform)pName.transform, new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(400f, 36f), new Vector2(0f, 1f));
            var pBar = F.FillBar("PlayerTreeBar", top, F.PanelMid, F.Green, out var pFill);
            F.Anchor((RectTransform)pBar.transform, new Vector2(0f, 1f), new Vector2(0f, -40f), new Vector2(620f, 40f), new Vector2(0f, 1f));
            var eName = F.OutlinedText("EnemyName", top, null, 28f, F.TextLight, TextAlignmentOptions.MidlineRight, "Bot");
            F.Anchor((RectTransform)eName.transform, new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(400f, 36f), new Vector2(1f, 1f));
            var eBar = F.FillBar("EnemyTreeBar", top, F.PanelMid, F.Red, out var eFill);
            F.Anchor((RectTransform)eBar.transform, new Vector2(1f, 1f), new Vector2(0f, -40f), new Vector2(620f, 40f), new Vector2(1f, 1f));
            eFill.fillOrigin = 1;
            var timerPill = F.Image("TimerPill", top, F.Ui("pill"), F.PanelDark, true, false);
            F.Anchor((RectTransform)timerPill.transform, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(220f, 70f), new Vector2(0.5f, 1f));
            var timer = F.OutlinedText("Timer", timerPill.transform, null, 40f, F.TextLight, TextAlignmentOptions.Center, "2:30");
            F.Stretch((RectTransform)timer.transform);
            var arena = F.Text("ArenaName", top, null, 22f, new Color(0.9f, 0.92f, 0.95f), TextAlignmentOptions.Center, false, "");
            F.Anchor((RectTransform)arena.transform, new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(500f, 30f), new Vector2(0.5f, 1f));
            var offline = F.Image("OfflineBadge", top, F.Ui("pill"), new Color(0.25f, 0.3f, 0.4f), true, false);
            F.Anchor((RectTransform)offline.transform, new Vector2(0.5f, 1f), new Vector2(0f, -108f), new Vector2(230f, 30f), new Vector2(0.5f, 1f));
            var offlineText = F.OutlinedText("Text", offline.transform, "battle_offline", 18f, F.TextLight); F.Stretch((RectTransform)offlineText.transform);
            var pause = F.IconButton("PauseButton", safe, F.PanelDark, F.Icon("pause"), F.TextLight);
            F.Anchor((RectTransform)pause.transform, new Vector2(1f, 1f), new Vector2(-14f, -130f), new Vector2(84f, 84f), new Vector2(1f, 1f));

            // Aim layer
            var aimLayer = F.Rect("AimLayer", safe);
            F.Stretch(aimLayer);
            aimView = aimLayer.gameObject.AddComponent<AimView>();
            var dots = new Object[12];
            for (int i = 0; i < 12; i++)
            {
                var d = F.Image("AimDot_" + i, aimLayer, F.Sprite(ArtPaths.Vfx("aim_dot")), new Color(1f, 0.95f, 0.6f), false, false);
                ((RectTransform)d.transform).sizeDelta = new Vector2(26f, 26f);
                d.enabled = false;
                dots[i] = d;
            }
            var gauge = F.Image("PowerGauge", aimLayer, F.Ui("bar_back"), F.PanelDark, true, false);
            ((RectTransform)gauge.transform).sizeDelta = new Vector2(160f, 26f);
            var gaugeFill = F.Image("Fill", gauge.transform, F.Ui("bar_fill"), F.Honey, true, false);
            F.Stretch((RectTransform)gaugeFill.transform, 5f, 5f, 5f, 5f);
            gaugeFill.type = Image.Type.Filled; gaugeFill.fillMethod = Image.FillMethod.Horizontal;
            gauge.gameObject.SetActive(false);
            var reticle = F.Image("Reticle", aimLayer, F.Sprite(ArtPaths.Vfx("aim_reticle")), new Color(1f, 0.95f, 0.6f), false, false);
            ((RectTransform)reticle.transform).sizeDelta = new Vector2(64f, 64f);
            reticle.enabled = false;
            F.Set(aimView, "layer", aimLayer); F.SetArray(aimView, "dots", dots); F.Set(aimView, "powerGauge", gauge.transform); F.Set(aimView, "powerFill", gaugeFill); F.Set(aimView, "reticle", reticle);

            // Guardian action bar
            var bar = F.HorizontalGroup("GuardianActionBar", safe, 10f, TextAnchor.MiddleCenter);
            F.Anchor(bar, new Vector2(0.5f, 0f), new Vector2(-180f, 12f), new Vector2(1500f, 170f), new Vector2(0.5f, 0f));
            bar.gameObject.AddComponent<FitRowScale>();
            var gButtons = new Object[8];
            for (int i = 0; i < 8; i++) gButtons[i] = GuardianActionButton("GuardianButton_" + i, bar);
            var toolBar = F.HorizontalGroup("ToolActionBar", safe, 10f, TextAnchor.MiddleCenter);
            F.Anchor(toolBar, new Vector2(1f, 0f), new Vector2(-20f, 12f), new Vector2(400f, 150f), new Vector2(1f, 0f));
            var tButtons = new Object[3];
            for (int i = 0; i < 3; i++) tButtons[i] = ToolActionButton("ToolButton_" + i, toolBar);
            var hint = F.OutlinedText("HintText", safe, null, 26f, F.Honey, TextAlignmentOptions.Center, "");
            F.Anchor((RectTransform)hint.transform, new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(1200f, 40f), new Vector2(0.5f, 0f));

            // Battle message
            var msg = F.Rect("BattleMessage", safe);
            F.Anchor(msg, new Vector2(0.5f, 0.6f), Vector2.zero, new Vector2(1400f, 160f));
            var msgGroup = F.Group(msg.gameObject, 0f);
            var msgText = F.OutlinedText("Text", msg, null, 96f, F.Honey, TextAlignmentOptions.Center, "READY");
            F.Stretch((RectTransform)msgText.transform);

            // Pause popup
            var popup = F.Image("PausePopup", safe, F.Ui("panel"), F.PanelDark, true, true);
            F.Anchor((RectTransform)popup.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800f, 460f));
            F.Group(popup.gameObject, 1f);
            var pausePanel = popup.gameObject.AddComponent<UIPanel>();
            var pTitle = F.OutlinedText("Title", popup.transform, "battle_paused", 44f, F.Honey);
            F.AnchorStretchX((RectTransform)pTitle.transform, -20f, 60f, 40f, 40f, 1f, 1f);
            var resume = F.Button("ResumeButton", popup.transform, F.Green, "battle_resume", 30f);
            F.Anchor((RectTransform)resume.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(420f, 84f));
            var forfeit = F.Button("ForfeitButton", popup.transform, F.Red, "battle_forfeit", 30f);
            F.Anchor((RectTransform)forfeit.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -90f), new Vector2(420f, 84f));
            var pGroup = popup.GetComponent<CanvasGroup>(); pGroup.alpha = 0f; pGroup.blocksRaycasts = false; pGroup.interactable = false;
            popup.gameObject.SetActive(false);

            // Tutorial overlay: non-blocking dim + step text + spotlight ring + skip.
            var tut = F.Image("BattleTutorialOverlay", safe, F.Ui("white"), new Color(0f, 0f, 0f, 0.35f), false, false);
            F.Stretch((RectTransform)tut.transform, -100f, -100f, -100f, -100f);
            var tutGroup = F.Group(tut.gameObject, 1f);
            var tutPanel = F.Image("TextPanel", tut.transform, F.Ui("panel"), F.PanelDark, true, false);
            F.Anchor((RectTransform)tutPanel.transform, new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(1300f, 130f));
            var tutText = F.OutlinedText("Text", tutPanel.transform, null, 32f, F.TextLight, TextAlignmentOptions.Center, "");
            F.Stretch((RectTransform)tutText.transform, 24f, 10f, 24f, 10f);
            var spotlight = F.Image("Spotlight", tut.transform, F.Ui("glow"), new Color(1f, 0.85f, 0.3f, 0.7f), true, false);
            spotlight.type = Image.Type.Simple;
            ((RectTransform)spotlight.transform).sizeDelta = new Vector2(220f, 220f);
            spotlight.gameObject.SetActive(false);
            var tutSkip = F.Button("SkipButton", tut.transform, F.PanelMid, "tut_skip", 24f);
            F.Anchor((RectTransform)tutSkip.transform, new Vector2(1f, 1f), new Vector2(-130f, -240f), new Vector2(280f, 64f), new Vector2(1f, 1f));
            tut.gameObject.SetActive(false);
            var tutorial = F.Root("BattleTutorialController").AddComponent<TreeGuardians.Tutorial.BattleTutorialController>();
            F.Set(tutorial, "overlayRoot", tut.gameObject); F.Set(tutorial, "overlayGroup", tutGroup); F.Set(tutorial, "stepText", tutText);
            F.Set(tutorial, "skipButton", tutSkip); F.Set(tutorial, "spotlight", spotlight.rectTransform);
            F.Set(tutorial, "guardianBarAnchor", bar); F.Set(tutorial, "toolBarAnchor", toolBar);
            tutorialController = tutorial;

            var overlay = F.Image("TransitionOverlay", canvas.transform, F.Ui("white"), Color.black, false, true);
            F.Stretch((RectTransform)overlay.transform, -100f, -100f, -100f, -100f);
            F.Group(overlay.gameObject, 1f);
            var transition = overlay.gameObject.AddComponent<TransitionOverlay>();

            F.Set(hud, "playerNameText", pName); F.Set(hud, "enemyNameText", eName); F.Set(hud, "playerCoreFill", pFill); F.Set(hud, "enemyCoreFill", eFill);
            F.Set(hud, "timerText", timer); F.Set(hud, "arenaText", arena); F.Set(hud, "offlineBadge", offline.gameObject);
            F.SetArray(hud, "guardianButtons", gButtons); F.SetArray(hud, "toolButtons", tButtons); F.Set(hud, "hintText", hint);
            F.Set(hud, "messageGroup", msgGroup); F.Set(hud, "messageText", msgText);
            F.Set(hud, "pauseButton", pause); F.Set(hud, "pausePopup", pausePanel); F.Set(hud, "resumeButton", resume); F.Set(hud, "forfeitButton", forfeit); F.Set(hud, "transition", transition);
            return hud;
        }

        static GuardianActionButton GuardianActionButton(string name, Transform parent)
        {
            var frame = F.Image(name, parent, F.Ui("card"), F.PanelMid, true, true);
            ((RectTransform)frame.transform).sizeDelta = new Vector2(150f, 170f);
            var btn = frame.gameObject.AddComponent<Button>();
            btn.targetGraphic = frame;
            frame.gameObject.AddComponent<UIButtonFeedback>();
            var glow = F.Image("SpecialGlow", frame.transform, F.Ui("glow"), F.Honey, false, false);
            F.Stretch((RectTransform)glow.transform, -30f, -30f, -30f, -30f);
            glow.transform.SetAsFirstSibling();
            glow.enabled = false;
            var portrait = F.Image("Portrait", frame.transform, null, Color.white, false, false);
            F.Stretch((RectTransform)portrait.transform, 12f, 44f, 12f, 12f);
            var cd = F.Image("CooldownFill", frame.transform, F.Ui("soft"), new Color(0f, 0f, 0f, 0.6f), true, false);
            F.Stretch((RectTransform)cd.transform, 12f, 44f, 12f, 12f);
            cd.type = Image.Type.Filled; cd.fillMethod = Image.FillMethod.Vertical; cd.fillOrigin = 1; cd.fillAmount = 0f;
            var hpBack = F.Image("HealthBack", frame.transform, F.Ui("bar_back"), F.PanelDark, true, false);
            F.AnchorStretchX((RectTransform)hpBack.transform, 24f, 18f, 12f, 12f, 0f, 0f);
            var hp = F.Image("HealthFill", hpBack.transform, F.Ui("bar_fill"), F.Green, true, false);
            F.Stretch((RectTransform)hp.transform, 3f, 3f, 3f, 3f); hp.type = Image.Type.Filled; hp.fillMethod = Image.FillMethod.Horizontal;
            var enBack = F.Image("EnergyBack", frame.transform, F.Ui("bar_back"), F.PanelDark, true, false);
            F.AnchorStretchX((RectTransform)enBack.transform, 8f, 14f, 12f, 12f, 0f, 0f);
            var en = F.Image("EnergyFill", enBack.transform, F.Ui("bar_fill"), F.Honey, true, false);
            F.Stretch((RectTransform)en.transform, 3f, 3f, 3f, 3f); en.type = Image.Type.Filled; en.fillMethod = Image.FillMethod.Horizontal;
            var sel = F.Image("SelectedFrame", frame.transform, F.Ui("card"), F.Honey, true, false);
            F.Stretch((RectTransform)sel.transform, -8f, -8f, -8f, -8f);
            sel.transform.SetSiblingIndex(1);
            sel.enabled = false;
            var dead = F.Image("DeadOverlay", frame.transform, F.Ui("soft"), new Color(0f, 0f, 0f, 0.7f), true, false);
            F.Stretch((RectTransform)dead.transform, 8f, 8f, 8f, 8f);
            var deadIcon = F.Image("Icon", dead.transform, F.Icon("close"), F.Red, false, false);
            F.Anchor((RectTransform)deadIcon.transform, new Vector2(0.5f, 0.6f), Vector2.zero, new Vector2(60f, 60f));
            dead.SetActive(false);
            var stun = F.Image("StunIcon", frame.transform, F.Icon("motion"), F.Honey, false, false);
            F.Anchor((RectTransform)stun.transform, new Vector2(1f, 1f), new Vector2(-4f, -4f), new Vector2(40f, 40f), new Vector2(1f, 1f));
            stun.gameObject.SetActive(false);
            var view = frame.gameObject.AddComponent<GuardianActionButton>();
            F.Set(view, "button", btn); F.Set(view, "portrait", portrait); F.Set(view, "cooldownFill", cd); F.Set(view, "healthFill", hp); F.Set(view, "energyFill", en);
            F.Set(view, "selectedFrame", sel); F.Set(view, "specialGlow", glow); F.Set(view, "deadOverlay", dead.gameObject); F.Set(view, "stunIcon", stun.gameObject);
            return view;
        }

        static void SetActive(this Image img, bool v) => img.gameObject.SetActive(v);

        static ToolActionButton ToolActionButton(string name, Transform parent)
        {
            var frame = F.Image(name, parent, F.Ui("card"), F.PanelMid, true, true);
            ((RectTransform)frame.transform).sizeDelta = new Vector2(120f, 140f);
            var btn = frame.gameObject.AddComponent<Button>();
            btn.targetGraphic = frame;
            frame.gameObject.AddComponent<UIButtonFeedback>();
            var icon = F.Image("Icon", frame.transform, F.Sprite(ArtPaths.Tool("catapult")), Color.white, false, false);
            F.Stretch((RectTransform)icon.transform, 16f, 36f, 16f, 12f);
            var cd = F.Image("CooldownFill", frame.transform, F.Ui("soft"), new Color(0f, 0f, 0f, 0.6f), true, false);
            F.Stretch((RectTransform)cd.transform, 10f, 30f, 10f, 10f);
            cd.type = Image.Type.Filled; cd.fillMethod = Image.FillMethod.Radial360; cd.fillAmount = 0f;
            var label = F.OutlinedText("Label", frame.transform, null, 16f, F.TextLight, TextAlignmentOptions.Center, "");
            F.AnchorStretchX((RectTransform)label.transform, 6f, 26f, 4f, 4f, 0f, 0f);
            var sel = F.Image("SelectedFrame", frame.transform, F.Ui("card"), F.Honey, true, false);
            F.Stretch((RectTransform)sel.transform, -8f, -8f, -8f, -8f);
            sel.transform.SetAsFirstSibling();
            sel.enabled = false;
            var view = frame.gameObject.AddComponent<ToolActionButton>();
            F.Set(view, "button", btn); F.Set(view, "icon", icon); F.Set(view, "cooldownFill", cd); F.Set(view, "selectedFrame", sel); F.Set(view, "label", label);
            return view;
        }
    }
}
