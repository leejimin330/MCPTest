using System.Collections.Generic;
using UnityEngine;

namespace FarmCreature.Data
{
    /// <summary>세이브 로드 시 speciesId 로 종을 되찾기 위한 목록.</summary>
    [CreateAssetMenu(fileName = "SpeciesDatabase", menuName = "FarmCreature/Species Database")]
    public class SpeciesDatabase : ScriptableObject
    {
        public List<CreatureSpeciesData> species = new List<CreatureSpeciesData>();

        public CreatureSpeciesData Find(string speciesId)
        {
            if (string.IsNullOrEmpty(speciesId) || species == null)
            {
                return null;
            }

            for (int i = 0; i < species.Count; i++)
            {
                if (species[i] != null && species[i].speciesId == speciesId)
                {
                    return species[i];
                }
            }

            return null;
        }
    }
}
