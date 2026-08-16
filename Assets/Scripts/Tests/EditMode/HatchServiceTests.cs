using FarmCreature.Core;
using FarmCreature.Data;
using NUnit.Framework;

namespace FarmCreature.Tests
{
    public class HatchServiceTests
    {
        private CreatureSpeciesData sprigling;
        private EggData egg;

        [SetUp]
        public void SetUp()
        {
            sprigling = TestFixtures.MakeSpecies("sprigling", "스프리글링", 5, 4, 20);
            egg = TestFixtures.MakeEgg("egg_sprigling", "스프리글링 알", sprigling);
        }

        private GameState EmptyState()
        {
            return new GameState(new Roster(), new Inventory(), 0);
        }

        private GameState StateWithEggs(int eggCount)
        {
            var state = EmptyState();
            state.Inventory.Add(egg.eggId, eggCount);
            return state;
        }

        [Test]
        public void Hatch_ConsumesEggAndAddsCreature()
        {
            var state = StateWithEggs(1);

            var result = HatchService.TryHatch(state, egg);

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(0, state.Inventory.GetCount("egg_sprigling"));
            Assert.AreEqual(1, state.Roster.Count);
            StringAssert.Contains("스프리글링", result.Message);
        }

        [Test]
        public void Hatch_ProducesSpeciesFromEggWithBaseStats()
        {
            var state = StateWithEggs(1);

            HatchService.TryHatch(state, egg);
            var hatched = state.Roster.GetMember(0);

            Assert.AreSame(sprigling, hatched.CurrentSpecies);
            Assert.AreEqual(5, hatched.GetBaseStat(StatType.Attack));
            Assert.AreEqual(4, hatched.GetBaseStat(StatType.Defense));
            Assert.AreEqual(20, hatched.GetBaseStat(StatType.Health));
            Assert.AreEqual(0, hatched.Exp, "갓 난 케어는 경험치가 0 이다");
        }

        [Test]
        public void Hatch_WithoutEgg_ChangesNothing()
        {
            var state = EmptyState();

            var result = HatchService.TryHatch(state, egg);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(0, state.Roster.Count, "실패하면 케어가 늘지 않는다");
            Assert.AreEqual(0, state.Inventory.GetCount("egg_sprigling"));
        }

        [Test]
        public void Hatch_WhenBarnFull_KeepsTheEgg()
        {
            var state = StateWithEggs(1);
            for (int i = 0; i < Roster.MaxMembers; i++)
            {
                state.Roster.Add(CreatureInstance.CreateNew(sprigling));
            }

            var result = HatchService.TryHatch(state, egg);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(Roster.MaxMembers, state.Roster.Count);
            Assert.AreEqual(1, state.Inventory.GetCount("egg_sprigling"),
                "자리가 없어 실패했으면 알은 그대로 남아야 한다");
            StringAssert.Contains("헛간", result.Message);
        }

        [Test]
        public void Hatch_EggWithoutTarget_IsRejected()
        {
            var broken = TestFixtures.MakeEgg("egg_broken", "빈 알");
            var state = EmptyState();
            state.Inventory.Add("egg_broken", 1);

            var result = HatchService.TryHatch(state, broken);

            Assert.IsFalse(result.Success, "무엇이 나오는지 없는 알은 데이터 실수다");
            Assert.AreEqual(1, state.Inventory.GetCount("egg_broken"));
            Assert.AreEqual(0, state.Roster.Count);
        }

        [Test]
        public void Hatch_RejectsNullStateOrEgg()
        {
            var state = StateWithEggs(1);

            Assert.IsFalse(HatchService.TryHatch(null, egg).Success);
            Assert.IsFalse(HatchService.TryHatch(state, null).Success);
            Assert.AreEqual(1, state.Inventory.GetCount("egg_sprigling"));
            Assert.AreEqual(0, state.Roster.Count);
        }

        [Test]
        public void Hatch_Twice_ProducesTwoDistinctCreatures()
        {
            var state = StateWithEggs(2);

            Assert.IsTrue(HatchService.TryHatch(state, egg).Success);
            Assert.IsTrue(HatchService.TryHatch(state, egg).Success);

            Assert.AreEqual(2, state.Roster.Count);
            Assert.AreEqual(0, state.Inventory.GetCount("egg_sprigling"));
            Assert.AreNotSame(state.Roster.GetMember(0), state.Roster.GetMember(1),
                "같은 인스턴스를 두 번 넣으면 Roster.Add 가 거부한다");
        }
    }
}
