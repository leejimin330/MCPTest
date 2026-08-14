using FarmCreature.Data;

namespace FarmCreature.Core
{
    /// <summary>
    /// 전투에 참가하는 한 명. <b>전투 중에만 존재한다</b> —
    /// 여기서 깎이는 체력은 <see cref="CreatureInstance"/> 에 반영되지 않는다.
    /// 전투가 끝나면 케어는 원래 상태 그대로다 (부상/사망 개념 없음).
    /// </summary>
    public class BattleUnit
    {
        public string Name;
        public int MaxHp;
        public int Hp;
        public int Attack;
        public int Defense;

        /// <summary>평타 간격(초).</summary>
        public float AttackInterval;

        /// <summary>없으면 평타만 친다. 적은 스킬이 없다.</summary>
        public SkillDefinition Skill;

        public bool IsAlly;

        /// <summary>쓰러뜨렸을 때 주는 재화. 아군은 0.</summary>
        public int GoldReward;

        /// <summary>전투 중에만 쓰이는 타이머. 시뮬레이터가 관리한다.</summary>
        internal float AttackTimer;
        internal float SkillTimer;

        public bool IsAlive
        {
            get { return Hp > 0; }
        }

        /// <summary>
        /// 케어의 <b>장비 보너스를 포함한</b> 스탯으로 만든다 — 장비를 끼면 전투가 실제로 세진다.
        /// 체력 스탯이 그대로 최대 체력이 된다.
        /// </summary>
        public static BattleUnit FromCreature(CreatureInstance creature)
        {
            if (creature == null)
            {
                return null;
            }

            var species = creature.CurrentSpecies;
            int hp = creature.GetStat(StatType.Health);

            return new BattleUnit
            {
                Name = species != null ? species.displayName : "케어",
                MaxHp = hp,
                Hp = hp,
                Attack = creature.GetStat(StatType.Attack),
                Defense = creature.GetStat(StatType.Defense),
                AttackInterval = species != null && species.attackInterval > 0f
                    ? species.attackInterval
                    : 1.2f,
                Skill = species != null ? species.skill : null,
                IsAlly = true,
                GoldReward = 0
            };
        }

        public static BattleUnit FromEnemy(EnemyData enemy)
        {
            if (enemy == null)
            {
                return null;
            }

            return new BattleUnit
            {
                Name = !string.IsNullOrEmpty(enemy.displayName) ? enemy.displayName : enemy.enemyId,
                MaxHp = enemy.maxHp,
                Hp = enemy.maxHp,
                Attack = enemy.attack,
                Defense = enemy.defense,
                AttackInterval = enemy.attackInterval > 0f ? enemy.attackInterval : 1.6f,
                Skill = null,
                IsAlly = false,
                GoldReward = enemy.goldReward
            };
        }

        public void TakeDamage(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Hp = Hp - amount < 0 ? 0 : Hp - amount;
        }

        public void Heal(int amount)
        {
            if (amount <= 0 || !IsAlive)
            {
                return;
            }

            Hp = Hp + amount > MaxHp ? MaxHp : Hp + amount;
        }
    }
}
