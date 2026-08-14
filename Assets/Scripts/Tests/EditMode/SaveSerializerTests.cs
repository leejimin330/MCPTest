using FarmCreature.Core;
using FarmCreature.Data;
using NUnit.Framework;

namespace FarmCreature.Tests
{
    public class SaveSerializerTests
    {
        private const string Stamp = "2026-08-13T01:40:00Z";

        private CreatureSpeciesData sprigling;
        private SpeciesDatabase speciesDb;
        private ItemData stone;
        private ItemData claw;
        private ItemDatabase itemDb;

        [SetUp]
        public void SetUp()
        {
            sprigling = TestFixtures.MakeSpecies("sprigling", "스프리글링", attack: 5, defense: 5, health: 20);
            speciesDb = TestFixtures.MakeSpeciesDatabase(sprigling);

            stone = TestFixtures.MakeItem("bloom_stone", StatType.Health, 5, "블룸 스톤");
            claw = TestFixtures.MakeItem("iron_claw", StatType.Attack, 3, "무쇠 발톱");
            itemDb = TestFixtures.MakeItemDatabase(stone, claw);
        }

        /// <summary>케어 한 마리짜리 상태. v2 시절 테스트와 같은 조건을 만들기 위한 것.</summary>
        private static GameState StateOf(CreatureInstance creature, Inventory inventory, int gold = 0)
        {
            var roster = new Roster();
            roster.Add(creature);
            return new GameState(roster, inventory, gold);
        }

        private static string CaptureJson(GameState state)
        {
            return SaveSerializer.ToJson(SaveSerializer.Capture(state, Stamp));
        }

        private bool Restore(string json, out GameState state, out string error)
        {
            return SaveSerializer.TryRestore(json, speciesDb, itemDb, out state, out error);
        }

        /// <summary>v3 스키마 JSON 을 손으로 만든다. 손상된 세이브를 흉내 낼 때 쓴다.</summary>
        private static string Json(
            string creatureFields,
            int version = 3,
            string partyIndices = "[]",
            int targetIndex = 0,
            int gold = 0,
            string itemIds = "[]",
            string itemCounts = "[]")
        {
            return "{\"version\":" + version
                   + ",\"creatures\":[" + creatureFields + "]"
                   + ",\"partyIndices\":" + partyIndices
                   + ",\"targetIndex\":" + targetIndex
                   + ",\"gold\":" + gold
                   + ",\"itemIds\":" + itemIds
                   + ",\"itemCounts\":" + itemCounts
                   + ",\"savedAtUtc\":\"x\"}";
        }

        private static string Creature(
            string speciesId = "sprigling",
            string equippedItemId = "",
            int exp = 0,
            int attack = 5,
            int defense = 5,
            int health = 20,
            int fullness = 60,
            int bond = 30)
        {
            return "{\"speciesId\":\"" + speciesId + "\""
                   + ",\"equippedItemId\":\"" + equippedItemId + "\""
                   + ",\"exp\":" + exp
                   + ",\"attack\":" + attack
                   + ",\"defense\":" + defense
                   + ",\"health\":" + health
                   + ",\"fullness\":" + fullness
                   + ",\"bond\":" + bond + "}";
        }

        // ---- 라운드트립 ----

        [Test]
        public void RoundTrip_PreservesAllState()
        {
            var creature = CreatureInstance.CreateNew(sprigling);
            creature.AddExp(140);
            creature.SetStat(StatType.Attack, 17);
            creature.SetStat(StatType.Defense, 6);
            creature.SetStat(StatType.Health, 22);
            creature.SetFullness(45);
            creature.SetBond(38);

            var inventory = new Inventory();
            inventory.Add("iron_claw", 2);
            inventory.Add("bloom_stone", 1);

            GameState loaded;
            string error;

            Assert.IsTrue(Restore(CaptureJson(StateOf(creature, inventory)), out loaded, out error), error);

            var restored = loaded.Roster.GetMember(0);
            Assert.AreSame(sprigling, restored.CurrentSpecies);
            Assert.AreEqual(140, restored.Exp);
            Assert.AreEqual(17, restored.GetStat(StatType.Attack));
            Assert.AreEqual(6, restored.GetStat(StatType.Defense));
            Assert.AreEqual(22, restored.GetStat(StatType.Health));
            Assert.AreEqual(45, restored.Fullness);
            Assert.AreEqual(38, restored.Bond);
            Assert.AreEqual(2, loaded.Inventory.GetCount("iron_claw"));
            Assert.AreEqual(1, loaded.Inventory.GetCount("bloom_stone"));
            Assert.IsNull(restored.EquippedItem);
        }

        [Test]
        public void RoundTrip_PreservesEquippedItem()
        {
            var creature = CreatureInstance.CreateNew(sprigling);
            var inventory = new Inventory();
            inventory.Add("iron_claw", 1);

            Assert.IsTrue(CareActions.Equip(creature, inventory, claw).Success);
            Assert.AreEqual(5 + 3, creature.GetStat(StatType.Attack));

            GameState loaded;
            string error;

            Assert.IsTrue(Restore(CaptureJson(StateOf(creature, inventory)), out loaded, out error), error);

            var restored = loaded.Roster.GetMember(0);
            Assert.AreSame(claw, restored.EquippedItem);
            Assert.AreEqual(5, restored.GetBaseStat(StatType.Attack), "기본 스탯에는 보너스가 섞이지 않는다");
            Assert.AreEqual(5 + 3, restored.GetStat(StatType.Attack), "장비 보너스가 다시 붙는다");
            Assert.AreEqual(0, loaded.Inventory.GetCount("iron_claw"), "장착 중인 것은 인벤토리에 없다");
        }

        [Test]
        public void RoundTrip_PreservesRosterOrderAndParty()
        {
            var roster = new Roster();
            var first = CreatureInstance.CreateNew(sprigling);
            var second = CreatureInstance.CreateNew(sprigling);
            var third = CreatureInstance.CreateNew(sprigling);

            first.AddExp(10);
            second.AddExp(20);
            third.AddExp(30);

            roster.Add(first);
            roster.Add(second);
            roster.Add(third);

            // 파티 순서가 보유 순서와 다르게 되도록 일부러 뒤에서부터 넣는다.
            Assert.IsTrue(roster.JoinParty(third).Success);
            Assert.IsTrue(roster.JoinParty(first).Success);

            var state = new GameState(roster, new Inventory(), 0);

            GameState loaded;
            string error;
            Assert.IsTrue(Restore(CaptureJson(state), out loaded, out error), error);

            Assert.AreEqual(3, loaded.Roster.Count, "보유 마릿수가 유지된다");
            Assert.AreEqual(10, loaded.Roster.GetMember(0).Exp, "보유 순서가 유지된다");
            Assert.AreEqual(20, loaded.Roster.GetMember(1).Exp);
            Assert.AreEqual(30, loaded.Roster.GetMember(2).Exp);

            Assert.AreEqual(2, loaded.Roster.Party.Count);
            Assert.AreEqual(30, loaded.Roster.Party[0].Exp, "파티 순서도 저장된 그대로다");
            Assert.AreEqual(10, loaded.Roster.Party[1].Exp);
            Assert.IsFalse(loaded.Roster.IsInParty(loaded.Roster.GetMember(1)), "안 나간 케어는 그대로다");
        }

        [Test]
        public void RoundTrip_PreservesGoldAndTarget()
        {
            var roster = new Roster();
            var first = CreatureInstance.CreateNew(sprigling);
            var second = CreatureInstance.CreateNew(sprigling);
            second.AddExp(77);
            roster.Add(first);
            roster.Add(second);

            var state = new GameState(roster, new Inventory(), 0);
            state.AddGold(125);
            Assert.IsTrue(state.SetTarget(second));

            GameState loaded;
            string error;
            Assert.IsTrue(Restore(CaptureJson(state), out loaded, out error), error);

            Assert.AreEqual(125, loaded.Gold);
            Assert.AreEqual(77, loaded.Target.Exp, "육성 대상이 유지된다");
        }

        // ---- 캡처 ----

        [Test]
        public void Capture_StoresBaseStatsNotBoostedOnes()
        {
            var creature = CreatureInstance.CreateNew(sprigling);
            var inventory = new Inventory();
            inventory.Add("iron_claw", 1);
            CareActions.Equip(creature, inventory, claw);

            var data = SaveSerializer.Capture(StateOf(creature, inventory), Stamp);

            Assert.AreEqual(5, data.creatures[0].attack,
                "장비 보너스가 저장값에 섞이면 로드할 때마다 스탯이 불어난다");
            Assert.AreEqual("iron_claw", data.creatures[0].equippedItemId);
        }

        [Test]
        public void Capture_WritesCurrentVersionAndSpeciesId()
        {
            var data = SaveSerializer.Capture(
                StateOf(CreatureInstance.CreateNew(sprigling), new Inventory()), Stamp);

            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(1, data.creatures.Length);
            Assert.AreEqual("sprigling", data.creatures[0].speciesId);
            Assert.AreEqual(Stamp, data.savedAtUtc);
            Assert.AreEqual(string.Empty, data.creatures[0].equippedItemId);
        }

        // ---- 손상된 세이브 ----

        [Test]
        public void TryRestore_RejectsMalformedJson()
        {
            GameState loaded;
            string error;

            Assert.IsFalse(Restore("{not json at all", out loaded, out error));
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void TryRestore_RejectsEmptyJson()
        {
            GameState loaded;
            string error;

            Assert.IsFalse(Restore("", out loaded, out error));
        }

        [Test]
        public void TryRestore_RejectsV2Save()
        {
            // v2 는 케어 1마리 구조였다. 파티/재화가 없어 되살릴 수 없으므로 새 게임으로 폴백한다.
            string json = "{\"version\":2,\"speciesId\":\"sprigling\",\"equippedItemId\":\"\",\"exp\":0," +
                          "\"attack\":5,\"defense\":5,\"health\":20,\"fullness\":60,\"bond\":30," +
                          "\"itemIds\":[],\"itemCounts\":[],\"savedAtUtc\":\"x\"}";

            GameState loaded;
            string error;

            Assert.IsFalse(Restore(json, out loaded, out error));
            StringAssert.Contains("버전", error);
        }

        [Test]
        public void TryRestore_RejectsUnknownSpeciesId()
        {
            GameState loaded;
            string error;

            Assert.IsFalse(Restore(Json(Creature(speciesId: "ghost")), out loaded, out error));
            StringAssert.Contains("ghost", error);
        }

        [Test]
        public void TryRestore_RejectsUnknownEquippedItemId()
        {
            GameState loaded;
            string error;

            Assert.IsFalse(Restore(Json(Creature(equippedItemId: "ghost_gear")), out loaded, out error));
            StringAssert.Contains("ghost_gear", error);
        }

        [Test]
        public void TryRestore_RejectsMismatchedInventoryArrays()
        {
            GameState loaded;
            string error;

            Assert.IsFalse(Restore(
                Json(Creature(), itemIds: "[\"a\",\"b\"]", itemCounts: "[1]"), out loaded, out error));
        }

        [Test]
        public void TryRestore_RejectsEmptyRoster()
        {
            GameState loaded;
            string error;

            Assert.IsFalse(Restore(Json(string.Empty), out loaded, out error));
            StringAssert.Contains("케어", error);
        }

        [Test]
        public void TryRestore_RejectsOutOfRangePartyIndex()
        {
            GameState loaded;
            string error;

            Assert.IsFalse(Restore(Json(Creature(), partyIndices: "[3]"), out loaded, out error));
            StringAssert.Contains("파티", error);
        }

        [Test]
        public void TryRestore_FallsBackWhenTargetIndexIsOutOfRange()
        {
            // 대상 인덱스만 망가진 것으로 세이브 전체를 버리지는 않는다.
            GameState loaded;
            string error;

            Assert.IsTrue(Restore(Json(Creature(exp: 42), targetIndex: 9), out loaded, out error), error);
            Assert.AreSame(loaded.Roster.GetMember(0), loaded.Target);
            Assert.AreEqual(42, loaded.Target.Exp);
        }

        [Test]
        public void TryRestore_ClampsOutOfRangeValues()
        {
            GameState loaded;
            string error;

            string json = Json(Creature(exp: -10, attack: -5, fullness: 500, bond: -20));

            Assert.IsTrue(Restore(json, out loaded, out error), error);

            var restored = loaded.Roster.GetMember(0);
            Assert.AreEqual(0, restored.Exp);
            Assert.AreEqual(CreatureInstance.MinStat, restored.GetStat(StatType.Attack));
            Assert.AreEqual(CreatureInstance.MaxCare, restored.Fullness);
            Assert.AreEqual(CreatureInstance.MinCare, restored.Bond);
        }

        [Test]
        public void TryRestore_ClampsNegativeGold()
        {
            GameState loaded;
            string error;

            Assert.IsTrue(Restore(Json(Creature(), gold: -50), out loaded, out error), error);
            Assert.AreEqual(0, loaded.Gold, "음수 재화는 0 으로 막는다");
        }

        [Test]
        public void RoundTrip_KeepsEggsInInventory_WithoutBumpingVersion()
        {
            var inventory = new Inventory();
            inventory.Add("bloom_stone", 1);
            inventory.Add("egg_sprigling", 2);

            string json = CaptureJson(StateOf(CreatureInstance.CreateNew(sprigling), inventory, 40));
            StringAssert.Contains("\"version\": 3", json, "알이 들어와도 세이브 버전은 그대로다");

            GameState restored;
            string error;
            Assert.IsTrue(Restore(json, out restored, out error), error);

            Assert.AreEqual(2, restored.Inventory.GetCount("egg_sprigling"),
                "알은 ItemDatabase 에 없지만 인벤토리는 문자열 id 만 담으므로 그대로 복원된다");
            Assert.AreEqual(1, restored.Inventory.GetCount("bloom_stone"));
        }
    }
}
