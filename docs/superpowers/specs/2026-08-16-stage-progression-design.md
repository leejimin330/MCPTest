# 설계 — 맵/스테이지 진행 구조 (2026-08-16)

**상태:** 설계 승인됨. 구현 전.
**전제:** 지금 전투터는 `BattleEncounterDatabase` 의 **평평한 목록**을 버튼 6칸에 그대로 편다.
맵(`BattleMapData`)은 인카운터에 딸린 부속이고, **어디까지 깼는지 아무도 기억하지 않는다.**
세이브 v3 에는 그것을 담을 자리가 없다.

이 문서는 그 위에 **맵 → 스테이지** 두 단계를 세우고, **순서대로 해금**되는 진행 구조를 넣는 설계다.
목표는 셋이다.

1. 전투터에서 **맵을 고르고 그 안의 스테이지를 고른다**
2. **스테이지를 순서대로 깨야 다음이 열린다** — 목표가 생기고 난이도 곡선을 설계할 수 있다
3. **스테이지의 몬스터 종류·마릿수를 인스펙터 리스트에서 쉽게 고친다.**
   스테이지를 늘려도 `BuildAll` 을 돌리지 않아도 된다

> **범위 밖: 건물별 개별 씬.** 처음에는 이것과 묶으려 했으나, 코드를 보니 겹치는 것은
> 전투터 화면 하나뿐이고 나머지 네 건물은 맵/스테이지와 무관하다. 화면을 패널에서 씬으로
> 옮기는 일은 행·버튼·문자열을 그대로 두고 그릇만 바꾸는 작업이라, UI 가 정해진 뒤에
> 한 번만 하는 편이 낫다. 별도 프로젝트로 뺐다.

---

## 1. 확정된 결정

| 항목 | 결정 |
|---|---|
| 계층 | **맵이 스테이지를 담는다.** 스테이지가 맵을 가리키지 않는다 |
| 해금 | **순서대로.** 단 "이전을 깼는가"를 코드에 박지 않고 **조건 목록**으로 둔다 |
| 진행도 기록 | **스테이지 id 문자열.** 인덱스가 아니다 |
| 세이브 | **v3 → v4.** v3 은 버리지 않고 **진행도만 빈 채로 승격**한다 |
| 화면 | **고정 6칸을 두 단계가 번갈아 쓴다** + 페이지 넘김. `BuildAll` 없이 스테이지를 늘릴 수 있다 |
| 잠긴 스테이지 | 누를 수 있게 두되 **왜 잠겼는지 알려준다** |
| 기존 인카운터 에셋 | **지우고 다시 깐다** (seed-only, 함정 3.11) |

### 1.1 왜 진행도를 인덱스가 아니라 id 로 기록하는가

이 작업의 목표 중 하나가 **스테이지를 쉽게 추가·재배치하는 것**이다. 인덱스로 기록하면
스테이지를 중간에 하나 끼워 넣는 순간 그 뒤의 진행도가 전부 한 칸씩 밀려 **엉뚱한 스테이지가
깬 것으로 바뀐다.** 목표와 정면으로 부딪힌다.

id 는 그 대가로 **중복 금지**라는 새 불변조건을 만든다. 6.1 의 테스트가 이것을 지킨다.

### 1.2 왜 해금을 조건 목록으로 두는가

지금 필요한 것은 "이전 스테이지를 깼는가" 하나뿐이다. 그런데 사용자가
**"드랍 아이템(지도·열쇠)으로 열리는 기믹을 나중에 추가한다"**고 밝혔다.

`if (previousCleared)` 로 박아 두면 그때 이 자리를 다시 헤집어야 한다. 조건 목록으로 두면
**종류를 하나 더하는 것으로 끝난다.** 이 코드베이스에는 이미 같은 패턴이 있다 —
진화 조건(`EvolutionCondition`)이 `ExpAtLeast`/`StatAtLeast`/`RequiresEquipped` 를 AND 로 조합한다.
같은 모양을 따르면 다음 사람이 새로 배울 것이 없다.

**`RequiresItem` 은 이번에 함께 구현한다.** `Inventory.GetCount` 가 이미 있어 몇 줄이면 되고,
그래야 확장 지점이 **실제로 도는 것**이지 주석뿐인 약속이 아니다. 상점에서 산 아이템으로
스테이지를 잠글 수 있게 된다. **드랍 자체(전투 보상으로 아이템을 주는 것)는 범위 밖이다** —
보상 경로가 지금 재화만 나르므로 별도 작업이고, 그것이 생기는 순간 이 조건이 바로 물린다.

### 1.3 왜 잠긴 스테이지를 회색 버튼으로만 두지 않는가

누를 수 없는 회색 버튼은 "왜"를 말해주지 않는다. 플레이어가 **무엇을 하면 열리는지**
알아야 목표가 된다. 부화소에서 헛간 점유율을 제목에 박은 것과 같은 판단이다(부화소 설계 1.2).

### 1.4 왜 v3 세이브를 버리지 않는가

v2 를 버린 것은 **보유 케어가 단일 → 배열로 바뀌어 옮길 방법이 없었기** 때문이다.
v3 → v4 는 **필드가 하나 느는 것뿐**이라 옮길 것이 없다 — 진행도를 빈 배열로 두면 그만이다.
지금까지 키운 케어를 날릴 이유가 없다.

---

## 2. 데이터

### 2.1 `BattleEncounter` — 스테이지로 승격 (필드 둘 추가)

기존 클래스를 버리지 않는다. `{적, 마리수}` 리스트와 표시 이름이 이미 있고,
**몬스터 편집이 일어나는 자리가 바로 그 리스트**다.

```csharp
public class BattleEncounter : ScriptableObject
{
    public string stageId;                 // 신설. 진행도가 이것으로 기록된다
    public string displayName = "들판";
    public List<EncounterEntry> entries;   // 그대로 — 몬스터 종류·마릿수 편집
    public List<StageUnlock> unlock;       // 신설. 비어 있으면 항상 열림
    public BattleMapData map;              // 남겨두되 안 쓴다 (2.2 참고)
}
```

`map` 필드는 **지우지 않는다.** 스테이지가 전장을 덮어쓰고 싶을 때를 위해 남긴다 —
평소에는 비워 두고 맵이 정한다.

**전투 씬에 전장을 어떻게 알리는가.** 지금은 `BattleSceneRoot.StartBattle` 이
`encounter.map` 을 읽는다. 전장이 `StageMap` 으로 올라가면 그 경로가 끊긴다.

→ **`BattleRequest` 에 `Field` 를 싣는다.**

```csharp
public class BattleRequest
{
    public GameState State;
    public BattleEncounter Encounter;
    public BattleMapData Field;      // 신설 — 스테이지가 속한 맵의 전장
}
```

`BattleSceneRoot` 가 고르는 순서: **`request.Field` → `encounter.map` → `defaultMap`.**
셋 다 비면 지금처럼 `LogError` 를 내고 유닛을 세우지 않는다.
`BattleHandoff.SetRequest` 에 인자가 하나 는다.

### 2.2 `StageMap` · `StageMapDatabase` — 신설

```csharp
[CreateAssetMenu(menuName = "FarmCreature/Stage Map")]
public class StageMap : ScriptableObject
{
    public string displayName = "들판";
    public BattleMapData field;              // 전장 기하 (지금의 FieldMap)
    public List<BattleEncounter> stages;     // 순서가 곧 해금 순서다
}

[CreateAssetMenu(menuName = "FarmCreature/Stage Map Database")]
public class StageMapDatabase : ScriptableObject
{
    public List<StageMap> maps;
}
```

**맵이 스테이지를 담는다.** 스테이지마다 맵을 지정하지 않으므로 편집이 한 번 줄고,
"맵 고르고 → 스테이지 고르고"라는 화면 구조와 데이터가 같은 모양이 된다.

`stages` 의 **순서가 곧 해금 순서**다 — `PreviousStageCleared` 가 이 순서를 읽는다.

`BattleEncounterDatabase` 는 **삭제한다.** `StageMapDatabase` 가 대신한다.

### 2.3 `StageUnlock` — 해금 조건

```csharp
public enum StageUnlockKind
{
    PreviousStageCleared,   // 같은 맵에서 바로 앞 스테이지
    StageCleared,           // 지정한 stageId (다른 맵도 가능)
    RequiresItem            // 아이템 보유 — 지도·열쇠가 여기로 들어온다
}

[System.Serializable]
public class StageUnlock
{
    public StageUnlockKind kind;
    public string stageId;    // StageCleared 일 때
    public string itemId;     // RequiresItem 일 때
    public int count = 1;     // RequiresItem 일 때
}
```

**전부 AND.** 목록이 비어 있으면 항상 열린다 — 각 맵의 첫 스테이지가 그렇다.

---

## 3. 코어 (순수 C#)

### 3.1 `StageProgress` — 무엇을 깼는가

```csharp
public class StageProgress
{
    public IReadOnlyList<string> ClearedStageIds { get; }

    public bool IsCleared(string stageId);

    /// <summary>처음 깬 것이면 true. 이미 깬 것이면 아무것도 안 하고 false.</summary>
    public bool MarkCleared(string stageId);
}
```

빈 id 는 받지 않는다 — 기록해봐야 무엇을 깼는지 알 수 없다.
**순서를 보존한다**(깬 차례대로). 세이브가 배열이라 그대로 실린다.

`GameState` 에 `Progress` 로 들어간다. `Roster`·`Inventory`·`Gold` 와 같은 자리다.

### 3.2 `StageUnlockEvaluator` — 열렸는가, 왜 안 열렸는가

```csharp
public static class StageUnlockEvaluator
{
    public static bool IsUnlocked(
        StageMap map, int stageIndex, StageProgress progress, Inventory inventory);

    /// <summary>잠긴 이유를 사람이 읽을 문장으로. 열려 있으면 null.</summary>
    public static string LockReason(
        StageMap map, int stageIndex, StageProgress progress, Inventory inventory,
        ItemDatabase itemDatabase);
}
```

`map` 과 `stageIndex` 를 함께 받는 이유: `PreviousStageCleared` 가 **바로 앞 스테이지**를
알아야 하는데, 그것은 스테이지 자신이 아니라 **맵의 목록 순서**가 정한다.

`LockReason` 이 `ItemDatabase` 를 받는 것은 `itemId` 를 사람이 읽을 이름으로 바꾸기 위해서다
(`"낡은 열쇠가 필요하다"`, `"iron_grain 이 필요하다"` 가 아니라).

**첫 스테이지(index 0)에 `PreviousStageCleared` 가 붙어 있으면 앞이 없으므로 열린 것으로 본다** —
데이터 실수로 맵 전체가 막히는 것이 더 나쁘다.

### 3.3 진행도가 기록되는 지점

**한 곳뿐이다: `GameRoot.ApplyBattleResult`.** 재화가 들어오는 곳과 같은 자리다
(전투 화면 설계 3.3의 이유가 그대로 적용된다 — 씬이 바뀌어도 이중 기록이 없다).

```
승리  → state.Progress.MarkCleared(result.StageId)  그리고 재화 지급
패배·이탈 → 아무것도 하지 않는다
```

`BattleResult` 에 `StageId` 가 는다. `BattleSceneRoot` 가 요청에 실려온 인카운터의
`stageId` 를 결과에 그대로 실어 보낸다 — **전투 씬은 진행도를 건드리지 않는다.**

---

## 4. 세이브 v4

```csharp
public const int CurrentVersion = 4;

public string[] clearedStageIds = new string[0];   // 신설
```

**v3 은 승격한다.** `TryRestore` 가 버전을 이렇게 다룬다.

| 저장된 버전 | 처리 |
|---|---|
| 4 | 그대로 읽는다 |
| 3 | **읽는다.** `clearedStageIds` 를 빈 배열로 둔다 (진행도만 초기화) |
| 2 이하 | 지금처럼 거부하고 새 게임 |

읽을 때 **빈 id 와 중복은 걸러낸다** — 손으로 고친 세이브가 진행도를 오염시키지 않게.

---

## 5. 화면 — 고정 6칸을 두 단계가 번갈아 쓴다

`BattlePanel` 이 두 모드를 갖는다. **클래스 이름은 그대로 둔다** — 여전히 전투터 패널이고,
바꾸면 `GameBootstrap`·`SceneIntegrityCheck`·`CareManager.panels` 가 함께 흔들린다.

```
[모드: 맵 선택]                     [모드: 스테이지 선택]
┌────────────────────────┐        ┌────────────────────────┐
│ 전투터                  │        │ 들쥐 들판   2/4 클리어   │
│  들쥐 들판    2/4       │  →     │  들판 1      ✓ 클리어    │
│  가시덤불     0/3 (잠김) │        │  들판 2      ✓ 클리어    │
│  ...                    │        │  들판 3      ← 다음      │
│                         │        │  들판 4      🔒 들판 3을 깨야 한다 │
│  [이전] [다음]   [닫기]  │        │  [이전] [다음] [뒤로] [닫기] │
└────────────────────────┘        └────────────────────────┘
```

**같은 6개 행 오브젝트를 두 모드가 재사용한다.** 그래서 씬에 만들어 둘 행은 6개뿐이고,
맵이 늘든 스테이지가 늘든 **`BuildAll` 이 필요 없다** (함정 3.1 을 정면으로 푸는 방법).

공개 표면:

```csharp
public class BattlePanel : GamePanel
{
    public Text titleText;
    public Text infoText;
    public Text[] rowTexts;          // 6개. GameBootstrap 이 채운다
    public StageMapDatabase stageDatabase;
    public ItemDatabase itemDatabase;   // 잠금 사유에 아이템 이름을 쓴다
    public CareManager careManager;

    public void SelectRow(int rowIndex);   // 버튼 6개가 각자 인덱스를 넘긴다 (함정 3.6)
    public void NextPage();
    public void PreviousPage();
    public void GoBack();                  // 스테이지 목록 → 맵 목록
}
```

`SelectRow` 는 **모드에 따라 갈린다** — 맵 모드면 그 맵을 열고, 스테이지 모드면 전투를 시작한다.
잠긴 스테이지를 누르면 **전투를 열지 않고 잠금 사유를 로그로 띄운다.**

**페이지 규칙** (모드와 무관하게 같다)

- 한 페이지는 **6칸**. 목록의 `page * 6` 번째부터 6개를 그린다
- 남는 칸은 **빈 문자열**로 두고 눌러도 아무 일이 없다 — 버튼을 지우거나 만들지 않는다(함정 3.1)
- `NextPage` 는 **다음 페이지에 그릴 것이 있을 때만** 넘어간다. `PreviousPage` 는 0 에서 멈춘다.
  경계에서 조용히 아무 일도 일어나지 않는 것이 맞다 — 되감기는 혼란스럽다
- 모드를 바꾸면(맵 선택 → 스테이지 목록, 뒤로) **페이지를 0 으로 되돌린다**
- 제목 옆에 `2/3` 처럼 현재 페이지를 보여준다. 페이지가 하나뿐이면 표시하지 않는다

패널을 열 때(`Refresh`)는 **항상 맵 모드로 시작**하고 페이지를 0 으로 되돌린다 —
지난번에 보던 화면이 남아 있으면 무엇을 보고 있는지 헷갈린다.

파티가 비었을 때 씬 전환 전에 막는 기존 검사는 그대로 둔다.

---

## 6. 검증

### 6.1 EditMode — 규칙과 데이터

| 픽스처 | 무엇을 |
|---|---|
| `StageProgressTests` | 기록·중복 무시·빈 id 거부·순서 보존 |
| `StageUnlockEvaluatorTests` | 조건 없으면 열림 / 이전 클리어 / 지정 스테이지 클리어 / 아이템 보유 / AND 조합 / **첫 스테이지의 `PreviousStageCleared` 는 열림** / 잠금 사유 문장 |
| `SaveSerializerTests` (확장) | v4 왕복 / **v3 을 읽어 진행도만 빈 채로 승격** / v2 거부 / 빈 id·중복 걸러내기 |
| `PanelLabelsTests` (확장) | 맵 행(클리어 수/전체) · 스테이지 행(클리어·다음·잠김) · 페이지 표시 |
| `GameDataIntegrationTests` (확장) | **실제 에셋에서 `stageId` 가 비지 않고 전 맵을 통틀어 유일한가** / 스테이지마다 적 총원이 `BattleField.MaxEnemies`(8) 이하인가 / 첫 스테이지가 열려 있는가 / **맵마다 `field` 가 배선됐는가** |
| `BattleHandoffTests` (확장) | 요청에 실린 `Field` 가 그대로 나오는가 / 결과의 `StageId` 가 그대로 나오는가 |

**`stageId` 유일성 검사가 이 설계의 핵심 방어선이다.** 중복이면 한 스테이지를 깼을 때
다른 스테이지도 깬 것이 된다 — 예외 없이 조용히 틀린다.

### 6.2 씬 무결성 검사 (확장)

- `battlePanel.stageDatabase` · `itemDatabase` 배선
- **행 6개 · 버튼 6개**가 존재하는가 (데이터 개수가 아니라 **고정 6**)
- `rowTexts` 가 6개인가
- 페이지·뒤로 버튼의 리스너

기존 `Encounter{i}` 개수 검사(= `BattleEncounterDatabase.Count`)는 **삭제한다** — 그 전제가 사라졌다.

### 6.3 PlayMode 스모크

전투 씬 자체는 안 바뀌므로 7개가 그대로 통과해야 한다.
**단 인카운터를 얻는 경로가 바뀌므로**(`BattleSceneRoot` 의 연습 전투 폴백은 그대로) 확인한다.

### 6.4 에셋 재생성 — 기존 인카운터를 지운다

`BattleEncounter` 에 필드가 늘어도 **기존 `.asset` 에는 빈 값이 들어간다**
(`stageId` 가 빈 문자열이면 진행도를 기록할 수 없다). seed-only 라 코드가 채워주지도 않는다.

→ **`RatField.asset` · `BoarThicket.asset` · `GolemRidge.asset` 를 지우고 `BuildAll` 로 다시 깐다.**
지금은 밸런스를 만진 적이 없으므로 잃을 것이 없다(밸런스는 개발 후에 하기로 했다).

새로 깔 씨앗:

```
StageMap "들쥐 들판"  (field = FieldMap)
  들판 1   stageId "field_1"   들쥐 2      unlock: (없음)
  들판 2   stageId "field_2"   들쥐 3      unlock: [PreviousStageCleared]
  들판 3   stageId "field_3"   가시멧돼지 1 + 들쥐 2   unlock: [PreviousStageCleared]
  들판 4   stageId "field_4"   바위골렘 1   unlock: [PreviousStageCleared]
```

맵은 하나로 시작한다 — 전장이 `FieldMap` 하나뿐이라 두 번째 맵을 만들면 같은 전장을 쓰는
가짜 구분이 된다. **맵을 늘리는 것은 에셋 작업이지 코드 작업이 아니다**(화면이 6칸 + 페이지라
`BuildAll` 없이 는다).

---

## 7. 범위 밖

- **건물별 개별 씬** — 별도 프로젝트. 이 패널이 그대로 씬으로 옮겨간다
- **드랍 아이템** — 전투 보상이 아이템을 주는 것. `RequiresItem` 은 준비돼 있다
- **케어 방출** — 헛간 8마리 상한(HANDOFF 4.3)
- **밸런스 수치** — 개발이 끝난 뒤에 한다. 이번에 새로 까는 스테이지 4개의 적 구성은
  난이도 곡선을 진지하게 잡은 값이 아니라 **자리를 채운 씨앗**이다
- 스테이지 첫 클리어 보너스, 별점·등급, 재도전 보상 감소

---

## 8. 남는 위험

**`stageId` 는 한 번 배포되면 바꾸기 어렵다.** 세이브에 문자열로 박히므로, id 를 바꾸면
그 스테이지를 깬 기록이 사라진다. 6.1 의 유일성 검사는 중복을 막지만 **개명은 막지 못한다.**
개명이 필요해지면 세이브 승격 코드에 옮김 표를 넣어야 한다.

**맵이 하나뿐이라 맵 선택 화면이 한 줄짜리로 시작한다.** 구조는 맞지만 당장은
"눌러서 들어가는 단계가 하나 늘었을 뿐"으로 느껴질 수 있다. 맵이 둘 이상이 되어야 값을 한다.

**해금이 막히면 게임이 멈춘다.** 다음 스테이지가 너무 어려우면 진행이 불가능해지는데,
지금은 재화를 벌 다른 수단이 없다(전투가 유일한 수입). 밸런스 단계에서 반드시 볼 것 —
**이전 스테이지를 반복해서 재화를 벌 수 있으므로 완전히 막히지는 않는다**는 것이 안전장치다.
