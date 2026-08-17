using FarmCreature.Core;
using NUnit.Framework;

namespace FarmCreature.Tests
{
    /// <summary>
    /// 재화와 진행도가 늘어나는 유일한 지점. 전투터가 씬이 되면서 <b>부르는 곳이 둘</b>
    /// (농장·전투터)이 됐으므로, 규칙이 여기 하나로 남아 있는지가 중요해졌다.
    ///
    /// 예전에는 <c>GameRoot</c> 의 private 메서드라 테스트가 없었다.
    /// </summary>
    public class BattleRewardTests
    {
        private static GameState MakeState(int gold)
        {
            var species = TestFixtures.MakeSpecies("sprigling", "스프리글링");
            var roster = new Roster();
            roster.Add(CreatureInstance.CreateNew(species));
            return new GameState(roster, new Inventory(), gold);
        }

        private static BattleResult Result(BattleConclusion conclusion, int gold, string stageId)
        {
            return new BattleResult
            {
                Conclusion = conclusion,
                GoldReward = gold,
                StageId = stageId
            };
        }

        [Test]
        public void Victory_AddsGoldAndMarksTheStage()
        {
            var state = MakeState(30);

            BattleReward.Apply(state, Result(BattleConclusion.Victory, 24, "field_1"));

            Assert.AreEqual(54, state.Gold);
            Assert.IsTrue(state.Progress.IsCleared("field_1"));
        }

        [Test]
        public void Victory_SaysSomethingOpenedOnlyTheFirstTime()
        {
            var state = MakeState(0);

            string first = BattleReward.Apply(state, Result(BattleConclusion.Victory, 10, "field_1"));
            string second = BattleReward.Apply(state, Result(BattleConclusion.Victory, 10, "field_1"));

            StringAssert.Contains("새 길이 열렸다", first);
            StringAssert.DoesNotContain("새 길이 열렸다", second,
                "이미 깬 스테이지를 다시 깼는데 새 길이 열렸다고 한다");
        }

        [Test]
        public void Victory_WithEmptyStageId_RecordsNoProgress()
        {
            // 연습 전투다. 재화는 주되 진행도는 남기지 않는다.
            var state = MakeState(0);

            BattleReward.Apply(state, Result(BattleConclusion.Victory, 8, string.Empty));

            Assert.AreEqual(8, state.Gold);
            Assert.AreEqual(0, state.Progress.ClearedStageIds.Count,
                "연습 전투가 진행도를 남기면 안 깬 스테이지가 열린다");
        }

        [Test]
        public void Defeat_ChangesNothing()
        {
            var state = MakeState(30);

            BattleReward.Apply(state, Result(BattleConclusion.Defeat, 24, "field_1"));

            Assert.AreEqual(30, state.Gold, "졌는데 보상이 들어갔다");
            Assert.IsFalse(state.Progress.IsCleared("field_1"));
        }

        [Test]
        public void Aborted_ChangesNothing()
        {
            var state = MakeState(30);

            BattleReward.Apply(state, Result(BattleConclusion.Aborted, 24, "field_1"));

            Assert.AreEqual(30, state.Gold, "물러났는데 보상이 들어갔다");
            Assert.IsFalse(state.Progress.IsCleared("field_1"));
        }

        [Test]
        public void NullResult_IsIgnored()
        {
            // 전투를 거치지 않고 그 씬을 연 경우다. 예외 없이 조용히 넘어가야 한다.
            var state = MakeState(30);

            Assert.IsNull(BattleReward.Apply(state, null));
            Assert.AreEqual(30, state.Gold);
        }

        [Test]
        public void NullState_IsIgnored()
        {
            Assert.IsNull(BattleReward.Apply(null, Result(BattleConclusion.Victory, 10, "field_1")));
        }
    }
}
