using TMPro;
using TreeGuardians.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.UI.Menu
{
    public sealed class ArenaRowView : MonoBehaviour
    {
        [SerializeField] Image badge;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text trophiesText;
        [SerializeField] GameObject currentMarker;
        [SerializeField] GameObject lockedOverlay;

        public void Bind(Sprite sprite, string nameKey, int unlock, bool current, bool locked)
        {
            if (badge != null) badge.sprite = sprite;
            if (nameText != null) nameText.text = LocalizationService.Tr(nameKey);
            if (trophiesText != null) trophiesText.text = unlock.ToString();
            if (currentMarker != null) currentMarker.SetActive(current);
            if (lockedOverlay != null) lockedOverlay.SetActive(locked);
            gameObject.SetActive(true);
        }
    }
}
