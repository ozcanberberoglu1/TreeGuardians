using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.UI.Popups
{
    public sealed class ConnectionInfoPopup : UIPopup
    {
        [SerializeField] Button okButton;

        protected override void Awake()
        {
            base.Awake();
            if (okButton != null) okButton.onClick.AddListener(Close);
        }

        public void Show() => Open();
    }
}
