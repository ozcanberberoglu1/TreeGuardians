using System.Collections.Generic;
using TMPro;
using TreeGuardians.Core;
using TreeGuardians.Localization;
using TreeGuardians.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.UI.Menu
{
    /// Arena path: every arena with its unlock threshold and the player's position.
    public sealed class RankingPanel : UIPanel
    {
        [SerializeField] TMP_Text trophiesText;
        [SerializeField] RectTransform listContent;
        [SerializeField] ArenaRowView rowTemplate;
        [SerializeField] Button closeButton;

        readonly List<ArenaRowView> rows = new List<ArenaRowView>(4);

        protected override void Awake()
        {
            base.Awake();
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
        }

        protected override void OnOpen()
        {
            var progress = Services.Get<PlayerProgressService>();
            if (progress == null) return;
            if (trophiesText != null) trophiesText.text = LocalizationService.Number(progress.Data.trophies);
            var arenas = progress.Database.arenas;
            int n = 0;
            for (int i = 0; i < arenas.Count; i++)
            {
                var a = arenas[i];
                if (a == null) continue;
                if (rowTemplate == null || listContent == null) break;
                while (rows.Count <= n) { var r = Instantiate(rowTemplate, listContent); r.name = "ArenaRow_" + rows.Count; rows.Add(r); }
                rows[n++].Bind(a.badge, a.nameKey, a.unlockTrophies, a.arenaIndex == progress.CurrentArenaIndex, a.unlockTrophies > progress.Data.trophies);
            }
            for (int i = n; i < rows.Count; i++) rows[i].gameObject.SetActive(false);
        }
    }
}
