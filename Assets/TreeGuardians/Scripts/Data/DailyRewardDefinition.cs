using System.Collections.Generic;
using TreeGuardians.Rewards;
using UnityEngine;

namespace TreeGuardians.Data
{
    [CreateAssetMenu(menuName = "Tree Guardians/Quests/Daily Reward Definition", fileName = "DailyRewards")]
    public sealed class DailyRewardDefinition : ScriptableObject
    {
        [Tooltip("7 günlük döngü. Son gün alındıktan sonra 1. güne döner.")]
        public List<RewardBundle> days = new List<RewardBundle>();

        public RewardBundle GetDay(int dayIndex)
        {
            if (days == null || days.Count == 0) return new RewardBundle();
            return days[Mathf.Clamp(dayIndex, 0, days.Count - 1)];
        }

        public int CycleLength => days != null && days.Count > 0 ? days.Count : 1;
    }
}
