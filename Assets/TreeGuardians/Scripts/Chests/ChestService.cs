using System;
using System.Collections.Generic;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Meta;
using TreeGuardians.Rewards;
using TreeGuardians.Save;
using UnityEngine;

namespace TreeGuardians.Chests
{
    /// Chest slots with a single active UTC timer that keeps ticking while the app is closed.
    public sealed class ChestService : MonoBehaviour
    {
        PlayerProgressService progress;
        SaveService save;
        GameBalanceConfig balance;
        GameDatabase db;
        System.Random rng;
        readonly List<GuardianDefinition> candidates = new List<GuardianDefinition>(16);

        public event Action OnChanged;

        public IReadOnlyList<ChestSlotSaveData> Slots => progress.Data.chestSlots;
        public int SlotCount => balance.chestSlotCount;

        void Awake()
        {
            Services.Register(this);
        }

        void OnDestroy()
        {
            Services.Unregister(this);
        }

        public void Initialize(PlayerProgressService progress, SaveService save, bool isNewSave)
        {
            this.progress = progress;
            this.save = save;
            balance = progress.Balance;
            db = progress.Database;
            rng = new System.Random(unchecked((int)DateTime.UtcNow.Ticks));
            EnsureSlots();
            if (isNewSave && !string.IsNullOrEmpty(balance.starterChestId))
                TryAddChest(balance.starterChestId, 0);
            ValidateTimers();
        }

        void EnsureSlots()
        {
            var slots = progress.Data.chestSlots;
            while (slots.Count < balance.chestSlotCount) slots.Add(new ChestSlotSaveData());
            while (slots.Count > balance.chestSlotCount) slots.RemoveAt(slots.Count - 1);
            for (int i = 0; i < slots.Count; i++)
                if (!slots[i].IsEmpty && db.GetChest(slots[i].chestId) == null) slots[i].Clear();
        }

        void ValidateTimers()
        {
            var slots = progress.Data.chestSlots;
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (s.IsEmpty || !s.unlocking) continue;
                var def = db.GetChest(s.chestId);
                if (def == null) { s.Clear(); continue; }
                long span = s.unlockEndUtcTicks - s.unlockStartUtcTicks;
                long expected = (long)(def.unlockSeconds * TimeSpan.TicksPerSecond);
                if (span < 0 || span > expected * 1.05f + TimeSpan.TicksPerSecond)
                    s.unlockEndUtcTicks = s.unlockStartUtcTicks + expected;
            }
        }

        public bool IsInitialized => progress != null && db != null;

        public ChestDefinition GetDefinition(int slot)
        {
            if (!IsInitialized) return null;
            var slots = progress.Data.chestSlots;
            if (slot < 0 || slot >= slots.Count || slots[slot].IsEmpty) return null;
            return db.GetChest(slots[slot].chestId);
        }

        public bool HasFreeSlot
        {
            get
            {
                var slots = progress.Data.chestSlots;
                for (int i = 0; i < slots.Count; i++) if (slots[i].IsEmpty) return true;
                return false;
            }
        }

        public bool IsAnyUnlocking
        {
            get
            {
                var slots = progress.Data.chestSlots;
                for (int i = 0; i < slots.Count; i++)
                    if (!slots[i].IsEmpty && slots[i].unlocking && !IsReady(i)) return true;
                return false;
            }
        }

        public int TryAddChest(string chestId, int arenaIndex)
        {
            if (db.GetChest(chestId) == null) return -1;
            var slots = progress.Data.chestSlots;
            for (int i = 0; i < slots.Count; i++)
            {
                if (!slots[i].IsEmpty) continue;
                slots[i].chestId = chestId;
                slots[i].unlocking = false;
                slots[i].unlockStartUtcTicks = 0;
                slots[i].unlockEndUtcTicks = 0;
                slots[i].arenaIndex = Mathf.Max(0, arenaIndex);
                Notify();
                return i;
            }
            return -1;
        }

        public bool CanStartUnlock(int slot)
        {
            var slots = progress.Data.chestSlots;
            if (slot < 0 || slot >= slots.Count || slots[slot].IsEmpty) return false;
            if (slots[slot].unlocking) return false;
            if (IsReady(slot)) return false;
            return !IsAnyUnlocking;
        }

        public bool StartUnlock(int slot)
        {
            if (!CanStartUnlock(slot)) return false;
            var def = GetDefinition(slot);
            if (def == null) return false;
            var s = progress.Data.chestSlots[slot];
            var now = save.GetUtcNow();
            s.unlocking = true;
            s.unlockStartUtcTicks = now.Ticks;
            s.unlockEndUtcTicks = now.AddSeconds(def.unlockSeconds).Ticks;
            save.SaveNow();
            Notify();
            return true;
        }

        public double GetRemainingSeconds(int slot)
        {
            var def = GetDefinition(slot);
            if (def == null) return 0;
            var s = progress.Data.chestSlots[slot];
            if (!s.unlocking) return def.unlockSeconds;
            var now = save.GetUtcNow();
            double rem = (new DateTime(s.unlockEndUtcTicks, DateTimeKind.Utc) - now).TotalSeconds;
            if (rem < 0) rem = 0;
            if (rem > def.unlockSeconds) rem = def.unlockSeconds;
            return rem;
        }

        public bool IsReady(int slot)
        {
            var def = GetDefinition(slot);
            if (def == null) return false;
            if (def.unlockSeconds <= 0f) return true;
            var s = progress.Data.chestSlots[slot];
            return s.unlocking && GetRemainingSeconds(slot) <= 0;
        }

        public int GetSkipGemCost(int slot)
        {
            var def = GetDefinition(slot);
            if (def == null) return 0;
            return def.GetSkipGemCost((float)GetRemainingSeconds(slot));
        }

        public bool TrySkipWithGems(int slot)
        {
            var def = GetDefinition(slot);
            if (def == null || IsReady(slot)) return false;
            int cost = GetSkipGemCost(slot);
            if (!progress.Wallet.TrySpend(CurrencyType.Gems, cost)) return false;
            var s = progress.Data.chestSlots[slot];
            var now = save.GetUtcNow();
            if (!s.unlocking) s.unlockStartUtcTicks = now.Ticks;
            s.unlocking = true;
            s.unlockEndUtcTicks = now.Ticks;
            save.SaveNow();
            Notify();
            return true;
        }

        public bool CanOpen(int slot) => IsReady(slot);

        public RewardBundle Open(int slot)
        {
            if (!CanOpen(slot)) return null;
            var def = GetDefinition(slot);
            var s = progress.Data.chestSlots[slot];
            int arenaIndex = s.arenaIndex;
            var bundle = RollContents(def, arenaIndex, rng);
            s.Clear();
            progress.Data.stats.chestsOpened++;
            GameEventBus.Publish(new ChestOpenedEvent { chestId = def.id });
            var rewards = Services.Get<RewardService>();
            if (rewards != null) rewards.Apply(bundle, "chest");
            else save.SaveNow();
            Notify();
            return bundle;
        }

        public RewardBundle RollContents(ChestDefinition def, int arenaIndex, System.Random r)
        {
            r ??= rng ?? new System.Random();
            float mult = 1f + def.arenaTierBonusPerIndex * Mathf.Max(0, arenaIndex);
            var bundle = new RewardBundle
            {
                coins = Mathf.RoundToInt(r.Next(def.coinsMin, def.coinsMax + 1) * mult),
                sap = Mathf.RoundToInt(r.Next(def.sapMin, def.sapMax + 1) * mult),
                gems = r.Next(def.gemsMin, def.gemsMax + 1)
            };

            bool epicGranted = false;
            bool forceEpic = def.guaranteeEpic || (def.epicPityThreshold > 0 && progress.Data.chestsOpenedSinceEpic + 1 >= def.epicPityThreshold);
            for (int i = 0; i < def.cardDraws; i++)
            {
                Rarity rarity = (forceEpic && i == 0) ? Rarity.Epic : RollRarity(def, r);
                var g = PickGuardian(rarity, r);
                if (g == null) continue;
                if (rarity >= Rarity.Epic) epicGranted = true;
                bundle.AddCards(g.id, CardsForRarity(rarity, r));
            }
            progress.Data.chestsOpenedSinceEpic = epicGranted ? 0 : progress.Data.chestsOpenedSinceEpic + 1;
            return bundle;
        }

        static int CardsForRarity(Rarity rarity, System.Random r)
        {
            switch (rarity)
            {
                case Rarity.Common: return r.Next(2, 5);
                case Rarity.Rare: return r.Next(1, 3);
                default: return 1;
            }
        }

        Rarity RollRarity(ChestDefinition def, System.Random r)
        {
            float total = 0f;
            for (int i = 0; i < 4; i++) total += def.GetRarityWeight((Rarity)i);
            if (total <= 0f) return Rarity.Common;
            float roll = (float)r.NextDouble() * total;
            for (int i = 0; i < 4; i++)
            {
                roll -= def.GetRarityWeight((Rarity)i);
                if (roll <= 0f) return (Rarity)i;
            }
            return Rarity.Common;
        }

        GuardianDefinition PickGuardian(Rarity rarity, System.Random r)
        {
            int highest = progress.Data.highestArenaIndex;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                candidates.Clear();
                var list = db.guardians;
                for (int i = 0; i < list.Count; i++)
                {
                    var g = list[i];
                    if (g == null || g.rarity != rarity) continue;
                    if (g.arenaUnlockIndex > highest) continue;
                    candidates.Add(g);
                }
                if (candidates.Count > 0) return candidates[r.Next(candidates.Count)];
                if (rarity == Rarity.Common) break;
                rarity = (Rarity)((int)rarity - 1);
            }
            return null;
        }

        void Notify()
        {
            OnChanged?.Invoke();
            GameEventBus.Publish(new ChestSlotsChangedEvent());
        }
    }
}
