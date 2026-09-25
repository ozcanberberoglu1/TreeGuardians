using System.Linq;
using TreeGuardians.Data;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TreeGuardians.Editor.Animals
{
    /// Maps every guardian definition to one of the rigged animals: world prefab, card art (Assets/Art/Guardian-UIcards) and display name.
    /// Ids, stats, rarities and upgrades stay as they are; only the visuals and names change.
    public static class AnimalGuardianSetup
    {
        public const string CardsFolder = "Assets/Art/Guardian-UIcards";

        public struct Entry { public string guardianId, animal, en, tr; public Entry(string g, string a, string e, string t) { guardianId = g; animal = a; en = e; tr = t; } }

        // Common: tavuk, horoz | Rare: kaplumbaga, ördek, maymun, penguen | Epic: papagan, baykus, ayi, kedi | Legendary: kartal, yabandomuzu
        public static readonly Entry[] Map =
        {
            new Entry("thorn_archer", "tavuk", "Chicken", "Tavuk"),
            new Entry("cone_bomber", "horoz", "Rooster", "Horoz"),
            new Entry("bark_knight", "kaplumbaga", "Turtle", "Kaplumbağa"),
            new Entry("dew_fairy", "ördek", "Duck", "Ördek"),
            new Entry("vine_master", "maymun", "Monkey", "Maymun"),
            new Entry("beaver_engineer", "penguen", "Penguin", "Penguen"),
            new Entry("spore_alchemist", "papagan", "Parrot", "Papağan"),
            new Entry("owl_scout", "baykus", "Owl", "Baykuş"),
            new Entry("oak_warden", "ayi", "Bear", "Ayı"),
            new Entry("firefly_mage", "kedi", "Cat", "Kedi"),
            new Entry("hedgehog_sniper", "kartal", "Eagle", "Kartal"),
            new Entry("ancient_sprout", "yabandomuzu", "Wild Boar", "Yaban Domuzu"),
        };

        public static bool TryGet(string guardianId, out Entry entry)
        {
            foreach (var e in Map) if (e.guardianId == guardianId) { entry = e; return true; }
            entry = default;
            return false;
        }

        public static Sprite Card(string animal) => AssetDatabase.LoadAssetAtPath<Sprite>($"{CardsFolder}/{animal}.png");
        public static GameObject Prefab(string animal) => AssetDatabase.LoadAssetAtPath<GameObject>(AnimalRigBuilder.PrefabPath(animal));

        /// Prefab + card art + scales on one definition. Returns false when the guardian has no animal or assets are missing.
        public static bool ApplyVisuals(GuardianDefinition g)
        {
            if (g == null || !TryGet(g.id, out var e)) return false;
            var prefab = Prefab(e.animal);
            var card = Card(e.animal);
            if (prefab == null) { Debug.LogWarning($"[TG] Animal prefab missing for '{g.id}' -> {e.animal}"); return false; }
            g.worldPrefab = prefab;
            if (card != null) { g.portrait = card; g.cardArt = card; }
            else Debug.LogWarning($"[TG] Card art missing for '{g.id}' -> {CardsFolder}/{e.animal}.png");
            g.tintColor = Color.white;
            g.menuVisualScale = 1f;
            g.battleVisualScale = 0.55f;
            EditorUtility.SetDirty(g);
            return true;
        }

        [MenuItem("Tree Guardians/Animals/Apply Animal Guardians (visuals + names)", priority = 130)]
        public static void ApplyAll()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(ContentBuilder.DatabasePath);
            if (db == null) { Debug.LogError("[TG] GameDatabase missing."); return; }
            int n = 0;
            foreach (var g in db.guardians) if (ApplyVisuals(g)) n++;
            db.localization = ContentBuilder.BuildLocalization();
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Debug.Log($"[TG] Animal guardians applied to {n}/{db.guardians.Count} definitions; strings rebuilt.");
        }

        /// Animator: an Attack trigger that plays the Attack clip once from Idle and returns.
        [MenuItem("Tree Guardians/Animals/Setup Animator (Attack trigger)", priority = 131)]
        public static void SetupAnimator()
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimalRigBuilder.ControllerPath);
            if (ctrl == null) { Debug.LogError("[TG] Animator controller missing: " + AnimalRigBuilder.ControllerPath); return; }
            if (!ctrl.parameters.Any(p => p.name == "Attack")) ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            var sm = ctrl.layers[0].stateMachine;
            var idle = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Idle");
            var attack = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Attack");
            if (idle == null || attack == null) { Debug.LogError("[TG] Controller needs 'Idle' and 'Attack' states."); return; }
            sm.defaultState = idle;
            if (!idle.transitions.Any(t => t.destinationState == attack))
            {
                var t = idle.AddTransition(attack);
                t.hasExitTime = false; t.duration = 0.05f; t.hasFixedDuration = true;
                t.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            }
            if (!attack.transitions.Any(t => t.destinationState == idle))
            {
                var t = attack.AddTransition(idle);
                t.hasExitTime = true; t.exitTime = 0.95f; t.duration = 0.1f; t.hasFixedDuration = true;
            }
            var clip = attack.motion as AnimationClip;
            if (clip != null)
            {
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                if (settings.loopTime) { settings.loopTime = false; AnimationUtility.SetAnimationClipSettings(clip, settings); EditorUtility.SetDirty(clip); }
            }
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            Debug.Log("[TG] Animator ready: Idle <-> Attack (trigger 'Attack', Attack plays once).");
        }
    }
}
