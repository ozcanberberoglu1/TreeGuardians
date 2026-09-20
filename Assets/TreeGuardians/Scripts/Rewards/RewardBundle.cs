using System;
using System.Collections.Generic;

namespace TreeGuardians.Rewards
{
    [Serializable]
    public struct CardReward
    {
        public string guardianId;
        public int count;

        public CardReward(string guardianId, int count)
        {
            this.guardianId = guardianId;
            this.count = count;
        }
    }

    [Serializable]
    public sealed class RewardBundle
    {
        public int coins;
        public int sap;
        public int gems;
        public int trophies;
        public List<CardReward> cards = new List<CardReward>();
        public List<string> chestIds = new List<string>();
        public List<string> unlockGuardianIds = new List<string>();
        public List<string> unlockToolIds = new List<string>();

        [NonSerialized] public bool consumed;

        public bool IsEmpty =>
            coins == 0 && sap == 0 && gems == 0 && trophies == 0 &&
            cards.Count == 0 && chestIds.Count == 0 && unlockGuardianIds.Count == 0 && unlockToolIds.Count == 0;

        public void Add(RewardBundle other)
        {
            if (other == null) return;
            coins += other.coins;
            sap += other.sap;
            gems += other.gems;
            trophies += other.trophies;
            for (int i = 0; i < other.cards.Count; i++) AddCards(other.cards[i].guardianId, other.cards[i].count);
            chestIds.AddRange(other.chestIds);
            unlockGuardianIds.AddRange(other.unlockGuardianIds);
            unlockToolIds.AddRange(other.unlockToolIds);
        }

        public void AddCards(string guardianId, int count)
        {
            if (string.IsNullOrEmpty(guardianId) || count <= 0) return;
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i].guardianId == guardianId)
                {
                    cards[i] = new CardReward(guardianId, cards[i].count + count);
                    return;
                }
            }
            cards.Add(new CardReward(guardianId, count));
        }

        public RewardBundle Clone()
        {
            var b = new RewardBundle { coins = coins, sap = sap, gems = gems, trophies = trophies };
            b.cards.AddRange(cards);
            b.chestIds.AddRange(chestIds);
            b.unlockGuardianIds.AddRange(unlockGuardianIds);
            b.unlockToolIds.AddRange(unlockToolIds);
            return b;
        }

        public void Clear()
        {
            coins = sap = gems = trophies = 0;
            cards.Clear();
            chestIds.Clear();
            unlockGuardianIds.Clear();
            unlockToolIds.Clear();
        }
    }
}
