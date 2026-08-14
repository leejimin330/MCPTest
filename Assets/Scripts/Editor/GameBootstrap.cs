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
        private const string ScenesDir = "Assets/Scenes";
        private const string FarmScenePath = ScenesDir + "/FarmScene.unity";
        private const string BattleScenePath = ScenesDir + "/BattleScene.unity";

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
            var encounterDb = BuildBattleDataAssets(enemies);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BuildFarmScene(farmScene, speciesDb, itemDb, encounterDb, species["sprigling"], items);

            // ★ 두 번째 NewScene 이 위에서 얻은 참조를 '다시' 죽인다 (함정 3.9와 같은 원리).
            // 그래서 전투 씬을 채우기 전에 필요한 것을 경로에서 전부 다시 로드한다.
            // 이 재로드를 빠뜨리면 슬롯도 버튼도 없는 전투 씬이 종료 코드 0 으로 조용히 저장된다.
            var battleScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            ReloadSpriteAssets();
            var battleMap = AssetDatabase.LoadAssetAtPath<BattleMapData>(BattleDir + "/FieldMap.asset");
            var practiceEncounter = AssetDatabase.LoadAssetAtPath<BattleEncounter>(BattleDir + "/RatField.asset");
            var practiceSpecies = AssetDatabase.LoadAssetAtPath<CreatureSpeciesData>(SpeciesDir + "/Sprigling.asset");

            if (battleMap == null || practiceEncounter == null || practiceSpecies == null)
            {
                Debug.LogError("[FarmCreature] 전투 씬용 에셋 재로드 실패 — "
                    + "map==null? " + (battleMap == null)
                    + " encounter==null? " + (practiceEncounter == null)
                    + " species==null? " + (practiceSpecies == null));
            }

            BuildBattleScene(battleScene, battleMap, practiceEncounter, practiceSpecies);

            // 두 씬을 모두 등록한다. 안 하면 LoadScene 이 런타임에 실패한다 (설계 5.1).
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(FarmScenePath, true),
                new EditorBuildSettingsScene(BattleScenePath, true)
            };

            Debug.Log("[FarmCreature] Build All 완료 — 에셋과 씬 2개를 생성했습니다: "
                + FarmScenePath + " / " + BattleScenePath);
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
        private static BattleEncounterDatabase BuildBattleDataAssets(List<EnemyData> enemies)
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

            // 적 3종을 그대로 세 판으로 나눈다. 마리 수로 난이도를 벌린다 —
            // 들쥐는 떼로, 골렘은 한 마리로. 총원은 BattleField.MaxEnemies 를 넘지 않는다.
            var ratField = SeedEncounter(BattleDir + "/RatField.asset",
                "들판 — 들쥐 떼", fieldMap, enemies[0], 3);
            var boarThicket = SeedEncounter(BattleDir + "/BoarThicket.asset",
                "덤불 — 가시멧돼지", fieldMap, enemies[1], 2);
            var golemRidge = SeedEncounter(BattleDir + "/GolemRidge.asset",
                "바위 언덕 — 바위골렘", fieldMap, enemies[2], 1);

            var db = LoadOrSeed<BattleEncounterDatabase>(
                DataRoot + "/BattleEncounterDatabase.asset", out created);
            if (created)
            {
                db.encounters = new List<BattleEncounter> { ratField, boarThicket, golemRidge };
                EditorUtility.SetDirty(db);
            }

            ValidateEncounters(db);
            return db;
        }

        private static BattleEncounter SeedEncounter(
            string assetPath, string displayName, BattleMapData map, EnemyData enemy, int count)
        {
            bool created;
            var encounter = LoadOrSeed<BattleEncounter>(assetPath, out created);

            if (created)
            {
                encounter.displayName = displayName;
                encounter.map = map;
                encounter.entries = new List<EncounterEntry>
                {
                    new EncounterEntry { enemy = enemy, count = count }
                };
                EditorUtility.SetDirty(encounter);
            }

            return encounter;
        }

        /// <summary>
        /// 인카운터의 적 총원이 씬 슬롯 수를 넘으면 <b>크게 실패시킨다.</b>
        /// 조용히 잘라내면 인스펙터에 8마리 넘게 적어놓고 왜 안 나오는지 모르게 된다 (설계 4.3).
        /// </summary>
        private static void ValidateEncounters(BattleEncounterDatabase db)
        {
            if (db == null || db.Count == 0)
            {
                Debug.LogError("[FarmCreature] 인카운터가 0개다. 전투터에 버튼이 하나도 생기지 않는다. "
                    + "BattleEncounterDatabase 참조가 죽었는지(NewScene 이후 로드했는지) 확인할 것. "
                    + "db==null? " + (db == null));
                return;
            }

            for (int i = 0; i < db.Count; i++)
            {
                var encounter = db.Get(i);
                if (encounter == null)
                {
                    Debug.LogError("[FarmCreature] 인카운터 " + i + " 번이 비어 있다.");
                    continue;
                }

                if (encounter.TotalEnemyCount > BattleField.MaxEnemies)
                {
                    Debug.LogError("[FarmCreature] 인카운터 '" + encounter.displayName + "' 의 적이 "
                        + encounter.TotalEnemyCount + "마리다. 슬롯은 "
                        + BattleField.MaxEnemies + "개뿐이다. "
                        + "적을 줄이거나 BattleField.MaxEnemies 를 올리고 BuildAll 을 다시 돌릴 것.");
                }

                if (encounter.TotalEnemyCount == 0)
                {
                    Debug.LogError("[FarmCreature] 인카운터 '" + encounter.displayName
                        + "' 에 적이 없다. 눌러도 아무 일도 일어나지 않는다.");
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

        // ---------- 씬 ----------

        /// <summary>
        /// 빈 씬을 채운다. <paramref name="scene"/> 는 <see cref="BuildAll"/> 이 <b>에셋을 만지기 전에</b>
        /// 만들어 넘긴다 — 여기서 새로 열면 넘겨받은 에셋 참조가 그 자리에서 죽는다.
        /// </summary>
        private static void BuildFarmScene(
            UnityEngine.SceneManagement.Scene scene,
            SpeciesDatabase speciesDb,
            ItemDatabase itemDb,
            BattleEncounterDatabase encounterDb,
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

            // 농장 건물 3동 — 배경에 서 있고 클릭/터치에 반응한다.
            var buildings = new List<FarmBuilding>
            {
                BuildFarmBuilding(BuildingKind.Barn, "헛간", -9f, new Color(0.78f, 0.34f, 0.28f)),
                BuildFarmBuilding(BuildingKind.Hatchery, "부화소", -3f, new Color(0.85f, 0.76f, 0.45f)),
                BuildFarmBuilding(BuildingKind.Storage, "창고", 3f, new Color(0.62f, 0.52f, 0.34f)),
                BuildFarmBuilding(BuildingKind.Shop, "상점", 9f, new Color(0.36f, 0.55f, 0.72f))
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

            // 패널 — Canvas 하위에 둬야 UiFontApplier 가 한글 폰트를 꽂아준다.
            // PanelRoot 자체는 활성이고 패널만 비활성이다 (부모가 꺼져 있으면 Open 해도 안 보인다).
            var panelRoot = new GameObject("PanelRoot", typeof(RectTransform));
            panelRoot.transform.SetParent(canvasGo.transform, false);
            StretchFull(panelRoot.GetComponent<RectTransform>());

            var storagePanel = BuildStoragePanel(panelRoot.transform, careManager, itemDb);
            var raisingPanel = BuildRaisingPanel(panelRoot.transform, careManager);
            var barnPanel = BuildBarnPanel(panelRoot.transform, careManager);
            var battlePanel = BuildBattlePanel(panelRoot.transform, careManager, encounterDb);

            careManager.panels = new List<GamePanel> { storagePanel, raisingPanel, barnPanel, battlePanel };
            careManager.raisingPanel = raisingPanel;
            careManager.partyField = partyField;

            // 건물 → 패널. 부화소만 아직 패널이 없어 "준비 중" 안내가 뜬다.
            for (int i = 0; i < buildings.Count; i++)
            {
                switch (buildings[i].kind)
                {
                    case BuildingKind.Barn:
                        buildings[i].panel = barnPanel;
                        break;
                    case BuildingKind.Storage:
                        buildings[i].panel = storagePanel;
                        break;
                    case BuildingKind.Shop:
                        buildings[i].panel = battlePanel;
                        break;
                }
            }

            // 배선이 전부 끝난 뒤에 닫는다. 켜 둔 채로 저장하면 시작하자마자 패널이 떠 있다.
            storagePanel.gameObject.SetActive(false);
            raisingPanel.gameObject.SetActive(false);
            barnPanel.gameObject.SetActive(false);
            battlePanel.gameObject.SetActive(false);

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
        private static BarnPanel BuildBarnPanel(Transform parent, CareManager careManager)
        {
            var panelGo = CreatePanelBackground(parent, "BarnPanel", new Vector2(1100f, 760f));
            var panel = panelGo.AddComponent<BarnPanel>();

            panel.titleText = CreatePanelText(panelGo.transform, "Title", new Vector2(0f, 320f),
                new Vector2(1040f, 50f), TextAnchor.MiddleCenter, 34, Color.white);
            panel.titleText.text = "헛간";

            var rows = new Text[Roster.MaxMembers];
            var partyLabels = new Text[Roster.MaxMembers];
            var groups = new GameObject[Roster.MaxMembers];

            float rowY = 250f;

            for (int i = 0; i < Roster.MaxMembers; i++)
            {
                rows[i] = CreatePanelText(panelGo.transform, "Row" + i, new Vector2(-230f, rowY),
                    new Vector2(620f, 50f), TextAnchor.MiddleLeft, 24, Color.white);

                // 빈 줄의 버튼을 통째로 숨기려고 묶어 둔다.
                var group = new GameObject("RowButtons" + i, typeof(RectTransform));
                group.transform.SetParent(panelGo.transform, false);
                CenterRect(group.GetComponent<RectTransform>(), new Vector2(0f, rowY),
                    new Vector2(1040f, 56f));
                groups[i] = group;

                var partyButton = CreatePanelButton(group.transform, "Party" + i, "내보내기",
                    new Vector2(210f, 0f), new Vector2(180f, 52f), new Color(0.42f, 0.6f, 0.36f));
                UnityEventTools.AddIntPersistentListener(
                    partyButton.onClick, new UnityAction<int>(careManager.DoTogglePartyRow), i);
                partyLabels[i] = partyButton.GetComponentInChildren<Text>();

                var raiseButton = CreatePanelButton(group.transform, "Raise" + i, "육성",
                    new Vector2(410f, 0f), new Vector2(150f, 52f), new Color(0.55f, 0.45f, 0.65f));
                UnityEventTools.AddIntPersistentListener(
                    raiseButton.onClick, new UnityAction<int>(careManager.DoRaiseRow), i);

                rowY -= 62f;
            }

            panel.rowTexts = rows;
            panel.partyButtonLabels = partyLabels;
            panel.rowButtonGroups = groups;

            var closeButton = CreatePanelButton(panelGo.transform, "Close", "닫기",
                new Vector2(0f, -305f), new Vector2(220f, 60f), new Color(0.4f, 0.4f, 0.46f));
            UnityEventTools.AddVoidPersistentListener(
                closeButton.onClick, new UnityAction(panel.Close));

            return panel;
        }

        /// <summary>
        /// 전투터 패널(상점 건물에 임시로 붙어 있다 — HANDOFF 4.1).
        /// 인카운터 버튼은 <see cref="BattleEncounterDatabase"/> 인덱스를 넘기고,
        /// 전투 자체는 <see cref="BattleSceneRoot"/> 가 별도 씬에서 굴린다.
        /// </summary>
        private static BattlePanel BuildBattlePanel(
            Transform parent, CareManager careManager, BattleEncounterDatabase encounterDb)
        {
            var panelGo = CreatePanelBackground(parent, "BattlePanel", new Vector2(1100f, 620f));
            var panel = panelGo.AddComponent<BattlePanel>();
            panel.encounterDatabase = encounterDb;
            panel.careManager = careManager;

            panel.titleText = CreatePanelText(panelGo.transform, "Title", new Vector2(0f, 250f),
                new Vector2(1040f, 50f), TextAnchor.MiddleCenter, 34, Color.white);
            panel.titleText.text = "전투터";

            panel.infoText = CreatePanelText(panelGo.transform, "Info", new Vector2(0f, 175f),
                new Vector2(1040f, 44f), TextAnchor.MiddleCenter, 26, new Color(0.85f, 0.85f, 0.7f));

            int encounterCount = encounterDb != null ? encounterDb.Count : 0;

            // 0 이면 전투를 시작할 수단이 없는 전투터가 저장된다 (함정 3.9의 일반화).
            if (encounterCount == 0)
            {
                Debug.LogError("[FarmCreature] 전투 패널: 인카운터가 0개다. "
                    + "BattleEncounterDatabase 참조가 죽었거나(NewScene 이후 로드했는지 확인) 목록이 비어 있다. "
                    + "encounterDb==null? " + (encounterDb == null));
            }

            // 한 줄에 3칸, 최대 2줄. 그 이상은 자리가 없다.
            const int columns = 3;
            const int maxButtons = columns * 2;

            if (encounterCount > maxButtons)
            {
                Debug.LogError("[FarmCreature] 인카운터가 " + encounterCount + "개다. "
                    + "전투터에는 " + maxButtons + "칸뿐이라 나머지는 화면에서 고를 수 없다. "
                    + "패널 레이아웃을 늘리거나 인카운터를 줄일 것.");
            }

            for (int i = 0; i < encounterCount && i < maxButtons; i++)
            {
                var encounter = encounterDb.Get(i);

                var button = CreatePanelButton(panelGo.transform, "Encounter" + i,
                    PanelLabels.EncounterButton(encounter),
                    new Vector2(-350f + (i % columns) * 350f, 30f - (i / columns) * 120f),
                    new Vector2(320f, 100f),
                    new Color(0.68f, 0.36f, 0.34f));

                UnityEventTools.AddIntPersistentListener(
                    button.onClick, new UnityAction<int>(panel.StartEncounter), i);
            }

            var closeButton = CreatePanelButton(panelGo.transform, "Close", "닫기",
                new Vector2(0f, -240f), new Vector2(220f, 60f), new Color(0.4f, 0.4f, 0.46f));
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
