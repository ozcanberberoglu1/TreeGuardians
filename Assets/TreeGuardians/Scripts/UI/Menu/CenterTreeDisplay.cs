using TreeGuardians.Core;
using TreeGuardians.Meta;
using UnityEngine;

namespace TreeGuardians.UI.Menu
{
    /// World-space tree in the main menu: one compartment per guardian slot, each with a GuardianSpawnPoint.
    /// While the Guardians panel is open the tree slides aside and its parts fade to a silhouette
    /// (Tree Guardians/2D/Sprite Silhouette) so only the equipped guardians stay visible inside the tree.
    public sealed class CenterTreeDisplay : MonoBehaviour
    {
        [Header("Parts (array order = guardian slot index)")]
        [SerializeField] TreePartSlot[] slots = new TreePartSlot[6];

        [Header("Silhouette (Guardians panel)")]
        [SerializeField] Color silhouetteColor = new Color(0.05f, 0.04f, 0.07f, 1f);
        [Tooltip("Seçili yuvanın silüet oranı (0 = tam görünür, 1 = tam siyah).")] [SerializeField, Range(0f, 1f)] float selectedSlotSilhouette = 0.45f;
        [SerializeField] float silhouetteFadeSeconds = 0.25f;
        [Tooltip("Panel açıkken ağacın kaydığı x (dünya birimi). Panel serbest alan bildirirse o kullanılır.")] [SerializeField] float loadoutOffsetX = -6.2f;
        [SerializeField] float loadoutMoveSeconds = 0.3f;

        [Header("Idle motion")]
        [SerializeField] Transform breathTarget;
        [SerializeField] float breathAmount = 0.012f;
        [SerializeField] float breathSpeed = 0.9f;

        static readonly int SilhouetteId = Shader.PropertyToID("_Silhouette");
        static readonly int SilhouetteColorId = Shader.PropertyToID("_SilhouetteColor");

        MaterialPropertyBlock block;
        Vector3 baseScale;
        Vector3 basePosition;
        float silhouette;
        int selectedSlot = -1;
        bool loadoutMode;
        bool warnedSlotMismatch;
        Coroutine silhouetteTween;
        Coroutine moveTween;

        public int SlotCount => slots.Length;
        public bool LoadoutMode => loadoutMode;
        public int SelectedSlot => selectedSlot;

        public GuardianSpawnPoint GetSpawnPoint(int slot) => slot >= 0 && slot < slots.Length && slots[slot] != null ? slots[slot].spawnPoint : null;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            if (breathTarget == null) breathTarget = transform;
            baseScale = breathTarget.localScale;
            basePosition = transform.localPosition;
            ApplySilhouette(0f);
        }

        void OnEnable()
        {
            BootAwaiter.WhenReady(Refresh);
        }

        public void Refresh()
        {
            var progress = Services.Get<PlayerProgressService>();
            if (progress == null || this == null) return;
            int slotCount = progress.Balance.guardianSlotCount;
            if (slotCount != slots.Length && !warnedSlotMismatch)
            {
                warnedSlotMismatch = true;
                Debug.LogWarning($"[CenterTreeDisplay] Tree has {slots.Length} compartments but GameBalanceConfig.guardianSlotCount is {slotCount}.", this);
            }
            for (int i = 0; i < slots.Length; i++)
            {
                var point = slots[i]?.spawnPoint;
                if (point == null) continue;
                point.Show(progress.Database.GetGuardian(progress.GetEquippedGuardianId(i)));
            }
        }

        /// Guardians panel opened/closed. worldCenterX: where the tree should sit while the panel is open (null = loadoutOffsetX).
        public void SetLoadoutMode(bool on, float? worldCenterX = null, bool instant = false)
        {
            loadoutMode = on;
            if (!on) selectedSlot = -1;
            bool reduce = QualityApplier.ReduceMotion;
            TGTween.Stop(silhouetteTween);
            silhouetteTween = TGTween.FloatTo(silhouette, on ? 1f : 0f, instant || reduce ? 0f : silhouetteFadeSeconds, ApplySilhouette);
            TGTween.Stop(moveTween);
            var target = basePosition;
            if (on) target.x = worldCenterX.HasValue ? worldCenterX.Value - (transform.parent != null ? transform.parent.position.x : 0f) : basePosition.x + loadoutOffsetX;
            moveTween = TGTween.MoveLocal(transform, target, instant || reduce ? 0f : loadoutMoveSeconds, Ease.OutCubic);
        }

        /// Highlights the compartment the player is about to fill (-1 = none).
        public void SetSelectedSlot(int slot)
        {
            selectedSlot = slot;
            ApplySilhouette(silhouette);
        }

        void ApplySilhouette(float value)
        {
            silhouette = value;
            if (block == null) block = new MaterialPropertyBlock();
            for (int i = 0; i < slots.Length; i++)
            {
                var sr = slots[i]?.part;
                if (sr == null) continue;
                float amount = i == selectedSlot ? value * selectedSlotSilhouette : value;
                sr.GetPropertyBlock(block);
                block.SetFloat(SilhouetteId, amount);
                block.SetColor(SilhouetteColorId, silhouetteColor);
                sr.SetPropertyBlock(block);
            }
        }

        void Update()
        {
            if (QualityApplier.ReduceMotion || breathTarget == null) return;
            float s = 1f + Mathf.Sin(Time.unscaledTime * breathSpeed) * breathAmount;
            breathTarget.localScale = new Vector3(baseScale.x * s, baseScale.y * s, baseScale.z);
        }
    }
}
