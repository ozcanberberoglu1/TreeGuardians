using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

namespace TreeGuardians.Editor.Animals
{
    /// Validation report + side-by-side preview scene for all rigged animals.
    public static class AnimalRigInspector
    {
        public const string PreviewScenePath = "Assets/Art/_AnimalRigPreview.unity";
        static readonly string[] Animals = { "yabandomuzu", "ördek", "kartal", "kedi", "maymun" };

        [MenuItem("Tree Guardians/Animals/Report Rigs", priority = 120)]
        public static void Report()
        {
            var sb = new StringBuilder("[TG] Animal rig report\n");
            foreach (var name in Animals) sb.AppendLine(Describe(name));
            Debug.Log(sb.ToString());
        }

        public static string Describe(string name)
        {
            string prefabPath = AnimalRigBuilder.PrefabPath(name);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AnimalRigBuilder.TexturePath(name));
            if (prefab == null || sprite == null) return $"  {name}: MISSING (prefab={prefab != null}, sprite={sprite != null})";
            var skin = prefab.GetComponent<SpriteSkin>();
            var animator = prefab.GetComponent<Animator>();
            var bones = sprite.GetBones();
            var bindPoses = sprite.GetBindPoses();
            var verts = sprite.GetVertexCount();
            int weighted = 0, weightCount = 0;
            var perBone = new int[Mathf.Max(1, bones.Length)];
            if (sprite.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.BlendWeight))
            {
                var weights = sprite.GetVertexAttribute<BoneWeight>(UnityEngine.Rendering.VertexAttribute.BlendWeight);
                weightCount = weights.Length;
                for (int i = 0; i < weights.Length; i++)
                {
                    var w = weights[i];
                    if (w.weight0 > 0f) { weighted++; if (w.boneIndex0 < perBone.Length) perBone[w.boneIndex0]++; }
                }
            }
            string state = "?";
            try
            {
                var util = typeof(SpriteSkin).Assembly.GetType("UnityEngine.U2D.Animation.SpriteSkinUtility");
                var validate = util?.GetMethod("Validate", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (validate != null && skin != null) state = validate.Invoke(null, new object[] { skin }).ToString();
            }
            catch { }
            string chain = string.Join(",", bones.Select(b => b.name));
            string influence = string.Join(" ", bones.Select((b, i) => $"{b.name}:{perBone[i]}"));
            bool orderOk = skin != null && skin.boneTransforms != null && skin.boneTransforms.Length == bones.Length && skin.boneTransforms.Select((t, i) => t != null && t.name == bones[i].name).All(x => x);
            return $"  {name}: bones={bones.Length} bindPoses={bindPoses.Length} verts={verts} weighted={weighted}/{weightCount} skin={state} boneOrderMatchesSprite={orderOk} controller={(animator != null && animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "none")} scale={prefab.transform.localScale.x:0.00}\n    bones: {chain}\n    dominant-vertex counts: {influence}";
        }

        [MenuItem("Tree Guardians/Animals/Build Preview Scene", priority = 121)]
        public static void BuildPreviewScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 4.6f;
            cam.transform.position = new Vector3(0f, 1.2f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.75f, 0.9f);
            camGo.AddComponent<UniversalAdditionalCameraData>().renderType = CameraRenderType.Base;

            float x = -6.4f;
            foreach (var name in Animals)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AnimalRigBuilder.PrefabPath(name));
                if (prefab == null) continue;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                inst.transform.position = new Vector3(x, 0f, 0f);
                var label = new GameObject("Label_" + name);
                label.transform.position = new Vector3(x, -2.6f, 0f);
                var tm = label.AddComponent<TextMesh>();
                tm.text = name;
                tm.fontSize = 48;
                tm.characterSize = 0.08f;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.color = Color.black;
                x += 3.2f;
            }
            EditorSceneManager.SaveScene(scene, PreviewScenePath);
            Debug.Log("[TG] Preview scene saved: " + PreviewScenePath);
        }
    }
}
