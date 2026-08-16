using System.Collections.Generic;
using FarmCreature.Core;
using FarmCreature.Data;
using NUnit.Framework;

namespace FarmCreature.Tests
{
    public class StageUnlockEvaluatorTests
    {
        private EnemyData rat;

        [SetUp]
        public void SetUp()
        {
            rat = TestFixtures.MakeEnemy("rat", maxHp: 8, attack: 3, defense: 1);
        }

        private BattleEncounter Stage(string id, params StageUnlock[] unlock)
        {
            return TestFixtures.MakeStage(id, id, new List<StageUnlock>(unlock), (rat, 1));
        }

        private static StageUnlock Previous()
        {
            return TestFixtures.Unlock(StageUnlockKind.PreviousStageCleared);
        }

        [Test]
        public void NoConditions_IsAlwaysUnlocked()
        {
            var map = TestFixtures.MakeStageMap("맵", Stage("a"));

            Assert.IsTrue(StageUnlockEvaluator.IsUnlocked(
                map, 0, new StageProgress(), new Inventory()));
        }

        [Test]
        public void PreviousStageCleared_BlocksUntilPreviousIsDone()
        {
            var map = TestFixtures.MakeStageMap("맵", Stage("a"), Stage("b", Previous()));
            var progress = new StageProgress();

            Assert.IsFalse(StageUnlockEvaluator.IsUnlocked(map, 1, progress, new Inventory()));

            progress.MarkCleared("a");
            Assert.IsTrue(StageUnlockEvaluator.IsUnlocked(map, 1, progress, new Inventory()));
        }

        [Test]
        public void PreviousStageCleared_OnFirstStage_IsUnlocked()
        {
            // 데이터 실수로 첫 스테이지에 붙어도 맵 전체가 막히면 안 된다 (설계 3.2).
            var map = TestFixtures.MakeStageMap("맵", Stage("a", Previous()));

            Assert.IsTrue(StageUnlockEvaluator.IsUnlocked(
                map, 0, new StageProgress(), new Inventory()));
        }

        [Test]
        public void StageCleared_LooksAtTheNamedStage()
        {
            var gate = TestFixtures.Unlock(StageUnlockKind.StageCleared, stageId: "boss");
            var map = TestFixtures.MakeStageMap("맵", Stage("a"), Stage("b", gate));
            var progress = new StageProgress();

            Assert.IsFalse(StageUnlockEvaluator.IsUnlocked(map, 1, progress, new Inventory()));

            progress.MarkCleared("boss");
            Assert.IsTrue(StageUnlockEvaluator.IsUnlocked(map, 1, progress, new Inventory()));
        }

        [Test]
        public void RequiresItem_ChecksInventoryCount()
        {
            var needsKey = TestFixtures.Unlock(
                StageUnlockKind.RequiresItem, itemId: "old_key", count: 2);
            var map = TestFixtures.MakeStageMap("맵", Stage("a", needsKey));
            var inventory = new Inventory();

            Assert.IsFalse(StageUnlockEvaluator.IsUnlocked(map, 0, new StageProgress(), inventory));

            inventory.Add("old_key", 1);
            Assert.IsFalse(StageUnlockEvaluator.IsUnlocked(map, 0, new StageProgress(), inventory),
                "개수가 모자라면 아직 잠긴다");

            inventory.Add("old_key", 1);
            Assert.IsTrue(StageUnlockEvaluator.IsUnlocked(map, 0, new StageProgress(), inventory));
        }

        [Test]
        public void Conditions_AreCombinedWithAnd()
        {
            var needsKey = TestFixtures.Unlock(
                StageUnlockKind.RequiresItem, itemId: "old_key", count: 1);
            var map = TestFixtures.MakeStageMap("맵", Stage("a"), Stage("b", Previous(), needsKey));
            var progress = new StageProgress();
            var inventory = new Inventory();

            progress.MarkCleared("a");
            Assert.IsFalse(StageUnlockEvaluator.IsUnlocked(map, 1, progress, inventory),
                "하나라도 안 맞으면 잠긴다");

            inventory.Add("old_key", 1);
            Assert.IsTrue(StageUnlockEvaluator.IsUnlocked(map, 1, progress, inventory));
        }

        [Test]
        public void LockReason_IsNullWhenUnlocked()
        {
            var map = TestFixtures.MakeStageMap("맵", Stage("a"));

            Assert.IsNull(StageUnlockEvaluator.LockReason(
                map, 0, new StageProgress(), new Inventory(), null));
        }

        [Test]
        public void LockReason_NamesThePreviousStage()
        {
            var map = TestFixtures.MakeStageMap("맵", Stage("들판 1"), Stage("들판 2", Previous()));

            string reason = StageUnlockEvaluator.LockReason(
                map, 1, new StageProgress(), new Inventory(), null);

            StringAssert.Contains("들판 1", reason, "무엇을 하면 열리는지 알려줘야 한다");
        }

        [Test]
        public void LockReason_UsesItemDisplayName()
        {
            var key = TestFixtures.MakeItem("old_key", StatType.Attack, 0, "낡은 열쇠");
            var db = TestFixtures.MakeItemDatabase(key);
            var needsKey = TestFixtures.Unlock(
                StageUnlockKind.RequiresItem, itemId: "old_key", count: 1);
            var map = TestFixtures.MakeStageMap("맵", Stage("a", needsKey));

            string reason = StageUnlockEvaluator.LockReason(
                map, 0, new StageProgress(), new Inventory(), db);

            StringAssert.Contains("낡은 열쇠", reason, "id 가 아니라 사람이 읽을 이름을 쓴다");
        }
    }
}
