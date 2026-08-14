using System.Collections.Generic;
using UnityEngine;

namespace FarmCreature.Data
{
    /// <summary>전투터 목록. 버튼이 인덱스로 적을 고르므로 순서가 곧 난이도 순이다.</summary>
    [CreateAssetMenu(fileName = "EnemyDatabase", menuName = "FarmCreature/Enemy Database")]
    public class EnemyDatabase : ScriptableObject
    {
        public List<EnemyData> enemies = new List<EnemyData>();

        public EnemyData Find(string enemyId)
        {
            if (string.IsNullOrEmpty(enemyId) || enemies == null)
            {
                return null;
            }

            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] != null && enemies[i].enemyId == enemyId)
                {
                    return enemies[i];
                }
            }

            return null;
        }
    }
}
