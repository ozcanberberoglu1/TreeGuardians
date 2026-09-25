using System;
using TreeGuardians.Core;
using TreeGuardians.Data;

namespace TreeGuardians.Battle
{
    public enum TurnPhase { None = 0, PlayerAct = 1, PlayerResolve = 2, EnemyThink = 3, EnemyResolve = 4 }

    /// Turn order for the castle duel: the player picks a guardian and fires once (with a countdown), the shot resolves,
    /// then the bot fires once, and so on. BattleManager owns it and drives Tick from the Playing state.
    public sealed class BattleTurnController
    {
        readonly BattleContext ctx;
        readonly GameBalanceConfig balance;

        public TurnPhase Phase { get; private set; } = TurnPhase.None;
        public float PhaseTime { get; private set; }
        public float TimeLeft { get; private set; }
        public int Round { get; private set; }
        public int PlayerShots { get; private set; }
        public int EnemyShots { get; private set; }

        public bool IsPlayerActing => Phase == TurnPhase.PlayerAct;
        public bool IsEnemyActing => Phase == TurnPhase.EnemyThink;
        public BattleSide CurrentSide => Phase == TurnPhase.PlayerAct || Phase == TurnPhase.PlayerResolve ? BattleSide.Player : BattleSide.Enemy;

        public event Action<TurnPhase> OnPhaseChanged;
        public event Action OnPlayerTimedOut;

        public BattleTurnController(BattleContext context)
        {
            ctx = context;
            balance = context.balance;
        }

        public void Start(BattleSide first)
        {
            Round = 1;
            SetPhase(first == BattleSide.Player ? TurnPhase.PlayerAct : TurnPhase.EnemyThink);
        }

        public bool CanFire(BattleSide side) => side == BattleSide.Player ? Phase == TurnPhase.PlayerAct : Phase == TurnPhase.EnemyThink;
        public bool CanUseTool(BattleSide side) => CanFire(side);

        /// A fire command was executed for this side: the shot now resolves before the other side acts.
        public void OnShotFired(BattleSide side)
        {
            if (side == BattleSide.Player && Phase == TurnPhase.PlayerAct) { PlayerShots++; SetPhase(TurnPhase.PlayerResolve); }
            else if (side == BattleSide.Enemy && Phase == TurnPhase.EnemyThink) { EnemyShots++; SetPhase(TurnPhase.EnemyResolve); }
        }

        public void Tick(float dt)
        {
            PhaseTime += dt;
            switch (Phase)
            {
                case TurnPhase.PlayerAct:
                    TimeLeft -= dt;
                    if (TimeLeft <= 0f)
                    {
                        TimeLeft = 0f;
                        OnPlayerTimedOut?.Invoke();
                        SetPhase(TurnPhase.EnemyThink);
                    }
                    break;
                case TurnPhase.EnemyThink:
                    if (PhaseTime >= balance.botTurnTimeoutSeconds) { Round++; SetPhase(TurnPhase.PlayerAct); }
                    break;
                case TurnPhase.PlayerResolve:
                    if (ShotSettled()) SetPhase(TurnPhase.EnemyThink);
                    break;
                case TurnPhase.EnemyResolve:
                    if (ShotSettled()) { Round++; SetPhase(TurnPhase.PlayerAct); }
                    break;
            }
        }

        bool ShotSettled()
        {
            int active = ctx.projectiles != null ? ctx.projectiles.ActiveCount : 0;
            if (active == 0 && PhaseTime >= balance.turnResolveMinSeconds) return true;
            return PhaseTime >= balance.turnResolveMaxSeconds;
        }

        void SetPhase(TurnPhase next)
        {
            Phase = next;
            PhaseTime = 0f;
            if (next == TurnPhase.PlayerAct) TimeLeft = balance.turnSeconds;
            OnPhaseChanged?.Invoke(next);
        }
    }
}
