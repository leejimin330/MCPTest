using FarmCreature.Core;
using FarmCreature.Data;
using NUnit.Framework;
using UnityEditor;

namespace FarmCreature.Tests
{
    /// <summary>
    /// <b>실제 생성된 에셋</b>(테스트 픽스처가 아니라 GameBootstrap 이 만든 것)으로
    /// 플레이를 시뮬레이션한다. 유닛 테스트는 로직만 보므로 데이터 작성 실수는 여기서 잡는다.
    /// </summary>
    public class GameDataIntegrationTests
    {
        private const string SpeciesDbPath = "Assets/GameData/SpeciesDatabase.asset";
        private const string ItemDbPath = "Assets/GameData/ItemDatabase.asset";
        private const string EggDbPath = "Assets/GameData/EggDatabase.asset";
        private const string ShopStockPath = "Assets/GameData/ShopStockDatabase.asset";
        private const string StageDbPath = "Assets/GameData/StageMapDatabase.asset";
        private const int SafetyCap = 300;

        private SpeciesDatabase speciesDb;
        private ItemDatabase itemDb;
        private EggDatabase eggDb;
        private ShopStockDatabase shopStock;
        private StageMapDatabase stageDb;

        [SetUp]
        public void SetUp()
        {
            speciesDb = AssetDatabase.LoadAssetAtPath<SpeciesDatabase>(SpeciesDbPath);
            itemDb = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ItemDbPath);
            eggDb = AssetDatabase.LoadAssetAtPath<EggDatabase>(EggDbPath);
            shopStock = AssetDatabase.LoadAssetAtPath<ShopStockDatabase>(ShopStockPath);

            Assert.IsNotNull(speciesDb, "SpeciesDatabase 에셋이 없다. GameBootstrap.BuildAll 을 먼저 실행할 것.");
            Assert.IsNotNull(itemDb, "ItemDatabase 에셋이 없다. GameBootstrap.BuildAll 을 먼저 실행할 것.");
            Assert.IsNotNull(eggDb, "EggDatabase 에셋이 없다. GameBootstrap.BuildAll 을 먼저 실행할 것.");
            Assert.IsNotNull(shopStock, "ShopStockDatabase 에셋이 없다. GameBootstrap.BuildAll 을 먼저 실행할 것.");

            stageDb = AssetDatabase.LoadAssetAtPath<StageMapDatabase>(StageDbPath);
            Assert.IsNotNull(stageDb, "StageMapDatabase 에셋이 없다. GameBootstrap.BuildAll 을 먼저 실행할 것.");
        }

        // ---- 데이터 무결성 ----

        [Test]
        public void SpeciesDatabase_ContainsAllThreeSpecies()
        {
            Assert.IsNotNull(speciesDb.Find("sprigling"));
            Assert.IsNotNull(speciesDb.Find("wildbeast"));
            Assert.IsNotNull(speciesDb.Find("blossom_guardian"));
        }

        [Test]
        public void EveryEnemyDrop_PointsAtSomethingThatExists()
        {
            // ★ 드랍 id 를 오타 내면 인벤토리에 아무도 모르는 키가 쌓인다 — 이름도 안 나오고
            // 쓸 수도 없고, 예외도 없다. 실제 에셋으로 대조하는 이 검사만 잡을 수 있다.
            var enemyDb = AssetDatabase.LoadAssetAtPath<EnemyDatabase>(
                "Assets/GameData/EnemyDatabase.asset");
            Assert.IsNotNull(enemyDb, "EnemyDatabase 에셋이 없다. BuildAll 을 먼저 실행할 것.");

            int checked_ = 0;

            for (int i = 0; i < enemyDb.enemies.Count; i++)
            {
                var enemy = enemyDb.enemies[i];
                Assert.IsNotNull(enemy, "EnemyDatabase 에 빈 칸이 있다");

                if (enemy.drops == null)
                {
                    continue;
                }

                for (int d = 0; d < enemy.drops.Count; d++)
                {
                    var drop = enemy.drops[d];
                    if (drop == null || string.IsNullOrEmpty(drop.itemId))
                    {
                        continue;
                    }

                    bool known = itemDb.Find(drop.itemId) != null || eggDb.Find(drop.itemId) != null;
                    Assert.IsTrue(known,
                        enemy.displayName + " 의 드랍 id '" + drop.itemId
                        + "' 가 아이템에도 알에도 없다");

                    Assert.Greater(drop.count, 0,
                        enemy.displayName + " 의 드랍 개수가 0 이하다 — 아무것도 안 나온다");

                    checked_++;
                }
            }

            Assert.Greater(checked_, 0, "드랍 표가 하나도 안 깔렸다 — 씨앗이 들어가지 않았다");
        }

        [Test]
        public void ItemDatabase_ContainsAllThreeItems()
        {
            Assert.IsNotNull(itemDb.Find("hearty_feed"));
            Assert.IsNotNull(itemDb.Find("iron_grain"));
            Assert.IsNotNull(itemDb.Find("bloom_stone"));
        }

        [Test]
        public void EvolvedSpecies_AreTerminal()
        {
            Assert.IsEmpty(speciesDb.Find("wildbeast").evolutions, "진화형은 더 진화하지 않아야 한다");
            Assert.IsEmpty(speciesDb.Find("blossom_guardian").evolutions);
        }

        [Test]
        public void Sprigling_EvolutionConditions_MatchPrd()
        {
            var sprigling = speciesDb.Find("sprigling");

            Assert.AreEqual(2, sprigling.evolutions.Count, "분기 진화는 2갈래");

            var toWildbeast = sprigling.evolutions[0];
            Assert.AreEqual("wildbeast", toWildbeast.result.speciesId, "훈련 노선이 먼저 평가된다");
            Assert.AreEqual(2, toWildbeast.requirements.Count);
            Assert.AreEqual(RequirementKind.ExpAtLeast, toWildbeast.requirements[0].kind);
            Assert.AreEqual(100, toWildbeast.requirements[0].value);
            Assert.AreEqual(RequirementKind.StatAtLeast, toWildbeast.requirements[1].kind);
            Assert.AreEqual(StatType.Attack, toWildbeast.requirements[1].stat);
            Assert.AreEqual(15, toWildbeast.requirements[1].value);

            var toGuardian = sprigling.evolutions[1];
            Assert.AreEqual("blossom_guardian", toGuardian.result.speciesId);
            Assert.AreEqual(RequirementKind.RequiresEquipped, toGuardian.requirements[1].kind);
            Assert.AreEqual("bloom_stone", toGuardian.requirements[1].itemId);
        }

        // ---- 플레이 시뮬레이션 ----

        /// <summary>
        /// 배고프면 먹이고, 아니면 지정 스탯을 훈련한다. 진화하면 즉시 반영하고 멈춘다.
        /// <paramref name="equipItemId"/> 를 주면 시작할 때 그 아이템을 장착한다.
        /// </summary>
        private CreatureInstance PlayUntilEvolved(
            StatType trainStat, Inventory inventory, out int actions, string equipItemId = null)
        {
            var creature = CreatureInstance.CreateNew(speciesDb.Find("sprigling"));
            actions = 0;

            if (!string.IsNullOrEmpty(equipItemId))
            {
                var item = itemDb.Find(equipItemId);
                Assert.IsNotNull(item, "장착할 아이템이 데이터베이스에 없다: " + equipItemId);
                Assert.IsTrue(CareActions.Equip(creature, inventory, item).Success, "장착 실패");
            }

            for (int i = 0; i < SafetyCap; i++)
            {
                ActionResult result = creature.Fullness < CareActions.TrainMinFullness
                    ? CareActions.Feed(creature)
                    : CareActions.Train(creature, trainStat);

                if (!result.Success)
                {
                    // 포만도 100 에서 먹이 거부 등 — 반대 액션으로 진행시킨다.
                    result = CareActions.Train(creature, trainStat);
                }

                Assert.IsTrue(result.Success, "시뮬레이션이 막혔다: " + result.Message);
                actions++;

                var evolved = EvolutionEvaluator.TryEvolve(creature, inventory);
                if (evolved != null)
                {
                    creature.CurrentSpecies = evolved;
                    return creature;
                }
            }

            return creature;
        }

        [Test]
        public void TrainingAttack_ReachesWildbeast()
        {
            var inventory = new Inventory();
            int actions;

            var creature = PlayUntilEvolved(StatType.Attack, inventory, out actions);

            Assert.AreEqual("wildbeast", creature.CurrentSpecies.speciesId,
                "공격 훈련 위주로 키우면 와일드비스트가 되어야 한다");
            Assert.LessOrEqual(actions, 30, "첫 진화가 너무 오래 걸리면 MVP 플레이 길이(5~15분)를 넘는다");
        }

        [Test]
        public void TrainingDefense_WithBloomStoneEquipped_ReachesBlossomGuardian()
        {
            var inventory = new Inventory();
            inventory.Add("bloom_stone", 1);
            int actions;

            var creature = PlayUntilEvolved(StatType.Defense, inventory, out actions, "bloom_stone");

            Assert.AreEqual("blossom_guardian", creature.CurrentSpecies.speciesId,
                "공격을 올리지 않고 블룸 스톤을 장착한 채 경험치를 채우면 블로섬 가디언이 되어야 한다");
            Assert.AreSame(itemDb.Find("bloom_stone"), creature.EquippedItem,
                "진화해도 장착은 유지된다");
        }

        [Test]
        public void TrainingDefense_WithBloomStoneOnlyHeld_DoesNotEvolve()
        {
            var inventory = new Inventory();
            inventory.Add("bloom_stone", 1);
            int actions;

            var creature = PlayUntilEvolved(StatType.Defense, inventory, out actions);

            Assert.AreEqual("sprigling", creature.CurrentSpecies.speciesId,
                "장착하지 않고 가지고만 있으면 진화하지 않는다");
        }

        [Test]
        public void TrainingDefense_WithNothing_DoesNotEvolve()
        {
            var inventory = new Inventory();
            int actions;

            var creature = PlayUntilEvolved(StatType.Defense, inventory, out actions);

            Assert.AreEqual("sprigling", creature.CurrentSpecies.speciesId,
                "공격도 안 올리고 장착도 없으면 진화 경로가 없다");
        }

        [Test]
        public void EquippingAttackItem_ReducesTrainingNeededForStatThreshold()
        {
            var claw = itemDb.Find("hearty_feed");
            Assert.AreEqual(StatType.Attack, claw.targetStat);

            const int threshold = 15;

            Assert.AreEqual(5, TrainingsToReachAttack(threshold, null),
                "기본 공격 5 에서 훈련(+2)만으로 15 에 닿으려면 5회");
            Assert.AreEqual(4, TrainingsToReachAttack(threshold, claw),
                "공격 +3 장비를 끼면 4회로 줄어야 한다");
        }

        private int TrainingsToReachAttack(int threshold, ItemData equip)
        {
            var creature = CreatureInstance.CreateNew(speciesDb.Find("sprigling"));
            var inventory = new Inventory();

            if (equip != null)
            {
                inventory.Add(equip.itemId, 1);
                Assert.IsTrue(CareActions.Equip(creature, inventory, equip).Success);
            }

            int trainings = 0;
            while (creature.GetStat(StatType.Attack) < threshold && trainings < SafetyCap)
            {
                // 포만도는 이 측정의 관심사가 아니므로 계속 채워준다.
                creature.SetFullness(CreatureInstance.MaxCare);
                Assert.IsTrue(CareActions.Train(creature, StatType.Attack).Success);
                trainings++;
            }

            return trainings;
        }

        [Test]
        public void WildbeastPath_IsGatedByExpNotAttack()
        {
            // 발견된 밸런스 사실: 공격 조건(15)은 경험치 조건(100)보다 먼저 충족된다.
            // 그래서 공격 장비를 껴도 진화 시점은 당겨지지 않는다.
            // 장비가 진화 속도에 영향을 주게 하려면 두 임계치를 다시 잡아야 한다.
            var bare = new Inventory();
            int actionsBare;
            var bareCreature = PlayUntilEvolved(StatType.Attack, bare, out actionsBare);

            var claw = itemDb.Find("hearty_feed");
            var geared = new Inventory();
            geared.Add(claw.itemId, 1);
            int actionsGeared;
            var gearedCreature = PlayUntilEvolved(StatType.Attack, geared, out actionsGeared, claw.itemId);

            Assert.AreEqual("wildbeast", bareCreature.CurrentSpecies.speciesId);
            Assert.AreEqual("wildbeast", gearedCreature.CurrentSpecies.speciesId);
            Assert.AreEqual(actionsBare, actionsGeared,
                "경험치가 병목이라 장비 유무가 진화 시점을 바꾸지 못한다 — 밸런스를 바꾸면 이 테스트를 갱신할 것");
            Assert.GreaterOrEqual(bareCreature.Exp, 100);
        }

        [Test]
        public void Equipment_IsSingleSlot_AndReversible()
        {
            var claw = itemDb.Find("hearty_feed");
            var shell = itemDb.Find("iron_grain");

            var creature = CreatureInstance.CreateNew(speciesDb.Find("sprigling"));
            var inventory = new Inventory();
            inventory.Add(claw.itemId, 1);
            inventory.Add(shell.itemId, 1);

            Assert.IsTrue(CareActions.Equip(creature, inventory, claw).Success);
            Assert.AreEqual(5 + claw.amount, creature.GetStat(StatType.Attack));

            Assert.IsTrue(CareActions.Equip(creature, inventory, shell).Success);
            Assert.AreEqual(5, creature.GetStat(StatType.Attack), "슬롯이 1칸이라 이전 장비 효과는 사라진다");
            Assert.AreEqual(1, inventory.GetCount(claw.itemId), "이전 장비는 인벤토리로 돌아온다");

            Assert.IsTrue(CareActions.Unequip(creature, inventory).Success);
            Assert.AreEqual(1, inventory.GetCount(shell.itemId));
            Assert.IsNull(creature.EquippedItem);
        }

        // ---- 상점 데이터 ----

        [Test]
        public void ShopStock_EveryEntry_PointsAtRealThingWithAPrice()
        {
            Assert.Greater(shopStock.Count, 0, "살 수 있는 것이 하나도 없으면 상점이 아니다");

            for (int i = 0; i < shopStock.Count; i++)
            {
                var entry = shopStock.Get(i);

                Assert.Greater(entry.price, 0, "항목 " + i + " 의 가격이 0 이하다");

                if (entry.kind == ShopEntryKind.Egg)
                {
                    Assert.IsNotNull(eggDb.Find(entry.targetId),
                        "항목 " + i + " 가 없는 알을 가리킨다: " + entry.targetId);
                }
                else
                {
                    Assert.IsNotNull(itemDb.Find(entry.targetId),
                        "항목 " + i + " 가 없는 아이템을 가리킨다: " + entry.targetId);
                }
            }
        }

        [Test]
        public void ShopStock_SellsEveryEquipmentAndTheEgg()
        {
            Assert.AreEqual(4, shopStock.Count, "장비 3종 + 알 1종");
        }

        [Test]
        public void Eggs_HaveHatchTargetAndPrefixedId()
        {
            Assert.Greater(eggDb.eggs.Count, 0);

            for (int i = 0; i < eggDb.eggs.Count; i++)
            {
                var egg = eggDb.eggs[i];

                StringAssert.StartsWith(EggData.IdPrefix, egg.eggId,
                    "알 id 는 아이템과 같은 인벤토리 키 공간을 쓴다 — 접두어로 갈라 둔다");
                Assert.IsNotNull(egg.hatchesInto, egg.eggId + " 에서 무엇이 나오는지 정해지지 않았다");
                Assert.IsNotNull(speciesDb.Find(egg.hatchesInto.speciesId),
                    "부화 결과가 종 데이터베이스에 없다: " + egg.eggId);
            }
        }

        [Test]
        public void EggIds_DoNotCollideWithItemIds()
        {
            for (int i = 0; i < eggDb.eggs.Count; i++)
            {
                Assert.IsNull(itemDb.Find(eggDb.eggs[i].eggId),
                    "알 id 가 아이템 id 와 겹친다 — 인벤토리에서 서로를 덮어쓴다");
            }
        }

        [Test]
        public void EveryEgg_ActuallyHatchesIntoACreature()
        {
            Assert.Greater(eggDb.eggs.Count, 0, "알이 하나도 없으면 부화소가 빈 패널이 된다");

            for (int i = 0; i < eggDb.eggs.Count; i++)
            {
                var egg = eggDb.eggs[i];
                var state = new GameState(new Roster(), new Inventory(), 0);
                state.Inventory.Add(egg.eggId, 1);

                var result = HatchService.TryHatch(state, egg);

                Assert.IsTrue(result.Success, egg.eggId + " 를 깨지 못했다: " + result.Message);
                Assert.AreEqual(1, state.Roster.Count, egg.eggId + " 를 깼는데 케어가 안 늘었다");
                Assert.AreSame(egg.hatchesInto, state.Roster.GetMember(0).CurrentSpecies,
                    egg.eggId + " 에서 다른 종이 나왔다");
                Assert.AreEqual(0, state.Inventory.GetCount(egg.eggId), "깬 알은 사라져야 한다");
            }
        }

        // ---------- 맵 / 스테이지 ----------

        [Test]
        public void EveryStage_HasANonBlankId()
        {
            for (int m = 0; m < stageDb.Count; m++)
            {
                var map = stageDb.Get(m);
                for (int s = 0; s < map.Count; s++)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(map.Get(s).stageId),
                        map.displayName + " 의 " + s + "번 스테이지에 id 가 없다 — 진행도를 기록할 수 없다");
                }
            }
        }

        /// <summary>
        /// 진행도가 id 로 기록되므로 <b>중복이면 조용히 틀린다</b> — 하나를 깨면 다른 것도
        /// 깬 것이 된다. 이 설계의 핵심 방어선이다 (설계 6.1).
        /// </summary>
        [Test]
        public void StageIds_AreUniqueAcrossAllMaps()
        {
            var seen = new System.Collections.Generic.HashSet<string>();

            for (int m = 0; m < stageDb.Count; m++)
            {
                var map = stageDb.Get(m);
                for (int s = 0; s < map.Count; s++)
                {
                    string id = map.Get(s).stageId;
                    Assert.IsTrue(seen.Add(id), "stageId 가 겹친다: " + id);
                }
            }
        }

        [Test]
        public void EveryMap_HasAField_AndItsFirstStageIsOpen()
        {
            Assert.Greater(stageDb.Count, 0, "맵이 하나도 없으면 전투터가 빈 화면이 된다");

            for (int m = 0; m < stageDb.Count; m++)
            {
                var map = stageDb.Get(m);
                Assert.IsNotNull(map.field, map.displayName + " 에 전장이 배선되지 않았다");
                Assert.Greater(map.Count, 0, map.displayName + " 에 스테이지가 없다");

                Assert.IsTrue(StageUnlockEvaluator.IsUnlocked(
                    map, 0, new StageProgress(), new Inventory()),
                    map.displayName + " 의 첫 스테이지가 잠겨 있다 — 아무도 들어갈 수 없다");
            }
        }

        [Test]
        public void EveryStage_FitsInTheEnemySlots()
        {
            for (int m = 0; m < stageDb.Count; m++)
            {
                var map = stageDb.Get(m);
                for (int s = 0; s < map.Count; s++)
                {
                    var stage = map.Get(s);
                    Assert.LessOrEqual(stage.TotalEnemyCount,
                        FarmCreature.Presentation.BattleField.MaxEnemies,
                        stage.displayName + " 의 적이 슬롯보다 많다");
                    Assert.Greater(stage.TotalEnemyCount, 0,
                        stage.displayName + " 에 적이 없다 — 눌러도 아무 일이 없다");
                }
            }
        }
    }
}
