using FarmCreature.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FarmCreature.Presentation
{
    /// <summary>
    /// 헛간 안 화면. 왼쪽은 <b>헛간지기와 지금 나가 있는 파티</b>, 오른쪽은 <b>보관함 격자</b>다.
    ///
    /// <b><see cref="GamePanel"/> 인데 팝업이 아니다.</b> 이 화면은 씬의 본체라 <b>열린 채로 저장</b>된다
    /// (함정 3.8 의 "패널은 닫아 저장한다" 는 팝업 규칙이다 — 여기서 그것을 따르면 빈 방이 뜬다).
    /// 그래도 <see cref="GamePanel"/> 을 쓰는 이유는 <see cref="CareManager"/> 가 액션 뒤에
    /// 다시 그려주는 대상이 <see cref="GamePanel"/> 목록이기 때문이다 — 새 배선을 만들 이유가 없다.
    ///
    /// <b>칸은 런타임에 만들지 않는다.</b> <see cref="Roster.MaxMembers"/> 개를 씬 생성 시점에
    /// 깔아두고 내용만 갈아끼운다 (함정 3.1).
    ///
    /// <b>선택은 여기 있고 규칙은 <see cref="CareManager"/> 에 있다.</b> 파티 토글·육성은
    /// 이미 있는 <c>DoTogglePartyRow</c> / <c>DoRaiseRow</c> 를 고른 칸 번호로 부를 뿐이다.
    /// 옛 헛간 패널은 줄마다 버튼이 인덱스를 들고 있었지만(함정 3.6), 격자는 칸을 먼저 고르고
    /// 조작은 아래 버튼으로 하므로 인덱스를 들고 있는 것은 <b>칸 버튼뿐</b>이다.
    /// </summary>
    public class BarnRoomPanel : GamePanel
    {
        /// <summary>격자 열 수. <see cref="Roster.MaxMembers"/> 를 나누어떨어지게 해야 한다.</summary>
        public const int Columns = 4;

        [Header("오른쪽 — 보관함")]
        public Text boxTitleText;

        [Tooltip("Roster.MaxMembers 개. GameBootstrap 이 채운다.")]
        public Text[] cellTexts;

        [Tooltip("고른 칸을 밝게 칠한다. cellTexts 와 길이가 같아야 한다.")]
        public Image[] cellBackgrounds;

        [Header("왼쪽 — 헛간지기와 파티")]
        public Image keeperPortrait;
        public Text keeperText;

        [Tooltip("Roster.MaxParty 개. GameBootstrap 이 채운다.")]
        public Text[] partySlotTexts;

        [Header("아래 — 고른 케어")]
        public Text detailText;

        [Tooltip("[내보내기] / [복귀] 로 글자가 바뀐다.")]
        public Text partyButtonLabel;

        [Tooltip("액션을 맡길 조율자. GameBootstrap 이 채운다.")]
        public CareManager careManager;

        [Header("칸 색")]
        public Color cellColor = new Color(0.2f, 0.19f, 0.24f, 0.95f);
        public Color emptyCellColor = new Color(0.14f, 0.14f, 0.16f, 0.8f);
        public Color selectedCellColor = new Color(0.46f, 0.38f, 0.22f, 1f);
        public Color partyCellColor = new Color(0.24f, 0.32f, 0.24f, 0.95f);

        /// <summary>고른 칸 = 보유 목록 인덱스. 비어 있는 칸은 고를 수 없다.</summary>
        private int selected;

        /// <summary>칸 버튼이 자기 번호를 넘긴다 (함정 3.6).</summary>
        public void SelectSlot(int index)
        {
            if (state == null)
            {
                return;
            }

            var creature = state.Roster.GetMember(index);
            if (creature == null)
            {
                Log("빈 칸이다.");
                return;
            }

            selected = index;

            // 고르는 것이 곧 육성 대상을 정하는 것이다 — 헛간이 원래 대상을 고르는 곳이고,
            // 이렇게 해야 상태 패널·창고가 같은 케어를 가리킨다.
            state.SetTarget(creature);

            Log(PanelLabels.CreatureName(creature) + "을(를) 골랐다.");
            RefreshEverything();
        }

        /// <summary>고른 케어를 내보내거나 불러들인다.</summary>
        public void TogglePartyOfSelected()
        {
            if (careManager != null)
            {
                careManager.DoTogglePartyRow(selected);
            }
        }

        /// <summary>고른 케어의 육성 패널을 연다.</summary>
        public void RaiseSelected()
        {
            if (careManager != null)
            {
                careManager.DoRaiseRow(selected);
            }
        }

        public override void Refresh()
        {
            var roster = state != null ? state.Roster : null;

            EnsureSelection(roster);

            if (boxTitleText != null)
            {
                boxTitleText.text = PanelLabels.BoxTitle(roster);
            }

            if (keeperText != null)
            {
                keeperText.text = PanelLabels.KeeperLine(roster);
            }

            RefreshPartySlots(roster);
            RefreshCells(roster);
            RefreshDetail(roster);
        }

        // ---------- 안쪽 ----------

        /// <summary>
        /// 고른 칸이 비었으면 첫 케어로 되돌린다. 케어를 부화시키거나 방출한 뒤에도
        /// 아래쪽 상세가 엉뚱한 칸을 가리키지 않게 한다.
        /// </summary>
        private void EnsureSelection(Roster roster)
        {
            int count = roster != null ? roster.Count : 0;

            if (selected < 0 || selected >= count)
            {
                selected = 0;
            }
        }

        private void RefreshPartySlots(Roster roster)
        {
            if (partySlotTexts == null)
            {
                return;
            }

            for (int i = 0; i < partySlotTexts.Length; i++)
            {
                if (partySlotTexts[i] != null)
                {
                    partySlotTexts[i].text = PanelLabels.PartySlotRow(roster, i);
                }
            }
        }

        private void RefreshCells(Roster roster)
        {
            if (cellTexts == null)
            {
                return;
            }

            for (int i = 0; i < cellTexts.Length; i++)
            {
                var creature = roster != null ? roster.GetMember(i) : null;
                bool inParty = creature != null && roster.IsInParty(creature);

                if (cellTexts[i] != null)
                {
                    cellTexts[i].text = PanelLabels.BoxCell(creature, inParty);
                }

                if (cellBackgrounds != null && i < cellBackgrounds.Length
                    && cellBackgrounds[i] != null)
                {
                    cellBackgrounds[i].color = CellColorFor(creature, inParty, i == selected);
                }
            }
        }

        /// <summary>고른 칸이 먼저다 — 나가 있는 케어를 골랐을 때 어느 칸을 골랐는지 보여야 한다.</summary>
        private Color CellColorFor(CreatureInstance creature, bool inParty, bool isSelected)
        {
            if (creature == null)
            {
                return emptyCellColor;
            }

            if (isSelected)
            {
                return selectedCellColor;
            }

            return inParty ? partyCellColor : cellColor;
        }

        private void RefreshDetail(Roster roster)
        {
            var creature = roster != null ? roster.GetMember(selected) : null;
            bool inParty = creature != null && roster.IsInParty(creature);

            if (detailText != null)
            {
                detailText.text = creature != null
                    ? PanelLabels.BarnRow(creature, inParty, creature == Target)
                    : "고른 케어가 없다.";
            }

            if (partyButtonLabel != null)
            {
                partyButtonLabel.text = PanelLabels.PartyButton(inParty);
            }
        }

        /// <summary>
        /// HUD 까지 함께 갱신한다. 대상이 바뀌면 왼쪽 상태 표시도 따라와야 한다.
        /// 조율자가 없으면(테스트·단독 Play) 자기만 다시 그린다.
        /// </summary>
        private void RefreshEverything()
        {
            if (careManager != null)
            {
                careManager.RefreshAll();
                return;
            }

            Refresh();
        }

        private void Log(string message)
        {
            if (careManager != null)
            {
                careManager.SetLog(message);
            }
        }
    }
}
