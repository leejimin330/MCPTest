using FarmCreature.Core;
using FarmCreature.Data;
using NUnit.Framework;

namespace FarmCreature.Tests
{
    public class CreatureInstanceTests
    {
        private CreatureSpeciesData species;
        private CreatureInstance creature;

        [SetUp]
        public void SetUp()
        {
            species = TestFixtures.MakeSpecies("sprigling", "스프리글링", attack: 5, defense: 5, health: 20);
            creature = CreatureInstance.CreateNew(species);
        }

        [Test]
        public void CreateNew_CopiesBaseStatsFromSpecies()
        {
            Assert.AreEqual(5, creature.GetStat(StatType.Attack));
            Assert.AreEqual(5, creature.GetStat(StatType.Defense));
            Assert.AreEqual(20, creature.GetStat(StatType.Health));
        }

        [Test]
        public void CreateNew_UsesStartingCareValues()
        {
            Assert.AreEqual(0, creature.Exp);
            Assert.AreEqual(CreatureInstance.StartingFullness, creature.Fullness);
            Assert.AreEqual(CreatureInstance.StartingBond, creature.Bond);
        }

        [Test]
        public void CreateNew_DoesNotShareStatBlockWithSpecies()
        {
            creature.AddStat(StatType.Attack, 10);

            Assert.AreEqual(5, species.baseStats.attack, "종 에셋의 기본치가 변경되면 안 된다");
        }

        [Test]
        public void AddExp_Accumulates()
        {
            creature.AddExp(15);
            creature.AddExp(15);

            Assert.AreEqual(30, creature.Exp);
        }

        [Test]
        public void SetExp_ClampsToZero()
        {
            creature.SetExp(-5);

            Assert.AreEqual(0, creature.Exp);
        }

        [Test]
        public void AddStat_ClampsToMinimumOne()
        {
            creature.AddStat(StatType.Attack, -100);

            Assert.AreEqual(CreatureInstance.MinStat, creature.GetStat(StatType.Attack));
        }

        [Test]
        public void AddFullness_ClampsToMax()
        {
            creature.AddFullness(500);

            Assert.AreEqual(CreatureInstance.MaxCare, creature.Fullness);
        }

        [Test]
        public void AddFullness_ClampsToMin()
        {
            creature.AddFullness(-500);

            Assert.AreEqual(CreatureInstance.MinCare, creature.Fullness);
        }

        [Test]
        public void AddBond_ClampsToRange()
        {
            creature.AddBond(500);
            Assert.AreEqual(CreatureInstance.MaxCare, creature.Bond);

            creature.AddBond(-500);
            Assert.AreEqual(CreatureInstance.MinCare, creature.Bond);
        }

        [Test]
        public void ChangingSpecies_KeepsStatsAndCareValues()
        {
            creature.AddStat(StatType.Attack, 10);
            creature.AddExp(120);
            creature.AddFullness(-20);
            creature.AddBond(10);

            var evolved = TestFixtures.MakeSpecies("wildbeast", "와일드비스트", attack: 99, defense: 99, health: 99);
            creature.CurrentSpecies = evolved;

            Assert.AreSame(evolved, creature.CurrentSpecies);
            Assert.AreEqual(15, creature.GetStat(StatType.Attack), "진화해도 스탯은 그대로 이어받는다");
            Assert.AreEqual(120, creature.Exp);
            Assert.AreEqual(40, creature.Fullness);
            Assert.AreEqual(40, creature.Bond);
        }
    }
}
