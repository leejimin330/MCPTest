using FarmCreature.Core;
using NUnit.Framework;

namespace FarmCreature.Tests
{
    /// <summary>
    /// 씬을 넘나드는 정적 전달자. <b>꺼내면 비워져야 한다</b> —
    /// 남아 있으면 다음에 전투터를 열었을 때 지난 전투의 유령이 되살아난다.
    ///
    /// 정적 상태는 테스트 사이에 살아남으므로 SetUp/TearDown 에서 반드시 비운다.
    /// </summary>
    public class BattleHandoffTests
    {
        [SetUp]
        public void SetUp()
        {
            BattleHandoff.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            BattleHandoff.Clear();
        }

        private static GameState MakeState()
        {
            var species = TestFixtures.MakeSpecies("sprigling", "스프리글링");
            var roster = new Roster();
            roster.Add(CreatureInstance.CreateNew(species));
            roster.JoinParty(roster.GetMember(0));
            return new GameState(roster, new Inventory(), 30);
        }

        [Test]
        public void Request_RoundTripsTheEncounter()
        {
            var encounter = TestFixtures.MakeEncounter("들판");

            BattleHandoff.SetRequest(MakeState(), encounter, null);
            var taken = BattleHandoff.TakeRequest();

            Assert.IsNotNull(taken);
            Assert.AreSame(encounter, taken.Encounter);
        }

        [Test]
        public void SetRequest_HandsTheStateToSceneHandoff()
        {
            // 짐과 상태를 따로 맡기게 하면 한쪽을 빠뜨릴 수 있고, 그 실패는
            // "돌아와 보니 진행이 날아갔다" 로만 드러난다. 그래서 여기서 함께 맡긴다.
            var state = MakeState();

            BattleHandoff.SetRequest(state, TestFixtures.MakeEncounter("들판"), null);

            Assert.AreSame(state, SceneHandoff.TakeState());
        }

        [Test]
        public void Request_IsClearedOnceTaken()
        {
            BattleHandoff.SetRequest(MakeState(), TestFixtures.MakeEncounter("들판"), null);

            Assert.IsNotNull(BattleHandoff.TakeRequest());
            Assert.IsNull(BattleHandoff.TakeRequest(), "두 번째로 꺼내면 비어 있어야 한다");
            Assert.IsFalse(BattleHandoff.HasRequest);
        }

        [Test]
        public void Request_IsNullWhenNothingWasSent()
        {
            Assert.IsFalse(BattleHandoff.HasRequest);
            Assert.IsNull(BattleHandoff.TakeRequest());
        }

        [Test]
        public void Result_RoundTripsConclusionAndReward()
        {
            BattleHandoff.SetResult(BattleConclusion.Victory, 24, MakeState(), "field_1");
            var taken = BattleHandoff.TakeResult();

            Assert.IsNotNull(taken);
            Assert.AreEqual(BattleConclusion.Victory, taken.Conclusion);
            Assert.AreEqual(24, taken.GoldReward);
        }

        [Test]
        public void SetResult_HandsTheStateToSceneHandoff()
        {
            var state = MakeState();

            BattleHandoff.SetResult(BattleConclusion.Victory, 24, state, "field_1");

            Assert.AreSame(state, SceneHandoff.TakeState(),
                "농장이 이 상태를 이어받아야 저장 전 진행이 남는다");
        }

        [Test]
        public void Result_IsClearedOnceTaken()
        {
            BattleHandoff.SetResult(BattleConclusion.Defeat, 0, MakeState(), "field_1");

            Assert.IsNotNull(BattleHandoff.TakeResult());
            Assert.IsNull(BattleHandoff.TakeResult(),
                "결과가 남아 있으면 농장에 돌아올 때마다 보상이 다시 들어간다");
            Assert.IsFalse(BattleHandoff.HasResult);
        }

        [Test]
        public void SetResult_DoesNotSpendTheReward()
        {
            // 재화는 농장 쪽에서만 더한다 (설계 3.3). 전달자는 액수만 나른다.
            var state = MakeState();

            BattleHandoff.SetResult(BattleConclusion.Victory, 24, state, "field_1");

            Assert.AreEqual(30, state.Gold, "전달자가 재화를 건드리면 지급 지점이 둘이 된다");
        }

        [Test]
        public void Clear_EmptiesBothSides()
        {
            BattleHandoff.SetRequest(MakeState(), TestFixtures.MakeEncounter("들판"), null);
            BattleHandoff.SetResult(BattleConclusion.Aborted, 0, MakeState(), "field_1");

            BattleHandoff.Clear();

            Assert.IsFalse(BattleHandoff.HasRequest);
            Assert.IsFalse(BattleHandoff.HasResult);
            Assert.IsFalse(SceneHandoff.HasState,
                "짐만 지우고 상태를 남기면 다음 씬이 지난 상태를 이어받는다");
        }

        [Test]
        public void Request_CarriesTheField()
        {
            var field = TestFixtures.MakeBattleMap();

            BattleHandoff.SetRequest(MakeState(), null, field);
            var taken = BattleHandoff.TakeRequest();

            Assert.AreSame(field, taken.Field, "전장은 맵이 정한다 — 요청에 실어 보낸다");
        }

        [Test]
        public void Result_CarriesTheStageId()
        {
            BattleHandoff.SetResult(BattleConclusion.Victory, 10, MakeState(), "field_1");
            var taken = BattleHandoff.TakeResult();

            Assert.AreEqual("field_1", taken.StageId, "농장이 이것으로 진행도를 기록한다");
        }
    }
}
