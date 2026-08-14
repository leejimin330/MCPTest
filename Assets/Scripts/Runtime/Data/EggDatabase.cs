using System.Collections.Generic;
using UnityEngine;

namespace FarmCreature.Data
{
    /// <summary>알 목록. 상점이 id 로 표시 이름을 찾을 때 쓴다.</summary>
    [CreateAssetMenu(fileName = "EggDatabase", menuName = "FarmCreature/Egg Database")]
    public class EggDatabase : ScriptableObject
    {
        public List<EggData> eggs = new List<EggData>();

        public EggData Find(string eggId)
        {
            if (string.IsNullOrEmpty(eggId) || eggs == null)
            {
                return null;
            }

            for (int i = 0; i < eggs.Count; i++)
            {
                if (eggs[i] != null && eggs[i].eggId == eggId)
                {
                    return eggs[i];
                }
            }

            return null;
        }
    }
}
