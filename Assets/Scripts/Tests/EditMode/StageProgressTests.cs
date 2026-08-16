using System.Collections.Generic;
using FarmCreature.Core;
using NUnit.Framework;

namespace FarmCreature.Tests
{
    public class StageProgressTests
    {
        [Test]
        public void NewProgress_HasNothingCleared()
        {
            var progress = new StageProgress();

            Assert.AreEqual(0, progress.ClearedStageIds.Count);
            Assert.IsFalse(progress.IsCleared("field_1"));
        }

        [Test]
        public void MarkCleared_RecordsIt()
        {
            var progress = new StageProgress();

            Assert.IsTrue(progress.MarkCleared("field_1"), "처음 깬 것이면 true");
            Assert.IsTrue(progress.IsCleared("field_1"));
            Assert.AreEqual(1, progress.ClearedStageIds.Count);
        }

        [Test]
        public void MarkCleared_Twice_ChangesNothing()
        {
            var progress = new StageProgress();
            progress.MarkCleared("field_1");

            Assert.IsFalse(progress.MarkCleared("field_1"), "이미 깬 것이면 false");
            Assert.AreEqual(1, progress.ClearedStageIds.Count, "중복 기록되면 안 된다");
        }

        [Test]
        public void MarkCleared_RejectsEmptyId()
        {
            var progress = new StageProgress();

            Assert.IsFalse(progress.MarkCleared(null));
            Assert.IsFalse(progress.MarkCleared(""));
            Assert.IsFalse(progress.MarkCleared("   "));
            Assert.AreEqual(0, progress.ClearedStageIds.Count,
                "무엇을 깼는지 알 수 없는 기록은 남기지 않는다");
        }

        [Test]
        public void ClearedOrder_IsPreserved()
        {
            var progress = new StageProgress();
            progress.MarkCleared("field_2");
            progress.MarkCleared("field_1");

            Assert.AreEqual("field_2", progress.ClearedStageIds[0], "깬 차례대로 남는다");
            Assert.AreEqual("field_1", progress.ClearedStageIds[1]);
        }

        [Test]
        public void Constructor_FiltersEmptyAndDuplicate()
        {
            var progress = new StageProgress(
                new List<string> { "field_1", "", "field_1", null, "field_2" });

            Assert.AreEqual(2, progress.ClearedStageIds.Count,
                "손으로 고친 세이브가 진행도를 오염시키면 안 된다");
            Assert.IsTrue(progress.IsCleared("field_1"));
            Assert.IsTrue(progress.IsCleared("field_2"));
        }
    }
}
