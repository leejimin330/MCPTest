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
        private const string ScenesDir = "Assets/Scenes";
        private const string ScenePath = ScenesDir + "/FarmScene.unity";

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
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            EnsureFolders();
            EnsureSpriteAssets();

            var species = BuildSpeciesAssets();
            var items = BuildItemAssets();
            var enemies = BuildEnemyAssets();
            var speciesDb = BuildSpeciesDatabase(species);
            var itemDb = BuildItemDatabase(items);
            var enemyDb = BuildEnemyDatabase(enemies);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BuildScene(scene, speciesDb, itemDb, enemyDb, species["sprigling"], items);

            Debug.Log("[FarmCreature] Build All 완료 — 에셋과 씬을 생성했습니다: " + ScenePath);
        }

        // ---------- 폴더 ----------

        private static void EnsureFolders()
        {
            EnsureFolder(DataRoot);
            EnsureFolder(SpeciesDir);
            EnsureFolder(ItemsDir);
            EnsureFolder(EnemiesDir);
            EnsureFolder(SpritesDir);
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
        /// 전투터의 상대 3종. 수치는 초안이다 —
        /// 스프리글링(공5 방5 체20) 한 마리로는 들쥐를 간신히 잡고, 가시멧돼지부터는
        /// 파티를 늘리거나 훈련·장비가 필요하도록 잡았다.
        /// </summary>
        private static List<EnemyData> BuildEnemyAssets()
        {
            var rat = LoadOrCreate<EnemyData>(EnemiesDir + "/FieldRat.asset");
            rat.enemyId = "field_rat";
            rat.displayName = "들쥐";
            rat.maxHp = 24;
            rat.attack = 4;
            rat.defense = 1;
            rat.attackInterval = 1.8f;
            rat.goldReward = 8;
            rat.placeholderColor = new Color(0.72f, 0.66f, 0.55f);

            var boar = LoadOrCreate<EnemyData>(EnemiesDir + "/ThornBoar.asset");
            boar.enemyId = "thorn_boar";
            boar.displayName = "가시멧돼지";
            boar.maxHp = 45;
            boar.attack = 7;
            boar.defense = 3;
            boar.attackInterval = 1.5f;
            boar.goldReward = 18;
            boar.placeholderColor = new Color(0.65f, 0.4f, 0.3f);

            var golem = LoadOrCreate<EnemyData>(EnemiesDir + "/StoneGolem.asset");
            golem.enemyId = "stone_golem";
            golem.displayName = "바위골렘";
            golem.maxHp = 80;
            golem.attack = 10;
            golem.defense = 6;
            golem.attackInterval = 1.9f;
            golem.goldReward = 40;
            golem.placeholderColor = new Color(0.5f, 0.5f, 0.55f);

            EditorUtility.SetDirty(rat);
            EditorUtility.SetDirty(boar);
            EditorUtility.SetDirty(golem);

            return new List<EnemyData> { rat, boar, golem };
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
        private static void BuildScene(
            UnityEngine.SceneManagement.Scene scene,
            SpeciesDatabase speciesDb,
            ItemDatabase itemDb,
            EnemyDatabase enemyDb,
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
            var battlePanel = BuildBattlePanel(panelRoot.transform, careManager, enemyDb);

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
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
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
        /// 전투터 패널(상점 건물). 상대 버튼은 <see cref="EnemyDatabase"/> 인덱스를 넘긴다.
        /// 전투 진행은 <see cref="BattlePanel"/> 의 Update 가 굴린다.
        /// </summary>
        private static BattlePanel BuildBattlePanel(
            Transform parent, CareManager careManager, EnemyDatabase enemyDb)
        {
            var panelGo = CreatePanelBackground(parent, "BattlePanel", new Vector2(1100f, 760f));
            var panel = panelGo.AddComponent<BattlePanel>();
            panel.enemyDatabase = enemyDb;
            panel.careManager = careManager;

            panel.titleText = CreatePanelText(panelGo.transform, "Title", new Vector2(0f, 320f),
                new Vector2(1040f, 50f), TextAnchor.MiddleCenter, 34, Color.white);
            panel.titleText.text = "전투터";

            panel.rosterText = CreatePanelText(panelGo.transform, "Roster", new Vector2(-270f, 70f),
                new Vector2(480f, 420f), TextAnchor.UpperLeft, 24, Color.white);

            panel.logText = CreatePanelText(panelGo.transform, "BattleLog", new Vector2(270f, 70f),
                new Vector2(480f, 420f), TextAnchor.UpperLeft, 22, new Color(0.85f, 0.85f, 0.7f));

            int enemyCount = enemyDb != null && enemyDb.enemies != null ? enemyDb.enemies.Count : 0;

            // 0 이면 전투를 시작할 수단이 없는 전투터가 저장된다. 창고와 같은 이유로 크게 실패시킨다.
            if (enemyCount == 0)
            {
                Debug.LogError("[FarmCreature] 전투 패널: 상대가 0명이다. "
                    + "EnemyDatabase 참조가 죽었거나(NewScene 이후 로드했는지 확인) 데이터베이스가 비어 있다. "
                    + "enemyDb==null? " + (enemyDb == null));
            }

            var slotX = new[] { -350f, 0f, 350f };

            for (int i = 0; i < enemyCount && i < slotX.Length; i++)
            {
                string label = enemyDb.enemies[i] != null
                    ? enemyDb.enemies[i].displayName
                    : "상대 " + i;

                var enemyButton = CreatePanelButton(panelGo.transform, "Enemy" + i, label,
                    new Vector2(slotX[i], -205f), new Vector2(300f, 76f),
                    new Color(0.68f, 0.36f, 0.34f));
                UnityEventTools.AddIntPersistentListener(
                    enemyButton.onClick, new UnityAction<int>(panel.StartBattle), i);
            }

            var closeButton = CreatePanelButton(panelGo.transform, "Close", "닫기",
                new Vector2(0f, -305f), new Vector2(220f, 60f), new Color(0.4f, 0.4f, 0.46f));
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
