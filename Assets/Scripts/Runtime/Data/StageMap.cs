using System.Collections.Generic;
using UnityEngine;

namespace FarmCreature.Data
{
    /// <summary>
    /// 전투터에서 먼저 고르는 것. <b>맵이 스테이지를 담는다</b> —
    /// 스테이지가 맵을 가리키지 않는다 (설계 2.2). 맵을 한 번만 지정하면 되고,
    /// 화면 구조("맵 고르고 → 스테이지 고르고")와 데이터가 같은 모양이 된다.
    ///
    /// <b><see cref="stages"/> 의 순서가 곧 해금 순서다.</b>
    /// </summary>
    [CreateAssetMenu(fileName = "StageMap", menuName = "FarmCreature/Stage Map")]
    public class StageMap : ScriptableObject
    {
        public string displayName = "들판";

        [Tooltip("이 맵의 전장 기하. 전투 씬이 이것으로 유닛을 세운다.")]
        public BattleMapData field;

        [Tooltip("이 맵의 스테이지. 위에서부터 순서대로 해금된다.")]
        public List<BattleEncounter> stages = new List<BattleEncounter>();

        public int Count
        {
            get { return stages != null ? stages.Count : 0; }
        }

        /// <summary>범위 밖이면 <c>null</c>. 화면이 빈 칸을 그릴 수 있어야 한다.</summary>
        public BattleEncounter Get(int index)
        {
            if (stages == null || index < 0 || index >= stages.Count)
            {
                return null;
            }

            return stages[index];
        }

        /// <summary>깬 스테이지 수. 맵 행에 <c>2/4</c> 로 보여준다.</summary>
        public int ClearedCount(FarmCreature.Core.StageProgress progress)
        {
            if (progress == null)
            {
                return 0;
            }

            int done = 0;
            for (int i = 0; i < Count; i++)
            {
                var stage = Get(i);
                if (stage != null && progress.IsCleared(stage.stageId))
                {
                    done++;
                }
            }

            return done;
        }
    }
}
