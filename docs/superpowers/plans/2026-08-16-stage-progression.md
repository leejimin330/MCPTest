# 맵/스테이지 진행 구조 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 전투터에서 맵을 고르고 그 안의 스테이지를 순서대로 깨 나간다. 스테이지를 늘려도 `BuildAll` 이 필요 없다.

**Architecture:** 규칙은 순수 C# 둘에 나눠 둔다 — `StageProgress`(무엇을 깼는가)와 `StageUnlockEvaluator`(열렸는가·왜 잠겼는가). 해금은 **조건 목록**이라 나중에 드랍 아이템이 종류 하나 추가로 들어온다. 진행도는 **stageId 문자열**로 기록해 스테이지를 끼워 넣어도 밀리지 않는다. 화면은 **고정 6칸을 맵 모드와 스테이지 모드가 번갈아 쓴다.**

**Tech Stack:** Unity 6000.2.8f1 (2D), C#, NUnit(EditMode/PlayMode), uGUI. 씬은 `GameBootstrap.BuildAll()` 이 전량 생성한다.

**Spec:** `docs/superpowers/specs/2026-08-16-stage-progression-design.md`

## Global Constraints

- **씬을 손으로 편집하지 않는다.** `GameBootstrap.cs` 를 고치고 `BuildAll` 로 재생성한다.
- **UI 행·버튼을 런타임에 생성하지 않는다** (함정 3.1). 행은 **항상 6개 고정**이다 — 데이터 개수와 무관하다.
- **uGUI persistent listener 는 무인자 또는 int 인자만 쓴다** (함정 3.6).
- **패널은 배선이 끝난 뒤에 닫아서 저장한다** (함정 3.8).
- **배치모드 실행은 Unity 에디터가 닫혀 있어야 한다** (함정 3.5).
- **종료 코드 0 을 믿지 않는다.** `results.xml` 의 숫자를 직접 읽는다.
- Core 계층은 UI·MonoBehaviour 에 의존하지 않는다. `ScriptableObject` 인자는 허용된다.
- 표시 문자열은 전부 `PanelLabels` 에 모은다.
- **진행도는 `stageId` 문자열로 기록한다. 인덱스 금지** (설계 1.1).
- **`stageId` 는 전 맵을 통틀어 유일해야 한다** — 중복이면 조용히 틀린다.
- **밸런스 수치는 건드리지 않는다.** 새로 까는 스테이지 4개의 적 구성은 자리를 채운 씨앗이다.
- 브랜치: `feature/stage-progression` (Task 1 전에 만든다).

## 사전 준비

```powershell
$UNITY = "C:\Program Files\Unity\Hub\Editor\6000.2.8f1\Editor\Unity.exe"
$PROJ  = "C:\Users\이지민\UnityProjects\MCPTest"
$LOG   = "$env:TEMP\mcptest"
New-Item -ItemType Directory -Force $LOG | Out-Null
```

**테스트 실행 헬퍼** — Unity 는 제어를 일찍 돌려주므로 `Start-Process -Wait` 로 기다린다.

```powershell
function Run-Tests($name, $filter, $platform = "EditMode") {
  $xml = "$LOG\$name.xml"
  Remove-Item $xml -ErrorAction SilentlyContinue
  $a = @('-batchmode','-runTests','-projectPath',$PROJ,'-testPlatform',$platform,
         '-testResults',$xml,'-logFile',"$LOG\$name.log")
  if ($filter) { $a += @('-testFilter',$filter) }
  $p = Start-Process -FilePath $UNITY -ArgumentList $a -PassThru -Wait
  "EXIT=$($p.ExitCode)"
  if (Test-Path $xml) {
    [xml]$x = Get-Content $xml
    "total=$($x.'test-run'.total) passed=$($x.'test-run'.passed) failed=$($x.'test-run'.failed)"
    $x.SelectNodes("//test-case[@result='Failed']") | ForEach-Object {
      "FAILED: $($_.name)"; $_.SelectSingleNode("failure/message").InnerText }
  } else {
    "results.xml 없음 — 컴파일 실패 가능"
    Select-String -Path "$LOG\$name.log" -Pattern 'error CS' | Select-Object -First 6 |
      ForEach-Object { $_.Line.Trim() }
  }
}
```

## 파일 구조

| 파일 | 책임 | 태스크 |
|---|---|---|
| `Runtime/Core/StageProgress.cs` | 무엇을 깼는가. 순수 C# | 1 |
| `Runtime/Data/StageUnlock.cs` | 해금 조건 한 줄 (직렬화 데이터) | 2 |
| `Runtime/Data/StageMap.cs` | 맵 = 전장 + 스테이지 목록 | 2 |
| `Runtime/Data/StageMapDatabase.cs` | 맵 목록 | 2 |
| `Runtime/Data/BattleEncounter.cs` | 스테이지. `stageId`·`unlock` 이 는다 | 2 |
| `Runtime/Core/StageUnlockEvaluator.cs` | 열렸는가 / 왜 잠겼는가 | 3 |
| `Runtime/Core/GameState.cs` | `Progress` 가 는다 | 4 |
| `Runtime/Core/SaveData.cs` · `SaveSerializer.cs` | v4 + v3 승격 | 4 |
| `Runtime/Core/BattleHandoff.cs` | 요청에 `Field`, 결과에 `StageId` | 5 |
| `Runtime/Presentation/GameRoot.cs` | 승리 시 진행도 기록 (한 곳) | 5 |
| `Runtime/Presentation/BattleSceneRoot.cs` | 전장 선택 순서 · 결과에 `StageId` | 5 |
| `Runtime/Presentation/PanelLabels.cs` | 맵 행·스테이지 행·페이지 문자열 | 6 |
| `Runtime/Presentation/BattlePanel.cs` | 2단계 화면. 규칙은 코어에 | 7 |
| `Editor/SceneIntegrityCheck.cs` | 고정 6칸·배선 검사 | 8 |
| `Editor/GameBootstrap.cs` | 에셋 씨앗 + 씬 배선 | 9 |
| `Runtime/Data/BattleEncounterDatabase.cs` | **삭제** | 9 |

## 태스크별 누적 테스트 수

| 태스크 | 새 테스트 | 누적 |
|---|---|---|
| 시작 | — | 205 |
| 1 | 6 | 211 |
| 2 | 2 | 213 |
| 3 | 9 | 222 |
| 4 | 5 | 227 |
| 5 | 2 | 229 |
| 6 | 5 | 234 |
| 7~9 | 0 (표현·에디터 계층) | 234 |
| 10 | 4 | **238** |

---

### Task 0: 브랜치

- [ ] **Step 1: 에디터를 닫고 브랜치를 만든다**

```powershell
Get-Process Unity -ErrorAction SilentlyContinue | ForEach-Object { $_.CloseMainWindow() | Out-Null }
Start-Sleep -Seconds 5
cd $PROJ
git checkout -b feature/stage-progression
git branch --show-current
```

기대: `feature/stage-progression`. 작업 트리가 깨끗해야 한다.

---

### Task 1: `StageProgress` — 무엇을 깼는가

**Files:**
- Create: `Assets/Scripts/Runtime/Core/StageProgress.cs`
- Test: `Assets/Scripts/Tests/EditMode/StageProgressTests.cs`

**Interfaces:**
- Consumes: 없음
- Produces: `FarmCreature.Core.StageProgress` — `IReadOnlyList<string> ClearedStageIds` / `bool IsCleared(string)` / `bool MarkCleared(string)` / 생성자 `StageProgress()` 와 `StageProgress(IEnumerable<string>)`. Task 3·4·5 가 쓴다

- [ ] **Step 1: 실패하는 테스트를 쓴다**

```csharp
using System.Collections.Generic;
using FarmCreature.Core;
using NUnit.Framework;

namespace FarmCreature.Tests
{
    public class StageProgressTests
    {
        [Test]
        public void NewProgress_HasNothingCleared()
        {
            var progress = new StageProgress();

            Assert.AreEqual(0, progress.ClearedStageIds.Count);
            Assert.IsFalse(progress.IsCleared("field_1"));
        }

        [Test]
        public void MarkCleared_RecordsIt()
        {
            var progress = new StageProgress();

            Assert.IsTrue(progress.MarkCleared("field_1"), "처음 깬 것이면 true");
            Assert.IsTrue(progress.IsCleared("field_1"));
            Assert.AreEqual(1, progress.ClearedStageIds.Count);
        }

        [Test]
        public void MarkCleared_Twice_ChangesNothing()
        {
            var progress = new StageProgress();
            progress.MarkCleared("field_1");

            Assert.IsFalse(progress.MarkCleared("field_1"), "이미 깬 것이면 false");
            Assert.AreEqual(1, progress.ClearedStageIds.Count, "중복 기록되면 안 된다");
        }

        [Test]
        public void MarkCleared_RejectsEmptyId()
        {
            var progress = new StageProgress();

            Assert.IsFalse(progress.MarkCleared(null));
            Assert.IsFalse(progress.MarkCleared(""));
            Assert.IsFalse(progress.MarkCleared("   "));
            Assert.AreEqual(0, progress.ClearedStageIds.Count,
                "무엇을 깼는지 알 수 없는 기록은 남기지 않는다");
        }

        [Test]
        public void ClearedOrder_IsPreserved()
        {
            var progress = new StageProgress();
            progress.MarkCleared("field_2");
            progress.MarkCleared("field_1");

            Assert.AreEqual("field_2", progress.ClearedStageIds[0], "깬 차례대로 남는다");
            Assert.AreEqual("field_1", progress.ClearedStageIds[1]);
        }

        [Test]
        public void Constructor_FiltersEmptyAndDuplicate()
        {
            var progress = new StageProgress(
                new List<string> { "field_1", "", "field_1", null, "field_2" });

            Assert.AreEqual(2, progress.ClearedStageIds.Count,
                "손으로 고친 세이브가 진행도를 오염시키면 안 된다");
            Assert.IsTrue(progress.IsCleared("field_1"));
            Assert.IsTrue(progress.IsCleared("field_2"));
        }
    }
}
```

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```powershell
Run-Tests "t1" "FarmCreature.Tests.StageProgressTests"
```

기대: **0 이 아니다.** `StageProgress` 를 찾을 수 없다는 컴파일 오류.

- [ ] **Step 3: 최소 구현을 쓴다**

`Assets/Scripts/Runtime/Core/StageProgress.cs`:

```csharp
using System.Collections.Generic;

namespace FarmCreature.Core
{
    /// <summary>
    /// 어떤 스테이지를 깼는가. <b>인덱스가 아니라 id 로 기록한다</b> —
    /// 인덱스로 두면 스테이지를 중간에 끼워 넣는 순간 진행도가 통째로 밀려
    /// 엉뚱한 스테이지가 깬 것이 된다 (설계 1.1).
    ///
    /// 깬 <b>차례를 보존한다.</b> 세이브가 배열이라 그대로 실린다.
    /// </summary>
    public class StageProgress
    {
        private readonly List<string> cleared = new List<string>();

        public StageProgress()
        {
        }

        /// <summary>세이브에서 복원할 때 쓴다. 빈 id 와 중복은 걸러낸다.</summary>
        public StageProgress(IEnumerable<string> clearedStageIds)
        {
            if (clearedStageIds == null)
            {
                return;
            }

            foreach (var id in clearedStageIds)
            {
                MarkCleared(id);
            }
        }

        public IReadOnlyList<string> ClearedStageIds
        {
            get { return cleared; }
        }

        public bool IsCleared(string stageId)
        {
            return !IsBlank(stageId) && cleared.Contains(stageId);
        }

        /// <summary>처음 깬 것이면 true. 이미 깼거나 id 가 비었으면 아무것도 안 하고 false.</summary>
        public bool MarkCleared(string stageId)
        {
            if (IsBlank(stageId) || cleared.Contains(stageId))
            {
                return false;
            }

            cleared.Add(stageId);
            return true;
        }

        private static bool IsBlank(string value)
        {
            return string.IsNullOrEmpty(value) || value.Trim().Length == 0;
        }
    }
}
```

- [ ] **Step 4: 테스트가 통과하는지 확인한다**

```powershell
Run-Tests "t1" "FarmCreature.Tests.StageProgressTests"
```

기대: 종료 코드 **0**, `total="6" passed="6"`.

- [ ] **Step 5: 커밋**

```powershell
git add Assets/Scripts/Runtime/Core/StageProgress.cs Assets/Scripts/Tests/EditMode/StageProgressTests.cs
git commit -m "feat: StageProgress — 진행도를 stageId 로 기록한다"
```

`.meta` 가 생겼으면 함께 담는다 (`git status` 확인).

---

### Task 2: 데이터 — `StageUnlock` · `StageMap` · `StageMapDatabase`

**Files:**
- Create: `Assets/Scripts/Runtime/Data/StageUnlock.cs`
- Create: `Assets/Scripts/Runtime/Data/StageMap.cs`
- Create: `Assets/Scripts/Runtime/Data/StageMapDatabase.cs`
- Modify: `Assets/Scripts/Runtime/Data/BattleEncounter.cs`
- Modify: `Assets/Scripts/Tests/EditMode/TestFixtures.cs` (픽스처 2개 추가)
- Test: `Assets/Scripts/Tests/EditMode/BattleDataTests.cs` (파일 끝)

**Interfaces:**
- Consumes: 없음
- Produces:
  - `FarmCreature.Data.StageUnlockKind` — `PreviousStageCleared` / `StageCleared` / `RequiresItem`
  - `FarmCreature.Data.StageUnlock` — 공개 필드 `kind` / `stageId` / `itemId` / `count`
  - `FarmCreature.Data.StageMap` — `displayName` / `field`(`BattleMapData`) / `stages`(`List<BattleEncounter>`), `int Count`, `BattleEncounter Get(int)`
  - `FarmCreature.Data.StageMapDatabase` — `maps`(`List<StageMap>`), `int Count`, `StageMap Get(int)`
  - `BattleEncounter.stageId`(`string`) / `BattleEncounter.unlock`(`List<StageUnlock>`)
  - `TestFixtures.MakeStage(...)` / `TestFixtures.MakeStageMap(...)`
  - Task 3·6·7·9 가 전부 쓴다

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`BattleDataTests.cs` 의 마지막 `}` 두 개 **앞에** 붙인다.

```csharp
        // ---------- 맵 / 스테이지 ----------

        [Test]
        public void NewEncounter_HasNoStageIdAndNoUnlockConditions()
        {
            var stage = ScriptableObject.CreateInstance<BattleEncounter>();

            Assert.IsTrue(string.IsNullOrEmpty(stage.stageId), "id 는 에셋마다 지어준다");
            Assert.IsNotNull(stage.unlock, "조건 목록은 null 이 아니라 빈 목록이다");
            Assert.AreEqual(0, stage.unlock.Count, "조건이 없으면 항상 열림이다");
        }

        [Test]
        public void StageMap_CountAndGet_AreSafeAtEdges()
        {
            var rat = TestFixtures.MakeEnemy("rat", maxHp: 8, attack: 3, defense: 1);
            var first = TestFixtures.MakeStage("field_1", "들판 1", null, (rat, 2));
            var map = TestFixtures.MakeStageMap("들쥐 들판", first);

            Assert.AreEqual(1, map.Count);
            Assert.AreSame(first, map.Get(0));
            Assert.IsNull(map.Get(-1), "범위 밖은 예외가 아니라 null 이다");
            Assert.IsNull(map.Get(1));
        }
```

`BattleDataTests.cs` 상단에 `using UnityEngine;` 가 없으면 추가한다.

**픽스처를 `TestFixtures.cs` 에 더한다** (`MakeEncounterDatabase` 뒤):

```csharp
        /// <summary>스테이지 픽스처. 해금 조건을 직접 넘긴다 (null 이면 항상 열림).</summary>
        public static BattleEncounter MakeStage(
            string stageId, string displayName, List<StageUnlock> unlock,
            params (EnemyData enemy, int count)[] entries)
        {
            var so = MakeEncounter(displayName, entries);
            so.stageId = stageId;
            so.map = null;                     // 전장은 맵이 정한다 (설계 2.1)
            so.unlock = unlock ?? new List<StageUnlock>();
            return so;
        }

        public static StageMap MakeStageMap(string displayName, params BattleEncounter[] stages)
        {
            var so = ScriptableObject.CreateInstance<StageMap>();
            so.displayName = displayName;
            so.field = MakeBattleMap();
            so.stages = new List<BattleEncounter>(stages);
            return so;
        }

        public static StageMapDatabase MakeStageMapDatabase(params StageMap[] maps)
        {
            var so = ScriptableObject.CreateInstance<StageMapDatabase>();
            so.maps = new List<StageMap>(maps);
            return so;
        }

        /// <summary>해금 조건 한 줄. 안 쓰는 필드는 비워 둔다.</summary>
        public static StageUnlock Unlock(
            StageUnlockKind kind, string stageId = null, string itemId = null, int count = 1)
        {
            return new StageUnlock
            {
                kind = kind,
                stageId = stageId,
                itemId = itemId,
                count = count
            };
        }
```

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```powershell
Run-Tests "t2" "FarmCreature.Tests.BattleDataTests"
```

기대: **0 이 아니다.** `StageMap`·`StageUnlock`·`stageId` 를 찾을 수 없다는 컴파일 오류.

- [ ] **Step 3: 데이터 클래스를 쓴다**

`Assets/Scripts/Runtime/Data/StageUnlock.cs`:

```csharp
namespace FarmCreature.Data
{
    /// <summary>
    /// 스테이지가 열리는 조건의 종류.
    ///
    /// <b>append-only 다.</b> 값이 에셋에 정수로 직렬화되므로 중간에 끼워 넣으면
    /// 이미 만든 조건들이 다른 종류로 바뀐다 (<c>BuildingKind</c> 와 같은 이유).
    /// </summary>
    public enum StageUnlockKind
    {
        /// <summary>같은 맵에서 바로 앞 스테이지를 깼는가. 첫 스테이지면 항상 만족한다.</summary>
        PreviousStageCleared = 0,

        /// <summary>지정한 <c>stageId</c> 를 깼는가. 다른 맵의 스테이지도 가리킬 수 있다.</summary>
        StageCleared = 1,

        /// <summary>
        /// <c>itemId</c> 를 <c>count</c> 개 이상 가졌는가.
        /// 나중에 들어올 드랍 아이템(지도·열쇠)이 이 종류로 들어온다 (설계 1.2).
        /// </summary>
        RequiresItem = 2
    }

    /// <summary>해금 조건 한 줄. 목록 전체가 AND 로 묶인다.</summary>
    [System.Serializable]
    public class StageUnlock
    {
        public StageUnlockKind kind = StageUnlockKind.PreviousStageCleared;

        [UnityEngine.Tooltip("StageCleared 일 때 쓴다.")]
        public string stageId;

        [UnityEngine.Tooltip("RequiresItem 일 때 쓴다.")]
        public string itemId;

        [UnityEngine.Tooltip("RequiresItem 일 때 필요한 개수.")]
        public int count = 1;
    }
}
```

`Assets/Scripts/Runtime/Data/StageMap.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace FarmCreature.Data
{
    /// <summary>
    /// 전투터에서 먼저 고르는 것. <b>맵이 스테이지를 담는다</b> —
    /// 스테이지가 맵을 가리키지 않는다 (설계 2.2). 맵을 한 번만 지정하면 되고,
    /// 화면 구조("맵 고르고 → 스테이지 고르고")와 데이터가 같은 모양이 된다.
    ///
    /// <b><see cref="stages"/> 의 순서가 곧 해금 순서다.</b>
    /// </summary>
    [CreateAssetMenu(fileName = "StageMap", menuName = "FarmCreature/Stage Map")]
    public class StageMap : ScriptableObject
    {
        public string displayName = "들판";

        [Tooltip("이 맵의 전장 기하. 전투 씬이 이것으로 유닛을 세운다.")]
        public BattleMapData field;

        [Tooltip("이 맵의 스테이지. 위에서부터 순서대로 해금된다.")]
        public List<BattleEncounter> stages = new List<BattleEncounter>();

        public int Count
        {
            get { return stages != null ? stages.Count : 0; }
        }

        /// <summary>범위 밖이면 <c>null</c>. 화면이 빈 칸을 그릴 수 있어야 한다.</summary>
        public BattleEncounter Get(int index)
        {
            if (stages == null || index < 0 || index >= stages.Count)
            {
                return null;
            }

            return stages[index];
        }

        /// <summary>깬 스테이지 수. 맵 행에 <c>2/4</c> 로 보여준다.</summary>
        public int ClearedCount(FarmCreature.Core.StageProgress progress)
        {
            if (progress == null)
            {
                return 0;
            }

            int done = 0;
            for (int i = 0; i < Count; i++)
            {
                var stage = Get(i);
                if (stage != null && progress.IsCleared(stage.stageId))
                {
                    done++;
                }
            }

            return done;
        }
    }
}
```

`Assets/Scripts/Runtime/Data/StageMapDatabase.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace FarmCreature.Data
{
    /// <summary>전투터가 보여줄 맵 목록. <c>BattleEncounterDatabase</c> 를 대체한다.</summary>
    [CreateAssetMenu(fileName = "StageMapDatabase", menuName = "FarmCreature/Stage Map Database")]
    public class StageMapDatabase : ScriptableObject
    {
        public List<StageMap> maps = new List<StageMap>();

        public int Count
        {
            get { return maps != null ? maps.Count : 0; }
        }

        public StageMap Get(int index)
        {
            if (maps == null || index < 0 || index >= maps.Count)
            {
                return null;
            }

            return maps[index];
        }
    }
}
```

- [ ] **Step 4: `BattleEncounter` 에 필드 둘을 더한다**

`displayName` 선언 **앞에** `stageId` 를, `map` 선언 **뒤에** `unlock` 을 넣는다.

```csharp
        [Tooltip("진행도가 이 id 로 기록된다. 전 맵을 통틀어 유일해야 한다. 한 번 정하면 바꾸지 말 것.")]
        public string stageId;
```

```csharp
        [Tooltip("이 스테이지가 열리는 조건. 전부 AND. 비어 있으면 항상 열림.")]
        public List<StageUnlock> unlock = new List<StageUnlock>();
```

`map` 필드의 Tooltip 을 고친다 — 이제 평소에는 비워 둔다.

```csharp
        [Tooltip("이 스테이지만 다른 전장을 쓰고 싶을 때. 비어 있으면 맵(StageMap.field)이 정한다.")]
        public BattleMapData map;
```

- [ ] **Step 5: 테스트가 통과하는지 확인한다**

```powershell
Run-Tests "t2" "FarmCreature.Tests.BattleDataTests"
```

기대: 종료 코드 **0**, `total="10" passed="10"` (기존 8 + 2).

- [ ] **Step 6: 커밋**

```powershell
git add Assets/Scripts/Runtime/Data/ Assets/Scripts/Tests/EditMode/
git commit -m "feat: 맵/스테이지 데이터 — 맵이 스테이지를 담고, 해금은 조건 목록이다"
```

---

### Task 3: `StageUnlockEvaluator` — 열렸는가, 왜 잠겼는가

**Files:**
- Create: `Assets/Scripts/Runtime/Core/StageUnlockEvaluator.cs`
- Test: `Assets/Scripts/Tests/EditMode/StageUnlockEvaluatorTests.cs`

**Interfaces:**
- Consumes: Task 1 의 `StageProgress`, Task 2 의 `StageMap`·`StageUnlock`·픽스처, 기존 `Inventory`·`ItemDatabase`
- Produces: `FarmCreature.Core.StageUnlockEvaluator.IsUnlocked(StageMap, int, StageProgress, Inventory) → bool` 와 `LockReason(StageMap, int, StageProgress, Inventory, ItemDatabase) → string`. Task 6·7 이 쓴다

- [ ] **Step 1: 실패하는 테스트를 쓴다**

```csharp
using System.Collections.Generic;
using FarmCreature.Core;
using FarmCreature.Data;
using NUnit.Framework;

namespace FarmCreature.Tests
{
    public class StageUnlockEvaluatorTests
    {
        private EnemyData rat;

        [SetUp]
        public void SetUp()
        {
            rat = TestFixtures.MakeEnemy("rat", maxHp: 8, attack: 3, defense: 1);
        }

        private BattleEncounter Stage(string id, params StageUnlock[] unlock)
        {
            return TestFixtures.MakeStage(id, id, new List<StageUnlock>(unlock), (rat, 1));
        }

        private static StageUnlock Previous()
        {
            return TestFixtures.Unlock(StageUnlockKind.PreviousStageCleared);
        }

        [Test]
        public void NoConditions_IsAlwaysUnlocked()
        {
            var map = TestFixtures.MakeStageMap("맵", Stage("a"));

            Assert.IsTrue(StageUnlockEvaluator.IsUnlocked(
                map, 0, new StageProgress(), new Inventory()));
        }

        [Test]
        public void PreviousStageCleared_BlocksUntilPreviousIsDone()
        {
            var map = TestFixtures.MakeStageMap("맵", Stage("a"), Stage("b", Previous()));
            var progress = new StageProgress();

            Assert.IsFalse(StageUnlockEvaluator.IsUnlocked(map, 1, progress, new Inventory()));

            progress.MarkCleared("a");
            Assert.IsTrue(StageUnlockEvaluator.IsUnlocked(map, 1, progress, new Inventory()));
        }

        [Test]
        public void PreviousStageCleared_OnFirstStage_IsUnlocked()
        {
            // 데이터 실수로 첫 스테이지에 붙어도 맵 전체가 막히면 안 된다 (설계 3.2).
            var map = TestFixtures.MakeStageMap("맵", Stage("a", Previous()));

            Assert.IsTrue(StageUnlockEvaluator.IsUnlocked(
                map, 0, new StageProgress(), new Inventory()));
        }

        [Test]
        public void StageCleared_LooksAtTheNamedStage()
        {
            var gate = TestFixtures.Unlock(StageUnlockKind.StageCleared, stageId: "boss");
            var map = TestFixtures.MakeStageMap("맵", Stage("a"), Stage("b", gate));
            var progress = new StageProgress();

            Assert.IsFalse(StageUnlockEvaluator.IsUnlocked(map, 1, progress, new Inventory()));

            progress.MarkCleared("boss");
            Assert.IsTrue(StageUnlockEvaluator.IsUnlocked(map, 1, progress, new Inventory()));
        }

        [Test]
        public void RequiresItem_ChecksInventoryCount()
        {
            var needsKey = TestFixtures.Unlock(
                StageUnlockKind.RequiresItem, itemId: "old_key", count: 2);
            var map = TestFixtures.MakeStageMap("맵", Stage("a", needsKey));
            var inventory = new Inventory();

            Assert.IsFalse(StageUnlockEvaluator.IsUnlocked(map, 0, new StageProgress(), inventory));

            inventory.Add("old_key", 1);
            Assert.IsFalse(StageUnlockEvaluator.IsUnlocked(map, 0, new StageProgress(), inventory),
                "개수가 모자라면 아직 잠긴다");

            inventory.Add("old_key", 1);
            Assert.IsTrue(StageUnlockEvaluator.IsUnlocked(map, 0, new StageProgress(), inventory));
        }

        [Test]
        public void Conditions_AreCombinedWithAnd()
        {
            var needsKey = TestFixtures.Unlock(
                StageUnlockKind.RequiresItem, itemId: "old_key", count: 1);
            var map = TestFixtures.MakeStageMap("맵", Stage("a"), Stage("b", Previous(), needsKey));
            var progress = new StageProgress();
            var inventory = new Inventory();

            progress.MarkCleared("a");
            Assert.IsFalse(StageUnlockEvaluator.IsUnlocked(map, 1, progress, inventory),
                "하나라도 안 맞으면 잠긴다");

            inventory.Add("old_key", 1);
            Assert.IsTrue(StageUnlockEvaluator.IsUnlocked(map, 1, progress, inventory));
        }

        [Test]
        public void LockReason_IsNullWhenUnlocked()
        {
            var map = TestFixtures.MakeStageMap("맵", Stage("a"));

            Assert.IsNull(StageUnlockEvaluator.LockReason(
                map, 0, new StageProgress(), new Inventory(), null));
        }

        [Test]
        public void LockReason_NamesThePreviousStage()
        {
            var map = TestFixtures.MakeStageMap("맵", Stage("들판 1"), Stage("들판 2", Previous()));

            string reason = StageUnlockEvaluator.LockReason(
                map, 1, new StageProgress(), new Inventory(), null);

            StringAssert.Contains("들판 1", reason, "무엇을 하면 열리는지 알려줘야 한다");
        }

        [Test]
        public void LockReason_UsesItemDisplayName()
        {
            var key = TestFixtures.MakeItem("old_key", StatType.Attack, 0, "낡은 열쇠");
            var db = TestFixtures.MakeItemDatabase(key);
            var needsKey = TestFixtures.Unlock(
                StageUnlockKind.RequiresItem, itemId: "old_key", count: 1);
            var map = TestFixtures.MakeStageMap("맵", Stage("a", needsKey));

            string reason = StageUnlockEvaluator.LockReason(
                map, 0, new StageProgress(), new Inventory(), db);

            StringAssert.Contains("낡은 열쇠", reason, "id 가 아니라 사람이 읽을 이름을 쓴다");
        }
    }
}
```

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```powershell
Run-Tests "t3" "FarmCreature.Tests.StageUnlockEvaluatorTests"
```

기대: **0 이 아니다.** `StageUnlockEvaluator` 를 찾을 수 없다는 컴파일 오류.

- [ ] **Step 3: 최소 구현을 쓴다**

`Assets/Scripts/Runtime/Core/StageUnlockEvaluator.cs`:

```csharp
using FarmCreature.Data;

namespace FarmCreature.Core
{
    /// <summary>
    /// 스테이지가 열렸는가를 판정한다. 조건은 <b>전부 AND</b> 이고,
    /// 목록이 비어 있으면 항상 열린다 (설계 1.2).
    ///
    /// <see cref="StageMap"/> 과 인덱스를 함께 받는 이유: <c>PreviousStageCleared</c> 가
    /// 바로 앞 스테이지를 알아야 하는데, 그것은 스테이지 자신이 아니라
    /// <b>맵의 목록 순서</b>가 정한다 (설계 3.2).
    /// </summary>
    public static class StageUnlockEvaluator
    {
        public static bool IsUnlocked(
            StageMap map, int stageIndex, StageProgress progress, Inventory inventory)
        {
            return LockReason(map, stageIndex, progress, inventory, null) == null;
        }

        /// <summary>잠긴 이유를 사람이 읽을 문장으로. 열려 있으면 <c>null</c>.</summary>
        public static string LockReason(
            StageMap map, int stageIndex, StageProgress progress, Inventory inventory,
            ItemDatabase itemDatabase)
        {
            var stage = map != null ? map.Get(stageIndex) : null;
            if (stage == null)
            {
                return "갈 수 없는 곳이다.";
            }

            if (stage.unlock == null)
            {
                return null;
            }

            for (int i = 0; i < stage.unlock.Count; i++)
            {
                string reason = ReasonFor(stage.unlock[i], map, stageIndex, progress, inventory, itemDatabase);
                if (reason != null)
                {
                    return reason;
                }
            }

            return null;
        }

        private static string ReasonFor(
            StageUnlock condition, StageMap map, int stageIndex,
            StageProgress progress, Inventory inventory, ItemDatabase itemDatabase)
        {
            if (condition == null)
            {
                return null;
            }

            switch (condition.kind)
            {
                case StageUnlockKind.PreviousStageCleared:
                {
                    // 첫 스테이지에는 앞이 없다. 데이터 실수로 맵 전체가 막히는 것이 더 나쁘다.
                    if (stageIndex <= 0)
                    {
                        return null;
                    }

                    var previous = map.Get(stageIndex - 1);
                    if (previous == null || IsClear(progress, previous.stageId))
                    {
                        return null;
                    }

                    return previous.displayName + "을(를) 깨야 한다.";
                }

                case StageUnlockKind.StageCleared:
                {
                    if (IsClear(progress, condition.stageId))
                    {
                        return null;
                    }

                    return NameOfStage(map, condition.stageId) + "을(를) 깨야 한다.";
                }

                case StageUnlockKind.RequiresItem:
                {
                    int need = condition.count < 1 ? 1 : condition.count;
                    int have = inventory != null ? inventory.GetCount(condition.itemId) : 0;
                    if (have >= need)
                    {
                        return null;
                    }

                    return ItemName(itemDatabase, condition.itemId)
                           + (need > 1 ? " " + need + "개가" : "이(가)") + " 필요하다.";
                }
            }

            return null;
        }

        private static bool IsClear(StageProgress progress, string stageId)
        {
            return progress != null && progress.IsCleared(stageId);
        }

        /// <summary>같은 맵 안에 있으면 표시 이름을, 없으면 id 를 쓴다.</summary>
        private static string NameOfStage(StageMap map, string stageId)
        {
            for (int i = 0; i < map.Count; i++)
            {
                var stage = map.Get(i);
                if (stage != null && stage.stageId == stageId)
                {
                    return stage.displayName;
                }
            }

            return stageId;
        }

        private static string ItemName(ItemDatabase itemDatabase, string itemId)
        {
            if (itemDatabase != null)
            {
                var item = itemDatabase.Find(itemId);
                if (item != null && !string.IsNullOrEmpty(item.displayName))
                {
                    return item.displayName;
                }
            }

            return itemId;
        }
    }
}
```

- [ ] **Step 4: 테스트가 통과하는지 확인한다**

```powershell
Run-Tests "t3" $null
```

기대: 종료 코드 **0**, `total="222" passed="222" failed="0"`.

- [ ] **Step 5: 커밋**

```powershell
git add Assets/Scripts/Runtime/Core/StageUnlockEvaluator.cs Assets/Scripts/Tests/EditMode/StageUnlockEvaluatorTests.cs
git commit -m "feat: 해금 판정 — 조건 목록을 AND 로 묶고 잠긴 이유를 문장으로 준다"
```

---

### Task 4: `GameState.Progress` + 세이브 v4

**Files:**
- Modify: `Assets/Scripts/Runtime/Core/GameState.cs`
- Modify: `Assets/Scripts/Runtime/Core/SaveData.cs`
- Modify: `Assets/Scripts/Runtime/Core/SaveSerializer.cs`
- Test: `Assets/Scripts/Tests/EditMode/GameStateTests.cs` · `SaveSerializerTests.cs`

**Interfaces:**
- Consumes: Task 1 의 `StageProgress`
- Produces: `GameState.Progress`(`StageProgress`), `SaveData.clearedStageIds`(`string[]`), `SaveData.CurrentVersion = 4`. Task 5·7 이 쓴다

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`GameStateTests.cs` 파일 끝(마지막 `}` 두 개 앞):

```csharp
        [Test]
        public void NewState_StartsWithEmptyProgress()
        {
            var state = new GameState(new Roster(), new Inventory(), 0);

            Assert.IsNotNull(state.Progress, "진행도는 null 이 아니라 빈 것이다");
            Assert.AreEqual(0, state.Progress.ClearedStageIds.Count);
        }
```

`SaveSerializerTests.cs` 파일 끝:

```csharp
        // ---------- 진행도 (v4) ----------

        [Test]
        public void Progress_SurvivesRoundTrip()
        {
            var state = new GameState(new Roster(), new Inventory(), 0);
            state.Roster.Add(CreatureInstance.CreateNew(species));
            state.Progress.MarkCleared("field_1");
            state.Progress.MarkCleared("field_2");

            string json = SaveSerializer.ToJson(SaveSerializer.Capture(state, "t"));

            GameState restored;
            string error;
            Assert.IsTrue(SaveSerializer.TryRestore(
                json, speciesDb, itemDb, out restored, out error), error);

            Assert.IsTrue(restored.Progress.IsCleared("field_1"));
            Assert.IsTrue(restored.Progress.IsCleared("field_2"));
            Assert.AreEqual(2, restored.Progress.ClearedStageIds.Count);
        }

        [Test]
        public void Version3Save_IsPromoted_WithEmptyProgress()
        {
            var state = new GameState(new Roster(), new Inventory(), 42);
            state.Roster.Add(CreatureInstance.CreateNew(species));

            var data = SaveSerializer.Capture(state, "t");
            data.version = 3;
            data.clearedStageIds = null;          // v3 에는 이 필드가 없었다
            string json = SaveSerializer.ToJson(data);

            GameState restored;
            string error;
            Assert.IsTrue(SaveSerializer.TryRestore(
                json, speciesDb, itemDb, out restored, out error),
                "v3 은 버리지 않고 승격한다: " + error);

            Assert.AreEqual(42, restored.Gold, "재화는 그대로 살아야 한다");
            Assert.AreEqual(1, restored.Roster.Count, "케어도 그대로 살아야 한다");
            Assert.AreEqual(0, restored.Progress.ClearedStageIds.Count, "진행도만 빈 채로 시작한다");
        }

        [Test]
        public void Version2Save_IsStillRejected()
        {
            var state = new GameState(new Roster(), new Inventory(), 0);
            state.Roster.Add(CreatureInstance.CreateNew(species));

            var data = SaveSerializer.Capture(state, "t");
            data.version = 2;
            string json = SaveSerializer.ToJson(data);

            GameState restored;
            string error;
            Assert.IsFalse(SaveSerializer.TryRestore(
                json, speciesDb, itemDb, out restored, out error));
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void RestoredProgress_DropsBlankAndDuplicateIds()
        {
            var state = new GameState(new Roster(), new Inventory(), 0);
            state.Roster.Add(CreatureInstance.CreateNew(species));

            var data = SaveSerializer.Capture(state, "t");
            data.clearedStageIds = new[] { "field_1", "", "field_1", null, "field_2" };
            string json = SaveSerializer.ToJson(data);

            GameState restored;
            string error;
            Assert.IsTrue(SaveSerializer.TryRestore(
                json, speciesDb, itemDb, out restored, out error), error);

            Assert.AreEqual(2, restored.Progress.ClearedStageIds.Count,
                "손으로 고친 세이브가 진행도를 오염시키면 안 된다");
        }
```

> `species`·`speciesDb`·`itemDb` 는 이 픽스처가 이미 들고 있는 필드다. 파일을 열어
> `[SetUp]` 의 이름을 확인하고 다르면 맞춘다.

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```powershell
Run-Tests "t4" "FarmCreature.Tests.SaveSerializerTests"
```

기대: **0 이 아니다.** `Progress`·`clearedStageIds` 를 찾을 수 없다는 컴파일 오류.

- [ ] **Step 3: `GameState` 에 `Progress` 를 더한다**

`Target` 속성 뒤에 넣고, 생성자에서 만든다.

```csharp
        /// <summary>어떤 스테이지를 깼는가. 세이브 v4 부터 저장된다.</summary>
        public StageProgress Progress { get; private set; }
```

생성자 안, `Gold` 를 세우는 줄 근처에 한 줄 추가:

```csharp
            Progress = new StageProgress();
```

- [ ] **Step 4: `SaveData` 를 v4 로 올린다**

```csharp
        public const int CurrentVersion = 4;
```

`itemCounts` 선언 뒤에 넣는다.

```csharp
        [Tooltip("깬 스테이지의 id. 인덱스가 아니라 id 다 (설계 1.1).")]
        public string[] clearedStageIds = new string[0];
```

> `SaveData` 에 `using UnityEngine;` 가 없으면 `Tooltip` 을 빼고 주석만 남긴다.

- [ ] **Step 5: `SaveSerializer` 를 고친다**

`Capture` 의 객체 초기화에 한 줄 더한다.

```csharp
                clearedStageIds = ClearedIdsOf(state),
```

그리고 헬퍼를 더한다.

```csharp
        private static string[] ClearedIdsOf(GameState state)
        {
            if (state == null || state.Progress == null)
            {
                return new string[0];
            }

            var ids = state.Progress.ClearedStageIds;
            var copy = new string[ids.Count];
            for (int i = 0; i < ids.Count; i++)
            {
                copy[i] = ids[i];
            }

            return copy;
        }
```

`TryRestore` 의 버전 검사를 **승격 가능하게** 바꾼다.

```csharp
            // v3 은 필드가 하나 느는 것뿐이라 옮길 것이 없다 — 진행도를 빈 채로 승격한다.
            // v2 이하는 보유 케어가 단일 → 배열로 바뀌어 옮길 방법이 없다 (설계 1.4).
            const int OldestPromotableVersion = 3;

            if (data.version > SaveData.CurrentVersion || data.version < OldestPromotableVersion)
            {
                error = "지원하지 않는 세이브 버전입니다: " + data.version;
                return false;
            }
```

복원한 `GameState` 를 만든 **뒤에**, 진행도를 채운다. 재화·인벤토리를 채우는 곳 근처다.

```csharp
            // StageProgress 생성자가 빈 id 와 중복을 걸러낸다.
            var progressIds = data.clearedStageIds ?? new string[0];
            var restoredProgress = new StageProgress(progressIds);
            for (int i = 0; i < restoredProgress.ClearedStageIds.Count; i++)
            {
                state.Progress.MarkCleared(restoredProgress.ClearedStageIds[i]);
            }
```

> `GameState.Progress` 가 읽기 전용 속성이라 **새 객체로 바꾸지 않고 채워 넣는다.**
> 그래야 `GameState` 의 다른 필드처럼 생성자가 만든 것을 그대로 쓴다.

- [ ] **Step 6: 테스트가 통과하는지 확인한다**

```powershell
Run-Tests "t4" $null
```

기대: 종료 코드 **0**, `total="227" passed="227" failed="0"`.

- [ ] **Step 7: 커밋**

```powershell
git add Assets/Scripts/Runtime/Core/ Assets/Scripts/Tests/EditMode/
git commit -m "feat: 세이브 v4 — 진행도를 담고 v3 은 버리지 않고 승격한다"
```

---

### Task 5: `BattleHandoff` 와 진행도 기록 지점

**Files:**
- Modify: `Assets/Scripts/Runtime/Core/BattleHandoff.cs`
- Modify: `Assets/Scripts/Runtime/Presentation/BattleSceneRoot.cs`
- Modify: `Assets/Scripts/Runtime/Presentation/GameRoot.cs`
- Test: `Assets/Scripts/Tests/EditMode/BattleHandoffTests.cs`

**Interfaces:**
- Consumes: Task 1 의 `StageProgress`, Task 2 의 `StageMap`
- Produces: `BattleRequest.Field`(`BattleMapData`), `BattleResult.StageId`(`string`), `BattleHandoff.SetRequest(GameState, BattleEncounter, BattleMapData)`, `BattleHandoff.SetResult(BattleConclusion, int, GameState, string)`. Task 7 이 `SetRequest` 를 부른다

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`BattleHandoffTests.cs` 파일 끝(마지막 `}` 두 개 앞):

```csharp
        [Test]
        public void Request_CarriesTheField()
        {
            var field = TestFixtures.MakeBattleMap();
            var state = new GameState(new Roster(), new Inventory(), 0);

            BattleHandoff.SetRequest(state, null, field);
            var taken = BattleHandoff.TakeRequest();

            Assert.AreSame(field, taken.Field, "전장은 맵이 정한다 — 요청에 실어 보낸다");
        }

        [Test]
        public void Result_CarriesTheStageId()
        {
            var state = new GameState(new Roster(), new Inventory(), 0);

            BattleHandoff.SetResult(BattleConclusion.Victory, 10, state, "field_1");
            var taken = BattleHandoff.TakeResult();

            Assert.AreEqual("field_1", taken.StageId, "농장이 이것으로 진행도를 기록한다");
        }
```

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```powershell
Run-Tests "t5" "FarmCreature.Tests.BattleHandoffTests"
```

기대: **0 이 아니다.** 인자 개수가 안 맞는다는 컴파일 오류.

- [ ] **Step 3: `BattleHandoff` 를 고친다**

`BattleRequest` 에 필드를 더한다.

```csharp
        /// <summary>스테이지가 속한 맵의 전장. 비어 있으면 인카운터의 맵, 그것도 없으면 기본 맵.</summary>
        public BattleMapData Field;
```

`BattleResult` 에 필드를 더한다.

```csharp
        /// <summary>이긴 스테이지의 id. 농장이 진행도를 기록할 때 쓴다. 연습 전투면 빈 문자열.</summary>
        public string StageId;
```

`SetRequest` · `SetResult` 에 인자를 더한다.

```csharp
        public static void SetRequest(GameState state, BattleEncounter encounter, BattleMapData field)
        {
            request = new BattleRequest { State = state, Encounter = encounter, Field = field };
        }

        public static void SetResult(
            BattleConclusion conclusion, int goldReward, GameState state, string stageId)
        {
            result = new BattleResult
            {
                Conclusion = conclusion,
                GoldReward = goldReward < 0 ? 0 : goldReward,
                State = state,
                StageId = stageId
            };
        }
```

- [ ] **Step 4: `BattleSceneRoot` 를 고친다**

전장을 고르는 순서를 바꾼다. `StartBattle` 의 `var map = ...` 줄을 이렇게 만든다.

```csharp
            // request.Field → encounter.map → defaultMap 순으로 물러난다 (설계 2.1).
            var map = requestField != null
                ? requestField
                : (encounter != null && encounter.map != null ? encounter.map : defaultMap);
```

`Start()` 에서 요청을 꺼낼 때 전장과 스테이지 id 를 기억해 둔다. 필드 둘을 클래스에 더한다.

```csharp
        private BattleMapData requestField;
        private string stageId = string.Empty;
```

요청을 꺼내는 블록에서 채운다.

```csharp
            if (request != null)
            {
                state = request.State;
                encounter = request.Encounter;
                requestField = request.Field;
                stageId = encounter != null ? encounter.stageId : string.Empty;
            }
            else
            {
                // 연습 전투는 진행도를 남기지 않는다.
                Debug.Log("[FarmCreature] 전투 요청이 없어 연습 전투로 시작합니다. (전투 씬 단독 Play)");
                state = BuildPracticeState();
                encounter = practiceEncounter;
                requestField = null;
                stageId = string.Empty;
            }
```

결과를 실어 보내는 `BattleHandoff.SetResult(...)` 호출에 `stageId` 를 더한다.

> `SetResult` 를 부르는 곳을 `grep -n "SetResult" Assets/Scripts/Runtime/Presentation/BattleSceneRoot.cs`
> 로 찾아 **전부** 고친다. 승리·패배·이탈 세 갈래가 있을 수 있다.

- [ ] **Step 5: `GameRoot` 가 진행도를 기록하게 한다**

`ApplyBattleResult` 를 바꾼다. **재화가 들어오는 곳과 같은 자리다 — 여기 하나뿐이다.**

```csharp
        private static string ApplyBattleResult(GameState state, BattleResult result)
        {
            switch (result.Conclusion)
            {
                case BattleConclusion.Victory:
                    state.AddGold(result.GoldReward);

                    // 진행도가 기록되는 지점도 여기 하나뿐이다 (설계 3.3).
                    // 빈 id 는 StageProgress 가 알아서 무시한다 — 연습 전투가 그렇다.
                    bool first = state.Progress.MarkCleared(result.StageId);

                    return first
                        ? "승리했다! 재화 " + result.GoldReward + " 획득. 새 길이 열렸다."
                        : "승리했다! 재화 " + result.GoldReward + " 획득.";

                case BattleConclusion.Defeat:
                    return "패배했다. 케어는 다치지 않는다 — 더 키워서 다시 오자.";

                default:
                    return "전투에서 물러났다.";
            }
        }
```

- [ ] **Step 6: 테스트가 통과하는지 확인한다**

```powershell
Run-Tests "t5" $null
```

기대: 종료 코드 **0**, `total="229" passed="229" failed="0"`.

`BattlePanel` 이 아직 `SetRequest` 를 인자 2개로 부르고 있으면 컴파일이 깨진다.
**임시로 `null` 을 세 번째 인자로 넘겨 컴파일을 통과시킨다** — Task 7 에서 제대로 채운다.

- [ ] **Step 7: 커밋**

```powershell
git add Assets/Scripts/Runtime/Core/BattleHandoff.cs Assets/Scripts/Runtime/Presentation/ Assets/Scripts/Tests/EditMode/BattleHandoffTests.cs
git commit -m "feat: 요청에 전장을, 결과에 스테이지 id 를 싣는다"
```

---

### Task 6: `PanelLabels` — 맵 행·스테이지 행·페이지

**Files:**
- Modify: `Assets/Scripts/Runtime/Presentation/PanelLabels.cs`
- Test: `Assets/Scripts/Tests/EditMode/PanelLabelsTests.cs`

**Interfaces:**
- Consumes: Task 1~3
- Produces: `PanelLabels.MapRow(StageMap, StageProgress)` / `StageRow(StageMap, int, StageProgress, Inventory, ItemDatabase)` / `PageIndicator(int page, int pageCount)` / `StageMapTitle(StageMap, StageProgress)`. Task 7 이 쓴다

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`PanelLabelsTests.cs` 파일 끝:

```csharp
        // ---------- 맵 / 스테이지 ----------

        private BattleEncounter StageFor(string id, params StageUnlock[] unlock)
        {
            var rat = TestFixtures.MakeEnemy("rat", maxHp: 8, attack: 3, defense: 1);
            return TestFixtures.MakeStage(id, id, new System.Collections.Generic.List<StageUnlock>(unlock), (rat, 1));
        }

        [Test]
        public void MapRow_ShowsClearedOverTotal()
        {
            var map = TestFixtures.MakeStageMap("들쥐 들판", StageFor("a"), StageFor("b"), StageFor("c"));
            var progress = new StageProgress();
            progress.MarkCleared("a");

            string row = PanelLabels.MapRow(map, progress);

            StringAssert.Contains("들쥐 들판", row);
            StringAssert.Contains("1/3", row, "어디까지 왔는지 한눈에 보여야 한다");
        }

        [Test]
        public void MapRow_NullMap_IsEmptySlot()
        {
            Assert.AreEqual("(빈 칸)", PanelLabels.MapRow(null, new StageProgress()));
        }

        [Test]
        public void StageRow_MarksClearedStages()
        {
            var map = TestFixtures.MakeStageMap("맵", StageFor("들판 1"));
            var progress = new StageProgress();
            progress.MarkCleared("들판 1");

            string row = PanelLabels.StageRow(map, 0, progress, new Inventory(), null);

            StringAssert.Contains("들판 1", row);
            StringAssert.Contains("클리어", row);
        }

        [Test]
        public void StageRow_ShowsWhyItIsLocked()
        {
            var previous = TestFixtures.Unlock(StageUnlockKind.PreviousStageCleared);
            var map = TestFixtures.MakeStageMap("맵", StageFor("들판 1"), StageFor("들판 2", previous));

            string row = PanelLabels.StageRow(map, 1, new StageProgress(), new Inventory(), null);

            StringAssert.Contains("들판 1", row, "무엇을 하면 열리는지 줄에 보여야 한다");
        }

        [Test]
        public void PageIndicator_IsEmptyWhenThereIsOnlyOnePage()
        {
            Assert.AreEqual(string.Empty, PanelLabels.PageIndicator(0, 1));
            StringAssert.Contains("2/3", PanelLabels.PageIndicator(1, 3));
        }
```

파일 상단에 `using FarmCreature.Core;` 와 `using FarmCreature.Data;` 가 없으면 추가한다.

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```powershell
Run-Tests "t6" "FarmCreature.Tests.PanelLabelsTests"
```

기대: **0 이 아니다.** `MapRow` 등이 없다는 컴파일 오류.

- [ ] **Step 3: 구현한다**

`PanelLabels.cs` 의 `GoldLine` **앞에** 넣는다.

```csharp
        // ---------- 맵 / 스테이지 ----------

        /// <summary>맵 한 줄. 어디까지 왔는지를 <c>1/3</c> 로 보여준다.</summary>
        public static string MapRow(StageMap map, StageProgress progress)
        {
            if (map == null)
            {
                return "(빈 칸)";
            }

            return map.displayName + "   " + map.ClearedCount(progress) + "/" + map.Count;
        }

        /// <summary>스테이지 목록의 제목. 맵 이름과 진척을 함께 보여준다.</summary>
        public static string StageMapTitle(StageMap map, StageProgress progress)
        {
            if (map == null)
            {
                return "전투터";
            }

            return map.displayName + "   " + map.ClearedCount(progress) + "/" + map.Count + " 클리어";
        }

        /// <summary>
        /// 스테이지 한 줄. <b>잠겼으면 왜 잠겼는지 함께 적는다</b> —
        /// 회색 버튼만 있는 것보다 정보가 많다 (설계 1.3).
        /// </summary>
        public static string StageRow(
            StageMap map, int stageIndex, StageProgress progress,
            Inventory inventory, ItemDatabase itemDatabase)
        {
            var stage = map != null ? map.Get(stageIndex) : null;
            if (stage == null)
            {
                return "(빈 칸)";
            }

            string head = stage.displayName + "   적 " + stage.TotalEnemyCount
                          + " · 재화 " + stage.TotalGoldReward;

            string reason = StageUnlockEvaluator.LockReason(
                map, stageIndex, progress, inventory, itemDatabase);

            if (reason != null)
            {
                return head + "   [잠김] " + reason;
            }

            if (progress != null && progress.IsCleared(stage.stageId))
            {
                return head + "   ✓ 클리어";
            }

            return head;
        }

        /// <summary>페이지가 하나뿐이면 아무것도 보여주지 않는다.</summary>
        public static string PageIndicator(int page, int pageCount)
        {
            if (pageCount <= 1)
            {
                return string.Empty;
            }

            return (page + 1) + "/" + pageCount;
        }
```

- [ ] **Step 4: 테스트가 통과하는지 확인한다**

```powershell
Run-Tests "t6" $null
```

기대: 종료 코드 **0**, `total="234" passed="234" failed="0"`.

- [ ] **Step 5: 커밋**

```powershell
git add Assets/Scripts/Runtime/Presentation/PanelLabels.cs Assets/Scripts/Tests/EditMode/PanelLabelsTests.cs
git commit -m "feat: 맵·스테이지 표시 문자열 — 잠긴 줄에 이유를 적는다"
```

---

### Task 7: `BattlePanel` — 2단계 화면

**Files:**
- Modify: `Assets/Scripts/Runtime/Presentation/BattlePanel.cs` (전면 재작성)

**Interfaces:**
- Consumes: Task 2~6 전부
- Produces: `BattlePanel` 의 공개 필드 `titleText` / `infoText` / `rowTexts`(`Text[]`, **6개**) / `stageDatabase`(`StageMapDatabase`) / `itemDatabase`(`ItemDatabase`) / `careManager`, 공개 메서드 `SelectRow(int)` / `NextPage()` / `PreviousPage()` / `GoBack()`. Task 8·9 가 이 이름을 그대로 쓴다

EditMode 테스트가 없다 — MonoBehaviour 다. **검증은 Task 8 의 무결성 검사가 한다.**

- [ ] **Step 1: 패널을 다시 쓴다**

`BattlePanel.cs` 를 통째로 바꾼다.

```csharp
using FarmCreature.Core;
using FarmCreature.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FarmCreature.Presentation
{
    /// <summary>
    /// 전투터 — 맵을 고르고 그 안의 스테이지를 고른다.
    ///
    /// <b>고정 6칸을 두 모드가 번갈아 쓴다</b> (설계 5절). 행을 런타임에 만들지 않으므로
    /// 맵이 늘든 스테이지가 늘든 <c>BuildAll</c> 이 필요 없다 (함정 3.1 을 정면으로 푸는 방법).
    ///
    /// 규칙은 <see cref="StageUnlockEvaluator"/> 에만 있다 — 여기는 표시와 배선만 한다.
    /// </summary>
    public class BattlePanel : GamePanel
    {
        public const string BattleSceneName = "BattleScene";

        /// <summary>한 페이지에 그리는 줄 수. 씬에 만들어 둔 행 개수와 같아야 한다.</summary>
        public const int RowsPerPage = 6;

        public Text titleText;
        public Text infoText;

        [Tooltip("항상 6개다. 데이터 개수와 무관하다. GameBootstrap 이 채운다.")]
        public Text[] rowTexts;

        [Tooltip("맵 목록. GameBootstrap 이 채운다.")]
        public StageMapDatabase stageDatabase;

        [Tooltip("잠금 사유에 아이템 이름을 쓴다. GameBootstrap 이 채운다.")]
        public ItemDatabase itemDatabase;

        [Tooltip("로그를 맡길 조율자. GameBootstrap 이 채운다.")]
        public CareManager careManager;

        /// <summary>−1 이면 맵 목록을 보고 있다. 0 이상이면 그 맵의 스테이지 목록이다.</summary>
        private int openMapIndex = -1;

        private int page;

        public void SelectRow(int rowIndex)
        {
            int index = page * RowsPerPage + rowIndex;

            if (openMapIndex < 0)
            {
                OpenMap(index);
                return;
            }

            StartStage(index);
        }

        public void NextPage()
        {
            // 그릴 것이 있을 때만 넘어간다. 빈 페이지로 가면 무엇을 보는지 알 수 없다.
            if ((page + 1) * RowsPerPage < ItemCount)
            {
                page++;
                Refresh();
            }
        }

        public void PreviousPage()
        {
            if (page > 0)
            {
                page--;
                Refresh();
            }
        }

        /// <summary>스테이지 목록에서 맵 목록으로 돌아간다.</summary>
        public void GoBack()
        {
            openMapIndex = -1;
            page = 0;
            Refresh();
        }

        public override void Refresh()
        {
            var map = OpenMap();

            if (titleText != null)
            {
                titleText.text = map != null
                    ? PanelLabels.StageMapTitle(map, Progress)
                    : "전투터";
            }

            if (infoText != null)
            {
                string page = PanelLabels.PageIndicator(this.page, PageCount);
                string ready = PanelLabels.BattleReadyLine(state);
                infoText.text = string.IsNullOrEmpty(page) ? ready : ready + "   " + page;
            }

            if (rowTexts == null)
            {
                return;
            }

            for (int i = 0; i < rowTexts.Length; i++)
            {
                if (rowTexts[i] == null)
                {
                    continue;
                }

                int index = this.page * RowsPerPage + i;
                rowTexts[i].text = RowTextAt(map, index);
            }
        }

        // ---------- 안쪽 ----------

        private StageProgress Progress
        {
            get { return state != null ? state.Progress : null; }
        }

        private StageMap OpenMap()
        {
            return openMapIndex >= 0 && stageDatabase != null
                ? stageDatabase.Get(openMapIndex)
                : null;
        }

        /// <summary>지금 모드에서 그릴 것의 총 개수.</summary>
        private int ItemCount
        {
            get
            {
                var map = OpenMap();
                if (map != null)
                {
                    return map.Count;
                }

                return stageDatabase != null ? stageDatabase.Count : 0;
            }
        }

        private int PageCount
        {
            get
            {
                int count = ItemCount;
                if (count <= 0)
                {
                    return 1;
                }

                return (count + RowsPerPage - 1) / RowsPerPage;
            }
        }

        private string RowTextAt(StageMap map, int index)
        {
            if (map == null)
            {
                var candidate = stageDatabase != null ? stageDatabase.Get(index) : null;

                // 남는 칸은 빈 문자열이다. "(빈 칸)" 은 고를 수 있는 것처럼 보인다.
                return candidate == null ? string.Empty : PanelLabels.MapRow(candidate, Progress);
            }

            if (map.Get(index) == null)
            {
                return string.Empty;
            }

            return PanelLabels.StageRow(map, index, Progress, Inventory, itemDatabase);
        }

        private void OpenMap(int index)
        {
            if (stageDatabase == null || stageDatabase.Get(index) == null)
            {
                return;   // 빈 칸을 눌렀다. 아무 일도 일어나지 않는 것이 맞다.
            }

            openMapIndex = index;
            page = 0;
            Refresh();
        }

        private void StartStage(int index)
        {
            var map = OpenMap();
            if (map == null)
            {
                return;
            }

            var stage = map.Get(index);
            if (stage == null)
            {
                return;   // 빈 칸
            }

            string reason = StageUnlockEvaluator.LockReason(
                map, index, Progress, Inventory, itemDatabase);
            if (reason != null)
            {
                Log(reason);
                return;
            }

            if (state == null)
            {
                Log("전투터를 열 수 없다.");
                return;
            }

            if (state.Roster.Party.Count == 0)
            {
                Log("밖에 나와 있는 케어가 없다. 헛간에서 먼저 내보내자.");
                return;
            }

            BattleHandoff.SetRequest(state, stage, map.field);
            SceneManager.LoadScene(BattleSceneName);
        }

        private void Log(string message)
        {
            if (careManager != null)
            {
                careManager.SetLog(message);
            }
        }

        /// <summary>
        /// 패널을 열 때는 <b>항상 맵 목록부터</b> 시작한다. 지난번에 보던 화면이 남아 있으면
        /// 무엇을 보고 있는지 헷갈린다 (설계 5절).
        /// </summary>
        private void OnEnable()
        {
            openMapIndex = -1;
            page = 0;
        }
    }
}
```

> `Refresh()` 안에서 지역 변수 `page` 가 필드 `page` 를 가리므로 `this.page` 로 구분했다.
> 헷갈리면 지역 변수 이름을 `pageLabel` 로 바꿔도 된다.

- [ ] **Step 2: 컴파일과 회귀를 확인한다**

```powershell
Run-Tests "t7" $null
```

기대: 종료 코드 **0**, `total="234" passed="234" failed="0"` (새 테스트 없음).

`GameBootstrap` 이 아직 옛 필드(`encounterDatabase`)를 세우고 있어 컴파일이 깨진다.
**Task 9 에서 고치므로, 여기서는 `GameBootstrap.cs` 의 그 줄만 잠시 주석 처리해 통과시킨다.**
주석에 `// Task 9 에서 교체` 를 남긴다.

- [ ] **Step 3: 커밋**

```powershell
git add Assets/Scripts/Runtime/Presentation/BattlePanel.cs Assets/Scripts/Editor/GameBootstrap.cs
git commit -m "feat: 전투터를 맵 → 스테이지 2단계로. 고정 6칸을 두 모드가 나눠 쓴다"
```

---

### Task 8: 무결성 검사를 먼저 올린다 (실패를 눈으로 본다)

**씬을 고치기 전에 검사를 올린다.** 검사가 실제로 실패하는 것을 보지 않으면
Task 9 의 통과가 "검사가 무언가를 보고 있다"는 증거가 되지 않는다 (함정 3.9 가 그래서 숨었다).

**Files:**
- Modify: `Assets/Scripts/Editor/SceneIntegrityCheck.cs`

**Interfaces:**
- Consumes: Task 7 의 `BattlePanel` 공개 표면
- Produces: 없음

- [ ] **Step 1: 세 곳을 고친다**

**(1) 8-1 의 전투 패널 배선** — `battlePanel.encounterDatabase` 를 확인하던 블록을 바꾼다.

```csharp
                failures += RequireRef(report, "battlePanel.stageDatabase", battle.stageDatabase);
                failures += RequireRef(report, "battlePanel.itemDatabase", battle.itemDatabase);
                failures += RequireRef(report, "battlePanel.careManager", battle.careManager);
                failures += RequireRef(report, "battlePanel.titleText", battle.titleText);
```

> `battle` 변수를 어떻게 얻는지는 파일을 열어 확인한다. 없으면
> `var battle = Object.FindFirstObjectByType<BattlePanel>(FindObjectsInactive.Include);` 로 얻는다.

**(2) 12번의 인카운터 개수 검사를 고정 6으로 바꾼다** — `Encounter{i}` 를 세던 블록을
통째로 바꾼다.

```csharp
            // 전투터는 데이터 개수가 아니라 고정 6칸이다 — 맵·스테이지가 늘어도 씬은 그대로다.
            if (battle != null)
            {
                failures += RequireNamedChildren(report, battle.transform, "Row",
                    BattlePanel.RowsPerPage, "전투터 줄");
                failures += RequireNamedChildren(report, battle.transform, "Select",
                    BattlePanel.RowsPerPage, "전투터 [선택] 버튼");

                int battleRowTexts = battle.rowTexts == null ? 0 : battle.rowTexts.Length;
                if (battleRowTexts != BattlePanel.RowsPerPage)
                {
                    report.AppendLine("  [실패] battlePanel.rowTexts 가 "
                                      + BattlePanel.RowsPerPage + "개가 아님: " + battleRowTexts);
                    failures++;
                }

                if (battle.stageDatabase != null && battle.stageDatabase.Count == 0)
                {
                    report.AppendLine("  [실패] StageMapDatabase 가 비어 있음 — 갈 곳이 없다");
                    failures++;
                }
            }
```

**(3) 페이지·뒤로 버튼이 배선됐는가** — (2) 블록 안에 이어서 넣는다.

```csharp
                string[] navButtons = { "PrevPage", "NextPage", "Back" };
                for (int i = 0; i < navButtons.Length; i++)
                {
                    var found = battle.transform.Find(navButtons[i]);
                    if (found == null)
                    {
                        report.AppendLine("  [실패] 전투터에 " + navButtons[i] + " 버튼이 없음");
                        failures++;
                    }
                }
```

- [ ] **Step 2: 검사가 실패하는지 확인한다**

```powershell
$p = Start-Process -FilePath $UNITY -ArgumentList @('-batchmode','-projectPath',$PROJ,
  '-executeMethod','FarmCreature.EditorTools.SceneIntegrityCheck.Run','-logFile',"$LOG\integrity-red.log") -PassThru -Wait
"EXIT=$($p.ExitCode)"
Select-String -Path "$LOG\integrity-red.log" -Pattern '\[실패\]' | ForEach-Object { $_.Line.Trim() }
```

기대: 종료 코드 **1**. 최소한 아래가 보여야 한다.

- `[실패] battlePanel.stageDatabase 가 비어 있음`
- `[실패] 전투터 줄 이 6개가 아님` (또는 그에 준하는 개수 불일치)
- `[실패] 전투터에 PrevPage 버튼이 없음`

**세 줄이 다 안 보이면 검사가 아무것도 안 보고 있다는 뜻이다.**

- [ ] **Step 3: 커밋**

```powershell
git add Assets/Scripts/Editor/SceneIntegrityCheck.cs
git commit -m "test: 무결성 검사를 고정 6칸 기준으로 — 아직 실패한다"
```

---

### Task 9: `GameBootstrap` — 에셋 씨앗과 씬 배선 (검사를 통과시킨다)

**Files:**
- Modify: `Assets/Scripts/Editor/GameBootstrap.cs`
- Delete: `Assets/Scripts/Runtime/Data/BattleEncounterDatabase.cs` (+ `.meta`)
- Delete: `Assets/GameData/Battle/RatField.asset` · `BoarThicket.asset` · `GolemRidge.asset` (+ `.meta`)
- Delete: `Assets/GameData/BattleEncounterDatabase.asset` (+ `.meta`)
- Modify: `Assets/Scripts/Tests/EditMode/TestFixtures.cs` (`MakeEncounterDatabase` 삭제)

**Interfaces:**
- Consumes: Task 2 의 데이터, Task 7 의 `BattlePanel`
- Produces: 씬 안의 배선된 `BattlePanel`, 새 에셋 `StageMapDatabase.asset` 과 `FieldStages.asset`

- [ ] **Step 1: 옛 인카운터 에셋을 지운다**

**seed-only 라 코드로 고쳐도 반영되지 않는다** (함정 3.11). 지우고 다시 깐다.
밸런스를 만진 적이 없으므로 잃을 것이 없다.

```powershell
cd $PROJ
Remove-Item Assets/GameData/Battle/RatField.asset* -Force
Remove-Item Assets/GameData/Battle/BoarThicket.asset* -Force
Remove-Item Assets/GameData/Battle/GolemRidge.asset* -Force
Remove-Item Assets/GameData/BattleEncounterDatabase.asset* -Force -ErrorAction SilentlyContinue
Remove-Item Assets/Scripts/Runtime/Data/BattleEncounterDatabase.cs* -Force
git status --short
```

> `BattleEncounterDatabase.asset` 의 실제 경로를 먼저 확인한다:
> `Get-ChildItem Assets -Recurse -Filter "BattleEncounterDatabase.asset"`

`TestFixtures.MakeEncounterDatabase` 도 지운다 — 타입이 사라졌다.
`grep -rn "MakeEncounterDatabase\|BattleEncounterDatabase" Assets/Scripts` 로 남은 참조를 전부 없앤다.

- [ ] **Step 2: 스테이지 씨앗을 만든다**

`GameBootstrap.cs` 의 `BuildBattleDataAssets`(435행)·`SeedEncounter`(474행)·
`ValidateEncounters`(498행) 를 아래로 **교체**한다.

> **`LoadOrSeed` 의 실제 시그니처는 `LoadOrSeed<T>(string assetPath, out bool created)` 다**
> (248행). 델리게이트를 받지 않는다. `created` 가 true 일 때만 초기값을 채운다 —
> 그것이 seed-only 의 뜻이다(함정 3.11).

```csharp
        /// <summary>
        /// 전장·스테이지·맵 씨앗. <b>이미 있으면 손대지 않는다</b> (seed-only, 함정 3.11) —
        /// 인스펙터에서 만진 밸런스를 지키기 위해서다.
        ///
        /// 적 구성은 난이도 곡선을 잡은 값이 아니라 <b>자리를 채운 씨앗</b>이다.
        /// 밸런스는 개발이 끝난 뒤에 잡는다 (설계 7절).
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
        /// 진행도가 엉뚱하게 기록된다.
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
                        Debug.LogError("[FarmCreature] " + map.displayName + " 의 " + s + "번 스테이지가 비었다.");
                        continue;
                    }

                    if (string.IsNullOrEmpty(stage.stageId))
                    {
                        Debug.LogError("[FarmCreature] " + stage.displayName
                            + " 에 stageId 가 없다 — 진행도를 기록할 수 없다.");
                    }
                    else if (!seenIds.Add(stage.stageId))
                    {
                        Debug.LogError("[FarmCreature] stageId 가 겹친다: " + stage.stageId
                            + " — 하나를 깨면 다른 것도 깬 것이 된다.");
                    }

                    if (stage.TotalEnemyCount > BattleField.MaxEnemies)
                    {
                        Debug.LogError("[FarmCreature] " + stage.displayName + " 의 적이 "
                            + stage.TotalEnemyCount + "마리다. 슬롯은 "
                            + BattleField.MaxEnemies + "개뿐이라 나머지는 안 나온다.");
                    }
                }
            }
        }
```

`GameBootstrap.cs` 상단에 `using System.Collections.Generic;` 은 이미 있다.
`HashSet` 을 쓰므로 그대로 된다.

- [ ] **Step 2-1: 연습 전투가 가리키는 에셋을 바꾼다**

**88행이 지워질 `RatField.asset` 을 직접 가리킨다.** 그대로 두면 전투 씬 단독 Play 가
빈 인카운터로 시작한다.

```csharp
            var practiceEncounter = AssetDatabase.LoadAssetAtPath<BattleEncounter>(
                BattleDir + "/Field1.asset");
```

- [ ] **Step 3: 전투 패널을 6칸으로 다시 만든다**

`BuildBattlePanel` 을 통째로 바꾼다.

```csharp
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

            // 행은 항상 6개다. 데이터가 늘어도 씬은 그대로다 (함정 3.1).
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
```

- [ ] **Step 4: 호출부를 고친다**

`BuildFarmScene` 에서 `BuildBattlePanel(...)` 호출을 새 인자에 맞춘다.

```csharp
            var battlePanel = BuildBattlePanel(panelRoot.transform, careManager, stageDb, itemDb);
```

`BuildBattleDataAssets` 의 **반환 타입이 바뀌었으므로**(→ `StageMapDatabase`)
받는 쪽 변수 이름을 `encounterDb` → `stageDb` 로 바꾼다(69행). `BuildFarmScene` 의
매개변수 타입도 함께 바꾼다(614행 근처).

```powershell
grep -n "encounterDb\|BattleEncounterDatabase\|encounterDatabase" Assets/Scripts/Editor/GameBootstrap.cs
```

로 남은 참조를 **전부** 없앤다. `BattleSceneRoot.practiceEncounter` 는 단일
`BattleEncounter` 라 타입이 그대로다 — 경로만 Step 2-1 에서 바꿨다.

Task 7 에서 주석 처리했던 줄도 여기서 지운다.

- [ ] **Step 5: 씬을 재생성한다**

```powershell
$p = Start-Process -FilePath $UNITY -ArgumentList @('-batchmode','-quit','-projectPath',$PROJ,
  '-executeMethod','FarmCreature.EditorTools.GameBootstrap.BuildAll','-logFile',"$LOG\rebuild.log") -PassThru -Wait
"EXIT=$($p.ExitCode)"
Select-String -Path "$LOG\rebuild.log" -Pattern 'Build All 완료'
Select-String -Path "$LOG\rebuild.log" -Pattern 'LogError|error CS|Exception'
```

기대: 종료 코드 **0**, `Build All 완료` 가 있고 **`LogError` 가 한 줄도 없다.**

- [ ] **Step 6: 무결성 검사를 통과시킨다**

```powershell
$p = Start-Process -FilePath $UNITY -ArgumentList @('-batchmode','-projectPath',$PROJ,
  '-executeMethod','FarmCreature.EditorTools.SceneIntegrityCheck.Run','-logFile',"$LOG\integrity-green.log") -PassThru -Wait
"EXIT=$($p.ExitCode)"
Select-String -Path "$LOG\integrity-green.log" -Pattern '\[실패\]'
Select-String -Path "$LOG\integrity-green.log" -Pattern '전투터'
```

기대: 종료 코드 **0**, `[실패]` 없음, `전투터 줄 6개 확인` · `전투터 [선택] 버튼 6개 확인`.

- [ ] **Step 7: 커밋**

```powershell
git add -A
git commit -m "feat: 스테이지 4개를 씨앗으로 깔고 전투터를 6칸으로 재생성"
```

---

### Task 10: 실제 에셋 검사 · 전량 회귀 · 문서

**Files:**
- Modify: `Assets/Scripts/Tests/EditMode/GameDataIntegrationTests.cs`
- Modify: `docs/HANDOFF.md` · `docs/PRD.md`

- [ ] **Step 1: 실제 에셋을 검사하는 테스트를 쓴다**

`GameDataIntegrationTests.cs` 는 `[SetUp]` 에서 경로 상수로 에셋을 로드하고
**없으면 바로 실패시킨다.** 같은 방식으로 `stageDb` 를 더한다.

경로 상수 옆에:

```csharp
        private const string StageDbPath = "Assets/GameData/StageMapDatabase.asset";
```

필드 옆에:

```csharp
        private StageMapDatabase stageDb;
```

`[SetUp]` 안에:

```csharp
            stageDb = AssetDatabase.LoadAssetAtPath<StageMapDatabase>(StageDbPath);
            Assert.IsNotNull(stageDb, "StageMapDatabase 에셋이 없다. GameBootstrap.BuildAll 을 먼저 실행할 것.");
```

그리고 파일 끝에 테스트를 더한다.

```csharp
        // ---------- 맵 / 스테이지 ----------

        [Test]
        public void EveryStage_HasANonBlankId()
        {
            for (int m = 0; m < stageDb.Count; m++)
            {
                var map = stageDb.Get(m);
                for (int s = 0; s < map.Count; s++)
                {
                    var stage = map.Get(s);
                    Assert.IsFalse(string.IsNullOrEmpty(stage.stageId),
                        map.displayName + " 의 " + s + "번 스테이지에 id 가 없다 — 진행도를 기록할 수 없다");
                }
            }
        }

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
                    Assert.IsTrue(seen.Add(id),
                        "stageId 가 겹친다: " + id + " — 하나를 깨면 다른 것도 깬 것이 된다");
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
                }
            }
        }
```

파일 상단에 `using FarmCreature.Core;` 가 없으면 추가한다.

- [ ] **Step 2: EditMode 전량**

```powershell
Run-Tests "final-edit" $null
```

기대: 종료 코드 **0**, `total="238" passed="238" failed="0"`.

- [ ] **Step 3: PlayMode 전량**

```powershell
Run-Tests "final-play" $null "PlayMode"
```

기대: 종료 코드 **0**, `total="7" passed="7"`.

**스폰이 랜덤이라 한 번 통과는 증거가 약하다 — 두 번 더 돌린다** (HANDOFF 5.4).

- [ ] **Step 4: `HANDOFF.md` 를 갱신한다**

1. 상단 날짜·머리말에 **맵/스테이지** 한 줄
2. **1절 표** — EditMode `238/238`, 씬 무결성 항목에 "전투터 고정 6칸" 반영
3. **1절 "무엇이 되고 무엇이 안 되나"** — 전투터 줄을 "맵 → 스테이지 2단계, 순서대로 해금"으로
4. **2.x 신설** — 맵/스테이지 절. 계층·해금 조건·진행도 id·고정 6칸·세이브 v4
5. **2.2 세이브 v3 설명** → **v4** 로. v3 은 승격, v2 이하는 거부
6. **5.1 테스트 표** — `StageProgressTests` 6 · `StageUnlockEvaluatorTests` 9 신설,
   `SaveSerializerTests` 18→22 · `PanelLabelsTests` 30→35 · `BattleDataTests` 8→10 ·
   `BattleHandoffTests` 7→9 · `GameDataIntegrationTests` 16→20, 합계 238
7. **5.3 무결성 검사** — 전투터가 데이터 개수가 아니라 **고정 6칸**임을 반영
8. **6절 파일 구조** — 신설 파일 5개 추가, `BattleEncounterDatabase` 삭제 반영
9. **7절 다음 할 것** — **건물 씬 분리**를 0순위로. 그 뒤 케어 방출
10. **8절 미검증 항목** — 새로 추가:
    - [ ] 전투터를 열면 맵 목록이 뜬다
    - [ ] 맵을 고르면 스테이지 목록으로 바뀌고 [뒤로]로 돌아온다
    - [ ] 잠긴 스테이지를 누르면 이유가 로그에 뜨고 전투가 안 열린다
    - [ ] 스테이지를 깨면 다음이 열린다
    - [ ] 저장하고 껐다 켜도 진행도가 남는다
    - [ ] 스테이지가 6개를 넘을 때 [다음] 페이지가 동작한다

- [ ] **Step 5: `PRD.md` 를 갱신한다**

**문서 버전 1.4 → 1.5.** **아래에서 위로** 작업한다.

| 위치 | 바꿀 것 |
|---|---|
| 10절 열린 질문 | **Q8 신설** — 드랍 아이템(지도·열쇠)을 언제 넣나. `RequiresItem` 이 준비돼 있다 |
| 9절 백로그 | 맵/스테이지를 ✅ 로, **다음 순번을 건물 씬 분리**로 |
| 6.7 세이브 | v3 → **v4**. 진행도가 늘고 v3 은 승격한다 |
| 6.9 전투 | 상대를 고르는 경로를 **맵 → 스테이지**로 |
| 6.5 진입점 표 | 전투터 줄을 "맵 선택 → 스테이지 선택"으로 |
| 2.2 구현 목록 | **맵/스테이지 진행 구조 (2026-08-16)** 줄 신설 |
| 머리말 | 1.5 에서 바뀐 것 — 스테이지를 순서대로 깨 나가게 됐다 |

> HANDOFF 7절과 PRD 9절의 "다음 순번"이 달라지면 안 된다.

- [ ] **Step 6: 커밋**

```powershell
git add -A
git commit -m "docs: 맵/스테이지 반영해 HANDOFF·PRD 갱신"
```

- [ ] **Step 7: 눈으로 확인할 것을 사람에게 넘긴다**

Step 4 의 10번 목록을 사용자에게 전달하고,
`superpowers:finishing-a-development-branch` 로 병합 여부를 묻는다.

---

## 실행 순서 요약

| 태스크 | 끝났을 때 확인되는 것 | EditMode |
|---|---|---|
| 1 | 진행도를 id 로 기록한다 | 211 |
| 2 | 맵이 스테이지를 담고 해금이 조건 목록이다 | 213 |
| 3 | 열렸는가·왜 잠겼는가를 판정한다 | 222 |
| 4 | 세이브 v4, **v3 은 승격된다** | 227 |
| 5 | 요청에 전장, 결과에 스테이지 id | 229 |
| 6 | 표시 문자열 | 234 |
| 7 | 전투터가 2단계 화면이 된다 (컴파일만) | 234 |
| 8 | 무결성 검사가 **실패**한다 (3줄 확인) | **종료 코드 1** |
| 9 | `BuildAll` 무오류 → 무결성 검사 통과 | 234 |
| 10 | 실제 에셋 검사 + PlayMode + 문서 | **238** |

**Task 8 만 종료 코드 1 이 정상이다.**

**Task 9 가 가장 위험하다** — 에셋을 지우고 타입을 삭제한다. `grep` 으로 남은 참조를
전부 없애지 않으면 컴파일이 깨진다. 그 자리에서 멈추고 하나씩 지운다.
