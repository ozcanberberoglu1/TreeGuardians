using System;
using System.Collections.Generic;
using TreeGuardians.Data;
using TreeGuardians.Rewards;

namespace TreeGuardians.Battle
{
    /// Everything a battle needs to start. Built by the menu, consumed by the Battle scene.
    [Serializable]
    public sealed class BattleSetup
    {
        public string arenaId = "";
        public string[] playerGuardianIds = new string[8];
        public int[] playerGuardianLevels = new int[8];
        public string[] playerToolIds = new string[3];
        public int[] playerToolLevels = new int[3];
        public int[] playerTreeUpgrades = new int[6];
        public TreeVisualTier playerTreeTier;
        public string[] enemyGuardianIds = new string[8];
        public int enemyGuardianLevel = 1;
        public string[] enemyToolIds = new string[3];
        public int enemyTreeUpgradeLevel;
        public TreeVisualTier enemyTreeTier;
        public BotDifficulty difficulty = BotDifficulty.Normal;
        public int activeSlots = 6;
        public bool isTutorial;
        public int seed;
        public int playerTrophies;
    }

    /// Outcome and statistics of a finished battle. Consumed by the Results scene.
    [Serializable]
    public sealed class BattleResultData
    {
        public BattleOutcome outcome = BattleOutcome.None;
        public BattleEndReason reason = BattleEndReason.None;
        public string arenaId = "";
        public int stars;
        public float durationSeconds;
        public float damageDealt;
        public float structureDamagePercent;
        public float playerCorePercent;
        public float enemyCorePercent;
        public int playerGuardiansAlive;
        public int enemyGuardiansAlive;
        public int shotsFired;
        public int shotsHit;
        public float specialDamage;
        public int barkBroken;
        public int branchesBroken;
        public int toolsUsed;
        public int trophyDelta;
        public RewardBundle rewards = new RewardBundle();
        public string chestId = "";
        public List<string> unlockedGuardianIds = new List<string>();
        public List<string> unlockedToolIds = new List<string>();
        public string unlockedArenaId = "";
        public bool rewardsApplied;

        public float Accuracy => shotsFired > 0 ? (float)shotsHit / shotsFired : 0f;
    }
}
