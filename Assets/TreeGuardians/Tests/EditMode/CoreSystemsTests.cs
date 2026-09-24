using System;
using NUnit.Framework;
using TreeGuardians.Battle;
using TreeGuardians.Chests;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Economy;
using TreeGuardians.Meta;
using TreeGuardians.Quests;
using TreeGuardians.Rewards;
using TreeGuardians.Save;
using UnityEngine;

namespace TreeGuardians.Tests
{
    static class TestContent
    {
        public static GameBalanceConfig Balance()
        {
            var b = ScriptableObject.CreateInstance<GameBalanceConfig>();
            b.startingCoins = 1000; b.startingSap = 50; b.startingGems = 5;
            b.starterGuardianIds = new[] { "g_common" };
            b.starterToolIds = Array.Empty<string>();
            b.starterChestId = "chest_test";
            return b;
        }

        public static GuardianDefinition Guardian(string id, Rarity rarity)
        {
            var g = ScriptableObject.CreateInstance<GuardianDefinition>();
            g.id = id; g.rarity = rarity; g.nameKey = id;
            return g;
        }

        public static ChestDefinition Chest(string id, float seconds)
        {
            var c = ScriptableObject.CreateInstance<ChestDefinition>();
            c.id = id; c.unlockSeconds = seconds; c.cardDraws = 2; c.coinsMin = 10; c.coinsMax = 10;
            return c;
        }

        public static GameDatabase Database()
        {
            var db = ScriptableObject.CreateInstance<GameDatabase>();
            db.guardians.Add(Guardian("g_common", Rarity.Common));
            db.guardians.Add(Guardian("g_epic", Rarity.Epic));
            db.chests.Add(Chest("chest_test", 60f));
            db.Build();
            return db;
        }

        public static PlayerProgressService Progress(out GameObject host, bool isNew = true)
        {
            host = new GameObject("TestProgress");
            var p = host.AddComponent<PlayerProgressService>();
            var data = new PlayerSaveData { schemaVersion = SaveService.CurrentSchemaVersion, playerId = "test" };
            p.InitializeDirect(Balance(), Database(), data, isNew);
            return p;
        }
    }

    public class WalletTests
    {
        [Test]
        public void TrySpend_SucceedsWhenAffordable_FailsWhenNot()
        {
            var data = new PlayerSaveData { coins = 100 };
            var w = new CurrencyWallet(data);
            Assert.IsTrue(w.TrySpend(CurrencyType.Coins, 60));
            Assert.AreEqual(40, data.coins);
            Assert.IsFalse(w.TrySpend(CurrencyType.Coins, 41));
            Assert.AreEqual(40, data.coins);
        }

        [Test]
        public void Add_NeverGoesNegative()
        {
            var data = new PlayerSaveData { trophies = 10 };
            var w = new CurrencyWallet(data);
            w.Add(CurrencyType.Trophies, -30);
            Assert.AreEqual(0, data.trophies);
        }

        [Test]
        public void TrySpendTwoCosts_IsAtomic()
        {
            var data = new PlayerSaveData { coins = 100, sap = 5 };
            var w = new CurrencyWallet(data);
            Assert.IsFalse(w.TrySpend(new Cost(CurrencyType.Coins, 50), new Cost(CurrencyType.Sap, 10)));
            Assert.AreEqual(100, data.coins);
            Assert.AreEqual(5, data.sap);
        }
    }

    public class GuardianUpgradeTests
    {
        [Test]
        public void Upgrade_RequiresCardsAndCoins_ThenConsumesBoth()
        {
            var p = TestContent.Progress(out var host);
            try
            {
                var state = p.GetGuardianState("g_common");
                Assert.IsTrue(state.unlocked);
                Assert.IsFalse(p.CanUpgradeGuardian("g_common", out int cards, out int coins, out string reason));
                Assert.AreEqual("insufficient_cards", reason);
                state.shards = cards;
                Assert.IsTrue(p.CanUpgradeGuardian("g_common", out _, out _, out _));
                int coinsBefore = p.Data.coins;
                Assert.IsTrue(p.TryUpgradeGuardian("g_common"));
                Assert.AreEqual(2, p.GetGuardianLevel("g_common"));
                Assert.AreEqual(0, state.shards);
                Assert.AreEqual(coinsBefore - coins, p.Data.coins);
                Assert.IsFalse(p.TryUpgradeGuardian("g_common"));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [Test]
        public void AddShards_UnlocksLockedGuardian()
        {
            var p = TestContent.Progress(out var host);
            try
            {
                Assert.IsFalse(p.IsGuardianUnlocked("g_epic"));
                bool unlocked = p.AddShards("g_epic", 2);
                Assert.IsTrue(unlocked);
                Assert.IsTrue(p.IsGuardianUnlocked("g_epic"));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }
    }

    public class RewardTests
    {
        [Test]
        public void RewardBundle_AppliesExactlyOnce()
        {
            var p = TestContent.Progress(out var host);
            var rewardsGo = new GameObject("Rewards");
            var rewards = rewardsGo.AddComponent<RewardService>();
            try
            {
                rewards.Initialize(p, null, null);
                var bundle = new RewardBundle { coins = 100 };
                int before = p.Data.coins;
                Assert.IsTrue(rewards.Apply(bundle, "test", false).applied);
                Assert.IsFalse(rewards.Apply(bundle, "test", false).applied);
                Assert.AreEqual(before + 100, p.Data.coins);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(rewardsGo); }
        }
    }

    public class SaveTests
    {
        [Test]
        public void Serialize_RoundTrip_PreservesFields()
        {
            var data = new PlayerSaveData { coins = 123, sap = 7, trophies = 55, displayName = "Ağaç Koruyucu" };
            data.guardians.Add(new GuardianSaveData { id = "x", unlocked = true, level = 4, shards = 9 });
            data.equippedGuardianIds[2] = "x";
            var json = SaveService.ToJson(data);
            var back = SaveService.FromJson(json);
            Assert.AreEqual(123, back.coins);
            Assert.AreEqual("Ağaç Koruyucu", back.displayName);
            Assert.AreEqual(1, back.guardians.Count);
            Assert.AreEqual(4, back.guardians[0].level);
            Assert.AreEqual("x", back.equippedGuardianIds[2]);
        }

        [Test]
        public void Migration_FromVersion0_FillsMissingStructures()
        {
            var go = new GameObject("Save");
            var save = go.AddComponent<SaveService>();
            try
            {
                var old = new PlayerSaveData { schemaVersion = 0, equippedGuardianIds = new string[3], tree = null, settings = null, playerId = "" };
                save.ApplyMigrations(old);
                Assert.AreEqual(SaveService.CurrentSchemaVersion, old.schemaVersion);
                Assert.AreEqual(8, old.equippedGuardianIds.Length);
                Assert.IsNotNull(old.tree);
                Assert.IsNotNull(old.settings);
                Assert.IsFalse(string.IsNullOrEmpty(old.playerId));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }

    public class ChestTests
    {
        [Test]
        public void ChestTimer_CompletesAndClamps()
        {
            var p = TestContent.Progress(out var host);
            var saveGo = new GameObject("Save");
            var save = saveGo.AddComponent<SaveService>();
            save.SetPathOverride(System.IO.Path.Combine(Application.temporaryCachePath, "tg_test_save.json"));
            var chestsGo = new GameObject("Chests");
            var chests = chestsGo.AddComponent<ChestService>();
            try
            {
                chests.Initialize(p, save, true);
                Assert.IsFalse(chests.GetDefinition(0) == null, "starter chest should be placed");
                Assert.IsFalse(chests.IsReady(0));
                Assert.IsTrue(chests.StartUnlock(0));
                Assert.IsFalse(chests.CanStartUnlock(0));
                var slot = p.Data.chestSlots[0];
                slot.unlockEndUtcTicks = slot.unlockStartUtcTicks + TimeSpan.FromHours(50).Ticks;
                Assert.LessOrEqual(chests.GetRemainingSeconds(0), 60.0);
                slot.unlockEndUtcTicks = DateTime.UtcNow.AddSeconds(-1).Ticks;
                Assert.IsTrue(chests.IsReady(0));
                var bundle = chests.RollContents(chests.GetDefinition(0), 0, new System.Random(42));
                Assert.AreEqual(10, bundle.coins);
                Assert.IsTrue(bundle.cards.Count > 0);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(saveGo); UnityEngine.Object.DestroyImmediate(chestsGo); }
        }
    }

    public class QuestSummaryTests
    {
        static QuestDefinition Quest(string id, QuestType type, int target)
        {
            var q = ScriptableObject.CreateInstance<QuestDefinition>();
            q.id = id; q.type = type; q.targetCount = target; q.reward = new RewardBundle { coins = 5 };
            return q;
        }

        static AchievementDefinition Achievement(string id, QuestType metric, int target)
        {
            var a = ScriptableObject.CreateInstance<AchievementDefinition>();
            a.id = id; a.metric = metric; a.targetCount = target; a.reward = new RewardBundle { coins = 5 };
            return a;
        }

        [Test]
        public void Summaries_CountCompletedAndClaimableSeparately()
        {
            var p = TestContent.Progress(out var host);
            p.Database.quests.Add(Quest("q_win", QuestType.WinBattles, 1));
            p.Database.quests.Add(Quest("q_play", QuestType.PlayBattles, 3));
            p.Database.achievements.Add(Achievement("a_win", QuestType.WinBattles, 2));
            p.Database.Build();
            var saveGo = new GameObject("Save");
            var save = saveGo.AddComponent<SaveService>();
            save.SetPathOverride(System.IO.Path.Combine(Application.temporaryCachePath, "tg_test_quests.json"));
            var questsGo = new GameObject("Quests");
            var quests = questsGo.AddComponent<QuestService>();
            try
            {
                quests.Initialize(p, save);
                var q0 = quests.GetQuestSummary();
                Assert.AreEqual(2, q0.total);
                Assert.AreEqual(0, q0.completed);
                Assert.AreEqual(0, q0.claimable);

                quests.Report(QuestType.WinBattles);
                var q1 = quests.GetQuestSummary();
                Assert.AreEqual(1, q1.completed);
                Assert.AreEqual(1, q1.claimable);
                Assert.AreEqual(0, quests.GetAchievementSummary().completed, "achievement needs two wins");

                Assert.IsNotNull(quests.TryClaim("q_win"));
                var q2 = quests.GetQuestSummary();
                Assert.AreEqual(1, q2.completed, "claimed quests still count as completed");
                Assert.AreEqual(0, q2.claimable);

                quests.Report(QuestType.WinBattles);
                var a = quests.GetAchievementSummary();
                Assert.AreEqual(1, a.total);
                Assert.AreEqual(1, a.completed);
                Assert.AreEqual(1, a.claimable);
                Assert.AreEqual(1, quests.AchievementClaimableCount);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(saveGo); UnityEngine.Object.DestroyImmediate(questsGo); }
        }
    }

    public class DamageTests
    {
        [Test]
        public void ArmorReduce_HalvesAt100Armor()
        {
            Assert.AreEqual(50f, DamageResolver.ArmorReduce(100f, 100f), 0.001f);
            Assert.AreEqual(100f, DamageResolver.ArmorReduce(100f, 0f), 0.001f);
            Assert.AreEqual(100f, DamageResolver.ArmorReduce(100f, -20f), 0.001f);
        }

        [Test]
        public void BallisticSolver_ReachesTargetOnFlatGround()
        {
            Vector2 from = Vector2.zero, to = new Vector2(10f, 0f);
            Assert.IsTrue(ProjectileService.SolveBallistic(from, to, 15f, 22f, out var v));
            float t = to.x / v.x;
            float y = v.y * t - 0.5f * 22f * t * t;
            Assert.AreEqual(0f, y, 0.05f);
        }
    }

    public class BattleStateTests
    {
        [Test]
        public void FinishedState_CannotBeOverridden()
        {
            var sm = new BattleStateMachine();
            sm.Set(BattleState.Playing);
            sm.Set(BattleState.Victory);
            sm.Set(BattleState.Defeat);
            Assert.AreEqual(BattleState.Victory, sm.State);
            sm.Set(BattleState.Exiting);
            Assert.AreEqual(BattleState.Exiting, sm.State);
        }

        [Test]
        public void PauseResume_RestoresPreviousState()
        {
            var sm = new BattleStateMachine();
            sm.Set(BattleState.Playing);
            sm.Pause();
            Assert.AreEqual(BattleState.Paused, sm.State);
            sm.Resume();
            Assert.AreEqual(BattleState.Playing, sm.State);
        }
    }
}
