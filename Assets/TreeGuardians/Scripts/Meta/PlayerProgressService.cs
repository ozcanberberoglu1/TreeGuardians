using System;
using System.Collections.Generic;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Economy;
using TreeGuardians.Save;
using UnityEngine;

namespace TreeGuardians.Meta
{
    /// Domain API over PlayerSaveData: guardians, tools, tree, arena, loadout. UI never edits save data directly.
    public sealed class PlayerProgressService : MonoBehaviour
    {
        public PlayerSaveData Data { get; private set; }
        public CurrencyWallet Wallet { get; private set; }
        public GameBalanceConfig Balance { get; private set; }
        public GameDatabase Database { get; private set; }

        SaveService save;
        bool boundToSave;

        public event Action OnLoadoutChanged;
        public event Action OnToolsChanged;
        public event Action OnTreeChanged;
        public event Action<string> OnGuardianChanged;

        void Awake()
        {
            Services.Register(this);
        }

        void OnDestroy()
        {
            Services.Unregister(this);
            if (save != null && boundToSave) save.OnLoaded -= HandleSaveLoaded;
        }

        public void Initialize(SaveService saveService, GameConfigProvider config)
        {
            save = saveService;
            Balance = config.Balance;
            Database = config.Database;
            if (!boundToSave)
            {
                save.OnLoaded += HandleSaveLoaded;
                boundToSave = true;
            }
            Bind(save.Data, save.IsNewSave);
        }

        void HandleSaveLoaded() => Bind(save.Data, save.IsNewSave);

        /// Test/tooling entry point: bind without SaveService or scene services.
        public void InitializeDirect(GameBalanceConfig balance, GameDatabase database, PlayerSaveData data, bool isNew)
        {
            Balance = balance;
            Database = database;
            Bind(data, isNew);
        }

        public void Bind(PlayerSaveData data, bool isNew)
        {
            Data = data;
            Wallet = new CurrencyWallet(data);
            if (isNew) ApplyNewSaveDefaults();
            EnsureIntegrity();
            RefreshArena();
        }

        void ApplyNewSaveDefaults()
        {
            Data.coins = Balance.startingCoins;
            Data.sap = Balance.startingSap;
            Data.gems = Balance.startingGems;
            Data.trophies = Balance.startingTrophies;
            Data.bestTrophies = Data.trophies;
            Data.lastDailyQuestResetUtcTicks = DateTime.UtcNow.Ticks;

            int slot = 0;
            for (int i = 0; i < Balance.starterGuardianIds.Length; i++)
            {
                var id = Balance.starterGuardianIds[i];
                if (Database.GetGuardian(id) == null) continue;
                var s = GetGuardianState(id);
                s.unlocked = true;
                s.isNew = false;
                if (slot < Data.equippedGuardianIds.Length) Data.equippedGuardianIds[slot++] = id;
            }

            int toolSlot = 0;
            for (int i = 0; i < Balance.starterToolIds.Length; i++)
            {
                var id = Balance.starterToolIds[i];
                if (Database.GetTool(id) == null) continue;
                GetToolState(id).unlocked = true;
                if (toolSlot < Data.equippedToolIds.Length) Data.equippedToolIds[toolSlot++] = id;
            }
        }

        public void EnsureIntegrity()
        {
            if (Data.equippedGuardianIds == null || Data.equippedGuardianIds.Length != Balance.guardianSlotCount)
            {
                var arr = new string[Balance.guardianSlotCount];
                if (Data.equippedGuardianIds != null)
                    Array.Copy(Data.equippedGuardianIds, arr, Mathf.Min(arr.Length, Data.equippedGuardianIds.Length));
                Data.equippedGuardianIds = arr;
            }
            if (Data.equippedToolIds == null || Data.equippedToolIds.Length != Balance.toolSlotCount)
            {
                var arr = new string[Balance.toolSlotCount];
                if (Data.equippedToolIds != null)
                    Array.Copy(Data.equippedToolIds, arr, Mathf.Min(arr.Length, Data.equippedToolIds.Length));
                Data.equippedToolIds = arr;
            }
            if (Data.tree == null) Data.tree = new TreeSaveData();
            if (Data.tree.upgradeLevels == null || Data.tree.upgradeLevels.Length != 6) Data.tree.upgradeLevels = new int[6];

            for (int i = 0; i < Database.guardians.Count; i++)
                if (Database.guardians[i] != null) GetGuardianState(Database.guardians[i].id);
            for (int i = 0; i < Database.tools.Count; i++)
                if (Database.tools[i] != null) GetToolState(Database.tools[i].id);

            var seen = new HashSet<string>();
            for (int i = 0; i < Data.equippedGuardianIds.Length; i++)
            {
                var id = Data.equippedGuardianIds[i];
                if (string.IsNullOrEmpty(id)) continue;
                if (!IsGuardianUnlocked(id) || Database.GetGuardian(id) == null || !seen.Add(id))
                    Data.equippedGuardianIds[i] = "";
            }
            seen.Clear();
            for (int i = 0; i < Data.equippedToolIds.Length; i++)
            {
                var id = Data.equippedToolIds[i];
                if (string.IsNullOrEmpty(id)) continue;
                if (!IsToolUnlocked(id) || Database.GetTool(id) == null || !seen.Add(id))
                    Data.equippedToolIds[i] = "";
            }

            for (int i = 0; i < Data.guardians.Count; i++)
                Data.guardians[i].level = Mathf.Clamp(Data.guardians[i].level, 1, Balance.guardianMaxLevel);
            for (int i = 0; i < Data.tree.upgradeLevels.Length; i++)
                Data.tree.upgradeLevels[i] = Mathf.Clamp(Data.tree.upgradeLevels[i], 0, Balance.treeUpgradeMaxLevel);
        }

        // ---------------- Guardians ----------------

        public GuardianSaveData GetGuardianState(string id)
        {
            for (int i = 0; i < Data.guardians.Count; i++)
                if (Data.guardians[i].id == id) return Data.guardians[i];
            var s = new GuardianSaveData { id = id, level = 1 };
            Data.guardians.Add(s);
            return s;
        }

        public bool IsGuardianUnlocked(string id) => !string.IsNullOrEmpty(id) && GetGuardianState(id).unlocked;
        public int GetGuardianLevel(string id) => GetGuardianState(id).level;
        public int GetGuardianShards(string id) => GetGuardianState(id).shards;

        public bool UnlockGuardian(string id)
        {
            if (Database.GetGuardian(id) == null) return false;
            var s = GetGuardianState(id);
            if (s.unlocked) return false;
            s.unlocked = true;
            s.isNew = true;
            GameEventBus.Publish(new GuardianUnlockedEvent { guardianId = id });
            OnGuardianChanged?.Invoke(id);
            return true;
        }

        public bool AddShards(string id, int count)
        {
            var def = Database.GetGuardian(id);
            if (def == null || count <= 0) return false;
            var s = GetGuardianState(id);
            s.shards += count;
            bool unlockedNow = false;
            if (!s.unlocked)
            {
                int need = Mathf.Max(1, Balance.GetUpgradeCost(def.rarity).cardsToUnlock);
                if (s.shards >= need)
                {
                    s.shards -= need;
                    s.unlocked = true;
                    s.isNew = true;
                    unlockedNow = true;
                    GameEventBus.Publish(new GuardianUnlockedEvent { guardianId = id });
                }
            }
            GameEventBus.Publish(new GuardianCardsChangedEvent { guardianId = id, shards = s.shards });
            OnGuardianChanged?.Invoke(id);
            return unlockedNow;
        }

        public bool CanUpgradeGuardian(string id, out int cardsNeeded, out int coinsNeeded, out string reasonKey)
        {
            cardsNeeded = 0;
            coinsNeeded = 0;
            reasonKey = "";
            var def = Database.GetGuardian(id);
            if (def == null) { reasonKey = "error_unknown"; return false; }
            var s = GetGuardianState(id);
            if (!s.unlocked) { reasonKey = "guardian_locked"; return false; }
            if (s.level >= Balance.guardianMaxLevel) { reasonKey = "guardian_max_level"; return false; }
            cardsNeeded = Balance.GetCardsRequired(def.rarity, s.level);
            coinsNeeded = Balance.GetCoinsRequired(def.rarity, s.level);
            if (s.shards < cardsNeeded) { reasonKey = "insufficient_cards"; return false; }
            if (!Wallet.CanAfford(CurrencyType.Coins, coinsNeeded)) { reasonKey = "insufficient_coins"; return false; }
            return true;
        }

        public bool TryUpgradeGuardian(string id)
        {
            if (!CanUpgradeGuardian(id, out int cards, out int coins, out _)) return false;
            var s = GetGuardianState(id);
            if (!Wallet.TrySpend(CurrencyType.Coins, coins)) return false;
            s.shards -= cards;
            s.level++;
            Data.stats.guardianUpgrades++;
            GameEventBus.Publish(new GuardianUpgradedEvent { guardianId = id, newLevel = s.level });
            OnGuardianChanged?.Invoke(id);
            save?.SaveNow();
            return true;
        }

        public int FindEquippedSlot(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < Data.equippedGuardianIds.Length; i++)
                if (Data.equippedGuardianIds[i] == id) return i;
            return -1;
        }

        public string GetEquippedGuardianId(int slot) =>
            slot >= 0 && slot < Data.equippedGuardianIds.Length ? Data.equippedGuardianIds[slot] : "";

        public int EquippedGuardianCount
        {
            get
            {
                int c = 0;
                for (int i = 0; i < Data.equippedGuardianIds.Length; i++)
                    if (!string.IsNullOrEmpty(Data.equippedGuardianIds[i])) c++;
                return c;
            }
        }

        public int FirstFreeGuardianSlot()
        {
            for (int i = 0; i < Data.equippedGuardianIds.Length; i++)
                if (string.IsNullOrEmpty(Data.equippedGuardianIds[i])) return i;
            return -1;
        }

        public bool EquipGuardian(string id, int slot)
        {
            if (!IsGuardianUnlocked(id)) return false;
            if (slot < 0 || slot >= Data.equippedGuardianIds.Length) return false;
            int existing = FindEquippedSlot(id);
            if (existing == slot) return true;
            if (existing >= 0) Data.equippedGuardianIds[existing] = Data.equippedGuardianIds[slot];
            Data.equippedGuardianIds[slot] = id;
            NotifyLoadout();
            return true;
        }

        public bool UnequipGuardian(int slot)
        {
            if (slot < 0 || slot >= Data.equippedGuardianIds.Length) return false;
            if (string.IsNullOrEmpty(Data.equippedGuardianIds[slot])) return false;
            Data.equippedGuardianIds[slot] = "";
            NotifyLoadout();
            return true;
        }

        public bool SwapEquipped(int a, int b)
        {
            if (a == b) return false;
            if (a < 0 || b < 0 || a >= Data.equippedGuardianIds.Length || b >= Data.equippedGuardianIds.Length) return false;
            (Data.equippedGuardianIds[a], Data.equippedGuardianIds[b]) = (Data.equippedGuardianIds[b], Data.equippedGuardianIds[a]);
            NotifyLoadout();
            return true;
        }

        void NotifyLoadout()
        {
            OnLoadoutChanged?.Invoke();
            GameEventBus.Publish(new LoadoutChangedEvent());
            save?.SaveNow();
        }

        public void ClearNewFlag(string id)
        {
            var s = GetGuardianState(id);
            if (s.isNew) { s.isNew = false; OnGuardianChanged?.Invoke(id); }
        }

        public int GetGuardianPower(string id)
        {
            var def = Database.GetGuardian(id);
            return def == null ? 0 : def.GetPower(GetGuardianLevel(id), Balance);
        }

        public int GetTreePower()
        {
            int p = 0;
            for (int i = 0; i < Data.equippedGuardianIds.Length; i++)
                p += GetGuardianPower(Data.equippedGuardianIds[i]);
            int treeLevels = 0;
            for (int i = 0; i < Data.tree.upgradeLevels.Length; i++) treeLevels += Data.tree.upgradeLevels[i];
            return p + treeLevels * 15;
        }

        // ---------------- Tools ----------------

        public ToolSaveData GetToolState(string id)
        {
            for (int i = 0; i < Data.tools.Count; i++)
                if (Data.tools[i].id == id) return Data.tools[i];
            var s = new ToolSaveData { id = id, level = 1 };
            Data.tools.Add(s);
            return s;
        }

        public bool IsToolUnlocked(string id) => !string.IsNullOrEmpty(id) && GetToolState(id).unlocked;
        public int GetToolLevel(string id) => GetToolState(id).level;

        public bool UnlockTool(string id)
        {
            if (Database.GetTool(id) == null) return false;
            var s = GetToolState(id);
            if (s.unlocked) return false;
            s.unlocked = true;
            OnToolsChanged?.Invoke();
            GameEventBus.Publish(new ToolsChangedEvent());
            return true;
        }

        public bool CanUpgradeTool(string id, out int sapCost, out string reasonKey)
        {
            sapCost = 0;
            reasonKey = "";
            var def = Database.GetTool(id);
            if (def == null) { reasonKey = "error_unknown"; return false; }
            var s = GetToolState(id);
            if (!s.unlocked) { reasonKey = "tool_locked"; return false; }
            if (s.level >= def.maxLevel) { reasonKey = "tool_max_level"; return false; }
            sapCost = def.GetUpgradeSapCost(s.level);
            if (!Wallet.CanAfford(CurrencyType.Sap, sapCost)) { reasonKey = "insufficient_sap"; return false; }
            return true;
        }

        public bool TryUpgradeTool(string id)
        {
            if (!CanUpgradeTool(id, out int sap, out _)) return false;
            if (!Wallet.TrySpend(CurrencyType.Sap, sap)) return false;
            GetToolState(id).level++;
            OnToolsChanged?.Invoke();
            GameEventBus.Publish(new ToolsChangedEvent());
            save?.SaveNow();
            return true;
        }

        public string GetEquippedToolId(int slot) =>
            slot >= 0 && slot < Data.equippedToolIds.Length ? Data.equippedToolIds[slot] : "";

        public int FindEquippedToolSlot(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < Data.equippedToolIds.Length; i++)
                if (Data.equippedToolIds[i] == id) return i;
            return -1;
        }

        public bool EquipTool(string id, int slot)
        {
            if (!IsToolUnlocked(id)) return false;
            if (slot < 0 || slot >= Data.equippedToolIds.Length) return false;
            int existing = FindEquippedToolSlot(id);
            if (existing == slot) return true;
            if (existing >= 0) Data.equippedToolIds[existing] = Data.equippedToolIds[slot];
            Data.equippedToolIds[slot] = id;
            OnToolsChanged?.Invoke();
            GameEventBus.Publish(new ToolsChangedEvent());
            save?.SaveNow();
            return true;
        }

        public bool UnequipTool(int slot)
        {
            if (slot < 0 || slot >= Data.equippedToolIds.Length) return false;
            if (string.IsNullOrEmpty(Data.equippedToolIds[slot])) return false;
            Data.equippedToolIds[slot] = "";
            OnToolsChanged?.Invoke();
            GameEventBus.Publish(new ToolsChangedEvent());
            save?.SaveNow();
            return true;
        }

        // ---------------- Tree ----------------

        public int GetTreeUpgradeLevel(TreeUpgradePath path) => Data.tree.upgradeLevels[(int)path];

        public int GetTreeUpgradeCost(TreeUpgradePath path) => Balance.GetTreeUpgradeSapCost(GetTreeUpgradeLevel(path));

        public bool CanUpgradeTree(TreeUpgradePath path, out int sapCost, out string reasonKey)
        {
            reasonKey = "";
            sapCost = GetTreeUpgradeCost(path);
            if (GetTreeUpgradeLevel(path) >= Balance.treeUpgradeMaxLevel) { reasonKey = "tree_max_level"; return false; }
            if (!Wallet.CanAfford(CurrencyType.Sap, sapCost)) { reasonKey = "insufficient_sap"; return false; }
            return true;
        }

        public bool TryUpgradeTree(TreeUpgradePath path)
        {
            if (!CanUpgradeTree(path, out int sap, out _)) return false;
            if (!Wallet.TrySpend(CurrencyType.Sap, sap)) return false;
            Data.tree.upgradeLevels[(int)path]++;
            OnTreeChanged?.Invoke();
            GameEventBus.Publish(new TreeUpgradedEvent { path = path, newLevel = Data.tree.upgradeLevels[(int)path] });
            save?.SaveNow();
            return true;
        }

        public int TotalTreeLevel
        {
            get
            {
                int t = 0;
                for (int i = 0; i < Data.tree.upgradeLevels.Length; i++) t += Data.tree.upgradeLevels[i];
                return t;
            }
        }

        public TreeVisualTier GetTreeVisualTier()
        {
            int t = TotalTreeLevel;
            if (t >= Balance.treeTierAncientThreshold) return TreeVisualTier.Ancient;
            if (t >= Balance.treeTierStrongThreshold) return TreeVisualTier.Strong;
            return TreeVisualTier.Sprouting;
        }

        // ---------------- Arena ----------------

        public int CurrentArenaIndex => Data.currentArenaIndex;
        public ArenaDefinition CurrentArena => Database.GetArenaByIndex(Data.currentArenaIndex);

        public bool RefreshArena()
        {
            var a = Database.GetHighestUnlockedArena(Data.trophies);
            if (a == null) return false;
            bool changed = a.arenaIndex != Data.currentArenaIndex;
            Data.currentArenaIndex = a.arenaIndex;
            if (a.arenaIndex > Data.highestArenaIndex) Data.highestArenaIndex = a.arenaIndex;
            if (changed) GameEventBus.Publish(new ArenaChangedEvent { arenaIndex = a.arenaIndex });
            return changed;
        }

        public void Save() => save?.SaveNow();
    }
}
