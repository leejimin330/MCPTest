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

        /// <summary>전투 맵 픽스처. 기본값은 테스트가 신경 쓰지 않는 값이다.</summary>
        public static BattleMapData MakeBattleMap(
            Vector2? allyCenter = null,
            Vector2? allySize = null,
            Vector2? enemyCenter = null,
            Vector2? enemySize = null,
            float minSpacing = 1.2f)
        {
            var so = ScriptableObject.CreateInstance<BattleMapData>();
            so.displayName = "테스트 맵";
            so.groundSize = new Vector2(16f, 9f);
            so.allySpawnCenter = allyCenter ?? new Vector2(-4f, 0f);
            so.allySpawnSize = allySize ?? new Vector2(5f, 6f);
            so.enemySpawnCenter = enemyCenter ?? new Vector2(4f, 0f);
            so.enemySpawnSize = enemySize ?? new Vector2(5f, 6f);
            so.minSpacing = minSpacing;
            return so;
        }

        /// <summary>인카운터 픽스처. (적, 마리수) 쌍을 목록 순서대로 넣는다.</summary>
        public static BattleEncounter MakeEncounter(
            string displayName, params (EnemyData enemy, int count)[] entries)
        {
            var so = ScriptableObject.CreateInstance<BattleEncounter>();
            so.displayName = displayName;
            so.map = MakeBattleMap();
            so.entries = new List<EncounterEntry>();

            for (int i = 0; i < entries.Length; i++)
            {
                so.entries.Add(new EncounterEntry
                {
                    enemy = entries[i].enemy,
                    count = entries[i].count
                });
            }

            return so;
        }

        /// <summary>스테이지 픽스처. 해금 조건을 직접 넘긴다 (null 이면 항상 열림).</summary>
        public static BattleEncounter MakeStage(
            string stageId, string displayName, List<StageUnlock> unlock,
            params (EnemyData enemy, int count)[] entries)
        {
            var so = MakeEncounter(displayName, entries);
            so.stageId = stageId;
            so.map = null;                     // 전장은 맵이 정한다 (설계 2.1)
            so.unlock = unlock ?? new List<StageUnlock>();
            return so;
        }

        public static StageMap MakeStageMap(string displayName, params BattleEncounter[] stages)
        {
            var so = ScriptableObject.CreateInstance<StageMap>();
            so.displayName = displayName;
            so.field = MakeBattleMap();
            so.stages = new List<BattleEncounter>(stages);
            return so;
        }

        public static StageMapDatabase MakeStageMapDatabase(params StageMap[] maps)
        {
            var so = ScriptableObject.CreateInstance<StageMapDatabase>();
            so.maps = new List<StageMap>(maps);
            return so;
        }

        /// <summary>해금 조건 한 줄. 안 쓰는 필드는 비워 둔다.</summary>
        public static StageUnlock Unlock(
            StageUnlockKind kind, string stageId = null, string itemId = null, int count = 1)
        {
            return new StageUnlock
            {
                kind = kind,
                stageId = stageId,
                itemId = itemId,
                count = count
            };
        }

        public static EggData MakeEgg(
            string eggId, string displayName = null, CreatureSpeciesData hatchesInto = null)
        {
            var so = ScriptableObject.CreateInstance<EggData>();
            so.eggId = eggId;
            so.displayName = displayName ?? eggId;
            so.hatchesInto = hatchesInto;
            return so;
        }

        public static EggDatabase MakeEggDatabase(params EggData[] entries)
        {
            var so = ScriptableObject.CreateInstance<EggDatabase>();
            so.eggs = new List<EggData>(entries);
            return so;
        }

        public static ShopEntry MakeShopEntry(ShopEntryKind kind, string targetId, int price)
        {
            return new ShopEntry { kind = kind, targetId = targetId, price = price };
        }

        public static ShopStockDatabase MakeShopStock(params ShopEntry[] entries)
        {
            var so = ScriptableObject.CreateInstance<ShopStockDatabase>();
            so.entries = new List<ShopEntry>(entries);
            return so;
        }
    }
}
