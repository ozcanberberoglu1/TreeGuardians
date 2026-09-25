using System.Collections.Generic;
using System.Linq;
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
        const float GroundY = -2.0f;          // castle base line: castles stand on floating islands above the card bar
        const float ProjectileFloorY = -5.4f; // misses drop past the islands and out of view
        const float BackdropCenterY = 7.1f;   // bg.png meadow row lands just under the island tops
        const float TreeX = 7.6f;
        const float IslandScale = 0.6f;
        const float IslandTopOffset = 0.22f;  // grass surface sits this far under the sprite's top edge
        public const string VfxSpritePrefabPath = "Assets/TreeGuardians/Prefabs/VFX/Vfx_Sprite.prefab";
        public const string GuardianFlashMaterialPath = "Assets/TreeGuardians/Art/Materials/TG_GuardianFlash.mat";
        public const string AdditiveMaterialPath = "Assets/TreeGuardians/Art/Materials/TG_FX_Additive.mat";
        public const string ProjectilePrefabPath = "Assets/TreeGuardians/Prefabs/Projectiles/Projectile_Default.prefab";
        public const string VfxFolder = "Assets/TreeGuardians/Prefabs/VFX";

        static readonly Color BarBack = new Color(0.08f, 0.1f, 0.14f, 0.85f);
        public const string CastleFrontMaterialPath = "Assets/TreeGuardians/Art/Materials/TG_CastleWall.mat";
        public const string CastleBackMaterialPath = "Assets/TreeGuardians/Art/Materials/TG_CastleBack.mat";
        const int CastleBackOrder = 0;   // black interior, behind the guardians (6..11)
        const int CastleWallOrder = 12;  // masked wall in front of the guardians
        const int CastleBaseOrder = 13;
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
            BuildVfxPrefabs(out var spritePrefab, out var additivePrefab, out var dmgText);

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
            F.Set(projectiles, "groundY", ProjectileFloorY);
            F.Set(vfx, "vfxRoot", vfxRoot.transform);
            F.Set(vfx, "textRoot", textRoot.transform);
            F.Set(vfx, "spritePrefab", spritePrefab); F.Set(vfx, "additivePrefab", additivePrefab); F.Set(vfx, "damageTextPrefab", dmgText);
            F.Set(vfx, "debrisFloorY", GroundY + 0.05f);
            WireVfxSprites(vfx);
            F.Set(vfx, "holeSmoke", BuildHoleSmoke(vfx.transform));
            F.Set(vfx, "confetti", BuildConfetti(vfx.transform));

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

            // Painted backdrop (user art), 29.7 units square: meadow at the bottom, tree line mid-castle, mountains + clouds up top.
            var backdropSprite = AssetDatabase.LoadAssetAtPath<Sprite>(UserArtSlicer.BackdropPath);
            var backdrop = F.WorldSprite("Backdrop", env.transform, backdropSprite, Color.white, -95, new Vector3(0f, BackdropCenterY, 5f));
            if (backdropSprite != null) { float s = 29.7f / backdropSprite.bounds.size.x; backdrop.transform.localScale = new Vector3(s, s, 1f); }

            // Floating islands (user art) under both castles; the enemy one is mirrored for variety.
            var islandSprite = AssetDatabase.LoadAllAssetsAtPath(UserArtSlicer.PlatformsPath).OfType<Sprite>().FirstOrDefault(sp => sp.name == "platform_big");
            var islands = new Object[2];
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -TreeX : TreeX;
                var isl = F.WorldSprite(i == 0 ? "Island_Player" : "Island_Enemy", env.transform, islandSprite, Color.white, -8, new Vector3(x, GroundY + IslandTopOffset, 0.5f));
                isl.transform.localScale = new Vector3(i == 0 ? IslandScale : -IslandScale, IslandScale, 1f);
                islands[i] = isl;
            }
            var mist = F.WorldSprite("Mist", env.transform, F.Sprite(ArtPaths.Arena("mist")), new Color(1f, 1f, 1f, 0.35f), -55, new Vector3(0f, -1.2f, 1.5f));
            mist.transform.localScale = new Vector3(6f, 2.5f, 1f);
            mist.enabled = false;

            var particles = F.Child("AtmosphereParticles", env.transform);
            var fireflies = MakeParticles("Fireflies", particles.transform, F.Sprite(ArtPaths.Vfx("glow")), new Color(1f, 0.95f, 0.5f), 0.14f, 3f, new Vector2(-0.4f, 0.4f), new Vector2(-0.2f, 0.3f), 36, -45);

            float islandWidth = 12.29f * IslandScale * 0.9f;
            WeatherBuilder.Build(env.transform, Object.FindFirstObjectByType<Camera>(), GroundY, 9.4f, 30f, 43, true,
                new[] { new Vector2(-TreeX, islandWidth), new Vector2(TreeX, islandWidth) });

            F.Set(presenter, "sky", grad);
            F.SetArray(presenter, "clouds", new Object[0]);
            F.Set(presenter, "mountains", (Object)null); F.Set(presenter, "forestBack", (Object)null); F.Set(presenter, "forestMid", (Object)null);
            F.Set(presenter, "ground", (Object)null); F.Set(presenter, "foreground", (Object)null); F.Set(presenter, "mist", mist);
            F.Set(presenter, "leaves", (Object)null); F.Set(presenter, "fireflies", fireflies);
            F.Set(presenter, "backdrop", backdrop);
            F.SetArray(presenter, "contactShadows", new Object[0]);
            F.SetArray(presenter, "islands", islands);
            sky.SetActive(false); // the painted backdrop covers the whole frame
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
            go.transform.localScale = Vector3.one * 0.85f;
            var pc = go.AddComponent<ProjectileController>();
            F.Set(pc, "sprite", sr);
            var glow = F.WorldSprite("Glow", go.transform, F.Sprite(ArtPaths.Vfx("glow")), new Color(1f, 0.95f, 0.8f, 0.45f), 19, Vector3.zero);
            glow.transform.localScale = new Vector3(1.3f, 1.3f, 1f);
            var tr = go.AddComponent<TrailRenderer>();
            tr.time = 0.22f;
            tr.minVertexDistance = 0.05f;
            tr.numCapVertices = 4;
            tr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.22f), new Keyframe(1f, 0f));
            tr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            tr.sortingOrder = 19;
            tr.emitting = false;
            tr.alignment = LineAlignment.View;
            F.Set(pc, "trail", tr);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, ProjectilePrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static void BuildVfxPrefabs(out VfxSprite sprite, out VfxSprite additive, out DamageText dmgText)
        {
            ContentBuilder.EnsureFolder(VfxFolder);
            sprite = VfxPrefab("Vfx_Sprite", VfxArtGenerator.PathOf("soft_puff"), 24, null);
            var addMat = LoadOrCreateMaterial(AdditiveMaterialPath, "Tree Guardians/2D/Sprite Additive");
            additive = VfxPrefab("Vfx_Additive", VfxArtGenerator.PathOf("flash"), 27, addMat);

            var go = new GameObject("DamageText");
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = F.Font;
            tmp.fontSize = 6f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Normal;
            var numberMat = DamageNumberMaterial(F.Font);
            if (numberMat != null) tmp.fontSharedMaterial = numberMat; // shared preset: no per-object material instance
            tmp.enableWordWrapping = false;
            tmp.text = "12";
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(4f, 1.2f);
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 60;
            var dt = go.AddComponent<DamageText>();
            F.Set(dt, "text", tmp);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, VfxFolder + "/DamageText.prefab");
            Object.DestroyImmediate(go);
            dmgText = prefab.GetComponent<DamageText>();
        }

        public const string DamageNumberMaterialPath = "Assets/TreeGuardians/Art/Materials/TG_DamageNumber.mat";

        /// Outline + soft drop shadow preset for world-space damage numbers, built from the font's own atlas material.
        static Material DamageNumberMaterial(TMP_FontAsset font)
        {
            if (font == null || font.material == null) return null;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(DamageNumberMaterialPath);
            if (mat == null)
            {
                ContentBuilder.EnsureFolder("Assets/TreeGuardians/Art/Materials");
                mat = new Material(font.material) { name = "TG_DamageNumber" };
                AssetDatabase.CreateAsset(mat, DamageNumberMaterialPath);
            }
            else if (mat.shader != font.material.shader) mat.shader = font.material.shader;
            mat.SetTexture(ShaderUtilities.ID_MainTex, font.material.GetTexture(ShaderUtilities.ID_MainTex));
            mat.SetFloat(ShaderUtilities.ID_GradientScale, font.material.GetFloat(ShaderUtilities.ID_GradientScale));
            mat.SetFloat(ShaderUtilities.ID_TextureWidth, font.material.GetFloat(ShaderUtilities.ID_TextureWidth));
            mat.SetFloat(ShaderUtilities.ID_TextureHeight, font.material.GetFloat(ShaderUtilities.ID_TextureHeight));
            mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.24f);
            mat.SetColor(ShaderUtilities.ID_OutlineColor, new Color32(34, 20, 14, 255));
            mat.SetFloat(ShaderUtilities.ID_FaceDilate, 0.1f);
            mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.45f));
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.4f);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
            mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.25f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static VfxSprite VfxPrefab(string name, string spritePath, int order, Material material)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = F.Sprite(spritePath);
            sr.sortingOrder = order;
            if (material != null) sr.sharedMaterial = material;
            var v = go.AddComponent<VfxSprite>();
            F.Set(v, "sprite", sr);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{VfxFolder}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<VfxSprite>();
        }

        static Sprite Fx(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(VfxArtGenerator.PathOf(name));

        static void WireVfxSprites(BattleVFX vfx)
        {
            F.SetArray(vfx, "splinters", new Object[] { Fx("splinter_0"), Fx("splinter_1"), Fx("splinter_2"), Fx("splinter_3") });
            F.SetArray(vfx, "chips", new Object[] { Fx("chip_0"), Fx("chip_1"), Fx("chip_2") });
            F.SetArray(vfx, "leafBits", WeatherBuilder.LeafSprites());
            F.Set(vfx, "puff", Fx("smoke_puff"));
            F.Set(vfx, "dust", Fx("dust"));
            F.Set(vfx, "spark", Fx("spark"));
            F.Set(vfx, "ring", Fx("ring"));
            F.Set(vfx, "flash", Fx("flash"));
            F.Set(vfx, "star", F.Sprite(ArtPaths.Vfx("burst")));
            F.Set(vfx, "glow", F.Sprite(ArtPaths.Vfx("glow")));
            F.Set(vfx, "heal", F.Sprite(ArtPaths.Vfx("heal")));
        }

        /// Smoke that keeps curling out of fresh holes (emitted by BattleVFX with EmitParams; rate stays 0).
        static ParticleSystem BuildHoleSmoke(Transform parent)
        {
            var go = F.Child("HoleSmoke", parent);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false; main.loop = true; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 2f; main.startSpeed = 0f; main.startSize = 0.45f;
            main.maxParticles = 160;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var em = ps.emission; em.rateOverTime = 0f;
            var shape = ps.shape; shape.enabled = false;
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.35f, 1.1f), new Keyframe(1f, 2.1f)));
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            var vel = ps.limitVelocityOverLifetime; vel.enabled = true; vel.dampen = 0.08f; vel.limit = 0.9f;
            var force = ps.forceOverLifetime; force.enabled = true; force.space = ParticleSystemSimulationSpace.World; force.x = new ParticleSystem.MinMaxCurve(-0.25f, -0.05f); force.y = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.9f, 0.9f, 0.92f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.12f), new GradientAlphaKey(0.5f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            var puff = Fx("smoke_puff");
            r.sharedMaterial = WeatherBuilder.ParticleMaterial("HoleSmoke", puff != null ? puff.texture : null);
            r.sortingOrder = 22;
            r.maxParticleSize = 0.5f;
            return ps;
        }

        /// Victory confetti: two bursts from above the arena, fluttering down.
        static ParticleSystem BuildConfetti(Transform parent)
        {
            var go = F.Child("VictoryConfetti", parent, new Vector3(0f, 8.8f, 0f));
            go.transform.position = new Vector3(0f, 8.8f, -1f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false; main.loop = false; main.duration = 2.5f; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.2f, 4.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 5f);
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.14f, 0.24f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.08f, 0.14f);
            main.startSizeZ = 1f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.35f;
            main.maxParticles = 260;
            var cg = new Gradient();
            cg.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.82f, 0.2f), 0f), new GradientColorKey(new Color(0.36f, 0.8f, 0.36f), 0.25f), new GradientColorKey(new Color(0.3f, 0.62f, 1f), 0.5f), new GradientColorKey(new Color(0.95f, 0.36f, 0.4f), 0.75f), new GradientColorKey(new Color(0.75f, 0.5f, 1f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(cg) { mode = ParticleSystemGradientMode.RandomColor };
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 120), new ParticleSystem.Burst(0.6f, 80) });
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(22f, 0.5f, 1f);
            shape.rotation = new Vector3(90f, 0f, 0f);
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.8f; noise.frequency = 0.6f; noise.quality = ParticleSystemNoiseQuality.Low;
            var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.limit = 2.4f; lim.dampen = 0.2f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            col.color = fade;
            var r = go.GetComponent<ParticleSystemRenderer>();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(VfxArtGenerator.PathOf("confetti"));
            r.sharedMaterial = WeatherBuilder.ParticleMaterial("Confetti", tex);
            r.sortingOrder = 70;
            return ps;
        }

        // ------------------------------------------------------------------ trees
        static Material LoadOrCreateMaterial(string path, string shaderName, System.Action<Material> setup = null)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) { Debug.LogError("[TG] Shader missing: " + shaderName); return null; }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                ContentBuilder.EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
                mat = new Material(shader);
                setup?.Invoke(mat);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader) { mat.shader = shader; setup?.Invoke(mat); EditorUtility.SetDirty(mat); }
            return mat;
        }

        /// Castle duel tree: base + wall parts (front = masked destructible sprite, back = black silhouette), guardian slots inside.
        static TreeController BuildCastle(string name, Transform parent, TreeDefinition def, BattleSide side, Vector3 position, bool mirrored, out GuardianRoster roster, out ToolController tools)
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
            var tso = new SerializedObject(tree);
            tso.FindProperty("destructible").boolValue = true;
            tso.FindProperty("tintByTier").boolValue = false;
            tso.ApplyModifiedPropertiesWithoutUndo();

            var frontMat = LoadOrCreateMaterial(CastleFrontMaterialPath, "Tree Guardians/2D/Sprite Destructible");
            var backMat = LoadOrCreateMaterial(CastleBackMaterialPath, "Tree Guardians/2D/Sprite Silhouette", m => { m.SetFloat("_Silhouette", 1f); m.SetColor("_SilhouetteColor", new Color(0.05f, 0.04f, 0.07f, 1f)); });

            var partsRoot = F.Child("CastleParts", root.transform);
            var backRoot = F.Child("CastleBack", root.transform);
            var sections = new List<Object>();
            foreach (var spec in def.sections)
            {
                bool isBase = spec.type == TreeSectionType.RootStabilizer;
                var go = F.Child(spec.sectionId, partsRoot.transform, new Vector3(spec.localPosition.x, spec.localPosition.y, 0f));
                var section = go.AddComponent<TreeSection>();
                var col = go.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                col.size = spec.size;
                var visual = F.WorldSprite("Visual", go.transform, spec.sprite, Color.white, isBase ? CastleBaseOrder : CastleWallOrder, Vector3.zero);
                if (frontMat != null) visual.sharedMaterial = frontMat;
                if (spec.sprite != null)
                {
                    var b = spec.sprite.bounds.size;
                    visual.transform.localScale = new Vector3(spec.size.x / Mathf.Max(0.001f, b.x), spec.size.y / Mathf.Max(0.001f, b.y), 1f);
                }
                if (!isBase)
                {
                    var back = F.WorldSprite(spec.sectionId + "_Back", backRoot.transform, spec.sprite, Color.white, CastleBackOrder, new Vector3(spec.localPosition.x, spec.localPosition.y, 0f));
                    back.transform.localScale = visual.transform.localScale;
                    if (backMat != null) back.sharedMaterial = backMat;
                }
                F.Set(section, "sectionId", spec.sectionId);
                F.Set(section, "type", (float)(int)spec.type);
                F.Set(section, "guardianSlotIndex", (float)spec.guardianSlotIndex);
                F.SetArray(section, "renderers", new Object[] { visual });
                F.SetArray(section, "damageStateSprites", new Object[0]);
                F.Set(section, "sectionCollider", col);
                var sso = new SerializedObject(section);
                sso.FindProperty("keepVisualsOnDestroy").boolValue = true;
                sso.ApplyModifiedPropertiesWithoutUndo();
                sections.Add(section);
            }

            var slotsRoot = F.Child("GuardianSlots_01_08", root.transform);
            var slots = new Object[8];
            var guardians = new Object[8];
            for (int i = 0; i < 8; i++)
            {
                Vector2 pos = def.guardianSlotPositions[i];
                var slot = F.Child($"Slot_{i + 1:00}", slotsRoot.transform, new Vector3(pos.x, pos.y, 0f));
                slots[i] = slot.transform;
                guardians[i] = BuildGuardianView($"Guardian_{i + 1:00}", slot.transform);
            }
            var mountsRoot = F.Child("ToolMounts_01_03", root.transform);
            var mounts = new Object[3];
            for (int i = 0; i < 3; i++)
            {
                Vector2 pos = def.toolMountPositions[i];
                var m = F.Child($"Mount_{i + 1:00}", mountsRoot.transform, new Vector3(pos.x, pos.y, 0f));
                mounts[i] = m.transform;
            }

            // Castle integrity lives in the top HUD plates; no world-space bar over the castle.
            F.SetArray(tree, "sections", sections.ToArray());
            F.SetArray(tree, "guardianSlots", slots);
            F.SetArray(tree, "toolMounts", mounts);
            F.SetArray(roster, "slots", guardians);
            F.SetArray(tools, "mounts", mounts);
            return tree;
        }

        static TreeController BuildTree(string name, Transform parent, TreeDefinition def, BattleSide side, Vector3 position, bool mirrored, out GuardianRoster roster, out ToolController tools)
        {
            if (def != null && def.destructibleCastle) return BuildCastle(name, parent, def, side, position, mirrored, out roster, out tools);
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
            var platform = F.WorldSprite("Shadow", go.transform, Fx("soft_puff"), new Color(0f, 0f, 0f, 0.38f), 7, new Vector3(0f, 0.02f, 0f));
            platform.transform.localScale = new Vector3(0.62f, 0.16f, 1f);
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
            F.SetArray(ctrl, "barRenderers", new Object[] { hpBack, hpFill, enBack, enFill });
            F.Set(ctrl, "selectionRing", ring); F.Set(ctrl, "shieldVisual", shield); F.Set(ctrl, "statusIcon", status); F.Set(ctrl, "bodyCollider", col); F.Set(ctrl, "platform", platform);
            F.Set(ctrl, "flashMaterial", LoadOrCreateMaterial(GuardianFlashMaterialPath, "Tree Guardians/2D/Sprite Silhouette", m => { m.SetFloat("_Silhouette", 0f); m.SetColor("_SilhouetteColor", Color.white); }));
            F.Set(ctrl, "showPlatform", true);
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
        static readonly Color InkBrown = new Color(0.36f, 0.2f, 0.06f);
        static readonly Color BarTrack = new Color(0.05f, 0.06f, 0.1f, 0.9f);

        /// Menu-style player/enemy plate: kit plate 51 with avatar slot, name, sliced integrity bar and percent.
        static RectTransform CastlePlate(string name, Transform parent, bool enemy, string literalName, Color barColor, int avatarKit,
            out TMP_Text nameText, out UIFill bar, out TMP_Text percent)
        {
            float dir = enemy ? -1f : 1f;
            var anchor = new Vector2(enemy ? 1f : 0f, 1f);
            var root = F.Rect(name, parent);
            F.Anchor(root, anchor, new Vector2(20f * dir, -14f), new Vector2(600f, 130f), anchor);
            var plate = F.Image("Plate", root, F.Kit(51), Color.white, true, false);
            F.Stretch((RectTransform)plate.transform);
            if (enemy) plate.transform.localScale = new Vector3(-1f, 1f, 1f);
            var avatar = F.Image("Avatar", root, F.Kit(avatarKit), Color.white, false, false);
            F.Anchor((RectTransform)avatar.transform, new Vector2(enemy ? 1f : 0f, 0.5f), new Vector2(64f * dir, 2f), new Vector2(86f, 86f));
            var align = enemy ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
            nameText = F.OutlinedText("Name", root, null, 34f, F.TextLight, align, literalName);
            F.Anchor((RectTransform)nameText.transform, anchor, new Vector2(150f * dir, -12f), new Vector2(340f, 44f), anchor);
            bar = F.SlicedBar("IntegrityBar", root, BarTrack, barColor, 4f, true, enemy);
            F.Anchor((RectTransform)bar.transform, anchor, new Vector2(150f * dir, -66f), new Vector2(340f, 32f), anchor);
            percent = F.OutlinedText("Percent", root, null, 30f, F.TextLight, TextAlignmentOptions.Center, "100%");
            F.Anchor((RectTransform)percent.transform, anchor, new Vector2(498f * dir, -62f), new Vector2(92f, 40f), anchor);
            return root;
        }

        static BattleHUD BuildHUD(Camera cam, out AimView aimView)
        {
            var canvas = F.Canvas("BattleCanvas");
            var hud = canvas.gameObject.AddComponent<BattleHUD>();
            var safe = F.SafeArea(canvas.transform);

            // Top HUD: two plates, the timer pill and one arena chip — the same kit art as the main menu.
            var top = F.Rect("TopHUD", safe);
            F.Stretch(top);
            CastlePlate("PlayerPlate", top, false, "You", new Color(0.47f, 0.82f, 0.29f), 39, out var pName, out var pBar, out var pPct);
            CastlePlate("EnemyPlate", top, true, "Bot", new Color(0.94f, 0.36f, 0.3f), 16, out var eName, out var eBar, out var ePct);
            var timerPill = F.Image("TimerPill", top, F.Kit(45), Color.white, true, false);
            F.Anchor((RectTransform)timerPill.transform, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(236f, 90f), new Vector2(0.5f, 1f));
            var timer = F.OutlinedText("Timer", timerPill.transform, null, 48f, F.TextLight, TextAlignmentOptions.Center, "4:59");
            F.Stretch((RectTransform)timer.transform, 10f, 8f, 10f, 4f);
            var arenaChip = F.Image("ArenaChip", top, F.Kit(45), new Color(1f, 1f, 1f, 0.92f), true, false);
            F.Anchor((RectTransform)arenaChip.transform, new Vector2(0.5f, 1f), new Vector2(0f, -106f), new Vector2(470f, 48f), new Vector2(0.5f, 1f));
            var arena = F.Text("ArenaName", arenaChip.transform, null, 22f, new Color(0.86f, 0.9f, 0.97f), TextAlignmentOptions.Center, false, "");
            F.Stretch((RectTransform)arena.transform, 18f, 4f, 18f, 4f);
            arena.enableAutoSizing = true; arena.fontSizeMin = 14f; arena.fontSizeMax = 22f;
            var pause = F.KitButton("PauseButton", top, 41, null);
            F.Anchor((RectTransform)pause.transform, new Vector2(1f, 1f), new Vector2(-636f, -22f), new Vector2(100f, 100f), new Vector2(1f, 1f));
            var pauseIcon = F.Image("Icon", pause.transform, F.Icon("pause"), F.TextLight, false, false);
            F.Stretch((RectTransform)pauseIcon.transform, 26f, 26f, 26f, 26f);

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
            var gauge = F.Image("PowerGauge", aimLayer, F.Ui("bar_back"), BarTrack, true, false);
            ((RectTransform)gauge.transform).sizeDelta = new Vector2(160f, 26f);
            var gaugeFill = F.Image("Fill", gauge.transform, F.Ui("bar_fill"), F.Honey, true, false);
            F.Stretch((RectTransform)gaugeFill.transform, 5f, 5f, 5f, 5f);
            gaugeFill.type = Image.Type.Filled; gaugeFill.fillMethod = Image.FillMethod.Horizontal;
            gauge.gameObject.SetActive(false);
            var reticle = F.Image("Reticle", aimLayer, F.Sprite(ArtPaths.Vfx("aim_reticle")), new Color(1f, 0.95f, 0.6f), false, false);
            ((RectTransform)reticle.transform).sizeDelta = new Vector2(64f, 64f);
            reticle.enabled = false;
            F.Set(aimView, "layer", aimLayer); F.SetArray(aimView, "dots", dots); F.Set(aimView, "powerGauge", gauge.transform); F.Set(aimView, "powerFill", gaugeFill); F.Set(aimView, "reticle", reticle);

            // Combat controls (fade out together when the battle ends)
            var combat = F.Rect("CombatControls", safe);
            F.Stretch(combat);
            var combatGroup = F.Group(combat.gameObject, 1f);
            var bar = F.HorizontalGroup("GuardianActionBar", combat, 12f, TextAnchor.LowerCenter);
            F.Anchor(bar, new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(1300f, 214f), new Vector2(0.5f, 0f));
            bar.gameObject.AddComponent<FitRowScale>();
            var gButtons = new Object[8];
            for (int i = 0; i < 8; i++) gButtons[i] = GuardianActionButton("GuardianButton_" + i, bar);
            var toolBar = F.HorizontalGroup("ToolActionBar", combat, 10f, TextAnchor.LowerRight);
            F.Anchor(toolBar, new Vector2(1f, 0f), new Vector2(-24f, 12f), new Vector2(480f, 160f), new Vector2(1f, 0f));
            var tButtons = new Object[3];
            for (int i = 0; i < 3; i++) tButtons[i] = ToolActionButton("ToolButton_" + i, toolBar);

            // Turn pill: label + count badge + sliced timer bar (castle duel)
            var turnPill = F.Image("TurnPill", combat, F.Kit(45), Color.white, true, false);
            F.Anchor((RectTransform)turnPill.transform, new Vector2(0.5f, 0f), new Vector2(0f, 222f), new Vector2(520f, 80f), new Vector2(0.5f, 0f));
            var turnText = F.OutlinedText("Text", turnPill.transform, null, 34f, F.TextLight, TextAlignmentOptions.Center, "");
            F.Stretch((RectTransform)turnText.transform, 28f, 16f, 96f, 6f);
            var badge = F.Image("CountBadge", turnPill.transform, F.Ui("circle"), new Color(0.36f, 0.75f, 0.35f), false, false);
            F.Anchor((RectTransform)badge.transform, new Vector2(1f, 0.5f), new Vector2(-50f, 4f), new Vector2(72f, 72f));
            var countText = F.OutlinedText("Count", badge.transform, null, 40f, F.TextLight, TextAlignmentOptions.Center, "10");
            F.Stretch((RectTransform)countText.transform);
            var turnBar = F.Image("TimerBack", turnPill.transform, F.Ui("bar_back"), BarTrack, true, false);
            F.Anchor((RectTransform)turnBar.transform, new Vector2(0.5f, 0f), new Vector2(-36f, 10f), new Vector2(380f, 10f), new Vector2(0.5f, 0f));
            var turnFill = F.Image("Fill", turnBar.transform, F.Ui("white"), F.Green, false, false);
            F.Stretch((RectTransform)turnFill.transform, 2f, 2f, 2f, 2f);
            turnFill.type = Image.Type.Filled; turnFill.fillMethod = Image.FillMethod.Horizontal;

            // Hint chip above the pill (hidden when empty)
            var hintChip = F.Image("HintChip", combat, F.Kit(45), new Color(1f, 1f, 1f, 0.9f), true, false);
            F.Anchor((RectTransform)hintChip.transform, new Vector2(0.5f, 0f), new Vector2(0f, 310f), new Vector2(720f, 54f), new Vector2(0.5f, 0f));
            var hintGroup = F.Group(hintChip.gameObject, 0f);
            var hint = F.Text("HintText", hintChip.transform, null, 24f, new Color(1f, 0.9f, 0.6f), TextAlignmentOptions.Center, false, "");
            F.Stretch((RectTransform)hint.transform, 26f, 4f, 26f, 4f);
            hint.enableAutoSizing = true; hint.fontSizeMin = 16f; hint.fontSizeMax = 24f;

            // Turn banner ribbon (slides in on every turn change)
            var banner = F.Rect("TurnBanner", safe);
            F.Anchor(banner, new Vector2(0.5f, 0.66f), Vector2.zero, new Vector2(860f, 128f));
            var bannerGroup = F.Group(banner.gameObject, 0f);
            var ribbon = F.Image("Ribbon", banner, F.Kit(46), Color.white, true, false);
            F.Stretch((RectTransform)ribbon.transform);
            var bannerText = F.OutlinedText("Text", banner, null, 60f, InkBrown, TextAlignmentOptions.Center, "");
            bannerText.outlineColor = new Color(1f, 0.97f, 0.85f);
            bannerText.outlineWidth = 0.12f;
            F.Stretch((RectTransform)bannerText.transform, 40f, 18f, 40f, 10f);
            banner.gameObject.SetActive(false);

            // Short messages (time's up, core exposed, can't fire): dark chip + text
            var msg = F.Rect("BattleMessage", safe);
            F.Anchor(msg, new Vector2(0.5f, 0.52f), Vector2.zero, new Vector2(900f, 110f));
            var msgGroup = F.Group(msg.gameObject, 0f);
            var msgBack = F.Image("Back", msg, F.Kit(45), new Color(1f, 1f, 1f, 0.94f), true, false);
            F.Stretch((RectTransform)msgBack.transform);
            var msgText = F.OutlinedText("Text", msg, null, 50f, F.Honey, TextAlignmentOptions.Center, "READY");
            F.Stretch((RectTransform)msgText.transform, 30f, 10f, 30f, 10f);
            msgText.enableAutoSizing = true; msgText.fontSizeMin = 26f; msgText.fontSizeMax = 50f;

            // End banner (victory / defeat / draw) with a starburst behind
            var end = F.Rect("EndBanner", safe);
            F.Anchor(end, new Vector2(0.5f, 0.6f), Vector2.zero, new Vector2(1080f, 170f));
            var endGroup = F.Group(end.gameObject, 0f);
            var burst = F.Image("Starburst", end, F.Kit(36), new Color(1f, 0.92f, 0.55f, 0.45f), false, false);
            F.Anchor((RectTransform)burst.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 500f));
            var endRibbon = F.Image("Ribbon", end, F.Kit(46), Color.white, true, false);
            F.Stretch((RectTransform)endRibbon.transform);
            var endText = F.OutlinedText("Text", end, null, 92f, InkBrown, TextAlignmentOptions.Center, "");
            endText.outlineColor = new Color(1f, 0.97f, 0.85f);
            endText.outlineWidth = 0.14f;
            F.Stretch((RectTransform)endText.transform, 40f, 20f, 40f, 10f);
            end.gameObject.SetActive(false);

            // Pause popup
            var popup = F.Image("PausePopup", safe, F.Kit(41), Color.white, true, true);
            F.Anchor((RectTransform)popup.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(780f, 470f));
            F.Group(popup.gameObject, 1f);
            var pausePanel = popup.gameObject.AddComponent<UIPanel>();
            var pTitle = F.OutlinedText("Title", popup.transform, "battle_paused", 52f, F.Honey);
            F.AnchorStretchX((RectTransform)pTitle.transform, -30f, 70f, 40f, 40f, 1f, 1f);
            var resume = F.KitButton("ResumeButton", popup.transform, 44, "battle_resume", 36f);
            F.Anchor((RectTransform)resume.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(430f, 116f));
            var forfeit = F.KitButton("ForfeitButton", popup.transform, 45, "battle_forfeit", 30f);
            F.Anchor((RectTransform)forfeit.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -118f), new Vector2(360f, 90f));
            var forfeitLabel = forfeit.GetComponentInChildren<TMP_Text>();
            if (forfeitLabel != null) forfeitLabel.color = new Color(1f, 0.62f, 0.56f);
            var pGroup = popup.GetComponent<CanvasGroup>(); pGroup.alpha = 0f; pGroup.blocksRaycasts = false; pGroup.interactable = false;
            popup.gameObject.SetActive(false);

            // Tutorial overlay: non-blocking dim + step text + spotlight ring + skip.
            var tut = F.Image("BattleTutorialOverlay", safe, F.Ui("white"), new Color(0f, 0f, 0f, 0.35f), false, false);
            F.Stretch((RectTransform)tut.transform, -100f, -100f, -100f, -100f);
            var tutGroup = F.Group(tut.gameObject, 1f);
            var tutPanel = F.Image("TextPanel", tut.transform, F.Kit(41), Color.white, true, false);
            F.Anchor((RectTransform)tutPanel.transform, new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(1300f, 140f));
            var tutText = F.OutlinedText("Text", tutPanel.transform, null, 34f, F.TextLight, TextAlignmentOptions.Center, "");
            F.Stretch((RectTransform)tutText.transform, 30f, 12f, 30f, 12f);
            tutText.enableAutoSizing = true; tutText.fontSizeMin = 22f; tutText.fontSizeMax = 34f;
            var spotlight = F.Image("Spotlight", tut.transform, F.Ui("glow"), new Color(1f, 0.85f, 0.3f, 0.7f), true, false);
            spotlight.type = Image.Type.Simple;
            ((RectTransform)spotlight.transform).sizeDelta = new Vector2(220f, 220f);
            spotlight.gameObject.SetActive(false);
            var tutSkip = F.KitButton("SkipButton", tut.transform, 45, "tut_skip", 26f);
            F.Anchor((RectTransform)tutSkip.transform, new Vector2(1f, 1f), new Vector2(-130f, -240f), new Vector2(280f, 76f), new Vector2(1f, 1f));
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

            F.Set(hud, "playerNameText", pName); F.Set(hud, "enemyNameText", eName);
            F.Set(hud, "playerCoreBar", pBar); F.Set(hud, "enemyCoreBar", eBar);
            F.Set(hud, "playerCorePercent", pPct); F.Set(hud, "enemyCorePercent", ePct);
            F.Set(hud, "timerText", timer); F.Set(hud, "timerPlate", timerPill); F.Set(hud, "arenaText", arena); F.Set(hud, "offlineBadge", (Object)null);
            F.SetArray(hud, "guardianButtons", gButtons); F.SetArray(hud, "toolButtons", tButtons);
            F.Set(hud, "combatGroup", combatGroup); F.Set(hud, "hintText", hint); F.Set(hud, "hintGroup", hintGroup);
            F.Set(hud, "turnGroup", turnPill.gameObject); F.Set(hud, "turnText", turnText); F.Set(hud, "turnCountText", countText);
            F.Set(hud, "turnPill", badge); F.Set(hud, "turnTimerFill", turnFill);
            F.Set(hud, "turnBanner", bannerGroup); F.Set(hud, "turnBannerText", bannerText); F.Set(hud, "turnBannerRibbon", ribbon);
            F.Set(hud, "messageGroup", msgGroup); F.Set(hud, "messageText", msgText);
            F.Set(hud, "endBanner", endGroup); F.Set(hud, "endBannerText", endText); F.Set(hud, "endBannerRibbon", endRibbon);
            F.Set(hud, "pauseButton", pause); F.Set(hud, "pausePopup", pausePanel); F.Set(hud, "resumeButton", resume); F.Set(hud, "forfeitButton", forfeit); F.Set(hud, "transition", transition);
            return hud;
        }

        /// Guardian card: the card art fills the button; selection lifts it with a honey halo; sliced HP/energy bars.
        static GuardianActionButton GuardianActionButton(string name, Transform parent)
        {
            var root = F.Image(name, parent, F.Ui("white"), new Color(1f, 1f, 1f, 0f), false, true);
            ((RectTransform)root.transform).sizeDelta = new Vector2(152f, 188f);
            var le = root.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 152f; le.preferredHeight = 188f;
            var btn = root.gameObject.AddComponent<Button>();
            btn.targetGraphic = root;
            btn.transition = Selectable.Transition.None;
            root.gameObject.AddComponent<UIButtonFeedback>();
            var group = F.Group(root.gameObject, 1f);
            var lift = F.Rect("Lift", root.transform);
            F.Stretch(lift);
            var glow = F.Image("SpecialGlow", lift, F.Ui("glow"), new Color(1f, 0.85f, 0.3f), false, false);
            F.Stretch((RectTransform)glow.transform, -34f, -34f, -34f, -34f);
            glow.preserveAspect = false;
            glow.enabled = false;
            var sel = F.Image("SelectedFrame", lift, F.Ui("glow"), new Color(1f, 0.86f, 0.35f, 0.95f), false, false);
            F.Stretch((RectTransform)sel.transform, -22f, -22f, -22f, -22f);
            sel.preserveAspect = false;
            sel.enabled = false;
            var shadow = F.Image("Shadow", lift, F.Ui("shadow"), new Color(0f, 0f, 0f, 0.45f), true, false);
            F.Stretch((RectTransform)shadow.transform, -6f, -10f, -6f, 2f);
            var portrait = F.Image("Portrait", lift, null, Color.white, false, false);
            F.Stretch((RectTransform)portrait.transform, -14f, -10f, -14f, -10f);
            var cd = F.Image("CooldownFill", lift, F.Ui("soft"), new Color(0f, 0f, 0f, 0.55f), true, false);
            F.Stretch((RectTransform)cd.transform, 10f, 40f, 10f, 10f);
            cd.type = Image.Type.Filled; cd.fillMethod = Image.FillMethod.Vertical; cd.fillOrigin = 1; cd.fillAmount = 0f;
            cd.enabled = false;
            var hp = F.SlicedBar("HealthBar", lift, BarTrack, new Color(0.47f, 0.82f, 0.29f), 3f, true);
            F.AnchorStretchX((RectTransform)hp.transform, 30f, 16f, 18f, 18f, 0f, 0f);
            var en = F.SlicedBar("EnergyBar", lift, BarTrack, new Color(0.36f, 0.72f, 1f), 2f, false);
            F.AnchorStretchX((RectTransform)en.transform, 14f, 11f, 24f, 24f, 0f, 0f);
            var dead = F.Image("DeadOverlay", lift, F.Ui("soft"), new Color(0.05f, 0.05f, 0.08f, 0.72f), true, false);
            F.Stretch((RectTransform)dead.transform, 6f, 6f, 6f, 6f);
            var deadIcon = F.Image("Icon", dead.transform, F.Icon("close"), F.Red, false, false);
            F.Anchor((RectTransform)deadIcon.transform, new Vector2(0.5f, 0.58f), Vector2.zero, new Vector2(64f, 64f));
            dead.SetActive(false);
            var stun = F.Image("StunIcon", lift, F.Icon("motion"), F.Honey, false, false);
            F.Anchor((RectTransform)stun.transform, new Vector2(1f, 1f), new Vector2(-2f, -2f), new Vector2(44f, 44f), new Vector2(1f, 1f));
            stun.gameObject.SetActive(false);
            var view = root.gameObject.AddComponent<GuardianActionButton>();
            F.Set(view, "button", btn); F.Set(view, "group", group); F.Set(view, "lift", lift); F.Set(view, "portrait", portrait); F.Set(view, "cooldownFill", cd);
            F.Set(view, "healthBar", hp); F.Set(view, "energyBar", en);
            F.Set(view, "selectedFrame", sel); F.Set(view, "specialGlow", glow); F.Set(view, "deadOverlay", dead.gameObject); F.Set(view, "stunIcon", stun.gameObject);
            return view;
        }

        static void SetActive(this Image img, bool v) => img.gameObject.SetActive(v);

        /// Tool button on the dark kit square: icon, radial cooldown with seconds, two-line label.
        static ToolActionButton ToolActionButton(string name, Transform parent)
        {
            var frame = F.Image(name, parent, F.Kit(41), Color.white, true, true);
            ((RectTransform)frame.transform).sizeDelta = new Vector2(150f, 156f);
            var le = frame.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 150f; le.preferredHeight = 156f;
            var btn = frame.gameObject.AddComponent<Button>();
            btn.targetGraphic = frame;
            var colors = btn.colors;
            colors.normalColor = Color.white; colors.highlightedColor = Color.white; colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f); colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            btn.colors = colors;
            frame.gameObject.AddComponent<UIButtonFeedback>();
            var group = F.Group(frame.gameObject, 1f);
            var sel = F.Image("SelectedFrame", frame.transform, F.Ui("glow"), new Color(1f, 0.86f, 0.35f, 0.95f), false, false);
            F.Stretch((RectTransform)sel.transform, -24f, -24f, -24f, -24f);
            sel.preserveAspect = false;
            sel.transform.SetAsFirstSibling();
            sel.enabled = false;
            var icon = F.Image("Icon", frame.transform, F.Sprite(ArtPaths.Tool("catapult")), Color.white, false, false);
            F.Anchor((RectTransform)icon.transform, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(88f, 88f), new Vector2(0.5f, 1f));
            var cd = F.Image("CooldownFill", frame.transform, F.Ui("circle"), new Color(0f, 0f, 0f, 0.55f), false, false);
            F.Anchor((RectTransform)cd.transform, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(96f, 96f), new Vector2(0.5f, 1f));
            cd.type = Image.Type.Filled; cd.fillMethod = Image.FillMethod.Radial360; cd.fillOrigin = 2; cd.fillClockwise = false; cd.fillAmount = 0f;
            var cdText = F.OutlinedText("CooldownText", cd.transform, null, 38f, F.TextLight, TextAlignmentOptions.Center, "");
            F.Stretch((RectTransform)cdText.transform);
            cdText.gameObject.SetActive(false);
            var label = F.Text("Label", frame.transform, null, 17f, F.TextLight, TextAlignmentOptions.Center, false, "");
            F.AnchorStretchX((RectTransform)label.transform, 8f, 46f, 8f, 8f, 0f, 0f);
            label.enableAutoSizing = true; label.fontSizeMin = 12f; label.fontSizeMax = 17f;
            label.lineSpacing = -18f;
            label.overflowMode = TextOverflowModes.Truncate;
            var view = frame.gameObject.AddComponent<ToolActionButton>();
            F.Set(view, "button", btn); F.Set(view, "group", group); F.Set(view, "icon", icon); F.Set(view, "cooldownFill", cd); F.Set(view, "cooldownText", cdText);
            F.Set(view, "selectedFrame", sel); F.Set(view, "label", label);
            return view;
        }
    }
}
