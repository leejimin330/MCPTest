using System;

namespace FarmCreature.Data
{
    public enum RequirementKind
    {
        ExpAtLeast,
        StatAtLeast,

        /// <summary>해당 아이템을 <b>장착 중</b>이어야 한다 (단순 보유로는 부족).</summary>
        RequiresEquipped
    }

    /// <summary>진화 요구사항 1건. 여러 개가 AND 로 묶여 하나의 조건을 이룬다.</summary>
    [Serializable]
    public class EvolutionRequirement
    {
        public RequirementKind kind;

        /// <summary>ExpAtLeast / StatAtLeast 의 임계치.</summary>
        public int value;

        /// <summary>StatAtLeast 일 때만 의미가 있다.</summary>
        public StatType stat;

        /// <summary>RequiresEquipped 일 때만 의미가 있다.</summary>
        public string itemId;

        public static EvolutionRequirement ExpAtLeast(int value)
        {
            return new EvolutionRequirement { kind = RequirementKind.ExpAtLeast, value = value };
        }

        public static EvolutionRequirement StatAtLeast(StatType stat, int value)
        {
            return new EvolutionRequirement
            {
                kind = RequirementKind.StatAtLeast,
                stat = stat,
                value = value
            };
        }

        public static EvolutionRequirement RequiresEquipped(string itemId)
        {
            return new EvolutionRequirement { kind = RequirementKind.RequiresEquipped, itemId = itemId };
        }
    }
}
