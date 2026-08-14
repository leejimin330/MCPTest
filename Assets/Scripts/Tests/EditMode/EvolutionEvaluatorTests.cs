using System.Collections.Generic;
using FarmCreature.Core;
using FarmCreature.Data;
using NUnit.Framework;

namespace FarmCreature.Tests
{
    public class EvolutionEvaluatorTests
    {
        private CreatureSpeciesData baseSpecies;
        private CreatureSpeciesData wildbeast;
        private CreatureSpeciesData guardian;
        private CreatureInstance creature;
        private Inventory inventory;

        [SetUp]
        public void SetUp()
        {
            baseSpecies = TestFixtures.MakeSpecies("sprigling", "스프리글링", attack: 5, defense: 5, health: 20);
            wildbeast = TestFixtures.MakeSpecies("wildbeast", "와일드비스트", attack: 15, defense: 8, health: 30);
            guardian = TestFixtures.MakeSpecies("blossom_guardian", "블로섬 가디언", attack: 8, defense: 15, health: 35);
            creature = CreatureInstance.CreateNew(baseSpecies);
            inventory = new Inventory();
        }

        private static EvolutionCondition Condition(
            CreatureSpeciesData result, params EvolutionRequirement[] requirements)
        {
            return new EvolutionCondition
            {
                result = result,
                requirements = new List<EvolutionRequirement>(requirements)
            };
        }

        [Test]
        public void TryEvolve_ReturnsNullWhenSpeciesHasNoEvolutions()
        {
            Assert.IsNull(EvolutionEvaluator.TryEvolve(creature, inventory));
        }

        [Test]
        public void TryEvolve_ReturnsNullWhenExpBelowThreshold()
        {
            baseSpecies.evolutions.Add(Condition(wildbeast, EvolutionRequirement.ExpAtLeast(100)));
            creature.AddExp(99);

            Assert.IsNull(EvolutionEvaluator.TryEvolve(creature, inventory));
        }

        [Test]
        public void TryEvolve_ReturnsResultWhenExpMet()
        {
            baseSpecies.evolutions.Add(Condition(wildbeast, EvolutionRequirement.ExpAtLeast(100)));
            creature.AddExp(100);

            Assert.AreSame(wildbeast, EvolutionEvaluator.TryEvolve(creature, inventory));
        }

        [Test]
        public void TryEvolve_RequiresAllRequirements_AndedTogether()
        {
            baseSpecies.evolutions.Add(Condition(
                wildbeast,
                EvolutionRequirement.ExpAtLeast(100),
                EvolutionRequirement.StatAtLeast(StatType.Attack, 15)));

            creature.AddExp(200);
            Assert.IsNull(EvolutionEvaluator.TryEvolve(creature, inventory), "스탯 미달이면 진화 불가");

            creature.SetStat(StatType.Attack, 15);
            Assert.AreSame(wildbeast, EvolutionEvaluator.TryEvolve(creature, inventory));
        }

        [Test]
        public void TryEvolve_RequiresEquipped_HoldingIsNotEnough()
        {
            var stone = TestFixtures.MakeItem("bloom_stone", StatType.Health, 5, "블룸 스톤");

            baseSpecies.evolutions.Add(Condition(
                guardian,
                EvolutionRequirement.ExpAtLeast(100),
                EvolutionRequirement.RequiresEquipped("bloom_stone")));
            creature.AddExp(100);

            // 보유만 해서는 안 된다.
            inventory.Add("bloom_stone", 1);
            Assert.IsNull(EvolutionEvaluator.TryEvolve(creature, inventory), "가지고만 있으면 진화하지 않는다");

            Assert.IsTrue(CareActions.Equip(creature, inventory, stone).Success);

            Assert.AreSame(guardian, EvolutionEvaluator.TryEvolve(creature, inventory));
        }

        [Test]
        public void TryEvolve_RequiresEquipped_FailsWhenDifferentItemEquipped()
        {
            var stone = TestFixtures.MakeItem("bloom_stone", StatType.Health, 5, "블룸 스톤");
            var claw = TestFixtures.MakeItem("iron_claw", StatType.Attack, 3, "무쇠 발톱");

            baseSpecies.evolutions.Add(Condition(
                guardian,
                EvolutionRequirement.ExpAtLeast(100),
                EvolutionRequirement.RequiresEquipped("bloom_stone")));
            creature.AddExp(100);

            inventory.Add("bloom_stone", 1);
            inventory.Add("iron_claw", 1);
            Assert.IsTrue(CareActions.Equip(creature, inventory, claw).Success);

            Assert.IsNull(EvolutionEvaluator.TryEvolve(creature, inventory), "다른 것을 끼면 조건 미충족");

            Assert.IsTrue(CareActions.Equip(creature, inventory, stone).Success);
            Assert.AreSame(guardian, EvolutionEvaluator.TryEvolve(creature, inventory));
        }

        [Test]
        public void TryEvolve_StatRequirement_CountsEquipmentBonus()
        {
            var claw = TestFixtures.MakeItem("iron_claw", StatType.Attack, 3, "무쇠 발톱");

            baseSpecies.evolutions.Add(Condition(
                wildbeast,
                EvolutionRequirement.StatAtLeast(StatType.Attack, 8)));

            Assert.IsNull(EvolutionEvaluator.TryEvolve(creature, inventory), "기본 공격 5 로는 부족");

            inventory.Add("iron_claw", 1);
            Assert.IsTrue(CareActions.Equip(creature, inventory, claw).Success);

            Assert.AreSame(wildbeast, EvolutionEvaluator.TryEvolve(creature, inventory),
                "장착 보너스를 더한 8 로 조건을 넘겨야 한다");
        }

        [Test]
        public void TryEvolve_PrefersEarlierConditionWhenBothSatisfied()
        {
            baseSpecies.evolutions.Add(Condition(
                wildbeast,
                EvolutionRequirement.ExpAtLeast(100),
                EvolutionRequirement.StatAtLeast(StatType.Attack, 15)));
            baseSpecies.evolutions.Add(Condition(
                guardian,
                EvolutionRequirement.ExpAtLeast(100),
                EvolutionRequirement.RequiresEquipped("bloom_stone")));

            var stone = TestFixtures.MakeItem("bloom_stone", StatType.Health, 5, "블룸 스톤");
            creature.AddExp(100);
            creature.SetStat(StatType.Attack, 15);
            inventory.Add("bloom_stone", 1);
            Assert.IsTrue(CareActions.Equip(creature, inventory, stone).Success);

            Assert.AreSame(wildbeast, EvolutionEvaluator.TryEvolve(creature, inventory));
        }

        [Test]
        public void TryEvolve_SkipsConditionWithNullResult()
        {
            baseSpecies.evolutions.Add(Condition(null, EvolutionRequirement.ExpAtLeast(0)));
            baseSpecies.evolutions.Add(Condition(guardian, EvolutionRequirement.ExpAtLeast(0)));

            Assert.AreSame(guardian, EvolutionEvaluator.TryEvolve(creature, inventory));
        }

        [Test]
        public void TryEvolve_EmptyRequirementList_CountsAsSatisfied()
        {
            baseSpecies.evolutions.Add(Condition(wildbeast));

            Assert.AreSame(wildbeast, EvolutionEvaluator.TryEvolve(creature, inventory));
        }

        [Test]
        public void TryEvolve_ReturnsNullAfterEvolvingToTerminalSpecies()
        {
            baseSpecies.evolutions.Add(Condition(wildbeast, EvolutionRequirement.ExpAtLeast(0)));
            creature.CurrentSpecies = wildbeast;

            Assert.IsNull(EvolutionEvaluator.TryEvolve(creature, inventory), "진화형은 더 진화하지 않는다");
        }
    }
}
