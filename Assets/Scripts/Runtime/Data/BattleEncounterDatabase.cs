using System.Collections.Generic;
using UnityEngine;

namespace FarmCreature.Data
{
    /// <summary>
    /// 전투터에 뜨는 인카운터 목록. <b>버튼 개수가 여기서 나온다</b> —
    /// 그래서 씬 무결성 검사가 버튼 수를 이 목록 크기와 대조한다.
    /// 버튼이 인덱스를 들고 있으므로 <b>순서를 바꾸지 않는다</b> (함정 3.6과 같은 이유).
    /// </summary>
    [CreateAssetMenu(fileName = "BattleEncounterDatabase", menuName = "FarmCreature/Battle Encounter Database")]
    public class BattleEncounterDatabase : ScriptableObject
    {
        public List<BattleEncounter> encounters = new List<BattleEncounter>();

        public int Count
        {
            get { return encounters != null ? encounters.Count : 0; }
        }

        public BattleEncounter Get(int index)
        {
            return index >= 0 && index < Count ? encounters[index] : null;
        }
    }
}
