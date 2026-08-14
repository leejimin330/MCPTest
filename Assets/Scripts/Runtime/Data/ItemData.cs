using UnityEngine;

namespace FarmCreature.Data
{
    /// <summary>아이템 정적 데이터. 사용 시 <see cref="targetStat"/> 에 <see cref="amount"/> 를 더한다.</summary>
    [CreateAssetMenu(fileName = "Item", menuName = "FarmCreature/Item")]
    public class ItemData : ScriptableObject
    {
        [Tooltip("세이브와 진화 조건에서 참조하는 안정적인 식별자.")]
        public string itemId;

        public string displayName;

        public StatType targetStat = StatType.Attack;
        public int amount = 1;
    }
}
