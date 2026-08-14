using System.Collections.Generic;
using FarmCreature.Data;
using UnityEngine;

namespace FarmCreature.Tests
{
    /// <summary>테스트용 ScriptableObject 인스턴스를 메모리에만 만든다 (에셋 파일 없음).</summary>
    public static class TestFixtures
    {
        public static CreatureSpeciesData MakeSpecies(
            string speciesId,
            string displayName = null,
            int attack = 5,
            int defense = 5,
            int health = 20)
        {
            var so = ScriptableObject.CreateInstance<CreatureSpeciesData>();
            so.speciesId = speciesId;
            so.displayName = displayName ?? speciesId;
            so.baseStats = new StatBlock { attack = attack, defense = defense, health = health };
            so.placeholderColor = Color.green;
            so.placeholderScale = 1f;
            so.evolutions = new List<EvolutionCondition>();
            return so;
        }

        public static ItemData MakeItem(string itemId, StatType stat, int amount, string displayName = null)
        {
            var so = ScriptableObject.CreateInstance<ItemData>();
            so.itemId = itemId;
            so.displayName = displayName ?? itemId;
            so.targetStat = stat;
            so.amount = amount;
            return so;
        }

        /// <summary>전투 테스트용 종. 평타 간격과 고유 스킬을 직접 지정한다.</summary>
        public static CreatureSpeciesData MakeFighter(
            string speciesId,
            int attack,
            int defense,
            int health,
            float attackInterval = 1f,
            SkillEffect effect = SkillEffect.Damage,
            float cooldownSeconds = 5f,
            float multiplier = 2f,
            string skillName = "테스트 스킬")
        {
            var so = MakeSpecies(speciesId, speciesId, attack, defense, health);
            so.attackInterval = attackInterval;
            so.skill = new SkillDefinition
            {
                displayName = skillName,
                effect = effect,
                cooldownSeconds = cooldownSeconds,
                multiplier = multiplier
            };
            return so;
        }

        public static EnemyData MakeEnemy(
            string enemyId,
            int maxHp,
            int attack,
            int defense,
            float attackInterval = 1.5f,
            int goldReward = 10,
            string displayName = null)
        {
            var so = ScriptableObject.CreateInstance<EnemyData>();
            so.enemyId = enemyId;
            so.displayName = displayName ?? enemyId;
            so.maxHp = maxHp;
            so.attack = attack;
            so.defense = defense;
            so.attackInterval = attackInterval;
            so.goldReward = goldReward;
            return so;
        }

        public static SpeciesDatabase MakeSpeciesDatabase(params CreatureSpeciesData[] entries)
        {
            var so = ScriptableObject.CreateInstance<SpeciesDatabase>();
            so.species = new List<CreatureSpeciesData>(entries);
            return so;
        }

        public static ItemDatabase MakeItemDatabase(params ItemData[] entries)
        {
            var so = ScriptableObject.CreateInstance<ItemDatabase>();
            so.items = new List<ItemData>(entries);
            return so;
        }
    }
}
