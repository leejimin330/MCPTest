using System;

namespace FarmCreature.Core
{
    /// <summary>
    /// 케어 한 마리분. 스탯 셋은 <b>장비 보너스를 뺀 기본 스탯</b>이다 —
    /// 보너스를 저장하면 불러올 때마다 장비 효과가 중복으로 얹혀 스탯이 불어난다.
    /// </summary>
    [Serializable]
    public class CreatureSaveData
    {
        public string speciesId;

        /// <summary>장착 중인 아이템. 없으면 빈 문자열. 인벤토리 배열에는 포함되지 않는다.</summary>
        public string equippedItemId;

        public int exp;
        public int attack;
        public int defense;
        public int health;
        public int fullness;
        public int bond;
    }

    /// <summary>
    /// 세이브 JSON 스키마. JsonUtility 가 Dictionary 를 직렬화하지 못하므로
    /// 인벤토리는 병렬 배열로 평탄화한다.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>
        /// v2 → v3 에서 케어 1마리 구조가 <b>로스터(여러 마리) + 파티 + 재화</b>로 바뀌었다.
        /// v2 이하는 옮길 방법이 없어 새 게임으로 폴백한다.
        ///
        /// v3 → v4 는 <see cref="clearedStageIds"/> 가 <b>느는 것뿐</b>이라 옮길 것이 없다 —
        /// v3 세이브는 진행도만 빈 채로 승격한다 (설계 1.4).
        /// </summary>
        public const int CurrentVersion = 4;

        public int version = CurrentVersion;

        /// <summary>보유한 케어 전부. 이 순서가 헛간 목록 순서가 된다.</summary>
        public CreatureSaveData[] creatures = new CreatureSaveData[0];

        /// <summary><see cref="creatures"/> 인덱스. 저장된 순서가 곧 파티 순서다.</summary>
        public int[] partyIndices = new int[0];

        /// <summary>육성 대상의 <see cref="creatures"/> 인덱스. 범위를 벗어나면 0 으로 되돌린다.</summary>
        public int targetIndex;

        public int gold;

        public string[] itemIds = new string[0];
        public int[] itemCounts = new int[0];

        /// <summary>깬 스테이지의 id. <b>인덱스가 아니라 id 다</b> (설계 1.1). v4 신설.</summary>
        public string[] clearedStageIds = new string[0];

        public string savedAtUtc;
    }
}
