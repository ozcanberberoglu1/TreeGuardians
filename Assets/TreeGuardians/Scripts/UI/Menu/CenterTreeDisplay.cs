using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Meta;
using UnityEngine;

namespace TreeGuardians.UI.Menu
{
    /// World-space tree in the main menu: tier-tinted parts, equipped guardians idling on platforms, gentle breathing.
    public sealed class CenterTreeDisplay : MonoBehaviour
    {
        [SerializeField] SpriteRenderer trunk;
        [SerializeField] SpriteRenderer canopy;
        [SerializeField] SpriteRenderer root;
        [SerializeField] SpriteRenderer[] branches = new SpriteRenderer[8];
        [SerializeField] SpriteRenderer[] guardianSprites = new SpriteRenderer[8];
        [SerializeField] Transform breathTarget;
        [SerializeField] float breathAmount = 0.012f;
        [SerializeField] float breathSpeed = 0.9f;
        [SerializeField] float branchSwayDegrees = 1.2f;
        [SerializeField] float bobAmount = 0.04f;

        Vector3 baseScale;
        Vector3[] guardianBase;
        float[] branchBase;

        void Awake()
        {
            if (breathTarget == null) breathTarget = transform;
            baseScale = breathTarget.localScale;
            guardianBase = new Vector3[guardianSprites.Length];
            for (int i = 0; i < guardianSprites.Length; i++) if (guardianSprites[i] != null) guardianBase[i] = guardianSprites[i].transform.localPosition;
            branchBase = new float[branches.Length];
            for (int i = 0; i < branches.Length; i++) if (branches[i] != null) branchBase[i] = branches[i].transform.localEulerAngles.z;
        }

        void OnEnable()
        {
            BootAwaiter.WhenReady(Refresh);
        }

        public void Refresh()
        {
            var progress = Services.Get<PlayerProgressService>();
            if (progress == null || this == null) return;
            var tree = progress.Database.playerTree;
            var visuals = tree != null ? tree.GetVisuals(progress.GetTreeVisualTier()) : null;
            if (visuals != null)
            {
                if (trunk != null) { trunk.color = visuals.barkColor; if (visuals.trunk != null) trunk.sprite = visuals.trunk; }
                if (root != null) { root.color = visuals.barkColor * 0.85f; if (visuals.root != null) root.sprite = visuals.root; }
                if (canopy != null) { canopy.color = visuals.leafColor; if (visuals.canopy != null) canopy.sprite = visuals.canopy; }
                foreach (var b in branches) if (b != null) { b.color = visuals.barkColor; if (visuals.branch != null) b.sprite = visuals.branch; }
            }
            for (int i = 0; i < guardianSprites.Length; i++)
            {
                var sr = guardianSprites[i];
                if (sr == null) continue;
                var def = progress.Database.GetGuardian(progress.GetEquippedGuardianId(i));
                sr.enabled = def != null && def.worldSprite != null;
                if (def != null) { sr.sprite = def.worldSprite; sr.color = def.tintColor; }
            }
        }

        void Update()
        {
            if (QualityApplier.ReduceMotion) return;
            float t = Time.unscaledTime;
            if (breathTarget != null)
            {
                float s = 1f + Mathf.Sin(t * breathSpeed) * breathAmount;
                breathTarget.localScale = new Vector3(baseScale.x * s, baseScale.y * s, baseScale.z);
            }
            for (int i = 0; i < branches.Length; i++)
            {
                var b = branches[i];
                if (b == null) continue;
                b.transform.localRotation = Quaternion.Euler(0f, 0f, branchBase[i] + Mathf.Sin(t * 0.7f + i * 0.9f) * branchSwayDegrees);
            }
            for (int i = 0; i < guardianSprites.Length; i++)
            {
                var g = guardianSprites[i];
                if (g == null || !g.enabled) continue;
                var p = guardianBase[i];
                p.y += Mathf.Sin(t * 1.6f + i * 1.3f) * bobAmount;
                g.transform.localPosition = p;
            }
        }
    }
}
