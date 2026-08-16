using System.Collections.Generic;
using FarmCreature.Core;
using FarmCreature.Data;
using NUnit.Framework;
using UnityEngine;

namespace FarmCreature.Tests
{
    /// <summary>
    /// 자동전투는 난수를 쓰지 않으므로 시간을 원하는 만큼 흘려보내며 결과를 못박을 수 있다.
    /// 여기서 검증하는 것은 규칙이고, 화면 표시는 SceneIntegrityCheck 가 본다.
    /// </summary>
    public class BattleSimulatorTests
    {
        /// <summary>사실상 발동하지 않는 간격/쿨타임. 검사하려는 행동만 남기는 데 쓴다.</summary>
        private const float Never = 9999f;

        private static List<CreatureInstance> Party(params CreatureSpeciesData[] species)
        {
            var list = new List<CreatureInstance>();
            for (int i = 0; i < species.Length; i++)
            {
                list.Add(CreatureInstance.CreateNew(species[i]));
            }

            return list;
        }

        /// <summary>
        /// 전원을 원점에 겹쳐 세운다. 거리가 0 이라 <b>항상 사거리 안</b>이므로 이동 구간이 없고,
        /// 타겟 동률이 목록 순서로 깨져 예전 <c>FirstAlive</c> 와 같은 유닛을 고른다 (설계 6.1).
        /// 좌표를 보지 않는 기존 테스트는 전부 이것을 쓴다.
        /// </summary>
        private static List<Vector2> Clustered(int count)
        {
            var points = new List<Vector2>();
            for (int i = 0; i < count; i++)
            {
                points.Add(Vector2.zero);
            }

            return points;
        }

        // ---- 피해 공식 ----

        [Test]
        public void DamageAgainst_SubtractsHalfDefense()
        {
            var attacker = new BattleUnit { Attack = 10 };
            var target = new BattleUnit { Defense = 4 };

            Assert.AreEqual(8, BattleSimulator.DamageAgainst(attacker, target));
        }

        [Test]
        public void DamageAgainst_IsAtLeastOne()
        {
            var attacker = new BattleUnit { Attack = 1 };
            var target = new BattleUnit { Defense = 100 };

            Assert.AreEqual(1, BattleSimulator.DamageAgainst(attacker, target),
                "방어가 아무리 높아도 완전 무적이 되지는 않는다");
        }

        // ---- 승패 ----

        [Test]
        public void Victory_WhenAllEnemiesFall()
        {
            var hero = TestFixtures.MakeFighter("hero", attack: 100, defense: 0, health: 100,
                attackInterval: 1f, cooldownSeconds: Never);
            var dummy = TestFixtures.MakeEnemy("dummy", maxHp: 24, attack: 0, defense: 0,
                attackInterval: Never, goldReward: 15);

            var battle = new BattleSimulator(Party(hero), new[] { dummy }, Clustered(1), Clustered(1));
            Assert.AreEqual(BattleOutcome.InProgress, battle.Outcome);

            battle.Tick(1f);

            Assert.AreEqual(BattleOutcome.Victory, battle.Outcome);
            Assert.AreEqual(15, battle.GoldReward);
        }

        [Test]
        public void Defeat_WhenAllAlliesFall()
        {
            var weakling = TestFixtures.MakeFighter("weakling", attack: 1, defense: 0, health: 5,
                attackInterval: Never, cooldownSeconds: Never);
            var brute = TestFixtures.MakeEnemy("brute", maxHp: 100, attack: 100, defense: 0,
                attackInterval: 1f);

            var battle = new BattleSimulator(Party(weakling), new[] { brute }, Clustered(1), Clustered(1));
            battle.Tick(1f);

            Assert.AreEqual(BattleOutcome.Defeat, battle.Outcome);
            Assert.AreEqual(0, battle.GoldReward, "졌으면 보상이 없다");
        }

        [Test]
        public void EmptyParty_LosesImmediately()
        {
            var dummy = TestFixtures.MakeEnemy("dummy", maxHp: 10, attack: 1, defense: 0);

            var battle = new BattleSimulator(new List<CreatureInstance>(), new[] { dummy }, Clustered(0), Clustered(1));

            Assert.AreEqual(BattleOutcome.Defeat, battle.Outcome,
                "밖에 나온 케어가 없으면 전투가 성립하지 않는다");
        }

        [Test]
        public void Tick_AfterOutcome_ChangesNothing()
        {
            var hero = TestFixtures.MakeFighter("hero", attack: 100, defense: 0, health: 100,
                attackInterval: 1f, cooldownSeconds: Never);
            var dummy = TestFixtures.MakeEnemy("dummy", maxHp: 10, attack: 0, defense: 0,
                attackInterval: Never, goldReward: 7);

            var battle = new BattleSimulator(Party(hero), new[] { dummy }, Clustered(1), Clustered(1));
            battle.Tick(1f);

            int logLines = battle.Log.Count;
            float elapsed = battle.ElapsedSeconds;

            battle.Tick(5f);

            Assert.AreEqual(BattleOutcome.Victory, battle.Outcome);
            Assert.AreEqual(7, battle.GoldReward, "보상이 두 번 계산되지 않는다");
            Assert.AreEqual(logLines, battle.Log.Count);
            Assert.AreEqual(elapsed, battle.ElapsedSeconds);
        }

        // ---- 평타 / 스킬 ----

        [Test]
        public void BasicAttack_FiresOnItsInterval()
        {
            var hero = TestFixtures.MakeFighter("hero", attack: 10, defense: 0, health: 100,
                attackInterval: 2f, cooldownSeconds: Never);
            var wall = TestFixtures.MakeEnemy("wall", maxHp: 1000, attack: 0, defense: 0,
                attackInterval: Never);

            var battle = new BattleSimulator(Party(hero), new[] { wall }, Clustered(1), Clustered(1));
            var target = battle.Enemies[0];

            battle.Tick(1f);
            Assert.AreEqual(1000, target.Hp, "간격이 차기 전에는 때리지 않는다");

            battle.Tick(1f);
            Assert.AreEqual(990, target.Hp, "간격이 차면 평타 1회");
        }

        [Test]
        public void Skill_FiresOnCooldown_AndScalesDamage()
        {
            var hero = TestFixtures.MakeFighter("hero", attack: 10, defense: 0, health: 100,
                attackInterval: Never, effect: SkillEffect.Damage,
                cooldownSeconds: 3f, multiplier: 2f);
            var wall = TestFixtures.MakeEnemy("wall", maxHp: 1000, attack: 0, defense: 0,
                attackInterval: Never);

            var battle = new BattleSimulator(Party(hero), new[] { wall }, Clustered(1), Clustered(1));
            var target = battle.Enemies[0];

            battle.Tick(2.9f);
            Assert.AreEqual(1000, target.Hp, "쿨타임 전에는 안 나간다");

            battle.Tick(0.1f);
            Assert.AreEqual(980, target.Hp, "평타 10 의 2배가 들어간다");
        }

        [Test]
        public void HealSkill_RestoresLowestAlly_AndNeverExceedsMax()
        {
            var healer = TestFixtures.MakeFighter("healer", attack: 10, defense: 0, health: 100,
                attackInterval: Never, effect: SkillEffect.Heal,
                cooldownSeconds: 2f, multiplier: 1.5f);
            var biter = TestFixtures.MakeEnemy("biter", maxHp: 1000, attack: 20, defense: 0,
                attackInterval: 1f);

            var battle = new BattleSimulator(Party(healer), new[] { biter }, Clustered(1), Clustered(1));
            var ally = battle.Allies[0];

            battle.Tick(1f);
            Assert.AreEqual(80, ally.Hp, "적에게 20 맞는다");

            // 쿨타임이 차서 회복(+15)이 먼저 들어가고, 그 뒤 적의 공격(-20)이 들어간다.
            battle.Tick(1f);
            Assert.AreEqual(75, ally.Hp);

            Assert.LessOrEqual(ally.Hp, ally.MaxHp, "최대 체력을 넘지 않는다");
        }

        [Test]
        public void Heal_DoesNotReviveTheFallen()
        {
            var unit = new BattleUnit { MaxHp = 10, Hp = 0 };
            unit.Heal(5);

            Assert.AreEqual(0, unit.Hp);
            Assert.IsFalse(unit.IsAlive);
        }

        // ---- 스탯 반영 ----

        [Test]
        public void FromCreature_UsesEquipmentBonus()
        {
            var species = TestFixtures.MakeFighter("hero", attack: 5, defense: 5, health: 20);
            var creature = CreatureInstance.CreateNew(species);
            var claw = TestFixtures.MakeItem("claw", StatType.Attack, 3);

            var inventory = new Inventory();
            inventory.Add("claw", 1);
            Assert.IsTrue(CareActions.Equip(creature, inventory, claw).Success);

            var unit = BattleUnit.FromCreature(creature);

            Assert.AreEqual(8, unit.Attack, "장비를 끼면 전투가 실제로 세져야 한다");
            Assert.AreEqual(20, unit.MaxHp);
            Assert.AreEqual(unit.MaxHp, unit.Hp);
        }

        // ---- 긴 프레임 방어 ----

        [Test]
        public void HugeDeltaTime_DoesNotLoopForever()
        {
            var hero = TestFixtures.MakeFighter("hero", attack: 1, defense: 0, health: 100,
                attackInterval: 0.001f, cooldownSeconds: Never);
            var wall = TestFixtures.MakeEnemy("wall", maxHp: 100000, attack: 0, defense: 0,
                attackInterval: Never);

            var battle = new BattleSimulator(Party(hero), new[] { wall }, Clustered(1), Clustered(1));

            // 한 틱에 이론상 1만 번 때릴 수 있는 dt 를 넘겨도 상한에서 멈춘다.
            battle.Tick(10f);

            Assert.Greater(battle.Enemies[0].Hp, 99900,
                "한 틱에 무한히 행동하면 프레임이 끊길 때 전투가 순간이동한다");
        }

        // ---- 재현성 ----

        [Test]
        public void SameInputAndTicks_ProduceSameResult()
        {
            var species = TestFixtures.MakeFighter("hero", attack: 7, defense: 3, health: 40,
                attackInterval: 1.1f, cooldownSeconds: 4f, multiplier: 2f);
            var enemy = TestFixtures.MakeEnemy("foe", maxHp: 60, attack: 6, defense: 2,
                attackInterval: 1.3f);

            int first = RunToEnd(species, enemy);
            int second = RunToEnd(species, enemy);

            Assert.AreEqual(first, second, "난수가 없으므로 같은 입력이면 같은 결과여야 한다");
        }

        private static int RunToEnd(CreatureSpeciesData species, EnemyData enemy)
        {
            var battle = new BattleSimulator(Party(species), new[] { enemy }, Clustered(1), Clustered(1));

            int ticks = 0;
            while (battle.Outcome == BattleOutcome.InProgress && ticks < 10000)
            {
                battle.Tick(0.05f);
                ticks++;
            }

            return ticks;
        }

        // ---------- 위치·이동속도·사거리 ----------

        [Test]
        public void FromCreature_GivesDefaultMovementValues()
        {
            var species = TestFixtures.MakeSpecies("sprigling", "스프리글링", 5, 4, 20);
            var unit = BattleUnit.FromCreature(CreatureInstance.CreateNew(species));

            Assert.AreEqual(BattleUnit.DefaultMoveSpeed, unit.MoveSpeed, 0.001f);
            Assert.AreEqual(BattleUnit.DefaultAttackRange, unit.AttackRange, 0.001f);
            Assert.AreEqual(Vector2.zero, unit.Position, "위치는 시뮬레이터가 배치한다");
        }

        [Test]
        public void FromEnemy_GivesDefaultMovementValues()
        {
            var enemy = TestFixtures.MakeEnemy("rat", maxHp: 8, attack: 3, defense: 1,
                attackInterval: 1.6f, goldReward: 4);
            var unit = BattleUnit.FromEnemy(enemy);

            Assert.AreEqual(BattleUnit.DefaultMoveSpeed, unit.MoveSpeed, 0.001f);
            Assert.AreEqual(BattleUnit.DefaultAttackRange, unit.AttackRange, 0.001f);
        }

        [Test]
        public void Constructor_PlacesUnitsAtGivenPositions()
        {
            var hero = TestFixtures.MakeFighter("hero", attack: 10, defense: 0, health: 100,
                attackInterval: Never, cooldownSeconds: Never);
            var dummy = TestFixtures.MakeEnemy("dummy", maxHp: 50, attack: 0, defense: 0,
                attackInterval: Never);

            var battle = new BattleSimulator(
                Party(hero), new[] { dummy },
                new List<Vector2> { new Vector2(1f, 2f) },
                new List<Vector2> { new Vector2(-3f, 4f) });

            Assert.AreEqual(new Vector2(1f, 2f), battle.Allies[0].Position);
            Assert.AreEqual(new Vector2(-3f, 4f), battle.Enemies[0].Position);
        }

        // ---------- 타겟과 사거리 ----------

        [Test]
        public void OutOfRange_NoOneTakesDamage()
        {
            var hero = TestFixtures.MakeFighter("hero", attack: 10, defense: 0, health: 100,
                attackInterval: 0.5f, cooldownSeconds: Never);
            var dummy = TestFixtures.MakeEnemy("dummy", maxHp: 50, attack: 5, defense: 0,
                attackInterval: 0.5f);

            var battle = new BattleSimulator(
                Party(hero), new[] { dummy },
                new List<Vector2> { Vector2.zero },
                new List<Vector2> { new Vector2(50f, 0f) });

            battle.Tick(1f);

            Assert.AreEqual(50, battle.Enemies[0].Hp, "사거리 밖인데 맞았다");
            Assert.AreEqual(battle.Allies[0].MaxHp, battle.Allies[0].Hp, "사거리 밖인데 때렸다");
        }

        [Test]
        public void Targets_TheNearestEnemy()
        {
            var hero = TestFixtures.MakeFighter("hero", attack: 10, defense: 0, health: 100,
                attackInterval: 0.5f, cooldownSeconds: Never);
            var far = TestFixtures.MakeEnemy("far", maxHp: 50, attack: 0, defense: 0,
                attackInterval: Never);
            var near = TestFixtures.MakeEnemy("near", maxHp: 50, attack: 0, defense: 0,
                attackInterval: Never);

            var battle = new BattleSimulator(
                Party(hero), new[] { far, near },      // 목록 순서는 먼 쪽이 앞
                new List<Vector2> { Vector2.zero },
                new List<Vector2> { new Vector2(0.9f, 0f), new Vector2(0.3f, 0f) });

            battle.Tick(1f);

            Assert.AreEqual(50, battle.Enemies[0].Hp, "먼 쪽이 맞았다 — 목록 순서를 따라간 것이다");
            Assert.Less(battle.Enemies[1].Hp, 50, "가까운 쪽이 맞아야 한다");
        }

        [Test]
        public void EqualDistance_BreaksTieByListOrder()
        {
            var hero = TestFixtures.MakeFighter("hero", attack: 10, defense: 0, health: 100,
                attackInterval: 0.5f, cooldownSeconds: Never);
            var first = TestFixtures.MakeEnemy("first", maxHp: 50, attack: 0, defense: 0,
                attackInterval: Never);
            var second = TestFixtures.MakeEnemy("second", maxHp: 50, attack: 0, defense: 0,
                attackInterval: Never);

            var battle = new BattleSimulator(
                Party(hero), new[] { first, second },
                Clustered(1), Clustered(2));       // 셋 다 원점 — 거리가 전부 0

            battle.Tick(1f);

            Assert.Less(battle.Enemies[0].Hp, 50, "동률은 목록에서 앞선 쪽이 맞아야 한다");
            Assert.AreEqual(50, battle.Enemies[1].Hp);
        }

        // ---------- 이동 ----------

        [Test]
        public void WalksIntoRange_ThenFights()
        {
            var hero = TestFixtures.MakeFighter("hero", attack: 10, defense: 0, health: 100,
                attackInterval: 0.5f, cooldownSeconds: Never);
            var dummy = TestFixtures.MakeEnemy("dummy", maxHp: 500, attack: 0, defense: 0,
                attackInterval: Never);

            var battle = new BattleSimulator(
                Party(hero), new[] { dummy },
                new List<Vector2> { Vector2.zero },
                new List<Vector2> { new Vector2(6f, 0f) });

            // 6 유닛 거리, 속도 2/초 → 사거리(1.2)까지 약 2.4초. 넉넉히 굴린다.
            for (int i = 0; i < 40; i++)
            {
                battle.Tick(0.1f);
            }

            float distance = (battle.Enemies[0].Position - battle.Allies[0].Position).magnitude;
            Assert.LessOrEqual(distance, battle.Allies[0].AttackRange + 0.01f, "붙지 못했다");
            Assert.Less(battle.Enemies[0].Hp, 500, "붙었는데 때리지 않았다");
        }

        [Test]
        public void Retargets_WhenACloserEnemyArrives()
        {
            var hero = TestFixtures.MakeFighter("hero", attack: 10, defense: 0, health: 100,
                attackInterval: 0.5f, cooldownSeconds: Never);
            var far = TestFixtures.MakeEnemy("far", maxHp: 500, attack: 0, defense: 0,
                attackInterval: Never);
            var rusher = TestFixtures.MakeEnemy("rusher", maxHp: 500, attack: 0, defense: 0,
                attackInterval: Never);

            var battle = new BattleSimulator(
                Party(hero), new[] { far, rusher },
                new List<Vector2> { Vector2.zero },
                new List<Vector2> { new Vector2(3f, 0f), new Vector2(-8f, 0f) });

            // 처음엔 far(3) 가 가깝다. rusher 는 −8 에서 다가온다.
            // 충분히 굴리면 rusher 가 더 가까워지는 시점이 온다.
            for (int i = 0; i < 100; i++)
            {
                battle.Tick(0.1f);
            }

            Assert.Less(battle.Enemies[1].Hp, 500,
                "달려든 적이 한 대도 안 맞았다 — 타겟을 다시 안 골랐다");
        }
    }
}
