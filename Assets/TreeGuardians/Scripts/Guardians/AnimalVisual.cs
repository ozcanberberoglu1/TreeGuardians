using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

namespace TreeGuardians.Guardians
{
    /// Helpers for the rigged animal prefabs (Assets/Art/<animal>/<animal>.prefab): feet alignment and the Attack trigger.
    public static class AnimalVisual
    {
        public const string AttackTrigger = "Attack";
        static readonly int AttackHash = Animator.StringToHash(AttackTrigger);

        /// Lowest bone tip (the feet) of the assembled pose, in the instance root's local space (before the root's own scale).
        public static float FeetLocalY(GameObject instance)
        {
            if (instance == null) return 0f;
            var skin = instance.GetComponentInChildren<SpriteSkin>(true);
            var sr = skin != null ? skin.GetComponent<SpriteRenderer>() : instance.GetComponentInChildren<SpriteRenderer>(true);
            if (sr == null || sr.sprite == null) return 0f;
            var root = instance.transform;
            var bones = sr.sprite.GetBones();
            var transforms = skin != null ? skin.boneTransforms : null;
            if (bones == null || transforms == null || transforms.Length == 0) return sr.sprite.bounds.min.y;
            float ppu = Mathf.Max(1f, sr.sprite.pixelsPerUnit);
            float minY = float.MaxValue;
            for (int i = 0; i < transforms.Length && i < bones.Length; i++)
            {
                var t = transforms[i];
                if (t == null) continue;
                var origin = root.InverseTransformPoint(t.position);
                var tip = root.InverseTransformPoint(t.TransformPoint(new Vector3(bones[i].length / ppu, 0f, 0f)));
                minY = Mathf.Min(minY, Mathf.Min(origin.y, tip.y));
            }
            return minY == float.MaxValue ? 0f : minY;
        }

        /// Places the instance so its feet sit on the parent's origin, at prefabScale * scale.
        public static void FitToAnchor(GameObject instance, Vector3 prefabScale, float scale, bool faceLeft)
        {
            if (instance == null) return;
            var t = instance.transform;
            t.localRotation = Quaternion.identity;
            t.localScale = new Vector3(prefabScale.x * (faceLeft ? -scale : scale), prefabScale.y * scale, prefabScale.z);
            float feet = FeetLocalY(instance);
            t.localPosition = new Vector3(0f, -feet * prefabScale.y * scale, 0f);
        }

        public static void PlayAttack(Animator animator)
        {
            if (animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null) animator.SetTrigger(AttackHash);
        }
    }
}
