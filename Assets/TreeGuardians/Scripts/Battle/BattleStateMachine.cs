using System;

namespace TreeGuardians.Battle
{
    public enum BattleState { Initializing, Intro, ReadyCountdown, Playing, Paused, ResolvingFinalHit, Victory, Defeat, Draw, Exiting }

    /// Single owner of battle state transitions.
    public sealed class BattleStateMachine
    {
        public BattleState State { get; private set; } = BattleState.Initializing;
        public float StateTime { get; private set; }
        float prePauseTime;
        public event Action<BattleState, BattleState> OnChanged;

        BattleState prePause = BattleState.Playing;

        public bool IsPlaying => State == BattleState.Playing;
        public bool IsFinished => State == BattleState.Victory || State == BattleState.Defeat || State == BattleState.Draw || State == BattleState.Exiting;
        public bool AcceptsCommands => State == BattleState.Playing;

        public void Set(BattleState next)
        {
            if (next == State) return;
            if (IsFinished && next != BattleState.Exiting) return;
            var prev = State;
            State = next;
            StateTime = 0f;
            OnChanged?.Invoke(prev, next);
        }

        public void Pause()
        {
            if (State != BattleState.Playing && State != BattleState.ReadyCountdown && State != BattleState.Intro) return;
            prePause = State;
            prePauseTime = StateTime;
            Set(BattleState.Paused);
        }

        public void Resume()
        {
            if (State != BattleState.Paused) return;
            Set(prePause);
            StateTime = prePauseTime; // intro / countdown continue where they were
        }

        public void Tick(float dt) => StateTime += dt;
    }
}
