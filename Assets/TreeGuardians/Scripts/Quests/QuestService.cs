using System;
using System.Collections.Generic;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Meta;
using TreeGuardians.Rewards;
using TreeGuardians.Save;
using UnityEngine;

namespace TreeGuardians.Quests
{
    /// Event-driven quests, achievements and the daily reward cycle.
    public sealed class QuestService : MonoBehaviour
    {
        PlayerProgressService progress;
        SaveService save;
        GameDatabase db;
        GameBalanceConfig balance;
        bool subscribed;

        public event Action OnChanged;

        public IReadOnlyList<QuestProgressSaveData> Quests => progress.Data.quests;
        public IReadOnlyList<AchievementProgressSaveData> Achievements => progress.Data.achievements;

        void Awake()
        {
            Services.Register(this);
        }

        void OnDestroy()
        {
            Services.Unregister(this);
            Unsubscribe();
        }

        public void Initialize(PlayerProgressService progress, SaveService save)
        {
            this.progress = progress;
            this.save = save;
            db = progress.Database;
            balance = progress.Balance;
            AssignMissing();
            CheckDailyReset();
            Subscribe();
        }

        void Subscribe()
        {
            if (subscribed) return;
            subscribed = true;
            GameEventBus.Subscribe<BattleFinishedEvent>(OnBattleFinished);
            GameEventBus.Subscribe<SectionDestroyedEvent>(OnSectionDestroyed);
            GameEventBus.Subscribe<GuardianUpgradedEvent>(OnGuardianUpgraded);
            GameEventBus.Subscribe<ChestOpenedEvent>(OnChestOpened);
            GameEventBus.Subscribe<SpecialDamageEvent>(OnSpecialDamage);
            GameEventBus.Subscribe<ToolUsedEvent>(OnToolUsed);
        }

        void Unsubscribe()
        {
            if (!subscribed) return;
            subscribed = false;
            GameEventBus.Unsubscribe<BattleFinishedEvent>(OnBattleFinished);
            GameEventBus.Unsubscribe<SectionDestroyedEvent>(OnSectionDestroyed);
            GameEventBus.Unsubscribe<GuardianUpgradedEvent>(OnGuardianUpgraded);
            GameEventBus.Unsubscribe<ChestOpenedEvent>(OnChestOpened);
            GameEventBus.Unsubscribe<SpecialDamageEvent>(OnSpecialDamage);
            GameEventBus.Unsubscribe<ToolUsedEvent>(OnToolUsed);
        }

        void AssignMissing()
        {
            var data = progress.Data;
            long now = DateTime.UtcNow.Ticks;
            for (int i = 0; i < db.quests.Count; i++)
            {
                var q = db.quests[i];
                if (q == null || FindQuest(q.id) != null) continue;
                data.quests.Add(new QuestProgressSaveData { id = q.id, assignedUtcTicks = now });
            }
            for (int i = 0; i < db.achievements.Count; i++)
            {
                var a = db.achievements[i];
                if (a == null || FindAchievement(a.id) != null) continue;
                data.achievements.Add(new AchievementProgressSaveData { id = a.id });
            }
        }

        public QuestProgressSaveData FindQuest(string id)
        {
            var list = progress.Data.quests;
            for (int i = 0; i < list.Count; i++) if (list[i].id == id) return list[i];
            return null;
        }

        public AchievementProgressSaveData FindAchievement(string id)
        {
            var list = progress.Data.achievements;
            for (int i = 0; i < list.Count; i++) if (list[i].id == id) return list[i];
            return null;
        }

        public QuestDefinition GetQuestDefinition(string id) => db.GetQuest(id);
        public AchievementDefinition GetAchievementDefinition(string id) => db.GetAchievement(id);

        public void Report(QuestType type, int amount = 1)
        {
            if (progress == null || amount <= 0) return;
            bool changed = false;
            var quests = progress.Data.quests;
            for (int i = 0; i < quests.Count; i++)
            {
                var q = quests[i];
                var def = db.GetQuest(q.id);
                if (def == null || def.type != type || q.claimed || q.progress >= def.targetCount) continue;
                q.progress = Mathf.Min(def.targetCount, q.progress + amount);
                changed = true;
                GameEventBus.Publish(new QuestProgressEvent { questId = q.id, progress = q.progress, target = def.targetCount });
            }
            var achievements = progress.Data.achievements;
            for (int i = 0; i < achievements.Count; i++)
            {
                var a = achievements[i];
                var def = db.GetAchievement(a.id);
                if (def == null || def.metric != type || a.claimed || a.progress >= def.targetCount) continue;
                a.progress = Mathf.Min(def.targetCount, a.progress + amount);
                changed = true;
            }
            if (changed) OnChanged?.Invoke();
        }

        public bool CanClaim(string questId)
        {
            var q = FindQuest(questId);
            var def = db.GetQuest(questId);
            return q != null && def != null && !q.claimed && q.progress >= def.targetCount;
        }

        public RewardBundle TryClaim(string questId)
        {
            if (!CanClaim(questId)) return null;
            var q = FindQuest(questId);
            var def = db.GetQuest(questId);
            q.claimed = true;
            var bundle = def.reward.Clone();
            Services.Get<RewardService>()?.Apply(bundle, "quest");
            GameEventBus.Publish(new QuestClaimedEvent { questId = questId });
            OnChanged?.Invoke();
            return bundle;
        }

        public bool CanClaimAchievement(string id)
        {
            var a = FindAchievement(id);
            var def = db.GetAchievement(id);
            return a != null && def != null && !a.claimed && a.progress >= def.targetCount;
        }

        public RewardBundle TryClaimAchievement(string id)
        {
            if (!CanClaimAchievement(id)) return null;
            var a = FindAchievement(id);
            var def = db.GetAchievement(id);
            a.claimed = true;
            var bundle = def.reward.Clone();
            Services.Get<RewardService>()?.Apply(bundle, "achievement");
            OnChanged?.Invoke();
            return bundle;
        }

        public int ClaimableCount
        {
            get
            {
                if (!IsInitialized) return 0;
                int c = 0;
                var quests = progress.Data.quests;
                for (int i = 0; i < quests.Count; i++) if (CanClaim(quests[i].id)) c++;
                var ach = progress.Data.achievements;
                for (int i = 0; i < ach.Count; i++) if (CanClaimAchievement(ach[i].id)) c++;
                if (CanClaimDailyReward()) c++;
                return c;
            }
        }

        /// Quests only (daily + permanent). 'completed' counts quests that reached their target, claimed or not.
        public QuestSummary GetQuestSummary()
        {
            var s = new QuestSummary();
            if (!IsInitialized) return s;
            var defs = db.quests;
            for (int i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                if (def == null) continue;
                s.total++;
                var q = FindQuest(def.id);
                if (q == null || q.progress < def.targetCount) continue;
                s.completed++;
                if (!q.claimed) s.claimable++;
            }
            return s;
        }

        /// Achievements only. 'completed' counts achievements that reached their target, claimed or not.
        public QuestSummary GetAchievementSummary()
        {
            var s = new QuestSummary();
            if (!IsInitialized) return s;
            var defs = db.achievements;
            for (int i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                if (def == null) continue;
                s.total++;
                var a = FindAchievement(def.id);
                if (a == null || a.progress < def.targetCount) continue;
                s.completed++;
                if (!a.claimed) s.claimable++;
            }
            return s;
        }

        /// Unclaimed quest rewards plus the daily reward when it is ready (what the Quests badge shows).
        public int QuestClaimableCount => GetQuestSummary().claimable + (CanClaimDailyReward() ? 1 : 0);

        /// Unclaimed achievement rewards (what the Rank badge shows).
        public int AchievementClaimableCount => GetAchievementSummary().claimable;

        public void CheckDailyReset()
        {
            var now = save.GetUtcNow();
            var last = progress.Data.lastDailyQuestResetUtcTicks > 0
                ? new DateTime(progress.Data.lastDailyQuestResetUtcTicks, DateTimeKind.Utc)
                : DateTime.MinValue;
            if (now.Date <= last.Date) return;
            var quests = progress.Data.quests;
            for (int i = 0; i < quests.Count; i++)
            {
                var def = db.GetQuest(quests[i].id);
                if (def == null || !def.isDaily) continue;
                quests[i].progress = 0;
                quests[i].claimed = false;
                quests[i].assignedUtcTicks = now.Ticks;
            }
            progress.Data.lastDailyQuestResetUtcTicks = now.Ticks;
            OnChanged?.Invoke();
        }

        // Daily reward. Offline prototype uses the monotonic device clock; a production build should
        // validate claim timestamps server-side to prevent clock manipulation.
        public int DailyRewardDayIndex
        {
            get
            {
                int cycle = db.dailyRewards != null ? db.dailyRewards.CycleLength : 7;
                return ((progress.Data.dailyReward.nextDayIndex % cycle) + cycle) % cycle;
            }
        }

        public bool IsInitialized => progress != null && db != null && save != null;

        public bool CanClaimDailyReward()
        {
            if (!IsInitialized || db.dailyRewards == null || db.dailyRewards.days.Count == 0) return false;
            return TimeUntilDailyReward() <= TimeSpan.Zero;
        }

        public TimeSpan TimeUntilDailyReward()
        {
            if (!IsInitialized) return TimeSpan.Zero;
            var last = progress.Data.dailyReward.lastClaimUtcTicks > 0
                ? new DateTime(progress.Data.dailyReward.lastClaimUtcTicks, DateTimeKind.Utc)
                : DateTime.MinValue;
            var next = last.AddHours(balance.dailyRewardCooldownHours);
            var rem = next - save.GetUtcNow();
            return rem < TimeSpan.Zero ? TimeSpan.Zero : rem;
        }

        public RewardBundle ClaimDailyReward()
        {
            if (!CanClaimDailyReward()) return null;
            int idx = DailyRewardDayIndex;
            var bundle = db.dailyRewards.GetDay(idx).Clone();
            progress.Data.dailyReward.nextDayIndex = (idx + 1) % db.dailyRewards.CycleLength;
            progress.Data.dailyReward.lastClaimUtcTicks = save.GetUtcNow().Ticks;
            Services.Get<RewardService>()?.Apply(bundle, "daily");
            OnChanged?.Invoke();
            return bundle;
        }

        void OnBattleFinished(BattleFinishedEvent e)
        {
            Report(QuestType.PlayBattles);
            if (e.outcome == BattleOutcome.Victory) Report(QuestType.WinBattles);
        }

        void OnSectionDestroyed(SectionDestroyedEvent e)
        {
            if (e.side != BattleSide.Enemy) return;
            if (e.type == TreeSectionType.BarkArmor) Report(QuestType.BreakBarkSections);
            else if (e.type == TreeSectionType.Branch) Report(QuestType.BreakBranches);
        }

        void OnGuardianUpgraded(GuardianUpgradedEvent e) => Report(QuestType.UpgradeGuardian);
        void OnChestOpened(ChestOpenedEvent e) => Report(QuestType.OpenChest);
        void OnSpecialDamage(SpecialDamageEvent e) { if (e.side == BattleSide.Player) Report(QuestType.SpecialDamage, Mathf.RoundToInt(e.amount)); }
        void OnToolUsed(ToolUsedEvent e) { if (e.side == BattleSide.Player) Report(QuestType.UseTools); }
    }
}
