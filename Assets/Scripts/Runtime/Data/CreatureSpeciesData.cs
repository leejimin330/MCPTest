using System.Collections.Generic;
using UnityEngine;

namespace FarmCreature.Data
{
    /// <summary>
    /// 종/진화 단계별 정적 데이터. 런타임에 <b>절대 수정하지 않는다</b> —
    /// 변하는 값은 전부 <see cref="FarmCreature.Core.CreatureInstance"/> 에 있다.
    /// </summary>
    [CreateAssetMenu(fileName = "Species", menuName = "FarmCreature/Creature Species")]
    public class CreatureSpeciesData : ScriptableObject
    {
        [Tooltip("세이브에 기록되는 안정적인 식별자. 한 번 정하면 바꾸지 않는다.")]
        public string speciesId;

        public string displayName;

        public StatBlock baseStats = new StatBlock();

        [Header("플레이스홀더 외형")]
        public Color placeholderColor = Color.green;
        public float placeholderScale = 1f;

        [Header("전투")]
        [Tooltip("이 종의 고유 스킬. 자동전투에서 쿨타임마다 저절로 나간다.")]
        public SkillDefinition skill = new SkillDefinition();

        [Tooltip("평타 간격(초). 작을수록 자주 때린다.")]
        public float attackInterval = 1.2f;

        [Header("진화")]
        [Tooltip("목록 순서가 곧 우선순위. 동시 충족 시 위쪽이 적용된다.")]
        public List<EvolutionCondition> evolutions = new List<EvolutionCondition>();
    }
}
