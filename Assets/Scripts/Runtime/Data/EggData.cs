using UnityEngine;

namespace FarmCreature.Data
{
    /// <summary>
    /// 알 정적 데이터. <b>장착 아이템이 아니다</b> — <see cref="ItemData"/> 와 별개 계통인 이유는
    /// 창고 행이 <see cref="ItemDatabase"/> 인덱스와 1:1 로 묶여 있기 때문이다 (설계 1.1).
    /// </summary>
    [CreateAssetMenu(fileName = "Egg", menuName = "FarmCreature/Egg")]
    public class EggData : ScriptableObject
    {
        /// <summary>알 id 는 이것으로 시작한다. 아이템과 같은 인벤토리 키 공간을 쓰기 때문이다.</summary>
        public const string IdPrefix = "egg_";

        [Tooltip("인벤토리·세이브가 쓰는 식별자. egg_ 로 시작한다.")]
        public string eggId;

        public string displayName;

        [Tooltip("부화하면 나오는 종. 부화소가 다음 작업에서 읽는다.")]
        public CreatureSpeciesData hatchesInto;
    }
}
