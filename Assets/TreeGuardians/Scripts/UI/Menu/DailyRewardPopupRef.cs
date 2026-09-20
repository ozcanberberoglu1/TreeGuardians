namespace TreeGuardians.UI.Menu
{
    /// Thin indirection so QuestsPanel can open the daily popup without a hard reference to the Popups namespace type.
    public abstract class DailyRewardPopupRef : UIPopup
    {
        public abstract void Show();
    }
}
