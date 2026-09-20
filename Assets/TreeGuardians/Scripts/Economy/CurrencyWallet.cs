using System;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Save;
using UnityEngine;

namespace TreeGuardians.Economy
{
    public readonly struct Cost
    {
        public readonly CurrencyType type;
        public readonly int amount;

        public Cost(CurrencyType type, int amount)
        {
            this.type = type;
            this.amount = amount;
        }
    }

    /// Atomic wallet over PlayerSaveData. Balances never go negative.
    public sealed class CurrencyWallet
    {
        PlayerSaveData data;
        readonly int[] scratch = new int[4];

        public event Action<CurrencyType, int, int> OnChanged;

        public CurrencyWallet(PlayerSaveData data)
        {
            Bind(data);
        }

        public void Bind(PlayerSaveData d)
        {
            data = d;
        }

        public int Get(CurrencyType type)
        {
            if (data == null) return 0;
            switch (type)
            {
                case CurrencyType.Coins: return data.coins;
                case CurrencyType.Sap: return data.sap;
                case CurrencyType.Gems: return data.gems;
                case CurrencyType.Trophies: return data.trophies;
                default: return 0;
            }
        }

        void Set(CurrencyType type, int value)
        {
            value = Mathf.Max(0, value);
            switch (type)
            {
                case CurrencyType.Coins: data.coins = value; break;
                case CurrencyType.Sap: data.sap = value; break;
                case CurrencyType.Gems: data.gems = value; break;
                case CurrencyType.Trophies:
                    data.trophies = value;
                    if (value > data.bestTrophies) data.bestTrophies = value;
                    break;
            }
        }

        public bool CanAfford(CurrencyType type, int amount) => amount <= 0 || Get(type) >= amount;

        public bool CanAfford(Cost a, Cost b)
        {
            Array.Clear(scratch, 0, scratch.Length);
            scratch[(int)a.type] += Mathf.Max(0, a.amount);
            scratch[(int)b.type] += Mathf.Max(0, b.amount);
            for (int i = 0; i < 4; i++)
                if (scratch[i] > 0 && Get((CurrencyType)i) < scratch[i]) return false;
            return true;
        }

        public bool TrySpend(CurrencyType type, int amount)
        {
            if (data == null || amount < 0) return false;
            if (amount == 0) return true;
            int old = Get(type);
            if (old < amount) return false;
            Set(type, old - amount);
            Notify(type, old, old - amount);
            return true;
        }

        public bool TrySpend(Cost a, Cost b)
        {
            if (data == null) return false;
            if (!CanAfford(a, b)) return false;
            if (a.amount > 0) { int old = Get(a.type); Set(a.type, old - a.amount); Notify(a.type, old, old - a.amount); }
            if (b.amount > 0) { int old = Get(b.type); Set(b.type, old - b.amount); Notify(b.type, old, old - b.amount); }
            return true;
        }

        public void Add(CurrencyType type, int amount)
        {
            if (data == null || amount == 0) return;
            int old = Get(type);
            int next = Mathf.Max(0, old + amount);
            Set(type, next);
            Notify(type, old, next);
        }

        void Notify(CurrencyType type, int oldValue, int newValue)
        {
            OnChanged?.Invoke(type, oldValue, newValue);
            GameEventBus.Publish(new CurrencyChangedEvent { type = type, oldValue = oldValue, newValue = newValue });
        }
    }
}
