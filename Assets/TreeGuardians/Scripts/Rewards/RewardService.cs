using System.Collections.Generic;
using TreeGuardians.Chests;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Meta;
using TreeGuardians.Save;
using UnityEngine;

namespace TreeGuardians.Rewards
{
    public sealed class RewardApplyResult
    {
        public bool applied;
        public readonly List<string> unlockedGuardianIds = new List<string>();
        public readonly List<string> unlockedToolIds = new List<string>();
        public readonly List<string> grantedChestIds = new List<string>();
        public readonly List<string> droppedChestIds = new List<string>();
    }

    /// Applies RewardBundles exactly once, then persists.
    public sealed class RewardService : MonoBehaviour
    {
        PlayerProgressService progress;
        ChestService chests;
        SaveService save;

        void Awake()
        {
            Services.Register(this);
        }

        void OnDestroy()
        {
            Services.Unregister(this);
        }

        public void Initialize(PlayerProgressService progress, ChestService chests, SaveService save)
        {
            this.progress = progress;
            this.chests = chests;
            this.save = save;
        }

        public RewardApplyResult Apply(RewardBundle bundle, string source, bool persist = true)
        {
            var result = new RewardApplyResult();
            if (bundle == null || bundle.consumed || progress == null) return result;
            bundle.consumed = true;

            var wallet = progress.Wallet;
            wallet.Add(CurrencyType.Coins, bundle.coins);
            wallet.Add(CurrencyType.Sap, bundle.sap);
            wallet.Add(CurrencyType.Gems, bundle.gems);
            if (bundle.trophies != 0)
            {
                wallet.Add(CurrencyType.Trophies, bundle.trophies);
                progress.RefreshArena();
            }

            for (int i = 0; i < bundle.cards.Count; i++)
            {
                var c = bundle.cards[i];
                if (progress.AddShards(c.guardianId, c.count)) result.unlockedGuardianIds.Add(c.guardianId);
            }

            for (int i = 0; i < bundle.unlockGuardianIds.Count; i++)
                if (progress.UnlockGuardian(bundle.unlockGuardianIds[i])) result.unlockedGuardianIds.Add(bundle.unlockGuardianIds[i]);

            for (int i = 0; i < bundle.unlockToolIds.Count; i++)
                if (progress.UnlockTool(bundle.unlockToolIds[i])) result.unlockedToolIds.Add(bundle.unlockToolIds[i]);

            for (int i = 0; i < bundle.chestIds.Count; i++)
            {
                var id = bundle.chestIds[i];
                if (chests != null && chests.TryAddChest(id, progress.CurrentArenaIndex) >= 0) result.grantedChestIds.Add(id);
                else result.droppedChestIds.Add(id);
            }

            result.applied = true;
            GameEventBus.Publish(new RewardAppliedEvent { source = source });
            if (persist && save != null) save.SaveNow();
            return result;
        }
    }
}
