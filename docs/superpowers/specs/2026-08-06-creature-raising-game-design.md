# 농장 육성 게임 — 설계 문서

**작성일:** 2026-08-06
**원 설계 세션:** 2026-07-29 23:22~23:57 (Claude Code 세션 `33ba3bf9-10da-46ae-a0d4-0dc02241b9f8`)
**관련 문서:** [PRD.md](../../PRD.md)
**대상:** Unity 6000.2.8f1 / 2D / `C:\Users\이지민\UnityProjects\MCPTest`

---

## 0-A. 2026-08-06 2차 개정 — 무엇이 바뀌었나

사용자 지시로 4가지가 바뀌었다. **이 절이 아래 본문보다 우선한다.**

| # | 변경 | 영향 범위 |
|---|---|---|
| 1 | **시점: 탑다운 → 측면 뷰.** 땅 윗면 위를 좌우로만 이동 | `FarmArea`, `PlayerController`, `CreatureController`, 씬 |
| 2 | **아이템: 소모품 → 장착 아이템으로 통일.** 슬롯 1칸 | `CreatureInstance`, `CareActions`, `SaveData` v2, HUD |
| 3 | **진화 조건: 보유 → 장착.** `RequiresItem` → `RequiresEquipped` | `EvolutionRequirement`, `EvolutionEvaluator` |
| 4 | **건물 4동 + 맵 확장 + 터치 슬라이드.** 맵 반너비 8 → 20 | `FarmBuilding`, `CameraPanController`, 씬 |

**스탯이 2층 구조가 됐다** — 이게 가장 파급이 큰 변경이다.

- **기본 스탯** (`GetBaseStat`) — 훈련으로 오르는 영구값. **세이브에 이것만 기록한다**
- **장비 보너스** (`GetEquipmentBonus`) — 장착 중에만 더해짐
- **합계** (`GetStat`) — 진화 조건과 UI 가 보는 값

세이브에 합계를 기록하면 저장/로드마다 스탯이 불어나므로 반드시 기본값만 저장한다.

**확정된 방향 (구현 전)**
- 몬스터는 **알을 부화**시켜 얻는다 (부화소)
- 재화는 **전투 보상**으로 얻는다 (전투 명세 대기 중)
- 소모품은 장착 아이템과 별개 계통으로 나중에 재도입
- 진화는 "장착" 또는 "장착 + 특정 상호작용"으로 일어난다. 후자는 상호작용이 미정의 상태

**알려진 밸런스 문제.** 와일드비스트 경로에서 공격 조건(15)이 경험치 조건(100)보다 먼저
충족되어, 공격 장비를 껴도 진화가 앞당겨지지 않는다. `WildbeastPath_IsGatedByExpNotAttack`
테스트가 현재 상태를 고정해 두었으니, 밸런스를 바꾸면 그 테스트도 갱신해야 한다.

---

## 0. 이 문서의 위상

1~3장은 2026-07-29 세션에서 사용자 승인까지 완료된 내용을 **그대로 복원**한 것이다.
그 세션은 4장(액션 데이터 흐름 + 세이브 시스템)을 설명하려던 지점에서 끊겼고, 코드는 한 줄도
작성되지 않았다. 4장 이후는 이번(2026-08-06)에 **원 설계의 방향을 따라 새로 확정**한 것이며,
새로 정한 값은 `※ 신규 확정` 으로 표시한다.

### 확정된 MVP 범위 (원 세션 요약 그대로)

- **핵심 루프**: 육성 중심 (다마고치 + 디지몬 월드 스타일), 배틀/탐험 없음
- **MVP 범위**: 육성 + 진화 핵심만. 던전/포획은 다음 단계
- **케어 종수**: 1종 (기본형 → 진화A / 진화B 분기)
- **스탯**: 전투형 스탯(공격/방어/체력) + 돌봄 상태값(배고픔/친밀도)
- **진화 조건**: 경험치 임계치, 스탯 임계치+아이템, 아이템 사용 등 복합 조건
  (아이템은 이미 인벤토리에 있다고 가정, 던전 획득은 다음 단계)
- **화면**: 작은 방을 케어가 돌아다니는 형태
- **데이터 관리**: ScriptableObject 기반
- **아트**: 임시 도형 플레이스홀더
- **시간 시스템**: 실시간 아님, 플레이 중에만 상태 변화
- **세이브**: 간단한 저장/불러오기 필요

---

## 1. 아키텍처 개요

**채택안: ScriptableObject 데이터 + 순수 C# 런타임 인스턴스 분리**

Unity에서 ScriptableObject는 "설계 시점 데이터(에셋)"로 쓰고, 런타임에 값이 바뀌는 상태는 별도의
순수 C# 클래스로 관리한다. SO 에셋 자체를 런타임에 직접 수정하면 에디터에서 값이 영구히
바뀌어버리는 Unity의 흔한 함정이 있어, 이를 구조적으로 차단하는 것이 목적이다.

### 1.1 구성 요소

| 타입 | 종류 | 역할 |
|---|---|---|
| `CreatureSpeciesData` | ScriptableObject | 종/진화 단계별 **정적** 데이터 — 이름, 기본 스탯, 플레이스홀더 색상/크기, 진화 조건 목록 |
| `EvolutionCondition` | 직렬화 클래스 | 요구사항 목록(AND) + 결과 종(`CreatureSpeciesData` 참조) |
| `EvolutionRequirement` | 직렬화 클래스 | 개별 요구사항 하나 (경험치 / 스탯 / 아이템 보유) ※ 신규 확정 |
| `ItemData` | ScriptableObject | 아이템 ID, 이름, 효과(스탯 보정치) |
| `SpeciesDatabase` | ScriptableObject | 모든 종 에셋 목록 — 세이브 로드 시 ID로 종을 되찾을 때 사용 |
| `ItemDatabase` | ScriptableObject | 모든 아이템 에셋 목록 — 세이브 로드 및 UI 나열용 ※ 신규 확정 |
| `CreatureInstance` | 순수 C# | 현재 종 참조, 경험치, 현재 스탯, 돌봄 상태값 — **바뀌는 값 전부** |
| `Inventory` | 순수 C# | 아이템 ID → 개수 ※ 신규 확정 |
| `EvolutionEvaluator` | 순수 C# | 조건 충족 여부만 판단 (Unity API 비의존 → 테스트 용이) |
| `CareActions` | 순수 C# | 먹이/훈련/아이템 액션의 규칙과 결과 ※ 신규 확정 |
| `SaveSystem` | 순수 C# + 파일 IO | `CreatureInstance`+`Inventory` 를 JSON으로 저장/로드 |
| `CreatureController` | MonoBehaviour | 씬의 케어 오브젝트 담당 — 비주얼 갱신 + 배회 이동 |
| `PlayerController` | MonoBehaviour | 방 안 이동 (WASD/화살표) |
| `CareManager` | MonoBehaviour | 액션 버튼 처리 → `CareActions` 호출 → 진화 평가 → 결과 전파 |
| `GameHud` | MonoBehaviour | 상태 패널 / 버튼 / 로그 UI ※ 신규 확정 |
| `UiFontApplier` | MonoBehaviour | Canvas 하위 모든 `Text` 에 OS 한글 폰트를 런타임에 주입 ※ 구현 중 추가 |
| `FarmBuilding` | MonoBehaviour | 배경 건물 4종(헛간/부화소/창고/상점) 클릭 진입점 ※ 2차 개정 |
| `CameraPanController` | MonoBehaviour | 드래그·터치로 카메라 좌우 슬라이드 + 클릭/드래그 구분 ※ 2차 개정 |
| `GameRoot` | MonoBehaviour | 시작 시 로드, 객체 조립, 전역 참조 보관 ※ 신규 확정 |

### 1.2 의존 방향

```
   ┌──────────────────────────────────────────────┐
   │  Presentation (MonoBehaviour)                │
   │  GameRoot · CareManager · GameHud            │
   │  CreatureController · PlayerController       │
   └───────────────┬──────────────────────────────┘
                   │ 호출 (한 방향)
                   ▼
   ┌──────────────────────────────────────────────┐
   │  Core (순수 C#) — UnityEngine 의존 없음       │
   │  CreatureInstance · Inventory                │
   │  EvolutionEvaluator · CareActions            │
   │  SaveData                                    │
   └───────────────┬──────────────────────────────┘
                   │ 읽기 전용 참조
                   ▼
   ┌──────────────────────────────────────────────┐
   │  Data (ScriptableObject) — 절대 런타임 수정 X │
   │  CreatureSpeciesData · ItemData              │
   │  SpeciesDatabase · ItemDatabase              │
   └──────────────────────────────────────────────┘
```

Core 계층은 `UnityEngine` 을 참조하지 않는다. 단 하나의 예외가 `Color`/`Vector` 인데, 이는
Core로 올리지 않고 Data 계층에만 두어 규칙을 지킨다.

> **주의:** `CreatureSpeciesData` 는 ScriptableObject라 `UnityEngine.ScriptableObject` 를
> 상속한다. Core 클래스가 이 타입을 참조하는 이상 완전한 무의존은 불가능하다. 따라서
> **"Core는 UnityEngine API를 *호출*하지 않는다"** 로 규칙을 완화해 적용한다 —
> `Debug.Log`, `Time.deltaTime`, `Random`, `MonoBehaviour` 사용 금지. 데이터 필드 읽기는 허용.
> 이 규칙만으로도 EditMode 테스트에서 문제없이 검증된다. ※ 신규 확정

---

## 2. 세계관 / 배경 설정

- **세계관**: 판타지 자연 세계 — 포켓몬 스타일의 생물학적 성장 느낌
- **배경**: 농장 — 플레이어가 도는 "작은 방(맵)"은 농장의 한 구역
- **케어 콘셉**: 처음부터 판타지 생물 (농장 동물이 아님).
  예시 라인 — `스프리글링(기본형)` → 스탯 훈련으로 `와일드비스트` / 아이템으로 `블로섬 가디언`
  (실제 이름/디자인은 나중에 자유롭게 변경 가능, 지금은 자리표시용 콘셉)
- **플레이어 역할**: 특별히 설정하지 않음 (UI 조작자에 가까움, 별도 캐릭터 설정 없음)

---

## 3. 진화 시스템 상세

### 3.1 진화 조건 (복합 조건 지원)

`EvolutionCondition` 은 여러 하위 요구사항을 **AND** 로 조합한다:

- `ExpAtLeast(value)` — 누적 경험치 임계치
- `StatAtLeast(statType, value)` — 특정 스탯(공격/방어/체력)이 기준치 이상
  (아이템으로 스탯을 올려 도달하는 경우 포함)
- `RequiresItem(itemId)` — 특정 아이템을 소지해야 함
  (던전 획득 아이템은 MVP에서는 인벤토리에 이미 있다고 가정)

각 `CreatureSpeciesData` 는 진화 가능한 `EvolutionCondition` 목록을 가지고, 조건별로 결과
종(다음 `CreatureSpeciesData`)을 지정한다.

### 3.2 평가 시점

매 프레임 체크하지 않고, **상태가 바뀌는 액션 직후에만** 평가한다 — 훈련하기, 먹이주기,
아이템 사용 직후. 성능상 이유도 있고, "방금 무엇 때문에 진화했는지" 인과관계가 명확해진다.

### 3.3 복수 조건 동시 충족 시

조건 목록의 순서상 **먼저 오는 것**을 우선 적용한다 (간단한 우선순위 방식).
나중에 필요하면 플레이어 선택 UI로 확장 가능.

### 3.4 진화 실행 절차

```
조건 충족 확인
   → CreatureInstance.CurrentSpecies 를 결과 종으로 교체
   → 전투 스탯은 유지 (값 그대로 이어받음, 기본치 재계산 안 함)
   → 돌봄 상태값(포만도/친밀도)도 유지
   → 비주얼(플레이스홀더 색/크기) 갱신
   → 진화 로그 메시지 표시
```

### 3.5 MVP 진화 테이블 ※ 신규 확정

| 순위 | 결과 종 | 요구사항 (AND) |
|---|---|---|
| 1 | 와일드비스트 | `ExpAtLeast(100)` + `StatAtLeast(Attack, 15)` |
| 2 | 블로섬 가디언 | `ExpAtLeast(100)` + `RequiresItem("bloom_stone")` |

`RequiresItem` 은 **보유만 확인하고 소모하지 않는다.** (소모까지 하면 "아이템을 써서 스탯을
올린 뒤 그 아이템으로 진화"가 불가능해져 조합이 좁아진다.)

진화형(와일드비스트/블로섬 가디언)은 진화 조건 목록이 비어 있어 더 진화하지 않는다.

---

## 4. 액션 데이터 흐름 ※ 이하 전부 신규 확정

*(원 세션이 여기까지 못 가고 끊겼던 부분)*

### 4.1 상태값 정의

| 값 | 타입 | 범위 | 초기값 |
|---|---|---|---|
| `Exp` | int | 0 이상 (상한 없음) | 0 |
| `Attack` / `Defense` / `Health` | int | 1 이상 | 종별 기본치 (5 / 5 / 20) |
| `Fullness` (포만도) | int | 0~100 | 60 |
| `Bond` (친밀도) | int | 0~100 | 30 |

**용어 확정:** 원 설계의 "배고픔"은 증감 방향이 헷갈린다. **포만도(Fullness) — 높을수록
배부름** 으로 확정한다.

모든 증감은 범위 안으로 **클램프**된다. 스탯은 하한 1, 상한 없음. 포만도/친밀도는 0~100.

### 4.2 액션 규칙

액션은 전부 `CareActions` 의 정적 메서드이며, `ActionResult` 를 반환한다.

```
ActionResult {
    bool   Success
    string Message      // 성공/실패 사유 (UI 로그에 그대로 표시)
}
```

**핵심 규칙: 실패 시 상태를 하나도 건드리지 않는다.** 조건 검사를 전부 통과한 뒤에야 값을
쓴다. (부분 적용 후 실패하면 세이브가 오염된다.)

| 액션 | 사전 조건 | 효과 |
|---|---|---|
| `Feed` | `Fullness < 100` | Fullness +25, Bond +2, Exp +5 |
| `Train(stat)` | `Fullness >= 20` | *stat* +2, Exp +15, Fullness −15, Bond −1 |
| `UseItem(itemId)` | 인벤토리 보유 ≥ 1 | 아이템 스탯 보정 적용, Bond +5, Exp +10, 인벤토리 −1 |

**밸런스 의도:** 훈련 1회당 포만도 15 소모, 먹이 1회당 25 회복 → 먹이 3회로 훈련 5회.
공격 15 도달까지 훈련 5회(5→15는 +10, 훈련당 +2) 이므로, 사료(공격+3) 없이도
훈련만으로 와일드비스트 도달이 가능하다. 경험치 100은 훈련 5회(75) + 먹이 5회(25) = 100 으로
자연스럽게 채워진다.

### 4.3 진화 평가 훅

액션 성공 → 진화 평가는 **`CareManager` 가 조율**한다. `CareActions` 는 진화를 모른다.

```
CareManager.DoFeed()
  ├─ result = CareActions.Feed(instance)
  ├─ if (!result.Success) → 로그만 표시하고 종료
  ├─ hud.Refresh(instance)
  ├─ evolved = EvolutionEvaluator.TryEvolve(instance, inventory)
  └─ if (evolved != null)
        ├─ instance.CurrentSpecies = evolved
        ├─ creatureController.RefreshVisual()
        └─ 로그: "스프리글링이(가) 와일드비스트(으)로 진화했다!"
```

`EvolutionEvaluator.TryEvolve` 는 **판정만** 하고 종을 바꾸지 않는다 — 순수 함수로 유지해
테스트를 쉽게 만든다. 실제 교체는 호출자가 한다.

### 4.4 아이템 효과 적용

`ItemData` 는 `StatType targetStat` + `int amount` 를 가진다. `UseItem` 은 해당 스탯에
`amount` 를 더한다. (회복형·상태이상형 아이템은 MVP 제외 — 필요해지면 `ItemEffectType`
열거형을 추가하는 방향으로 확장한다.)

---

## 5. 세이브 시스템 ※ 신규 확정

### 5.1 저장 포맷

`JsonUtility` 로 직렬화한다 (Unity 내장, 추가 의존 없음). `Dictionary` 를 직렬화하지 못하므로
인벤토리는 **병렬 배열**로 평탄화한다.

```json
{
  "version": 1,
  "speciesId": "sprigling",
  "exp": 40,
  "attack": 9,
  "defense": 5,
  "health": 20,
  "fullness": 45,
  "bond": 34,
  "itemIds":    ["hearty_feed", "iron_grain", "bloom_stone"],
  "itemCounts": [2, 3, 1],
  "savedAtUtc": "2026-08-06T01:40:00Z"
}
```

- 저장 위치: `Application.persistentDataPath/save.json`
- `version` 필드는 지금은 항상 `1`. 포맷이 바뀔 때 마이그레이션 분기점으로 쓴다

### 5.2 로드 절차와 실패 처리

```
파일 없음                → 새 게임으로 시작 (조용히)
JSON 파싱 실패           → 경고 로그 + 새 게임으로 시작
version 미지원           → 경고 로그 + 새 게임으로 시작
speciesId 를 DB에서 못 찾음 → 경고 로그 + 새 게임으로 시작
itemIds / itemCounts 길이 불일치 → 경고 로그 + 새 게임으로 시작
```

**원칙: 깨진 세이브로 게임이 죽지 않는다.** 어떤 실패든 새 게임으로 폴백한다.
저장 파일은 덮어쓰지 않고 그대로 두어 사용자가 수동 복구할 여지를 남긴다.

### 5.3 직렬화 경계

`SaveSystem` 은 두 층으로 나눈다.

- `SaveSerializer` (순수 C#, 파일 IO 없음) — `CreatureInstance`+`Inventory` ↔ `SaveData` ↔ JSON 문자열.
  **여기가 테스트 대상.**
- `SaveSystem` (파일 IO) — 경로 결정과 읽기/쓰기만. 얇게 유지.

이렇게 나눠야 파일 시스템 없이 라운드트립을 테스트할 수 있다.

---

## 6. 씬 구성 ※ 신규 확정

맵은 월드 좌표 기준 **가로 16 × 세로 9** 의 사각 구역. 카메라는 Orthographic size 5, 원점 중심.

```
FarmScene
├── Main Camera            (Orthographic, size 5, 배경색 하늘색)
├── EventSystem            (uGUI 입력용)
├── GameRoot               [GameRoot]
├── Farm
│   └── Ground             [SpriteRenderer] 옅은 녹색, 16×9
├── Creature               [SpriteRenderer][CreatureController]
├── Player                 [SpriteRenderer][PlayerController]
└── Canvas                 (Screen Space - Overlay)
    ├── StatusPanel        [GameHud] — 종 이름/스탯/경험치/포만도/친밀도 텍스트
    ├── ActionPanel        — 먹이주기 / 훈련(공격·방어·체력) / 아이템(3종) 버튼
    ├── SaveButton         — 저장
    └── LogText            — 최근 메시지 한 줄
```

### 6.1 UI 기술 선택

**uGUI(`com.unity.ugui`) + 레거시 `UnityEngine.UI.Text`** 를 쓴다. TextMeshPro는 쓰지 않는다.

이유: TMP는 한글을 렌더링하려면 한글 글리프가 포함된 폰트 에셋을 별도로 만들어야 하고, TMP
Essential Resources 임포트가 에디터 상호작용을 요구한다. 레거시 `Text` 는
`Font.CreateDynamicFontFromOSFont("맑은 고딕", size)` 로 OS 폰트를 그대로 써서 한글이 바로 나온다.

UI를 실제 폰트/디자인으로 다듬는 단계에서 TMP로 옮기는 것은 이후 과제다.

**폰트 주입 시점 (구현 중 확정).** `Font.CreateDynamicFontFromOSFont` 가 만드는 `Font` 는
에셋이 아니라 런타임 객체다. 에디터에서 `Text.font` 에 넣어 씬을 저장하면 `m_Font: {fileID: 0}`
으로 저장돼 **한글이 아예 안 보인다** (실제로 첫 씬 생성에서 이 증상이 나왔다). 그래서 폰트는
씬에 직렬화하지 않고, Canvas 에 붙은 `UiFontApplier` 가 `Awake` 에서
`GetComponentsInChildren<Text>` 로 전부 주입한다. 폰트 후보는
`맑은 고딕` → `Malgun Gothic` → `Gulim` → `Arial` 순으로 폴백한다.

**아이템 버튼의 인자 전달 (구현 중 확정).** `UnityEventTools.AddObjectPersistentListener` 로
`ItemData` 를 버튼 인자로 등록하면 빈 항목(`m_MethodName:` 공란)만 남아 버튼이 동작하지 않는다.
따라서 아이템 버튼은 `ItemDatabase` 의 **인덱스(int)** 를 넘기고, `CareManager.DoUseItem(int)`
가 `itemDatabase.items[index]` 로 해석한다. int 인자 등록(`AddIntPersistentListener`)은 정상
직렬화됨을 확인했다.

### 6.2 플레이스홀더 스프라이트

런타임에 텍스처를 코드로 만들어 `Sprite.Create` 한다 (에셋 파일 불필요).

- 사각형: 1×1 흰색 텍스처 → 스케일로 크기 조절
- 원: 지름 64px 텍스처에 원을 그려 생성, 색은 `SpriteRenderer.color` 로 지정

종별 색/크기는 `CreatureSpeciesData.placeholderColor` / `placeholderScale` 에서 읽는다.

### 6.3 씬·에셋 생성 방식

씬 파일(`.unity`)과 SO 에셋을 **손으로 편집하지 않는다.** 에디터 스크립트
`GameBootstrap.BuildAll()` 이 전부 생성하며, 배치모드 CLI로도 실행 가능하다.

```
Unity.exe -batchmode -projectPath <프로젝트> -executeMethod FarmCreature.EditorTools.GameBootstrap.BuildAll -quit
```

이렇게 하면 씬이 깨져도 명령 한 줄로 복구되고, 데이터 구성이 코드로 남아 리뷰 가능하다.

---

## 7. 폴더 / 어셈블리 구조 ※ 신규 확정

```
Assets/
  Scenes/
    FarmScene.unity                    (GameBootstrap 이 생성)
  GameData/                            (GameBootstrap 이 생성)
    Species/  Sprigling.asset, Wildbeast.asset, BlossomGuardian.asset
    Items/    HeartyFeed.asset, IronGrain.asset, BloomStone.asset
    SpeciesDatabase.asset
    ItemDatabase.asset
  Scripts/
    Runtime/
      FarmCreature.Runtime.asmdef
      Data/         StatType · StatBlock · CreatureSpeciesData · ItemData
                    EvolutionRequirement · EvolutionCondition
                    SpeciesDatabase · ItemDatabase
      Core/         CreatureInstance · Inventory · EvolutionEvaluator
                    CareActions · ActionResult · SaveData · SaveSerializer
      Presentation/ GameRoot · CareManager · GameHud
                    CreatureController · PlayerController
                    PlaceholderSprites · SaveSystem
    Editor/
      FarmCreature.Editor.asmdef       (Runtime 참조)
      GameBootstrap.cs
    Tests/
      EditMode/
        FarmCreature.Tests.EditMode.asmdef   (Runtime 참조, testAssemblies)
        CreatureInstanceTests · EvolutionEvaluatorTests
        CareActionsTests · SaveSerializerTests
```

**네임스페이스:** `FarmCreature`, `FarmCreature.Data`, `FarmCreature.Core`,
`FarmCreature.Presentation`, `FarmCreature.EditorTools`, `FarmCreature.Tests`

### 7.1 추가 패키지

`Packages/manifest.json` 에 아래를 더한다. 둘 다 에디터 설치본에 내장돼 있어 오프라인에서도 해결된다.

- `com.unity.test-framework` — EditMode 테스트
- `com.unity.ugui` — Canvas / Button / Text

---

## 8. 테스트 전략 ※ 신규 확정

EditMode 테스트만 쓴다 (PlayMode 테스트는 MVP 과잉). 대상은 Core 계층 전부.

| 테스트 파일 | 검증 대상 |
|---|---|
| `CreatureInstanceTests` | 스탯 클램프(하한 1), 포만도/친밀도 0~100 클램프, 경험치 누적, 종 교체 시 값 유지 |
| `EvolutionEvaluatorTests` | 단일/복합 조건 판정, 미충족 시 null, 동시 충족 시 목록 순서 우선, 진화형은 더 진화 안 함 |
| `CareActionsTests` | 각 액션의 수치 변화, 실패 조건(포만도 100/20 미만/아이템 0개), **실패 시 무변경** |
| `SaveSerializerTests` | 라운드트립 동일성, 미지원 version 거부, 알 수 없는 speciesId 거부, 배열 길이 불일치 거부 |

MonoBehaviour 계층(`CreatureController` 등)은 자동 테스트하지 않고, PRD 8장의 수동 완료
조건으로 확인한다.

**테스트 실행 (에디터를 닫은 상태에서):**

```
"C:\Program Files\Unity\Hub\Editor\6000.2.8f1\Editor\Unity.exe" \
  -batchmode -runTests \
  -projectPath "C:\Users\이지민\UnityProjects\MCPTest" \
  -testPlatform EditMode \
  -testResults "<결과>.xml" -logFile "<로그>.log"
```

---

## 9. 원 설계에서 바뀐 점 요약

| 항목 | 원 설계 (7/29) | 현재 | 사유 |
|---|---|---|---|
| "배고픔" | 이름만 언급 | **포만도(Fullness), 높을수록 배부름** | 증감 방향 모호성 제거 |
| 진화 요구사항 | 개념만 (`ExpAtLeast` 등) | `EvolutionRequirement` 클래스로 구체화 | 직렬화·조합 필요 |
| 인벤토리 | 언급 없음 | `Inventory` 클래스 신설 | `RequiresItem`/`UseItem` 에 필수 |
| `SaveSystem` | 단일 클래스 | `SaveSerializer`(순수) + `SaveSystem`(IO) 분리 | 파일 없이 테스트 가능하게 |
| UI | 언급 없음 | uGUI + 레거시 Text + OS 동적 폰트 | 한글 렌더링 / TMP 임포트 회피 |
| 씬 구성 | 언급 없음 | 에디터 스크립트로 전량 생성 | 수작업 씬 편집 의존 제거 |
| `ItemDatabase` | 없음 | 신설 | 세이브 로드 시 아이템 역참조 + 버튼 인덱스 해석 |
| 아이템 버튼 인자 | (미설계) | `ItemData` 객체 → **int 인덱스** | Object 인자 persistent listener 가 직렬화되지 않음 |
| UI 폰트 | (미설계) | 에디터 지정 → **런타임 주입** | 동적 OS 폰트는 씬에 직렬화 불가 |

---

## 10. 승인 상태

- 1~3장: **2026-07-29 세션에서 사용자 승인 완료**
- 4~9장: 2026-08-06 작성. 원 설계 방향을 따라 확정했으며, 되돌리기 쉬운 결정들로 구성.
  이견이 있으면 해당 장만 고치면 되고 다른 장에 파급되지 않는다.
