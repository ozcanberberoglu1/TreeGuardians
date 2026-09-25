using TreeGuardians.Core;
using TreeGuardians.Data;
using UnityEngine;

namespace TreeGuardians.UI.Menu
{
    /// Anchor inside a tree compartment where the guardian equipped in that slot stands (feet on this transform).
    /// Sprite guardians use the pre-authored "Visual" SpriteRenderer child; worldPrefab guardians (animated rigs)
    /// are instantiated once under this point and cached until the slot changes.
    public sealed class GuardianSpawnPoint : MonoBehaviour
    {
        [SerializeField] SpriteRenderer spriteVisual;
        [Tooltip("Görsel ölçeği; GuardianDefinition.menuVisualScale ile çarpılır.")] [SerializeField] float visualScale = 0.5f;
        [Tooltip("Sola baksın (sağ sütundaki bölmeler için).")] [SerializeField] bool faceLeft;
        [Tooltip("Muhafız görselinin sorting order'ı; ağaç parçalarının üstünde kalmalı.")] [SerializeField] int sortingOrder = 6;
        [Tooltip("Boşta yukarı-aşağı salınım (dünya birimi).")] [SerializeField] float bobAmount = 0.03f;
        [SerializeField] float bobSpeed = 1.6f;

        GuardianDefinition current;
        GameObject prefabInstance;
        GuardianDefinition prefabDefinition;
        Vector3 prefabBaseScale = Vector3.one;
        Vector3 spriteBasePosition;
        float bobPhase;

        public GuardianDefinition Current => current;
        public bool HasGuardian => current != null;
        public SpriteRenderer SpriteVisual => spriteVisual;

        void Awake()
        {
            bobPhase = Random.value * Mathf.PI * 2f;
            if (spriteVisual != null) spriteVisual.enabled = false;
        }

        public void Show(GuardianDefinition def)
        {
            current = def;
            if (def == null) { Clear(); return; }
            float scale = visualScale * Mathf.Max(0.01f, def.menuVisualScale);
            if (def.worldPrefab != null)
            {
                if (spriteVisual != null) spriteVisual.enabled = false;
                if (prefabInstance == null || prefabDefinition != def)
                {
                    if (prefabInstance != null) Destroy(prefabInstance);
                    prefabInstance = Instantiate(def.worldPrefab, transform);
                    prefabInstance.name = "PrefabVisual";
                    prefabDefinition = def;
                    prefabBaseScale = def.worldPrefab.transform.localScale;
                    foreach (var r in prefabInstance.GetComponentsInChildren<Renderer>(true)) r.sortingOrder += sortingOrder;
                }
                prefabInstance.SetActive(true);
                TreeGuardians.Guardians.AnimalVisual.FitToAnchor(prefabInstance, prefabBaseScale, scale, faceLeft);
                return;
            }
            if (prefabInstance != null) prefabInstance.SetActive(false);
            if (spriteVisual == null) return;
            spriteVisual.sprite = def.worldSprite;
            spriteVisual.color = def.tintColor;
            spriteVisual.sortingOrder = sortingOrder;
            spriteVisual.flipX = faceLeft;
            spriteVisual.enabled = def.worldSprite != null;
            var t = spriteVisual.transform;
            t.localScale = new Vector3(scale, scale, 1f);
            float lift = def.worldSprite != null ? -def.worldSprite.bounds.min.y * scale : 0f;
            spriteBasePosition = new Vector3(0f, lift, 0f);
            t.localPosition = spriteBasePosition;
        }

        public void Clear()
        {
            current = null;
            if (spriteVisual != null) spriteVisual.enabled = false;
            if (prefabInstance != null) prefabInstance.SetActive(false);
        }

        void Update()
        {
            if (spriteVisual == null || !spriteVisual.enabled || QualityApplier.ReduceMotion) return;
            var p = spriteBasePosition;
            p.y += Mathf.Sin(Time.unscaledTime * bobSpeed + bobPhase) * bobAmount;
            spriteVisual.transform.localPosition = p;
        }
    }
}
