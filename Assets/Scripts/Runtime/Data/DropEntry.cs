namespace FarmCreature.Data
{
    /// <summary>
    /// 적 하나를 쓰러뜨렸을 때 나올 수 있는 것 한 줄.
    ///
    /// <b>왜 <c>ItemData</c> 참조가 아니라 문자열 id 인가</b>: 인벤토리가 문자열 키 하나로
    /// 아이템과 알을 함께 담기 때문이다(설계 4.3). id 로 두면 두 계통 어느 쪽이든 드랍할 수 있고,
    /// <see cref="StageUnlock"/> 의 <c>itemId</c> 와도 같은 공간을 쓴다 —
    /// 그래서 드랍한 열쇠가 해금 조건에 그대로 물린다.
    /// </summary>
    [System.Serializable]
    public class DropEntry
    {
        [UnityEngine.Tooltip("인벤토리 키. 아이템 id 또는 알 id(egg_ 로 시작).")]
        public string itemId;

        [UnityEngine.Range(0, 100)]
        [UnityEngine.Tooltip("한 마리당 나올 확률(%). 100 이면 반드시 나온다.")]
        public int chancePercent = 20;

        [UnityEngine.Tooltip("나왔을 때 몇 개인가.")]
        public int count = 1;
    }
}
