using FarmCreature.Core;
using FarmCreature.Data;
using NUnit.Framework;

namespace FarmCreature.Tests
{
    public class GameStateTests
    {
        private CreatureSpeciesData species;

        [SetUp]
        public void SetUp()
        {
            species = TestFixtures.MakeSpecies("sprigling", "스프리글링");
        }

        private CreatureInstance New()
        {
            return CreatureInstance.CreateNew(species);
        }

        private GameState StateWith(params CreatureInstance[] creatures)
        {
            var roster = new Roster();
            for (int i = 0; i < creatures.Length; i++)
            {
                roster.Add(creatures[i]);
            }

            return new GameState(roster, new Inventory(), 0);
        }

        [Test]
        public void NewState_TargetsFirstMember()
        {
            var a = New();
            var b = New();

            Assert.AreSame(a, StateWith(a, b).Target);
        }

        [Test]
        public void NewState_WithEmptyRoster_HasNoTarget()
        {
            Assert.IsNull(StateWith().Target);
        }

        [Test]
        public void SetTarget_RejectsOutsider()
        {
            var state = StateWith(New());
            var stranger = New();

            Assert.IsFalse(state.SetTarget(stranger), "헛간에 없는 케어는 육성 대상이 될 수 없다");
            Assert.AreNotSame(stranger, state.Target);
        }

        [Test]
        public void SetTarget_RejectsNull()
        {
            var state = StateWith(New());
            var before = state.Target;

            Assert.IsFalse(state.SetTarget(null));
            Assert.AreSame(before, state.Target);
        }

        [Test]
        public void AddGold_IgnoresNonPositive()
        {
            var state = StateWith(New());

            state.AddGold(30);
            state.AddGold(0);
            state.AddGold(-10);

            Assert.AreEqual(30, state.Gold);
        }

        [Test]
        public void TrySpendGold_OnlyWhenAffordable()
        {
            var state = StateWith(New());
            state.AddGold(50);

            Assert.IsFalse(state.TrySpendGold(51), "부족하면 아무것도 바꾸지 않는다");
            Assert.AreEqual(50, state.Gold);

            Assert.IsTrue(state.TrySpendGold(50));
            Assert.AreEqual(0, state.Gold);
        }

        [Test]
        public void Constructor_ClampsNegativeGold()
        {
            var roster = new Roster();
            roster.Add(New());

            Assert.AreEqual(0, new GameState(roster, new Inventory(), -99).Gold);
        }

        [Test]
        public void EnsureTarget_RestoresFirstMemberWhenTargetIsGone()
        {
            var a = New();
            var state = StateWith(a);

            // 대상이 비어 있는 상태를 흉내 낸다 (빈 로스터로 만든 뒤 채워지는 경우).
            var empty = StateWith();
            empty.EnsureTarget();
            Assert.IsNull(empty.Target);

            state.EnsureTarget();
            Assert.AreSame(a, state.Target, "정상 대상은 건드리지 않는다");
        }

        [Test]
        public void NewState_StartsWithEmptyProgress()
        {
            var state = new GameState(new Roster(), new Inventory(), 0);

            Assert.IsNotNull(state.Progress, "진행도는 null 이 아니라 빈 것이다");
            Assert.AreEqual(0, state.Progress.ClearedStageIds.Count);
        }
    }
}
