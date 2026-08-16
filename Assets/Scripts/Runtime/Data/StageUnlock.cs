namespace FarmCreature.Data
{
    /// <summary>
    /// 스테이지가 열리는 조건의 종류.
    ///
    /// <b>append-only 다.</b> 값이 에셋에 정수로 직렬화되므로 중간에 끼워 넣으면
    /// 이미 만든 조건들이 다른 종류로 바뀐다 (<c>BuildingKind</c> 와 같은 이유).
    /// </summary>
    public enum StageUnlockKind
    {
        /// <summary>같은 맵에서 바로 앞 스테이지를 깼는가. 첫 스테이지면 항상 만족한다.</summary>
        PreviousStageCleared = 0,

        /// <summary>지정한 <c>stageId</c> 를 깼는가. 다른 맵의 스테이지도 가리킬 수 있다.</summary>
        StageCleared = 1,

        /// <summary>
        /// <c>itemId</c> 를 <c>count</c> 개 이상 가졌는가.
        /// 나중에 들어올 드랍 아이템(지도·열쇠)이 이 종류로 들어온다 (설계 1.2).
        /// </summary>
        RequiresItem = 2
    }

    /// <summary>해금 조건 한 줄. 목록 전체가 AND 로 묶인다.</summary>
    [System.Serializable]
    public class StageUnlock
    {
        public StageUnlockKind kind = StageUnlockKind.PreviousStageCleared;

        [UnityEngine.Tooltip("StageCleared 일 때 쓴다.")]
        public string stageId;

        [UnityEngine.Tooltip("RequiresItem 일 때 쓴다.")]
        public string itemId;

        [UnityEngine.Tooltip("RequiresItem 일 때 필요한 개수.")]
        public int count = 1;
    }
}
