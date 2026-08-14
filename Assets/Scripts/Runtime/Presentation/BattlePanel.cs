using System.Text;
using FarmCreature.Core;
using FarmCreature.Data;
using UnityEngine;
using UnityEngine.UI;

namespace FarmCreature.Presentation
{
    /// <summary>
    /// 전투터 — 상대를 고르면 <b>지금 밖에 나와 있는 파티가 그대로</b> 출전해 자동전투한다.
    ///
    /// 규칙은 전부 <see cref="BattleSimulator"/> 안에 있고 이 클래스는 시간만 흘려보낸다.
    /// 그래서 전투 결과는 프레임률과 무관하게 재현된다.
    /// </summary>
    public class BattlePanel : GamePanel
    {
        [Tooltip("전투 로그에서 화면에 남길 줄 수.")]
        public int visibleLogLines = 8;

        public Text titleText;
        public Text rosterText;
        public Text logText;

        public EnemyDatabase enemyDatabase;

        /// <summary>보상 정산과 로그를 맡길 조율자. GameBootstrap 이 채운다.</summary>
        public CareManager careManager;

        private BattleSimulator battle;
        private readonly StringBuilder builder = new StringBuilder();

        /// <summary>전투가 끝난 뒤 보상을 한 번만 주기 위한 표시.</summary>
        private bool rewardGranted;

        public BattleSimulator Current
        {
            get { return battle; }
        }

        /// <summary>
        /// uGUI 버튼이 <see cref="EnemyDatabase"/> 인덱스를 넘긴다.
        /// 파티가 비어 있으면 시뮬레이터가 시작하자마자 패배로 끝내므로 여기서 따로 막지 않는다.
        /// </summary>
        public void StartBattle(int enemyIndex)
        {
            if (state == null || enemyDatabase == null
                || enemyIndex < 0 || enemyIndex >= enemyDatabase.enemies.Count)
            {
                Log("상대를 찾을 수 없다.");
                return;
            }

            var enemy = enemyDatabase.enemies[enemyIndex];
            if (enemy == null)
            {
                Log("상대를 찾을 수 없다.");
                return;
            }

            var party = new System.Collections.Generic.List<CreatureInstance>(state.Roster.Party);
            battle = new BattleSimulator(party, new[] { enemy });
            rewardGranted = false;

            Log(party.Count > 0
                ? enemy.displayName + "와(과)의 전투를 시작했다."
                : "밖에 나와 있는 케어가 없다. 헛간에서 먼저 내보내자.");

            Refresh();
        }

        private void Update()
        {
            if (!IsOpen || battle == null || battle.Outcome != BattleOutcome.InProgress)
            {
                return;
            }

            battle.Tick(Time.deltaTime);

            if (battle.Outcome != BattleOutcome.InProgress)
            {
                GrantReward();
            }

            Refresh();
        }

        private void GrantReward()
        {
            if (rewardGranted)
            {
                return;
            }

            rewardGranted = true;

            if (battle.Outcome == BattleOutcome.Victory && battle.GoldReward > 0)
            {
                state.AddGold(battle.GoldReward);
                Log("승리! 재화 " + battle.GoldReward + " 획득.");
            }
            else if (battle.Outcome == BattleOutcome.Defeat)
            {
                Log("파티가 전멸했다. 케어는 다치지 않는다 — 더 키워서 다시 오자.");
            }

            // 재화가 바뀌었으니 상태 패널도 다시 그린다.
            if (careManager != null)
            {
                careManager.RefreshAll();
            }
        }

        public override void Refresh()
        {
            if (titleText != null)
            {
                titleText.text = PanelLabels.BattleTitle(battle);
            }

            // 진영 목록은 전투 씬의 유닛과 체력바가 대신한다 (Task 6 에서 이 패널을 통째로 갈아치운다).
            if (logText != null)
            {
                logText.text = TailOfLog();
            }
        }

        private string TailOfLog()
        {
            if (battle == null)
            {
                return string.Empty;
            }

            var lines = battle.Log;
            int start = lines.Count - visibleLogLines;
            if (start < 0)
            {
                start = 0;
            }

            builder.Length = 0;
            for (int i = start; i < lines.Count; i++)
            {
                builder.AppendLine(lines[i]);
            }

            return builder.ToString();
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
