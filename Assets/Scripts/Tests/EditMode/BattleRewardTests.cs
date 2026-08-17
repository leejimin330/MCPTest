using System.Collections.Generic;
using FarmCreature.Core;
using FarmCreature.Data;
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

        private static BattleResult Result(
            BattleConclusion conclusion, int gold, string stageId, params DropAward[] drops)
        {
            return new BattleResult
            {
                Conclusion = conclusion,
                GoldReward = gold,
                StageId = stageId,
                Drops = drops.Length == 0 ? null : new List<DropAward>(drops)
            };
        }

        private static DropAward Award(string itemId, int count)
        {
            return new DropAward { ItemId = itemId, Count = count };
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

        // ---- 드랍 ----

        [Test]
        public void Victory_PutsDropsIntoTheInventory()
        {
            // 드랍이 들어가는 지점도 여기 하나뿐이다 — 전투 씬은 굴려진 결과만 실어 보낸다.
            var state = MakeState(0);

            BattleReward.Apply(state, Result(BattleConclusion.Victory, 5, "field_1",
                Award("bloom_stone", 2), Award("egg_sprigling", 1)));

            Assert.AreEqual(2, state.Inventory.GetCount("bloom_stone"));
            Assert.AreEqual(1, state.Inventory.GetCount("egg_sprigling"));
        }

        [Test]
        public void Victory_MentionsTheDropsByName()
        {
            var state = MakeState(0);
            var items = TestFixtures.MakeItemDatabase(
                TestFixtures.MakeItem("bloom_stone", StatType.Health, 5, "블룸 스톤"));

            string message = BattleReward.Apply(
                state, Result(BattleConclusion.Victory, 5, "field_1", Award("bloom_stone", 1)),
                items, null);

            StringAssert.Contains("블룸 스톤", message);
            StringAssert.DoesNotContain("bloom_stone", message, "로그에 id 가 그대로 나왔다");
        }

        [Test]
        public void Victory_WithoutDrops_SaysNothingAboutThem()
        {
            var state = MakeState(0);

            string message = BattleReward.Apply(state, Result(BattleConclusion.Victory, 5, "field_1"));

            StringAssert.DoesNotContain("얻었다", message);
        }

        [Test]
        public void Defeat_GivesNoDropsEvenIfSomeAreAttached()
        {
            // 재화와 같은 규칙이다 — 지면 아무것도 안 준다.
            var state = MakeState(0);

            BattleReward.Apply(state, Result(BattleConclusion.Defeat, 10, "field_1",
                Award("bloom_stone", 1)));

            Assert.AreEqual(0, state.Inventory.GetCount("bloom_stone"));
        }

        [Test]
        public void BrokenAwards_AreIgnored()
        {
            var state = MakeState(0);

            string message = BattleReward.Apply(state, Result(BattleConclusion.Victory, 5, "field_1",
                Award(null, 1), Award("ok", 0)));

            Assert.AreEqual(0, state.Inventory.GetCount("ok"));
            StringAssert.DoesNotContain("얻었다", message);
        }
    }
}
