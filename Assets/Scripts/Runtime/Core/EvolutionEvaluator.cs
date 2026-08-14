using FarmCreature.Data;

namespace FarmCreature.Core
{
    /// <summary>
    /// 진화 조건을 <b>판정만</b> 한다. 종 교체는 호출자가 수행한다 (순수 함수 유지).
    /// 목록 순서가 곧 우선순위다.
    /// </summary>
    public static class EvolutionEvaluator
    {
        /// <returns>충족되는 첫 조건의 결과 종. 없으면 null.</returns>
        public static CreatureSpeciesData TryEvolve(CreatureInstance creature, Inventory inventory)
        {
            if (creature == null || creature.CurrentSpecies == null)
            {
                return null;
            }

            var conditions = creature.CurrentSpecies.evolutions;
            if (conditions == null)
            {
                return null;
            }

            for (int i = 0; i < conditions.Count; i++)
            {
                var condition = conditions[i];
                if (condition == null || condition.result == null)
                {
                    continue;
                }

                if (IsSatisfied(condition, creature, inventory))
                {
                    return condition.result;
                }
            }

            return null;
        }

        public static bool IsSatisfied(EvolutionCondition condition, CreatureInstance creature, Inventory inventory)
        {
            if (condition == null)
            {
                return false;
            }

            var requirements = condition.requirements;
            if (requirements == null)
            {
                return true;
            }

            for (int i = 0; i < requirements.Count; i++)
            {
                if (!IsSatisfied(requirements[i], creature, inventory))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsSatisfied(EvolutionRequirement requirement, CreatureInstance creature, Inventory inventory)
        {
            if (requirement == null || creature == null)
            {
                return false;
            }

            switch (requirement.kind)
            {
                case RequirementKind.ExpAtLeast:
                    return creature.Exp >= requirement.value;

                case RequirementKind.StatAtLeast:
                    // 장비 보너스를 포함한 값 — "장착 후 진화"가 성립하도록.
                    return creature.GetStat(requirement.stat) >= requirement.value;

                case RequirementKind.RequiresEquipped:
                    return creature.EquippedItem != null
                           && creature.EquippedItem.itemId == requirement.itemId;

                default:
                    return false;
            }
        }
    }
}
