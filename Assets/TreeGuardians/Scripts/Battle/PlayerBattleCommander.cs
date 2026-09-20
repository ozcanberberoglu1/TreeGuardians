using System.Collections.Generic;
using TreeGuardians.Audio;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Guardians;
using TreeGuardians.Meta;
using UnityEngine;

namespace TreeGuardians.Battle
{
    /// Turns pointer input into battle commands: select guardian, drag to aim, release to fire, tap to place tools.
    public sealed class PlayerBattleCommander : MonoBehaviour, IBattleCommander
    {
        [SerializeField] Camera worldCamera;
        [SerializeField] AimView aimView;
        [SerializeField] float maxDragDistance = 3.2f;
        [SerializeField] float minPower = 0.2f;
        [SerializeField] float directDragRange = 9f;

        public BattleSide Side => BattleSide.Player;
        public int SelectedSlot { get; private set; } = -1;
        public bool SpecialArmed { get; private set; }
        public int PendingTool { get; private set; } = -1;
        public bool IsAiming { get; private set; }

        readonly Queue<BattleCommand> queue = new Queue<BattleCommand>(8);
        readonly List<Collider2D> overlap = new List<Collider2D>(8);
        BattleContext ctx;
        InputService input;
        Vector2 dragStart;
        AimMode aimMode;
        ContactFilter2D filter;

        public event System.Action OnSelectionChanged;

        public void Initialize(BattleContext context)
        {
            ctx = context;
            input = Services.Get<InputService>();
            var progress = Services.Get<PlayerProgressService>();
            aimMode = progress != null ? (AimMode)progress.Data.settings.aimMode : AimMode.PullBack;
            filter = new ContactFilter2D { useTriggers = true, useLayerMask = false };
            if (aimView != null) aimView.Initialize(worldCamera);
            SelectedSlot = -1;
            SpecialArmed = false;
            PendingTool = -1;
            IsAiming = false;
            queue.Clear();
            AutoSelectFirst();
        }

        void AutoSelectFirst()
        {
            var roster = ctx.playerRoster;
            if (roster == null) return;
            for (int i = 0; i < roster.Slots.Count; i++)
            {
                var g = roster.Slots[i];
                if (g != null && g.IsActive && g.IsAlive && g.Definition.playerAimable) { SelectSlot(i); return; }
            }
        }

        public void SelectSlot(int slot)
        {
            var roster = ctx?.playerRoster;
            if (roster == null) return;
            var g = roster.Get(slot);
            if (g == null || !g.IsActive || !g.IsAlive) return;
            if (SelectedSlot == slot)
            {
                if (g.SpecialReady) { SpecialArmed = !SpecialArmed; Services.Get<AudioService>()?.PlayUi(AudioEventId.UiClick); }
            }
            else
            {
                SelectedSlot = slot;
                SpecialArmed = false;
                PendingTool = -1;
                roster.SetPlayerControlled(slot);
                queue.Enqueue(BattleCommand.Select(Side, slot));
                Services.Get<AudioService>()?.PlaySfx(AudioEventId.AimStart);
            }
            OnSelectionChanged?.Invoke();
        }

        public void ArmSpecial(bool armed)
        {
            SpecialArmed = armed;
            OnSelectionChanged?.Invoke();
        }

        public void SelectTool(int index)
        {
            var tools = ctx?.playerTools;
            if (tools == null || !tools.CanUse(index)) return;
            if (tools.RequiresAim(index))
            {
                PendingTool = PendingTool == index ? -1 : index;
                IsAiming = false;
                aimView?.Hide();
            }
            else
            {
                var target = ctx.enemyTree != null && ctx.enemyTree.Core != null ? (Vector2)ctx.enemyTree.Core.transform.position : Vector2.zero;
                queue.Enqueue(BattleCommand.Tool(Side, index, target));
                PendingTool = -1;
            }
            OnSelectionChanged?.Invoke();
        }

        public void Tick(float dt)
        {
            if (ctx == null || !ctx.isPlaying || input == null || worldCamera == null) return;
            Vector2 world = input.PointerWorldPosition(worldCamera);
            var roster = ctx.playerRoster;

            if (input.PointerDownThisFrame && !input.PointerOverUI)
            {
                if (PendingTool >= 0)
                {
                    queue.Enqueue(BattleCommand.Tool(Side, PendingTool, world));
                    PendingTool = -1;
                    OnSelectionChanged?.Invoke();
                    return;
                }
                overlap.Clear();
                Physics2D.OverlapPoint(world, filter, overlap);
                for (int i = 0; i < overlap.Count; i++)
                {
                    if (ctx.targeting != null && ctx.targeting.TryGetGuardian(overlap[i], out var g) && g.Side == Side && g.IsAlive && g.Definition.playerAimable)
                    {
                        int viewIndex = IndexOf(roster, g);
                        if (viewIndex >= 0 && viewIndex != SelectedSlot) SelectSlot(viewIndex);
                        break;
                    }
                }
                if (SelectedSlot >= 0)
                {
                    IsAiming = true;
                    dragStart = world;
                }
            }

            if (IsAiming && input.PointerHeld)
            {
                UpdateAim(world, false);
            }

            if (IsAiming && (input.PointerUpThisFrame || !input.PointerHeld))
            {
                IsAiming = false;
                UpdateAim(world, true);
            }
        }

        static int IndexOf(GuardianRoster roster, GuardianController g)
        {
            for (int i = 0; i < roster.Slots.Count; i++) if (roster.Slots[i] == g) return i;
            return -1;
        }

        void UpdateAim(Vector2 world, bool release)
        {
            var g = ctx.playerRoster?.Get(SelectedSlot);
            if (g == null || !g.IsAlive) { aimView?.Hide(); return; }
            Vector2 origin = g.MuzzlePosition;
            Vector2 dir;
            float power;
            if (aimMode == AimMode.PullBack)
            {
                Vector2 pull = dragStart - world;
                power = Mathf.Clamp01(pull.magnitude / maxDragDistance);
                dir = pull.sqrMagnitude > 0.0001f ? pull.normalized : Vector2.right;
            }
            else
            {
                Vector2 to = world - origin;
                power = Mathf.Clamp01(to.magnitude / directDragRange);
                dir = to.sqrMagnitude > 0.0001f ? to.normalized : Vector2.right;
            }
            bool special = SpecialArmed && g.SpecialReady;
            bool valid = power >= minPower && g.CanFire(special) && dir.x > -0.2f;
            var def = special && g.Definition.specialProjectile != null ? g.Definition.specialProjectile : g.Definition.normalProjectile;

            if (release)
            {
                aimView?.Hide();
                if (!valid) return;
                queue.Enqueue(BattleCommand.Fire(Side, SelectedSlot, dir, power, special));
                if (special) SpecialArmed = false;
                OnSelectionChanged?.Invoke();
                return;
            }
            if (def != null && aimView != null)
            {
                var velocity = dir * def.speed * Mathf.Clamp(power, 0.35f, 1f);
                aimView.Show(ctx.projectiles, def, origin, velocity, power, valid, special);
            }
        }

        public bool TryDequeue(out BattleCommand command)
        {
            if (queue.Count > 0) { command = queue.Dequeue(); return true; }
            command = default;
            return false;
        }

        public void OnBattleEnded()
        {
            IsAiming = false;
            aimView?.Hide();
            queue.Clear();
        }
    }
}
