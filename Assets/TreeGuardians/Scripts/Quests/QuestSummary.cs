namespace TreeGuardians.Quests
{
    /// Aggregate of one quest list: how many exist, how many reached their target (claimed or not), how many rewards wait to be claimed.
    public struct QuestSummary
    {
        public int total;
        public int completed;
        public int claimable;
    }
}
