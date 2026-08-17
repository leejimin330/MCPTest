using System.Collections.Generic;
using UnityEngine;

namespace FarmCreature.Data
{
    /// <summary>
    /// 전투터에 나오는 적. 케어와 달리 성장하지 않으므로 변하는 값이 없고,
    /// 전투 중 체력은 <see cref="FarmCreature.Core.BattleUnit"/> 이 따로 들고 있는다.
    /// </summary>
    [CreateAssetMenu(fileName = "Enemy", menuName = "FarmCreature/Enemy")]
    public class EnemyData : ScriptableObject
    {
        [Tooltip("세이브/진행도에 쓰일 수 있는 안정적인 식별자.")]
        public string enemyId;

        public string displayName;

        public int maxHp = 30;
        public int attack = 6;
        public int defense = 2;

        [Tooltip("이 간격마다 평타를 넣는다. 작을수록 빠르다.")]
        public float attackInterval = 1.6f;

        [Tooltip("쓰러뜨렸을 때 주는 재화.")]
        public int goldReward = 10;

        [Tooltip("쓰러뜨렸을 때 나올 수 있는 것들. 한 마리마다 따로 굴린다. 비어 있으면 아무것도 안 나온다.")]
        public List<DropEntry> drops = new List<DropEntry>();

        public Color placeholderColor = new Color(0.8f, 0.35f, 0.35f);
    }
}
