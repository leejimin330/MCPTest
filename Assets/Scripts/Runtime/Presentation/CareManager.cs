using System.Collections.Generic;
using FarmCreature.Core;
using FarmCreature.Data;
using UnityEngine;

namespace FarmCreature.Presentation
{
    /// <summary>
    /// 패널 버튼 → 액션 실행 → 진화 평가 → 결과 전파를 조율한다.
    /// 액션 성공 직후에만 진화를 판정한다 (설계 3.2).
    ///
    /// 육성·장착은 전부 <see cref="GameState.Target"/> 한 마리에게만 적용된다.
    /// 대상은 헛간에서 고른다.
    /// </summary>
    public class CareManager : MonoBehaviour
    {
        [Tooltip("아이템 버튼이 인덱스로 아이템을 찾을 때 쓴다. GameBootstrap 이 채운다.")]
        public ItemDatabase itemDatabase;

        [Tooltip("액션 후 다시 그릴 패널들. GameBootstrap 이 채운다.")]
        public List<GamePanel> panels = new List<GamePanel>();

        [Tooltip("헛간의 [육성] 버튼이 여는 패널.")]
        public RaisingPanel raisingPanel;

        [Tooltip("파티원을 필드에 세우는 담당. GameBootstrap 이 채운다.")]
        public PartyField partyField;

        private GameState state;
        private GameHud hud;

        public GameState State
        {
            get { return state; }
        }

        public void Bind(GameState gameState, GameHud gameHud)
        {
            state = gameState;
            hud = gameHud;

            for (int i = 0; i < panels.Count; i++)
            {
                if (panels[i] != null)
                {
                    panels[i].Bind(state, itemDatabase);
                }
            }

            if (partyField != null)
            {
                partyField.Bind(state, this);
            }

            RefreshAll();
        }

        // ---------- 육성 ----------

        public void DoFeed()
        {
            Apply(CareActions.Feed(state != null ? state.Target : null));
        }

        /// <summary>uGUI Button 이 int 만 넘길 수 있어 인덱스로 받는다 (0=공격, 1=방어, 2=체력).</summary>
        public void DoTrain(int statIndex)
        {
            Apply(CareActions.Train(state != null ? state.Target : null, (StatType)statIndex));
        }

        // ---------- 장착 ----------

        /// <summary>
        /// uGUI 의 persistent listener 는 int / Object 인자만 안정적으로 직렬화되는데,
        /// Object 인자 등록(AddObjectPersistentListener)이 빈 항목을 남기는 문제가 있어
        /// <see cref="itemDatabase"/> 인덱스로 받는다.
        /// </summary>
        public void DoEquip(int itemIndex)
        {
            if (itemDatabase == null || itemIndex < 0 || itemIndex >= itemDatabase.items.Count)
            {
                SetLog("아이템을 찾을 수 없습니다.");
                return;
            }

            Apply(CareActions.Equip(
                state != null ? state.Target : null,
                state != null ? state.Inventory : null,
                itemDatabase.items[itemIndex]));
        }

        public void DoUnequip()
        {
            Apply(CareActions.Unequip(
                state != null ? state.Target : null,
                state != null ? state.Inventory : null));
        }

        // ---------- 헛간 / 파티 ----------

        /// <summary>헛간 한 줄의 파티 토글. 인자는 보유 목록 인덱스다.</summary>
        public void DoTogglePartyRow(int memberIndex)
        {
            if (state == null)
            {
                return;
            }

            var creature = state.Roster.GetMember(memberIndex);
            if (creature == null)
            {
                SetLog("그 자리에는 케어가 없다.");
                return;
            }

            var result = state.Roster.IsInParty(creature)
                ? state.Roster.LeaveParty(creature)
                : state.Roster.JoinParty(creature);

            ApplyRosterChange(result);
        }

        /// <summary>헛간 한 줄의 [육성] 버튼. 대상을 바꾸고 육성 패널을 연다.</summary>
        public void DoRaiseRow(int memberIndex)
        {
            if (state == null)
            {
                return;
            }

            var creature = state.Roster.GetMember(memberIndex);
            if (creature == null || !state.SetTarget(creature))
            {
                SetLog("그 자리에는 케어가 없다.");
                return;
            }

            SetLog(PanelLabels.CreatureName(creature) + "을(를) 돌보기로 했다.");
            RefreshAll();

            if (raisingPanel != null)
            {
                raisingPanel.Open();
            }
        }

        /// <summary>필드의 케어를 클릭했을 때 — 헛간으로 돌려보낸다.</summary>
        public void ReturnToBarn(CreatureInstance creature)
        {
            if (state == null)
            {
                return;
            }

            ApplyRosterChange(state.Roster.LeaveParty(creature));
        }

        private void ApplyRosterChange(ActionResult result)
        {
            if (result.Success && partyField != null)
            {
                partyField.Rebuild();
            }

            RefreshAll();
            SetLog(result.Message);
        }

        // ---------- 저장 ----------

        public void DoSave()
        {
            string error;
            bool ok = SaveSystem.Save(state, out error);
            SetLog(ok ? "저장했다." : "저장 실패: " + error);
        }

        // ---------- 공통 ----------

        private void Apply(ActionResult result)
        {
            if (!result.Success)
            {
                // 실패해도 다시 그린다 — 거부 사유(예: 포만도 부족)를 패널이 안내하고 있어서
                // 갱신하지 않으면 로그와 패널이 서로 다른 말을 하게 된다.
                RefreshAll();
                SetLog(result.Message);
                return;
            }

            string message = result.Message;
            var target = state.Target;

            var evolved = EvolutionEvaluator.TryEvolve(target, state.Inventory);
            if (evolved != null)
            {
                string before = PanelLabels.CreatureName(target);
                target.CurrentSpecies = evolved;

                // 필드에 나와 있으면 겉모습(색·크기)도 바로 바뀌어야 한다.
                if (partyField != null)
                {
                    partyField.RefreshVisuals();
                }

                message = before + "이(가) " + evolved.displayName + "(으)로 진화했다!";
            }

            RefreshAll();
            SetLog(message);
        }

        /// <summary>HUD 와 열려 있는 패널을 전부 다시 그린다.</summary>
        public void RefreshAll()
        {
            if (hud != null)
            {
                hud.Refresh();
            }

            for (int i = 0; i < panels.Count; i++)
            {
                if (panels[i] != null && panels[i].IsOpen)
                {
                    panels[i].Refresh();
                }
            }
        }

        public void SetLog(string message)
        {
            if (hud != null)
            {
                hud.SetLog(message);
            }
        }
    }
}
