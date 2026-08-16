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

        // ---------- 겹침 ----------

        /// <summary>
        /// <b>한 틱의 이동량은 dt 에 정비례해야 한다</b> — 밀어내기 성분까지 포함해서다.
        /// 밀어내기를 <c>dt</c> 로 안 곱하면 그 성분이 두 배가 되지 않아 여기서 걸린다.
        ///
        /// 여러 틱으로 쪼개 비교하지 않는 이유: 밀어내는 힘이 위치에 의존하므로
        /// 오일러 적분 오차가 남아 <b>쪼갠 결과와 한 번에 간 결과는 원리상 같지 않다</b>.
        /// 한 틱만 비교하면 그 오차 없이 dt 스케일링만 정확히 본다.
        ///
        /// <b>첫 번째 아군만 본다.</b> 한 틱 안에서 유닛은 순차로 갱신되므로
        /// (기존 "아군 먼저, 그다음 적"과 같은 이유 — 순서를 고정해야 재현된다),
        /// 두 번째 유닛은 <b>이미 움직인</b> 첫 번째를 기준으로 밀려난다.
        /// 그 이동량은 dt 에 비례할 수 없다. 첫 번째 유닛은 모두가 제자리일 때 계산되므로 정확히 선형이다.
        /// </summary>
        [Test]
        public void OneTickDisplacement_ScalesLinearlyWithDeltaTime()
        {
            var hero = TestFixtures.MakeFighter("hero", attack: 10, defense: 0, health: 100,
                attackInterval: Never, cooldownSeconds: Never);
            var dummy = TestFixtures.MakeEnemy("dummy", maxHp: 500, attack: 0, defense: 0,
                attackInterval: Never);

            // 아군 둘을 minSpacing(0.9) 안으로 붙여 밀어내기가 반드시 걸리게 한다.
            var allyStart = new List<Vector2> { new Vector2(0f, 0f), new Vector2(0.2f, 0.1f) };
            var foeStart = new List<Vector2> { new Vector2(9f, 0f) };

            var single = new BattleSimulator(Party(hero, hero), new[] { dummy }, allyStart, foeStart);
            var doubled = new BattleSimulator(Party(hero, hero), new[] { dummy }, allyStart, foeStart);

            single.Tick(0.1f);
            doubled.Tick(0.2f);

            Vector2 moved = single.Allies[0].Position - allyStart[0];
            Vector2 movedTwice = doubled.Allies[0].Position - allyStart[0];

            Assert.AreEqual(moved.x * 2f, movedTwice.x, 0.0001f,
                "dt 를 두 배로 했는데 이동량이 두 배가 아니다 — 밀어내기를 dt 로 안 곱했다");
            Assert.AreEqual(moved.y * 2f, movedTwice.y, 0.0001f,
                "dt 를 두 배로 했는데 이동량이 두 배가 아니다 — 밀어내기를 dt 로 안 곱했다");

            // 밀어내기가 실제로 걸린 상태여야 이 테스트가 의미를 갖는다. y 로 밀려난 것이 그 증거다 —
            // 타겟은 정확히 +x 쪽에 있으므로 y 이동은 오직 밀어내기에서만 나온다.
            Assert.AreNotEqual(0f, moved.y,
                "밀어내기가 안 걸렸다 — dt 곱을 빠뜨려도 이 테스트가 못 잡는다");
        }

        [Test]
        public void ExactlyOverlappingUnits_AreNotPushed()
        {
            var hero = TestFixtures.MakeFighter("hero", attack: 10, defense: 0, health: 100,
                attackInterval: Never, cooldownSeconds: Never);
            var dummy = TestFixtures.MakeEnemy("dummy", maxHp: 500, attack: 0, defense: 0,
                attackInterval: Never);

            var battle = new BattleSimulator(
                Party(hero, hero), new[] { dummy },
                Clustered(2),                                   // 아군 둘이 정확히 같은 좌표
                new List<Vector2> { new Vector2(9f, 0f) });

            battle.Tick(0.5f);

            Assert.AreEqual(battle.Allies[0].Position, battle.Allies[1].Position,
                "겹친 좌표는 방향이 없다 — 지어내면 숨은 난수가 된다");
        }

        /// <summary>
        /// 여럿이 한 상대에게 몰리면 <b>전원이 사거리에서 멈추므로</b> 밀어내기가 이동 중인
        /// 유닛에만 걸리면 아무도 안 밀려나 그대로 포개진다. 2026-08-16 에 Play 로 확인한
        /// 실제 증상이다 — 들쥐 셋 중 둘이 0.20 까지 겹쳐 한 마리가 완전히 숨었다 (HANDOFF 4.8).
        /// </summary>
        [Test]
        public void UnitsConvergingOnOneTarget_DoNotStack()
        {
            // 아무도 죽지 않게 체력을 크게 잡는다. 여기서 볼 것은 피해가 아니라 배치다.
            var hero = TestFixtures.MakeFighter("hero", attack: 1, defense: 0, health: 100000,
                attackInterval: Never, cooldownSeconds: Never);
            var foe = TestFixtures.MakeEnemy("foe", maxHp: 100000, attack: 0, defense: 0,
                attackInterval: Never);

            var battle = new BattleSimulator(
                Party(hero), new[] { foe, foe, foe },
                new List<Vector2> { Vector2.zero },
                new List<Vector2>
                {
                    new Vector2(4f, 0f), new Vector2(4.1f, 0.1f), new Vector2(4.2f, -0.1f)
                });

            for (int i = 0; i < 300; i++)
            {
                battle.Tick(0.1f);
            }

            for (int i = 0; i < battle.Enemies.Count; i++)
            {
                for (int j = i + 1; j < battle.Enemies.Count; j++)
                {
                    float gap = Vector2.Distance(
                        battle.Enemies[i].Position, battle.Enemies[j].Position);

                    Assert.Greater(gap, 0.8f,
                        "적 " + i + " 와 " + j + " 가 겹쳤다 (간격 " + gap.ToString("F2") + ") — "
                        + "사거리에서 멈춘 유닛이 서로를 밀어내지 않는다");
                }
            }

            // 겹침을 풀겠다고 사거리 밖으로 밀려나면 안 된다 — 그러면 다시 다가오며 떨린다.
            for (int i = 0; i < battle.Enemies.Count; i++)
            {
                float toTarget = Vector2.Distance(
                    battle.Enemies[i].Position, battle.Allies[0].Position);

                Assert.LessOrEqual(toTarget, battle.Enemies[i].AttackRange + 0.001f,
                    "적 " + i + " 가 사거리 밖으로 밀려났다 (" + toTarget.ToString("F2") + ")");
            }
        }

        // ---------- 스킬 사거리 ----------

        [Test]
        public void HealSkill_IgnoresDistance()
        {
            var healer = TestFixtures.MakeFighter("healer", attack: 10, defense: 0, health: 100,
                attackInterval: Never, effect: SkillEffect.Heal,
                cooldownSeconds: 1f, multiplier: 1.5f);
            var wounded = TestFixtures.MakeFighter("wounded", attack: 1, defense: 0, health: 100,
                attackInterval: Never, cooldownSeconds: Never);
            var biter = TestFixtures.MakeEnemy("biter", maxHp: 500, attack: 20, defense: 0,
                attackInterval: 0.5f);

            var battle = new BattleSimulator(
                Party(healer, wounded), new[] { biter },
                new List<Vector2> { new Vector2(-40f, 0f), Vector2.zero },   // 회복사는 멀리
                new List<Vector2> { Vector2.zero });

            // 원점의 부상자가 맞아 체력이 깎이고, 멀리 있는 회복사가 회복시킨다.
            for (int i = 0; i < 20; i++)
            {
                battle.Tick(0.1f);
            }

            StringAssert.Contains("회복", string.Join("\n", battle.Log),
                "멀리 있는 회복사가 한 번도 회복시키지 않았다");
        }

        [Test]
        public void DamageSkill_RespectsRange()
        {
            var caster = TestFixtures.MakeFighter("caster", attack: 10, defense: 0, health: 100,
                attackInterval: Never, effect: SkillEffect.Damage,
                cooldownSeconds: 0.5f, multiplier: 2f);
            var dummy = TestFixtures.MakeEnemy("dummy", maxHp: 500, attack: 0, defense: 0,
                attackInterval: Never);

            var battle = new BattleSimulator(
                Party(caster), new[] { dummy },
                new List<Vector2> { Vector2.zero },
                new List<Vector2> { new Vector2(500f, 0f) });   // 걸어도 못 닿는 거리

            battle.Tick(2f);   // 쿨다운(0.5)은 네 번 찼다

            Assert.AreEqual(500, battle.Enemies[0].Hp, "사거리 밖인데 스킬이 나갔다");
        }

        // ---------- 결정성과 배치의 영향 ----------

        [Test]
        public void MinSpacing_IsTighterThanAttackRange()
        {
            Assert.Less(BattleSimulator.DefaultMinSpacing, BattleUnit.DefaultAttackRange,
                "분리 간격이 사거리보다 넓으면 유닛이 붙기 전에 밀려나 영원히 못 때린다");
        }

        /// <summary>
        /// 맵 데이터의 <c>minSpacing</c> 은 <b>스폰 간격</b>용이라 사거리와 무관하게 정해진다 —
        /// 실제로 <c>FieldMap.asset</c> 은 1.4 이고 사거리는 1.2 다. 자르지 않으면 유닛이
        /// 사거리 안에 들기 전에 서로 밀어내 <b>전투가 조용히 멎는다</b>
        /// (2026-08-16 PlayMode 스모크가 실제로 이렇게 실패했다).
        /// </summary>
        [Test]
        public void WideMapSpacing_DoesNotPreventFighting()
        {
            var hero = TestFixtures.MakeFighter("hero", attack: 10, defense: 0, health: 100,
                attackInterval: 0.5f, cooldownSeconds: Never);
            var dummy = TestFixtures.MakeEnemy("dummy", maxHp: 500, attack: 0, defense: 0,
                attackInterval: Never);

            var battle = new BattleSimulator(
                Party(hero, hero), new[] { dummy },
                new List<Vector2> { new Vector2(-3f, 0f), new Vector2(-3f, 1.5f) },
                new List<Vector2> { new Vector2(3f, 0f) },
                minSpacing: 1.4f);   // FieldMap.asset 의 실제 값. 사거리(1.2)보다 넓다.

            for (int i = 0; i < 100; i++)
            {
                battle.Tick(0.1f);
            }

            Assert.Less(battle.Enemies[0].Hp, 500,
                "밀어내기가 접근을 이겨 아무도 못 때렸다 — minSpacing 을 사거리 기준으로 자르지 않았다");
        }

        [Test]
        public void SamePositions_GiveTheSameLog()
        {
            var hero = TestFixtures.MakeFighter("hero", attack: 6, defense: 1, health: 40,
                attackInterval: 1f, cooldownSeconds: 3f);
            var foe = TestFixtures.MakeEnemy("foe", maxHp: 30, attack: 4, defense: 2,
                attackInterval: 1.1f, goldReward: 5);

            var allyAt = new List<Vector2> { new Vector2(-2f, 1f), new Vector2(-1f, -2f) };
            var foeAt = new List<Vector2> { new Vector2(3f, 0f), new Vector2(4f, 2f) };

            var first = new BattleSimulator(Party(hero, hero), new[] { foe, foe }, allyAt, foeAt);
            var second = new BattleSimulator(Party(hero, hero), new[] { foe, foe }, allyAt, foeAt);

            for (int i = 0; i < 200; i++)
            {
                first.Tick(0.1f);
                second.Tick(0.1f);
            }

            Assert.AreEqual(first.Outcome, second.Outcome);
            CollectionAssert.AreEqual(first.Log, second.Log,
                "같은 좌표인데 로그가 다르다 — 시뮬레이터 안에 난수가 들어갔다");
        }

        [Test]
        public void DifferentPositions_CanFlipTheResult()
        {
            var hero = TestFixtures.MakeFighter("hero", attack: 7, defense: 1, health: 30,
                attackInterval: 1f, cooldownSeconds: Never);
            var foe = TestFixtures.MakeEnemy("foe", maxHp: 22, attack: 5, defense: 1,
                attackInterval: 1f, goldReward: 5);

            // 붙어서 시작: 아군 둘이 함께 싸운다
            var together = new BattleSimulator(
                Party(hero, hero), new[] { foe, foe },
                new List<Vector2> { new Vector2(0f, 0f), new Vector2(1f, 0f) },
                new List<Vector2> { new Vector2(2f, 0f), new Vector2(3f, 0f) });

            // 갈라져서 시작: 한 명이 멀리 떨어져 늦게 합류한다
            var split = new BattleSimulator(
                Party(hero, hero), new[] { foe, foe },
                new List<Vector2> { new Vector2(0f, 0f), new Vector2(-30f, 0f) },
                new List<Vector2> { new Vector2(2f, 0f), new Vector2(3f, 0f) });

            for (int i = 0; i < 400; i++)
            {
                together.Tick(0.1f);
                split.Tick(0.1f);
            }

            Assert.AreNotEqual(together.ElapsedSeconds, split.ElapsedSeconds,
                "배치를 바꿔도 전투가 똑같이 흘렀다 — 좌표가 결과에 영향을 주지 않는다");
        }
    }
}
