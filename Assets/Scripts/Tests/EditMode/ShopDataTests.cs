using FarmCreature.Core;
using FarmCreature.Data;
using NUnit.Framework;

namespace FarmCreature.Tests
{
    public class ShopDataTests
    {
        [Test]
        public void EggDatabase_FindsById()
        {
            var egg = TestFixtures.MakeEgg("egg_sprigling", "스프리글링 알");
            var db = TestFixtures.MakeEggDatabase(egg);

            Assert.AreSame(egg, db.Find("egg_sprigling"));
        }

        [Test]
        public void EggDatabase_ReturnsNullForUnknownOrEmptyId()
        {
            var db = TestFixtures.MakeEggDatabase(TestFixtures.MakeEgg("egg_sprigling"));

            Assert.IsNull(db.Find("egg_none"));
            Assert.IsNull(db.Find(null));
            Assert.IsNull(db.Find(string.Empty));
        }

        [Test]
        public void EggId_UsesPrefix()
        {
            Assert.AreEqual("egg_", EggData.IdPrefix,
                "알 id 는 아이템 id 와 같은 인벤토리 키 공간을 쓴다 — 접두어로 갈라 둔다");
        }

        [Test]
        public void ShopStock_GetReturnsNullOutOfRange()
        {
            var stock = TestFixtures.MakeShopStock(
                TestFixtures.MakeShopEntry(ShopEntryKind.Equipment, "hearty_feed", 30));

            Assert.IsNotNull(stock.Get(0));
            Assert.IsNull(stock.Get(-1));
            Assert.IsNull(stock.Get(1), "버튼이 인덱스를 들고 있으므로 범위 밖은 조용히 null 이어야 한다");
        }

        [Test]
        public void ShopStock_CountMatchesEntries()
        {
            var stock = TestFixtures.MakeShopStock(
                TestFixtures.MakeShopEntry(ShopEntryKind.Equipment, "hearty_feed", 30),
                TestFixtures.MakeShopEntry(ShopEntryKind.Egg, "egg_sprigling", 60));

            Assert.AreEqual(2, stock.Count);
        }

        [Test]
        public void EggAndItemIds_ShareInventoryKeySpaceWithoutColliding()
        {
            var inventory = new Inventory();
            inventory.Add("hearty_feed", 2);
            inventory.Add("egg_sprigling", 1);

            Assert.AreEqual(2, inventory.GetCount("hearty_feed"));
            Assert.AreEqual(1, inventory.GetCount("egg_sprigling"));
        }
    }
}
