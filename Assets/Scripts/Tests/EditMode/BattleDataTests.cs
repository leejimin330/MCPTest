using FarmCreature.Data;
using NUnit.Framework;
using UnityEngine;

namespace FarmCreature.Tests
{
    /// <summary>
    /// 전투 데이터의 계산 규칙. 좌표는 표시 전용이지만 <b>영역 계산이 틀리면 유닛이 맵 밖에 선다</b>.
    /// 인카운터의 총원은 씬 슬롯 개수(BattleField.MaxEnemies)와 대조되므로 정확해야 한다.
    /// </summary>
    public class BattleDataTests
    {
        // ---- 맵 영역 ----

        [Test]
        public void AllySpawnArea_IsCenteredOnTheGivenCenter()
        {
            var map = TestFixtures.MakeBattleMap(
                allyCenter: new Vector2(-4f, 1f), allySize: new Vector2(6f, 4f));

            var area = map.AllySpawnArea;

            Assert.AreEqual(-7f, area.xMin, 0.001f);
            Assert.AreEqual(-1f, area.xMax, 0.001f);
            Assert.AreEqual(-1f, area.yMin, 0.001f);
            Assert.AreEqual(3f, area.yMax, 0.001f);
        }

        [Test]
        public void EnemySpawnArea_IsCenteredOnTheGivenCenter()
        {
            var map = TestFixtures.MakeBattleMap(
                enemyCenter: new Vector2(5f, 0f), enemySize: new Vector2(4f, 8f));

            var area = map.EnemySpawnArea;

            Assert.AreEqual(3f, area.xMin, 0.001f);
            Assert.AreEqual(7f, area.xMax, 0.001f);
            Assert.AreEqual(-4f, area.yMin, 0.001f);
            Assert.AreEqual(4f, area.yMax, 0.001f);
        }

        // ---- 인카운터 총원 ----

        [Test]
        public void TotalEnemyCount_SumsTheEntryCounts()
        {
            var rat = TestFixtures.MakeEnemy("rat", maxHp: 10, attack: 1, defense: 0);
            var boar = TestFixtures.MakeEnemy("boar", maxHp: 20, attack: 2, defense: 1);

            var encounter = TestFixtures.MakeEncounter("들판", (rat, 3), (boar, 2));

            Assert.AreEqual(5, encounter.TotalEnemyCount);
        }

        [Test]
        public void TotalEnemyCount_IsZeroForAnEmptyEncounter()
        {
            var encounter = TestFixtures.MakeEncounter("빈 들판");

            Assert.AreEqual(0, encounter.TotalEnemyCount);
        }

        [Test]
        public void TotalEnemyCount_IgnoresBlankRows()
        {
            // 인스펙터에서 목록을 늘리면 빈 칸이 생긴다. 그것 때문에 적이 늘어나면 안 된다.
            var rat = TestFixtures.MakeEnemy("rat", maxHp: 10, attack: 1, defense: 0);
            var encounter = TestFixtures.MakeEncounter("들판", (rat, 2), (null, 5), (rat, 0), (rat, -3));

            Assert.AreEqual(2, encounter.TotalEnemyCount);
        }

        [Test]
        public void BuildEnemyList_ExpandsCountsInListOrder()
        {
            var rat = TestFixtures.MakeEnemy("rat", maxHp: 10, attack: 1, defense: 0);
            var boar = TestFixtures.MakeEnemy("boar", maxHp: 20, attack: 2, defense: 1);

            var foes = TestFixtures.MakeEncounter("들판", (rat, 2), (boar, 1)).BuildEnemyList();

            Assert.AreEqual(3, foes.Count);
            Assert.AreSame(rat, foes[0]);
            Assert.AreSame(rat, foes[1]);
            Assert.AreSame(boar, foes[2]);
        }

        [Test]
        public void BuildEnemyList_MatchesTotalEnemyCount()
        {
            var rat = TestFixtures.MakeEnemy("rat", maxHp: 10, attack: 1, defense: 0);
            var encounter = TestFixtures.MakeEncounter("들판", (rat, 4), (null, 2));

            Assert.AreEqual(encounter.TotalEnemyCount, encounter.BuildEnemyList().Count,
                "두 값이 어긋나면 슬롯 개수 검사가 통과해도 화면에 안 나오는 적이 생긴다");
        }

        [Test]
        public void TotalGoldReward_SumsPerEnemyRewards()
        {
            var rat = TestFixtures.MakeEnemy("rat", maxHp: 10, attack: 1, defense: 0, goldReward: 8);
            var boar = TestFixtures.MakeEnemy("boar", maxHp: 20, attack: 2, defense: 1, goldReward: 18);

            var encounter = TestFixtures.MakeEncounter("들판", (rat, 3), (boar, 1));

            Assert.AreEqual(8 * 3 + 18, encounter.TotalGoldReward);
        }
    }
}
