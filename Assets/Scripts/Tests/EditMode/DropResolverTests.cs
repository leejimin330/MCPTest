using System.Collections.Generic;
using FarmCreature.Core;
using FarmCreature.Data;
using NUnit.Framework;

namespace FarmCreature.Tests
{
    /// <summary>
    /// 드랍 확률을 굴리는 유일한 곳. <b>seed 를 주입받으므로 완전히 결정적이다</b> —
    /// 시뮬레이터가 난수를 안 쓴다는 규율을 지키려고 확률을 전투 밖으로 뺐다.
    /// </summary>
    public class DropResolverTests
    {
        private static EnemyData EnemyWithDrops(string id, params DropEntry[] drops)
        {
            var enemy = TestFixtures.MakeEnemy(id, maxHp: 10, attack: 1, defense: 0);
            enemy.drops = new List<DropEntry>(drops);
            return enemy;
        }

        private static DropEntry Drop(string itemId, int chance, int count = 1)
        {
            return new DropEntry { itemId = itemId, chancePercent = chance, count = count };
        }

        [Test]
        public void HundredPercent_AlwaysDrops()
        {
            var foes = new List<EnemyData> { EnemyWithDrops("rat", Drop("key", 100)) };

            var awards = DropResolver.Resolve(foes, seed: 1);

            Assert.AreEqual(1, awards.Count);
            Assert.AreEqual("key", awards[0].ItemId);
            Assert.AreEqual(1, awards[0].Count);
        }

        [Test]
        public void ZeroPercent_NeverDrops()
        {
            var foes = new List<EnemyData> { EnemyWithDrops("rat", Drop("key", 0)) };

            Assert.AreEqual(0, DropResolver.Resolve(foes, seed: 1).Count);
        }

        [Test]
        public void EachEnemyRollsSeparately()
        {
            // 한 마리씩 펼친 목록을 받는다 — 3마리면 세 번 굴린다.
            var rat = EnemyWithDrops("rat", Drop("hide", 100));
            var foes = new List<EnemyData> { rat, rat, rat };

            var awards = DropResolver.Resolve(foes, seed: 1);

            Assert.AreEqual(1, awards.Count, "같은 id 는 한 줄로 합쳐야 한다");
            Assert.AreEqual(3, awards[0].Count, "세 마리분이 합쳐지지 않았다");
        }

        [Test]
        public void SameSeed_GivesTheSameResult()
        {
            // 재현되지 않으면 밸런스를 잴 수 없다.
            var foes = new List<EnemyData>
            {
                EnemyWithDrops("rat", Drop("a", 50), Drop("b", 50)),
                EnemyWithDrops("boar", Drop("c", 50))
            };

            var first = DropResolver.Resolve(foes, seed: 12345);
            var second = DropResolver.Resolve(foes, seed: 12345);

            Assert.AreEqual(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
            {
                Assert.AreEqual(first[i].ItemId, second[i].ItemId);
                Assert.AreEqual(first[i].Count, second[i].Count);
            }
        }

        [Test]
        public void DifferentSeeds_DoNotAllAgree()
        {
            // 확률이 실제로 굴려지는가. 늘 같은 결과면 seed 를 안 쓰고 있다는 뜻이다.
            var foes = new List<EnemyData> { EnemyWithDrops("rat", Drop("coin", 50)) };

            bool sawDrop = false;
            bool sawNothing = false;

            for (int seed = 1; seed <= 30; seed++)
            {
                if (DropResolver.Resolve(foes, seed).Count > 0)
                {
                    sawDrop = true;
                }
                else
                {
                    sawNothing = true;
                }
            }

            Assert.IsTrue(sawDrop, "30개 seed 를 굴려도 한 번도 안 나왔다");
            Assert.IsTrue(sawNothing, "30개 seed 를 굴려도 늘 나왔다 — 확률이 안 먹는다");
        }

        [Test]
        public void MultipleItemsFromOneEnemy_AreListedSeparately()
        {
            var foes = new List<EnemyData>
            {
                EnemyWithDrops("golem", Drop("core", 100, 2), Drop("shard", 100, 3))
            };

            var awards = DropResolver.Resolve(foes, seed: 7);

            Assert.AreEqual(2, awards.Count);
            Assert.AreEqual("core", awards[0].ItemId);
            Assert.AreEqual(2, awards[0].Count);
            Assert.AreEqual("shard", awards[1].ItemId);
            Assert.AreEqual(3, awards[1].Count);
        }

        [Test]
        public void BrokenEntries_AreIgnored()
        {
            // 데이터 실수로 죽지 않아야 한다. 빈 id·0개는 굴리지도 않는다.
            var foes = new List<EnemyData>
            {
                EnemyWithDrops("rat", Drop(null, 100), Drop("", 100), Drop("ok", 100, 0), null)
            };

            Assert.AreEqual(0, DropResolver.Resolve(foes, seed: 1).Count);
        }

        [Test]
        public void EmptyOrNullInput_IsSafe()
        {
            Assert.AreEqual(0, DropResolver.Resolve(null, seed: 1).Count);
            Assert.AreEqual(0, DropResolver.Resolve(new List<EnemyData>(), seed: 1).Count);
            Assert.AreEqual(0, DropResolver.Resolve(new List<EnemyData> { null }, seed: 1).Count);
        }

        [Test]
        public void EnemyWithNoDropTable_DropsNothing()
        {
            // 드랍이 없는 적이 기본이다 — 목록이 비어 있으면 아무것도 나오지 않는다.
            var plain = TestFixtures.MakeEnemy("plain", maxHp: 10, attack: 1, defense: 0);

            Assert.AreEqual(0, DropResolver.Resolve(new List<EnemyData> { plain }, seed: 1).Count);
        }
    }
}
