using FarmCreature.Core;
using FarmCreature.Data;
using FarmCreature.Presentation;
using NUnit.Framework;

namespace FarmCreature.Tests
{
    /// <summary>
    /// 패널에 찍히는 문자열 규칙. 레이아웃과 배선은 SceneIntegrityCheck 가 보고,
    /// "무엇이 적히는가" 는 여기서 못박는다.
    /// </summary>
    public class PanelLabelsTests
    {
        private CreatureSpeciesData species;
        private ItemData claw;

        [SetUp]
        public void SetUp()
        {
            species = TestFixtures.MakeSpecies("sprigling", "스프리글링");
            claw = TestFixtures.MakeItem("claw", StatType.Attack, 3, "무쇠 발톱");
        }

        // ---- 창고 ----

        [Test]
        public void StorageRow_ShowsOwnedCountWhenNotEquipped()
        {
            string row = PanelLabels.StorageRow(claw, owned: 2, equipped: false);

            StringAssert.Contains("무쇠 발톱", row);
            StringAssert.Contains("공격 +3", row);
            StringAssert.Contains("소지 2", row);
        }

        [Test]
        public void StorageRow_ShowsEquippedInsteadOfCount()
        {
            string row = PanelLabels.StorageRow(claw, owned: 0, equipped: true);

            StringAssert.Contains("장착 중", row);
            StringAssert.DoesNotContain("소지", row);
        }

        [Test]
        public void StorageRow_ShowsSpareCountWhenEquippedAndOwned()
        {
            // 장착 중인 것은 인벤토리에서 빠지므로, 남은 수는 '여분'이다.
            string row = PanelLabels.StorageRow(claw, owned: 2, equipped: true);

            StringAssert.Contains("장착 중", row);
            StringAssert.Contains("여분 2", row);
        }

        [Test]
        public void StorageRow_HandlesEmptySlot()
        {
            Assert.AreEqual("(빈 칸)", PanelLabels.StorageRow(null, 0, false));
        }

        [Test]
        public void StorageTitle_NamesTheTarget()
        {
            var creature = CreatureInstance.CreateNew(species);

            StringAssert.Contains("스프리글링", PanelLabels.StorageTitle(creature));
            StringAssert.Contains("대상 없음", PanelLabels.StorageTitle(null));
        }

        [Test]
        public void EquippedLine_ShowsNoneWhenEmpty()
        {
            StringAssert.Contains("(없음)", PanelLabels.EquippedLine(null));
            StringAssert.Contains("무쇠 발톱", PanelLabels.EquippedLine(claw));
        }

        // ---- 육성 ----

        [Test]
        public void RaisingStatus_WarnsWhenTooHungryToTrain()
        {
            var creature = CreatureInstance.CreateNew(species);
            creature.SetFullness(CareActions.TrainMinFullness - 1);

            StringAssert.Contains("배가 고파", PanelLabels.RaisingStatus(creature));
        }

        [Test]
        public void RaisingStatus_NoWarningAtThreshold()
        {
            var creature = CreatureInstance.CreateNew(species);
            creature.SetFullness(CareActions.TrainMinFullness);

            StringAssert.DoesNotContain("배가 고파", PanelLabels.RaisingStatus(creature),
                "임계값과 안내 문구가 어긋나면 로그와 패널이 서로 다른 말을 한다");
        }

        [Test]
        public void RaisingStatus_HandlesNoTarget()
        {
            StringAssert.Contains("헛간", PanelLabels.RaisingStatus(null));
        }

        // ---- 헛간 ----

        [Test]
        public void BarnRow_MarksPartyMemberAndTarget()
        {
            var creature = CreatureInstance.CreateNew(species);

            string plain = PanelLabels.BarnRow(creature, inParty: false, isTarget: false);
            string outAndTarget = PanelLabels.BarnRow(creature, inParty: true, isTarget: true);

            StringAssert.DoesNotContain("[밖]", plain);
            StringAssert.DoesNotContain("육성중", plain);
            StringAssert.Contains("[밖]", outAndTarget);
            StringAssert.Contains("육성중", outAndTarget);
        }

        [Test]
        public void BarnRow_ShowsCombinedStats()
        {
            var creature = CreatureInstance.CreateNew(species);
            var inventory = new Inventory();
            inventory.Add("claw", 1);
            CareActions.Equip(creature, inventory, claw);

            // 기본 공격 5 + 장비 3 = 8 이 보여야 전투력이 눈에 보인다.
            StringAssert.Contains("공 8", PanelLabels.BarnRow(creature, false, false));
        }

        [Test]
        public void PartyButton_TogglesWording()
        {
            Assert.AreEqual("내보내기", PanelLabels.PartyButton(false));
            Assert.AreEqual("복귀", PanelLabels.PartyButton(true));
        }

        [Test]
        public void BarnTitle_ShowsBothCapacities()
        {
            var roster = new Roster();
            roster.Add(CreatureInstance.CreateNew(species));
            roster.JoinParty(roster.GetMember(0));

            string title = PanelLabels.BarnTitle(roster);

            StringAssert.Contains("1/" + Roster.MaxMembers, title);
            StringAssert.Contains("1/" + Roster.MaxParty, title);
        }

        // ---- 전투 ----

        [Test]
        public void BattleTitle_ReflectsOutcome()
        {
            StringAssert.Contains("고르자", PanelLabels.BattleTitle(null));

            var hero = TestFixtures.MakeFighter("hero", attack: 100, defense: 0, health: 100,
                attackInterval: 1f, cooldownSeconds: 9999f);
            var dummy = TestFixtures.MakeEnemy("dummy", maxHp: 5, attack: 0, defense: 0,
                attackInterval: 9999f);

            var party = new System.Collections.Generic.List<CreatureInstance>
            {
                CreatureInstance.CreateNew(hero)
            };

            // 원점에 겹쳐 세운다 — 거리 0 이라 항상 사거리 안이고, 이 테스트는 좌표를 보지 않는다.
            var atOrigin = new System.Collections.Generic.List<UnityEngine.Vector2>
            {
                UnityEngine.Vector2.zero
            };

            var battle = new BattleSimulator(party, new[] { dummy }, atOrigin, atOrigin);
            StringAssert.Contains("전투 중", PanelLabels.BattleTitle(battle));

            battle.Tick(1f);
            StringAssert.Contains("승리", PanelLabels.BattleTitle(battle));
        }

        [Test]
        public void EncounterButton_ShowsNameCountAndReward()
        {
            var rat = TestFixtures.MakeEnemy("rat", maxHp: 10, attack: 1, defense: 0, goldReward: 8);
            var encounter = TestFixtures.MakeEncounter("들판 — 들쥐 떼", (rat, 3));

            string label = PanelLabels.EncounterButton(encounter);

            StringAssert.Contains("들판 — 들쥐 떼", label);
            StringAssert.Contains("3", label, "몇 마리와 붙는지 모르고 들어가면 안 된다");
            StringAssert.Contains("24", label, "보상 합계 8×3");
        }

        [Test]
        public void EncounterButton_HandlesMissingEncounter()
        {
            Assert.AreEqual("(없음)", PanelLabels.EncounterButton(null));
        }

        [Test]
        public void BattleReadyLine_WarnsWhenNobodyIsOut()
        {
            var state = new GameState(new Roster(), new Inventory(), 0);

            StringAssert.Contains("헛간", PanelLabels.BattleReadyLine(state),
                "파티가 비었으면 어디서 내보내는지 알려줘야 한다");
        }

        [Test]
        public void BattleReadyLine_ShowsPartySizeAndGold()
        {
            var roster = new Roster();
            roster.Add(CreatureInstance.CreateNew(species));
            roster.JoinParty(roster.GetMember(0));

            string line = PanelLabels.BattleReadyLine(new GameState(roster, new Inventory(), 120));

            StringAssert.Contains("1/" + Roster.MaxParty, line);
            StringAssert.Contains("120", line);
        }

        [Test]
        public void SpeedLabel_ShowsTheMultiplier()
        {
            StringAssert.Contains("1x", PanelLabels.SpeedLabel(1f));
            StringAssert.Contains("2x", PanelLabels.SpeedLabel(2f));
        }

        [Test]
        public void GoldLine_ShowsAmount()
        {
            StringAssert.Contains("120", PanelLabels.GoldLine(120));
        }

        [Test]
        public void CreatureName_HandlesMissingSpecies()
        {
            Assert.AreEqual("???", PanelLabels.CreatureName(null));
        }

        // ---------- 상점 ----------

        [Test]
        public void ShopTitle_ShowsGold()
        {
            var roster = new Roster();
            roster.Add(CreatureInstance.CreateNew(TestFixtures.MakeSpecies("sprigling")));
            var state = new GameState(roster, new Inventory(), 128);

            StringAssert.Contains("상점", PanelLabels.ShopTitle(state));
            StringAssert.Contains("128", PanelLabels.ShopTitle(state));
        }

        [Test]
        public void ShopTitle_WithoutState_StillReadable()
        {
            Assert.AreEqual("상점", PanelLabels.ShopTitle(null));
        }

        [Test]
        public void ShopRow_Equipment_ShowsBonusPriceAndOwned()
        {
            var item = TestFixtures.MakeItem("hearty_feed", StatType.Attack, 3, "무쇠 발톱");

            string row = PanelLabels.ShopRow(item, 30, 2);

            StringAssert.Contains("무쇠 발톱", row);
            StringAssert.Contains("+3", row);
            StringAssert.Contains("30G", row);
            StringAssert.Contains("보유 2", row);
        }

        [Test]
        public void ShopRow_Egg_ShowsPriceAndOwned()
        {
            var egg = TestFixtures.MakeEgg("egg_sprigling", "스프리글링 알");

            string row = PanelLabels.ShopRow(egg, 60, 0);

            StringAssert.Contains("스프리글링 알", row);
            StringAssert.Contains("60G", row);
            StringAssert.Contains("보유 0", row);
        }

        [Test]
        public void ShopRow_NullTarget_IsEmptySlot()
        {
            Assert.AreEqual("(빈 칸)", PanelLabels.ShopRow((ItemData)null, 30, 0));
            Assert.AreEqual("(빈 칸)", PanelLabels.ShopRow((EggData)null, 60, 0));
        }

        // ---------- 부화소 ----------

        [Test]
        public void HatchTitle_ShowsBarnOccupancy()
        {
            var roster = new Roster();
            roster.Add(CreatureInstance.CreateNew(species));
            var state = new GameState(roster, new Inventory(), 0);

            string title = PanelLabels.HatchTitle(state);

            StringAssert.Contains("부화소", title);
            StringAssert.Contains("1/" + Roster.MaxMembers, title,
                "부화가 거부되는 유일한 이유가 헛간 자리다 — 누르기 전에 보여야 한다");
        }

        [Test]
        public void HatchTitle_WithoutState_StillReadable()
        {
            Assert.AreEqual("부화소", PanelLabels.HatchTitle(null));
        }

        [Test]
        public void HatchRow_ShowsOwnedCountAndResultSpecies()
        {
            // 종 표시 이름을 알 이름과 겹치지 않게 둔다 — 겹치면 결과 종을 빼먹어도 단언이 통과한다.
            var egg = TestFixtures.MakeEgg("egg_sprigling", "스프리글링 알",
                TestFixtures.MakeSpecies("sprigling", "새싹이"));

            string row = PanelLabels.HatchRow(egg, 2);

            StringAssert.Contains("스프리글링 알", row);
            StringAssert.Contains("보유 2", row);
            StringAssert.Contains("새싹이", row, "무엇이 나오는지 모르고 깨는 것은 이상하다");
        }

        [Test]
        public void HatchRow_NullEgg_IsEmptySlot()
        {
            Assert.AreEqual("(빈 칸)", PanelLabels.HatchRow(null, 0));
        }
    }
}
