using System.Collections.Generic;

namespace FarmCreature.Core
{
    /// <summary>
    /// 어떤 스테이지를 깼는가. <b>인덱스가 아니라 id 로 기록한다</b> —
    /// 인덱스로 두면 스테이지를 중간에 끼워 넣는 순간 진행도가 통째로 밀려
    /// 엉뚱한 스테이지가 깬 것이 된다 (설계 1.1).
    ///
    /// 깬 <b>차례를 보존한다.</b> 세이브가 배열이라 그대로 실린다.
    /// </summary>
    public class StageProgress
    {
        private readonly List<string> cleared = new List<string>();

        public StageProgress()
        {
        }

        /// <summary>세이브에서 복원할 때 쓴다. 빈 id 와 중복은 걸러낸다.</summary>
        public StageProgress(IEnumerable<string> clearedStageIds)
        {
            if (clearedStageIds == null)
            {
                return;
            }

            foreach (var id in clearedStageIds)
            {
                MarkCleared(id);
            }
        }

        public IReadOnlyList<string> ClearedStageIds
        {
            get { return cleared; }
        }

        public bool IsCleared(string stageId)
        {
            return !IsBlank(stageId) && cleared.Contains(stageId);
        }

        /// <summary>처음 깬 것이면 true. 이미 깼거나 id 가 비었으면 아무것도 안 하고 false.</summary>
        public bool MarkCleared(string stageId)
        {
            if (IsBlank(stageId) || cleared.Contains(stageId))
            {
                return false;
            }

            cleared.Add(stageId);
            return true;
        }

        private static bool IsBlank(string value)
        {
            return string.IsNullOrEmpty(value) || value.Trim().Length == 0;
        }
    }
}
