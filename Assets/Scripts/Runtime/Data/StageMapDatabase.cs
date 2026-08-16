using System.Collections.Generic;
using UnityEngine;

namespace FarmCreature.Data
{
    /// <summary>전투터가 보여줄 맵 목록.</summary>
    [CreateAssetMenu(fileName = "StageMapDatabase", menuName = "FarmCreature/Stage Map Database")]
    public class StageMapDatabase : ScriptableObject
    {
        public List<StageMap> maps = new List<StageMap>();

        public int Count
        {
            get { return maps != null ? maps.Count : 0; }
        }

        public StageMap Get(int index)
        {
            if (maps == null || index < 0 || index >= maps.Count)
            {
                return null;
            }

            return maps[index];
        }
    }
}
