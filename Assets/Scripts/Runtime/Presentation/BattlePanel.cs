using FarmCreature.Core;
using FarmCreature.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FarmCreature.Presentation
{
    /// <summary>
    /// 전투터 — 어디로 나갈지 고르는 곳이다. <b>전투 자체는 여기서 굴리지 않는다</b>:
    /// 인카운터를 골라 <see cref="BattleHandoff"/> 에 실은 뒤 전투 씬을 연다 (설계 3절).
    ///
    /// 이전 판에서는 이 패널이 <see cref="BattleSimulator"/> 를 직접 굴리고 재화까지 지급했다.
    /// 그 역할은 각각 <see cref="BattleSceneRoot"/> 와 <see cref="GameRoot"/> 로 옮겼다.
    /// </summary>
    public class BattlePanel : GamePanel
    {
        public const string BattleSceneName = "BattleScene";

        public Text titleText;
        public Text infoText;

        [Tooltip("버튼 개수가 이 목록에서 나온다. GameBootstrap 이 채운다.")]
        public BattleEncounterDatabase encounterDatabase;

        [Tooltip("안내 로그를 맡길 조율자. GameBootstrap 이 채운다.")]
        public CareManager careManager;

        /// <summary>
        /// uGUI 버튼이 <see cref="BattleEncounterDatabase"/> 인덱스를 넘긴다 (함정 3.6).
        ///
        /// 파티가 비었으면 <b>씬을 바꾸기 전에</b> 막는다. 예전에는 시뮬레이터가 시작하자마자
        /// 패배로 끝내도록 뒀지만, 이제는 그 대가가 씬 전환 왕복이라 그냥 두면 안 된다.
        /// </summary>
        public void StartEncounter(int encounterIndex)
        {
            if (state == null || encounterDatabase == null)
            {
                Log("전투터를 열 수 없다.");
                return;
            }

            var encounter = encounterDatabase.Get(encounterIndex);
            if (encounter == null)
            {
                Log("갈 곳을 찾을 수 없다.");
                return;
            }

            if (state.Roster.Party.Count == 0)
            {
                Log("밖에 나와 있는 케어가 없다. 헛간에서 먼저 내보내자.");
                return;
            }

            BattleHandoff.SetRequest(state, encounter);
            SceneManager.LoadScene(BattleSceneName);
        }

        public override void Refresh()
        {
            if (titleText != null)
            {
                titleText.text = "전투터";
            }

            if (infoText != null)
            {
                infoText.text = PanelLabels.BattleReadyLine(state);
            }
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
