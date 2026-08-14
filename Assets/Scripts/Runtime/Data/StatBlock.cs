using System;

namespace FarmCreature.Data
{
    /// <summary>전투형 스탯 3종 묶음. 종의 기본치와 세이브 스냅샷에 쓰인다.</summary>
    [Serializable]
    public class StatBlock
    {
        public int attack = 5;
        public int defense = 5;
        public int health = 20;

        public int Get(StatType stat)
        {
            switch (stat)
            {
                case StatType.Attack: return attack;
                case StatType.Defense: return defense;
                case StatType.Health: return health;
                default: throw new ArgumentOutOfRangeException(nameof(stat), stat, null);
            }
        }
    }
}
