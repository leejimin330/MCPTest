using FarmCreature.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FarmCreature.Presentation
{
    /// <summary>
    /// 헛간 — 보유한 케어 목록. 여기서 파티에 내보내고, 육성 대상을 고른다.
    ///
    /// 줄 수는 <see cref="Roster.MaxMembers"/> 로 고정되어 씬 생성 시점에 만들어진다.
    /// 버튼이 persistent listener 로 <b>보유 목록 인덱스</b>를 들고 있기 때문에,
    /// <see cref="Roster"/> 는 보유 순서를 절대 바꾸지 않는다.
    /// </summary>
    public class BarnPanel : GamePanel
    {
        public Text titleText;

        [Tooltip("Roster.MaxMembers 개. GameBootstrap 이 채운다.")]
        public Text[] rowTexts;

        [Tooltip("각 줄의 파티 토글 버튼 라벨. rowTexts 와 길이가 같아야 한다.")]
        public Text[] partyButtonLabels;

        [Tooltip("빈 줄의 버튼은 숨긴다. rowTexts 와 길이가 같아야 한다.")]
        public GameObject[] rowButtonGroups;

        public override void Refresh()
        {
            var roster = state != null ? state.Roster : null;

            if (titleText != null)
            {
                titleText.text = PanelLabels.BarnTitle(roster);
            }

            if (rowTexts == null)
            {
                return;
            }

            int count = roster != null ? roster.Count : 0;

            for (int i = 0; i < rowTexts.Length; i++)
            {
                var creature = roster != null ? roster.GetMember(i) : null;
                bool used = i < count && creature != null;

                if (rowTexts[i] != null)
                {
                    rowTexts[i].text = used
                        ? PanelLabels.BarnRow(creature, roster.IsInParty(creature), creature == state.Target)
                        : string.Empty;
                }

                // 비어 있는 줄의 버튼을 눌러도 아무 일이 없지만, 보이면 누를 수 있는 것처럼 읽힌다.
                if (rowButtonGroups != null && i < rowButtonGroups.Length && rowButtonGroups[i] != null)
                {
                    rowButtonGroups[i].SetActive(used);
                }

                if (used && partyButtonLabels != null && i < partyButtonLabels.Length
                    && partyButtonLabels[i] != null)
                {
                    partyButtonLabels[i].text = PanelLabels.PartyButton(roster.IsInParty(creature));
                }
            }
        }
    }
}
