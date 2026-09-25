using System.Collections.Generic;
using TreeGuardians.Audio;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Guardians;
using TreeGuardians.Meta;
using UnityEngine;

namespace TreeGuardians.Battle
{
    /// Turns pointer input into battle commands. Turn-based castle duel: pick a guardian card (own castle turns into a
    /// silhouette so only your guardians are visible), then tap the enemy castle to fire at that point or drag to aim.
    public sealed class PlayerBattleCommander : MonoBehaviour, IBattleCommander
    {
        [SerializeField] Camera worldCamera;
        [SerializeField] AimView aimView;
        [SerializeField] float maxDragDistance = 3.2f;
        [SerializeField] float minPower = 0.2f;
        [SerializeField] float directDragRange = 9f;
        [Tooltip("Bu mesafeden az sürüklenen dokunuş 'noktaya ateş' sayılır (dünya birimi).")] [SerializeField] float tapDistance = 0.35f;
        [SerializeField] float tapSeconds = 0.45f;
        [Tooltip("Nişan alırken kendi muhafızlarının kale duvarı üstüne çıkarılma sorting offset'i.")] [SerializeField] int revealSortingOffset = 20;

        public BattleSide Side => BattleSide.Player;
        public int SelectedSlot { get; private set; } = -1;
        public bool SpecialArmed { get; private set; }
        public int PendingTool { get; private set; } = -1;
        public bool IsAiming { get; private set; }
        public bool IsRevealing { get; private set; }

        readonly Queue<BattleCommand> queue = new Queue<BattleCommand>(8);
        readonly List<Collider2D> overlap = new List<Collider2D>(8);
        BattleContext ctx;
        InputService input;
        Vector2 dragStart;
        float pressTime;
        int lastFiredSlot = -1;
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
            IsRevealing = false;
            lastFiredSlot = -1;
            queue.Clear();
            if (!ctx.turnBased) AutoSelectFirst();
        }

        bool CanAct => ctx != null && ctx.isPlaying && (ctx.turns == null || ctx.turns.IsPlayerActing);

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

        /// Player turn started: the guardian that fired last (or the first alive one) is pre-selected and ready; the player can switch.
        public void BeginTurn()
        {
            SelectedSlot = -1;
            SpecialArmed = false;
            PendingTool = -1;
            IsAiming = false;
            aimView?.Hide();
            ctx?.playerRoster?.SetPlayerControlled(-1);
            SetReveal(false);
            OnSelectionChanged?.Invoke();
            int pick = AliveAimable(lastFiredSlot) ? lastFiredSlot : FirstAliveAimable();
            if (pick >= 0) SelectSlot(pick);
        }

        bool AliveAimable(int slot)
        {
            var g = ctx?.playerRoster?.Get(slot);
            return g != null && g.IsActive && g.IsAlive && g.Definition.playerAimable;
        }

        int FirstAliveAimable()
        {
            var roster = ctx?.playerRoster;
            if (roster == null) return -1;
            for (int i = 0; i < roster.Slots.Count; i++) if (AliveAimable(i)) return i;
            return -1;
        }

        /// Shot fired or turn lost: hide the aim guide and restore the castle.
        public void EndTurn()
        {
            IsAiming = false;
            aimView?.Hide();
            SetReveal(false);
            if (ctx != null && ctx.turnBased)
            {
                SelectedSlot = -1;
                SpecialArmed = false;
                PendingTool = -1;
                ctx.playerRoster?.SetPlayerControlled(-1);
                OnSelectionChanged?.Invoke();
            }
        }

        void SetReveal(bool on)
        {
            if (ctx == null || !ctx.turnBased || IsRevealing == on) return;
            IsRevealing = on;
            ctx.playerTree?.SetSilhouette(on);
            ctx.playerRoster?.SetSortingOffset(on ? revealSortingOffset : 0);
        }

        public void SelectSlot(int slot)
        {
            var roster = ctx?.playerRoster;
            if (roster == null) return;
            if (ctx.turnBased && !CanAct) return;
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
                SetReveal(true);
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
            if (ctx.turnBased && !CanAct) return;
            if (tools.RequiresAim(index))
            {
                PendingTool = PendingTool == index ? -1 : index;
                IsAiming = false;
                aimView?.Hide();
            }
            else
            {
                Vector2 target = Vector2.zero;
                if (ctx.enemyTree != null) target = ctx.enemyTree.Core != null ? (Vector2)ctx.enemyTree.Core.transform.position : (Vector2)ctx.enemyTree.transform.position + Vector2.up * 3f;
                queue.Enqueue(BattleCommand.Tool(Side, index, target));
                PendingTool = -1;
            }
            OnSelectionChanged?.Invoke();
        }

        public void Tick(float dt)
        {
            if (input == null || worldCamera == null) return;
            if (!CanAct) { if (IsAiming) { IsAiming = false; aimView?.Hide(); } return; }
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
                    pressTime = Time.unscaledTime;
                }
            }

            if (IsAiming && input.PointerHeld)
            {
                if (Vector2.Distance(world, dragStart) >= tapDistance) UpdateAim(world, false);
            }

            if (IsAiming && (input.PointerUpThisFrame || !input.PointerHeld))
            {
                IsAiming = false;
                bool tap = Vector2.Distance(world, dragStart) < tapDistance && Time.unscaledTime - pressTime <= tapSeconds;
                if (tap) FireAtPoint(dragStart);
                else UpdateAim(world, true);
            }
        }

        static int IndexOf(GuardianRoster roster, GuardianController g)
        {
            for (int i = 0; i < roster.Slots.Count; i++) if (roster.Slots[i] == g) return i;
            return -1;
        }

        /// Fires the selected guardian at a world point (same path as a tap); used by tests and tutorials.
        public void FireAt(Vector2 worldTarget) => FireAtPoint(worldTarget);

        /// Tap on the enemy half: solve the launch so the projectile lands on the tapped point.
        void FireAtPoint(Vector2 target)
        {
            var g = ctx.playerRoster?.Get(SelectedSlot);
            aimView?.Hide();
            if (g == null || !g.IsAlive || ctx.projectiles == null) return;
            if (ctx.targeting != null && target.x <= ctx.targeting.MidlineX) return; // own half: not a shot
            bool special = SpecialArmed && g.SpecialReady;
            if (!g.CanFire(special)) return;
            var def = special && g.Definition.specialProjectile != null ? g.Definition.specialProjectile : g.Definition.normalProjectile;
            if (def == null) return;
            var velocity = ctx.projectiles.LaunchVelocity(def, g.MuzzlePosition, target, 1f);
            if (velocity.sqrMagnitude < 0.001f) return;
            queue.Enqueue(BattleCommand.Fire(Side, SelectedSlot, velocity.normalized, 1f, special));
            lastFiredSlot = SelectedSlot;
            if (special) SpecialArmed = false;
            OnSelectionChanged?.Invoke();
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
                lastFiredSlot = SelectedSlot;
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
            SetReveal(false);
            queue.Clear();
        }
    }
}
