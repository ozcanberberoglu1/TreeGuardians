using System.Collections.Generic;
using TreeGuardians.AI;
using TreeGuardians.Audio;
using TreeGuardians.Chests;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Guardians;
using TreeGuardians.Meta;
using TreeGuardians.Rewards;
using TreeGuardians.SceneFlow;
using TreeGuardians.Tools;
using TreeGuardians.Trees;
using TreeGuardians.UI;
using UnityEngine;

namespace TreeGuardians.Battle
{
    /// Orchestrates one battle: setup, state machine, command execution, win conditions, rewards and hand-off to Results.
    public sealed class BattleManager : MonoBehaviour
    {
        [Header("Scene systems (pre-authored)")]
        [SerializeField] TreeController playerTree;
        [SerializeField] TreeController enemyTree;
        [SerializeField] GuardianRoster playerRoster;
        [SerializeField] GuardianRoster enemyRoster;
        [SerializeField] ToolController playerTools;
        [SerializeField] ToolController enemyTools;
        [SerializeField] ProjectileService projectiles;
        [SerializeField] BattleVFX vfx;
        [SerializeField] BattleHUD hud;
        [SerializeField] ArenaPresenter arenaPresenter;
        [SerializeField] PlayerBattleCommander playerCommander;
        [SerializeField] BotBattleCommander botCommander;
        [SerializeField] BattleCameraController cameraController;
        [SerializeField] Camera battleCamera;

        [Header("Debug")]
        [SerializeField] bool autoStartWhenPlayedDirectly = true;
        [SerializeField] string directPlayArenaId = "sunny_grove";
        [SerializeField] BotDifficulty directPlayDifficulty = BotDifficulty.Normal;

        public BattleContext Context => ctx;
        public BattleStateMachine StateMachine => sm;

        BattleContext ctx;
        readonly BattleStateMachine sm = new BattleStateMachine();
        BattleOutcome outcome = BattleOutcome.None;
        BattleEndReason endReason = BattleEndReason.None;
        readonly List<GuardianDefinition> cardPool = new List<GuardianDefinition>(16);
        bool started;
        bool resultSent;
        int lastCountdown = -1;
        bool coreExposedShown;

        void Start()
        {
            BootAwaiter.WhenReady(Begin);
        }

        void OnDestroy()
        {
            Time.timeScale = 1f;
            GameEventBus.Unsubscribe<CoreExposedEvent>(OnCoreExposed);
        }

        void Begin()
        {
            if (started || this == null) return;
            started = true;
            var flow = Services.Get<SceneFlowService>();
            var progress = Services.Get<PlayerProgressService>();
            var cfg = Services.Get<GameConfigProvider>();
            if (progress == null || cfg == null) { TGLog.Error("BattleManager: services missing."); return; }

            var setup = flow != null ? flow.PendingBattleSetup : null;
            if (setup == null)
            {
                if (!autoStartWhenPlayedDirectly) return;
                var arena = progress.Database.GetArena(directPlayArenaId) ?? progress.Database.GetArenaByIndex(0);
                setup = BattleSetupFactory.Create(progress, arena, directPlayDifficulty);
                if (flow != null) flow.PendingBattleSetup = setup;
                TGLog.Info("BattleManager: no pending setup; using direct-play defaults.");
            }

            ctx = new BattleContext
            {
                setup = setup,
                balance = cfg.Balance,
                database = cfg.Database,
                rng = new System.Random(setup.seed == 0 ? 1234 : setup.seed),
                timeRemaining = cfg.Balance.battleDurationSeconds,
                playerTree = playerTree, enemyTree = enemyTree,
                playerRoster = playerRoster, enemyRoster = enemyRoster,
                playerTools = playerTools, enemyTools = enemyTools,
                projectiles = projectiles, vfx = vfx
            };
            ctx.damage = new DamageResolver(ctx);
            ctx.targeting = new TargetingController(ctx);
            if (playerTree != null && enemyTree != null) ctx.targeting.MidlineX = (playerTree.transform.position.x + enemyTree.transform.position.x) * 0.5f;

            vfx?.Initialize();
            projectiles?.Initialize(ctx);

            var enemyUpgrades = new int[6];
            for (int i = 0; i < 6; i++) enemyUpgrades[i] = Mathf.Clamp(setup.enemyTreeUpgradeLevel / 2, 0, cfg.Balance.treeUpgradeMaxLevel);
            playerTree?.Initialize(setup.playerTreeUpgrades, setup.playerTreeTier, ctx);
            enemyTree?.Initialize(enemyUpgrades, setup.enemyTreeTier, ctx);
            playerRoster?.Initialize(setup.playerGuardianIds, setup.playerGuardianLevels, 1, setup.activeSlots, ctx, playerTree);
            enemyRoster?.Initialize(setup.enemyGuardianIds, null, setup.enemyGuardianLevel, setup.activeSlots, ctx, enemyTree);
            playerTools?.Initialize(setup.playerToolIds, setup.playerToolLevels, ctx, playerTree);
            enemyTools?.Initialize(setup.enemyToolIds, null, ctx, enemyTree);
            ctx.targeting.BuildRegistry();
            PrewarmProjectiles();
            ctx.enemyStructureTotalAtStart = enemyTree != null ? enemyTree.StructureHealthTotal : 1f;

            var arenaDef = ctx.database.GetArena(setup.arenaId);
            arenaPresenter?.Apply(arenaDef);
            Services.Get<AudioService>()?.PlayMusic(arenaDef != null ? arenaDef.music : MusicTrackId.BattleSunny);

            playerCommander?.Initialize(ctx);
            botCommander?.Initialize(ctx);
            hud?.Initialize(ctx, this, playerCommander);

            if (playerTree != null) playerTree.OnCoreDestroyed += () => End(BattleOutcome.Defeat, BattleEndReason.CoreDestroyed);
            if (enemyTree != null) enemyTree.OnCoreDestroyed += () => End(BattleOutcome.Victory, BattleEndReason.CoreDestroyed);
            GameEventBus.Subscribe<CoreExposedEvent>(OnCoreExposed);

            GameEventBus.Publish(new BattleStartedEvent { arenaId = setup.arenaId });
            sm.Set(BattleState.Intro);
        }

        void PrewarmProjectiles()
        {
            if (projectiles == null) return;
            Prewarm(playerRoster);
            Prewarm(enemyRoster);
        }

        void Prewarm(GuardianRoster roster)
        {
            if (roster == null) return;
            for (int i = 0; i < roster.Slots.Count; i++)
            {
                var g = roster.Slots[i];
                if (g == null || !g.IsActive) continue;
                projectiles.Prewarm(g.Definition.normalProjectile);
                projectiles.Prewarm(g.Definition.specialProjectile);
            }
        }

        void OnCoreExposed(CoreExposedEvent e)
        {
            if (e.side != BattleSide.Enemy || coreExposedShown) return;
            coreExposedShown = true;
            hud?.ShowMessage("battle_core_exposed", 1.4f);
            Services.Get<AudioService>()?.PlaySfx(AudioEventId.CoreExposed);
        }

        void Update()
        {
            if (ctx == null) return;
            float dt = Time.deltaTime;
            if (sm.State != BattleState.Paused) sm.Tick(dt);
            switch (sm.State)
            {
                case BattleState.Intro:
                    if (sm.StateTime >= ctx.balance.introSeconds) { sm.Set(BattleState.ReadyCountdown); hud?.ShowMessage("battle_ready", ctx.balance.readyCountdownSeconds); lastCountdown = -1; }
                    break;
                case BattleState.ReadyCountdown:
                {
                    int remaining = Mathf.CeilToInt(ctx.balance.readyCountdownSeconds - sm.StateTime);
                    if (remaining != lastCountdown && remaining > 0) { lastCountdown = remaining; Services.Get<AudioService>()?.PlaySfx(AudioEventId.Countdown); }
                    if (sm.StateTime >= ctx.balance.readyCountdownSeconds)
                    {
                        sm.Set(BattleState.Playing);
                        ctx.isPlaying = true;
                        hud?.ShowMessage("battle_fight", 0.9f);
                    }
                    break;
                }
                case BattleState.Playing:
                    ctx.elapsed += dt;
                    ctx.timeRemaining -= dt;
                    TickSystems(dt);
                    DrainCommands(playerCommander, dt);
                    DrainCommands(botCommander, dt);
                    CheckWinConditions();
                    break;
                case BattleState.ResolvingFinalHit:
                    TickSystems(dt);
                    if ((projectiles == null || projectiles.ActiveCount == 0) || sm.StateTime > 2.5f) ResolveTimeUp();
                    break;
                case BattleState.Victory:
                case BattleState.Defeat:
                case BattleState.Draw:
                    vfx?.Tick(dt);
                    if (sm.StateTime >= ctx.balance.resultRevealDelaySeconds) Exit();
                    break;
            }
            hud?.Tick();
        }

        void FixedUpdate()
        {
            if (ctx == null) return;
            if (sm.State == BattleState.Playing || sm.State == BattleState.ResolvingFinalHit) projectiles?.Tick(Time.fixedDeltaTime);
        }

        void TickSystems(float dt)
        {
            playerTree?.Tick();
            enemyTree?.Tick();
            playerRoster?.Tick(dt);
            enemyRoster?.Tick(dt);
            playerTools?.Tick(dt);
            enemyTools?.Tick(dt);
            vfx?.Tick(dt);
            playerTree?.UpdateHealthBar();
            enemyTree?.UpdateHealthBar();
        }

        void DrainCommands(IBattleCommander commander, float dt)
        {
            if (commander == null) return;
            commander.Tick(dt);
            int guard = 0;
            while (guard++ < 8 && commander.TryDequeue(out var cmd)) Execute(cmd);
        }

        public void Execute(BattleCommand cmd)
        {
            if (!sm.AcceptsCommands) return;
            var roster = ctx.RosterOf(cmd.side);
            switch (cmd.type)
            {
                case BattleCommandType.SelectGuardian:
                    if (cmd.side == BattleSide.Player) roster?.SetPlayerControlled(cmd.slotIndex);
                    break;
                case BattleCommandType.FireNormal:
                case BattleCommandType.FireSpecial:
                {
                    var g = roster?.Get(cmd.slotIndex);
                    if (g == null) return;
                    g.Fire(cmd.direction, cmd.power, cmd.type == BattleCommandType.FireSpecial);
                    break;
                }
                case BattleCommandType.UseTool:
                    ctx.ToolsOf(cmd.side)?.Use(cmd.slotIndex, cmd.targetPoint);
                    break;
            }
        }

        void CheckWinConditions()
        {
            if (sm.State != BattleState.Playing) return;
            if (enemyTree != null && enemyTree.IsCoreDestroyed) { End(BattleOutcome.Victory, BattleEndReason.CoreDestroyed); return; }
            if (playerTree != null && playerTree.IsCoreDestroyed) { End(BattleOutcome.Defeat, BattleEndReason.CoreDestroyed); return; }
            if (enemyRoster != null && enemyRoster.ActiveCount > 0 && enemyRoster.AliveCount == 0) { End(BattleOutcome.Victory, BattleEndReason.AllGuardiansDown); return; }
            if (playerRoster != null && playerRoster.ActiveCount > 0 && playerRoster.AliveCount == 0) { End(BattleOutcome.Defeat, BattleEndReason.AllGuardiansDown); return; }
            if (ctx.timeRemaining <= 0f)
            {
                ctx.timeRemaining = 0f;
                ctx.isPlaying = false;
                sm.Set(BattleState.ResolvingFinalHit);
                hud?.ShowMessage("battle_time_up", 1.5f);
            }
        }

        void ResolveTimeUp()
        {
            if (enemyTree != null && enemyTree.IsCoreDestroyed) { End(BattleOutcome.Victory, BattleEndReason.CoreDestroyed); return; }
            if (playerTree != null && playerTree.IsCoreDestroyed) { End(BattleOutcome.Defeat, BattleEndReason.CoreDestroyed); return; }
            float p = Score(BattleSide.Player);
            float e = Score(BattleSide.Enemy);
            if (Mathf.Abs(p - e) < 2f) End(BattleOutcome.Draw, BattleEndReason.TimeUp);
            else End(p > e ? BattleOutcome.Victory : BattleOutcome.Defeat, BattleEndReason.TimeUp);
        }

        float Score(BattleSide side)
        {
            var w = ctx.balance.timeUpScoring;
            var tree = ctx.TreeOf(side);
            var roster = ctx.RosterOf(side);
            var enemy = ctx.TreeOf(BattleContext.Opponent(side));
            float core = tree != null ? tree.CorePercent * 100f * w.coreHealthWeight : 0f;
            float alive = roster != null ? roster.AliveCount * w.aliveGuardianWeight : 0f;
            float structure = enemy != null ? enemy.StructureDamagePercent() * 100f * w.structureDamageWeight : 0f;
            return core + alive + structure;
        }

        void End(BattleOutcome result, BattleEndReason reason)
        {
            if (sm.IsFinished) return;
            outcome = result;
            endReason = reason;
            ctx.isPlaying = false;
            Time.timeScale = 1f;
            sm.Set(result == BattleOutcome.Victory ? BattleState.Victory : result == BattleOutcome.Defeat ? BattleState.Defeat : BattleState.Draw);
            playerCommander?.OnBattleEnded();
            botCommander?.OnBattleEnded();
            projectiles?.ReleaseAll();
            hud?.ShowMessage(result == BattleOutcome.Victory ? "battle_victory" : result == BattleOutcome.Defeat ? "battle_defeat" : "battle_draw", 3f);
            hud?.ShowPause(false);
            Services.Get<AudioService>()?.PlaySfx(result == BattleOutcome.Victory ? AudioEventId.Victory : AudioEventId.Defeat);
            Services.Get<HapticService>()?.Heavy();
            GameEventBus.Publish(new BattleFinishedEvent { outcome = result, reason = reason });
            ApplyResult();
        }

        BattleResultData resultData;

        void ApplyResult()
        {
            var progress = Services.Get<PlayerProgressService>();
            var rewards = Services.Get<RewardService>();
            var chests = Services.Get<ChestService>();
            if (progress == null) return;
            var s = ctx.playerStats;
            var arena = ctx.database.GetArena(ctx.setup.arenaId);
            float mult = arena != null ? arena.rewardMultiplier : 1f;
            bool win = outcome == BattleOutcome.Victory;

            var r = new BattleResultData
            {
                outcome = outcome, reason = endReason, arenaId = ctx.setup.arenaId, durationSeconds = ctx.elapsed,
                damageDealt = s.damageDealt, structureDamagePercent = enemyTree != null ? enemyTree.StructureDamagePercent() : 0f,
                playerCorePercent = playerTree != null ? playerTree.CorePercent : 0f, enemyCorePercent = enemyTree != null ? enemyTree.CorePercent : 0f,
                playerGuardiansAlive = playerRoster != null ? playerRoster.AliveCount : 0, enemyGuardiansAlive = enemyRoster != null ? enemyRoster.AliveCount : 0,
                shotsFired = s.shotsFired, shotsHit = s.shotsHit, specialDamage = s.specialDamage, barkBroken = s.barkBroken, branchesBroken = s.branchesBroken, toolsUsed = s.toolsUsed
            };
            r.stars = outcome == BattleOutcome.Draw ? 1 : !win ? 0 : (endReason == BattleEndReason.CoreDestroyed && r.playerCorePercent > 0.66f) ? 3 : (r.playerCorePercent > 0.33f || endReason == BattleEndReason.AllGuardiansDown) ? 2 : 1;

            var bundle = new RewardBundle
            {
                coins = Mathf.RoundToInt((win ? ctx.balance.winCoinsBase : outcome == BattleOutcome.Draw ? ctx.balance.loseCoinsBase * 2 : ctx.balance.loseCoinsBase) * mult),
                sap = Mathf.RoundToInt((win ? ctx.balance.winSapBase : ctx.balance.loseSapBase) * mult),
                trophies = win ? ctx.balance.trophiesOnWin : outcome == BattleOutcome.Draw ? ctx.balance.trophiesOnDraw : -Mathf.Min(progress.Data.trophies, ctx.balance.trophiesOnLoss)
            };
            if (win)
            {
                for (int i = 0; i < ctx.balance.winCardDraws; i++)
                {
                    var g = PickCard(progress);
                    if (g != null) bundle.AddCards(g.id, 1);
                }
                if (arena != null && chests != null && chests.HasFreeSlot && ctx.NextFloat() < ctx.balance.winChestChance)
                {
                    var chestId = arena.PickChestId(ctx.rng);
                    if (!string.IsNullOrEmpty(chestId)) { bundle.chestIds.Add(chestId); r.chestId = chestId; }
                }
            }
            r.trophyDelta = bundle.trophies;

            int arenaBefore = progress.CurrentArenaIndex;
            var stats = progress.Data.stats;
            stats.battlesPlayed++;
            if (win) stats.wins++; else if (outcome == BattleOutcome.Defeat) stats.losses++; else stats.draws++;
            stats.barkBroken += s.barkBroken; stats.branchesBroken += s.branchesBroken; stats.toolsUsed += s.toolsUsed;
            stats.shotsFired += s.shotsFired; stats.shotsHit += s.shotsHit; stats.specialDamage += s.specialDamage; stats.totalDamage += s.damageDealt;
            if (!ctx.setup.isTutorial || true) progress.Data.tutorial.battleTutorialDone = progress.Data.tutorial.battleTutorialDone || ctx.setup.isTutorial;

            r.rewards = bundle;
            if (rewards != null)
            {
                var applied = rewards.Apply(bundle, "battle");
                r.rewardsApplied = true;
                if (applied != null) { r.unlockedGuardianIds.AddRange(applied.unlockedGuardianIds); r.unlockedToolIds.AddRange(applied.unlockedToolIds); }
            }
            else progress.Save();
            if (progress.CurrentArenaIndex != arenaBefore)
            {
                var newArena = progress.CurrentArena;
                r.unlockedArenaId = newArena != null ? newArena.id : "";
            }
            resultData = r;
        }

        GuardianDefinition PickCard(PlayerProgressService progress)
        {
            cardPool.Clear();
            var list = ctx.database.guardians;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].arenaUnlockIndex <= progress.Data.highestArenaIndex) cardPool.Add(list[i]);
            if (cardPool.Count == 0) return null;
            float roll = ctx.NextFloat() * 100f;
            Rarity want = roll < 60f ? Rarity.Common : roll < 90f ? Rarity.Rare : roll < 99f ? Rarity.Epic : Rarity.Legendary;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                int count = 0;
                for (int i = 0; i < cardPool.Count; i++) if (cardPool[i].rarity == want) count++;
                if (count > 0)
                {
                    int pick = ctx.rng.Next(count);
                    for (int i = 0; i < cardPool.Count; i++) if (cardPool[i].rarity == want && pick-- == 0) return cardPool[i];
                }
                if (want == Rarity.Common) break;
                want = (Rarity)((int)want - 1);
            }
            return cardPool[ctx.rng.Next(cardPool.Count)];
        }

        void Exit()
        {
            if (resultSent) return;
            resultSent = true;
            sm.Set(BattleState.Exiting);
            var flow = Services.Get<SceneFlowService>();
            if (flow == null) return;
            var transition = hud != null ? hud.Transition : null;
            if (transition != null) transition.Cover(() => flow.GoToResults(resultData));
            else flow.GoToResults(resultData);
        }

        public void TogglePause()
        {
            if (sm.State == BattleState.Paused) Resume(); else Pause();
        }

        public void Pause()
        {
            if (sm.IsFinished) return;
            sm.Pause();
            if (sm.State != BattleState.Paused) return;
            ctx.isPlaying = false;
            Time.timeScale = 0f;
            hud?.ShowPause(true);
        }

        public void Resume()
        {
            if (sm.State != BattleState.Paused) return;
            sm.Resume();
            Time.timeScale = 1f;
            ctx.isPlaying = sm.State == BattleState.Playing;
            hud?.ShowPause(false);
        }

        public void Forfeit()
        {
            Time.timeScale = 1f;
            if (sm.State == BattleState.Paused) sm.Resume();
            End(BattleOutcome.Defeat, BattleEndReason.Forfeit);
        }
    }
}
