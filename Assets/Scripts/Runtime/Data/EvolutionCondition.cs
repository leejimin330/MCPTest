using System;
using System.Collections.Generic;

namespace FarmCreature.Data
{
    /// <summary>요구사항 전부(AND)를 만족하면 <see cref="result"/> 종으로 진화한다.</summary>
    [Serializable]
    public class EvolutionCondition
    {
        public CreatureSpeciesData result;
        public List<EvolutionRequirement> requirements = new List<EvolutionRequirement>();
    }
}
