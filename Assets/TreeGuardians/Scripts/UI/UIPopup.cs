namespace TreeGuardians.UI
{
    /// Base for popups: dims the background while open and stays on top.
    public class UIPopup : UIPanel
    {
        protected override void OnOpen()
        {
            transform.SetAsLastSibling();
            PopupDimmer.Instance?.Retain();
        }

        protected override void OnClose()
        {
            PopupDimmer.Instance?.Release();
        }
    }
}
