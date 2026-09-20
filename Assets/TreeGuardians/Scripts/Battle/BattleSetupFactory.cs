using System;
using TreeGuardians.Data;
using TreeGuardians.Meta;
using UnityEngine;

namespace TreeGuardians.Battle
{
    public static class BattleSetupFactory
    {
        public static BattleSetup Create(PlayerProgressService progress, ArenaDefinition arena, BotDifficulty difficulty, bool tutorial = false)
        {
            var db = progress.Database;
            var setup = new BattleSetup
            {
                arenaId = arena != null ? arena.id : "",
                difficulty = difficulty,
                activeSlots = arena != null ? arena.activeGuardianSlots : 6,
                isTutorial = tutorial,
                seed = unchecked((int)DateTime.UtcNow.Ticks),
                playerTrophies = progress.Data.trophies,
                playerTreeTier = progress.GetTreeVisualTier()
            };

            int slots = progress.Balance.guardianSlotCount;
            setup.playerGuardianIds = new string[slots];
            setup.playerGuardianLevels = new int[slots];
            for (int i = 0; i < slots; i++)
            {
                var id = progress.GetEquippedGuardianId(i);
                setup.playerGuardianIds[i] = id ?? "";
                setup.playerGuardianLevels[i] = string.IsNullOrEmpty(id) ? 1 : progress.GetGuardianLevel(id);
            }

            int toolSlots = progress.Balance.toolSlotCount;
            setup.playerToolIds = new string[toolSlots];
            setup.playerToolLevels = new int[toolSlots];
            for (int i = 0; i < toolSlots; i++)
            {
                var id = progress.GetEquippedToolId(i);
                setup.playerToolIds[i] = id ?? "";
                setup.playerToolLevels[i] = string.IsNullOrEmpty(id) ? 1 : progress.GetToolLevel(id);
            }

            setup.playerTreeUpgrades = (int[])progress.Data.tree.upgradeLevels.Clone();

            setup.enemyGuardianIds = new string[slots];
            if (arena != null)
            {
                for (int i = 0; i < slots; i++)
                    setup.enemyGuardianIds[i] = i < arena.botGuardianIds.Length && db.GetGuardian(arena.botGuardianIds[i]) != null ? arena.botGuardianIds[i] : "";
                setup.enemyGuardianLevel = Mathf.Max(1, arena.botGuardianLevel);
                setup.enemyToolIds = new string[toolSlots];
                for (int i = 0; i < toolSlots; i++)
                    setup.enemyToolIds[i] = i < arena.botToolIds.Length ? arena.botToolIds[i] : "";
                setup.enemyTreeTier = arena.botTreeTier;
                setup.enemyTreeUpgradeLevel = arena.botTreeUpgradeLevel;
            }
            else
            {
                setup.enemyToolIds = new string[toolSlots];
            }
            return setup;
        }
    }
}
