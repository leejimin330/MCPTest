using FarmCreature.Core;
using FarmCreature.Data;
using NUnit.Framework;

namespace FarmCreature.Tests
{
    public class CareActionsTests
    {
        private CreatureInstance creature;
        private Inventory inventory;
        private ItemData feedItem;

        [SetUp]
        public void SetUp()
        {
            creature = CreatureInstance.CreateNew(
                TestFixtures.MakeSpecies("sprigling", "스프리글링", attack: 5, defense: 5, health: 20));
            inventory = new Inventory();
            feedItem = TestFixtures.MakeItem("hearty_feed", StatType.Attack, 3, "든든한 사료");
        }

        // ---- 인벤토리 ----

        [Test]
        public void Inventory_Add_ThenGetCount()
        {
            inventory.Add("bloom_stone", 2);

            Assert.AreEqual(2, inventory.GetCount("bloom_stone"));
            Assert.IsTrue(inventory.Has("bloom_stone"));
        }

        [Test]
        public void Inventory_UnknownId_IsZeroAndNotHeld()
        {
            Assert.AreEqual(0, inventory.GetCount("nope"));
            Assert.IsFalse(inventory.Has("nope"));
        }

        [Test]
        public void Inventory_TryConsume_FailsWhenInsufficient()
        {
            inventory.Add("bloom_stone", 1);

            Assert.IsFalse(inventory.TryConsume("bloom_stone", 2));
            Assert.AreEqual(1, inventory.GetCount("bloom_stone"), "실패 시 개수가 줄면 안 된다");
        }

        [Test]
        public void Inventory_ItemIds_KeepsInsertionOrder()
        {
            inventory.Add("a", 1);
            inventory.Add("b", 1);
            inventory.Add("a", 1);

            CollectionAssert.AreEqual(new[] { "a", "b" }, inventory.ItemIds);
        }

        // ---- 먹이주기 ----

        [Test]
        public void Feed_RaisesFullnessBondAndExp()
        {
            var result = CareActions.Feed(creature);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(CreatureInstance.StartingFullness + CareActions.FeedFullness, creature.Fullness);
            Assert.AreEqual(CreatureInstance.StartingBond + CareActions.FeedBond, creature.Bond);
            Assert.AreEqual(CareActions.FeedExp, creature.Exp);
        }

        [Test]
        public void Feed_FailsWhenAlreadyFull_AndChangesNothing()
        {
            creature.SetFullness(CreatureInstance.MaxCare);
            int bondBefore = creature.Bond;
            int expBefore = creature.Exp;

            var result = CareActions.Feed(creature);

            Assert.IsFalse(result.Success);
            Assert.IsNotEmpty(result.Message);
            Assert.AreEqual(CreatureInstance.MaxCare, creature.Fullness);
            Assert.AreEqual(bondBefore, creature.Bond);
            Assert.AreEqual(expBefore, creature.Exp);
        }

        // ---- 훈련하기 ----

        [Test]
        public void Train_RaisesChosenStatAndExp_CostsFullnessAndBond()
        {
            var result = CareActions.Train(creature, StatType.Attack);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(5 + CareActions.TrainStatGain, creature.GetStat(StatType.Attack));
            Assert.AreEqual(5, creature.GetStat(StatType.Defense), "다른 스탯은 안 오른다");
            Assert.AreEqual(CareActions.TrainExp, creature.Exp);
            Assert.AreEqual(CreatureInstance.StartingFullness - CareActions.TrainFullnessCost, creature.Fullness);
            Assert.AreEqual(CreatureInstance.StartingBond - CareActions.TrainBondCost, creature.Bond);
        }

        [Test]
        public void Train_FailsBelowMinimumFullness_AndChangesNothing()
        {
            creature.SetFullness(CareActions.TrainMinFullness - 1);
            int attackBefore = creature.GetStat(StatType.Attack);
            int expBefore = creature.Exp;
            int bondBefore = creature.Bond;
            int fullnessBefore = creature.Fullness;

            var result = CareActions.Train(creature, StatType.Attack);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(attackBefore, creature.GetStat(StatType.Attack));
            Assert.AreEqual(expBefore, creature.Exp);
            Assert.AreEqual(bondBefore, creature.Bond);
            Assert.AreEqual(fullnessBefore, creature.Fullness);
        }

        [Test]
        public void Train_SucceedsExactlyAtMinimumFullness()
        {
            creature.SetFullness(CareActions.TrainMinFullness);

            Assert.IsTrue(CareActions.Train(creature, StatType.Attack).Success);
        }

        // ---- 장착 ----

        [Test]
        public void Equip_AddsBonusToStat_AndTakesItemOutOfInventory()
        {
            inventory.Add("hearty_feed", 2);

            var result = CareActions.Equip(creature, inventory, feedItem);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(5, creature.GetBaseStat(StatType.Attack), "기본 스탯은 그대로");
            Assert.AreEqual(5 + 3, creature.GetStat(StatType.Attack), "표시 스탯에는 보너스가 더해진다");
            Assert.AreSame(feedItem, creature.EquippedItem);
            Assert.AreEqual(1, inventory.GetCount("hearty_feed"), "장착한 1개는 인벤토리에서 빠진다");
        }

        [Test]
        public void Equip_DoesNotGrantExp()
        {
            inventory.Add("hearty_feed", 1);

            CareActions.Equip(creature, inventory, feedItem);

            Assert.AreEqual(0, creature.Exp, "장착은 육성 행위가 아니므로 경험치를 주지 않는다");
            Assert.AreEqual(CreatureInstance.StartingBond + CareActions.EquipBond, creature.Bond);
        }

        [Test]
        public void Unequip_RemovesBonus_AndReturnsItemToInventory()
        {
            inventory.Add("hearty_feed", 1);
            CareActions.Equip(creature, inventory, feedItem);

            var result = CareActions.Unequip(creature, inventory);

            Assert.IsTrue(result.Success);
            Assert.IsNull(creature.EquippedItem);
            Assert.AreEqual(5, creature.GetStat(StatType.Attack), "빼면 보너스가 사라진다");
            Assert.AreEqual(1, inventory.GetCount("hearty_feed"), "아이템은 소모되지 않고 돌아온다");
        }

        [Test]
        public void Equip_SwapsPreviousItemBackIntoInventory()
        {
            var shell = TestFixtures.MakeItem("iron_grain", StatType.Defense, 3, "돌 껍질");
            inventory.Add("hearty_feed", 1);
            inventory.Add("iron_grain", 1);

            CareActions.Equip(creature, inventory, feedItem);
            var result = CareActions.Equip(creature, inventory, shell);

            Assert.IsTrue(result.Success);
            Assert.AreSame(shell, creature.EquippedItem);
            Assert.AreEqual(1, inventory.GetCount("hearty_feed"), "이전 장비는 인벤토리로 돌아온다");
            Assert.AreEqual(0, inventory.GetCount("iron_grain"));
            Assert.AreEqual(5, creature.GetStat(StatType.Attack), "이전 보너스는 사라진다");
            Assert.AreEqual(5 + 3, creature.GetStat(StatType.Defense));
        }

        [Test]
        public void Equip_OnlyOneSlot()
        {
            var shell = TestFixtures.MakeItem("iron_grain", StatType.Defense, 3, "돌 껍질");
            inventory.Add("hearty_feed", 1);
            inventory.Add("iron_grain", 1);

            CareActions.Equip(creature, inventory, feedItem);
            CareActions.Equip(creature, inventory, shell);

            Assert.AreEqual(1, CreatureInstance.EquipmentSlots);
            Assert.AreEqual(5, creature.GetStat(StatType.Attack), "두 개를 동시에 낄 수 없다");
        }

        [Test]
        public void Equip_FailsWhenNotHeld_AndChangesNothing()
        {
            var result = CareActions.Equip(creature, inventory, feedItem);

            Assert.IsFalse(result.Success);
            Assert.IsNull(creature.EquippedItem);
            Assert.AreEqual(5, creature.GetStat(StatType.Attack));
            Assert.AreEqual(CreatureInstance.StartingBond, creature.Bond);
        }

        [Test]
        public void Equip_FailsWhenAlreadyEquipped_AndDoesNotConsume()
        {
            inventory.Add("hearty_feed", 2);
            CareActions.Equip(creature, inventory, feedItem);

            var result = CareActions.Equip(creature, inventory, feedItem);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(1, inventory.GetCount("hearty_feed"), "중복 장착 시도가 개수를 줄이면 안 된다");
        }

        [Test]
        public void Equip_FailsForNullItem()
        {
            Assert.IsFalse(CareActions.Equip(creature, inventory, null).Success);
        }

        [Test]
        public void Unequip_FailsWhenNothingEquipped()
        {
            Assert.IsFalse(CareActions.Unequip(creature, inventory).Success);
        }
    }
}
