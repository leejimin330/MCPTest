using System.Collections.Generic;
using System.IO;
using FarmCreature.Core;
using FarmCreature.Data;
using FarmCreature.Presentation;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FarmCreature.EditorTools
{
    /// <summary>
    /// 데이터 에셋과 씬을 전량 생성한다. 씬을 손으로 편집하지 않기 위한 장치 —
    /// 씬이 깨져도 이 메뉴/명령 하나로 복구된다.
    /// 배치모드: -executeMethod FarmCreature.EditorTools.GameBootstrap.BuildAll
    /// </summary>
    public static class GameBootstrap
    {
        private const string DataRoot = "Assets/GameData";
        private const string SpeciesDir = DataRoot + "/Species";
        private const string ItemsDir = DataRoot + "/Items";
        private const string EnemiesDir = DataRoot + "/Enemies";
        private const string SpritesDir = DataRoot + "/Sprites";
        private const string BattleDir = DataRoot + "/Battle";
        private const string EggsDir = DataRoot + "/Eggs";
        private const string ScenesDir = "Assets/Scenes";
        private const string FarmScenePath = ScenesDir + "/FarmScene.unity";
        private const string BattleScenePath = ScenesDir + "/BattleScene.unity";
        private const string BarnScenePath = ScenesDir + "/BarnScene.unity";
        private const string HatcheryScenePath = ScenesDir + "/HatcheryScene.unity";
        private const string StorageScenePath = ScenesDir + "/StorageScene.unity";
        private const string ShopScenePath = ScenesDir + "/ShopScene.unity";
        private const string BattleGroundScenePath = ScenesDir + "/BattleGroundScene.unity";

        // 씬에 직렬화되려면 스프라이트가 '에셋'이어야 한다.
        // 코드로 만든 런타임 Sprite 는 저장되지 않아 씬을 다시 열면 전부 사라진다.
        private static Sprite squareSprite;
        private static Sprite circleSprite;

        [MenuItem("FarmCreature/Build All")]
        public static void BuildAll()
        {
            // Play 모드에서는 NewScene 이 예외를 던진다 (함정 3.2).
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[FarmCreature] Play 모드에서는 씬을 재생성할 수 없습니다. Play 를 멈추고 다시 실행하세요.");
                return;
            }

            // ★ 빈 씬을 '에셋을 만지기 전에' 먼저 연다.
            //
            // NewScene(Single) 은 참조되지 않는 에셋을 언로드하면서, 그 이전에 얻어 둔
            // ScriptableObject 참조의 네이티브 객체를 파괴한다. 그러면 관리 래퍼는 살아 있는데
            // (ReferenceEquals(x, null) == false) Unity 의 == 연산자로는 x != null 이 false 가 된다.
            //
            // 예전에는 에셋을 먼저 만들고 BuildScene 안에서 NewScene 을 불렀다. 그 결과
            //   int rowCount = itemDb != null && itemDb.items != null ? itemDb.items.Count : 0;
            // 이 예외 없이 조용히 0 을 돌려줘, 창고의 [장착] 버튼과 전투터의 상대 버튼이
            // 한 개도 만들어지지 않은 채 씬이 저장됐다. 종료 코드는 0 이었다.
            var farmScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            EnsureFolders();
            EnsureSpriteAssets();

            var species = BuildSpeciesAssets();
            var items = BuildItemAssets();
            var enemies = BuildEnemyAssets();
            var speciesDb = BuildSpeciesDatabase(species);
            var itemDb = BuildItemDatabase(items);
            BuildEnemyDatabase(enemies);
            BuildBattleDataAssets(enemies);

            // 아래 데이터베이스들은 농장이 아니라 각 건물 씬이 쓴다. 그 씬을 채울 때
            // 경로에서 다시 로드하므로(NextScene 주석) 여기서는 만들어 두기만 한다.
            BuildEggDatabase(BuildEggAssets(species));
            BuildShopStock();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BuildFarmScene(farmScene, speciesDb, itemDb, species["sprigling"], items);

            // ★ 두 번째 NewScene 이 위에서 얻은 참조를 '다시' 죽인다 (함정 3.9와 같은 원리).
            // 그래서 전투 씬을 채우기 전에 필요한 것을 경로에서 전부 다시 로드한다.
            // 이 재로드를 빠뜨리면 슬롯도 버튼도 없는 전투 씬이 종료 코드 0 으로 조용히 저장된다.
            var battleScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            ReloadSpriteAssets();
            var battleMap = AssetDatabase.LoadAssetAtPath<BattleMapData>(BattleDir + "/FieldMap.asset");
            var practiceEncounter = AssetDatabase.LoadAssetAtPath<BattleEncounter>(BattleDir + "/Field1.asset");
            var practiceSpecies = AssetDatabase.LoadAssetAtPath<CreatureSpeciesData>(SpeciesDir + "/Sprigling.asset");

            if (battleMap == null || practiceEncounter == null || practiceSpecies == null)
            {
                Debug.LogError("[FarmCreature] 전투 씬용 에셋 재로드 실패 — "
                    + "map==null? " + (battleMap == null)
                    + " encounter==null? " + (practiceEncounter == null)
                    + " species==null? " + (practiceSpecies == null));
            }

            BuildBattleScene(battleScene, battleMap, practiceEncounter, practiceSpecies);

            // ★ 여기서부터 건물 씬 5개. NewScene 을 부를 때마다 이전에 얻은 에셋 참조가
            // 죽으므로, 씬을 채우기 직전에 매번 경로에서 다시 로드한다 (함정 3.9 / 3.10).
            // 이 재로드를 빠뜨리면 조각이 빠진 씬이 종료 코드 0 으로 조용히 저장된다.
            BuildBarnScene(NextScene());
            BuildHatcheryScene(NextScene());
            BuildStorageScene(NextScene());
            BuildShopScene(NextScene());
            BuildBattleGroundScene(NextScene());

            // 일곱 씬을 모두 등록한다. 안 하면 LoadScene 이 런타임에 실패한다 (설계 5.1).
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(FarmScenePath, true),
                new EditorBuildSettingsScene(BattleScenePath, true),
                new EditorBuildSettingsScene(BarnScenePath, true),
                new EditorBuildSettingsScene(HatcheryScenePath, true),
                new EditorBuildSettingsScene(StorageScenePath, true),
                new EditorBuildSettingsScene(ShopScenePath, true),
                new EditorBuildSettingsScene(BattleGroundScenePath, true)
            };

            Debug.Log("[FarmCreature] Build All 완료 — 에셋과 씬 7개를 생성했습니다.");
        }

        /// <summary>
        /// <c>NewScene</c> 이후에 스프라이트 참조를 되살린다.
        /// PNG 를 다시 쓰지 않고 경로에서 로드만 한다 — 이미 만들어진 에셋이다.
        /// </summary>
        private static void ReloadSpriteAssets()
        {
            squareSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritesDir + "/Square.png");
            circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritesDir + "/Circle.png");

            if (squareSprite == null || circleSprite == null)
            {
                Debug.LogError("[FarmCreature] 스프라이트 재로드 실패 — NewScene 이후 참조가 죽었다. "
                    + "square==null? " + (squareSprite == null)
                    + " circle==null? " + (circleSprite == null));
            }
        }

        // ---------- 폴더 ----------

        private static void EnsureFolders()
        {
            EnsureFolder(DataRoot);
            EnsureFolder(SpeciesDir);
            EnsureFolder(ItemsDir);
            EnsureFolder(EnemiesDir);
            EnsureFolder(SpritesDir);
            EnsureFolder(BattleDir);
            EnsureFolder(EggsDir);
            EnsureFolder(ScenesDir);
        }

        // ---------- 플레이스홀더 스프라이트 (실제 에셋) ----------

        /// <summary>
        /// 사각형 / 원 스프라이트를 <b>PNG 에셋</b>으로 만든다.
        /// 런타임 생성 Sprite 는 씬에 직렬화되지 않아, 에디터를 껐다 켜면
        /// 모든 SpriteRenderer 가 빈 상태(m_Sprite: fileID 0)가 되어버린다.
        /// </summary>
        private static void EnsureSpriteAssets()
        {
            squareSprite = CreateSpriteAsset(SpritesDir + "/Square.png", 8, false);
            circleSprite = CreateSpriteAsset(SpritesDir + "/Circle.png", 64, true);
        }

        private static Sprite CreateSpriteAsset(string assetPath, int size, bool circle)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float radius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (!circle)
                    {
                        texture.SetPixel(x, y, Color.white);
                        continue;
                    }

                    float dx = x + 0.5f - radius;
                    float dy = y + 0.5f - radius;
                    bool inside = dx * dx + dy * dy <= radius * radius;
                    texture.SetPixel(x, y, inside ? Color.white : Color.clear);
                }
            }

            texture.Apply();

            string absolutePath = Application.dataPath + assetPath.Substring("Assets".Length);
            File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                // PPU = 텍스처 크기 → 스케일 1 에서 딱 1 월드 유닛이 된다.
                importer.spritePixelsPerUnit = size;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                Debug.LogError("[FarmCreature] 스프라이트 에셋 생성 실패: " + assetPath);
            }

            return sprite;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        // ---------- 에셋 ----------

        private static T LoadOrCreate<T>(string assetPath) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, assetPath);
            return created;
        }

        /// <summary>
        /// 없으면 만들어 초기값을 넣고, <b>이미 있으면 손대지 않는다.</b>
        /// <paramref name="created"/> 가 <c>true</c> 면 이번에 새로 만든 것이라 초기값을 채워야 한다.
        ///
        /// <b>전투 데이터에만 쓴다</b> — 손으로 밸런스를 만지는 대상이 전투뿐이기 때문이다.
        /// 종·아이템·씬은 지금처럼 <see cref="LoadOrCreate{T}"/> 로 코드가 전권을 쥔다.
        /// 전면 seed-only 로 바꾸면 "씬과 에셋은 언제든 재생성 가능해야 한다"(PRD N5)가 약해진다 (설계 2.2).
        ///
        /// <b>대가:</b> 초기값을 코드에서 바꿔도 기존 에셋에는 반영되지 않는다.
        /// 다시 깔려면 해당 .asset 을 지우고 BuildAll 을 돌린다.
        /// </summary>
        private static T LoadOrSeed<T>(string assetPath, out bool created) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing != null)
            {
                created = false;
                return existing;
            }

            var seeded = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(seeded, assetPath);
            created = true;
            return seeded;
        }

        private static Dictionary<string, CreatureSpeciesData> BuildSpeciesAssets()
        {
            var sprigling = LoadOrCreate<CreatureSpeciesData>(SpeciesDir + "/Sprigling.asset");
            var wildbeast = LoadOrCreate<CreatureSpeciesData>(SpeciesDir + "/Wildbeast.asset");
            var guardian = LoadOrCreate<CreatureSpeciesData>(SpeciesDir + "/BlossomGuardian.asset");

            sprigling.speciesId = "sprigling";
            sprigling.displayName = "스프리글링";
            sprigling.baseStats = new StatBlock { attack = 5, defense = 5, health = 20 };
            sprigling.placeholderColor = new Color(0.55f, 0.85f, 0.35f);
            sprigling.placeholderScale = 1f;
            sprigling.attackInterval = 1.2f;
            sprigling.skill = new SkillDefinition
            {
                displayName = "새싹 타격",
                effect = SkillEffect.Damage,
                cooldownSeconds = 5f,
                multiplier = 1.8f
            };
            sprigling.evolutions = new List<EvolutionCondition>
            {
                new EvolutionCondition
                {
                    result = wildbeast,
                    requirements = new List<EvolutionRequirement>
                    {
                        EvolutionRequirement.ExpAtLeast(100),
                        EvolutionRequirement.StatAtLeast(StatType.Attack, 15)
                    }
                },
                new EvolutionCondition
                {
                    result = guardian,
                    requirements = new List<EvolutionRequirement>
                    {
                        EvolutionRequirement.ExpAtLeast(100),
                        EvolutionRequirement.RequiresEquipped("bloom_stone")
                    }
                }
            };

            wildbeast.speciesId = "wildbeast";
            wildbeast.displayName = "와일드비스트";
            wildbeast.baseStats = new StatBlock { attack = 15, defense = 8, health = 30 };
            wildbeast.placeholderColor = new Color(0.95f, 0.55f, 0.2f);
            wildbeast.placeholderScale = 1.5f;
            // 공격 특화 — 평타가 빠르고 스킬 배수가 크다.
            wildbeast.attackInterval = 1f;
            wildbeast.skill = new SkillDefinition
            {
                displayName = "야성의 돌진",
                effect = SkillEffect.Damage,
                cooldownSeconds = 6f,
                multiplier = 2.6f
            };
            wildbeast.evolutions = new List<EvolutionCondition>();

            guardian.speciesId = "blossom_guardian";
            guardian.displayName = "블로섬 가디언";
            guardian.baseStats = new StatBlock { attack = 8, defense = 15, health = 35 };
            guardian.placeholderColor = new Color(0.95f, 0.6f, 0.8f);
            guardian.placeholderScale = 1.5f;
            // 방어 특화 — 딜은 약하지만 파티를 살린다. 두 진화 경로가 전투에서 갈리도록 한 것.
            guardian.attackInterval = 1.4f;
            guardian.skill = new SkillDefinition
            {
                displayName = "치유의 개화",
                effect = SkillEffect.Heal,
                cooldownSeconds = 5f,
                multiplier = 1.5f
            };
            guardian.evolutions = new List<EvolutionCondition>();

            EditorUtility.SetDirty(sprigling);
            EditorUtility.SetDirty(wildbeast);
            EditorUtility.SetDirty(guardian);

            return new Dictionary<string, CreatureSpeciesData>
            {
                { "sprigling", sprigling },
                { "wildbeast", wildbeast },
                { "blossom_guardian", guardian }
            };
        }

        private static List<ItemData> BuildItemAssets()
        {
            // 전부 '장착 아이템'이다. 끼면 보너스, 빼면 사라진다 (소모되지 않음).
            var feed = LoadOrCreate<ItemData>(ItemsDir + "/HeartyFeed.asset");
            feed.itemId = "hearty_feed";
            feed.displayName = "무쇠 발톱";
            feed.targetStat = StatType.Attack;
            feed.amount = 3;

            var grain = LoadOrCreate<ItemData>(ItemsDir + "/IronGrain.asset");
            grain.itemId = "iron_grain";
            grain.displayName = "돌 껍질";
            grain.targetStat = StatType.Defense;
            grain.amount = 3;

            var stone = LoadOrCreate<ItemData>(ItemsDir + "/BloomStone.asset");
            stone.itemId = "bloom_stone";
            stone.displayName = "블룸 스톤";
            stone.targetStat = StatType.Health;
            stone.amount = 5;

            EditorUtility.SetDirty(feed);
            EditorUtility.SetDirty(grain);
            EditorUtility.SetDirty(stone);

            return new List<ItemData> { feed, grain, stone };
        }

        /// <summary>
        /// 전투터의 상대 3종. <b>seed-only</b> 다 — 이미 있으면 인스펙터에서 만진 값을 그대로 둔다 (설계 2.2).
        ///
        /// 수치는 초안이다: 스프리글링(공5 방5 체20) 한 마리로는 들쥐를 간신히 잡고,
        /// 가시멧돼지부터는 파티를 늘리거나 훈련·장비가 필요하도록 잡았다.
        /// </summary>
        private static List<EnemyData> BuildEnemyAssets()
        {
            bool created;

            var rat = LoadOrSeed<EnemyData>(EnemiesDir + "/FieldRat.asset", out created);
            if (created)
            {
                rat.enemyId = "field_rat";
                rat.displayName = "들쥐";
                rat.maxHp = 24;
                rat.attack = 4;
                rat.defense = 1;
                rat.attackInterval = 1.8f;
                rat.goldReward = 8;
                rat.placeholderColor = new Color(0.72f, 0.66f, 0.55f);
                EditorUtility.SetDirty(rat);
            }

            var boar = LoadOrSeed<EnemyData>(EnemiesDir + "/ThornBoar.asset", out created);
            if (created)
            {
                boar.enemyId = "thorn_boar";
                boar.displayName = "가시멧돼지";
                boar.maxHp = 45;
                boar.attack = 7;
                boar.defense = 3;
                boar.attackInterval = 1.5f;
                boar.goldReward = 18;
                boar.placeholderColor = new Color(0.65f, 0.4f, 0.3f);
                EditorUtility.SetDirty(boar);
            }

            var golem = LoadOrSeed<EnemyData>(EnemiesDir + "/StoneGolem.asset", out created);
            if (created)
            {
                golem.enemyId = "stone_golem";
                golem.displayName = "바위골렘";
                golem.maxHp = 80;
                golem.attack = 10;
                golem.defense = 6;
                golem.attackInterval = 1.9f;
                golem.goldReward = 40;
                golem.placeholderColor = new Color(0.5f, 0.5f, 0.55f);
                EditorUtility.SetDirty(golem);
            }

            return new List<EnemyData> { rat, boar, golem };
        }

        /// <summary>
        /// 전투 맵과 인카운터를 <b>seed-only</b> 로 깐다 (설계 2.2).
        /// 맵은 하나(들판)만 만든다 — 맵이 늘어나면 인카운터에서 골라 쓰면 된다 (설계 8.4).
        /// </summary>
        private static StageMapDatabase BuildBattleDataAssets(List<EnemyData> enemies)
        {
            bool created;

            var fieldMap = LoadOrSeed<BattleMapData>(BattleDir + "/FieldMap.asset", out created);
            if (created)
            {
                fieldMap.displayName = "들판";
                fieldMap.groundSize = new Vector2(17f, 9f);
                fieldMap.groundColor = new Color(0.45f, 0.62f, 0.35f);
                fieldMap.allySpawnCenter = new Vector2(-4.5f, -0.5f);
                fieldMap.allySpawnSize = new Vector2(5f, 5.5f);
                fieldMap.enemySpawnCenter = new Vector2(4.5f, -0.5f);
                fieldMap.enemySpawnSize = new Vector2(5f, 5.5f);
                fieldMap.minSpacing = 1.4f;
                EditorUtility.SetDirty(fieldMap);
            }

            // 마리 수와 종류로 난이도를 벌린다. 총원은 BattleField.MaxEnemies 를 넘지 않는다.
            // 이 수치는 난이도 곡선을 잡은 값이 아니라 자리를 채운 씨앗이다 —
            // 밸런스는 개발이 끝난 뒤에 잡는다 (설계 7절).
            var s1 = SeedStage(BattleDir + "/Field1.asset", "field_1", "들판 1", false,
                new[] { (enemies[0], 2) });
            var s2 = SeedStage(BattleDir + "/Field2.asset", "field_2", "들판 2", true,
                new[] { (enemies[0], 3) });
            var s3 = SeedStage(BattleDir + "/Field3.asset", "field_3", "들판 3", true,
                new[] { (enemies[1], 1), (enemies[0], 2) });
            var s4 = SeedStage(BattleDir + "/Field4.asset", "field_4", "들판 4", true,
                new[] { (enemies[2], 1) });

            var map = LoadOrSeed<StageMap>(BattleDir + "/FieldStages.asset", out created);
            if (created)
            {
                map.displayName = "들쥐 들판";
                map.field = fieldMap;
                map.stages = new List<BattleEncounter> { s1, s2, s3, s4 };
                EditorUtility.SetDirty(map);
            }

            // 데이터베이스는 코드가 전권을 쥔다 — 맵 목록은 밸런스가 아니다.
            var db = LoadOrCreate<StageMapDatabase>(DataRoot + "/StageMapDatabase.asset");
            db.maps = new List<StageMap> { map };
            EditorUtility.SetDirty(db);

            ValidateStages(db);
            return db;
        }

        /// <summary>
        /// 스테이지 하나를 깐다. <paramref name="gatedByPrevious"/> 가 true 면
        /// 바로 앞 스테이지를 깨야 열린다.
        /// <c>map</c> 은 비워 둔다 — 전장은 <see cref="StageMap"/> 이 정한다 (설계 2.1).
        /// </summary>
        private static BattleEncounter SeedStage(
            string assetPath, string stageId, string displayName, bool gatedByPrevious,
            (EnemyData enemy, int count)[] entries)
        {
            bool created;
            var stage = LoadOrSeed<BattleEncounter>(assetPath, out created);

            if (created)
            {
                stage.stageId = stageId;
                stage.displayName = displayName;
                stage.map = null;
                stage.entries = new List<EncounterEntry>();

                for (int i = 0; i < entries.Length; i++)
                {
                    stage.entries.Add(new EncounterEntry
                    {
                        enemy = entries[i].enemy,
                        count = entries[i].count
                    });
                }

                stage.unlock = new List<StageUnlock>();
                if (gatedByPrevious)
                {
                    stage.unlock.Add(new StageUnlock
                    {
                        kind = StageUnlockKind.PreviousStageCleared
                    });
                }

                EditorUtility.SetDirty(stage);
            }

            return stage;
        }

        /// <summary>
        /// 데이터 실수를 <b>크게 실패시킨다.</b> 조용히 넘기면 전투터가 빈 화면이 되거나
        /// 진행도가 엉뚱하게 기록된다 (설계 4.3).
        /// </summary>
        private static void ValidateStages(StageMapDatabase db)
        {
            if (db == null || db.Count == 0)
            {
                Debug.LogError("[FarmCreature] 맵이 0개다. 전투터가 빈 화면이 된다. "
                    + "StageMapDatabase 참조가 죽었는지(NewScene 이후 로드했는지) 확인할 것. "
                    + "db==null? " + (db == null));
                return;
            }

            var seenIds = new HashSet<string>();

            for (int m = 0; m < db.Count; m++)
            {
                var map = db.Get(m);
                if (map == null || map.field == null)
                {
                    Debug.LogError("[FarmCreature] " + m + "번 맵이 비었거나 전장이 없다.");
                    continue;
                }

                for (int s = 0; s < map.Count; s++)
                {
                    var stage = map.Get(s);
                    if (stage == null)
                    {
                        Debug.LogError("[FarmCreature] " + map.displayName + " 의 "
                            + s + "번 스테이지가 비었다.");
                        continue;
                    }

                    // 진행도가 id 로 기록되므로, 비었거나 겹치면 조용히 틀린다 (설계 1.1).
                    if (string.IsNullOrEmpty(stage.stageId))
                    {
                        Debug.LogError("[FarmCreature] '" + stage.displayName
                            + "' 에 stageId 가 없다 — 진행도를 기록할 수 없다.");
                    }
                    else if (!seenIds.Add(stage.stageId))
                    {
                        Debug.LogError("[FarmCreature] stageId 가 겹친다: " + stage.stageId
                            + " — 하나를 깨면 다른 것도 깬 것이 된다.");
                    }

                    if (stage.TotalEnemyCount > BattleField.MaxEnemies)
                    {
                        Debug.LogError("[FarmCreature] '" + stage.displayName + "' 의 적이 "
                            + stage.TotalEnemyCount + "마리다. 슬롯은 "
                            + BattleField.MaxEnemies + "개뿐이라 나머지는 안 나온다.");
                    }

                    if (stage.TotalEnemyCount == 0)
                    {
                        Debug.LogError("[FarmCreature] '" + stage.displayName
                            + "' 에 적이 없다. 눌러도 아무 일도 일어나지 않는다.");
                    }
                }
            }
        }

        private static EnemyDatabase BuildEnemyDatabase(List<EnemyData> enemies)
        {
            var db = LoadOrCreate<EnemyDatabase>(DataRoot + "/EnemyDatabase.asset");
            db.enemies = new List<EnemyData>(enemies);
            EditorUtility.SetDirty(db);
            return db;
        }

        private static SpeciesDatabase BuildSpeciesDatabase(Dictionary<string, CreatureSpeciesData> species)
        {
            var db = LoadOrCreate<SpeciesDatabase>(DataRoot + "/SpeciesDatabase.asset");
            db.species = new List<CreatureSpeciesData>
            {
                species["sprigling"],
                species["wildbeast"],
                species["blossom_guardian"]
            };
            EditorUtility.SetDirty(db);
            return db;
        }

        private static ItemDatabase BuildItemDatabase(List<ItemData> items)
        {
            var db = LoadOrCreate<ItemDatabase>(DataRoot + "/ItemDatabase.asset");
            db.items = new List<ItemData>(items);
            EditorUtility.SetDirty(db);
            return db;
        }

        /// <summary>
        /// 알 에셋. <b>seed-only 가 아니다</b> — 코드가 전권을 쥔다 (함정 3.11 은 전투 데이터에만 해당).
        /// </summary>
        private static List<EggData> BuildEggAssets(Dictionary<string, CreatureSpeciesData> species)
        {
            var egg = LoadOrCreate<EggData>(EggsDir + "/SpriglingEgg.asset");
            egg.eggId = EggData.IdPrefix + "sprigling";
            egg.displayName = "스프리글링 알";
            egg.hatchesInto = species["sprigling"];
            EditorUtility.SetDirty(egg);

            return new List<EggData> { egg };
        }

        private static EggDatabase BuildEggDatabase(List<EggData> eggs)
        {
            var db = LoadOrCreate<EggDatabase>(DataRoot + "/EggDatabase.asset");
            db.eggs = new List<EggData>(eggs);
            EditorUtility.SetDirty(db);
            return db;
        }

        /// <summary>
        /// 상점 판매 목록. <b>상점 패널의 행 개수가 여기서 나온다</b> —
        /// 항목을 늘리면 BuildAll 을 다시 돌려야 한다 (함정 3.1).
        ///
        /// 가격은 초안이다: 들쥐 한 판이 재화 8 이므로 장비가 약 4판, 알이 약 8판이다.
        /// </summary>
        private static ShopStockDatabase BuildShopStock()
        {
            var db = LoadOrCreate<ShopStockDatabase>(DataRoot + "/ShopStockDatabase.asset");
            db.entries = new List<ShopEntry>
            {
                new ShopEntry { kind = ShopEntryKind.Equipment, targetId = "hearty_feed", price = 30 },
                new ShopEntry { kind = ShopEntryKind.Equipment, targetId = "iron_grain", price = 30 },
                new ShopEntry { kind = ShopEntryKind.Equipment, targetId = "bloom_stone", price = 45 },
                new ShopEntry { kind = ShopEntryKind.Egg, targetId = EggData.IdPrefix + "sprigling", price = 60 }
            };
            EditorUtility.SetDirty(db);
            return db;
        }

        // ---------- 씬 ----------

        /// <summary>
        /// 빈 씬을 채운다. <paramref name="scene"/> 는 <see cref="BuildAll"/> 이 <b>에셋을 만지기 전에</b>
        /// 만들어 넘긴다 — 여기서 새로 열면 넘겨받은 에셋 참조가 그 자리에서 죽는다.
        /// </summary>
        private static void BuildFarmScene(
            UnityEngine.SceneManagement.Scene scene,
            SpeciesDatabase speciesDb,
            ItemDatabase itemDb,
            CreatureSpeciesData startingSpecies,
            List<ItemData> items)
        {
            // 폰트는 씬에 직렬화하지 않는다 — Canvas 의 UiFontApplier 가 런타임에 붙인다.

            // 카메라
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.backgroundColor = new Color(0.62f, 0.83f, 0.94f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            cameraGo.AddComponent<CameraPanController>();

            // 땅 — 화면 아래쪽에 깔리는 사각형. 윗면이 FarmArea.GroundSurfaceY 에 오도록 배치한다.
            var ground = new GameObject("Ground");
            var groundRenderer = ground.AddComponent<SpriteRenderer>();
            groundRenderer.sprite = squareSprite;
            groundRenderer.color = new Color(0.55f, 0.42f, 0.28f);
            groundRenderer.sortingOrder = -10;
            ground.transform.localScale = new Vector3(
                FarmArea.HalfWidth * 2f + 8f, FarmArea.GroundThickness, 1f);
            ground.transform.position = new Vector3(
                0f, FarmArea.GroundSurfaceY - FarmArea.GroundThickness * 0.5f, 0f);

            // 잔디 — 땅 윗면에 얹는 얇은 띠. 지면 선을 눈에 띄게 한다.
            var grass = new GameObject("GrassTop");
            var grassRenderer = grass.AddComponent<SpriteRenderer>();
            grassRenderer.sprite = squareSprite;
            grassRenderer.color = new Color(0.45f, 0.75f, 0.35f);
            grassRenderer.sortingOrder = -9;
            grass.transform.localScale = new Vector3(FarmArea.HalfWidth * 2f + 8f, 0.4f, 1f);
            grass.transform.position = new Vector3(0f, FarmArea.GroundSurfaceY - 0.2f, 0f);

            // 농장 건물 5동 — 배경에 서 있고 클릭/터치에 반응한다.
            // x 간격 6, 본체 폭 3.4 → 맵 반너비 15 안에 전부 들어온다 (FarmLayoutTests 가 단언).
            var buildings = new List<FarmBuilding>
            {
                BuildFarmBuilding(BuildingKind.Barn, "헛간", -12f, new Color(0.78f, 0.34f, 0.28f)),
                BuildFarmBuilding(BuildingKind.Hatchery, "부화소", -6f, new Color(0.85f, 0.76f, 0.45f)),
                BuildFarmBuilding(BuildingKind.Storage, "창고", 0f, new Color(0.62f, 0.52f, 0.34f)),
                BuildFarmBuilding(BuildingKind.Shop, "상점", 6f, new Color(0.36f, 0.55f, 0.72f)),
                BuildFarmBuilding(BuildingKind.BattleGround, "전투터", 12f, new Color(0.7f, 0.35f, 0.35f))
            };

            // 파티 슬롯 — Roster.MaxParty 개를 미리 만들어 두고 PartyField 가 켜고 끈다.
            // 런타임 생성을 쓰지 않는 이유: 코드로 만든 오브젝트의 스프라이트 참조는 씬에 남지 않는다.
            var partyFieldGo = new GameObject("PartyField");
            var partyField = partyFieldGo.AddComponent<PartyField>();
            partyField.slots = new List<CreatureController>();

            for (int i = 0; i < Roster.MaxParty; i++)
            {
                var creatureGo = new GameObject("PartySlot" + i);
                creatureGo.transform.SetParent(partyFieldGo.transform, false);

                var creatureRenderer = creatureGo.AddComponent<SpriteRenderer>();
                creatureRenderer.sprite = circleSprite;
                creatureRenderer.color = startingSpecies.placeholderColor;
                creatureRenderer.sortingOrder = 1;

                var controller = creatureGo.AddComponent<CreatureController>();
                float creatureScale = startingSpecies.placeholderScale;
                creatureGo.transform.localScale = new Vector3(creatureScale, creatureScale, 1f);
                creatureGo.transform.position = FarmArea.OnGround(
                    -4f + i * 3f, creatureScale, 0.6f);

                partyField.slots.Add(controller);

                // 첫 슬롯만 켜 둔다. 나머지는 파티가 늘어날 때 PartyField 가 켠다.
                creatureGo.SetActive(i == 0);
            }

            // 플레이어
            var playerGo = new GameObject("Player");
            var playerRenderer = playerGo.AddComponent<SpriteRenderer>();
            playerRenderer.sprite = squareSprite;
            playerRenderer.color = new Color(0.25f, 0.45f, 0.9f);
            playerRenderer.sortingOrder = 2;
            playerGo.AddComponent<PlayerController>();
            playerGo.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
            playerGo.transform.position = FarmArea.OnGround(-1.5f, 0.8f, 0.5f);

            // UI
            var canvasGo = new GameObject("Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(UiFontApplier));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));

            var statusText = CreateText(canvasGo.transform, "StatusText",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(520f, 420f),
                TextAnchor.UpperLeft, 24);

            var logText = CreateText(canvasGo.transform, "LogText",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(1200f, 50f),
                TextAnchor.MiddleCenter, 26);

            var hudGo = new GameObject("GameHud");
            hudGo.transform.SetParent(canvasGo.transform, false);
            var hud = hudGo.AddComponent<GameHud>();
            hud.statusText = statusText;
            hud.logText = logText;

            // 액션 조율자. 화면의 조작은 전부 패널을 거쳐 이 메서드들을 부른다.
            var careGo = new GameObject("CareManager");
            var careManager = careGo.AddComponent<CareManager>();
            careManager.itemDatabase = itemDb;

            // ★ 농장에는 패널이 하나도 없다. 다섯 동이 전부 자기 씬을 갖는다.
            // 농장은 이제 '건물이 서 있는 바깥'이고 조작은 전부 안에서 한다.
            careManager.panels = new List<GamePanel>();
            careManager.raisingPanel = null;   // 육성은 헛간 씬 안에 있다
            careManager.partyField = partyField;

            // 건물 → 씬. 클릭하면 상태를 들려 보내고 그 씬으로 들어간다.
            for (int i = 0; i < buildings.Count; i++)
            {
                buildings[i].careManager = careManager;
                buildings[i].panel = null;
                buildings[i].sceneName = SceneNameOf(buildings[i].kind);
            }

            // 루트 조립
            var rootGo = new GameObject("GameRoot");
            var root = rootGo.AddComponent<GameRoot>();
            root.speciesDatabase = speciesDb;
            root.itemDatabase = itemDb;
            root.startingSpecies = startingSpecies;
            root.startingItems = new List<ItemData>(items);
            root.startingItemCounts = new List<int> { 3, 3, 1 };
            root.careManager = careManager;
            root.hud = hud;
            root.partyField = partyField;
            root.farmBuildings = buildings;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, FarmScenePath);
        }

        /// <summary>
        /// 전투 씬을 채운다. <paramref name="scene"/> 는 <see cref="BuildAll"/> 이 만들어 넘긴다 —
        /// 여기서 새로 열면 넘겨받은 에셋 참조가 그 자리에서 죽는다 (함정 3.9).
        ///
        /// 유닛 슬롯은 아군 <see cref="Roster.MaxParty"/> 개 + 적 <see cref="BattleField.MaxEnemies"/> 개를
        /// 미리 만들어 두고 <b>전부 꺼진 채로</b> 저장한다. 인원수에 맞춰 켜는 것은
        /// <see cref="BattleSceneRoot"/> 의 일이다 (함정 3.1).
        /// </summary>
        private static void BuildBattleScene(
            UnityEngine.SceneManagement.Scene scene,
            BattleMapData map,
            BattleEncounter practiceEncounter,
            CreatureSpeciesData practiceSpecies)
        {
            // 카메라 — 농장과 달리 팬하지 않는다. 전투 맵은 한 화면에 다 들어온다.
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.6f;
            camera.backgroundColor = new Color(0.18f, 0.2f, 0.24f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.transform.position = new Vector3(0f, 0.6f, -10f);

            // 탑뷰 바닥. 크기·색은 BattleSceneRoot 가 맵에서 읽어 덮어쓴다.
            var groundGo = new GameObject("BattleGround");
            var ground = groundGo.AddComponent<SpriteRenderer>();
            ground.sprite = squareSprite;
            ground.color = map != null ? map.groundColor : new Color(0.45f, 0.62f, 0.35f);
            ground.sortingOrder = -100;
            groundGo.transform.localScale = map != null
                ? new Vector3(map.groundSize.x, map.groundSize.y, 1f)
                : new Vector3(17f, 9f, 1f);
            groundGo.transform.position = Vector3.zero;

            var allySlots = new List<BattleUnitView>();
            var enemySlots = new List<BattleUnitView>();

            var allyRoot = new GameObject("AllySlots");
            for (int i = 0; i < Roster.MaxParty; i++)
            {
                allySlots.Add(BuildUnitView(allyRoot.transform, "AllySlot" + i,
                    new Color(0.55f, 0.85f, 0.35f)));
            }

            var enemyRoot = new GameObject("EnemySlots");
            for (int i = 0; i < BattleField.MaxEnemies; i++)
            {
                enemySlots.Add(BuildUnitView(enemyRoot.transform, "EnemySlot" + i,
                    new Color(0.8f, 0.35f, 0.35f)));
            }

            // UI — Canvas 하위여야 UiFontApplier 가 한글 폰트를 꽂아준다 (함정 3.7).
            var canvasGo = new GameObject("Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(UiFontApplier));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));

            var titleText = CreateText(canvasGo.transform, "TitleText",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f),
                new Vector2(1200f, 60f), TextAnchor.MiddleCenter, 34);
            titleText.color = Color.white;

            var logText = CreateText(canvasGo.transform, "BattleLogText",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 260f),
                new Vector2(1100f, 220f), TextAnchor.LowerLeft, 24);
            logText.color = new Color(0.9f, 0.9f, 0.78f);

            var speedText = CreateText(canvasGo.transform, "SpeedText",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-280f, 130f),
                new Vector2(240f, 44f), TextAnchor.MiddleCenter, 26);
            speedText.color = Color.white;

            var root = new GameObject("BattleSceneRoot").AddComponent<BattleSceneRoot>();
            root.ground = ground;
            root.defaultMap = map;
            root.allySlots = allySlots;
            root.enemySlots = enemySlots;
            root.titleText = titleText;
            root.logText = logText;
            root.speedText = speedText;
            root.practiceEncounter = practiceEncounter;
            root.practiceSpecies = practiceSpecies;

            // 버튼 3개 — 배속 2개와 나가기. 배속은 int 인덱스를 넘긴다 (함정 3.6).
            var buttonRoot = new GameObject("BattleButtons", typeof(RectTransform));
            buttonRoot.transform.SetParent(canvasGo.transform, false);
            CenterRect(buttonRoot.GetComponent<RectTransform>(), new Vector2(0f, -430f),
                new Vector2(1200f, 80f));

            var speed1 = CreatePanelButton(buttonRoot.transform, "Speed1x", "1x",
                new Vector2(-180f, 0f), new Vector2(140f, 64f), new Color(0.36f, 0.5f, 0.68f));
            UnityEventTools.AddIntPersistentListener(
                speed1.onClick, new UnityAction<int>(root.SetSpeed), 0);

            var speed2 = CreatePanelButton(buttonRoot.transform, "Speed2x", "2x",
                new Vector2(-20f, 0f), new Vector2(140f, 64f), new Color(0.36f, 0.5f, 0.68f));
            UnityEventTools.AddIntPersistentListener(
                speed2.onClick, new UnityAction<int>(root.SetSpeed), 1);

            var exitButton = CreatePanelButton(buttonRoot.transform, "Exit", "나가기",
                new Vector2(200f, 0f), new Vector2(200f, 64f), new Color(0.6f, 0.42f, 0.32f));
            UnityEventTools.AddVoidPersistentListener(
                exitButton.onClick, new UnityAction(root.ExitBattle));

            // 슬롯은 전부 꺼진 채로 저장한다 — 배선이 끝난 뒤에 끈다 (함정 3.8).
            for (int i = 0; i < allySlots.Count; i++)
            {
                allySlots[i].gameObject.SetActive(false);
            }

            for (int i = 0; i < enemySlots.Count; i++)
            {
                enemySlots[i].gameObject.SetActive(false);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, BattleScenePath);
        }

        // ---------- 건물 씬 공통 ----------

        /// <summary>건물 씬 하나가 되는 조각들. <see cref="BuildBuildingShell"/> 이 돌려준다.</summary>
        private class BuildingScenePieces
        {
            public GameObject Canvas;
            public Transform RoomRoot;
            public Transform PopupRoot;
            public CareManager Care;
            public GameHud Hud;
        }

        /// <summary>
        /// 빈 씬을 열고 스프라이트 참조를 되살린다. <b>건물 씬마다 부른다.</b>
        ///
        /// <c>NewScene</c> 은 참조되지 않는 에셋을 언로드하면서 그 전에 얻어 둔 참조의
        /// 네이티브 객체를 파괴한다 (함정 3.9). 씬이 늘어날수록 이 함정을 밟을 자리도 늘어나므로,
        /// "씬을 여는 것과 참조를 되살리는 것"을 한 함수로 묶어 빠뜨릴 수 없게 했다.
        /// </summary>
        private static UnityEngine.SceneManagement.Scene NextScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ReloadSpriteAssets();
            return scene;
        }

        /// <summary>경로에서 다시 로드한다. 없으면 크게 실패시킨다 — 0 은 정상 상황이 아니다.</summary>
        private static T RequireAsset<T>(string assetPath) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);

            if (asset == null)
            {
                Debug.LogError("[FarmCreature] 에셋 재로드 실패 — NewScene 이후 참조가 죽었다: "
                               + assetPath);
            }

            return asset;
        }

        /// <summary>
        /// 건물 씬의 공통 뼈대 — 카메라 · Canvas · EventSystem · 방/팝업 루트 · 조율자 · HUD.
        ///
        /// <b>방과 팝업의 부모를 나눈다.</b> <see cref="GamePanel.Open"/> 이 형제 패널을 닫으므로
        /// 한 부모에 두면 팝업을 여는 순간 방 화면이 통째로 꺼진다 (함정 3.13).
        /// 팝업이 없는 건물도 루트는 만들어 둔다 — 나중에 하나 붙일 때 이 함정을 다시 밟지 않게.
        /// </summary>
        private static BuildingScenePieces BuildBuildingShell(Color background, ItemDatabase itemDb)
        {
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.4f;
            camera.backgroundColor = background;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.transform.position = new Vector3(0f, 0f, -10f);

            var canvasGo = new GameObject("Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(UiFontApplier));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));

            var roomRoot = new GameObject("RoomRoot", typeof(RectTransform));
            roomRoot.transform.SetParent(canvasGo.transform, false);
            StretchFull(roomRoot.GetComponent<RectTransform>());

            var popupRoot = new GameObject("PopupRoot", typeof(RectTransform));
            popupRoot.transform.SetParent(canvasGo.transform, false);
            StretchFull(popupRoot.GetComponent<RectTransform>());

            var careGo = new GameObject("CareManager");
            var care = careGo.AddComponent<CareManager>();
            care.itemDatabase = itemDb;
            care.partyField = null;   // 필드는 농장에만 있다

            var statusText = CreateText(canvasGo.transform, "StatusText",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f),
                new Vector2(420f, 200f), TextAnchor.UpperLeft, 24);
            statusText.color = new Color(0.92f, 0.9f, 0.82f);

            var logText = CreateText(canvasGo.transform, "LogText",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f),
                new Vector2(1500f, 50f), TextAnchor.MiddleCenter, 26);
            logText.color = new Color(0.92f, 0.9f, 0.82f);

            var hudGo = new GameObject("GameHud");
            hudGo.transform.SetParent(canvasGo.transform, false);
            var hud = hudGo.AddComponent<GameHud>();
            hud.statusText = statusText;
            hud.logText = logText;

            return new BuildingScenePieces
            {
                Canvas = canvasGo,
                RoomRoot = roomRoot.transform,
                PopupRoot = popupRoot.transform,
                Care = care,
                Hud = hud
            };
        }

        /// <summary>
        /// 방 뒤에 깔리는 바닥. 건물마다 색만 다르다 — 실제 내부 그림이 들어올 자리다.
        /// </summary>
        private static void BuildRoomFloor(Transform roomRoot, Color color, string title)
        {
            var floorGo = new GameObject("RoomFloor", typeof(RectTransform), typeof(Image));
            floorGo.transform.SetParent(roomRoot, false);
            StretchFull(floorGo.GetComponent<RectTransform>());

            var image = floorGo.GetComponent<Image>();
            image.sprite = squareSprite;
            image.color = color;

            // 바닥이 패널보다 뒤에 있어야 한다. 먼저 만들었으므로 형제 순서상 이미 뒤다.
            floorGo.transform.SetAsFirstSibling();

            var titleText = CreatePanelText(roomRoot, "RoomTitle", new Vector2(0f, 470f),
                new Vector2(900f, 60f), TextAnchor.MiddleCenter, 40, new Color(0.95f, 0.92f, 0.84f));
            titleText.text = title;
        }

        /// <summary>
        /// 패널의 [닫기] 를 [농장으로] 로 바꾼다.
        ///
        /// 패널이 씬의 본체가 되면서 <b>닫는다는 개념이 없어졌다</b> — 닫으면 빈 방만 남는다.
        /// 버튼 이름도 <c>ExitToFarm</c> 으로 바꾼다. 무결성 검사가 이 이름으로 나가는 길을 찾는다.
        /// </summary>
        private static void RewireCloseToExit(Transform panel, BuildingSceneRoot root)
        {
            var close = panel.Find("Close");

            if (close == null)
            {
                Debug.LogError("[FarmCreature] " + panel.name
                               + " 에 Close 버튼이 없어 나가는 길을 만들지 못했다");
                return;
            }

            var button = close.GetComponent<Button>();

            // 기존 Close 리스너를 먼저 지운다. 남겨두면 누를 때 방이 꺼지고 씬도 바뀐다.
            while (button.onClick.GetPersistentEventCount() > 0)
            {
                UnityEventTools.RemovePersistentListener(button.onClick, 0);
            }

            UnityEventTools.AddVoidPersistentListener(
                button.onClick, new UnityAction(root.ExitToFarm));

            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = "농장으로";
            }

            close.name = "ExitToFarm";
        }

        /// <summary>배선을 끝낸 뒤 씬을 저장한다.</summary>
        private static void SaveScene(UnityEngine.SceneManagement.Scene scene, string path)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
        }

        /// <summary>건물 종류 → 그 건물의 씬 이름. 농장의 진입 배선이 이것을 쓴다.</summary>
        private static string SceneNameOf(BuildingKind kind)
        {
            switch (kind)
            {
                case BuildingKind.Barn:
                    return BarnSceneRoot.SceneName;
                case BuildingKind.Hatchery:
                    return HatcherySceneRoot.SceneName;
                case BuildingKind.Storage:
                    return StorageSceneRoot.SceneName;
                case BuildingKind.Shop:
                    return ShopSceneRoot.SceneName;
                case BuildingKind.BattleGround:
                    return BattleGroundSceneRoot.SceneName;
                default:
                    return string.Empty;
            }
        }

        // ---------- 건물 씬 넷 (기존 패널이 그대로 옮겨온다) ----------

        private static void BuildHatcheryScene(UnityEngine.SceneManagement.Scene scene)
        {
            var speciesDb = RequireAsset<SpeciesDatabase>(DataRoot + "/SpeciesDatabase.asset");
            var itemDb = RequireAsset<ItemDatabase>(DataRoot + "/ItemDatabase.asset");
            var eggDb = RequireAsset<EggDatabase>(DataRoot + "/EggDatabase.asset");

            var pieces = BuildBuildingShell(new Color(0.2f, 0.18f, 0.12f), itemDb);
            BuildRoomFloor(pieces.RoomRoot, new Color(0.3f, 0.27f, 0.16f, 1f), "부화소");

            var panel = BuildHatchPanel(pieces.RoomRoot, pieces.Care, eggDb);

            var root = new GameObject("HatcherySceneRoot").AddComponent<HatcherySceneRoot>();
            root.speciesDatabase = speciesDb;
            root.itemDatabase = itemDb;
            root.careManager = pieces.Care;
            root.hud = pieces.Hud;
            root.roomPanel = panel;

            pieces.Care.panels = new List<GamePanel> { panel };

            RewireCloseToExit(panel.transform, root);
            panel.gameObject.SetActive(true);

            SaveScene(scene, HatcheryScenePath);
        }

        private static void BuildStorageScene(UnityEngine.SceneManagement.Scene scene)
        {
            var speciesDb = RequireAsset<SpeciesDatabase>(DataRoot + "/SpeciesDatabase.asset");
            var itemDb = RequireAsset<ItemDatabase>(DataRoot + "/ItemDatabase.asset");

            var pieces = BuildBuildingShell(new Color(0.16f, 0.14f, 0.11f), itemDb);
            BuildRoomFloor(pieces.RoomRoot, new Color(0.28f, 0.23f, 0.16f, 1f), "창고");

            var panel = BuildStoragePanel(pieces.RoomRoot, pieces.Care, itemDb);

            var root = new GameObject("StorageSceneRoot").AddComponent<StorageSceneRoot>();
            root.speciesDatabase = speciesDb;
            root.itemDatabase = itemDb;
            root.careManager = pieces.Care;
            root.hud = pieces.Hud;
            root.roomPanel = panel;

            pieces.Care.panels = new List<GamePanel> { panel };

            RewireCloseToExit(panel.transform, root);
            panel.gameObject.SetActive(true);

            SaveScene(scene, StorageScenePath);
        }

        private static void BuildShopScene(UnityEngine.SceneManagement.Scene scene)
        {
            var speciesDb = RequireAsset<SpeciesDatabase>(DataRoot + "/SpeciesDatabase.asset");
            var itemDb = RequireAsset<ItemDatabase>(DataRoot + "/ItemDatabase.asset");
            var eggDb = RequireAsset<EggDatabase>(DataRoot + "/EggDatabase.asset");
            var shopStock = RequireAsset<ShopStockDatabase>(DataRoot + "/ShopStockDatabase.asset");

            var pieces = BuildBuildingShell(new Color(0.12f, 0.15f, 0.2f), itemDb);
            BuildRoomFloor(pieces.RoomRoot, new Color(0.2f, 0.26f, 0.34f, 1f), "상점");

            var panel = BuildShopPanel(pieces.RoomRoot, pieces.Care, shopStock, eggDb);

            var root = new GameObject("ShopSceneRoot").AddComponent<ShopSceneRoot>();
            root.speciesDatabase = speciesDb;
            root.itemDatabase = itemDb;
            root.careManager = pieces.Care;
            root.hud = pieces.Hud;
            root.roomPanel = panel;

            pieces.Care.panels = new List<GamePanel> { panel };

            RewireCloseToExit(panel.transform, root);
            panel.gameObject.SetActive(true);

            SaveScene(scene, ShopScenePath);
        }

        /// <summary>
        /// 전투터 씬. 다른 건물과 다른 점 하나 — 여기서 <b>또 다른 씬(전투)으로</b> 나갔다 돌아온다.
        /// <see cref="BattlePanel.battleReturnScene"/> 에 자기 씬 이름을 적어,
        /// 전투가 끝나면 농장이 아니라 여기로 돌아오게 한다.
        /// </summary>
        private static void BuildBattleGroundScene(UnityEngine.SceneManagement.Scene scene)
        {
            var speciesDb = RequireAsset<SpeciesDatabase>(DataRoot + "/SpeciesDatabase.asset");
            var itemDb = RequireAsset<ItemDatabase>(DataRoot + "/ItemDatabase.asset");
            var stageDb = RequireAsset<StageMapDatabase>(DataRoot + "/StageMapDatabase.asset");

            var pieces = BuildBuildingShell(new Color(0.18f, 0.11f, 0.11f), itemDb);
            BuildRoomFloor(pieces.RoomRoot, new Color(0.3f, 0.18f, 0.17f, 1f), "전투터");

            var panel = BuildBattlePanel(pieces.RoomRoot, pieces.Care, stageDb, itemDb);
            panel.battleReturnScene = BattleGroundSceneRoot.SceneName;

            var root = new GameObject("BattleGroundSceneRoot").AddComponent<BattleGroundSceneRoot>();
            root.speciesDatabase = speciesDb;
            root.itemDatabase = itemDb;
            root.careManager = pieces.Care;
            root.hud = pieces.Hud;
            root.roomPanel = panel;

            pieces.Care.panels = new List<GamePanel> { panel };

            RewireCloseToExit(panel.transform, root);
            panel.gameObject.SetActive(true);

            SaveScene(scene, BattleGroundScenePath);
        }

        /// <summary>
        /// 헛간 안 씬. 왼쪽은 헛간지기와 파티, 오른쪽은 보관함 격자다.
        ///
        /// <b>방 화면은 열린 채로 저장한다</b> — 씬의 본체라서 닫아 두면 빈 방이 뜬다.
        /// 함정 3.8("패널은 닫아 저장")은 팝업 규칙이고, 여기서는 육성 팝업만 그것을 따른다.
        ///
        /// <b>방과 팝업의 부모를 나눈 이유</b>: <see cref="GamePanel.Open"/> 은 <b>형제 패널을 닫는다.</b>
        /// 한 부모에 두면 [육성]을 누르는 순간 방 화면이 통째로 꺼진다.
        ///
        /// 칸은 <see cref="Roster.MaxMembers"/> 개, 파티 줄은 <see cref="Roster.MaxParty"/> 개를
        /// 씬 생성 시점에 고정한다 (함정 3.1).
        /// </summary>
        private static void BuildBarnScene(UnityEngine.SceneManagement.Scene scene)
        {
            var speciesDb = RequireAsset<SpeciesDatabase>(DataRoot + "/SpeciesDatabase.asset");
            var itemDb = RequireAsset<ItemDatabase>(DataRoot + "/ItemDatabase.asset");

            // 헛간은 방 화면이 자기 배경을 갖고 있어 RoomFloor 를 깔지 않는다.
            var pieces = BuildBuildingShell(new Color(0.16f, 0.12f, 0.1f), itemDb);

            var roomPanel = BuildBarnRoomPanel(pieces.RoomRoot, pieces.Care);
            var raisingPanel = BuildRaisingPanel(pieces.PopupRoot, pieces.Care);

            pieces.Care.panels = new List<GamePanel> { roomPanel, raisingPanel };
            pieces.Care.raisingPanel = raisingPanel;

            var rootGo = new GameObject("BarnSceneRoot");
            var root = rootGo.AddComponent<BarnSceneRoot>();
            root.speciesDatabase = speciesDb;
            root.itemDatabase = itemDb;
            root.careManager = pieces.Care;
            root.hud = pieces.Hud;
            root.roomPanel = roomPanel;

            // [나가기] 는 방이 아니라 루트의 일이다 — 왕복 규칙이 BuildingSceneRoot 에 있다.
            var exitButton = CreatePanelButton(pieces.RoomRoot, "ExitToFarm", "농장으로",
                new Vector2(690f, -260f), new Vector2(220f, 64f), new Color(0.6f, 0.42f, 0.32f));
            UnityEventTools.AddVoidPersistentListener(
                exitButton.onClick, new UnityAction(root.ExitToFarm));

            // 방은 켜진 채로, 팝업은 꺼진 채로 저장한다.
            roomPanel.gameObject.SetActive(true);
            raisingPanel.gameObject.SetActive(false);

            SaveScene(scene, BarnScenePath);
        }

        /// <summary>
        /// 헛간 방 화면. 칸 버튼만 <b>int 인자 persistent listener</b> 로 자기 번호를 들고 있고
        /// (함정 3.6), 아래쪽 조작 버튼은 고른 칸을 읽는 무인자 메서드를 부른다.
        /// </summary>
        private static BarnRoomPanel BuildBarnRoomPanel(Transform parent, CareManager careManager)
        {
            var roomGo = new GameObject("BarnRoomPanel", typeof(RectTransform), typeof(Image));
            roomGo.transform.SetParent(parent, false);
            StretchFull(roomGo.GetComponent<RectTransform>());

            var roomImage = roomGo.GetComponent<Image>();
            roomImage.sprite = squareSprite;
            roomImage.color = new Color(0.22f, 0.16f, 0.13f, 1f);   // 헛간 바닥

            var panel = roomGo.AddComponent<BarnRoomPanel>();
            panel.careManager = careManager;

            // ---------- 왼쪽: 헛간지기와 파티 ----------

            var portraitGo = new GameObject("KeeperPortrait", typeof(RectTransform), typeof(Image));
            portraitGo.transform.SetParent(roomGo.transform, false);
            CenterRect(portraitGo.GetComponent<RectTransform>(),
                new Vector2(-600f, 160f), new Vector2(300f, 300f));

            var portrait = portraitGo.GetComponent<Image>();
            portrait.sprite = squareSprite;
            portrait.color = new Color(0.52f, 0.42f, 0.3f);   // 실제 그림이 들어올 자리
            panel.keeperPortrait = portrait;

            panel.keeperText = CreatePanelText(roomGo.transform, "KeeperText",
                new Vector2(-600f, -60f), new Vector2(360f, 200f),
                TextAnchor.UpperCenter, 24, new Color(0.9f, 0.86f, 0.74f));

            CreatePanelText(roomGo.transform, "PartyTitle",
                new Vector2(-600f, -215f), new Vector2(360f, 40f),
                TextAnchor.MiddleCenter, 26, new Color(0.75f, 0.85f, 0.7f))
                .text = "밖에 나가 있는 케어";

            var partySlots = new Text[Roster.MaxParty];
            float partyY = -270f;

            for (int i = 0; i < Roster.MaxParty; i++)
            {
                partySlots[i] = CreatePanelText(roomGo.transform, "PartySlot" + i,
                    new Vector2(-600f, partyY), new Vector2(360f, 42f),
                    TextAnchor.MiddleLeft, 22, new Color(0.88f, 0.88f, 0.82f));
                partyY -= 48f;
            }

            panel.partySlotTexts = partySlots;

            // ---------- 오른쪽: 보관함 격자 ----------

            panel.boxTitleText = CreatePanelText(roomGo.transform, "BoxTitle",
                new Vector2(420f, 420f), new Vector2(900f, 50f),
                TextAnchor.MiddleCenter, 34, Color.white);
            panel.boxTitleText.text = "보관함";

            var cellTexts = new Text[Roster.MaxMembers];
            var cellBackgrounds = new Image[Roster.MaxMembers];

            for (int i = 0; i < Roster.MaxMembers; i++)
            {
                int column = i % BarnRoomPanel.Columns;
                int row = i / BarnRoomPanel.Columns;

                var position = new Vector2(
                    420f + (column - (BarnRoomPanel.Columns - 1) * 0.5f) * 224f,
                    262f - row * 224f);

                var cellButton = CreatePanelButton(roomGo.transform, "Cell" + i, string.Empty,
                    position, new Vector2(200f, 200f), new Color(0.2f, 0.19f, 0.24f, 0.95f));
                UnityEventTools.AddIntPersistentListener(
                    cellButton.onClick, new UnityAction<int>(panel.SelectSlot), i);

                cellBackgrounds[i] = cellButton.GetComponent<Image>();

                // CreatePanelButton 이 만든 라벨을 그대로 칸 글씨로 쓴다. 크기만 칸에 맞춘다.
                var label = cellButton.GetComponentInChildren<Text>();
                label.fontSize = 20;
                CenterRect(label.rectTransform, Vector2.zero, new Vector2(190f, 190f));
                cellTexts[i] = label;
            }

            panel.cellTexts = cellTexts;
            panel.cellBackgrounds = cellBackgrounds;

            // ---------- 아래: 고른 케어와 조작 ----------

            panel.detailText = CreatePanelText(roomGo.transform, "Detail",
                new Vector2(420f, -160f), new Vector2(900f, 44f),
                TextAnchor.MiddleCenter, 24, new Color(0.9f, 0.9f, 0.78f));

            var partyButton = CreatePanelButton(roomGo.transform, "TogglePartySelected", "내보내기",
                new Vector2(180f, -260f), new Vector2(240f, 64f), new Color(0.42f, 0.6f, 0.36f));
            UnityEventTools.AddVoidPersistentListener(
                partyButton.onClick, new UnityAction(panel.TogglePartyOfSelected));
            panel.partyButtonLabel = partyButton.GetComponentInChildren<Text>();

            var raiseButton = CreatePanelButton(roomGo.transform, "RaiseSelected", "육성",
                new Vector2(440f, -260f), new Vector2(200f, 64f), new Color(0.55f, 0.45f, 0.65f));
            UnityEventTools.AddVoidPersistentListener(
                raiseButton.onClick, new UnityAction(panel.RaiseSelected));

            return panel;
        }

        /// <summary>
        /// 유닛 슬롯 하나 — 몸통(Circle) · 체력바 바탕/채움(Square) · 이름표(TextMesh).
        /// uGUI 가 아니라 SpriteRenderer/TextMesh 를 쓰는 이유는 <see cref="BattleUnitView"/> 주석에 있다.
        /// </summary>
        private static BattleUnitView BuildUnitView(Transform parent, string name, Color bodyColor)
        {
            const float barWidth = 1.3f;
            const float barY = 0.85f;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(go.transform, false);
            var body = bodyGo.AddComponent<SpriteRenderer>();
            body.sprite = circleSprite;
            body.color = bodyColor;
            body.sortingOrder = 0;

            var backGo = new GameObject("HealthBarBack");
            backGo.transform.SetParent(go.transform, false);
            var back = backGo.AddComponent<SpriteRenderer>();
            back.sprite = squareSprite;
            back.color = new Color(0.1f, 0.1f, 0.12f, 0.85f);
            back.sortingOrder = 1;
            backGo.transform.localScale = new Vector3(barWidth, 0.16f, 1f);
            backGo.transform.localPosition = new Vector3(0f, barY, 0f);

            var fillGo = new GameObject("HealthBarFill");
            fillGo.transform.SetParent(go.transform, false);
            var fill = fillGo.AddComponent<SpriteRenderer>();
            fill.sprite = squareSprite;
            fill.color = new Color(0.4f, 0.82f, 0.4f);
            fill.sortingOrder = 2;
            fillGo.transform.localScale = new Vector3(barWidth, 0.12f, 1f);
            fillGo.transform.localPosition = new Vector3(0f, barY, 0f);

            var labelGo = new GameObject("NameLabel");
            labelGo.transform.SetParent(go.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, barY + 0.22f, 0f);
            var label = labelGo.AddComponent<TextMesh>();
            label.text = name;
            label.characterSize = 0.09f;
            label.fontSize = 56;
            label.anchor = TextAnchor.LowerCenter;
            label.alignment = TextAlignment.Center;
            label.color = Color.white;
            labelGo.GetComponent<MeshRenderer>().sortingOrder = 3;

            var view = go.AddComponent<BattleUnitView>();
            view.body = body;
            view.healthBarBack = back;
            view.healthBarFill = fill;
            view.nameLabel = label;
            view.healthBarWidth = barWidth;

            return view;
        }

        /// <summary>
        /// 배경 건물. 본체 + 지붕 + 문 세 조각으로 만들고, 본체에 콜라이더를 달아 클릭을 받는다.
        /// 캐릭터보다 뒤(sortingOrder 낮음)에 그린다.
        /// </summary>
        private static FarmBuilding BuildFarmBuilding(BuildingKind kind, string label, float x, Color bodyColor)
        {
            const float bodyWidth = 3.4f;
            const float bodyHeight = 2.6f;
            const float roofHeight = 0.7f;

            var go = new GameObject("Building_" + label);
            var body = go.AddComponent<SpriteRenderer>();
            body.sprite = squareSprite;
            body.color = bodyColor;
            body.sortingOrder = -5;
            go.transform.localScale = new Vector3(bodyWidth, bodyHeight, 1f);
            go.transform.position = FarmArea.OnGround(x, bodyHeight, 0f);

            // 콜라이더는 로컬 기준 1x1 (스케일이 곱해져 본체 크기가 된다).
            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;

            var roof = new GameObject("Roof");
            var roofRenderer = roof.AddComponent<SpriteRenderer>();
            roofRenderer.sprite = squareSprite;
            roofRenderer.color = new Color(bodyColor.r * 0.55f, bodyColor.g * 0.55f, bodyColor.b * 0.55f);
            roofRenderer.sortingOrder = -4;
            roof.transform.SetParent(go.transform, false);
            // 부모 스케일이 곱해지므로 로컬 값은 비율로 준다.
            roof.transform.localScale = new Vector3(1.12f, roofHeight / bodyHeight, 1f);
            roof.transform.localPosition = new Vector3(0f, 0.5f + (roofHeight / bodyHeight) * 0.5f, 0f);

            var door = new GameObject("Door");
            var doorRenderer = door.AddComponent<SpriteRenderer>();
            doorRenderer.sprite = squareSprite;
            doorRenderer.color = new Color(0.32f, 0.2f, 0.14f);
            doorRenderer.sortingOrder = -4;
            door.transform.SetParent(go.transform, false);
            door.transform.localScale = new Vector3(0.28f, 0.5f, 1f);
            door.transform.localPosition = new Vector3(0f, -0.25f, 0f);

            // 이름표 — 지붕 위에 띄운다. World Space Canvas 없이 TextMesh 를 쓴다.
            var sign = new GameObject("Sign");
            sign.transform.SetParent(go.transform, false);
            sign.transform.localScale = new Vector3(1f / bodyWidth, 1f / bodyHeight, 1f);
            sign.transform.localPosition = new Vector3(0f, 0.5f + (roofHeight / bodyHeight) + 0.06f, 0f);

            var textMesh = sign.AddComponent<TextMesh>();
            textMesh.text = label;
            // 월드 높이 ≈ characterSize × fontSize × 0.1 → 약 0.45 유닛 (건물 높이의 1/6 정도)
            textMesh.characterSize = 0.07f;
            textMesh.fontSize = 64;
            textMesh.anchor = TextAnchor.LowerCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.color = new Color(0.15f, 0.12f, 0.1f);
            sign.GetComponent<MeshRenderer>().sortingOrder = -3;

            var farmBuilding = go.AddComponent<FarmBuilding>();
            farmBuilding.kind = kind;
            farmBuilding.displayName = label;

            return farmBuilding;
        }


        // ---------- 패널 ----------

        /// <summary>
        /// 창고 패널. 행은 <see cref="ItemDatabase"/> 인덱스와 1:1 이고, 장착 버튼은
        /// <b>int 인자 persistent listener</b> 로 그 인덱스를 넘긴다.
        /// Object 인자 등록(AddObjectPersistentListener)은 m_MethodName 이 빈 항목만 남긴다 (함정 3.6).
        /// </summary>
        private static StoragePanel BuildStoragePanel(
            Transform parent, CareManager careManager, ItemDatabase itemDb)
        {
            var panelGo = CreatePanelBackground(parent, "StoragePanel", new Vector2(900f, 560f));
            var panel = panelGo.AddComponent<StoragePanel>();

            panel.titleText = CreatePanelText(panelGo.transform, "Title", new Vector2(0f, 230f),
                new Vector2(860f, 50f), TextAnchor.MiddleCenter, 34, Color.white);
            panel.titleText.text = "창고 — 장비";

            int rowCount = itemDb != null && itemDb.items != null ? itemDb.items.Count : 0;

            // 0 은 정상 상황이 아니다. 아이템이 하나도 없는 게임이 아니라, 참조가 죽었다는 뜻이다.
            // 조용히 넘어가면 [장착] 버튼 없는 창고가 그대로 저장된다.
            if (rowCount == 0)
            {
                Debug.LogError("[FarmCreature] 창고 패널: 아이템이 0개다. "
                    + "ItemDatabase 참조가 죽었거나(NewScene 이후 로드했는지 확인) 데이터베이스가 비어 있다. "
                    + "itemDb==null? " + (itemDb == null));
            }

            var rows = new Text[rowCount];
            float rowY = 140f;

            for (int i = 0; i < rowCount; i++)
            {
                rows[i] = CreatePanelText(panelGo.transform, "Row" + i, new Vector2(-150f, rowY),
                    new Vector2(560f, 50f), TextAnchor.MiddleLeft, 26, Color.white);

                var equipButton = CreatePanelButton(panelGo.transform, "Equip" + i, "장착",
                    new Vector2(300f, rowY), new Vector2(150f, 54f),
                    new Color(0.35f, 0.55f, 0.42f));
                UnityEventTools.AddIntPersistentListener(
                    equipButton.onClick, new UnityAction<int>(careManager.DoEquip), i);

                rowY -= 80f;
            }

            panel.rowTexts = rows;

            panel.equippedText = CreatePanelText(panelGo.transform, "EquippedLine", new Vector2(0f, -100f),
                new Vector2(860f, 40f), TextAnchor.MiddleCenter, 26, new Color(0.85f, 0.85f, 0.7f));

            var unequipButton = CreatePanelButton(panelGo.transform, "Unequip", "해제",
                new Vector2(-150f, -190f), new Vector2(220f, 60f), new Color(0.6f, 0.42f, 0.32f));
            UnityEventTools.AddVoidPersistentListener(
                unequipButton.onClick, new UnityAction(careManager.DoUnequip));

            var closeButton = CreatePanelButton(panelGo.transform, "Close", "닫기",
                new Vector2(150f, -190f), new Vector2(220f, 60f), new Color(0.4f, 0.4f, 0.46f));
            UnityEventTools.AddVoidPersistentListener(
                closeButton.onClick, new UnityAction(panel.Close));

            return panel;
        }

        /// <summary>
        /// 상점 패널. 행은 <see cref="ShopStockDatabase"/> 인덱스와 1:1 이고,
        /// [구매] 버튼은 <b>int 인자 persistent listener</b> 로 그 인덱스를 넘긴다 (함정 3.6).
        /// </summary>
        private static ShopPanel BuildShopPanel(
            Transform parent, CareManager careManager, ShopStockDatabase stock, EggDatabase eggDb)
        {
            var panelGo = CreatePanelBackground(parent, "ShopPanel", new Vector2(900f, 620f));
            var panel = panelGo.AddComponent<ShopPanel>();
            panel.stockDatabase = stock;
            panel.eggDatabase = eggDb;
            panel.careManager = careManager;

            panel.titleText = CreatePanelText(panelGo.transform, "Title", new Vector2(0f, 260f),
                new Vector2(860f, 50f), TextAnchor.MiddleCenter, 34, Color.white);
            panel.titleText.text = "상점";

            int rowCount = stock != null ? stock.Count : 0;

            // 0 은 정상 상황이 아니다 — 참조가 죽었다는 뜻이다 (함정 3.9/3.10).
            if (rowCount == 0)
            {
                Debug.LogError("[FarmCreature] 상점 패널: 판매 항목이 0개다. "
                    + "ShopStockDatabase 참조가 죽었거나 비어 있다. stock==null? " + (stock == null));
            }

            var rows = new Text[rowCount];
            float rowY = 160f;

            for (int i = 0; i < rowCount; i++)
            {
                rows[i] = CreatePanelText(panelGo.transform, "Row" + i, new Vector2(-150f, rowY),
                    new Vector2(560f, 50f), TextAnchor.MiddleLeft, 26, Color.white);

                var buyButton = CreatePanelButton(panelGo.transform, "Buy" + i, "구매",
                    new Vector2(300f, rowY), new Vector2(150f, 54f),
                    new Color(0.36f, 0.5f, 0.68f));
                UnityEventTools.AddIntPersistentListener(
                    buyButton.onClick, new UnityAction<int>(panel.Buy), i);

                rowY -= 80f;
            }

            panel.rowTexts = rows;

            var closeButton = CreatePanelButton(panelGo.transform, "Close", "닫기",
                new Vector2(0f, -230f), new Vector2(220f, 60f), new Color(0.4f, 0.4f, 0.46f));
            UnityEventTools.AddVoidPersistentListener(
                closeButton.onClick, new UnityAction(panel.Close));

            return panel;
        }

        /// <summary>
        /// 부화소 패널. 행은 <see cref="EggDatabase"/> 인덱스와 1:1 이고
        /// [부화] 버튼이 그 인덱스를 int 인자로 넘긴다 (함정 3.6).
        /// 알이 1종뿐이라 상점(620)보다 낮은 420 이다 — 알이 4종을 넘으면 올려야 한다.
        /// </summary>
        private static HatchPanel BuildHatchPanel(
            Transform parent, CareManager careManager, EggDatabase eggDb)
        {
            var panelGo = CreatePanelBackground(parent, "HatchPanel", new Vector2(900f, 420f));
            var panel = panelGo.AddComponent<HatchPanel>();
            panel.eggDatabase = eggDb;
            panel.careManager = careManager;

            panel.titleText = CreatePanelText(panelGo.transform, "Title", new Vector2(0f, 160f),
                new Vector2(860f, 50f), TextAnchor.MiddleCenter, 34, Color.white);
            panel.titleText.text = "부화소";

            int rowCount = eggDb != null && eggDb.eggs != null ? eggDb.eggs.Count : 0;

            // 0 은 정상 상황이 아니다 — 참조가 죽었다는 뜻이다 (함정 3.9/3.10).
            if (rowCount == 0)
            {
                Debug.LogError("[FarmCreature] 부화소 패널: 알이 0종이다. "
                    + "EggDatabase 참조가 죽었거나 비어 있다. eggDb==null? " + (eggDb == null));
            }

            var rows = new Text[rowCount];
            float rowY = 60f;

            for (int i = 0; i < rowCount; i++)
            {
                rows[i] = CreatePanelText(panelGo.transform, "Row" + i, new Vector2(-150f, rowY),
                    new Vector2(560f, 50f), TextAnchor.MiddleLeft, 26, Color.white);

                var hatchButton = CreatePanelButton(panelGo.transform, "Hatch" + i, "부화",
                    new Vector2(300f, rowY), new Vector2(150f, 54f),
                    new Color(0.55f, 0.45f, 0.28f));
                UnityEventTools.AddIntPersistentListener(
                    hatchButton.onClick, new UnityAction<int>(panel.Hatch), i);

                rowY -= 80f;
            }

            panel.rowTexts = rows;

            var hatchCloseButton = CreatePanelButton(panelGo.transform, "Close", "닫기",
                new Vector2(0f, -140f), new Vector2(220f, 60f), new Color(0.4f, 0.4f, 0.46f));
            UnityEventTools.AddVoidPersistentListener(
                hatchCloseButton.onClick, new UnityAction(panel.Close));

            return panel;
        }

        /// <summary>
        /// 육성 패널. 케어를 직접 클릭하면 열린다.
        /// 훈련 버튼은 <see cref="StatType"/> 인덱스(0=공격, 1=방어, 2=체력)를 넘긴다.
        /// </summary>
        private static RaisingPanel BuildRaisingPanel(Transform parent, CareManager careManager)
        {
            var panelGo = CreatePanelBackground(parent, "RaisingPanel", new Vector2(900f, 520f));
            var panel = panelGo.AddComponent<RaisingPanel>();

            panel.titleText = CreatePanelText(panelGo.transform, "Title", new Vector2(0f, 200f),
                new Vector2(860f, 50f), TextAnchor.MiddleCenter, 34, Color.white);
            panel.titleText.text = "육성";

            panel.statusText = CreatePanelText(panelGo.transform, "Status", new Vector2(0f, 120f),
                new Vector2(860f, 80f), TextAnchor.MiddleCenter, 26, new Color(0.85f, 0.85f, 0.7f));

            var feedButton = CreatePanelButton(panelGo.transform, "Feed", "먹이주기",
                new Vector2(0f, 30f), new Vector2(300f, 64f), new Color(0.42f, 0.6f, 0.36f));
            UnityEventTools.AddVoidPersistentListener(
                feedButton.onClick, new UnityAction(careManager.DoFeed));

            var trainLabels = new[] { "공격 훈련", "방어 훈련", "체력 훈련" };
            var trainX = new[] { -270f, 0f, 270f };

            for (int i = 0; i < trainLabels.Length; i++)
            {
                var trainButton = CreatePanelButton(panelGo.transform, "Train" + i, trainLabels[i],
                    new Vector2(trainX[i], -55f), new Vector2(250f, 64f),
                    new Color(0.55f, 0.45f, 0.65f));
                UnityEventTools.AddIntPersistentListener(
                    trainButton.onClick, new UnityAction<int>(careManager.DoTrain), i);
            }

            var saveButton = CreatePanelButton(panelGo.transform, "Save", "저장",
                new Vector2(-150f, -160f), new Vector2(220f, 60f), new Color(0.36f, 0.5f, 0.68f));
            UnityEventTools.AddVoidPersistentListener(
                saveButton.onClick, new UnityAction(careManager.DoSave));

            var closeButton = CreatePanelButton(panelGo.transform, "Close", "닫기",
                new Vector2(150f, -160f), new Vector2(220f, 60f), new Color(0.4f, 0.4f, 0.46f));
            UnityEventTools.AddVoidPersistentListener(
                closeButton.onClick, new UnityAction(panel.Close));

            return panel;
        }

        /// <summary>
        /// 헛간 패널. 줄 수는 <see cref="Roster.MaxMembers"/> 로 고정이고,
        /// 각 줄의 버튼이 persistent listener 로 <b>보유 목록 인덱스</b>를 들고 있다.
        /// 그래서 <see cref="Roster"/> 는 보유 순서를 절대 바꾸지 않는다.
        /// </summary>
        /// <summary>
        /// 전투터 패널. <b>행은 항상 6개다</b> — 데이터 개수와 무관하다.
        /// 맵 모드와 스테이지 모드가 같은 6칸을 번갈아 쓰므로, 맵이 늘든 스테이지가 늘든
        /// 씬은 그대로다 (함정 3.1 을 정면으로 푸는 방법, 설계 5절).
        /// 줄 버튼은 <b>int 인자 persistent listener</b> 로 줄 번호를 넘긴다 (함정 3.6).
        /// </summary>
        private static BattlePanel BuildBattlePanel(
            Transform parent, CareManager careManager,
            StageMapDatabase stageDb, ItemDatabase itemDb)
        {
            var panelGo = CreatePanelBackground(parent, "BattlePanel", new Vector2(1100f, 700f));
            var panel = panelGo.AddComponent<BattlePanel>();
            panel.stageDatabase = stageDb;
            panel.itemDatabase = itemDb;
            panel.careManager = careManager;

            panel.titleText = CreatePanelText(panelGo.transform, "Title", new Vector2(0f, 300f),
                new Vector2(1040f, 50f), TextAnchor.MiddleCenter, 34, Color.white);
            panel.titleText.text = "전투터";

            panel.infoText = CreatePanelText(panelGo.transform, "Info", new Vector2(0f, 245f),
                new Vector2(1040f, 44f), TextAnchor.MiddleCenter, 24, new Color(0.85f, 0.85f, 0.7f));

            var rows = new Text[BattlePanel.RowsPerPage];
            float rowY = 175f;

            for (int i = 0; i < BattlePanel.RowsPerPage; i++)
            {
                rows[i] = CreatePanelText(panelGo.transform, "Row" + i, new Vector2(-120f, rowY),
                    new Vector2(700f, 50f), TextAnchor.MiddleLeft, 24, Color.white);

                var selectButton = CreatePanelButton(panelGo.transform, "Select" + i, "선택",
                    new Vector2(380f, rowY), new Vector2(150f, 54f),
                    new Color(0.68f, 0.36f, 0.34f));
                UnityEventTools.AddIntPersistentListener(
                    selectButton.onClick, new UnityAction<int>(panel.SelectRow), i);

                rowY -= 70f;
            }

            panel.rowTexts = rows;

            var prevButton = CreatePanelButton(panelGo.transform, "PrevPage", "이전",
                new Vector2(-330f, -290f), new Vector2(150f, 56f), new Color(0.4f, 0.45f, 0.55f));
            UnityEventTools.AddVoidPersistentListener(
                prevButton.onClick, new UnityAction(panel.PreviousPage));

            var nextButton = CreatePanelButton(panelGo.transform, "NextPage", "다음",
                new Vector2(-165f, -290f), new Vector2(150f, 56f), new Color(0.4f, 0.45f, 0.55f));
            UnityEventTools.AddVoidPersistentListener(
                nextButton.onClick, new UnityAction(panel.NextPage));

            var backButton = CreatePanelButton(panelGo.transform, "Back", "뒤로",
                new Vector2(165f, -290f), new Vector2(150f, 56f), new Color(0.5f, 0.45f, 0.35f));
            UnityEventTools.AddVoidPersistentListener(
                backButton.onClick, new UnityAction(panel.GoBack));

            var closeButton = CreatePanelButton(panelGo.transform, "Close", "닫기",
                new Vector2(330f, -290f), new Vector2(150f, 56f), new Color(0.4f, 0.4f, 0.46f));
            UnityEventTools.AddVoidPersistentListener(
                closeButton.onClick, new UnityAction(panel.Close));

            return panel;
        }

        /// <summary>
        /// 화면 가운데 뜨는 패널 배경. 스프라이트는 반드시 <b>에셋</b>이어야 한다 —
        /// 코드로 만든 Sprite 는 씬에 fileID 0 으로 저장된다 (함정 3.1).
        /// </summary>
        private static GameObject CreatePanelBackground(Transform parent, string name, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            CenterRect(go.GetComponent<RectTransform>(), Vector2.zero, size);

            var image = go.GetComponent<Image>();
            image.sprite = squareSprite;
            image.color = new Color(0.13f, 0.12f, 0.16f, 0.95f);

            return go;
        }

        private static Text CreatePanelText(
            Transform parent, string name, Vector2 anchoredPosition, Vector2 size,
            TextAnchor alignment, int fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            CenterRect(go.GetComponent<RectTransform>(), anchoredPosition, size);

            var text = go.GetComponent<Text>();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            return text;
        }

        private static Button CreatePanelButton(
            Transform parent, string name, string label,
            Vector2 anchoredPosition, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            CenterRect(go.GetComponent<RectTransform>(), anchoredPosition, size);

            var image = go.GetComponent<Image>();
            image.sprite = squareSprite;
            image.color = color;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;

            var text = CreatePanelText(go.transform, "Label", Vector2.zero, size,
                TextAnchor.MiddleCenter, 26, Color.white);
            text.text = label;

            return button;
        }

        /// <summary>부모 가운데를 기준으로 놓는다. 해상도가 바뀌어도 패널이 가운데 남는다.</summary>
        private static void CenterRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        private static Text CreateText(
            Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size,
            TextAnchor alignment, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(anchorMin.x, anchorMax.y);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var text = go.GetComponent<Text>();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.black;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            return text;
        }

    }
}
