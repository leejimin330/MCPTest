# 농장 육성 게임 MVP 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 농장에서 판타지 생물 한 마리를 돌보며 육성 방식에 따라 두 갈래로 분기 진화시키는 2D Unity 게임의 MVP를 완성한다.

**Architecture:** ScriptableObject를 정적 데이터(종/아이템/진화 조건)로, 순수 C# 클래스를 런타임 상태(스탯/경험치/포만도/친밀도/인벤토리)로 분리한다. 게임 규칙(진화 판정·육성 액션·세이브 직렬화)은 전부 순수 C#으로 작성해 EditMode 테스트로 검증하고, MonoBehaviour 계층은 그 위에 얇게 얹는다. 씬과 데이터 에셋은 손으로 만들지 않고 에디터 스크립트가 전량 생성한다.

**Tech Stack:** Unity 6000.2.8f1 · C# · Unity Test Framework (NUnit, EditMode) · uGUI (레거시 `UnityEngine.UI.Text`) · `JsonUtility`

**참고 문서:** [PRD](../../PRD.md) · [설계 문서](../specs/2026-08-06-creature-raising-game-design.md)

---

## Global Constraints

프로젝트 전역 규칙. **모든 태스크의 요구사항에 암묵적으로 포함된다.**

- **Unity 버전:** `6000.2.8f1`. 에디터 실행 파일 경로는
  `C:\Program Files\Unity\Hub\Editor\6000.2.8f1\Editor\Unity.exe`
- **프로젝트 경로:** `C:\Users\이지민\UnityProjects\MCPTest`
- **네임스페이스:** `FarmCreature` / `FarmCreature.Data` / `FarmCreature.Core` /
  `FarmCreature.Presentation` / `FarmCreature.EditorTools` / `FarmCreature.Tests`
- **Core 계층 규칙:** `FarmCreature.Core` 의 클래스는 `MonoBehaviour`, `Debug`, `Time`,
  `UnityEngine.Random`, `Application` 을 **사용하지 않는다.** `ScriptableObject` 파생 데이터
  타입의 필드를 읽는 것과 `JsonUtility` 사용은 허용한다.
- **ScriptableObject 불변:** 런타임 코드는 `CreatureSpeciesData` / `ItemData` /
  `SpeciesDatabase` / `ItemDatabase` 의 필드에 **절대 쓰지 않는다.** 읽기 전용으로만 쓴다.
- **문자열:** UI에 보이는 모든 문자열은 한국어. 파일은 **UTF-8** 로 저장한다.
- **텍스트 UI:** TextMeshPro를 쓰지 않는다. `UnityEngine.UI.Text` +
  `Font.CreateDynamicFontFromOSFont("맑은 고딕", 16)` 만 쓴다.
- **입력:** 레거시 Input Manager (`Input.GetAxisRaw`). 프로젝트 설정
  `activeInputHandler: 0` 을 바꾸지 않는다.
- **맵 경계:** 농장 구역 반너비 `X = 7.0`, 반높이 `Y = 3.5` (월드 단위). 카메라는
  Orthographic size `5`.
- **수치 상수:** PRD 6.1/6.2 의 값을 그대로 쓴다. 임의로 밸런싱하지 않는다.
- **테스트 실행 명령** (에디터가 닫혀 있어야 한다):

  ```bash
  "C:/Program Files/Unity/Hub/Editor/6000.2.8f1/Editor/Unity.exe" \
    -batchmode -runTests \
    -projectPath "C:/Users/이지민/UnityProjects/MCPTest" \
    -testPlatform EditMode \
    -testResults "C:/Users/이지민/AppData/Local/Temp/claude/results.xml" \
    -logFile "C:/Users/이지민/AppData/Local/Temp/claude/unity-test.log"
  ```

  종료 코드 `0` = 전체 통과, `2` = 테스트 실패, 그 외 = 실행 오류.
  결과는 `results.xml` 의 `<test-run ... total= passed= failed=>` 속성으로 확인한다.
- **커밋:** 이 프로젝트에는 git 저장소가 없다. 각 태스크의 마지막 단계는 커밋 대신
  **검증 체크포인트**다. 사용자가 `git init` 을 승인하면 각 체크포인트를 커밋으로 바꾼다.

---

## File Structure

각 파일의 책임. 이 분해가 태스크 경계를 결정한다.

| 파일 | 책임 |
|---|---|
| `Assets/Scripts/Runtime/FarmCreature.Runtime.asmdef` | 런타임 어셈블리 정의 |
| `Assets/Scripts/Runtime/Data/StatType.cs` | 스탯 종류 열거형 |
| `Assets/Scripts/Runtime/Data/StatBlock.cs` | 스탯 3종 묶음 (직렬화용) |
| `Assets/Scripts/Runtime/Data/EvolutionRequirement.cs` | 진화 요구사항 1건 + 생성 헬퍼 |
| `Assets/Scripts/Runtime/Data/EvolutionCondition.cs` | 요구사항 AND 묶음 + 결과 종 |
| `Assets/Scripts/Runtime/Data/CreatureSpeciesData.cs` | 종 정적 데이터 SO |
| `Assets/Scripts/Runtime/Data/ItemData.cs` | 아이템 정적 데이터 SO |
| `Assets/Scripts/Runtime/Data/SpeciesDatabase.cs` | 종 ID → 종 역참조 SO |
| `Assets/Scripts/Runtime/Data/ItemDatabase.cs` | 아이템 ID → 아이템 역참조 SO |
| `Assets/Scripts/Runtime/Core/CreatureInstance.cs` | 케어의 **변하는** 상태 전부 |
| `Assets/Scripts/Runtime/Core/Inventory.cs` | 아이템 ID → 개수 |
| `Assets/Scripts/Runtime/Core/ActionResult.cs` | 액션 성공 여부 + 메시지 |
| `Assets/Scripts/Runtime/Core/CareActions.cs` | 먹이/훈련/아이템 액션 규칙 |
| `Assets/Scripts/Runtime/Core/EvolutionEvaluator.cs` | 진화 조건 판정 (교체는 안 함) |
| `Assets/Scripts/Runtime/Core/SaveData.cs` | 세이브 JSON 스키마 |
| `Assets/Scripts/Runtime/Core/SaveSerializer.cs` | 상태 ↔ SaveData ↔ JSON (파일 IO 없음) |
| `Assets/Scripts/Runtime/Presentation/FarmArea.cs` | 맵 경계 상수 + 클램프 |
| `Assets/Scripts/Runtime/Presentation/PlaceholderSprites.cs` | 코드 생성 사각형/원 스프라이트 |
| `Assets/Scripts/Runtime/Presentation/CreatureController.cs` | 케어 비주얼 + 배회 이동 |
| `Assets/Scripts/Runtime/Presentation/PlayerController.cs` | 플레이어 이동 |
| `Assets/Scripts/Runtime/Presentation/GameHud.cs` | 상태 텍스트 + 로그 갱신 |
| `Assets/Scripts/Runtime/Presentation/CareManager.cs` | 액션 조율 + 진화 실행 |
| `Assets/Scripts/Runtime/Presentation/SaveSystem.cs` | 세이브 파일 읽기/쓰기 |
| `Assets/Scripts/Runtime/Presentation/GameRoot.cs` | 시작 시 로드/조립 |
| `Assets/Scripts/Editor/FarmCreature.Editor.asmdef` | 에디터 어셈블리 정의 |
| `Assets/Scripts/Editor/GameBootstrap.cs` | SO 에셋 + 씬 전량 생성 |
| `Assets/Scripts/Tests/EditMode/FarmCreature.Tests.EditMode.asmdef` | 테스트 어셈블리 정의 |
| `Assets/Scripts/Tests/EditMode/TestFixtures.cs` | 테스트용 종/아이템 SO 생성 헬퍼 |
| `Assets/Scripts/Tests/EditMode/CreatureInstanceTests.cs` | 상태값 클램프/누적 |
| `Assets/Scripts/Tests/EditMode/CareActionsTests.cs` | 액션 효과 + 실패 시 무변경 |
| `Assets/Scripts/Tests/EditMode/EvolutionEvaluatorTests.cs` | 조건 판정/우선순위 |
| `Assets/Scripts/Tests/EditMode/SaveSerializerTests.cs` | 라운드트립 + 손상 세이브 거부 |

---

## Task 1: 패키지 · 어셈블리 · 테스트 파이프라인

먼저 "테스트가 실제로 돌아가는 것"부터 확인한다. 이게 안 되면 이후 전부가 검증 불가다.

**Files:**
- Modify: `Packages/manifest.json`
- Create: `Assets/Scripts/Runtime/FarmCreature.Runtime.asmdef`
- Create: `Assets/Scripts/Runtime/Data/StatType.cs`
- Create: `Assets/Scripts/Tests/EditMode/FarmCreature.Tests.EditMode.asmdef`
- Create: `Assets/Scripts/Tests/EditMode/PipelineSmokeTests.cs`

**Interfaces:**
- Consumes: 없음 (최초 태스크)
- Produces: `FarmCreature.Data.StatType` — `Attack` / `Defense` / `Health` 값을 가진 public enum.
  어셈블리 이름 `FarmCreature.Runtime` (테스트·에디터 어셈블리가 참조할 이름)

- [x] **Step 1: Unity 에디터가 닫혀 있는지 확인**

```bash
powershell -Command "Get-Process Unity -ErrorAction SilentlyContinue"
```

출력이 비어 있어야 한다. Unity가 떠 있으면 프로젝트가 잠겨 배치모드 실행이 실패한다.
떠 있다면 사용자에게 에디터를 닫아달라고 요청한다.

- [x] **Step 2: manifest.json 에 두 패키지 추가**

`Packages/manifest.json` 의 `"dependencies"` 첫 줄 `"com.unity.ai.assistant"` **앞에** 두 줄을 넣는다
(JSON 키 순서는 무관하지만 알파벳 순 유지):

```json
    "com.unity.test-framework": "1.6.0",
    "com.unity.ugui": "2.0.0",
```

두 패키지 모두 에디터 설치본
(`C:\Program Files\Unity\Hub\Editor\6000.2.8f1\Editor\Data\Resources\PackageManager\BuiltInPackages`)
에 내장돼 있어 네트워크 없이 해결된다.

- [x] **Step 3: 런타임 asmdef 작성**

`Assets/Scripts/Runtime/FarmCreature.Runtime.asmdef`:

```json
{
    "name": "FarmCreature.Runtime",
    "rootNamespace": "FarmCreature",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [x] **Step 4: 최소 런타임 타입 작성**

`Assets/Scripts/Runtime/Data/StatType.cs`:

```csharp
namespace FarmCreature.Data
{
    /// <summary>전투형 스탯 종류.</summary>
    public enum StatType
    {
        Attack,
        Defense,
        Health
    }
}
```

- [x] **Step 5: 테스트 asmdef 작성**

`Assets/Scripts/Tests/EditMode/FarmCreature.Tests.EditMode.asmdef`:

```json
{
    "name": "FarmCreature.Tests.EditMode",
    "rootNamespace": "FarmCreature.Tests",
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "FarmCreature.Runtime"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [x] **Step 6: 스모크 테스트 작성**

`Assets/Scripts/Tests/EditMode/PipelineSmokeTests.cs`:

```csharp
using FarmCreature.Data;
using NUnit.Framework;

namespace FarmCreature.Tests
{
    public class PipelineSmokeTests
    {
        [Test]
        public void StatType_HasThreeCombatStats()
        {
            Assert.AreEqual(3, System.Enum.GetValues(typeof(StatType)).Length);
        }

        [Test]
        public void StatType_AttackIsFirst()
        {
            Assert.AreEqual(0, (int)StatType.Attack);
        }
    }
}
```

- [x] **Step 7: 테스트 실행 — 통과 확인**

Global Constraints 의 테스트 실행 명령을 그대로 돌린다.
Expected: 종료 코드 `0`, `results.xml` 에 `total="2" passed="2" failed="0"`.

첫 실행은 패키지 임포트 + 스크립트 컴파일 때문에 **3~8분** 걸릴 수 있다. 타임아웃을
600000ms 로 준다. 실패하면 `unity-test.log` 에서 `error CS` 또는 `Compilation failed` 를 찾는다.

- [x] **Step 8: 체크포인트**

확인 사항:
- `results.xml` 에 `passed="2" failed="0"`
- `Packages/packages-lock.json` 에 `com.unity.test-framework` 와 `com.unity.ugui` 항목이 생겼다
- `Library/ScriptAssemblies/FarmCreature.Runtime.dll` 이 존재한다

---

## Task 2: 종 · 아이템 데이터 계층 (ScriptableObject)

**Files:**
- Create: `Assets/Scripts/Runtime/Data/StatBlock.cs`
- Create: `Assets/Scripts/Runtime/Data/EvolutionRequirement.cs`
- Create: `Assets/Scripts/Runtime/Data/EvolutionCondition.cs`
- Create: `Assets/Scripts/Runtime/Data/CreatureSpeciesData.cs`
- Create: `Assets/Scripts/Runtime/Data/ItemData.cs`
- Create: `Assets/Scripts/Runtime/Data/SpeciesDatabase.cs`
- Create: `Assets/Scripts/Runtime/Data/ItemDatabase.cs`
- Create: `Assets/Scripts/Tests/EditMode/TestFixtures.cs`
- Create: `Assets/Scripts/Tests/EditMode/DataLayerTests.cs`

**Interfaces:**
- Consumes: `FarmCreature.Data.StatType` (Task 1)
- Produces:
  - `StatBlock` — public 필드 `attack` / `defense` / `health` (int), 메서드 `int Get(StatType)`
  - `RequirementKind` enum — `ExpAtLeast` / `StatAtLeast` / `RequiresItem`
  - `EvolutionRequirement` — 필드 `kind`, `value`, `stat`, `itemId` +
    정적 팩토리 `ExpAtLeast(int)`, `StatAtLeast(StatType,int)`, `RequiresItem(string)`
  - `EvolutionCondition` — 필드 `result` (`CreatureSpeciesData`), `requirements` (`List<EvolutionRequirement>`)
  - `CreatureSpeciesData : ScriptableObject` — 필드 `speciesId`, `displayName`, `baseStats`,
    `placeholderColor`, `placeholderScale`, `evolutions`
  - `ItemData : ScriptableObject` — 필드 `itemId`, `displayName`, `targetStat`, `amount`
  - `SpeciesDatabase : ScriptableObject` — 필드 `species`, 메서드 `CreatureSpeciesData Find(string)`
  - `ItemDatabase : ScriptableObject` — 필드 `items`, 메서드 `ItemData Find(string)`
  - `TestFixtures` (테스트 전용) — `MakeSpecies(...)`, `MakeItem(...)`, `MakeSpeciesDatabase(...)`,
    `MakeItemDatabase(...)`

- [x] **Step 1: 실패하는 테스트 작성 — 데이터베이스 조회**

먼저 테스트 헬퍼를 만든다. `Assets/Scripts/Tests/EditMode/TestFixtures.cs`:

```csharp
using System.Collections.Generic;
using FarmCreature.Data;
using UnityEngine;

namespace FarmCreature.Tests
{
    /// <summary>테스트용 ScriptableObject 인스턴스를 메모리에만 만든다 (에셋 파일 없음).</summary>
    public static class TestFixtures
    {
        public static CreatureSpeciesData MakeSpecies(
            string speciesId,
            string displayName = null,
            int attack = 5,
            int defense = 5,
            int health = 20)
        {
            var so = ScriptableObject.CreateInstance<CreatureSpeciesData>();
            so.speciesId = speciesId;
            so.displayName = displayName ?? speciesId;
            so.baseStats = new StatBlock { attack = attack, defense = defense, health = health };
            so.placeholderColor = Color.green;
            so.placeholderScale = 1f;
            so.evolutions = new List<EvolutionCondition>();
            return so;
        }

        public static ItemData MakeItem(string itemId, StatType stat, int amount, string displayName = null)
        {
            var so = ScriptableObject.CreateInstance<ItemData>();
            so.itemId = itemId;
            so.displayName = displayName ?? itemId;
            so.targetStat = stat;
            so.amount = amount;
            return so;
        }

        public static SpeciesDatabase MakeSpeciesDatabase(params CreatureSpeciesData[] entries)
        {
            var so = ScriptableObject.CreateInstance<SpeciesDatabase>();
            so.species = new List<CreatureSpeciesData>(entries);
            return so;
        }

        public static ItemDatabase MakeItemDatabase(params ItemData[] entries)
        {
            var so = ScriptableObject.CreateInstance<ItemDatabase>();
            so.items = new List<ItemData>(entries);
            return so;
        }
    }
}
```

그리고 `Assets/Scripts/Tests/EditMode/DataLayerTests.cs`:

```csharp
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
            var item = EvolutionRequirement.RequiresItem("bloom_stone");

            Assert.AreEqual(RequirementKind.ExpAtLeast, exp.kind);
            Assert.AreEqual(100, exp.value);

            Assert.AreEqual(RequirementKind.StatAtLeast, stat.kind);
            Assert.AreEqual(StatType.Attack, stat.stat);
            Assert.AreEqual(15, stat.value);

            Assert.AreEqual(RequirementKind.RequiresItem, item.kind);
            Assert.AreEqual("bloom_stone", item.itemId);
        }
    }
}
```

- [x] **Step 2: 테스트 실행 — 컴파일 실패 확인**

Expected: 종료 코드 0이 아님. `unity-test.log` 에 `error CS0246: The type or namespace name
'StatBlock' could not be found` 류의 컴파일 에러가 뜬다.

- [x] **Step 3: `StatBlock` 구현**

`Assets/Scripts/Runtime/Data/StatBlock.cs`:

```csharp
using System;

namespace FarmCreature.Data
{
    /// <summary>전투형 스탯 3종 묶음. 종의 기본치와 세이브 스냅샷에 쓰인다.</summary>
    [Serializable]
    public class StatBlock
    {
        public int attack = 5;
        public int defense = 5;
        public int health = 20;

        public int Get(StatType stat)
        {
            switch (stat)
            {
                case StatType.Attack: return attack;
                case StatType.Defense: return defense;
                case StatType.Health: return health;
                default: throw new ArgumentOutOfRangeException(nameof(stat), stat, null);
            }
        }
    }
}
```

- [x] **Step 4: `EvolutionRequirement` 구현**

`Assets/Scripts/Runtime/Data/EvolutionRequirement.cs`:

```csharp
using System;

namespace FarmCreature.Data
{
    public enum RequirementKind
    {
        ExpAtLeast,
        StatAtLeast,
        RequiresItem
    }

    /// <summary>진화 요구사항 1건. 여러 개가 AND 로 묶여 하나의 조건을 이룬다.</summary>
    [Serializable]
    public class EvolutionRequirement
    {
        public RequirementKind kind;

        /// <summary>ExpAtLeast / StatAtLeast 의 임계치.</summary>
        public int value;

        /// <summary>StatAtLeast 일 때만 의미가 있다.</summary>
        public StatType stat;

        /// <summary>RequiresItem 일 때만 의미가 있다.</summary>
        public string itemId;

        public static EvolutionRequirement ExpAtLeast(int value)
        {
            return new EvolutionRequirement { kind = RequirementKind.ExpAtLeast, value = value };
        }

        public static EvolutionRequirement StatAtLeast(StatType stat, int value)
        {
            return new EvolutionRequirement
            {
                kind = RequirementKind.StatAtLeast,
                stat = stat,
                value = value
            };
        }

        public static EvolutionRequirement RequiresItem(string itemId)
        {
            return new EvolutionRequirement { kind = RequirementKind.RequiresItem, itemId = itemId };
        }
    }
}
```

- [x] **Step 5: `EvolutionCondition` 구현**

`Assets/Scripts/Runtime/Data/EvolutionCondition.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace FarmCreature.Data
{
    /// <summary>요구사항 전부(AND)를 만족하면 <see cref="result"/> 종으로 진화한다.</summary>
    [Serializable]
    public class EvolutionCondition
    {
        public CreatureSpeciesData result;
        public List<EvolutionRequirement> requirements = new List<EvolutionRequirement>();
    }
}
```

- [x] **Step 6: `CreatureSpeciesData` 구현**

`Assets/Scripts/Runtime/Data/CreatureSpeciesData.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace FarmCreature.Data
{
    /// <summary>
    /// 종/진화 단계별 정적 데이터. 런타임에 <b>절대 수정하지 않는다</b> —
    /// 변하는 값은 전부 <see cref="FarmCreature.Core.CreatureInstance"/> 에 있다.
    /// </summary>
    [CreateAssetMenu(fileName = "Species", menuName = "FarmCreature/Creature Species")]
    public class CreatureSpeciesData : ScriptableObject
    {
        [Tooltip("세이브에 기록되는 안정적인 식별자. 한 번 정하면 바꾸지 않는다.")]
        public string speciesId;

        public string displayName;

        public StatBlock baseStats = new StatBlock();

        [Header("플레이스홀더 외형")]
        public Color placeholderColor = Color.green;
        public float placeholderScale = 1f;

        [Header("진화")]
        [Tooltip("목록 순서가 곧 우선순위. 동시 충족 시 위쪽이 적용된다.")]
        public List<EvolutionCondition> evolutions = new List<EvolutionCondition>();
    }
}
```

- [x] **Step 7: `ItemData` 구현**

`Assets/Scripts/Runtime/Data/ItemData.cs`:

```csharp
using UnityEngine;

namespace FarmCreature.Data
{
    /// <summary>아이템 정적 데이터. 사용 시 <see cref="targetStat"/> 에 <see cref="amount"/> 를 더한다.</summary>
    [CreateAssetMenu(fileName = "Item", menuName = "FarmCreature/Item")]
    public class ItemData : ScriptableObject
    {
        [Tooltip("세이브와 진화 조건에서 참조하는 안정적인 식별자.")]
        public string itemId;

        public string displayName;

        public StatType targetStat = StatType.Attack;
        public int amount = 1;
    }
}
```

- [x] **Step 8: 두 데이터베이스 구현**

`Assets/Scripts/Runtime/Data/SpeciesDatabase.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace FarmCreature.Data
{
    /// <summary>세이브 로드 시 speciesId 로 종을 되찾기 위한 목록.</summary>
    [CreateAssetMenu(fileName = "SpeciesDatabase", menuName = "FarmCreature/Species Database")]
    public class SpeciesDatabase : ScriptableObject
    {
        public List<CreatureSpeciesData> species = new List<CreatureSpeciesData>();

        public CreatureSpeciesData Find(string speciesId)
        {
            if (string.IsNullOrEmpty(speciesId) || species == null)
            {
                return null;
            }

            for (int i = 0; i < species.Count; i++)
            {
                if (species[i] != null && species[i].speciesId == speciesId)
                {
                    return species[i];
                }
            }

            return null;
        }
    }
}
```

`Assets/Scripts/Runtime/Data/ItemDatabase.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace FarmCreature.Data
{
    /// <summary>세이브 로드와 UI 나열을 위한 아이템 목록.</summary>
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "FarmCreature/Item Database")]
    public class ItemDatabase : ScriptableObject
    {
        public List<ItemData> items = new List<ItemData>();

        public ItemData Find(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || items == null)
            {
                return null;
            }

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && items[i].itemId == itemId)
                {
                    return items[i];
                }
            }

            return null;
        }
    }
}
```

- [x] **Step 9: 테스트 실행 — 통과 확인**

Expected: 종료 코드 `0`, `total="7" passed="7" failed="0"` (스모크 2 + 데이터 5).

- [x] **Step 10: 체크포인트**

Data 계층 7개 파일이 컴파일되고 조회 테스트가 통과한다.

---

## Task 3: `CreatureInstance` — 런타임 상태

**Files:**
- Create: `Assets/Scripts/Runtime/Core/CreatureInstance.cs`
- Create: `Assets/Scripts/Tests/EditMode/CreatureInstanceTests.cs`

**Interfaces:**
- Consumes: `StatType`, `StatBlock`, `CreatureSpeciesData` (Task 2)
- Produces: `FarmCreature.Core.CreatureInstance`
  - 상수: `MinStat = 1`, `MinCare = 0`, `MaxCare = 100`, `StartingFullness = 60`, `StartingBond = 30`
  - 프로퍼티: `CreatureSpeciesData CurrentSpecies { get; set; }`,
    `int Exp { get; }`, `int Fullness { get; }`, `int Bond { get; }`
  - 메서드: `static CreatureInstance CreateNew(CreatureSpeciesData)`,
    `int GetStat(StatType)`, `void SetStat(StatType,int)`, `void AddStat(StatType,int)`,
    `void AddExp(int)`, `void SetExp(int)`,
    `void AddFullness(int)`, `void SetFullness(int)`,
    `void AddBond(int)`, `void SetBond(int)`

- [x] **Step 1: 실패하는 테스트 작성**

`Assets/Scripts/Tests/EditMode/CreatureInstanceTests.cs`:

```csharp
using FarmCreature.Core;
using FarmCreature.Data;
using NUnit.Framework;

namespace FarmCreature.Tests
{
    public class CreatureInstanceTests
    {
        private CreatureSpeciesData species;
        private CreatureInstance creature;

        [SetUp]
        public void SetUp()
        {
            species = TestFixtures.MakeSpecies("sprigling", "스프리글링", attack: 5, defense: 5, health: 20);
            creature = CreatureInstance.CreateNew(species);
        }

        [Test]
        public void CreateNew_CopiesBaseStatsFromSpecies()
        {
            Assert.AreEqual(5, creature.GetStat(StatType.Attack));
            Assert.AreEqual(5, creature.GetStat(StatType.Defense));
            Assert.AreEqual(20, creature.GetStat(StatType.Health));
        }

        [Test]
        public void CreateNew_UsesStartingCareValues()
        {
            Assert.AreEqual(0, creature.Exp);
            Assert.AreEqual(CreatureInstance.StartingFullness, creature.Fullness);
            Assert.AreEqual(CreatureInstance.StartingBond, creature.Bond);
        }

        [Test]
        public void CreateNew_DoesNotShareStatBlockWithSpecies()
        {
            creature.AddStat(StatType.Attack, 10);

            Assert.AreEqual(5, species.baseStats.attack, "종 에셋의 기본치가 변경되면 안 된다");
        }

        [Test]
        public void AddExp_Accumulates()
        {
            creature.AddExp(15);
            creature.AddExp(15);

            Assert.AreEqual(30, creature.Exp);
        }

        [Test]
        public void SetExp_ClampsToZero()
        {
            creature.SetExp(-5);

            Assert.AreEqual(0, creature.Exp);
        }

        [Test]
        public void AddStat_ClampsToMinimumOne()
        {
            creature.AddStat(StatType.Attack, -100);

            Assert.AreEqual(CreatureInstance.MinStat, creature.GetStat(StatType.Attack));
        }

        [Test]
        public void AddFullness_ClampsToMax()
        {
            creature.AddFullness(500);

            Assert.AreEqual(CreatureInstance.MaxCare, creature.Fullness);
        }

        [Test]
        public void AddFullness_ClampsToMin()
        {
            creature.AddFullness(-500);

            Assert.AreEqual(CreatureInstance.MinCare, creature.Fullness);
        }

        [Test]
        public void AddBond_ClampsToRange()
        {
            creature.AddBond(500);
            Assert.AreEqual(CreatureInstance.MaxCare, creature.Bond);

            creature.AddBond(-500);
            Assert.AreEqual(CreatureInstance.MinCare, creature.Bond);
        }

        [Test]
        public void ChangingSpecies_KeepsStatsAndCareValues()
        {
            creature.AddStat(StatType.Attack, 10);
            creature.AddExp(120);
            creature.AddFullness(-20);
            creature.AddBond(10);

            var evolved = TestFixtures.MakeSpecies("wildbeast", "와일드비스트", attack: 99, defense: 99, health: 99);
            creature.CurrentSpecies = evolved;

            Assert.AreSame(evolved, creature.CurrentSpecies);
            Assert.AreEqual(15, creature.GetStat(StatType.Attack), "진화해도 스탯은 그대로 이어받는다");
            Assert.AreEqual(120, creature.Exp);
            Assert.AreEqual(40, creature.Fullness);
            Assert.AreEqual(40, creature.Bond);
        }
    }
}
```

- [x] **Step 2: 테스트 실행 — 실패 확인**

Expected: `error CS0246: ... 'CreatureInstance' could not be found`

- [x] **Step 3: `CreatureInstance` 구현**

`Assets/Scripts/Runtime/Core/CreatureInstance.cs`:

```csharp
using System;
using FarmCreature.Data;

namespace FarmCreature.Core
{
    /// <summary>
    /// 케어의 <b>변하는 값 전부</b>. ScriptableObject 를 건드리지 않기 위해 존재한다.
    /// UnityEngine API 를 호출하지 않으므로 EditMode 테스트에서 그대로 검증된다.
    /// </summary>
    public class CreatureInstance
    {
        public const int MinStat = 1;
        public const int MinCare = 0;
        public const int MaxCare = 100;
        public const int StartingFullness = 60;
        public const int StartingBond = 30;

        private int attack;
        private int defense;
        private int health;

        public CreatureSpeciesData CurrentSpecies { get; set; }
        public int Exp { get; private set; }
        public int Fullness { get; private set; }
        public int Bond { get; private set; }

        private CreatureInstance()
        {
        }

        /// <summary>종의 기본치를 <b>복사</b>해 새 개체를 만든다 (참조 공유 금지).</summary>
        public static CreatureInstance CreateNew(CreatureSpeciesData species)
        {
            if (species == null)
            {
                throw new ArgumentNullException(nameof(species));
            }

            var stats = species.baseStats ?? new StatBlock();

            return new CreatureInstance
            {
                CurrentSpecies = species,
                attack = Math.Max(MinStat, stats.attack),
                defense = Math.Max(MinStat, stats.defense),
                health = Math.Max(MinStat, stats.health),
                Exp = 0,
                Fullness = StartingFullness,
                Bond = StartingBond
            };
        }

        /// <summary>세이브 복원용. 클램프는 동일하게 적용된다.</summary>
        public static CreatureInstance Restore(
            CreatureSpeciesData species, int exp, int attack, int defense, int health, int fullness, int bond)
        {
            var creature = CreateNew(species);
            creature.SetExp(exp);
            creature.SetStat(StatType.Attack, attack);
            creature.SetStat(StatType.Defense, defense);
            creature.SetStat(StatType.Health, health);
            creature.SetFullness(fullness);
            creature.SetBond(bond);
            return creature;
        }

        public int GetStat(StatType stat)
        {
            switch (stat)
            {
                case StatType.Attack: return attack;
                case StatType.Defense: return defense;
                case StatType.Health: return health;
                default: throw new ArgumentOutOfRangeException(nameof(stat), stat, null);
            }
        }

        public void SetStat(StatType stat, int value)
        {
            int clamped = Math.Max(MinStat, value);

            switch (stat)
            {
                case StatType.Attack: attack = clamped; break;
                case StatType.Defense: defense = clamped; break;
                case StatType.Health: health = clamped; break;
                default: throw new ArgumentOutOfRangeException(nameof(stat), stat, null);
            }
        }

        public void AddStat(StatType stat, int delta)
        {
            SetStat(stat, GetStat(stat) + delta);
        }

        public void SetExp(int value)
        {
            Exp = Math.Max(0, value);
        }

        public void AddExp(int delta)
        {
            SetExp(Exp + delta);
        }

        public void SetFullness(int value)
        {
            Fullness = ClampCare(value);
        }

        public void AddFullness(int delta)
        {
            SetFullness(Fullness + delta);
        }

        public void SetBond(int value)
        {
            Bond = ClampCare(value);
        }

        public void AddBond(int delta)
        {
            SetBond(Bond + delta);
        }

        private static int ClampCare(int value)
        {
            if (value < MinCare) return MinCare;
            if (value > MaxCare) return MaxCare;
            return value;
        }
    }
}
```

- [x] **Step 4: 테스트 실행 — 통과 확인**

Expected: 종료 코드 `0`, `total="17" passed="17" failed="0"`.

- [x] **Step 5: 체크포인트**

`CreatureInstance` 의 클램프·복사 의미론이 테스트로 고정됐다.

---

## Task 4: `Inventory` + `CareActions` — 육성 액션

**Files:**
- Create: `Assets/Scripts/Runtime/Core/Inventory.cs`
- Create: `Assets/Scripts/Runtime/Core/ActionResult.cs`
- Create: `Assets/Scripts/Runtime/Core/CareActions.cs`
- Create: `Assets/Scripts/Tests/EditMode/CareActionsTests.cs`

**Interfaces:**
- Consumes: `CreatureInstance` (Task 3), `ItemData`, `StatType` (Task 2)
- Produces:
  - `FarmCreature.Core.Inventory` — `int GetCount(string)`, `bool Has(string)`,
    `void Add(string,int)`, `bool TryConsume(string,int)`,
    `IReadOnlyList<string> ItemIds { get; }` (삽입 순서 유지)
  - `FarmCreature.Core.ActionResult` — `bool Success`, `string Message`,
    `static ActionResult Ok(string)`, `static ActionResult Fail(string)`
  - `FarmCreature.Core.CareActions` — `static ActionResult Feed(CreatureInstance)`,
    `static ActionResult Train(CreatureInstance, StatType)`,
    `static ActionResult UseItem(CreatureInstance, Inventory, ItemData)`
    + public 상수 `FeedFullness=25`, `FeedBond=2`, `FeedExp=5`, `TrainStatGain=2`,
    `TrainExp=15`, `TrainFullnessCost=15`, `TrainBondCost=1`, `TrainMinFullness=20`,
    `ItemBond=5`, `ItemExp=10`

- [x] **Step 1: 실패하는 테스트 작성**

`Assets/Scripts/Tests/EditMode/CareActionsTests.cs`:

```csharp
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

        // ---- 아이템주기 ----

        [Test]
        public void UseItem_AppliesStatBonusAndConsumesOne()
        {
            inventory.Add("hearty_feed", 2);

            var result = CareActions.UseItem(creature, inventory, feedItem);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(5 + 3, creature.GetStat(StatType.Attack));
            Assert.AreEqual(CreatureInstance.StartingBond + CareActions.ItemBond, creature.Bond);
            Assert.AreEqual(CareActions.ItemExp, creature.Exp);
            Assert.AreEqual(1, inventory.GetCount("hearty_feed"));
        }

        [Test]
        public void UseItem_FailsWhenNotHeld_AndChangesNothing()
        {
            int attackBefore = creature.GetStat(StatType.Attack);
            int expBefore = creature.Exp;
            int bondBefore = creature.Bond;

            var result = CareActions.UseItem(creature, inventory, feedItem);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(attackBefore, creature.GetStat(StatType.Attack));
            Assert.AreEqual(expBefore, creature.Exp);
            Assert.AreEqual(bondBefore, creature.Bond);
            Assert.AreEqual(0, inventory.GetCount("hearty_feed"));
        }

        [Test]
        public void UseItem_FailsForNullItem()
        {
            Assert.IsFalse(CareActions.UseItem(creature, inventory, null).Success);
        }
    }
}
```

- [x] **Step 2: 테스트 실행 — 실패 확인**

Expected: `error CS0246: ... 'Inventory' could not be found`

- [x] **Step 3: `Inventory` 구현**

`Assets/Scripts/Runtime/Core/Inventory.cs`:

```csharp
using System.Collections.Generic;

namespace FarmCreature.Core
{
    /// <summary>아이템 ID → 개수. 삽입 순서를 유지해 UI 나열이 안정적이다.</summary>
    public class Inventory
    {
        private readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        private readonly List<string> order = new List<string>();

        public IReadOnlyList<string> ItemIds
        {
            get { return order; }
        }

        public int GetCount(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return 0;
            }

            int count;
            return counts.TryGetValue(itemId, out count) ? count : 0;
        }

        public bool Has(string itemId)
        {
            return GetCount(itemId) > 0;
        }

        public void Add(string itemId, int amount)
        {
            if (string.IsNullOrEmpty(itemId) || amount <= 0)
            {
                return;
            }

            if (!counts.ContainsKey(itemId))
            {
                counts[itemId] = 0;
                order.Add(itemId);
            }

            counts[itemId] += amount;
        }

        /// <summary>충분할 때만 차감한다. 부족하면 아무것도 바꾸지 않고 false.</summary>
        public bool TryConsume(string itemId, int amount = 1)
        {
            if (amount <= 0 || GetCount(itemId) < amount)
            {
                return false;
            }

            counts[itemId] -= amount;
            return true;
        }
    }
}
```

- [x] **Step 4: `ActionResult` 구현**

`Assets/Scripts/Runtime/Core/ActionResult.cs`:

```csharp
namespace FarmCreature.Core
{
    /// <summary>육성 액션의 결과. Message 는 UI 로그에 그대로 표시된다.</summary>
    public readonly struct ActionResult
    {
        public bool Success { get; }
        public string Message { get; }

        private ActionResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public static ActionResult Ok(string message)
        {
            return new ActionResult(true, message);
        }

        public static ActionResult Fail(string message)
        {
            return new ActionResult(false, message);
        }
    }
}
```

- [x] **Step 5: `CareActions` 구현**

`Assets/Scripts/Runtime/Core/CareActions.cs`:

```csharp
using FarmCreature.Data;

namespace FarmCreature.Core
{
    /// <summary>
    /// 육성 액션의 규칙. 진화는 알지 못한다 — 진화 평가는 호출자(CareManager)가 조율한다.
    /// <b>실패하면 상태를 하나도 바꾸지 않는다</b> (부분 적용 금지).
    /// </summary>
    public static class CareActions
    {
        public const int FeedFullness = 25;
        public const int FeedBond = 2;
        public const int FeedExp = 5;

        public const int TrainStatGain = 2;
        public const int TrainExp = 15;
        public const int TrainFullnessCost = 15;
        public const int TrainBondCost = 1;
        public const int TrainMinFullness = 20;

        public const int ItemBond = 5;
        public const int ItemExp = 10;

        public static ActionResult Feed(CreatureInstance creature)
        {
            if (creature == null)
            {
                return ActionResult.Fail("케어가 없습니다.");
            }

            if (creature.Fullness >= CreatureInstance.MaxCare)
            {
                return ActionResult.Fail("배가 불러서 더 먹지 않는다.");
            }

            creature.AddFullness(FeedFullness);
            creature.AddBond(FeedBond);
            creature.AddExp(FeedExp);

            return ActionResult.Ok("먹이를 주었다. 기분이 좋아 보인다.");
        }

        public static ActionResult Train(CreatureInstance creature, StatType stat)
        {
            if (creature == null)
            {
                return ActionResult.Fail("케어가 없습니다.");
            }

            if (creature.Fullness < TrainMinFullness)
            {
                return ActionResult.Fail("너무 배가 고파서 훈련할 수 없다. 먼저 먹이를 주자.");
            }

            creature.AddStat(stat, TrainStatGain);
            creature.AddExp(TrainExp);
            creature.AddFullness(-TrainFullnessCost);
            creature.AddBond(-TrainBondCost);

            return ActionResult.Ok(StatName(stat) + " 훈련을 마쳤다.");
        }

        public static ActionResult UseItem(CreatureInstance creature, Inventory inventory, ItemData item)
        {
            if (creature == null || inventory == null)
            {
                return ActionResult.Fail("케어가 없습니다.");
            }

            if (item == null)
            {
                return ActionResult.Fail("사용할 아이템이 없습니다.");
            }

            if (!inventory.TryConsume(item.itemId))
            {
                return ActionResult.Fail(item.displayName + "이(가) 없다.");
            }

            creature.AddStat(item.targetStat, item.amount);
            creature.AddBond(ItemBond);
            creature.AddExp(ItemExp);

            return ActionResult.Ok(item.displayName + "을(를) 주었다. " + StatName(item.targetStat) + "이(가) 올랐다.");
        }

        public static string StatName(StatType stat)
        {
            switch (stat)
            {
                case StatType.Attack: return "공격";
                case StatType.Defense: return "방어";
                case StatType.Health: return "체력";
                default: return stat.ToString();
            }
        }
    }
}
```

> `TryConsume` 이 소모까지 하므로 아이템 액션은 "검사 후 적용" 순서가 뒤집힌 것처럼 보이지만,
> `TryConsume` 은 실패 시 아무것도 바꾸지 않고, 성공하면 그 뒤 단계는 실패할 수 없다.
> 따라서 부분 적용은 발생하지 않는다.

- [x] **Step 6: 테스트 실행 — 통과 확인**

Expected: 종료 코드 `0`, `total="29" passed="29" failed="0"`.

- [x] **Step 7: 체크포인트**

3가지 액션의 수치와 **실패 시 무변경** 규칙이 테스트로 고정됐다.

---

## Task 5: `EvolutionEvaluator` — 진화 판정

**Files:**
- Create: `Assets/Scripts/Runtime/Core/EvolutionEvaluator.cs`
- Create: `Assets/Scripts/Tests/EditMode/EvolutionEvaluatorTests.cs`

**Interfaces:**
- Consumes: `CreatureInstance` (Task 3), `Inventory` (Task 4),
  `EvolutionCondition` / `EvolutionRequirement` / `RequirementKind` (Task 2)
- Produces: `FarmCreature.Core.EvolutionEvaluator`
  - `static CreatureSpeciesData TryEvolve(CreatureInstance, Inventory)` — 충족되는 첫 조건의
    결과 종을 반환. 없으면 `null`. **종을 교체하지 않는다.**
  - `static bool IsSatisfied(EvolutionCondition, CreatureInstance, Inventory)`
  - `static bool IsSatisfied(EvolutionRequirement, CreatureInstance, Inventory)`

- [x] **Step 1: 실패하는 테스트 작성**

`Assets/Scripts/Tests/EditMode/EvolutionEvaluatorTests.cs`:

```csharp
using System.Collections.Generic;
using FarmCreature.Core;
using FarmCreature.Data;
using NUnit.Framework;

namespace FarmCreature.Tests
{
    public class EvolutionEvaluatorTests
    {
        private CreatureSpeciesData baseSpecies;
        private CreatureSpeciesData wildbeast;
        private CreatureSpeciesData guardian;
        private CreatureInstance creature;
        private Inventory inventory;

        [SetUp]
        public void SetUp()
        {
            baseSpecies = TestFixtures.MakeSpecies("sprigling", "스프리글링", attack: 5, defense: 5, health: 20);
            wildbeast = TestFixtures.MakeSpecies("wildbeast", "와일드비스트", attack: 15, defense: 8, health: 30);
            guardian = TestFixtures.MakeSpecies("blossom_guardian", "블로섬 가디언", attack: 8, defense: 15, health: 35);
            creature = CreatureInstance.CreateNew(baseSpecies);
            inventory = new Inventory();
        }

        private static EvolutionCondition Condition(
            CreatureSpeciesData result, params EvolutionRequirement[] requirements)
        {
            return new EvolutionCondition
            {
                result = result,
                requirements = new List<EvolutionRequirement>(requirements)
            };
        }

        [Test]
        public void TryEvolve_ReturnsNullWhenSpeciesHasNoEvolutions()
        {
            Assert.IsNull(EvolutionEvaluator.TryEvolve(creature, inventory));
        }

        [Test]
        public void TryEvolve_ReturnsNullWhenExpBelowThreshold()
        {
            baseSpecies.evolutions.Add(Condition(wildbeast, EvolutionRequirement.ExpAtLeast(100)));
            creature.AddExp(99);

            Assert.IsNull(EvolutionEvaluator.TryEvolve(creature, inventory));
        }

        [Test]
        public void TryEvolve_ReturnsResultWhenExpMet()
        {
            baseSpecies.evolutions.Add(Condition(wildbeast, EvolutionRequirement.ExpAtLeast(100)));
            creature.AddExp(100);

            Assert.AreSame(wildbeast, EvolutionEvaluator.TryEvolve(creature, inventory));
        }

        [Test]
        public void TryEvolve_RequiresAllRequirements_AndedTogether()
        {
            baseSpecies.evolutions.Add(Condition(
                wildbeast,
                EvolutionRequirement.ExpAtLeast(100),
                EvolutionRequirement.StatAtLeast(StatType.Attack, 15)));

            creature.AddExp(200);
            Assert.IsNull(EvolutionEvaluator.TryEvolve(creature, inventory), "스탯 미달이면 진화 불가");

            creature.SetStat(StatType.Attack, 15);
            Assert.AreSame(wildbeast, EvolutionEvaluator.TryEvolve(creature, inventory));
        }

        [Test]
        public void TryEvolve_RequiresItem_ChecksInventoryWithoutConsuming()
        {
            baseSpecies.evolutions.Add(Condition(
                guardian,
                EvolutionRequirement.ExpAtLeast(100),
                EvolutionRequirement.RequiresItem("bloom_stone")));
            creature.AddExp(100);

            Assert.IsNull(EvolutionEvaluator.TryEvolve(creature, inventory));

            inventory.Add("bloom_stone", 1);

            Assert.AreSame(guardian, EvolutionEvaluator.TryEvolve(creature, inventory));
            Assert.AreEqual(1, inventory.GetCount("bloom_stone"), "진화 조건 확인은 아이템을 소모하지 않는다");
        }

        [Test]
        public void TryEvolve_PrefersEarlierConditionWhenBothSatisfied()
        {
            baseSpecies.evolutions.Add(Condition(
                wildbeast,
                EvolutionRequirement.ExpAtLeast(100),
                EvolutionRequirement.StatAtLeast(StatType.Attack, 15)));
            baseSpecies.evolutions.Add(Condition(
                guardian,
                EvolutionRequirement.ExpAtLeast(100),
                EvolutionRequirement.RequiresItem("bloom_stone")));

            creature.AddExp(100);
            creature.SetStat(StatType.Attack, 15);
            inventory.Add("bloom_stone", 1);

            Assert.AreSame(wildbeast, EvolutionEvaluator.TryEvolve(creature, inventory));
        }

        [Test]
        public void TryEvolve_SkipsConditionWithNullResult()
        {
            baseSpecies.evolutions.Add(Condition(null, EvolutionRequirement.ExpAtLeast(0)));
            baseSpecies.evolutions.Add(Condition(guardian, EvolutionRequirement.ExpAtLeast(0)));

            Assert.AreSame(guardian, EvolutionEvaluator.TryEvolve(creature, inventory));
        }

        [Test]
        public void TryEvolve_EmptyRequirementList_CountsAsSatisfied()
        {
            baseSpecies.evolutions.Add(Condition(wildbeast));

            Assert.AreSame(wildbeast, EvolutionEvaluator.TryEvolve(creature, inventory));
        }

        [Test]
        public void TryEvolve_ReturnsNullAfterEvolvingToTerminalSpecies()
        {
            baseSpecies.evolutions.Add(Condition(wildbeast, EvolutionRequirement.ExpAtLeast(0)));
            creature.CurrentSpecies = wildbeast;

            Assert.IsNull(EvolutionEvaluator.TryEvolve(creature, inventory), "진화형은 더 진화하지 않는다");
        }
    }
}
```

- [x] **Step 2: 테스트 실행 — 실패 확인**

Expected: `error CS0246: ... 'EvolutionEvaluator' could not be found`

- [x] **Step 3: `EvolutionEvaluator` 구현**

`Assets/Scripts/Runtime/Core/EvolutionEvaluator.cs`:

```csharp
using FarmCreature.Data;

namespace FarmCreature.Core
{
    /// <summary>
    /// 진화 조건을 <b>판정만</b> 한다. 종 교체는 호출자가 수행한다 (순수 함수 유지).
    /// 목록 순서가 곧 우선순위다.
    /// </summary>
    public static class EvolutionEvaluator
    {
        /// <returns>충족되는 첫 조건의 결과 종. 없으면 null.</returns>
        public static CreatureSpeciesData TryEvolve(CreatureInstance creature, Inventory inventory)
        {
            if (creature == null || creature.CurrentSpecies == null)
            {
                return null;
            }

            var conditions = creature.CurrentSpecies.evolutions;
            if (conditions == null)
            {
                return null;
            }

            for (int i = 0; i < conditions.Count; i++)
            {
                var condition = conditions[i];
                if (condition == null || condition.result == null)
                {
                    continue;
                }

                if (IsSatisfied(condition, creature, inventory))
                {
                    return condition.result;
                }
            }

            return null;
        }

        public static bool IsSatisfied(EvolutionCondition condition, CreatureInstance creature, Inventory inventory)
        {
            if (condition == null)
            {
                return false;
            }

            var requirements = condition.requirements;
            if (requirements == null)
            {
                return true;
            }

            for (int i = 0; i < requirements.Count; i++)
            {
                if (!IsSatisfied(requirements[i], creature, inventory))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsSatisfied(EvolutionRequirement requirement, CreatureInstance creature, Inventory inventory)
        {
            if (requirement == null || creature == null)
            {
                return false;
            }

            switch (requirement.kind)
            {
                case RequirementKind.ExpAtLeast:
                    return creature.Exp >= requirement.value;

                case RequirementKind.StatAtLeast:
                    return creature.GetStat(requirement.stat) >= requirement.value;

                case RequirementKind.RequiresItem:
                    return inventory != null && inventory.Has(requirement.itemId);

                default:
                    return false;
            }
        }
    }
}
```

- [x] **Step 4: 테스트 실행 — 통과 확인**

Expected: 종료 코드 `0`, `total="38" passed="38" failed="0"`.

- [x] **Step 5: 체크포인트**

AND 조합·우선순위·아이템 비소모·종착 종 규칙이 전부 테스트로 고정됐다.

---

## Task 6: 세이브 직렬화

**Files:**
- Create: `Assets/Scripts/Runtime/Core/SaveData.cs`
- Create: `Assets/Scripts/Runtime/Core/SaveSerializer.cs`
- Create: `Assets/Scripts/Tests/EditMode/SaveSerializerTests.cs`

**Interfaces:**
- Consumes: `CreatureInstance` (Task 3), `Inventory` (Task 4), `SpeciesDatabase` (Task 2)
- Produces:
  - `FarmCreature.Core.SaveData` — `[Serializable]`, 상수 `CurrentVersion = 1`,
    필드 `version`, `speciesId`, `exp`, `attack`, `defense`, `health`, `fullness`, `bond`,
    `itemIds` (`string[]`), `itemCounts` (`int[]`), `savedAtUtc`
  - `FarmCreature.Core.SaveSerializer` —
    `static SaveData Capture(CreatureInstance, Inventory, string savedAtUtc)`,
    `static string ToJson(SaveData)`,
    `static bool TryRestore(string json, SpeciesDatabase, out CreatureInstance, out Inventory, out string error)`

- [x] **Step 1: 실패하는 테스트 작성**

`Assets/Scripts/Tests/EditMode/SaveSerializerTests.cs`:

```csharp
using FarmCreature.Core;
using FarmCreature.Data;
using NUnit.Framework;

namespace FarmCreature.Tests
{
    public class SaveSerializerTests
    {
        private CreatureSpeciesData sprigling;
        private SpeciesDatabase database;

        [SetUp]
        public void SetUp()
        {
            sprigling = TestFixtures.MakeSpecies("sprigling", "스프리글링", attack: 5, defense: 5, health: 20);
            database = TestFixtures.MakeSpeciesDatabase(sprigling);
        }

        private static string CaptureJson(CreatureInstance creature, Inventory inventory)
        {
            return SaveSerializer.ToJson(SaveSerializer.Capture(creature, inventory, "2026-08-06T01:40:00Z"));
        }

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
            inventory.Add("hearty_feed", 2);
            inventory.Add("bloom_stone", 1);

            string json = CaptureJson(creature, inventory);

            CreatureInstance loaded;
            Inventory loadedInventory;
            string error;
            bool ok = SaveSerializer.TryRestore(json, database, out loaded, out loadedInventory, out error);

            Assert.IsTrue(ok, error);
            Assert.AreSame(sprigling, loaded.CurrentSpecies);
            Assert.AreEqual(140, loaded.Exp);
            Assert.AreEqual(17, loaded.GetStat(StatType.Attack));
            Assert.AreEqual(6, loaded.GetStat(StatType.Defense));
            Assert.AreEqual(22, loaded.GetStat(StatType.Health));
            Assert.AreEqual(45, loaded.Fullness);
            Assert.AreEqual(38, loaded.Bond);
            Assert.AreEqual(2, loadedInventory.GetCount("hearty_feed"));
            Assert.AreEqual(1, loadedInventory.GetCount("bloom_stone"));
        }

        [Test]
        public void Capture_WritesCurrentVersionAndSpeciesId()
        {
            var data = SaveSerializer.Capture(
                CreatureInstance.CreateNew(sprigling), new Inventory(), "2026-08-06T01:40:00Z");

            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual("sprigling", data.speciesId);
            Assert.AreEqual("2026-08-06T01:40:00Z", data.savedAtUtc);
        }

        [Test]
        public void TryRestore_RejectsMalformedJson()
        {
            CreatureInstance loaded;
            Inventory loadedInventory;
            string error;

            Assert.IsFalse(SaveSerializer.TryRestore(
                "{not json at all", database, out loaded, out loadedInventory, out error));
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void TryRestore_RejectsEmptyJson()
        {
            CreatureInstance loaded;
            Inventory loadedInventory;
            string error;

            Assert.IsFalse(SaveSerializer.TryRestore("", database, out loaded, out loadedInventory, out error));
        }

        [Test]
        public void TryRestore_RejectsUnsupportedVersion()
        {
            string json = "{\"version\":99,\"speciesId\":\"sprigling\",\"exp\":0,\"attack\":5," +
                          "\"defense\":5,\"health\":20,\"fullness\":60,\"bond\":30," +
                          "\"itemIds\":[],\"itemCounts\":[],\"savedAtUtc\":\"x\"}";

            CreatureInstance loaded;
            Inventory loadedInventory;
            string error;

            Assert.IsFalse(SaveSerializer.TryRestore(json, database, out loaded, out loadedInventory, out error));
            StringAssert.Contains("버전", error);
        }

        [Test]
        public void TryRestore_RejectsUnknownSpeciesId()
        {
            string json = "{\"version\":1,\"speciesId\":\"ghost\",\"exp\":0,\"attack\":5," +
                          "\"defense\":5,\"health\":20,\"fullness\":60,\"bond\":30," +
                          "\"itemIds\":[],\"itemCounts\":[],\"savedAtUtc\":\"x\"}";

            CreatureInstance loaded;
            Inventory loadedInventory;
            string error;

            Assert.IsFalse(SaveSerializer.TryRestore(json, database, out loaded, out loadedInventory, out error));
            StringAssert.Contains("ghost", error);
        }

        [Test]
        public void TryRestore_RejectsMismatchedInventoryArrays()
        {
            string json = "{\"version\":1,\"speciesId\":\"sprigling\",\"exp\":0,\"attack\":5," +
                          "\"defense\":5,\"health\":20,\"fullness\":60,\"bond\":30," +
                          "\"itemIds\":[\"a\",\"b\"],\"itemCounts\":[1],\"savedAtUtc\":\"x\"}";

            CreatureInstance loaded;
            Inventory loadedInventory;
            string error;

            Assert.IsFalse(SaveSerializer.TryRestore(json, database, out loaded, out loadedInventory, out error));
        }

        [Test]
        public void TryRestore_ClampsOutOfRangeValues()
        {
            string json = "{\"version\":1,\"speciesId\":\"sprigling\",\"exp\":-10,\"attack\":-5," +
                          "\"defense\":5,\"health\":20,\"fullness\":500,\"bond\":-20," +
                          "\"itemIds\":[],\"itemCounts\":[],\"savedAtUtc\":\"x\"}";

            CreatureInstance loaded;
            Inventory loadedInventory;
            string error;

            Assert.IsTrue(SaveSerializer.TryRestore(json, database, out loaded, out loadedInventory, out error), error);
            Assert.AreEqual(0, loaded.Exp);
            Assert.AreEqual(CreatureInstance.MinStat, loaded.GetStat(StatType.Attack));
            Assert.AreEqual(CreatureInstance.MaxCare, loaded.Fullness);
            Assert.AreEqual(CreatureInstance.MinCare, loaded.Bond);
        }
    }
}
```

- [x] **Step 2: 테스트 실행 — 실패 확인**

Expected: `error CS0246: ... 'SaveSerializer' could not be found`

- [x] **Step 3: `SaveData` 구현**

`Assets/Scripts/Runtime/Core/SaveData.cs`:

```csharp
using System;

namespace FarmCreature.Core
{
    /// <summary>
    /// 세이브 JSON 스키마. JsonUtility 가 Dictionary 를 직렬화하지 못하므로
    /// 인벤토리는 병렬 배열로 평탄화한다.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string speciesId;
        public int exp;
        public int attack;
        public int defense;
        public int health;
        public int fullness;
        public int bond;
        public string[] itemIds = new string[0];
        public int[] itemCounts = new int[0];
        public string savedAtUtc;
    }
}
```

- [x] **Step 4: `SaveSerializer` 구현**

`Assets/Scripts/Runtime/Core/SaveSerializer.cs`:

```csharp
using FarmCreature.Data;
using UnityEngine;

namespace FarmCreature.Core
{
    /// <summary>
    /// 상태 ↔ SaveData ↔ JSON 변환. <b>파일 IO 를 하지 않는다</b> —
    /// 그래야 파일 시스템 없이 라운드트립을 테스트할 수 있다.
    /// </summary>
    public static class SaveSerializer
    {
        public static SaveData Capture(CreatureInstance creature, Inventory inventory, string savedAtUtc)
        {
            var ids = inventory != null ? inventory.ItemIds : null;
            int count = ids != null ? ids.Count : 0;

            var data = new SaveData
            {
                version = SaveData.CurrentVersion,
                speciesId = creature.CurrentSpecies != null ? creature.CurrentSpecies.speciesId : null,
                exp = creature.Exp,
                attack = creature.GetStat(StatType.Attack),
                defense = creature.GetStat(StatType.Defense),
                health = creature.GetStat(StatType.Health),
                fullness = creature.Fullness,
                bond = creature.Bond,
                itemIds = new string[count],
                itemCounts = new int[count],
                savedAtUtc = savedAtUtc
            };

            for (int i = 0; i < count; i++)
            {
                data.itemIds[i] = ids[i];
                data.itemCounts[i] = inventory.GetCount(ids[i]);
            }

            return data;
        }

        public static string ToJson(SaveData data)
        {
            return JsonUtility.ToJson(data, true);
        }

        /// <summary>
        /// 어떤 실패든 false 를 반환하고 <paramref name="error"/> 에 사유를 담는다.
        /// 호출자는 실패 시 새 게임으로 폴백한다.
        /// </summary>
        public static bool TryRestore(
            string json,
            SpeciesDatabase database,
            out CreatureInstance creature,
            out Inventory inventory,
            out string error)
        {
            creature = null;
            inventory = null;

            if (string.IsNullOrEmpty(json))
            {
                error = "세이브 내용이 비어 있습니다.";
                return false;
            }

            if (database == null)
            {
                error = "종 데이터베이스가 없습니다.";
                return false;
            }

            SaveData data;
            try
            {
                data = JsonUtility.FromJson<SaveData>(json);
            }
            catch (System.Exception e)
            {
                error = "세이브를 읽을 수 없습니다: " + e.Message;
                return false;
            }

            if (data == null)
            {
                error = "세이브를 읽을 수 없습니다.";
                return false;
            }

            if (data.version != SaveData.CurrentVersion)
            {
                error = "지원하지 않는 세이브 버전입니다: " + data.version;
                return false;
            }

            var species = database.Find(data.speciesId);
            if (species == null)
            {
                error = "알 수 없는 종입니다: " + data.speciesId;
                return false;
            }

            var ids = data.itemIds ?? new string[0];
            var counts = data.itemCounts ?? new int[0];
            if (ids.Length != counts.Length)
            {
                error = "인벤토리 데이터가 손상됐습니다.";
                return false;
            }

            creature = CreatureInstance.Restore(
                species, data.exp, data.attack, data.defense, data.health, data.fullness, data.bond);

            inventory = new Inventory();
            for (int i = 0; i < ids.Length; i++)
            {
                inventory.Add(ids[i], counts[i]);
            }

            error = null;
            return true;
        }
    }
}
```

> `JsonUtility.FromJson` 은 잘못된 JSON에 대해 `ArgumentException` 을 던진다. 위의 try/catch가
> 이를 흡수한다. 빈 문자열은 그 전에 걸러진다.

- [x] **Step 5: 테스트 실행 — 통과 확인**

Expected: 종료 코드 `0`, `total="46" passed="46" failed="0"`.

- [x] **Step 6: 체크포인트**

라운드트립과 4가지 손상 세이브 거부 경로가 전부 테스트로 고정됐다. **Core 계층 완성.**

---

## Task 7: 프레젠테이션 계층 (MonoBehaviour)

여기부터는 자동 테스트 대상이 아니다. 컴파일 통과 + Task 8의 씬 생성으로 검증한다.

**Files:**
- Create: `Assets/Scripts/Runtime/Presentation/FarmArea.cs`
- Create: `Assets/Scripts/Runtime/Presentation/PlaceholderSprites.cs`
- Create: `Assets/Scripts/Runtime/Presentation/SaveSystem.cs`
- Create: `Assets/Scripts/Runtime/Presentation/PlayerController.cs`
- Create: `Assets/Scripts/Runtime/Presentation/CreatureController.cs`
- Create: `Assets/Scripts/Runtime/Presentation/GameHud.cs`
- Create: `Assets/Scripts/Runtime/Presentation/CareManager.cs`
- Create: `Assets/Scripts/Runtime/Presentation/GameRoot.cs`
- Modify: `Assets/Scripts/Runtime/FarmCreature.Runtime.asmdef` (uGUI 참조 추가)

**Interfaces:**
- Consumes: Core 계층 전부 (Task 3~6), Data 계층 (Task 2)
- Produces:
  - `FarmArea` — `static readonly Vector2 HalfExtents = new Vector2(7f, 3.5f)`,
    `static Vector3 Clamp(Vector3, float margin)`
  - `PlaceholderSprites` — `static Sprite Square()`, `static Sprite Circle()`
  - `SaveSystem` — `static string SavePath`, `static bool Save(CreatureInstance, Inventory, out string)`,
    `static bool TryLoad(SpeciesDatabase, out CreatureInstance, out Inventory, out string)`
  - `PlayerController : MonoBehaviour` — `public float moveSpeed`
  - `CreatureController : MonoBehaviour` — `public void Bind(CreatureInstance)`, `public void RefreshVisual()`
  - `GameHud : MonoBehaviour` — `public Text statusText`, `public Text logText`,
    `public void Bind(CreatureInstance, Inventory)`, `public void Refresh()`, `public void SetLog(string)`
  - `CareManager : MonoBehaviour` — `public void Bind(CreatureInstance, Inventory, GameHud, CreatureController)`,
    `public void DoFeed()`, `public void DoTrain(int statIndex)`, `public void DoUseItem(ItemData)`, `public void DoSave()`
  - `GameRoot : MonoBehaviour` — 인스펙터 필드 `speciesDatabase`, `itemDatabase`, `startingSpecies`,
    `startingItems` (`List<ItemData>`), `startingItemCounts` (`List<int>`),
    `careManager`, `hud`, `creatureController`

- [x] **Step 1: asmdef 에 uGUI 참조 추가**

`Assets/Scripts/Runtime/FarmCreature.Runtime.asmdef` 의 `"references"` 를 다음으로 바꾼다:

```json
    "references": [
        "UnityEngine.UI"
    ],
```

- [x] **Step 2: `FarmArea` 작성**

`Assets/Scripts/Runtime/Presentation/FarmArea.cs`:

```csharp
using UnityEngine;

namespace FarmCreature.Presentation
{
    /// <summary>농장 구역의 경계. 카메라 Orthographic size 5 기준으로 화면에 딱 들어온다.</summary>
    public static class FarmArea
    {
        public static readonly Vector2 HalfExtents = new Vector2(7f, 3.5f);

        public static Vector3 Clamp(Vector3 position, float margin)
        {
            float x = Mathf.Clamp(position.x, -HalfExtents.x + margin, HalfExtents.x - margin);
            float y = Mathf.Clamp(position.y, -HalfExtents.y + margin, HalfExtents.y - margin);
            return new Vector3(x, y, position.z);
        }

        public static Vector3 RandomPoint(float margin)
        {
            return new Vector3(
                Random.Range(-HalfExtents.x + margin, HalfExtents.x - margin),
                Random.Range(-HalfExtents.y + margin, HalfExtents.y - margin),
                0f);
        }
    }
}
```

- [x] **Step 3: `PlaceholderSprites` 작성**

`Assets/Scripts/Runtime/Presentation/PlaceholderSprites.cs`:

```csharp
using UnityEngine;

namespace FarmCreature.Presentation
{
    /// <summary>에셋 파일 없이 코드로 만드는 플레이스홀더 스프라이트. 색은 SpriteRenderer.color 로 준다.</summary>
    public static class PlaceholderSprites
    {
        private static Sprite square;
        private static Sprite circle;

        public static Sprite Square()
        {
            if (square == null)
            {
                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                texture.hideFlags = HideFlags.HideAndDontSave;

                square = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
                square.hideFlags = HideFlags.HideAndDontSave;
            }

            return square;
        }

        public static Sprite Circle()
        {
            if (circle == null)
            {
                const int size = 64;
                const float radius = size * 0.5f;

                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.hideFlags = HideFlags.HideAndDontSave;

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x + 0.5f - radius;
                        float dy = y + 0.5f - radius;
                        bool inside = dx * dx + dy * dy <= radius * radius;
                        texture.SetPixel(x, y, inside ? Color.white : Color.clear);
                    }
                }

                texture.Apply();

                circle = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
                circle.hideFlags = HideFlags.HideAndDontSave;
            }

            return circle;
        }
    }
}
```

- [x] **Step 4: `SaveSystem` 작성**

`Assets/Scripts/Runtime/Presentation/SaveSystem.cs`:

```csharp
using System;
using System.IO;
using FarmCreature.Core;
using FarmCreature.Data;
using UnityEngine;

namespace FarmCreature.Presentation
{
    /// <summary>세이브 파일 읽기/쓰기만 담당한다. 변환 로직은 전부 SaveSerializer 에 있다.</summary>
    public static class SaveSystem
    {
        public const string FileName = "save.json";

        public static string SavePath
        {
            get { return Path.Combine(Application.persistentDataPath, FileName); }
        }

        public static bool Save(CreatureInstance creature, Inventory inventory, out string error)
        {
            try
            {
                string stamp = DateTime.UtcNow.ToString("o");
                string json = SaveSerializer.ToJson(SaveSerializer.Capture(creature, inventory, stamp));
                File.WriteAllText(SavePath, json);
                error = null;
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        /// <summary>세이브가 없거나 손상됐으면 false. 호출자는 새 게임으로 시작한다.</summary>
        public static bool TryLoad(
            SpeciesDatabase database, out CreatureInstance creature, out Inventory inventory, out string error)
        {
            creature = null;
            inventory = null;

            if (!File.Exists(SavePath))
            {
                error = null;
                return false;
            }

            string json;
            try
            {
                json = File.ReadAllText(SavePath);
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }

            return SaveSerializer.TryRestore(json, database, out creature, out inventory, out error);
        }
    }
}
```

- [x] **Step 5: `PlayerController` 작성**

`Assets/Scripts/Runtime/Presentation/PlayerController.cs`:

```csharp
using UnityEngine;

namespace FarmCreature.Presentation
{
    /// <summary>WASD / 화살표키로 농장 구역 안을 이동한다 (레거시 Input Manager).</summary>
    public class PlayerController : MonoBehaviour
    {
        public float moveSpeed = 5f;
        public float margin = 0.5f;

        private void Update()
        {
            float x = Input.GetAxisRaw("Horizontal");
            float y = Input.GetAxisRaw("Vertical");

            if (x == 0f && y == 0f)
            {
                return;
            }

            Vector3 delta = new Vector3(x, y, 0f).normalized * (moveSpeed * Time.deltaTime);
            transform.position = FarmArea.Clamp(transform.position + delta, margin);
        }
    }
}
```

- [x] **Step 6: `CreatureController` 작성**

`Assets/Scripts/Runtime/Presentation/CreatureController.cs`:

```csharp
using FarmCreature.Core;
using UnityEngine;

namespace FarmCreature.Presentation
{
    /// <summary>케어의 외형 갱신과 배회 이동. 게임 규칙은 전혀 모른다.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class CreatureController : MonoBehaviour
    {
        public float moveSpeed = 1.2f;
        public float repathInterval = 3f;
        public float margin = 0.6f;

        private SpriteRenderer spriteRenderer;
        private CreatureInstance creature;
        private Vector3 target;
        private float repathTimer;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = PlaceholderSprites.Circle();
            target = transform.position;
        }

        public void Bind(CreatureInstance instance)
        {
            creature = instance;
            RefreshVisual();
        }

        /// <summary>종이 바뀌었을 때 색과 크기를 다시 읽는다.</summary>
        public void RefreshVisual()
        {
            if (creature == null || creature.CurrentSpecies == null)
            {
                return;
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            spriteRenderer.color = creature.CurrentSpecies.placeholderColor;

            float scale = creature.CurrentSpecies.placeholderScale;
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        private void Update()
        {
            repathTimer -= Time.deltaTime;
            if (repathTimer <= 0f || (transform.position - target).sqrMagnitude < 0.01f)
            {
                target = FarmArea.RandomPoint(margin);
                repathTimer = repathInterval;
            }

            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
        }
    }
}
```

- [x] **Step 7: `GameHud` 작성**

`Assets/Scripts/Runtime/Presentation/GameHud.cs`:

```csharp
using System.Text;
using FarmCreature.Core;
using FarmCreature.Data;
using UnityEngine;
using UnityEngine.UI;

namespace FarmCreature.Presentation
{
    /// <summary>상태 패널과 로그 한 줄. 값을 읽어 표시만 한다.</summary>
    public class GameHud : MonoBehaviour
    {
        public Text statusText;
        public Text logText;

        private CreatureInstance creature;
        private Inventory inventory;
        private readonly StringBuilder builder = new StringBuilder();

        public void Bind(CreatureInstance instance, Inventory items)
        {
            creature = instance;
            inventory = items;
            Refresh();
        }

        public void Refresh()
        {
            if (statusText == null || creature == null)
            {
                return;
            }

            string speciesName = creature.CurrentSpecies != null
                ? creature.CurrentSpecies.displayName
                : "???";

            builder.Length = 0;
            builder.AppendLine("[ " + speciesName + " ]");
            builder.AppendLine("경험치  " + creature.Exp);
            builder.AppendLine("공격  " + creature.GetStat(StatType.Attack)
                               + "   방어  " + creature.GetStat(StatType.Defense)
                               + "   체력  " + creature.GetStat(StatType.Health));
            builder.AppendLine("포만도  " + Bar(creature.Fullness) + " " + creature.Fullness);
            builder.AppendLine("친밀도  " + Bar(creature.Bond) + " " + creature.Bond);

            if (inventory != null && inventory.ItemIds.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("소지품");
                for (int i = 0; i < inventory.ItemIds.Count; i++)
                {
                    string id = inventory.ItemIds[i];
                    builder.AppendLine("  " + id + " x" + inventory.GetCount(id));
                }
            }

            statusText.text = builder.ToString();
        }

        public void SetLog(string message)
        {
            if (logText != null)
            {
                logText.text = message;
            }
        }

        private static string Bar(int value)
        {
            int filled = Mathf.Clamp(value / 10, 0, 10);
            return new string('#', filled) + new string('.', 10 - filled);
        }
    }
}
```

- [x] **Step 8: `CareManager` 작성**

`Assets/Scripts/Runtime/Presentation/CareManager.cs`:

```csharp
using FarmCreature.Core;
using FarmCreature.Data;
using UnityEngine;

namespace FarmCreature.Presentation
{
    /// <summary>
    /// UI 버튼 → 액션 실행 → 진화 평가 → 결과 전파를 조율한다.
    /// 액션 성공 직후에만 진화를 판정한다 (설계 3.2).
    /// </summary>
    public class CareManager : MonoBehaviour
    {
        private CreatureInstance creature;
        private Inventory inventory;
        private GameHud hud;
        private CreatureController creatureController;

        public void Bind(CreatureInstance instance, Inventory items, GameHud gameHud, CreatureController controller)
        {
            creature = instance;
            inventory = items;
            hud = gameHud;
            creatureController = controller;
        }

        public void DoFeed()
        {
            Apply(CareActions.Feed(creature));
        }

        /// <summary>uGUI Button 이 int 만 넘길 수 있어 인덱스로 받는다 (0=공격, 1=방어, 2=체력).</summary>
        public void DoTrain(int statIndex)
        {
            Apply(CareActions.Train(creature, (StatType)statIndex));
        }

        public void DoUseItem(ItemData item)
        {
            Apply(CareActions.UseItem(creature, inventory, item));
        }

        public void DoSave()
        {
            string error;
            bool ok = SaveSystem.Save(creature, inventory, out error);
            SetLog(ok ? "저장했다." : "저장 실패: " + error);
        }

        private void Apply(ActionResult result)
        {
            if (!result.Success)
            {
                SetLog(result.Message);
                return;
            }

            string message = result.Message;

            var evolved = EvolutionEvaluator.TryEvolve(creature, inventory);
            if (evolved != null)
            {
                string before = creature.CurrentSpecies != null ? creature.CurrentSpecies.displayName : "???";
                creature.CurrentSpecies = evolved;

                if (creatureController != null)
                {
                    creatureController.RefreshVisual();
                }

                message = before + "이(가) " + evolved.displayName + "(으)로 진화했다!";
            }

            if (hud != null)
            {
                hud.Refresh();
            }

            SetLog(message);
        }

        private void SetLog(string message)
        {
            if (hud != null)
            {
                hud.SetLog(message);
            }
        }
    }
}
```

- [x] **Step 9: `GameRoot` 작성**

`Assets/Scripts/Runtime/Presentation/GameRoot.cs`:

```csharp
using System.Collections.Generic;
using FarmCreature.Core;
using FarmCreature.Data;
using UnityEngine;

namespace FarmCreature.Presentation
{
    /// <summary>시작 시 세이브를 불러오거나 새 게임을 만들고, 씬의 조각들을 연결한다.</summary>
    public class GameRoot : MonoBehaviour
    {
        [Header("데이터")]
        public SpeciesDatabase speciesDatabase;
        public ItemDatabase itemDatabase;
        public CreatureSpeciesData startingSpecies;

        [Header("새 게임 시작 소지품 (두 목록의 길이를 맞출 것)")]
        public List<ItemData> startingItems = new List<ItemData>();
        public List<int> startingItemCounts = new List<int>();

        [Header("씬 참조")]
        public CareManager careManager;
        public GameHud hud;
        public CreatureController creatureController;

        private void Start()
        {
            CreatureInstance creature;
            Inventory inventory;
            string error;
            string message;

            if (SaveSystem.TryLoad(speciesDatabase, out creature, out inventory, out error))
            {
                message = "이어서 돌보는 중.";
            }
            else
            {
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogWarning("[FarmCreature] 세이브를 불러오지 못해 새로 시작합니다: " + error);
                }

                creature = CreatureInstance.CreateNew(startingSpecies);
                inventory = BuildStartingInventory();
                message = "새로운 케어를 맞이했다.";
            }

            if (creatureController != null)
            {
                creatureController.Bind(creature);
            }

            if (hud != null)
            {
                hud.Bind(creature, inventory);
                hud.SetLog(message);
            }

            if (careManager != null)
            {
                careManager.Bind(creature, inventory, hud, creatureController);
            }
        }

        private Inventory BuildStartingInventory()
        {
            var inventory = new Inventory();

            for (int i = 0; i < startingItems.Count; i++)
            {
                var item = startingItems[i];
                if (item == null)
                {
                    continue;
                }

                int count = i < startingItemCounts.Count ? startingItemCounts[i] : 1;
                inventory.Add(item.itemId, count);
            }

            return inventory;
        }
    }
}
```

- [x] **Step 10: 컴파일 확인 (테스트 재실행)**

Expected: 종료 코드 `0`, `total="46" passed="46" failed="0"` (테스트 수는 그대로, 컴파일만 확인).
`unity-test.log` 에 `error CS` 가 없어야 한다.

- [x] **Step 11: 체크포인트**

프레젠테이션 8개 파일이 컴파일되고 기존 테스트가 여전히 통과한다.

---

## Task 8: 에디터 부트스트랩 — 에셋 + 씬 생성

**Files:**
- Create: `Assets/Scripts/Editor/FarmCreature.Editor.asmdef`
- Create: `Assets/Scripts/Editor/GameBootstrap.cs`
- 생성물: `Assets/GameData/**/*.asset`, `Assets/Scenes/FarmScene.unity`
- Modify: `ProjectSettings/EditorBuildSettings.asset` (`AddScene` API 가 자동 갱신)

**Interfaces:**
- Consumes: Data 계층 (Task 2), Presentation 계층 (Task 7)
- Produces: `FarmCreature.EditorTools.GameBootstrap`
  - `[MenuItem("FarmCreature/Build All")] static void BuildAll()`
  - 배치모드 진입점으로도 동일 메서드를 쓴다

- [x] **Step 1: 에디터 asmdef 작성**

`Assets/Scripts/Editor/FarmCreature.Editor.asmdef`:

```json
{
    "name": "FarmCreature.Editor",
    "rootNamespace": "FarmCreature.EditorTools",
    "references": [
        "FarmCreature.Runtime",
        "UnityEngine.UI"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [x] **Step 2: `GameBootstrap` 작성**

`Assets/Scripts/Editor/GameBootstrap.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using FarmCreature.Data;
using FarmCreature.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        private const string ScenesDir = "Assets/Scenes";
        private const string ScenePath = ScenesDir + "/FarmScene.unity";

        private static Font uiFont;

        [MenuItem("FarmCreature/Build All")]
        public static void BuildAll()
        {
            EnsureFolders();

            var species = BuildSpeciesAssets();
            var items = BuildItemAssets();
            var speciesDb = BuildSpeciesDatabase(species);
            var itemDb = BuildItemDatabase(items);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BuildScene(speciesDb, itemDb, species["sprigling"], items);

            Debug.Log("[FarmCreature] Build All 완료 — 에셋과 씬을 생성했습니다: " + ScenePath);
        }

        // ---------- 폴더 ----------

        private static void EnsureFolders()
        {
            EnsureFolder(DataRoot);
            EnsureFolder(SpeciesDir);
            EnsureFolder(ItemsDir);
            EnsureFolder(ScenesDir);
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
                        EvolutionRequirement.RequiresItem("bloom_stone")
                    }
                }
            };

            wildbeast.speciesId = "wildbeast";
            wildbeast.displayName = "와일드비스트";
            wildbeast.baseStats = new StatBlock { attack = 15, defense = 8, health = 30 };
            wildbeast.placeholderColor = new Color(0.95f, 0.55f, 0.2f);
            wildbeast.placeholderScale = 1.5f;
            wildbeast.evolutions = new List<EvolutionCondition>();

            guardian.speciesId = "blossom_guardian";
            guardian.displayName = "블로섬 가디언";
            guardian.baseStats = new StatBlock { attack = 8, defense = 15, health = 35 };
            guardian.placeholderColor = new Color(0.95f, 0.6f, 0.8f);
            guardian.placeholderScale = 1.5f;
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
            var feed = LoadOrCreate<ItemData>(ItemsDir + "/HeartyFeed.asset");
            feed.itemId = "hearty_feed";
            feed.displayName = "든든한 사료";
            feed.targetStat = StatType.Attack;
            feed.amount = 3;

            var grain = LoadOrCreate<ItemData>(ItemsDir + "/IronGrain.asset");
            grain.itemId = "iron_grain";
            grain.displayName = "무쇠 곡물";
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

        private static void BuildScene(
            SpeciesDatabase speciesDb,
            ItemDatabase itemDb,
            CreatureSpeciesData startingSpecies,
            List<ItemData> items)
        {
            uiFont = Font.CreateDynamicFontFromOSFont("맑은 고딕", 16);
            if (uiFont == null)
            {
                uiFont = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 16);
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 카메라
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.backgroundColor = new Color(0.62f, 0.83f, 0.94f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.transform.position = new Vector3(0f, 0f, -10f);

            // 농장 바닥
            var ground = new GameObject("Ground");
            var groundRenderer = ground.AddComponent<SpriteRenderer>();
            groundRenderer.sprite = PlaceholderSprites.Square();
            groundRenderer.color = new Color(0.72f, 0.87f, 0.6f);
            groundRenderer.sortingOrder = -10;
            ground.transform.localScale = new Vector3(
                FarmArea.HalfExtents.x * 2f, FarmArea.HalfExtents.y * 2f, 1f);

            // 케어
            var creatureGo = new GameObject("Creature");
            var creatureRenderer = creatureGo.AddComponent<SpriteRenderer>();
            creatureRenderer.sortingOrder = 1;
            var creatureController = creatureGo.AddComponent<CreatureController>();
            creatureGo.transform.position = new Vector3(2f, 0f, 0f);

            // 플레이어
            var playerGo = new GameObject("Player");
            var playerRenderer = playerGo.AddComponent<SpriteRenderer>();
            playerRenderer.sprite = PlaceholderSprites.Square();
            playerRenderer.color = new Color(0.25f, 0.45f, 0.9f);
            playerRenderer.sortingOrder = 2;
            playerGo.AddComponent<PlayerController>();
            playerGo.transform.position = new Vector3(-2f, 0f, 0f);
            playerGo.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

            // UI
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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

            var careGo = new GameObject("CareManager");
            var careManager = careGo.AddComponent<CareManager>();

            // 버튼들
            float x = -700f;
            const float step = 190f;

            CreateButton(canvasGo.transform, "먹이주기", x, careManager, ButtonAction.Feed, 0, null);
            x += step;
            CreateButton(canvasGo.transform, "공격 훈련", x, careManager, ButtonAction.Train, 0, null);
            x += step;
            CreateButton(canvasGo.transform, "방어 훈련", x, careManager, ButtonAction.Train, 1, null);
            x += step;
            CreateButton(canvasGo.transform, "체력 훈련", x, careManager, ButtonAction.Train, 2, null);
            x += step;

            for (int i = 0; i < items.Count; i++)
            {
                CreateButton(canvasGo.transform, items[i].displayName, x, careManager, ButtonAction.UseItem, 0, items[i]);
                x += step;
            }

            CreateButton(canvasGo.transform, "저장", x, careManager, ButtonAction.Save, 0, null);

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
            root.creatureController = creatureController;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private enum ButtonAction
        {
            Feed,
            Train,
            UseItem,
            Save
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
            text.font = uiFont;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.black;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            return text;
        }

        private static void CreateButton(
            Transform parent, string label, float x,
            CareManager careManager, ButtonAction action, int statIndex, ItemData item)
        {
            var go = new GameObject("Button_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(x, 60f);
            rect.sizeDelta = new Vector2(180f, 70f);

            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.9f);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var text = labelGo.GetComponent<Text>();
            text.font = uiFont;
            text.fontSize = 24;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.black;
            text.text = label;

            var button = go.GetComponent<Button>();

            switch (action)
            {
                case ButtonAction.Feed:
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, careManager.DoFeed);
                    break;

                case ButtonAction.Train:
                    UnityEditor.Events.UnityEventTools.AddIntPersistentListener(
                        button.onClick, careManager.DoTrain, statIndex);
                    break;

                case ButtonAction.UseItem:
                    UnityEditor.Events.UnityEventTools.AddObjectPersistentListener(
                        button.onClick, careManager.DoUseItem, item);
                    break;

                case ButtonAction.Save:
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, careManager.DoSave);
                    break;
            }
        }
    }
}
```

> `UnityEventTools.AddObjectPersistentListener<T>` 는 제네릭 인자를 추론하므로
> `careManager.DoUseItem` (`UnityAction<ItemData>`) 과 `item` 이 짝을 이룬다.

- [x] **Step 3: 배치모드로 부트스트랩 실행**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.2.8f1/Editor/Unity.exe" \
  -batchmode -quit \
  -projectPath "C:/Users/이지민/UnityProjects/MCPTest" \
  -executeMethod FarmCreature.EditorTools.GameBootstrap.BuildAll \
  -logFile "C:/Users/이지민/AppData/Local/Temp/claude/unity-bootstrap.log"
```

Expected: 종료 코드 `0`, 로그에 `[FarmCreature] Build All 완료`.

- [x] **Step 4: 생성물 확인**

```bash
ls -R "C:/Users/이지민/UnityProjects/MCPTest/Assets/GameData" \
      "C:/Users/이지민/UnityProjects/MCPTest/Assets/Scenes"
```

Expected: `Sprigling.asset`, `Wildbeast.asset`, `BlossomGuardian.asset`,
`HeartyFeed.asset`, `IronGrain.asset`, `BloomStone.asset`,
`SpeciesDatabase.asset`, `ItemDatabase.asset`, `FarmScene.unity` (+ 각 `.meta`).

- [x] **Step 5: 테스트 재실행 — 회귀 없음 확인**

Expected: 종료 코드 `0`, `total="46" passed="46" failed="0"`.

- [x] **Step 6: 체크포인트**

에셋 8개 + 씬 1개가 생성되고, 씬이 빌드 설정에 등록됐으며, 기존 테스트가 전부 통과한다.

---

## Task 9: 수동 플레이 검증

자동 테스트로 덮이지 않는 PRD 8장 완료 조건을 눈으로 확인한다. **사용자가 수행한다.**

**Files:** 없음 (검증만)

- [ ] **Step 1: 에디터에서 씬 열기**

Unity Hub → MCPTest 프로젝트 열기 → `Assets/Scenes/FarmScene.unity` 더블클릭 → Play.

- [ ] **Step 2: 체크리스트 확인**

- [ ] 하늘색 배경에 연녹색 농장 바닥이 보인다
- [ ] 파란 사각형(플레이어)이 WASD/화살표로 움직이고 경계 밖으로 안 나간다
- [ ] 연두색 원(스프리글링)이 스스로 배회한다
- [ ] 좌상단 상태 패널에 한글이 깨지지 않고 나온다
- [ ] "먹이주기"를 누르면 포만도/친밀도/경험치가 오른다
- [ ] "공격 훈련"을 반복하면 포만도가 떨어지고 20 미만에서 거부 메시지가 뜬다
- [ ] 먹이를 주면 다시 훈련할 수 있다
- [ ] 훈련 위주로 진행하면 주황색 큰 원(**와일드비스트**)으로 진화한다
- [ ] "저장" 후 Play 중지 → 다시 Play 하면 진행 상태가 복원된다

- [ ] **Step 3: 두 번째 진화 경로 확인**

세이브 파일을 지우고 다시 시작한 뒤(`%USERPROFILE%\AppData\LocalLow\DefaultCompany\MCPTest\save.json`),
공격을 15 미만으로 유지한 채 먹이/방어훈련으로 경험치 100을 채운다.
`bloom_stone` 을 쓰지 않고 남겨두면 분홍색 큰 원(**블로섬 가디언**)으로 진화해야 한다.

- [ ] **Step 4: 체크포인트**

PRD 8장의 완료 조건이 전부 충족된다. 어긋나는 항목이 있으면 해당 태스크로 돌아간다.

---

## 실행 기록 — 계획에서 벗어난 부분 (2026-08-06)

계획대로 되지 않아 실제로 고친 것들. 코드와 설계 문서에는 이미 반영돼 있다.

| # | 계획 | 실제 | 사유 |
|---|---|---|---|
| 1 | `com.unity.test-framework` `1.5.1` | `1.6.0` | 에디터 6000.2.8f1 내장본의 실제 버전 |
| 2 | 누적 테스트 수 30 / 39 / 47 | **29 / 38 / 46** | 계획 작성 시 산술 오차. 실제 테스트는 전부 통과 |
| 3 | Task 2~6 를 태스크별로 red → green | 파일을 모아 쓰고 **한 번에 검증** | 배치모드 1회에 1~2분. red 상태가 전부 "타입 없음" 컴파일 에러라 정보 이득이 낮다고 판단. 최종 46/46 통과로 확인 |
| 4 | 아이템 버튼에 `AddObjectPersistentListener(…, ItemData)` | `AddIntPersistentListener(…, index)` + `CareManager.DoUseItem(int)` | Object 인자 등록이 `m_MethodName` 공란인 빈 항목만 남겨 **버튼 3개가 무동작**이었음 |
| 5 | 에디터에서 `Text.font` 에 OS 폰트 지정 | Canvas 의 `UiFontApplier` 가 **런타임 주입** | 동적 OS 폰트는 에셋이 아니라 `m_Font: {fileID: 0}` 로 저장됨 → 한글 전부 안 보임 |
| 6 | 각 태스크 끝에 커밋 | 커밋 없음 (검증 체크포인트만) | 프로젝트에 git 저장소가 없고 사용자 승인 없음 |

4번과 5번은 씬 YAML을 직접 검사해서 발견했다. 배치모드 종료 코드만 봤으면 둘 다 놓쳤을 것이다 —
**생성물 검증을 종료 코드에 의존하지 말 것.**

---

## Self-Review

**1. 스펙 커버리지**

| 설계 문서 절 | 담당 태스크 |
|---|---|
| 1. 아키텍처 (3계층 분리) | Task 2 / 3~6 / 7 |
| 2. 세계관·배경 | Task 8 (에셋 값), Task 9 (확인) |
| 3. 진화 시스템 | Task 5 (판정), Task 7 CareManager (실행), Task 8 (조건 데이터) |
| 4. 액션 데이터 흐름 | Task 4 |
| 5. 세이브 시스템 | Task 6 (직렬화), Task 7 SaveSystem (IO) |
| 6. 씬 구성 | Task 8 |
| 7. 폴더·어셈블리 | Task 1, 7, 8 |
| 8. 테스트 전략 | Task 1 (파이프라인), Task 3~6 (테스트) |

PRD 8장 완료 조건 9개는 Task 9의 체크리스트와 1:1 대응한다. 미커버 요구사항 없음.

**2. 플레이스홀더 스캔**

"TBD" / "적절히 처리" / "테스트 작성" 류 없음. 모든 코드 단계가 실제 코드 블록을 포함한다.

**3. 타입 일관성 확인**

- `CreatureInstance.Restore` 는 Task 3에서 정의되고 Task 6 `SaveSerializer.TryRestore` 에서 쓰인다 ✓
- `Inventory.ItemIds` 는 Task 4에서 `IReadOnlyList<string>` 로 정의되고 Task 6/7에서 `.Count` / 인덱서로 쓰인다 ✓
- `CareManager.DoTrain(int)` 은 uGUI 버튼 제약 때문에 `StatType` 이 아닌 `int` 를 받는다 —
  Task 7 정의와 Task 8 `AddIntPersistentListener` 사용이 일치한다 ✓
- `EvolutionEvaluator.TryEvolve` 는 종을 바꾸지 않고 반환만 한다 — Task 5 테스트와
  Task 7 `CareManager.Apply` 의 교체 로직이 일치한다 ✓
- `PlaceholderSprites` 는 Task 7(런타임)에 있고 Task 8(에디터)에서 호출한다.
  에디터 asmdef 가 `FarmCreature.Runtime` 을 참조하므로 접근 가능 ✓
- `GameHud.Bind(CreatureInstance, Inventory)` / `CareManager.Bind(4개 인자)` 시그니처가
  Task 7 정의와 `GameRoot.Start` 호출부에서 일치한다 ✓

**4. 알려진 리스크**

| 리스크 | 완화 |
|---|---|
| 배치모드 첫 실행이 느리다 (패키지 임포트 + 컴파일) | 타임아웃 600초. Task 1에서 한 번 겪고 나면 이후는 빠르다 |
| Unity 에디터가 켜져 있으면 배치모드가 프로젝트 잠금으로 실패 | Task 1 Step 1에서 확인. 각 실행 전 재확인 |
| `Font.CreateDynamicFontFromOSFont` 가 배치모드에서 null 반환 가능 | "맑은 고딕" 실패 시 "Malgun Gothic" 폴백. 둘 다 실패하면 Task 9에서 글자가 안 보이므로 즉시 발견됨 |
| `UnityEventTools` 로 연결한 버튼이 씬 저장 시 유실 | Task 9 Step 2에서 버튼 동작으로 직접 확인 |
