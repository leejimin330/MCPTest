using System;
using FarmCreature.Data;

namespace FarmCreature.Core
{
    /// <summary>
    /// 케어의 <b>변하는 값 전부</b>. ScriptableObject 를 건드리지 않기 위해 존재한다.
    /// UnityEngine API 를 호출하지 않으므로 EditMode 테스트에서 그대로 검증된다.
    ///
    /// 스탯은 두 층으로 나뉜다:
    /// <list type="bullet">
    /// <item><b>기본 스탯</b> — 훈련으로 영구히 오르는 값. 세이브에 기록된다.</item>
    /// <item><b>장비 보너스</b> — 장착 중에만 더해지는 값. 빼면 사라진다.</item>
    /// </list>
    /// 진화 조건은 둘을 합친 <see cref="GetStat"/> 을 본다.
    /// </summary>
    public class CreatureInstance
    {
        public const int MinStat = 1;
        public const int MinCare = 0;
        public const int MaxCare = 100;
        public const int StartingFullness = 60;
        public const int StartingBond = 30;

        /// <summary>장착 슬롯 개수. 지금은 1칸만 쓴다.</summary>
        public const int EquipmentSlots = 1;

        private int attack;
        private int defense;
        private int health;

        public CreatureSpeciesData CurrentSpecies { get; set; }

        /// <summary>장착 중인 아이템. 없으면 null. 인벤토리에는 들어 있지 않다.</summary>
        public ItemData EquippedItem { get; private set; }

        public int Exp { get; private set; }
        public int Fullness { get; private set; }
        public int Bond { get; private set; }

        private CreatureInstance()
        {
        }

        /// <summary>종의 기본치를 <b>복사</b>해 새 개체를 만든다 (참조 공유 금지).</summary>
        public static CreatureInstance CreateNew(CreatureSpeciesData species)
        {
            if (species == null)
            {
                throw new ArgumentNullException(nameof(species));
            }

            var stats = species.baseStats ?? new StatBlock();

            return new CreatureInstance
            {
                CurrentSpecies = species,
                attack = Math.Max(MinStat, stats.attack),
                defense = Math.Max(MinStat, stats.defense),
                health = Math.Max(MinStat, stats.health),
                Exp = 0,
                Fullness = StartingFullness,
                Bond = StartingBond,
                EquippedItem = null
            };
        }

        /// <summary>세이브 복원용. 넘기는 스탯은 <b>장비 보너스를 뺀 기본 스탯</b>이어야 한다.</summary>
        public static CreatureInstance Restore(
            CreatureSpeciesData species, int exp, int attack, int defense, int health, int fullness, int bond)
        {
            var creature = CreateNew(species);
            creature.SetExp(exp);
            creature.SetStat(StatType.Attack, attack);
            creature.SetStat(StatType.Defense, defense);
            creature.SetStat(StatType.Health, health);
            creature.SetFullness(fullness);
            creature.SetBond(bond);
            return creature;
        }

        // ---------- 스탯 ----------

        /// <summary>훈련으로 쌓인 영구 스탯. 장비 보너스는 빠져 있다.</summary>
        public int GetBaseStat(StatType stat)
        {
            switch (stat)
            {
                case StatType.Attack: return attack;
                case StatType.Defense: return defense;
                case StatType.Health: return health;
                default: throw new ArgumentOutOfRangeException(nameof(stat), stat, null);
            }
        }

        /// <summary>장착 중인 아이템이 주는 보너스. 없으면 0.</summary>
        public int GetEquipmentBonus(StatType stat)
        {
            if (EquippedItem == null || EquippedItem.targetStat != stat)
            {
                return 0;
            }

            return EquippedItem.amount;
        }

        /// <summary>기본 스탯 + 장비 보너스. 진화 조건과 UI 가 보는 값.</summary>
        public int GetStat(StatType stat)
        {
            return GetBaseStat(stat) + GetEquipmentBonus(stat);
        }

        /// <summary>기본 스탯을 직접 설정한다 (하한 <see cref="MinStat"/>).</summary>
        public void SetStat(StatType stat, int value)
        {
            int clamped = Math.Max(MinStat, value);

            switch (stat)
            {
                case StatType.Attack: attack = clamped; break;
                case StatType.Defense: defense = clamped; break;
                case StatType.Health: health = clamped; break;
                default: throw new ArgumentOutOfRangeException(nameof(stat), stat, null);
            }
        }

        public void AddStat(StatType stat, int delta)
        {
            SetStat(stat, GetBaseStat(stat) + delta);
        }

        // ---------- 장착 ----------

        /// <summary>
        /// 장착 슬롯을 교체한다. 인벤토리 정합성(빼기/되돌리기)은
        /// <see cref="CareActions.Equip"/> 이 책임진다 — 여기서는 슬롯만 바꾼다.
        /// </summary>
        public void SetEquipped(ItemData item)
        {
            EquippedItem = item;
        }

        // ---------- 육성 상태값 ----------

        public void SetExp(int value)
        {
            Exp = Math.Max(0, value);
        }

        public void AddExp(int delta)
        {
            SetExp(Exp + delta);
        }

        public void SetFullness(int value)
        {
            Fullness = ClampCare(value);
        }

        public void AddFullness(int delta)
        {
            SetFullness(Fullness + delta);
        }

        public void SetBond(int value)
        {
            Bond = ClampCare(value);
        }

        public void AddBond(int delta)
        {
            SetBond(Bond + delta);
        }

        private static int ClampCare(int value)
        {
            if (value < MinCare) return MinCare;
            if (value > MaxCare) return MaxCare;
            return value;
        }
    }
}
