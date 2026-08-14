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

            var battle = new BattleSimulator(party, new[] { dummy });
            StringAssert.Contains("전투 중", PanelLabels.BattleTitle(battle));

            battle.Tick(1f);
            StringAssert.Contains("승리", PanelLabels.BattleTitle(battle));
        }

        [Test]
        public void BattleRoster_ShowsBothSidesWithHealth()
        {
            var hero = TestFixtures.MakeFighter("hero", attack: 1, defense: 0, health: 30,
                attackInterval: 9999f, cooldownSeconds: 9999f);
            var foe = TestFixtures.MakeEnemy("foe", maxHp: 40, attack: 0, defense: 0,
                attackInterval: 9999f, displayName: "들쥐");

            var party = new System.Collections.Generic.List<CreatureInstance>
            {
                CreatureInstance.CreateNew(hero)
            };

            string text = PanelLabels.BattleRoster(new BattleSimulator(party, new[] { foe }));

            StringAssert.Contains("[파티]", text);
            StringAssert.Contains("[상대]", text);
            StringAssert.Contains("30/30", text);
            StringAssert.Contains("40/40", text);
            StringAssert.Contains("들쥐", text);
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
    }
}
