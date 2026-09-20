using TreeGuardians.Rewards;
using UnityEngine;

namespace TreeGuardians.Data
{
    [CreateAssetMenu(menuName = "Tree Guardians/Quests/Achievement Definition", fileName = "Achievement_New")]
    public sealed class AchievementDefinition : ScriptableObject
    {
        public string id = "achievement_new";
        public string titleKey = "achievement_new_title";
        public string descriptionKey = "achievement_new_desc";
        public Sprite icon;
        public QuestType metric = QuestType.WinBattles;
        public int targetCount = 1;
        public RewardBundle reward = new RewardBundle();

        void OnValidate()
        {
            targetCount = Mathf.Max(1, targetCount);
        }
    }
}
