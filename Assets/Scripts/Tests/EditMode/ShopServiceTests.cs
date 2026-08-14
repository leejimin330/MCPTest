using FarmCreature.Core;
using FarmCreature.Data;
using NUnit.Framework;

namespace FarmCreature.Tests
{
    public class ShopServiceTests
    {
        private GameState StateWithGold(int gold)
        {
            var roster = new Roster();
            roster.Add(CreatureInstance.CreateNew(TestFixtures.MakeSpecies("sprigling")));
            return new GameState(roster, new Inventory(), gold);
        }

        [Test]
        public void Buy_SpendsGoldAndAddsToInventory()
        {
            var state = StateWithGold(100);
            var entry = TestFixtures.MakeShopEntry(ShopEntryKind.Equipment, "hearty_feed", 30);

            var result = ShopService.TryBuy(state, entry, "무쇠 발톱");

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(70, state.Gold);
            Assert.AreEqual(1, state.Inventory.GetCount("hearty_feed"));
            StringAssert.Contains("무쇠 발톱", result.Message);
        }

        [Test]
        public void Buy_Egg_AddsEggIdToInventory()
        {
            var state = StateWithGold(60);
            var entry = TestFixtures.MakeShopEntry(ShopEntryKind.Egg, "egg_sprigling", 60);

            Assert.IsTrue(ShopService.TryBuy(state, entry, "스프리글링 알").Success);
            Assert.AreEqual(0, state.Gold);
            Assert.AreEqual(1, state.Inventory.GetCount("egg_sprigling"));
        }

        [Test]
        public void Buy_WhenTooPoor_ChangesNothing()
        {
            var state = StateWithGold(29);
            var entry = TestFixtures.MakeShopEntry(ShopEntryKind.Equipment, "hearty_feed", 30);

            var result = ShopService.TryBuy(state, entry, "무쇠 발톱");

            Assert.IsFalse(result.Success);
            Assert.AreEqual(29, state.Gold, "실패하면 재화가 그대로여야 한다");
            Assert.AreEqual(0, state.Inventory.GetCount("hearty_feed"), "실패하면 물건도 들어오지 않는다");
        }

        [Test]
        public void Buy_RejectsNonPositivePrice()
        {
            var state = StateWithGold(100);
            var free = TestFixtures.MakeShopEntry(ShopEntryKind.Equipment, "hearty_feed", 0);

            Assert.IsFalse(ShopService.TryBuy(state, free, "무쇠 발톱").Success,
                "공짜 항목은 정상 상황이 아니라 데이터 실수다");
            Assert.AreEqual(100, state.Gold);
            Assert.AreEqual(0, state.Inventory.GetCount("hearty_feed"));
        }

        [Test]
        public void Buy_RejectsNullStateOrEntryOrEmptyTarget()
        {
            var state = StateWithGold(100);

            Assert.IsFalse(ShopService.TryBuy(null,
                TestFixtures.MakeShopEntry(ShopEntryKind.Equipment, "hearty_feed", 30), "무쇠 발톱").Success);
            Assert.IsFalse(ShopService.TryBuy(state, null, "무쇠 발톱").Success);
            Assert.IsFalse(ShopService.TryBuy(state,
                TestFixtures.MakeShopEntry(ShopEntryKind.Equipment, string.Empty, 30), "이름").Success);
            Assert.AreEqual(100, state.Gold);
        }

        [Test]
        public void Buy_Twice_Accumulates()
        {
            var state = StateWithGold(100);
            var entry = TestFixtures.MakeShopEntry(ShopEntryKind.Equipment, "hearty_feed", 30);

            ShopService.TryBuy(state, entry, "무쇠 발톱");
            ShopService.TryBuy(state, entry, "무쇠 발톱");

            Assert.AreEqual(40, state.Gold);
            Assert.AreEqual(2, state.Inventory.GetCount("hearty_feed"));
        }
    }
}
