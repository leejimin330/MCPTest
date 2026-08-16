using System.Collections.Generic;
using UnityEngine;

namespace FarmCreature.Data
{
    /// <summary>인카운터 한 줄 — 어떤 적이 몇 마리 나오는가.</summary>
    [System.Serializable]
    public class EncounterEntry
    {
        public EnemyData enemy;

        public int count = 1;
    }

    /// <summary>
    /// 전투터 버튼 하나에 대응하는 한 판. <b>인스펙터에서만 만든다</b> —
    /// 적 파티 편성 UI 는 이번 범위 밖이다 (설계 7절).
    ///
    /// 적 총원은 씬에 미리 만들어 둔 슬롯 수
    /// (<see cref="FarmCreature.Presentation.BattleField.MaxEnemies"/>)를 넘을 수 없다.
    /// 넘으면 <c>BuildAll</c> 이 실패시킨다 — 조용히 잘라내면 인스펙터에 적어놓은 적이
    /// 왜 안 나오는지 알 수 없게 된다 (설계 4.3).
    /// </summary>
    [CreateAssetMenu(fileName = "Encounter", menuName = "FarmCreature/Battle Encounter")]
    public class BattleEncounter : ScriptableObject
    {
        [Tooltip("진행도가 이 id 로 기록된다. 전 맵을 통틀어 유일해야 한다. 한 번 정하면 바꾸지 말 것.")]
        public string stageId;

        public string displayName = "들판";

        [Tooltip("등장하는 적과 그 수. 위에서부터 순서대로 나온다.")]
        public List<EncounterEntry> entries = new List<EncounterEntry>();

        [Tooltip("이 스테이지만 다른 전장을 쓰고 싶을 때. 비어 있으면 맵(StageMap.field)이 정한다.")]
        public BattleMapData map;

        [Tooltip("이 스테이지가 열리는 조건. 전부 AND. 비어 있으면 항상 열림.")]
        public List<StageUnlock> unlock = new List<StageUnlock>();

        /// <summary>등장 적 총원. 빈 칸과 0 이하의 수는 세지 않는다.</summary>
        public int TotalEnemyCount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < CountOfEntries; i++)
                {
                    total += ValidCountAt(i);
                }

                return total;
            }
        }

        /// <summary>전부 쓰러뜨렸을 때 받는 재화. 전투터 버튼에 미리 보여준다.</summary>
        public int TotalGoldReward
        {
            get
            {
                int total = 0;
                for (int i = 0; i < CountOfEntries; i++)
                {
                    // ValidCountAt 이 0 이면 빈 칸이라는 뜻이므로 적을 들여다보지 않는다.
                    int count = ValidCountAt(i);
                    if (count > 0)
                    {
                        total += count * entries[i].enemy.goldReward;
                    }
                }

                return total;
            }
        }

        /// <summary>
        /// <see cref="entries"/> 를 한 마리씩 펼친다. 순서는 목록 순서 그대로다 —
        /// <see cref="FarmCreature.Core.BattleSimulator"/> 가 앞쪽부터 때리므로 순서가 곧 전열이다.
        /// </summary>
        public List<EnemyData> BuildEnemyList()
        {
            var foes = new List<EnemyData>();

            for (int i = 0; i < CountOfEntries; i++)
            {
                int count = ValidCountAt(i);
                for (int n = 0; n < count; n++)
                {
                    foes.Add(entries[i].enemy);
                }
            }

            return foes;
        }

        private int CountOfEntries
        {
            get { return entries != null ? entries.Count : 0; }
        }

        /// <summary>빈 칸·음수를 걸러 실제로 나올 마리 수를 돌려준다.</summary>
        private int ValidCountAt(int index)
        {
            var entry = entries[index];
            if (entry == null || entry.enemy == null || entry.count <= 0)
            {
                return 0;
            }

            return entry.count;
        }
    }
}
