using TreeGuardians.Rewards;
using UnityEngine;

namespace TreeGuardians.Data
{
    [CreateAssetMenu(menuName = "Tree Guardians/Quests/Quest Definition", fileName = "Quest_New")]
    public sealed class QuestDefinition : ScriptableObject
    {
        public string id = "quest_new";
        public string titleKey = "quest_new_title";
        public string descriptionKey = "quest_new_desc";
        public Sprite icon;
        public QuestType type = QuestType.PlayBattles;
        [Tooltip("Hedef sayı (ör. 3 kabuk kır → 3).")] public int targetCount = 1;
        [Tooltip("Günlük görev mi (sıfırlanır) yoksa kalıcı mı.")] public bool isDaily;
        public RewardBundle reward = new RewardBundle();

        void OnValidate()
        {
            targetCount = Mathf.Max(1, targetCount);
            if (string.IsNullOrWhiteSpace(id)) Debug.LogWarning($"[QuestDefinition] {name} has an empty id.", this);
        }
    }
}
