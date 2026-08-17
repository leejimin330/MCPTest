using FarmCreature.Core;
using NUnit.Framework;

namespace FarmCreature.Tests
{
    /// <summary>
    /// 모든 씬 전환이 거치는 상태 전달자. 여기가 새면 <b>저장 버튼을 누르지 않은 진행이 날아간다</b> —
    /// 건물마다 씬이 생기면서 그 자리가 건물 수만큼 늘었기 때문에 규칙을 여기 한 곳에 못 박는다.
    ///
    /// 정적 상태는 테스트 사이에 살아남으므로 SetUp/TearDown 에서 반드시 비운다.
    /// </summary>
    public class SceneHandoffTests
    {
        [SetUp]
        public void SetUp()
        {
            SceneHandoff.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            SceneHandoff.Clear();
        }

        private static GameState MakeState()
        {
            var species = TestFixtures.MakeSpecies("sprigling", "스프리글링");
            var roster = new Roster();
            roster.Add(CreatureInstance.CreateNew(species));
            return new GameState(roster, new Inventory(), 30);
        }

        [Test]
        public void State_RoundTripsAsTheSameObject()
        {
            // 복사본이 아니라 같은 객체여야 한다 — 씬을 넘는 동안에도 한 덩어리로 이어져야
            // 저장 전 진행(먹이·훈련·파티 편성)이 그대로 남는다.
            var state = MakeState();

            SceneHandoff.Carry(state);

            Assert.AreSame(state, SceneHandoff.TakeState());
        }

        [Test]
        public void State_IsClearedOnceTaken()
        {
            SceneHandoff.Carry(MakeState());

            Assert.IsNotNull(SceneHandoff.TakeState());
            Assert.IsNull(SceneHandoff.TakeState(),
                "남겨두면 다음에 그 씬을 열 때 지난 상태가 되살아난다");
            Assert.IsFalse(SceneHandoff.HasState);
        }

        [Test]
        public void State_IsNullWhenNothingWasCarried()
        {
            // 씬을 단독으로 Play 한 경우다. 도착한 쪽이 세이브를 읽어야 한다.
            Assert.IsFalse(SceneHandoff.HasState);
            Assert.IsNull(SceneHandoff.TakeState());
        }

        [Test]
        public void Message_RoundTripsAndIsCleared()
        {
            SceneHandoff.Carry(MakeState(), "헛간에서 나왔다.");

            Assert.AreEqual("헛간에서 나왔다.", SceneHandoff.TakePendingMessage());
            Assert.IsNull(SceneHandoff.TakePendingMessage());
        }

        [Test]
        public void Message_IsNullWhenCarriedWithoutOne()
        {
            SceneHandoff.Carry(MakeState());

            Assert.IsNull(SceneHandoff.TakePendingMessage(),
                "없으면 도착한 씬이 알아서 로그를 정한다");
        }

        [Test]
        public void Carry_OverwritesThePreviousMessage()
        {
            // 두 번 맡기는 것은 실수지만, 남은 옛 문장이 새 상태와 함께 뜨는 것이 더 나쁘다.
            SceneHandoff.Carry(MakeState(), "첫 번째");
            SceneHandoff.Carry(MakeState(), null);

            Assert.IsNull(SceneHandoff.TakePendingMessage());
        }

        [Test]
        public void Clear_EmptiesStateAndMessage()
        {
            SceneHandoff.Carry(MakeState(), "무엇인가");

            SceneHandoff.Clear();

            Assert.IsFalse(SceneHandoff.HasState);
            Assert.IsNull(SceneHandoff.TakeState());
            Assert.IsNull(SceneHandoff.TakePendingMessage());
        }

        [Test]
        public void TakingTheMessage_DoesNotTakeTheState()
        {
            // 로그만 필요한 호출자가 상태까지 비워버리면 그 씬은 새 게임으로 시작한다.
            SceneHandoff.Carry(MakeState(), "헛간에서 나왔다.");

            SceneHandoff.TakePendingMessage();

            Assert.IsTrue(SceneHandoff.HasState);
        }
    }
}
