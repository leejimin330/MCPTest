# 전투 화면 구현 계획

> **에이전트 작업자에게:** 이 계획은 `superpowers:subagent-driven-development`(권장) 또는
> `superpowers:executing-plans` 로 한 태스크씩 실행한다. 단계는 체크박스(`- [ ]`)로 추적한다.

**목표:** 텍스트 두 덩어리뿐인 전투터를, 유닛이 서 있고 체력바가 줄어드는 **별도 전투 씬**으로 바꾼다.

**아키텍처:** `BattleSimulator`(순수 C#, 난수 없음)는 **한 줄도 바꾸지 않는다.** 좌표는 표시 전용이다.
농장 씬은 인카운터를 고르고 `BattleHandoff`(정적 전달자)에 `GameState` 를 실어 전투 씬을 연다.
전투 씬은 상태를 **읽기만** 하고 승패·보상액만 실어 돌려보내며, 재화는 농장 쪽에서 한 번만 지급한다.

**기술 스택:** Unity 6000.2.8f1 / C# / uGUI(`UnityEngine.UI.Text`·`Button`) / NUnit EditMode 테스트 /
씬·에셋은 `GameBootstrap.BuildAll` 이 전량 생성

**설계 문서:** `docs/superpowers/specs/2026-08-14-battle-screen-design.md` — 이 계획은 그 문서를 근거로 삼는다. 실행자는 둘 다 읽는다.

**인수인계 문서:** `docs/HANDOFF.md` — 3절의 함정 목록은 이 계획 전체에 적용된다.

---

## 전역 제약

이 절의 규칙은 **모든 태스크의 요구사항에 암묵적으로 포함된다.**

1. **`BattleSimulator.cs` 와 `BattleUnit.cs` 를 수정하지 않는다.** `BattleOutcome` 열거형에 값을
   추가하는 것도 수정이다. 전투 씬이 필요로 하는 "중도 이탈" 은 별도 열거형 `BattleConclusion` 으로 둔다.
   근거: 설계 1.1 — 이 두 파일이 고정되어야 EditMode 테스트 13개가 그대로 산다.
2. **런타임에 GameObject·Sprite·Font 를 생성하지 않는다** (HANDOFF 함정 3.1).
   유닛 슬롯·체력바·이름표는 전부 씬 생성 시점에 만들어 두고 켜고 끈다.
   스프라이트는 반드시 `Assets/GameData/Sprites/Square.png` · `Circle.png` **에셋**을 참조한다.
3. **`EditorSceneManager.NewScene` 을 부른 뒤에는 그 이전에 얻은 에셋 참조가 전부 죽는다** (함정 3.9).
   씬을 새로 열었으면 필요한 에셋을 **경로에서 다시 로드**한다.
4. **uGUI persistent listener 는 무인자와 int 인자만 직렬화된다** (함정 3.6).
   버튼은 `UnityEventTools.AddVoidPersistentListener` 또는 `AddIntPersistentListener` 로만 건다.
5. **개수가 데이터에서 나오는 UI 는 개수가 0 이면 `Debug.LogError` 로 크게 실패시킨다.**
   루프가 0회 돌아도 예외는 나지 않는다 (함정 3.9의 일반화).
6. **패널·슬롯은 저장 시점에 꺼져 있어야 한다** (함정 3.8).
7. **uGUI `Text` 는 반드시 `Canvas` 하위**에 둔다 — `UiFontApplier` 가 Awake 에서 한글 폰트를 꽂는다 (함정 3.7).
8. **배치모드 명령은 Unity 에디터를 닫고 실행한다.** 종료 코드 0 을 믿지 말고 생성물을 직접 검사한다 (함정 3.5).
9. 네임스페이스: 데이터 `FarmCreature.Data` / 코어 `FarmCreature.Core` / 표시 `FarmCreature.Presentation` /
   에디터 `FarmCreature.EditorTools` / 테스트 `FarmCreature.Tests`.
10. 주석과 UI 문자열은 **한국어**로 쓴다. 기존 코드 스타일(왜 그렇게 했는지를 적는 주석)을 따른다.

### 자주 쓰는 명령

에디터: `C:\Program Files\Unity\Hub\Editor\6000.2.8f1\Editor\Unity.exe`
로그 경로의 `<tmp>` 는 스크래치패드 디렉터리를 쓴다.

```bash
# 테스트
Unity.exe -batchmode -runTests -projectPath "C:\Users\이지민\UnityProjects\MCPTest" \
  -testPlatform EditMode -testResults "<tmp>\results.xml" -logFile "<tmp>\test.log"

# 에셋 + 씬 전량 재생성
Unity.exe -batchmode -quit -projectPath "C:\Users\이지민\UnityProjects\MCPTest" \
  -executeMethod FarmCreature.EditorTools.GameBootstrap.BuildAll -logFile "<tmp>\rebuild.log"

# 씬 무결성 검사
Unity.exe -batchmode -projectPath "C:\Users\이지민\UnityProjects\MCPTest" \
  -executeMethod FarmCreature.EditorTools.SceneIntegrityCheck.Run -logFile "<tmp>\integrity.log"
```

### 테스트 개수 기준선

시작 시점 **122개 전부 통과**. 이 계획이 끝나면 **145개**가 된다.

| 변화 | 개수 |
|---|---|
| 기존 | 122 |
| `PanelLabelsTests` — `BattleRoster` 삭제(−1), 새 라벨(+4) | +3 |
| `BattleDataTests` 신규 | +8 |
| `SpawnPointPickerTests` 신규 | +6 |
| `BattleHandoffTests` 신규 | +6 |
| **합계** | **145** |

숫자가 안 맞으면 **다음 태스크로 넘어가기 전에** 원인을 찾는다. 조용히 사라진 테스트가 제일 위험하다.

---

## 파일 구조

### 새로 만드는 파일

| 파일 | 책임 |
|---|---|
| `Assets/Scripts/Runtime/Data/BattleMapData.cs` | 탑뷰 맵 — 바닥 크기·색, 양측 스폰 영역, 최소 간격 |
| `Assets/Scripts/Runtime/Data/BattleEncounter.cs` | 인카운터 — 표시 이름, 등장 적 목록(수 포함), 쓸 맵 |
| `Assets/Scripts/Runtime/Data/BattleEncounterDatabase.cs` | 전투터 버튼 개수의 출처 |
| `Assets/Scripts/Runtime/Core/SpawnPointPicker.cs` | 영역 안에서 겹치지 않는 좌표 고르기 (순수 함수) |
| `Assets/Scripts/Runtime/Core/BattleHandoff.cs` | 씬 사이 상태 전달자 + `BattleConclusion` |
| `Assets/Scripts/Runtime/Presentation/BattleField.cs` | 전투 씬의 좌표 규약 — `FarmArea` 의 전투판 |
| `Assets/Scripts/Runtime/Presentation/BattleUnitView.cs` | 유닛 하나의 표시 (몸통·체력바·이름표) |
| `Assets/Scripts/Runtime/Presentation/BattleSceneRoot.cs` | 전투 씬 구동 — 시뮬레이터·배속·나가기·결과 전달 |
| `Assets/Scripts/Tests/EditMode/BattleDataTests.cs` | 맵 영역 변환, 인카운터 총원·적 목록 전개 |
| `Assets/Scripts/Tests/EditMode/SpawnPointPickerTests.cs` | 영역 안·최소 간격·재현성·종료 |
| `Assets/Scripts/Tests/EditMode/BattleHandoffTests.cs` | 왕복, 꺼내면 비워짐 |

### 고치는 파일

| 파일 | 무엇을 |
|---|---|
| `Runtime/Presentation/BattlePanel.cs` | **전량 재작성** — 전투를 굴리는 패널에서 인카운터를 고르는 패널로 |
| `Runtime/Presentation/GameRoot.cs` | 전투에서 돌아왔을 때 상태를 이어받고 보상을 지급 |
| `Runtime/Presentation/PanelLabels.cs` | `BattleRoster` 삭제, 전투터용 라벨 3종 추가 |
| `Editor/GameBootstrap.cs` | `LoadOrSeed`, 전투 데이터 seed, `BuildBattleScene`, 두 씬 등록 |
| `Editor/SceneIntegrityCheck.cs` | 두 씬을 각각 열어 검사 |
| `Tests/EditMode/TestFixtures.cs` | 맵·인카운터 픽스처 추가 |
| `Tests/EditMode/PanelLabelsTests.cs` | `BattleRoster` 테스트 삭제, 새 라벨 테스트 추가 |
| `docs/HANDOFF.md` | 마지막 태스크에서 갱신 |

### 손대지 않는 파일

`BattleSimulator.cs` · `BattleUnit.cs` · `Roster.cs` · `GameState.cs` · `SaveSerializer.cs` ·
`CareActions.cs` · `EvolutionEvaluator.cs` · `CreatureController.cs` · `PartyField.cs` · `FarmArea.cs`

---

## Task 1: 전투 데이터 3종

**Files:**
- Create: `Assets/Scripts/Runtime/Data/BattleMapData.cs`
- Create: `Assets/Scripts/Runtime/Data/BattleEncounter.cs`
- Create: `Assets/Scripts/Runtime/Data/BattleEncounterDatabase.cs`
- Modify: `Assets/Scripts/Tests/EditMode/TestFixtures.cs` (끝에 추가)
- Test: `Assets/Scripts/Tests/EditMode/BattleDataTests.cs`

**Interfaces:**
- Consumes: `EnemyData`(기존, `Assets/Scripts/Runtime/Data/EnemyData.cs` — `enemyId`, `displayName`,
  `maxHp`, `attack`, `defense`, `attackInterval`, `goldReward`, `placeholderColor`)
- Produces:
  - `BattleMapData` : `ScriptableObject` — `string displayName`, `Vector2 groundSize`, `Color groundColor`,
    `Vector2 allySpawnCenter/allySpawnSize`, `Vector2 enemySpawnCenter/enemySpawnSize`, `float minSpacing`,
    `Rect AllySpawnArea { get; }`, `Rect EnemySpawnArea { get; }`
  - `EncounterEntry` : `[Serializable] class` — `EnemyData enemy`, `int count`
  - `BattleEncounter` : `ScriptableObject` — `string displayName`, `List<EncounterEntry> entries`,
    `BattleMapData map`, `int TotalEnemyCount { get; }`, `int TotalGoldReward { get; }`,
    `List<EnemyData> BuildEnemyList()`
  - `BattleEncounterDatabase` : `ScriptableObject` — `List<BattleEncounter> encounters`,
    `BattleEncounter Get(int index)`
  - `TestFixtures.MakeBattleMap(...)`, `TestFixtures.MakeEncounter(...)`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Scripts/Tests/EditMode/BattleDataTests.cs`:

```csharp
using FarmCreature.Data;
using NUnit.Framework;
using UnityEngine;

namespace FarmCreature.Tests
{
    /// <summary>
    /// 전투 데이터의 계산 규칙. 좌표는 표시 전용이지만 <b>영역 계산이 틀리면 유닛이 맵 밖에 선다</b>.
    /// 인카운터의 총원은 씬 슬롯 개수(BattleField.MaxEnemies)와 대조되므로 정확해야 한다.
    /// </summary>
    public class BattleDataTests
    {
        // ---- 맵 영역 ----

        [Test]
        public void AllySpawnArea_IsCenteredOnTheGivenCenter()
        {
            var map = TestFixtures.MakeBattleMap(
                allyCenter: new Vector2(-4f, 1f), allySize: new Vector2(6f, 4f));

            var area = map.AllySpawnArea;

            Assert.AreEqual(-7f, area.xMin, 0.001f);
            Assert.AreEqual(-1f, area.xMax, 0.001f);
            Assert.AreEqual(-1f, area.yMin, 0.001f);
            Assert.AreEqual(3f, area.yMax, 0.001f);
        }

        [Test]
        public void EnemySpawnArea_IsCenteredOnTheGivenCenter()
        {
            var map = TestFixtures.MakeBattleMap(
                enemyCenter: new Vector2(5f, 0f), enemySize: new Vector2(4f, 8f));

            var area = map.EnemySpawnArea;

            Assert.AreEqual(3f, area.xMin, 0.001f);
            Assert.AreEqual(7f, area.xMax, 0.001f);
            Assert.AreEqual(-4f, area.yMin, 0.001f);
            Assert.AreEqual(4f, area.yMax, 0.001f);
        }

        // ---- 인카운터 총원 ----

        [Test]
        public void TotalEnemyCount_SumsTheEntryCounts()
        {
            var rat = TestFixtures.MakeEnemy("rat", maxHp: 10, attack: 1, defense: 0);
            var boar = TestFixtures.MakeEnemy("boar", maxHp: 20, attack: 2, defense: 1);

            var encounter = TestFixtures.MakeEncounter("들판", (rat, 3), (boar, 2));

            Assert.AreEqual(5, encounter.TotalEnemyCount);
        }

        [Test]
        public void TotalEnemyCount_IsZeroForAnEmptyEncounter()
        {
            var encounter = TestFixtures.MakeEncounter("빈 들판");

            Assert.AreEqual(0, encounter.TotalEnemyCount);
        }

        [Test]
        public void TotalEnemyCount_IgnoresBlankRows()
        {
            // 인스펙터에서 목록을 늘리면 빈 칸이 생긴다. 그것 때문에 적이 늘어나면 안 된다.
            var rat = TestFixtures.MakeEnemy("rat", maxHp: 10, attack: 1, defense: 0);
            var encounter = TestFixtures.MakeEncounter("들판", (rat, 2), (null, 5), (rat, 0), (rat, -3));

            Assert.AreEqual(2, encounter.TotalEnemyCount);
        }

        [Test]
        public void BuildEnemyList_ExpandsCountsInListOrder()
        {
            var rat = TestFixtures.MakeEnemy("rat", maxHp: 10, attack: 1, defense: 0);
            var boar = TestFixtures.MakeEnemy("boar", maxHp: 20, attack: 2, defense: 1);

            var foes = TestFixtures.MakeEncounter("들판", (rat, 2), (boar, 1)).BuildEnemyList();

            Assert.AreEqual(3, foes.Count);
            Assert.AreSame(rat, foes[0]);
            Assert.AreSame(rat, foes[1]);
            Assert.AreSame(boar, foes[2]);
        }

        [Test]
        public void BuildEnemyList_MatchesTotalEnemyCount()
        {
            var rat = TestFixtures.MakeEnemy("rat", maxHp: 10, attack: 1, defense: 0);
            var encounter = TestFixtures.MakeEncounter("들판", (rat, 4), (null, 2));

            Assert.AreEqual(encounter.TotalEnemyCount, encounter.BuildEnemyList().Count,
                "두 값이 어긋나면 슬롯 개수 검사가 통과해도 화면에 안 나오는 적이 생긴다");
        }

        [Test]
        public void TotalGoldReward_SumsPerEnemyRewards()
        {
            var rat = TestFixtures.MakeEnemy("rat", maxHp: 10, attack: 1, defense: 0, goldReward: 8);
            var boar = TestFixtures.MakeEnemy("boar", maxHp: 20, attack: 2, defense: 1, goldReward: 18);

            var encounter = TestFixtures.MakeEncounter("들판", (rat, 3), (boar, 1));

            Assert.AreEqual(8 * 3 + 18, encounter.TotalGoldReward);
        }
    }
}
```

`Assets/Scripts/Tests/EditMode/TestFixtures.cs` 의 마지막 메서드(`MakeItemDatabase`) 뒤에 추가:

```csharp
        /// <summary>전투 맵 픽스처. 기본값은 테스트가 신경 쓰지 않는 값이다.</summary>
        public static BattleMapData MakeBattleMap(
            Vector2? allyCenter = null,
            Vector2? allySize = null,
            Vector2? enemyCenter = null,
            Vector2? enemySize = null,
            float minSpacing = 1.2f)
        {
            var so = ScriptableObject.CreateInstance<BattleMapData>();
            so.displayName = "테스트 맵";
            so.groundSize = new Vector2(16f, 9f);
            so.allySpawnCenter = allyCenter ?? new Vector2(-4f, 0f);
            so.allySpawnSize = allySize ?? new Vector2(5f, 6f);
            so.enemySpawnCenter = enemyCenter ?? new Vector2(4f, 0f);
            so.enemySpawnSize = enemySize ?? new Vector2(5f, 6f);
            so.minSpacing = minSpacing;
            return so;
        }

        /// <summary>인카운터 픽스처. (적, 마리수) 쌍을 목록 순서대로 넣는다.</summary>
        public static BattleEncounter MakeEncounter(
            string displayName, params (EnemyData enemy, int count)[] entries)
        {
            var so = ScriptableObject.CreateInstance<BattleEncounter>();
            so.displayName = displayName;
            so.map = MakeBattleMap();
            so.entries = new List<EncounterEntry>();

            for (int i = 0; i < entries.Length; i++)
            {
                so.entries.Add(new EncounterEntry
                {
                    enemy = entries[i].enemy,
                    count = entries[i].count
                });
            }

            return so;
        }

        public static BattleEncounterDatabase MakeEncounterDatabase(params BattleEncounter[] entries)
        {
            var so = ScriptableObject.CreateInstance<BattleEncounterDatabase>();
            so.encounters = new List<BattleEncounter>(entries);
            return so;
        }
```

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

에디터를 닫고:

```bash
Unity.exe -batchmode -runTests -projectPath "C:\Users\이지민\UnityProjects\MCPTest" \
  -testPlatform EditMode -testResults "<tmp>\results.xml" -logFile "<tmp>\test.log"
```

기대: **컴파일 에러**. `<tmp>\test.log` 에 `BattleMapData` / `BattleEncounter` /
`BattleEncounterDatabase` / `EncounterEntry` 를 찾을 수 없다는 CS0246 이 나온다.
컴파일 에러가 아니라 통과가 나오면 파일을 잘못 저장한 것이다.

- [ ] **Step 3: `BattleMapData` 를 만든다**

`Assets/Scripts/Runtime/Data/BattleMapData.cs`:

```csharp
using UnityEngine;

namespace FarmCreature.Data
{
    /// <summary>
    /// 전투가 벌어지는 탑뷰 맵. 여기 있는 좌표는 <b>표시 전용</b>이다 —
    /// <see cref="FarmCreature.Core.BattleSimulator"/> 는 이 값을 한 번도 읽지 않는다 (설계 1.1).
    /// 그래서 스폰 좌표가 랜덤이어도 전투 결과는 여전히 결정적이다.
    ///
    /// 영역을 <see cref="Rect"/> 로 직접 두지 않고 중심+크기로 나눈 이유:
    /// 인스펙터에서 <c>Rect</c> 는 좌하단 모서리를 x·y 로 보여줘서, 맵 가운데를 기준으로
    /// 영역을 잡을 때 매번 암산을 해야 한다.
    /// </summary>
    [CreateAssetMenu(fileName = "BattleMap", menuName = "FarmCreature/Battle Map")]
    public class BattleMapData : ScriptableObject
    {
        public string displayName = "들판";

        [Header("바닥")]
        [Tooltip("탑뷰 바닥의 가로·세로 (월드 유닛).")]
        public Vector2 groundSize = new Vector2(17f, 9f);

        public Color groundColor = new Color(0.45f, 0.62f, 0.35f);

        [Header("아군 스폰 영역 (중심 + 크기)")]
        public Vector2 allySpawnCenter = new Vector2(-4.5f, -0.5f);
        public Vector2 allySpawnSize = new Vector2(5f, 5.5f);

        [Header("적 스폰 영역 (중심 + 크기)")]
        public Vector2 enemySpawnCenter = new Vector2(4.5f, -0.5f);
        public Vector2 enemySpawnSize = new Vector2(5f, 5.5f);

        [Tooltip("유닛끼리 최소 간격. 겹쳐 서는 것을 막는다.")]
        public float minSpacing = 1.4f;

        public Rect AllySpawnArea
        {
            get { return MakeRect(allySpawnCenter, allySpawnSize); }
        }

        public Rect EnemySpawnArea
        {
            get { return MakeRect(enemySpawnCenter, enemySpawnSize); }
        }

        private static Rect MakeRect(Vector2 center, Vector2 size)
        {
            return new Rect(
                center.x - size.x * 0.5f,
                center.y - size.y * 0.5f,
                size.x,
                size.y);
        }
    }
}
```

- [ ] **Step 4: `BattleEncounter` 를 만든다**

`Assets/Scripts/Runtime/Data/BattleEncounter.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace FarmCreature.Data
{
    /// <summary>인카운터 한 줄 — 어떤 적이 몇 마리 나오는가.</summary>
    [System.Serializable]
    public class EncounterEntry
    {
        public EnemyData enemy;

        [Min(0)]
        public int count = 1;
    }

    /// <summary>
    /// 전투터 버튼 하나에 대응하는 한 판. <b>인스펙터에서만 만든다</b> —
    /// 적 파티 편성 UI 는 이번 범위 밖이다 (설계 7절).
    ///
    /// 적 총원은 씬에 미리 만들어 둔 슬롯 수
    /// (<see cref="FarmCreature.Presentation.BattleField.MaxEnemies"/>)를 넘을 수 없다.
    /// 넘으면 <c>BuildAll</c> 이 실패시킨다 — 조용히 잘라내면 인스펙터에 적어놓은 적이
    /// 왜 안 나오는지 알 수 없게 된다 (설계 4.3).
    /// </summary>
    [CreateAssetMenu(fileName = "Encounter", menuName = "FarmCreature/Battle Encounter")]
    public class BattleEncounter : ScriptableObject
    {
        public string displayName = "들판";

        [Tooltip("등장하는 적과 그 수. 위에서부터 순서대로 나온다.")]
        public List<EncounterEntry> entries = new List<EncounterEntry>();

        [Tooltip("이 전투에 쓸 맵. 비어 있으면 전투 씬의 기본 맵을 쓴다.")]
        public BattleMapData map;

        /// <summary>등장 적 총원. 빈 칸과 0 이하의 수는 세지 않는다.</summary>
        public int TotalEnemyCount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < CountOfEntries; i++)
                {
                    total += ValidCountAt(i);
                }

                return total;
            }
        }

        /// <summary>전부 쓰러뜨렸을 때 받는 재화. 전투터 버튼에 미리 보여준다.</summary>
        public int TotalGoldReward
        {
            get
            {
                int total = 0;
                for (int i = 0; i < CountOfEntries; i++)
                {
                    total += ValidCountAt(i) * entries[i].enemy.goldReward;
                }

                return total;
            }
        }

        /// <summary>
        /// <see cref="entries"/> 를 한 마리씩 펼친다. 순서는 목록 순서 그대로다 —
        /// <see cref="FarmCreature.Core.BattleSimulator"/> 가 앞쪽부터 때리므로 순서가 곧 전열이다.
        /// </summary>
        public List<EnemyData> BuildEnemyList()
        {
            var foes = new List<EnemyData>();

            for (int i = 0; i < CountOfEntries; i++)
            {
                int count = ValidCountAt(i);
                for (int n = 0; n < count; n++)
                {
                    foes.Add(entries[i].enemy);
                }
            }

            return foes;
        }

        private int CountOfEntries
        {
            get { return entries != null ? entries.Count : 0; }
        }

        /// <summary>빈 칸·음수를 걸러 실제로 나올 마리 수를 돌려준다.</summary>
        private int ValidCountAt(int index)
        {
            var entry = entries[index];
            if (entry == null || entry.enemy == null || entry.count <= 0)
            {
                return 0;
            }

            return entry.count;
        }
    }
}
```

- [ ] **Step 5: `BattleEncounterDatabase` 를 만든다**

`Assets/Scripts/Runtime/Data/BattleEncounterDatabase.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace FarmCreature.Data
{
    /// <summary>
    /// 전투터에 뜨는 인카운터 목록. <b>버튼 개수가 여기서 나온다</b> —
    /// 그래서 씬 무결성 검사가 버튼 수를 이 목록 크기와 대조한다.
    /// 버튼이 인덱스를 들고 있으므로 <b>순서를 바꾸지 않는다</b> (함정 3.6과 같은 이유).
    /// </summary>
    [CreateAssetMenu(fileName = "BattleEncounterDatabase", menuName = "FarmCreature/Battle Encounter Database")]
    public class BattleEncounterDatabase : ScriptableObject
    {
        public List<BattleEncounter> encounters = new List<BattleEncounter>();

        public int Count
        {
            get { return encounters != null ? encounters.Count : 0; }
        }

        public BattleEncounter Get(int index)
        {
            return index >= 0 && index < Count ? encounters[index] : null;
        }
    }
}
```

- [ ] **Step 6: 테스트를 다시 돌려 통과를 확인한다**

Step 2 와 같은 명령. 기대: 종료 코드 0, `<tmp>\results.xml` 의
`<test-run total="130" passed="130" failed="0"`.

`total` 이 130 이 아니면 멈추고 원인을 찾는다 (122 + 8).

- [ ] **Step 7: 커밋**

```bash
git add Assets/Scripts/Runtime/Data Assets/Scripts/Tests/EditMode
git commit -m "feat: 전투 맵·인카운터·인카운터DB 데이터 3종 추가"
```

---

## Task 2: 스폰 좌표 고르기 (순수 함수)

**Files:**
- Create: `Assets/Scripts/Runtime/Core/SpawnPointPicker.cs`
- Test: `Assets/Scripts/Tests/EditMode/SpawnPointPickerTests.cs`

**Interfaces:**
- Consumes: 없음 (`UnityEngine.Rect` · `Vector2` · `Mathf` 만 쓴다)
- Produces:
  `public static List<Vector2> SpawnPointPicker.PickSpawnPoints(Rect area, int count, float minSpacing, int seed)`

**설계 근거:** 4.3 — 순수 함수로 분리해 EditMode 로 검증한다. `seed` 를 인자로 받는 이유는
테스트에서 재현하기 위함이고, 게임에서는 매번 다른 값을 넘긴다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Scripts/Tests/EditMode/SpawnPointPickerTests.cs`:

```csharp
using FarmCreature.Core;
using NUnit.Framework;
using UnityEngine;

namespace FarmCreature.Tests
{
    /// <summary>
    /// 스폰 좌표는 랜덤이지만 <b>전투 결과는 여전히 결정적이다</b> — 규칙이 좌표를 읽지 않기 때문이다.
    /// 여기서 못박는 것은 "맵 밖에 서지 않는다 / 겹쳐 서지 않는다 / 무한 루프에 빠지지 않는다" 셋이다.
    /// </summary>
    public class SpawnPointPickerTests
    {
        private static readonly Rect Roomy = new Rect(-5f, -3f, 10f, 6f);

        [Test]
        public void PickSpawnPoints_ReturnsTheRequestedCount()
        {
            var points = SpawnPointPicker.PickSpawnPoints(Roomy, 8, minSpacing: 1f, seed: 1);

            Assert.AreEqual(8, points.Count);
        }

        [Test]
        public void PickSpawnPoints_StaysInsideTheArea()
        {
            var points = SpawnPointPicker.PickSpawnPoints(Roomy, 8, minSpacing: 1f, seed: 7);

            for (int i = 0; i < points.Count; i++)
            {
                Assert.GreaterOrEqual(points[i].x, Roomy.xMin, "좌표 " + i + " 가 왼쪽으로 벗어났다");
                Assert.LessOrEqual(points[i].x, Roomy.xMax, "좌표 " + i + " 가 오른쪽으로 벗어났다");
                Assert.GreaterOrEqual(points[i].y, Roomy.yMin, "좌표 " + i + " 가 아래로 벗어났다");
                Assert.LessOrEqual(points[i].y, Roomy.yMax, "좌표 " + i + " 가 위로 벗어났다");
            }
        }

        [Test]
        public void PickSpawnPoints_KeepsUnitsApart()
        {
            const float spacing = 1.2f;
            var points = SpawnPointPicker.PickSpawnPoints(Roomy, 6, spacing, seed: 3);

            for (int i = 0; i < points.Count; i++)
            {
                for (int j = i + 1; j < points.Count; j++)
                {
                    float distance = Vector2.Distance(points[i], points[j]);
                    Assert.GreaterOrEqual(distance, spacing - 0.001f,
                        "유닛 " + i + " 와 " + j + " 가 겹쳐 선다 (거리 " + distance + ")");
                }
            }
        }

        [Test]
        public void PickSpawnPoints_IsReproducibleForTheSameSeed()
        {
            var first = SpawnPointPicker.PickSpawnPoints(Roomy, 5, 1f, seed: 42);
            var second = SpawnPointPicker.PickSpawnPoints(Roomy, 5, 1f, seed: 42);

            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void PickSpawnPoints_DiffersForADifferentSeed()
        {
            var first = SpawnPointPicker.PickSpawnPoints(Roomy, 5, 1f, seed: 1);
            var second = SpawnPointPicker.PickSpawnPoints(Roomy, 5, 1f, seed: 2);

            CollectionAssert.AreNotEqual(first, second,
                "seed 가 달라도 같은 배치가 나오면 매 전투가 똑같아 보인다");
        }

        [Test]
        public void PickSpawnPoints_TerminatesWhenTheAreaIsTooCrowded()
        {
            // 간격 5 를 12마리에게 주려면 이 영역으로는 절대 불가능하다.
            // 물러날 곳이 없다고 무한 루프에 빠지거나 개수를 줄이면 안 된다.
            var points = SpawnPointPicker.PickSpawnPoints(new Rect(0f, 0f, 4f, 4f), 12, 5f, seed: 9);

            Assert.AreEqual(12, points.Count);

            for (int i = 0; i < points.Count; i++)
            {
                Assert.IsTrue(new Rect(0f, 0f, 4f, 4f).Contains(points[i]) || OnEdge(points[i]),
                    "격자로 물러났더라도 영역 안이어야 한다: " + points[i]);
            }
        }

        [Test]
        public void PickSpawnPoints_ReturnsEmptyForZeroCount()
        {
            Assert.AreEqual(0, SpawnPointPicker.PickSpawnPoints(Roomy, 0, 1f, seed: 1).Count);
            Assert.AreEqual(0, SpawnPointPicker.PickSpawnPoints(Roomy, -3, 1f, seed: 1).Count);
        }

        /// <summary>Rect.Contains 는 오른쪽·위쪽 경계를 포함하지 않아 경계 좌표를 따로 봐준다.</summary>
        private static bool OnEdge(Vector2 point)
        {
            return point.x >= 0f && point.x <= 4f && point.y >= 0f && point.y <= 4f;
        }
    }
}
```

> 테스트가 7개다. 기준선 표의 `+6` 은 어림수였다 — **최종 개수는 146 이 된다.**
> Task 6 이후의 기대 개수를 이 값으로 읽는다.

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```bash
Unity.exe -batchmode -runTests -projectPath "C:\Users\이지민\UnityProjects\MCPTest" \
  -testPlatform EditMode -testResults "<tmp>\results.xml" -logFile "<tmp>\test.log"
```

기대: 컴파일 에러 — `SpawnPointPicker` 를 찾을 수 없음(CS0103/CS0246).

- [ ] **Step 3: 구현한다**

`Assets/Scripts/Runtime/Core/SpawnPointPicker.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace FarmCreature.Core
{
    /// <summary>
    /// 전투 시작 시 유닛을 세울 좌표를 고른다. <b>표시 전용</b>이라 전투 규칙과 무관하다 (설계 1.1).
    ///
    /// <see cref="UnityEngine.Random"/> 대신 <see cref="System.Random"/> 을 쓰는 이유:
    /// 전역 난수 상태를 건드리지 않아야 같은 seed 로 항상 같은 배치가 나오고, 그래야 테스트가 선다.
    /// </summary>
    public static class SpawnPointPicker
    {
        /// <summary>한 좌표를 고르는 데 허용하는 시도 횟수. 넘으면 격자로 물러난다.</summary>
        private const int MaxAttemptsPerPoint = 24;

        /// <summary>
        /// <paramref name="area"/> 안에서 서로 <paramref name="minSpacing"/> 이상 떨어진 좌표를
        /// <paramref name="count"/> 개 고른다.
        ///
        /// 자리를 못 찾으면 <b>전량 격자로 물러난다.</b> 랜덤 좌표와 격자 좌표를 섞으면
        /// 간격 보장이 깨지므로, 한 번이라도 실패하면 처음부터 격자로 다시 깐다.
        /// 어느 쪽이든 좌표는 항상 영역 안이고 개수는 항상 <paramref name="count"/> 다.
        /// </summary>
        public static List<Vector2> PickSpawnPoints(Rect area, int count, float minSpacing, int seed)
        {
            var points = new List<Vector2>();
            if (count <= 0)
            {
                return points;
            }

            var random = new System.Random(seed);
            float sqrSpacing = minSpacing * minSpacing;

            for (int i = 0; i < count; i++)
            {
                Vector2 picked;
                if (!TryPick(area, points, sqrSpacing, random, out picked))
                {
                    return GridPoints(area, count);
                }

                points.Add(picked);
            }

            return points;
        }

        private static bool TryPick(
            Rect area, List<Vector2> taken, float sqrSpacing, System.Random random, out Vector2 picked)
        {
            for (int attempt = 0; attempt < MaxAttemptsPerPoint; attempt++)
            {
                var candidate = new Vector2(
                    area.xMin + (float)random.NextDouble() * area.width,
                    area.yMin + (float)random.NextDouble() * area.height);

                if (IsFarEnough(taken, candidate, sqrSpacing))
                {
                    picked = candidate;
                    return true;
                }
            }

            picked = Vector2.zero;
            return false;
        }

        private static bool IsFarEnough(List<Vector2> taken, Vector2 candidate, float sqrSpacing)
        {
            for (int i = 0; i < taken.Count; i++)
            {
                if ((taken[i] - candidate).sqrMagnitude < sqrSpacing)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>영역을 격자로 나눠 칸 가운데에 세운다. 좁아서 간격을 못 지킬 때의 최후 수단이다.</summary>
        private static List<Vector2> GridPoints(Rect area, int count)
        {
            var points = new List<Vector2>(count);

            int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
            if (columns < 1)
            {
                columns = 1;
            }

            int rows = Mathf.CeilToInt((float)count / columns);

            for (int i = 0; i < count; i++)
            {
                int column = i % columns;
                int row = i / columns;

                points.Add(new Vector2(
                    area.xMin + area.width * (column + 0.5f) / columns,
                    area.yMin + area.height * (row + 0.5f) / rows));
            }

            return points;
        }
    }
}
```

- [ ] **Step 4: 테스트를 다시 돌린다**

기대: 종료 코드 0, `total="137" passed="137" failed="0"` (130 + 7).

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/Runtime/Core/SpawnPointPicker.cs Assets/Scripts/Tests/EditMode/SpawnPointPickerTests.cs
git commit -m "feat: 겹치지 않는 스폰 좌표를 고르는 순수 함수 추가"
```

---

## Task 3: 씬 사이 상태 전달자

**Files:**
- Create: `Assets/Scripts/Runtime/Core/BattleHandoff.cs`
- Test: `Assets/Scripts/Tests/EditMode/BattleHandoffTests.cs`

**Interfaces:**
- Consumes: `GameState`(기존, `Runtime/Core/GameState.cs`), `BattleEncounter`(Task 1)
- Produces:
  - `enum BattleConclusion { Victory, Defeat, Aborted }`
  - `class BattleRequest` — `GameState State`, `BattleEncounter Encounter`
  - `class BattleResult` — `BattleConclusion Conclusion`, `int GoldReward`, `GameState State`
  - `static class BattleHandoff` — `HasRequest`, `HasResult`, `SetRequest(GameState, BattleEncounter)`,
    `TakeRequest()`, `SetResult(BattleConclusion, int, GameState)`, `TakeResult()`, `Clear()`

**왜 `GameState` 를 결과에도 실어 보내는가 (설계 3.2 를 넘어서는 결정):**
농장 씬이 다시 로드되면 `GameRoot.Start` 가 처음부터 다시 돈다. 여기서 세이브를 읽으면
**저장 버튼을 누르지 않은 진행이 전부 날아간다** (먹이·훈련·장착은 자동 저장되지 않는다).
그래서 전투에 들려보낸 상태 객체를 그대로 돌려받아 이어 쓴다. 전투 씬은 상태를 **읽기만** 하므로
돌아온 객체는 떠날 때와 같다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Scripts/Tests/EditMode/BattleHandoffTests.cs`:

```csharp
using FarmCreature.Core;
using NUnit.Framework;

namespace FarmCreature.Tests
{
    /// <summary>
    /// 씬을 넘나드는 정적 전달자. <b>꺼내면 비워져야 한다</b> —
    /// 남아 있으면 다음에 전투터를 열었을 때 지난 전투의 유령이 되살아난다.
    ///
    /// 정적 상태는 테스트 사이에 살아남으므로 SetUp/TearDown 에서 반드시 비운다.
    /// </summary>
    public class BattleHandoffTests
    {
        [SetUp]
        public void SetUp()
        {
            BattleHandoff.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            BattleHandoff.Clear();
        }

        private static GameState MakeState()
        {
            var species = TestFixtures.MakeSpecies("sprigling", "스프리글링");
            var roster = new Roster();
            roster.Add(CreatureInstance.CreateNew(species));
            roster.JoinParty(roster.GetMember(0));
            return new GameState(roster, new Inventory(), 30);
        }

        [Test]
        public void Request_RoundTripsStateAndEncounter()
        {
            var state = MakeState();
            var encounter = TestFixtures.MakeEncounter("들판");

            BattleHandoff.SetRequest(state, encounter);
            var taken = BattleHandoff.TakeRequest();

            Assert.IsNotNull(taken);
            Assert.AreSame(state, taken.State);
            Assert.AreSame(encounter, taken.Encounter);
        }

        [Test]
        public void Request_IsClearedOnceTaken()
        {
            BattleHandoff.SetRequest(MakeState(), TestFixtures.MakeEncounter("들판"));

            Assert.IsNotNull(BattleHandoff.TakeRequest());
            Assert.IsNull(BattleHandoff.TakeRequest(), "두 번째로 꺼내면 비어 있어야 한다");
            Assert.IsFalse(BattleHandoff.HasRequest);
        }

        [Test]
        public void Request_IsNullWhenNothingWasSent()
        {
            Assert.IsFalse(BattleHandoff.HasRequest);
            Assert.IsNull(BattleHandoff.TakeRequest());
        }

        [Test]
        public void Result_RoundTripsConclusionRewardAndState()
        {
            var state = MakeState();

            BattleHandoff.SetResult(BattleConclusion.Victory, 24, state);
            var taken = BattleHandoff.TakeResult();

            Assert.IsNotNull(taken);
            Assert.AreEqual(BattleConclusion.Victory, taken.Conclusion);
            Assert.AreEqual(24, taken.GoldReward);
            Assert.AreSame(state, taken.State);
        }

        [Test]
        public void Result_IsClearedOnceTaken()
        {
            BattleHandoff.SetResult(BattleConclusion.Defeat, 0, MakeState());

            Assert.IsNotNull(BattleHandoff.TakeResult());
            Assert.IsNull(BattleHandoff.TakeResult(),
                "결과가 남아 있으면 농장에 돌아올 때마다 보상이 다시 들어간다");
            Assert.IsFalse(BattleHandoff.HasResult);
        }

        [Test]
        public void SetResult_DoesNotSpendTheReward()
        {
            // 재화는 농장 쪽에서만 더한다 (설계 3.3). 전달자는 액수만 나른다.
            var state = MakeState();

            BattleHandoff.SetResult(BattleConclusion.Victory, 24, state);

            Assert.AreEqual(30, state.Gold, "전달자가 재화를 건드리면 지급 지점이 둘이 된다");
        }

        [Test]
        public void Clear_EmptiesBothSides()
        {
            BattleHandoff.SetRequest(MakeState(), TestFixtures.MakeEncounter("들판"));
            BattleHandoff.SetResult(BattleConclusion.Aborted, 0, MakeState());

            BattleHandoff.Clear();

            Assert.IsFalse(BattleHandoff.HasRequest);
            Assert.IsFalse(BattleHandoff.HasResult);
        }
    }
}
```

테스트 7개. 기준선 표의 `+6` 대신 `+7` 이다 → 최종 **147개**.

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

기대: 컴파일 에러 — `BattleHandoff` / `BattleConclusion` 를 찾을 수 없음.

- [ ] **Step 3: 구현한다**

`Assets/Scripts/Runtime/Core/BattleHandoff.cs`:

```csharp
using FarmCreature.Data;

namespace FarmCreature.Core
{
    /// <summary>
    /// 전투가 어떻게 끝났는가. <see cref="BattleOutcome"/> 과 달리 '진행 중'이 없고
    /// '중도 이탈'이 있다. <b>따로 둔 이유</b>: <see cref="BattleSimulator"/> 는 손대지 않기로 했고
    /// (설계 1.1), 나가기는 규칙이 아니라 화면의 사정이다.
    /// </summary>
    public enum BattleConclusion
    {
        Victory,
        Defeat,

        /// <summary>전투 중에 나갔다. 보상 없음, 페널티도 없음.</summary>
        Aborted
    }

    /// <summary>농장 → 전투 씬으로 넘기는 것.</summary>
    public class BattleRequest
    {
        public GameState State;
        public BattleEncounter Encounter;
    }

    /// <summary>
    /// 전투 씬 → 농장으로 돌려보내는 것.
    /// <see cref="State"/> 를 함께 돌려주는 이유는 <see cref="BattleHandoff"/> 주석에 있다.
    /// </summary>
    public class BattleResult
    {
        public BattleConclusion Conclusion;

        /// <summary>승리했을 때 줄 재화. <b>여기서 지급하지 않는다</b> — 농장이 더한다 (설계 3.3).</summary>
        public int GoldReward;

        public GameState State;
    }

    /// <summary>
    /// 씬을 넘나들며 상태를 나르는 정적 전달자.
    ///
    /// <b>왜 <c>DontDestroyOnLoad</c> 싱글턴이 아닌가</b>: <see cref="GameState"/> 는 이미 순수 C# 이라
    /// <c>MonoBehaviour</c> 에 실어 나를 이유가 없고, 정적 클래스는 EditMode 에서 그대로 테스트된다.
    ///
    /// <b>왜 결과에도 상태를 싣는가</b>: 농장 씬이 다시 로드되면 <c>GameRoot.Start</c> 가 처음부터 돈다.
    /// 거기서 세이브를 읽으면 저장하지 않은 진행(먹이·훈련·장착)이 전부 날아간다.
    /// 그래서 들려보낸 상태 객체를 그대로 돌려받아 이어 쓴다.
    ///
    /// <b>꺼내면 비워진다.</b> 남겨두면 농장에 돌아올 때마다 지난 결과가 다시 적용된다.
    /// </summary>
    public static class BattleHandoff
    {
        private static BattleRequest request;
        private static BattleResult result;

        public static bool HasRequest
        {
            get { return request != null; }
        }

        public static bool HasResult
        {
            get { return result != null; }
        }

        public static void SetRequest(GameState state, BattleEncounter encounter)
        {
            request = new BattleRequest { State = state, Encounter = encounter };
        }

        /// <summary>요청을 꺼내고 비운다. 없으면 <c>null</c> — 전투 씬을 단독으로 Play 한 경우다.</summary>
        public static BattleRequest TakeRequest()
        {
            var taken = request;
            request = null;
            return taken;
        }

        public static void SetResult(BattleConclusion conclusion, int goldReward, GameState state)
        {
            result = new BattleResult
            {
                Conclusion = conclusion,
                GoldReward = goldReward < 0 ? 0 : goldReward,
                State = state
            };
        }

        /// <summary>결과를 꺼내고 비운다. 없으면 <c>null</c> — 전투를 거치지 않고 농장을 연 경우다.</summary>
        public static BattleResult TakeResult()
        {
            var taken = result;
            result = null;
            return taken;
        }

        /// <summary>양쪽을 모두 비운다. 테스트와 새 게임 시작에 쓴다.</summary>
        public static void Clear()
        {
            request = null;
            result = null;
        }
    }
}
```

- [ ] **Step 4: 테스트를 다시 돌린다**

기대: `total="144" passed="144" failed="0"` (137 + 7).

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/Runtime/Core/BattleHandoff.cs Assets/Scripts/Tests/EditMode/BattleHandoffTests.cs
git commit -m "feat: 농장↔전투 씬 상태 전달자 BattleHandoff 추가"
```

---

## Task 4: 전투터 라벨 정리

**Files:**
- Modify: `Assets/Scripts/Runtime/Presentation/PanelLabels.cs` (150–213줄의 "전투" 절)
- Modify: `Assets/Scripts/Tests/EditMode/PanelLabelsTests.cs` (176–196줄 `BattleRoster_ShowsBothSidesWithHealth` 삭제, 새 테스트 추가)

**Interfaces:**
- Consumes: `BattleEncounter`(Task 1), `GameState`·`Roster`(기존), `BattleSimulator`(기존, 읽기만)
- Produces:
  - `PanelLabels.EncounterButton(BattleEncounter encounter)` → `string`
  - `PanelLabels.BattleReadyLine(GameState state)` → `string`
  - `PanelLabels.SpeedLabel(float speed)` → `string`
  - `PanelLabels.BattleTitle(BattleSimulator battle)` → `string` (**그대로 둔다**)
  - `PanelLabels.BattleRoster` → **삭제** (전투 씬에서는 유닛과 체력바가 그 일을 한다)

- [ ] **Step 1: 새 라벨 테스트를 쓰고 죽은 테스트를 지운다**

`PanelLabelsTests.cs` 의 `BattleRoster_ShowsBothSidesWithHealth` 테스트(176–196줄)를 **통째로 삭제**하고,
그 자리에 다음을 넣는다:

```csharp
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
```

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

기대: 컴파일 에러 — `EncounterButton` / `BattleReadyLine` / `SpeedLabel` 이 없음.

- [ ] **Step 3: `PanelLabels` 의 전투 절을 고친다**

`BattleRoster` 와 그 전용 헬퍼 `AppendSide`(170–203줄)를 **삭제**하고, `EnemyButton`(205–213줄)도 삭제한다.
(`EnemyButton` 은 전투터가 인카운터 단위로 바뀌면서 쓰이지 않는다.)

`BattleTitle` 은 그대로 두고 — 전투 씬의 제목 줄이 그대로 쓴다 — 그 아래에 다음을 넣는다:

```csharp
        /// <summary>전투터 버튼 한 칸. 몇 마리와 붙고 얼마를 버는지 들어가기 전에 보여준다.</summary>
        public static string EncounterButton(BattleEncounter encounter)
        {
            if (encounter == null)
            {
                return "(없음)";
            }

            return encounter.displayName
                   + "\n적 " + encounter.TotalEnemyCount
                   + " · 재화 " + encounter.TotalGoldReward;
        }

        /// <summary>전투터를 열었을 때의 안내 한 줄. 파티가 비어 있으면 어디서 내보내는지 알려준다.</summary>
        public static string BattleReadyLine(GameState state)
        {
            if (state == null || state.Roster.Party.Count == 0)
            {
                return "밖에 나와 있는 케어가 없다. 헛간에서 먼저 내보내자.";
            }

            return "출전 " + state.Roster.Party.Count + "/" + Roster.MaxParty
                   + "   " + GoldLine(state.Gold);
        }

        /// <summary>전투 화면의 배속 표시. 1x / 2x 두 단계뿐이다 (설계 8.3).</summary>
        public static string SpeedLabel(float speed)
        {
            return "배속 " + (speed >= 2f ? "2x" : "1x");
        }
```

- [ ] **Step 4: 테스트를 다시 돌린다**

기대: `total="148" passed="148" failed="0"` (144 − 1 + 5).

`BattleRoster` 를 지웠는데 컴파일 에러가 나면 아직 부르는 곳이 남아 있다는 뜻이다 —
`BattlePanel.cs` 의 `rosterText` 배선인데, 그 파일은 Task 6 에서 통째로 갈아치운다.
지금 단계에서는 `BattlePanel.Refresh()` 의 `rosterText.text = PanelLabels.BattleRoster(battle);`
두 줄(122–125줄)만 잠시 지워 컴파일을 통과시킨다.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/Runtime/Presentation/PanelLabels.cs \
        Assets/Scripts/Runtime/Presentation/BattlePanel.cs \
        Assets/Scripts/Tests/EditMode/PanelLabelsTests.cs
git commit -m "refactor: 전투터 라벨을 인카운터 단위로 바꾸고 죽은 로스터 라벨 삭제"
```

---

## Task 5: 전투 씬 좌표 규약과 유닛 뷰

**Files:**
- Create: `Assets/Scripts/Runtime/Presentation/BattleField.cs`
- Create: `Assets/Scripts/Runtime/Presentation/BattleUnitView.cs`

**Interfaces:**
- Consumes: `BattleUnit`(기존, 읽기만 — `Name`·`Hp`·`MaxHp`·`IsAlive`)
- Produces:
  - `BattleField.MaxEnemies` (`const int` = 8)
  - `BattleField.SortingOrderFor(float y)` → `int`
  - `BattleField.ToWorld(Vector2 point)` → `Vector3`
  - `BattleUnitView` : `MonoBehaviour` — 공개 필드 `body`, `healthBarBack`, `healthBarFill`,
    `nameLabel`, `healthBarWidth`; 메서드 `Show(BattleUnit, Color, float, bool, Vector2)`,
    `Hide()`, `Refresh()`

**체력바를 uGUI 가 아니라 `SpriteRenderer` 로 만드는 이유 (설계 4.1 에서 벗어나는 점):**
설계는 "`Square.png` 를 늘린 Image 두 장" 이라고 적었지만, 월드 좌표에 uGUI 를 붙이려면
World Space Canvas 라는 새 장치가 필요하다. 농장 씬은 이미 건물 이름표를 `TextMesh` 로 처리하고 있어
같은 방식을 따른다. 덤으로 **무결성 검사 1번(모든 `SpriteRenderer` 가 에셋 스프라이트를 참조하는가)이
체력바까지 공짜로 덮는다.**

- [ ] **Step 1: `BattleField` 를 만든다**

`Assets/Scripts/Runtime/Presentation/BattleField.cs`:

```csharp
using UnityEngine;

namespace FarmCreature.Presentation
{
    /// <summary>
    /// 전투 씬의 탑뷰 평면. 농장의 <see cref="FarmArea"/> 에 대응한다.
    ///
    /// <b>여기 있는 좌표는 전부 표시 전용이다</b> — <see cref="FarmCreature.Core.BattleSimulator"/> 는
    /// 이 값을 한 번도 읽지 않는다 (설계 1.1). 이동·사거리를 넣고 싶어지면 그때는 이 결정을
    /// 뒤집는 별도 설계가 필요하다.
    /// </summary>
    public static class BattleField
    {
        /// <summary>
        /// 씬에 미리 만들어 두는 적 슬롯 수. 인카운터 총원이 이보다 많으면
        /// <c>BuildAll</c> 이 <c>LogError</c> 로 실패시킨다 (설계 4.3).
        /// <b>이 값을 바꾸면 슬롯이 씬에 고정돼 있으므로 BuildAll 재실행이 필요하다.</b>
        /// </summary>
        public const int MaxEnemies = 8;

        /// <summary>탑뷰라서 아래(작은 y)에 선 유닛이 앞에 그려져야 겹침이 자연스럽다.</summary>
        public static int SortingOrderFor(float y)
        {
            return Mathf.RoundToInt(-y * 10f);
        }

        /// <summary>z 는 0 으로 고정한다. 앞뒤는 <see cref="SortingOrderFor"/> 로만 가른다.</summary>
        public static Vector3 ToWorld(Vector2 point)
        {
            return new Vector3(point.x, point.y, 0f);
        }
    }
}
```

- [ ] **Step 2: `BattleUnitView` 를 만든다**

`Assets/Scripts/Runtime/Presentation/BattleUnitView.cs`:

```csharp
using FarmCreature.Core;
using UnityEngine;

namespace FarmCreature.Presentation
{
    /// <summary>
    /// 전투 화면의 유닛 하나 — 몸통 · 이름표 · 체력바.
    ///
    /// 슬롯은 씬에 <see cref="Roster.MaxParty"/> + <see cref="BattleField.MaxEnemies"/> 개가
    /// 미리 만들어져 있고 인원수에 맞춰 켜고 끈다. 런타임 생성을 쓰지 않는 이유는
    /// 코드로 만든 오브젝트의 스프라이트 참조가 씬에 남지 않기 때문이다 (HANDOFF 함정 3.1).
    ///
    /// 체력바에 uGUI 를 쓰지 않고 <see cref="SpriteRenderer"/> 를 쓰는 이유:
    /// 월드 좌표에 uGUI 를 붙이려면 World Space Canvas 가 필요한데, 농장 씬은 이미 건물 이름표를
    /// <see cref="TextMesh"/> 로 처리하고 있어 같은 방식을 따른다.
    /// 덤으로 무결성 검사 1번이 체력바 스프라이트까지 함께 본다.
    /// </summary>
    public class BattleUnitView : MonoBehaviour
    {
        [Tooltip("몸통. 농장과 같은 Circle.png 에 종/적의 색을 입힌다. GameBootstrap 이 채운다.")]
        public SpriteRenderer body;

        public SpriteRenderer healthBarBack;
        public SpriteRenderer healthBarFill;
        public TextMesh nameLabel;

        [Tooltip("체력이 가득 찼을 때 체력바의 가로 길이 (월드 유닛).")]
        public float healthBarWidth = 1.3f;

        [Tooltip("쓰러진 유닛을 남겨둘 때의 투명도. 지우지 않는 이유는 무슨 일이 났는지 보이게 하려는 것.")]
        public float downedAlpha = 0.35f;

        private BattleUnit unit;
        private Color baseColor = Color.white;

        /// <summary>
        /// 이 슬롯을 켜고 한 유닛을 담는다.
        /// <paramref name="facingRight"/> 로 서로 마주 보게 만든다 — 아군은 오른쪽, 적은 왼쪽.
        /// </summary>
        public void Show(BattleUnit battleUnit, Color color, float scale, bool facingRight, Vector2 position)
        {
            unit = battleUnit;
            baseColor = color;

            gameObject.SetActive(true);
            transform.position = BattleField.ToWorld(position);

            if (body != null)
            {
                body.flipX = !facingRight;
                body.transform.localScale = new Vector3(scale, scale, 1f);
                body.sortingOrder = BattleField.SortingOrderFor(position.y);
            }

            // 체력바와 이름표는 항상 몸통보다 앞에 그린다.
            ApplySortingOffset(healthBarBack, 1);
            ApplySortingOffset(healthBarFill, 2);

            if (nameLabel != null)
            {
                nameLabel.text = battleUnit != null ? battleUnit.Name : string.Empty;

                var labelRenderer = nameLabel.GetComponent<MeshRenderer>();
                if (labelRenderer != null && body != null)
                {
                    labelRenderer.sortingOrder = body.sortingOrder + 3;
                }
            }

            Refresh();
        }

        public void Hide()
        {
            unit = null;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 체력바를 다시 그린다. 쓰러진 유닛은 <b>지우지 않고</b> 반투명으로 남긴다 —
        /// 갑자기 사라지면 무슨 일이 났는지 보이지 않는다 (설계 4.1).
        /// </summary>
        public void Refresh()
        {
            if (unit == null)
            {
                return;
            }

            float ratio = unit.MaxHp > 0 ? Mathf.Clamp01((float)unit.Hp / unit.MaxHp) : 0f;

            if (healthBarFill != null)
            {
                var scale = healthBarFill.transform.localScale;
                healthBarFill.transform.localScale = new Vector3(healthBarWidth * ratio, scale.y, 1f);

                // 가운데 피벗이라, 왼쪽 끝을 고정하려면 줄어든 만큼 오른쪽으로 밀어준다.
                var local = healthBarFill.transform.localPosition;
                healthBarFill.transform.localPosition = new Vector3(
                    -healthBarWidth * 0.5f + healthBarWidth * ratio * 0.5f, local.y, local.z);
            }

            if (body != null)
            {
                body.color = unit.IsAlive
                    ? baseColor
                    : new Color(baseColor.r, baseColor.g, baseColor.b, downedAlpha);
            }
        }

        private void ApplySortingOffset(SpriteRenderer renderer, int offset)
        {
            if (renderer != null && body != null)
            {
                renderer.sortingOrder = body.sortingOrder + offset;
            }
        }
    }
}
```

- [ ] **Step 3: 컴파일과 기존 테스트를 확인한다**

```bash
Unity.exe -batchmode -runTests -projectPath "C:\Users\이지민\UnityProjects\MCPTest" \
  -testPlatform EditMode -testResults "<tmp>\results.xml" -logFile "<tmp>\test.log"
```

기대: 종료 코드 0, `total="148" passed="148"`. 개수가 늘지 않는 것이 정상이다 —
`MonoBehaviour` 는 EditMode 로 검증하지 않고 씬 무결성 검사(Task 10)가 본다.
`<tmp>\test.log` 에 `error CS` 가 한 줄도 없어야 한다.

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/Runtime/Presentation/BattleField.cs \
        Assets/Scripts/Runtime/Presentation/BattleUnitView.cs
git commit -m "feat: 전투 씬 좌표 규약과 유닛 뷰(몸통·체력바·이름표) 추가"
```

---

## Task 6: 전투 씬 구동과 농장 쪽 배선

이 태스크는 세 파일을 함께 바꾼다. **따로 나누면 중간 상태가 컴파일되지 않는다** —
`BattlePanel` 이 전투를 굴리는 것을 그만두는 순간 `GameRoot` 가 보상을 받아야 하고,
그 보상은 `BattleSceneRoot` 가 만들어 보낸다.

**Files:**
- Create: `Assets/Scripts/Runtime/Presentation/BattleSceneRoot.cs`
- Rewrite: `Assets/Scripts/Runtime/Presentation/BattlePanel.cs` (전량)
- Modify: `Assets/Scripts/Runtime/Presentation/GameRoot.cs` (30–69줄 `Start`)

**Interfaces:**
- Consumes: `BattleHandoff`·`BattleConclusion`·`BattleResult`(Task 3), `SpawnPointPicker`(Task 2),
  `BattleEncounter`·`BattleEncounterDatabase`·`BattleMapData`(Task 1),
  `BattleField`·`BattleUnitView`(Task 5), `PanelLabels.BattleTitle/EncounterButton/BattleReadyLine/SpeedLabel`(Task 4),
  `BattleSimulator`(기존, 읽기만)
- Produces:
  - `BattleSceneRoot.SetSpeed(int speedIndex)` — 버튼이 int 로 부른다 (0 = 1x, 1 = 2x)
  - `BattleSceneRoot.ExitBattle()` — 무인자, 버튼이 부른다
  - `BattleSceneRoot.FarmSceneName` (`const string` = `"FarmScene"`)
  - `BattlePanel.StartEncounter(int encounterIndex)` — 버튼이 int 로 부른다
  - `BattlePanel.BattleSceneName` (`const string` = `"BattleScene"`)
  - `BattlePanel.encounterDatabase` / `BattlePanel.careManager` / `BattlePanel.titleText` / `BattlePanel.infoText`

**인덱스가 어긋나면 안 된다 — 중요:**
`BattleSimulator` 생성자는 `null` 인 케어/적을 건너뛴다. 그래서 시뮬레이터에 넘기기 **전에**
`null` 을 걸러낸 목록을 만들고, 그 목록으로 색·크기를 읽어야 `Allies[i]` ↔ `party[i]` 가 1:1 로 맞는다.
걸러내지 않으면 어떤 유닛이 남의 색을 입고 남의 체력바를 흔든다.

- [ ] **Step 1: `BattleSceneRoot` 를 만든다**

`Assets/Scripts/Runtime/Presentation/BattleSceneRoot.cs`:

```csharp
using System.Collections.Generic;
using System.Text;
using FarmCreature.Core;
using FarmCreature.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FarmCreature.Presentation
{
    /// <summary>
    /// 전투 씬을 구동한다. 규칙은 전부 <see cref="BattleSimulator"/> 안에 있고 여기서는
    /// <b>시간만 흘려보낸다</b> — 그래서 배속은 <c>Time.deltaTime * speed</c> 한 줄로 끝난다 (설계 4.2).
    ///
    /// 플레이어는 <b>관전만</b> 한다. 조작은 배속과 나가기뿐이다.
    ///
    /// <b>재화를 여기서 더하지 않는다.</b> 승패와 보상액만 <see cref="BattleHandoff"/> 에 담고,
    /// 농장으로 돌아간 뒤 <see cref="GameRoot"/> 가 지급한다. 지급 지점이 하나뿐이라
    /// 씬이 중간에 바뀌어도 재화가 이중으로 들어가지 않는다 (설계 3.3).
    /// </summary>
    public class BattleSceneRoot : MonoBehaviour
    {
        public const string FarmSceneName = "FarmScene";

        [Header("맵")]
        [Tooltip("탑뷰 바닥. GameBootstrap 이 채운다.")]
        public SpriteRenderer ground;

        [Tooltip("인카운터에 맵이 지정되지 않았을 때 쓸 기본 맵.")]
        public BattleMapData defaultMap;

        [Header("슬롯 — 씬에 미리 만들어 둔다 (런타임 생성 금지)")]
        public List<BattleUnitView> allySlots = new List<BattleUnitView>();
        public List<BattleUnitView> enemySlots = new List<BattleUnitView>();

        [Header("UI — 전부 Canvas 하위여야 한글 폰트가 붙는다")]
        public Text titleText;
        public Text logText;
        public Text speedText;

        [Tooltip("전투 로그에서 화면에 남길 줄 수.")]
        public int visibleLogLines = 6;

        [Header("연습 전투 — 전투 씬을 단독으로 Play 했을 때만 쓴다")]
        public BattleEncounter practiceEncounter;
        public CreatureSpeciesData practiceSpecies;

        private BattleSimulator battle;
        private GameState state;
        private float speed = 1f;
        private bool concluded;

        private readonly List<BattleUnitView> shownAllies = new List<BattleUnitView>();
        private readonly List<BattleUnitView> shownEnemies = new List<BattleUnitView>();
        private readonly StringBuilder builder = new StringBuilder();

        private void Start()
        {
            // 배경에서도 전투가 흘러야 한다. 켜지 않으면 에디터 창이 비활성일 때
            // 플레이어 루프가 멈춰 전투가 멎은 것처럼 보인다 (HANDOFF 8절).
            Application.runInBackground = true;

            HideAllSlots();

            var request = BattleHandoff.TakeRequest();
            BattleEncounter encounter;

            if (request != null)
            {
                state = request.State;
                encounter = request.Encounter;
            }
            else
            {
                // 개발 중 전투 씬만 열어보는 일이 잦다. 오류로 죽는 대신 연습 전투로 시작한다 (설계 3.2).
                Debug.Log("[FarmCreature] 전투 요청이 없어 연습 전투로 시작합니다. (전투 씬 단독 Play)");
                state = BuildPracticeState();
                encounter = practiceEncounter;
            }

            StartBattle(encounter);
        }

        private void StartBattle(BattleEncounter encounter)
        {
            var party = LivePartyOf(state);
            var foes = FoesOf(encounter);

            battle = new BattleSimulator(party, foes);

            var map = encounter != null && encounter.map != null ? encounter.map : defaultMap;
            ApplyMap(map);
            PlaceUnits(map, party, foes);

            RefreshTexts();
        }

        // ---------- 준비 ----------

        /// <summary>
        /// <see cref="BattleSimulator"/> 는 <c>null</c> 을 건너뛴다. 그래서 넘기기 전에 걸러야
        /// <c>battle.Allies[i]</c> 와 <c>party[i]</c> 가 1:1 로 맞는다 — 어긋나면 유닛이 남의 색을 입는다.
        /// </summary>
        private static List<CreatureInstance> LivePartyOf(GameState gameState)
        {
            var party = new List<CreatureInstance>();
            if (gameState == null)
            {
                return party;
            }

            var members = gameState.Roster.Party;
            for (int i = 0; i < members.Count && party.Count < Roster.MaxParty; i++)
            {
                if (members[i] != null)
                {
                    party.Add(members[i]);
                }
            }

            return party;
        }

        /// <summary>
        /// 인카운터를 적 목록으로 펼친다. 슬롯보다 많으면 <c>LogError</c> 로 알리고 잘라낸다 —
        /// 여기까지 왔다는 것은 <c>BuildAll</c> 의 검사를 지나쳤다는 뜻이라 조용히 넘기면 안 된다.
        /// </summary>
        private static List<EnemyData> FoesOf(BattleEncounter encounter)
        {
            if (encounter == null)
            {
                Debug.LogError("[FarmCreature] 인카운터가 없어 적 없는 전투가 됐다.");
                return new List<EnemyData>();
            }

            var foes = encounter.BuildEnemyList();

            if (foes.Count > BattleField.MaxEnemies)
            {
                Debug.LogError("[FarmCreature] 인카운터 '" + encounter.displayName + "' 의 적이 "
                    + foes.Count + "마리다. 슬롯은 " + BattleField.MaxEnemies
                    + "개뿐이라 잘라낸다. BuildAll 의 검사를 확인할 것.");
                foes.RemoveRange(BattleField.MaxEnemies, foes.Count - BattleField.MaxEnemies);
            }

            return foes;
        }

        private void ApplyMap(BattleMapData map)
        {
            if (map == null || ground == null)
            {
                return;
            }

            ground.transform.localScale = new Vector3(map.groundSize.x, map.groundSize.y, 1f);
            ground.color = map.groundColor;
        }

        /// <summary>
        /// 양측을 각자의 영역 안 랜덤 좌표에 세운다. <b>좌표는 표시 전용이다</b> —
        /// 스폰이 랜덤이어도 전투 결과는 결정적이다 (설계 1.1).
        /// </summary>
        private void PlaceUnits(BattleMapData map, List<CreatureInstance> party, List<EnemyData> foes)
        {
            if (map == null)
            {
                Debug.LogError("[FarmCreature] 맵이 없어 유닛을 세울 수 없다. defaultMap 배선을 확인할 것.");
                return;
            }

            // 게임에서는 매번 다른 배치가 나와야 한다. seed 를 인자로 받는 것은 테스트를 위한 것이다.
            int seed = Random.Range(int.MinValue, int.MaxValue);

            var allyPoints = SpawnPointPicker.PickSpawnPoints(
                map.AllySpawnArea, party.Count, map.minSpacing, seed);
            var enemyPoints = SpawnPointPicker.PickSpawnPoints(
                map.EnemySpawnArea, foes.Count, map.minSpacing, seed + 1);

            shownAllies.Clear();
            for (int i = 0; i < party.Count && i < allySlots.Count; i++)
            {
                var species = party[i].CurrentSpecies;
                var view = allySlots[i];

                view.Show(battle.Allies[i],
                    species != null ? species.placeholderColor : Color.white,
                    species != null ? species.placeholderScale : 1f,
                    facingRight: true,
                    position: allyPoints[i]);

                shownAllies.Add(view);
            }

            shownEnemies.Clear();
            for (int i = 0; i < foes.Count && i < enemySlots.Count; i++)
            {
                var view = enemySlots[i];

                view.Show(battle.Enemies[i],
                    foes[i].placeholderColor,
                    scale: 1f,
                    facingRight: false,
                    position: enemyPoints[i]);

                shownEnemies.Add(view);
            }
        }

        private void HideAllSlots()
        {
            for (int i = 0; i < allySlots.Count; i++)
            {
                if (allySlots[i] != null)
                {
                    allySlots[i].Hide();
                }
            }

            for (int i = 0; i < enemySlots.Count; i++)
            {
                if (enemySlots[i] != null)
                {
                    enemySlots[i].Hide();
                }
            }
        }

        private GameState BuildPracticeState()
        {
            var roster = new Roster();

            if (practiceSpecies != null)
            {
                roster.Add(CreatureInstance.CreateNew(practiceSpecies));
                roster.JoinParty(roster.GetMember(0));
            }

            return new GameState(roster, new Inventory(), 0);
        }

        // ---------- 진행 ----------

        private void Update()
        {
            if (battle == null)
            {
                return;
            }

            if (battle.Outcome == BattleOutcome.InProgress)
            {
                battle.Tick(Time.deltaTime * speed);
                RefreshUnits();
            }

            if (battle.Outcome != BattleOutcome.InProgress && !concluded)
            {
                concluded = true;
                RefreshUnits();

                BattleHandoff.SetResult(
                    battle.Outcome == BattleOutcome.Victory
                        ? BattleConclusion.Victory
                        : BattleConclusion.Defeat,
                    battle.Outcome == BattleOutcome.Victory ? battle.GoldReward : 0,
                    state);
            }

            RefreshTexts();
        }

        private void RefreshUnits()
        {
            for (int i = 0; i < shownAllies.Count; i++)
            {
                shownAllies[i].Refresh();
            }

            for (int i = 0; i < shownEnemies.Count; i++)
            {
                shownEnemies[i].Refresh();
            }
        }

        private void RefreshTexts()
        {
            if (titleText != null)
            {
                titleText.text = concluded
                    ? PanelLabels.BattleTitle(battle) + "   [나가기]로 농장에 돌아간다"
                    : PanelLabels.BattleTitle(battle);
            }

            if (speedText != null)
            {
                speedText.text = PanelLabels.SpeedLabel(speed);
            }

            if (logText != null)
            {
                logText.text = TailOfLog();
            }
        }

        private string TailOfLog()
        {
            if (battle == null)
            {
                return string.Empty;
            }

            var lines = battle.Log;
            int start = lines.Count - visibleLogLines;
            if (start < 0)
            {
                start = 0;
            }

            builder.Length = 0;
            for (int i = start; i < lines.Count; i++)
            {
                builder.AppendLine(lines[i]);
            }

            return builder.ToString();
        }

        // ---------- 버튼 ----------

        /// <summary>
        /// uGUI 버튼이 배속 인덱스를 넘긴다 (0 = 1x, 1 = 2x).
        /// int 인자만 안정적으로 직렬화되기 때문이다 (HANDOFF 함정 3.6).
        /// </summary>
        public void SetSpeed(int speedIndex)
        {
            speed = speedIndex <= 0 ? 1f : 2f;
            RefreshTexts();
        }

        /// <summary>
        /// 농장으로 돌아간다. 전투 중에 눌러도 되고, 그때는 <b>보상 없음</b>이다.
        /// 도망 페널티는 두지 않는다 — 져도 잃는 것이 없는 게임이다 (설계 4.2).
        /// </summary>
        public void ExitBattle()
        {
            if (!concluded)
            {
                BattleHandoff.SetResult(BattleConclusion.Aborted, 0, state);
            }

            SceneManager.LoadScene(FarmSceneName);
        }
    }
}
```

- [ ] **Step 2: `BattlePanel` 을 인카운터 선택 패널로 갈아치운다**

`Assets/Scripts/Runtime/Presentation/BattlePanel.cs` **전량 교체**:

```csharp
using FarmCreature.Core;
using FarmCreature.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FarmCreature.Presentation
{
    /// <summary>
    /// 전투터 — 어디로 나갈지 고르는 곳이다. <b>전투 자체는 여기서 굴리지 않는다</b>:
    /// 인카운터를 골라 <see cref="BattleHandoff"/> 에 실은 뒤 전투 씬을 연다 (설계 3절).
    ///
    /// 이전 판에서는 이 패널이 <see cref="BattleSimulator"/> 를 직접 굴리고 재화까지 지급했다.
    /// 그 역할은 각각 <see cref="BattleSceneRoot"/> 와 <see cref="GameRoot"/> 로 옮겼다.
    /// </summary>
    public class BattlePanel : GamePanel
    {
        public const string BattleSceneName = "BattleScene";

        public Text titleText;
        public Text infoText;

        [Tooltip("버튼 개수가 이 목록에서 나온다. GameBootstrap 이 채운다.")]
        public BattleEncounterDatabase encounterDatabase;

        [Tooltip("안내 로그를 맡길 조율자. GameBootstrap 이 채운다.")]
        public CareManager careManager;

        /// <summary>
        /// uGUI 버튼이 <see cref="BattleEncounterDatabase"/> 인덱스를 넘긴다 (함정 3.6).
        ///
        /// 파티가 비었으면 <b>씬을 바꾸기 전에</b> 막는다. 예전에는 시뮬레이터가 시작하자마자
        /// 패배로 끝내도록 뒀지만, 이제는 그 대가가 씬 전환 왕복이라 그냥 두면 안 된다.
        /// </summary>
        public void StartEncounter(int encounterIndex)
        {
            if (state == null || encounterDatabase == null)
            {
                Log("전투터를 열 수 없다.");
                return;
            }

            var encounter = encounterDatabase.Get(encounterIndex);
            if (encounter == null)
            {
                Log("갈 곳을 찾을 수 없다.");
                return;
            }

            if (state.Roster.Party.Count == 0)
            {
                Log("밖에 나와 있는 케어가 없다. 헛간에서 먼저 내보내자.");
                return;
            }

            BattleHandoff.SetRequest(state, encounter);
            SceneManager.LoadScene(BattleSceneName);
        }

        public override void Refresh()
        {
            if (titleText != null)
            {
                titleText.text = "전투터";
            }

            if (infoText != null)
            {
                infoText.text = PanelLabels.BattleReadyLine(state);
            }
        }

        private void Log(string message)
        {
            if (careManager != null)
            {
                careManager.SetLog(message);
            }
        }
    }
}
```

- [ ] **Step 3: `GameRoot.Start` 가 전투 복귀를 처리하게 한다**

`Assets/Scripts/Runtime/Presentation/GameRoot.cs` 의 `Start()`(30–69줄)를 다음으로 교체한다:

```csharp
        private void Start()
        {
            GameState state;
            string message;

            // 전투에서 돌아왔는가? 세이브를 다시 읽으면 저장 전 진행(먹이·훈련·장착)이 날아가므로,
            // 전투에 들려보냈던 상태를 그대로 이어받는다.
            var battleResult = BattleHandoff.TakeResult();

            if (battleResult != null && battleResult.State != null)
            {
                state = battleResult.State;
                message = ApplyBattleResult(state, battleResult);
            }
            else
            {
                string error;

                if (SaveSystem.TryLoad(speciesDatabase, itemDatabase, out state, out error))
                {
                    message = "이어서 돌보는 중.";
                }
                else
                {
                    if (!string.IsNullOrEmpty(error))
                    {
                        Debug.LogWarning("[FarmCreature] 세이브를 불러오지 못해 새로 시작합니다: " + error);
                    }

                    state = BuildNewGame();
                    message = "새로운 케어들을 맞이했다.";
                }
            }

            if (hud != null)
            {
                hud.Bind(state);
            }

            if (careManager != null)
            {
                careManager.Bind(state, hud);
                careManager.SetLog(message);
            }

            for (int i = 0; i < farmBuildings.Count; i++)
            {
                if (farmBuildings[i] != null)
                {
                    farmBuildings[i].Bind(hud);
                }
            }
        }

        /// <summary>
        /// 전투 보상을 지급한다. <b>재화가 들어오는 지점은 여기 하나뿐이다</b> —
        /// 전투 씬은 액수만 실어 보낸다. 씬이 중간에 바뀌어도 이중 지급이 없다 (설계 3.3).
        /// </summary>
        private static string ApplyBattleResult(GameState state, BattleResult result)
        {
            switch (result.Conclusion)
            {
                case BattleConclusion.Victory:
                    state.AddGold(result.GoldReward);
                    return "승리했다! 재화 " + result.GoldReward + " 획득.";

                case BattleConclusion.Defeat:
                    return "패배했다. 케어는 다치지 않는다 — 더 키워서 다시 오자.";

                default:
                    return "전투에서 물러났다.";
            }
        }
```

`GameRoot.cs` 의 `using` 목록에 `FarmCreature.Core` 가 이미 있는지 확인한다 (2줄에 있다).

- [ ] **Step 4: 컴파일과 기존 테스트를 확인한다**

```bash
Unity.exe -batchmode -runTests -projectPath "C:\Users\이지민\UnityProjects\MCPTest" \
  -testPlatform EditMode -testResults "<tmp>\results.xml" -logFile "<tmp>\test.log"
```

기대: 종료 코드 0, `total="148" passed="148"`.

`<tmp>\test.log` 에서 `error CS` 를 찾는다. 이 시점에 `GameBootstrap.cs` 는 아직
없어진 `BattlePanel.rosterText` · `logText` · `enemyDatabase` 를 참조하고 있어 **컴파일 에러가 난다.**
Task 8 에서 고칠 예정이지만 지금 통과시켜야 하므로, `GameBootstrap.BuildBattlePanel`(786–835줄)의
`panel.enemyDatabase = enemyDb;` · `panel.rosterText = ...` · `panel.logText = ...` 세 줄과
상대 버튼 루프를 **임시로 주석 처리**한다. Task 8 에서 그 메서드를 통째로 다시 쓴다.

> 임시 주석을 남긴 채 커밋하는 것이 마음에 걸리면 Task 6 과 Task 8 을 한 커밋으로 묶어도 된다.
> 다만 **테스트는 반드시 이 단계에서 한 번 돌린다** — 런타임 코드의 컴파일 에러를
> 에디터 코드 에러와 섞으면 어느 쪽이 원인인지 알 수 없게 된다.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/Runtime/Presentation/BattleSceneRoot.cs \
        Assets/Scripts/Runtime/Presentation/BattlePanel.cs \
        Assets/Scripts/Runtime/Presentation/GameRoot.cs \
        Assets/Scripts/Editor/GameBootstrap.cs
git commit -m "feat: 전투를 별도 씬으로 분리하고 보상 지급을 농장 한 곳으로 모음"
```

---

## Task 7: 전투 데이터 seed (`LoadOrSeed`)

**Files:**
- Modify: `Assets/Scripts/Editor/GameBootstrap.cs`
  - 22–28줄 경로 상수에 `BattleDir` 추가
  - 77–85줄 `EnsureFolders` 에 `BattleDir` 추가
  - 168–179줄 `LoadOrCreate` 아래에 `LoadOrSeed` 추가
  - 299–336줄 `BuildEnemyAssets` 를 seed-only 로 변경
  - `BuildBattleDataAssets` 신규 추가
  - 36–73줄 `BuildAll` 에서 호출

**Interfaces:**
- Consumes: `BattleMapData`·`BattleEncounter`·`BattleEncounterDatabase`(Task 1), `BattleField.MaxEnemies`(Task 5)
- Produces:
  - `GameBootstrap.LoadOrSeed<T>(string assetPath, out bool created)` (private)
  - `GameBootstrap.BuildBattleDataAssets(List<EnemyData> enemies)` → `BattleEncounterDatabase` (private)
  - 에셋: `Assets/GameData/Battle/FieldMap.asset`, `RatField.asset`, `BoarThicket.asset`,
    `GolemRidge.asset`, `Assets/GameData/BattleEncounterDatabase.asset`

**seed-only 의 대가 (설계 2.2):** 전투 데이터의 **초기값을 코드에서 바꿔도 기존 에셋에는 반영되지 않는다.**
초기값을 다시 깔려면 해당 `.asset` 파일을 지우고 `BuildAll` 을 돌려야 한다.
이 사실은 Task 10 에서 HANDOFF 에 적는다.

- [ ] **Step 1: 경로 상수와 폴더를 추가한다**

22–28줄의 상수 블록에 추가:

```csharp
        private const string BattleDir = DataRoot + "/Battle";
        private const string FarmScenePath = ScenesDir + "/FarmScene.unity";
        private const string BattleScenePath = ScenesDir + "/BattleScene.unity";
```

기존 `ScenePath` 상수(28줄)는 `FarmScenePath` 로 이름을 바꾸고, 참조하는 두 곳
(72줄 로그, 545·547줄 저장·빌드설정)을 함께 고친다.

`EnsureFolders`(77–85줄)에 한 줄 추가:

```csharp
            EnsureFolder(BattleDir);
```

- [ ] **Step 2: `LoadOrSeed` 를 추가한다**

`LoadOrCreate`(168–179줄) 바로 아래에:

```csharp
        /// <summary>
        /// 없으면 만들어 초기값을 넣고, <b>이미 있으면 손대지 않는다.</b>
        /// <paramref name="created"/> 가 <c>true</c> 면 이번에 새로 만든 것이라 초기값을 채워야 한다.
        ///
        /// <b>전투 데이터에만 쓴다</b> — 손으로 밸런스를 만지는 대상이 전투뿐이기 때문이다.
        /// 종·아이템·씬은 지금처럼 <see cref="LoadOrCreate"/> 로 코드가 전권을 쥔다.
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
```

- [ ] **Step 3: 적 에셋을 seed-only 로 바꾼다**

`BuildEnemyAssets`(299–336줄)를 다음으로 교체한다. 값은 기존과 **완전히 같다** —
바뀌는 것은 "이미 있으면 덮어쓰지 않는다" 뿐이다.

```csharp
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
```

- [ ] **Step 4: 전투 맵·인카운터를 seed 한다**

`BuildEnemyDatabase`(338–344줄) 아래에 추가:

```csharp
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
```

`GameBootstrap.cs` 위쪽의 `using` 에 `FarmCreature.Presentation` 이 이미 있다(5줄) — `BattleField` 를 그대로 쓴다.

- [ ] **Step 5: `BuildAll` 에서 부른다**

`BuildAll`(60–70줄)의 에셋 생성 블록에 한 줄 추가:

```csharp
            var enemyDb = BuildEnemyDatabase(enemies);
            var encounterDb = BuildBattleDataAssets(enemies);
```

`BuildScene` 호출은 Task 8 에서 다시 손대므로 지금은 시그니처를 그대로 두고
`encounterDb` 를 넘기지 않는다 — 다음 태스크에서 이어붙인다.

- [ ] **Step 6: 에셋이 생기는지 확인한다**

에디터를 닫고:

```bash
Unity.exe -batchmode -quit -projectPath "C:\Users\이지민\UnityProjects\MCPTest" \
  -executeMethod FarmCreature.EditorTools.GameBootstrap.BuildAll -logFile "<tmp>\rebuild.log"
```

확인할 것 — **종료 코드만 믿지 말 것** (함정 3.5):
1. `<tmp>\rebuild.log` 에 `LogError` 가 없다.
2. 다음 파일이 생겼다:
   `Assets/GameData/Battle/FieldMap.asset`, `RatField.asset`, `BoarThicket.asset`,
   `GolemRidge.asset`, `Assets/GameData/BattleEncounterDatabase.asset`
3. `RatField.asset` 을 텍스트로 열어 `count: 3` 과 `enemy: {fileID: ...}` 가 0 이 아닌지 본다.
   `fileID: 0` 이면 참조가 죽은 것이다 (함정 3.9).

- [ ] **Step 7: seed-only 가 실제로 보존되는지 확인한다 ★이 태스크의 핵심**

```bash
# RatField.asset 의 count 를 3 → 5 로 손으로 고친다 (텍스트 편집).
# 그 뒤 BuildAll 을 다시 돌린다.
Unity.exe -batchmode -quit -projectPath "C:\Users\이지민\UnityProjects\MCPTest" \
  -executeMethod FarmCreature.EditorTools.GameBootstrap.BuildAll -logFile "<tmp>\rebuild2.log"
```

기대: `RatField.asset` 의 `count` 가 **5 로 남아 있다.** 3 으로 되돌아갔다면 `LoadOrSeed` 가
`created` 를 잘못 판정하고 있는 것이다.

확인이 끝나면 **5 를 3 으로 되돌린다.**

- [ ] **Step 8: 테스트를 돌린다**

기대: `total="148" passed="148"`.

- [ ] **Step 9: 커밋**

```bash
git add Assets/Scripts/Editor/GameBootstrap.cs Assets/GameData
git commit -m "feat: 전투 맵·인카운터 에셋을 seed-only 로 생성 (인스펙터 편집 보존)"
```

---

## Task 8: 전투 씬 생성

**Files:**
- Modify: `Assets/Scripts/Editor/GameBootstrap.cs`
  - `BuildAll` — 씬 두 개를 순서대로 만들고 둘 다 빌드 설정에 등록
  - `BuildScene` → `BuildFarmScene` 으로 이름 변경, `BuildBattlePanel` 을 인카운터 버튼으로 교체
  - `BuildBattleScene` 신규
  - `ReloadSpriteAssets` 신규

**Interfaces:**
- Consumes: `BattleSceneRoot`·`BattleUnitView`·`BattleField`(Task 5·6), `BattlePanel.StartEncounter`(Task 6),
  `BattleEncounterDatabase`(Task 1), `PanelLabels.EncounterButton`(Task 4)
- Produces: `Assets/Scenes/BattleScene.unity`, 빌드 설정에 두 씬 등록

**★ 이 태스크에서 제일 위험한 것:** `BuildAll` 이 `NewScene` 을 **두 번** 부른다.
두 번째 호출이 그전에 얻어 둔 에셋 참조를 다시 죽인다 (함정 3.9).
그래서 전투 씬을 채우기 전에 **스프라이트와 데이터베이스를 경로에서 다시 로드한다.**
이 재로드를 빠뜨리면 8/13 과 똑같은 사고(버튼 0개 씬 저장)가 난다.

- [ ] **Step 1: `BuildAll` 을 두 씬 구조로 바꾼다**

`BuildAll`(36–73줄)을 다음으로 교체한다:

```csharp
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
            // 그 결과 개수가 데이터에서 나오는 버튼이 0개인 씬이 종료 코드 0 으로 저장된다.
            var farmScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            EnsureFolders();
            EnsureSpriteAssets();

            var species = BuildSpeciesAssets();
            var items = BuildItemAssets();
            var enemies = BuildEnemyAssets();
            var speciesDb = BuildSpeciesDatabase(species);
            var itemDb = BuildItemDatabase(items);
            var enemyDb = BuildEnemyDatabase(enemies);
            var encounterDb = BuildBattleDataAssets(enemies);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BuildFarmScene(farmScene, speciesDb, itemDb, encounterDb, species["sprigling"], items);

            // ★ 두 번째 NewScene 이 위에서 얻은 참조를 '다시' 죽인다.
            // 그래서 전투 씬을 채우기 전에 필요한 것을 경로에서 전부 다시 로드한다.
            // 이 재로드를 빠뜨리면 슬롯도 버튼도 없는 전투 씬이 조용히 저장된다.
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
        /// <see cref="NewScene"/> 이후에 스프라이트 참조를 되살린다.
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
```

`BuildScene`(373–548줄)의 이름을 `BuildFarmScene` 으로 바꾸고, 매개변수 `EnemyDatabase enemyDb` 를
`BattleEncounterDatabase encounterDb` 로 바꾼다. 마지막 두 줄(545–547)에서
`ScenePath` → `FarmScenePath` 로 고치고 `EditorBuildSettings.scenes = ...` 줄은 **삭제한다**
(위의 `BuildAll` 이 두 씬을 함께 등록한다).

- [ ] **Step 2: 농장의 전투터 패널을 인카운터 버튼으로 바꾼다**

`BuildBattlePanel`(786–835줄)을 다음으로 교체한다:

```csharp
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
```

`BuildFarmScene` 안의 호출부(502줄)를 고친다:

```csharp
            var battlePanel = BuildBattlePanel(panelRoot.transform, careManager, encounterDb);
```

- [ ] **Step 3: `BuildBattleScene` 을 만든다**

`BuildFarmScene` 뒤에 추가한다:

```csharp
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
            groundGo.transform.position = new Vector3(0f, 0f, 0f);

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

            var titleText = CreateText(canvasGo.transform, "TitleText",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f),
                new Vector2(1200f, 60f), TextAnchor.MiddleCenter, 34);
            titleText.color = Color.white;

            var logText = CreateText(canvasGo.transform, "BattleLogText",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 40f),
                new Vector2(1100f, 220f), TextAnchor.LowerLeft, 24);
            logText.color = new Color(0.9f, 0.9f, 0.78f);

            var speedText = CreateText(canvasGo.transform, "SpeedText",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-330f, 190f),
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
            var speed1 = CreatePanelButton(canvasGo.transform, "Speed1x", "1x",
                new Vector2(-460f, -420f), new Vector2(140f, 64f), new Color(0.36f, 0.5f, 0.68f));
            UnityEventTools.AddIntPersistentListener(
                speed1.onClick, new UnityAction<int>(root.SetSpeed), 0);

            var speed2 = CreatePanelButton(canvasGo.transform, "Speed2x", "2x",
                new Vector2(-300f, -420f), new Vector2(140f, 64f), new Color(0.36f, 0.5f, 0.68f));
            UnityEventTools.AddIntPersistentListener(
                speed2.onClick, new UnityAction<int>(root.SetSpeed), 1);

            var exitButton = CreatePanelButton(canvasGo.transform, "Exit", "나가기",
                new Vector2(-80f, -420f), new Vector2(200f, 64f), new Color(0.6f, 0.42f, 0.32f));
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
```

- [ ] **Step 4: 재생성하고 생성물을 직접 검사한다**

```bash
Unity.exe -batchmode -quit -projectPath "C:\Users\이지민\UnityProjects\MCPTest" \
  -executeMethod FarmCreature.EditorTools.GameBootstrap.BuildAll -logFile "<tmp>\rebuild.log"
```

**종료 코드만 믿지 말 것** (함정 3.5). 다음을 직접 확인한다:

1. `<tmp>\rebuild.log` 에 `LogError` 가 한 줄도 없다. 특히 "재로드 실패" 와 "인카운터가 0개다".
2. `Assets/Scenes/BattleScene.unity` 가 생겼다.
3. `BattleScene.unity` 를 텍스트로 열어 `m_Sprite: {fileID: 0}` 이 **하나도 없는지** 본다.
   하나라도 있으면 스프라이트 참조가 죽은 것이다 (함정 3.1).
4. `ProjectSettings/EditorBuildSettings.asset` 에 두 씬이 등록됐다.
5. `FarmScene.unity` 에 `Encounter0` · `Encounter1` · `Encounter2` 가 있고 `Enemy0` 은 없다.

- [ ] **Step 5: 테스트를 돌린다**

기대: `total="148" passed="148"`.

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/Editor/GameBootstrap.cs Assets/Scenes Assets/GameData ProjectSettings
git commit -m "feat: 전투 씬을 생성하고 두 씬을 빌드 설정에 등록"
```

---

## Task 9: 씬 무결성 검사를 두 씬으로 확장

**Files:**
- Modify: `Assets/Scripts/Editor/SceneIntegrityCheck.cs` (전면 개편 — `Run` 을 두 검사로 나눈다)

**Interfaces:**
- Consumes: `BattleSceneRoot`·`BattleUnitView`·`BattleField`(Task 5·6), `BattlePanel.encounterDatabase`(Task 6)
- Produces: `SceneIntegrityCheck.Run()` — 두 씬을 각각 열어 검사하고 실패 합계로 종료 코드를 정한다

**설계 5.2 가 요구하는 전투 씬 단언:**
아군 슬롯 4 · 적 슬롯 8 이 존재하고 저장 시점에 꺼져 있는가 / 모든 스프라이트가 에셋인가 /
버튼 3개가 리스너를 갖는가 / 체력바 채움이 슬롯마다 있는가 / 빌드 설정에 두 씬이 등록됐는가.

- [ ] **Step 1: `Run` 을 두 검사로 나눈다**

`Run()`(25–313줄)의 구조를 다음으로 바꾼다. **기존 1–12번 검사 내용은 그대로 옮긴다** —
바뀌는 것은 (a) 감싸는 틀, (b) 8번과 12번의 전투 패널 부분, (c) 전투 씬 검사 추가뿐이다.

```csharp
        private const string FarmScenePath = "Assets/Scenes/FarmScene.unity";
        private const string BattleScenePath = "Assets/Scenes/BattleScene.unity";

        public static void Run()
        {
            var report = new StringBuilder();
            int failures = 0;

            failures += CheckBuildSettings(report);
            failures += CheckFarmScene(report);
            failures += CheckBattleScene(report);

            if (failures == 0)
            {
                report.AppendLine("결과: 통과 — 두 씬의 모든 참조가 유효합니다.");
                Debug.Log("[SceneIntegrityCheck]\n" + report);
                EditorApplication.Exit(0);
            }
            else
            {
                report.AppendLine("결과: 실패 " + failures + "건");
                Debug.LogError("[SceneIntegrityCheck]\n" + report);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// 두 씬이 빌드 설정에 등록됐는가. 빠지면 <c>SceneManager.LoadScene</c> 이
        /// <b>런타임에</b> 실패한다 — 에디터에서는 멀쩡해 보인다 (설계 5.1).
        /// </summary>
        private static int CheckBuildSettings(StringBuilder report)
        {
            int failures = 0;
            var scenes = EditorBuildSettings.scenes;

            failures += RequireSceneRegistered(report, scenes, FarmScenePath);
            failures += RequireSceneRegistered(report, scenes, BattleScenePath);

            report.AppendLine("빌드 설정 씬 " + scenes.Length + "개 검사");
            return failures;
        }

        private static int RequireSceneRegistered(
            StringBuilder report, EditorBuildSettingsScene[] scenes, string path)
        {
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].path == path && scenes[i].enabled)
                {
                    return 0;
                }
            }

            report.AppendLine("  [실패] 빌드 설정에 없거나 꺼져 있음: " + path
                              + " — LoadScene 이 런타임에 실패한다");
            return 1;
        }
```

기존 `Run` 의 본문(30–299줄)은 `CheckFarmScene(StringBuilder report)` 로 옮긴다.
`var scene = EditorSceneManager.OpenScene(ScenePath, ...)` 의 상수만 `FarmScenePath` 로 바꾸고,
끝에 `return failures;` 를 둔다. `EditorApplication.Exit` 호출은 옮기지 않는다.

- [ ] **Step 2: 농장 검사의 전투 패널 부분을 고친다**

8번 검사(198–204줄):

```csharp
            // 8. 전투터 패널의 인카운터 데이터베이스
            var battle = Object.FindFirstObjectByType<BattlePanel>(FindObjectsInactive.Include);
            if (battle != null)
            {
                failures += RequireRef(report, "battlePanel.encounterDatabase", battle.encounterDatabase);
                failures += RequireRef(report, "battlePanel.careManager", battle.careManager);
                failures += RequireRef(report, "battlePanel.titleText", battle.titleText);
                failures += RequireRef(report, "battlePanel.infoText", battle.infoText);
            }
```

12번 검사의 전투 부분(278–291줄):

```csharp
            if (battle != null && battle.encounterDatabase != null)
            {
                int encounters = battle.encounterDatabase.Count;

                if (encounters == 0)
                {
                    report.AppendLine("  [실패] BattleEncounterDatabase 가 비어 있음 — 전투를 시작할 수단이 없다");
                    failures++;
                }

                // 자리는 6칸(3×2)뿐이라 인카운터가 더 많아도 버튼은 6개까지만 만든다.
                int expected = encounters < 6 ? encounters : 6;
                failures += RequireNamedChildren(report, battle.transform, "Encounter", expected, "전투터 인카운터 버튼");
            }
```

- [ ] **Step 3: 전투 씬 검사를 추가한다**

```csharp
        /// <summary>
        /// 전투 씬은 농장과 다른 것을 단언한다 — 슬롯이 <b>존재하고 꺼져 있는가</b>,
        /// 체력바 채움이 슬롯마다 있는가, 배속·나가기 버튼이 배선됐는가 (설계 5.2).
        ///
        /// 슬롯은 씬에 꺼진 채로 저장되므로 <b>FindObjectsInactive.Include 가 필수</b>다 —
        /// 빼면 슬롯이 한 개도 검사되지 않는다.
        /// </summary>
        private static int CheckBattleScene(StringBuilder report)
        {
            int failures = 0;

            var scene = EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Single);
            report.AppendLine("씬 열기: " + scene.path + " (루트 " + scene.rootCount + "개)");

            // 1. 스프라이트 — 몸통·체력바·바닥 전부 에셋이어야 한다
            var renderers = Object.FindObjectsByType<SpriteRenderer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < renderers.Length; i++)
            {
                var sprite = renderers[i].sprite;

                if (sprite == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(sprite)))
                {
                    report.AppendLine("  [실패] 전투 씬: 에셋이 아닌 스프라이트: " + Path(renderers[i].transform));
                    failures++;
                }
            }

            report.AppendLine("전투 씬 SpriteRenderer " + renderers.Length + "개 검사");

            // 2. BattleSceneRoot 배선
            var root = Object.FindFirstObjectByType<BattleSceneRoot>(FindObjectsInactive.Include);
            if (root == null)
            {
                report.AppendLine("  [실패] BattleSceneRoot 없음");
                return failures + 1;
            }

            failures += RequireRef(report, "battleRoot.ground", root.ground);
            failures += RequireRef(report, "battleRoot.defaultMap", root.defaultMap);
            failures += RequireRef(report, "battleRoot.titleText", root.titleText);
            failures += RequireRef(report, "battleRoot.logText", root.logText);
            failures += RequireRef(report, "battleRoot.speedText", root.speedText);
            failures += RequireRef(report, "battleRoot.practiceEncounter", root.practiceEncounter);
            failures += RequireRef(report, "battleRoot.practiceSpecies", root.practiceSpecies);

            // 3. 슬롯 개수 — 데이터가 아니라 상한으로 고정된다
            failures += RequireSlots(report, root.allySlots, Roster.MaxParty, "아군 슬롯");
            failures += RequireSlots(report, root.enemySlots, BattleField.MaxEnemies, "적 슬롯");

            // 4. 버튼 3개(1x · 2x · 나가기)가 리스너를 갖는가
            var buttons = Object.FindObjectsByType<UnityEngine.UI.Button>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (buttons.Length != 3)
            {
                report.AppendLine("  [실패] 전투 씬 버튼이 3개가 아님: " + buttons.Length
                                  + " (1x · 2x · 나가기)");
                failures++;
            }

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].onClick.GetPersistentEventCount() == 0
                    || string.IsNullOrEmpty(buttons[i].onClick.GetPersistentMethodName(0))
                    || buttons[i].onClick.GetPersistentTarget(0) == null)
                {
                    report.AppendLine("  [실패] 전투 씬: 리스너 없는 버튼: " + Path(buttons[i].transform));
                    failures++;
                }
            }

            // 5. 카메라
            var camera = Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
            if (camera == null)
            {
                report.AppendLine("  [실패] 전투 씬에 카메라가 없음");
                failures++;
            }

            return failures;
        }

        /// <summary>
        /// 슬롯이 <paramref name="expected"/> 개 있고, 참조가 살아 있고, 체력바 채움이 있고,
        /// <b>저장 시점에 꺼져 있는지</b> 본다. 켜진 채로 저장되면 전투 시작 전부터 유령이 서 있다.
        /// </summary>
        private static int RequireSlots(
            StringBuilder report, System.Collections.Generic.List<BattleUnitView> slots,
            int expected, string label)
        {
            if (slots == null || slots.Count != expected)
            {
                report.AppendLine("  [실패] " + label + " 이 " + expected + "개가 아님: "
                                  + (slots == null ? 0 : slots.Count));
                return 1;
            }

            int failures = 0;

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];

                if (slot == null)
                {
                    report.AppendLine("  [실패] " + label + " " + i + " 참조 끊김");
                    failures++;
                    continue;
                }

                if (slot.body == null || slot.healthBarFill == null
                    || slot.healthBarBack == null || slot.nameLabel == null)
                {
                    report.AppendLine("  [실패] " + label + " " + i + " 의 조각이 빠졌다 "
                                      + "(body/healthBarBack/healthBarFill/nameLabel)");
                    failures++;
                }

                if (slot.gameObject.activeSelf)
                {
                    report.AppendLine("  [실패] " + label + " " + i + " 이(가) 켜진 채로 저장됨");
                    failures++;
                }
            }

            report.AppendLine(label + " " + slots.Count + "개 확인");
            return failures;
        }
```

`using FarmCreature.Presentation;` 가 이미 3줄에 있고 `FarmCreature.Core` 도 2줄에 있다.

- [ ] **Step 4: 검사를 돌린다**

```bash
Unity.exe -batchmode -projectPath "C:\Users\이지민\UnityProjects\MCPTest" \
  -executeMethod FarmCreature.EditorTools.SceneIntegrityCheck.Run -logFile "<tmp>\integrity.log"
```

기대: **종료 코드 0**. `<tmp>\integrity.log` 에서 다음 줄을 눈으로 확인한다:
- `아군 슬롯 4개 확인`
- `적 슬롯 8개 확인`
- `전투터 인카운터 버튼 3개 확인`
- `빌드 설정 씬 2개 검사`
- `결과: 통과`

실패하면 **여기서 멈춘다.** 이 검사가 잡으라고 만든 것이 바로 지금 상황이다.

- [ ] **Step 5: 검사가 실제로 실패를 잡는지 확인한다 ★**

검사가 통과하는 것만으로는 검사가 일하는지 알 수 없다. 일부러 깨뜨려 본다:

```
GameBootstrap.BuildBattleScene 의 마지막 슬롯 끄기 루프에서
enemySlots 쪽 SetActive(false) 를 잠시 주석 처리 → BuildAll → 무결성 검사
```

기대: **종료 코드 1**, 로그에 `적 슬롯 0 이(가) 켜진 채로 저장됨` 8줄.
확인했으면 주석을 되돌리고 `BuildAll` 을 다시 돌려 통과 상태로 만든다.

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/Editor/SceneIntegrityCheck.cs
git commit -m "test: 씬 무결성 검사를 농장·전투 두 씬으로 확장"
```

---

## Task 10: 전량 재생성 · 검증 · 문서 갱신

**Files:**
- Modify: `docs/HANDOFF.md`
- Modify: `docs/superpowers/specs/2026-08-14-battle-screen-design.md` (상태 줄만)

- [ ] **Step 1: 깨끗한 상태에서 전량 재생성한다**

에디터를 닫고 순서대로:

```bash
Unity.exe -batchmode -quit -projectPath "C:\Users\이지민\UnityProjects\MCPTest" \
  -executeMethod FarmCreature.EditorTools.GameBootstrap.BuildAll -logFile "<tmp>\final-rebuild.log"

Unity.exe -batchmode -runTests -projectPath "C:\Users\이지민\UnityProjects\MCPTest" \
  -testPlatform EditMode -testResults "<tmp>\final-results.xml" -logFile "<tmp>\final-test.log"

Unity.exe -batchmode -projectPath "C:\Users\이지민\UnityProjects\MCPTest" \
  -executeMethod FarmCreature.EditorTools.SceneIntegrityCheck.Run -logFile "<tmp>\final-integrity.log"
```

**증거를 눈으로 확인하고 적는다** (`superpowers:verification-before-completion`):
- `final-rebuild.log` — `LogError` 0건
- `final-results.xml` — `total="148" passed="148" failed="0"`
- `final-integrity.log` — `결과: 통과`, 종료 코드 0

셋 중 하나라도 어긋나면 **다음 단계로 가지 않는다.**

- [ ] **Step 2: 실제로 Play 해서 전투를 본다**

`docs/HANDOFF.md` 8절에 추가할 항목들. **직접 눌러 확인하고 결과를 적는다:**

- [ ] 전투터에서 인카운터를 고르면 전투 씬으로 넘어간다
- [ ] 아군이 왼쪽, 적이 오른쪽에 **겹치지 않게** 서고, 매 전투 배치가 달라진다
- [ ] 체력바가 실제로 줄어들고, 쓰러진 유닛이 반투명으로 남는다
- [ ] 유닛 이름이 한글로 보인다 (안 보이면 농장의 건물 이름표도 같은 상태인지 확인 — `TextMesh` 폰트 문제다)
- [ ] 2x 를 누르면 눈에 띄게 빨라지고, 1x 로 돌아온다
- [ ] 승리 후 [나가기] → 농장으로 돌아오고 **재화가 한 번만** 들어온다
- [ ] 전투 중에 [나가기] → 보상 없이 돌아오고 "물러났다" 로그가 뜬다
- [ ] **전투를 다녀와도 저장 전 진행(먹이·훈련·장착 결과)이 남아 있다** ★ 제일 중요
- [ ] 파티가 빈 채로 인카운터를 누르면 씬이 바뀌지 않고 안내만 뜬다
- [ ] `BattleScene` 을 단독으로 Play 하면 연습 전투가 시작되고 로그로 알린다

> 에디터 창이 비활성이면 플레이어 루프가 멈춘다. `BattleSceneRoot.Start` 가
> `Application.runInBackground = true` 를 켜므로 전투 씬에서는 이 문제가 없어야 한다.
> 농장 씬은 여전히 영향을 받는다.

- [ ] **Step 3: HANDOFF 를 갱신한다**

`docs/HANDOFF.md` 에서 고칠 곳:

1. **1절 표** — 테스트 148개, 씬 2개, 버튼 수 갱신
2. **1절 건물 표** — 상점 줄을 "전투터 패널이 임시로 붙어 있다 → **인카운터를 골라 전투 씬으로 간다**"
3. **2절에 2.4 추가** — 전투 화면: 별도 씬, 관전+배속, 표시 전용 좌표, `BattleHandoff` 왕복,
   재화는 `GameRoot` 한 곳에서만 지급
4. **3절에 함정 3.10 추가**:

   > ### 3.10 `BuildAll` 이 `NewScene` 을 두 번 부른다
   > 씬이 둘이 되면서 `NewScene` 도 두 번 불린다. **두 번째 호출이 첫 번째 이후에 얻은
   > 에셋 참조를 다시 죽인다** (3.9와 같은 원리). 그래서 전투 씬을 채우기 전에
   > 스프라이트와 데이터 에셋을 `ReloadSpriteAssets` / `LoadAssetAtPath` 로 다시 로드한다.
   > 이 재로드를 빠뜨리면 슬롯도 버튼도 없는 전투 씬이 종료 코드 0 으로 저장된다.

5. **3절에 함정 3.11 추가**:

   > ### 3.11 전투 데이터는 seed-only 다 — 코드로 초기값을 바꿔도 반영되지 않는다
   > `EnemyData` · `BattleMapData` · `BattleEncounter` · `BattleEncounterDatabase` 는
   > `LoadOrSeed` 로 만든다. **이미 있으면 손대지 않는다.** 인스펙터 편집을 보존하기 위한 것이지만
   > (설계 2.2), 반대로 `GameBootstrap` 의 초기값을 고쳐도 기존 에셋에는 안 들어간다.
   > 다시 깔려면 해당 `.asset` 을 지우고 `BuildAll` 을 돌린다.
   > 종·아이템·씬은 여전히 코드가 전권을 쥔다.

6. **4.1 을 고친다** — 전투가 별도 씬이 됐지만 **진입점은 여전히 상점 건물이다.** 미결 그대로.
7. **5절 명령** — 무결성 검사가 두 씬을 본다고 적는다
8. **5.1 테스트 표** — `BattleDataTests` 8 / `SpawnPointPickerTests` 7 / `BattleHandoffTests` 7 추가,
   `PanelLabelsTests` 17 → 21, 합계 148
9. **6절 파일 구조** — 새 파일 8개와 `Assets/GameData/Battle/`, `Assets/Scenes/BattleScene.unity` 추가
10. **7절 다음 할 것** — 0순위였던 전투 화면을 ✅ 로 바꾸고, **상점을 0순위로** 올린다.
    선행 조건은 여전히 4.1(전투 진입점)이다.
11. **8절** — Step 2 에서 확인한 항목을 결과와 함께 옮긴다

- [ ] **Step 4: 설계 문서의 상태 줄을 고친다**

`docs/superpowers/specs/2026-08-14-battle-screen-design.md` 3줄:

```markdown
**상태:** 구현 완료 (2026-08-14). 구현 계획은 `docs/superpowers/plans/2026-08-14-battle-screen.md`.
```

8.2 (`MaxEnemies` 값)에 한 줄 덧붙인다: 첫 인카운터는 최대 3마리라 8은 아직 여유가 있다.

- [ ] **Step 5: 마지막 커밋**

```bash
git add docs Assets
git commit -m "docs: 전투 화면 구현 반영해 HANDOFF·설계 문서 갱신"
```

---

## 자체 점검 (계획 작성자가 이미 수행함)

**설계 문서 각 절이 태스크에 매핑되는가:**

| 설계 | 태스크 |
|---|---|
| 1. 확정된 결정 (별도 씬·관전·탑뷰·랜덤 스폰·표시 전용) | 전역 제약 1, Task 5·6 |
| 2.1 새 ScriptableObject 3종 | Task 1 |
| 2.2 seed-only 편집 보존 | Task 7 |
| 3. 씬 전환과 상태 전달 (`BattleHandoff`, 단독 Play 폴백, 재화는 농장에서) | Task 3·6 |
| 4.1 화면 구성 (유닛·체력바·쓰러진 유닛) | Task 5·8 |
| 4.2 배속과 종료 | Task 6·8 |
| 4.3 스폰 (겹치지 않게, 슬롯 미리 생성, 초과 시 실패) | Task 2·7·8 |
| 5.1 `GameBootstrap` (두 씬, 재로드, 빌드 설정) | Task 8 |
| 5.2 `SceneIntegrityCheck` (두 씬 검사) | Task 9 |
| 6. 테스트 (`PickSpawnPoints`·`BattleHandoff`·총원 계산) | Task 1·2·3 |
| 7. 범위 밖 | 건드리지 않음 |
| 8.1 전투 진입점 미결 | **그대로 미결** — 상점 건물이 계속 연다. Task 10 에서 HANDOFF 에 남긴다 |
| 8.2 `MaxEnemies` = 8 | Task 5 (`BattleField.MaxEnemies`) |
| 8.3 배속 1x/2x | Task 6 |
| 8.4 맵 하나(들판) | Task 7 |

**설계에서 벗어난 두 곳 (의도적):**
1. 체력바를 uGUI `Image` 가 아니라 `SpriteRenderer` 로 만든다 — Task 5 에 근거를 적었다.
2. `BattleResult` 가 `GameState` 를 함께 나른다 — 설계에 없던 것이다. 없으면 전투를 다녀올 때마다
   저장 전 진행이 날아간다. Task 3 에 근거를 적었다.

**테스트 개수 기준선의 정정:** 계획 앞머리의 표는 145 로 어림했지만, 실제로 쓴 테스트는
`SpawnPointPickerTests` 7 · `BattleHandoffTests` 7 · `BattleDataTests` 8 · `PanelLabels` +4−1 이라
**최종 148개**다. 각 태스크의 기대값은 148 기준으로 적혀 있다.
