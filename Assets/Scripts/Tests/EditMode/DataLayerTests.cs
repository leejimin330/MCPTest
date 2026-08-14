using FarmCreature.Data;
using NUnit.Framework;

namespace FarmCreature.Tests
{
    public class DataLayerTests
    {
        [Test]
        public void StatBlock_Get_ReturnsMatchingStat()
        {
            var block = new StatBlock { attack = 7, defense = 3, health = 25 };

            Assert.AreEqual(7, block.Get(StatType.Attack));
            Assert.AreEqual(3, block.Get(StatType.Defense));
            Assert.AreEqual(25, block.Get(StatType.Health));
        }

        [Test]
        public void SpeciesDatabase_Find_ReturnsMatchingSpecies()
        {
            var a = TestFixtures.MakeSpecies("sprigling");
            var b = TestFixtures.MakeSpecies("wildbeast");
            var db = TestFixtures.MakeSpeciesDatabase(a, b);

            Assert.AreSame(b, db.Find("wildbeast"));
        }

        [Test]
        public void SpeciesDatabase_Find_ReturnsNullForUnknownId()
        {
            var db = TestFixtures.MakeSpeciesDatabase(TestFixtures.MakeSpecies("sprigling"));

            Assert.IsNull(db.Find("nope"));
        }

        [Test]
        public void ItemDatabase_Find_ReturnsMatchingItem()
        {
            var feed = TestFixtures.MakeItem("hearty_feed", StatType.Attack, 3);
            var db = TestFixtures.MakeItemDatabase(feed);

            Assert.AreSame(feed, db.Find("hearty_feed"));
        }

        [Test]
        public void EvolutionRequirement_Factories_SetKindAndPayload()
        {
            var exp = EvolutionRequirement.ExpAtLeast(100);
            var stat = EvolutionRequirement.StatAtLeast(StatType.Attack, 15);
            var item = EvolutionRequirement.RequiresEquipped("bloom_stone");

            Assert.AreEqual(RequirementKind.ExpAtLeast, exp.kind);
            Assert.AreEqual(100, exp.value);

            Assert.AreEqual(RequirementKind.StatAtLeast, stat.kind);
            Assert.AreEqual(StatType.Attack, stat.stat);
            Assert.AreEqual(15, stat.value);

            Assert.AreEqual(RequirementKind.RequiresEquipped, item.kind);
            Assert.AreEqual("bloom_stone", item.itemId);
        }
    }
}
