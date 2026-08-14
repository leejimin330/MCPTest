# 작업 인수인계 — 2026-08-14

**프로젝트:** `C:\Users\이지민\UnityProjects\MCPTest` (Unity 6000.2.8f1, 2D)
**이 문서:** 지금 상태 / 함정 / 다음 할 것. **재개할 때 이것부터 읽는다.**

> 이전 판(2026-08-06)은 "육성 조작 수단이 화면에 없다 / 창고 UI 가 1순위" 상태였다.
> 그 뒤 8/13 에 **창고·육성·헛간 패널과 전투**가 전부 들어왔고,
> 8/14 에 **전투가 별도 씬으로 나가면서 실제 전투 화면**이 생겨 내용이 또 바뀌었다.

---

## 1. 지금 상태 한눈에

| 항목 | 상태 |
|---|---|
| EditMode 테스트 | **148/148 통과** (2026-08-14) |
| 씬 | **2개** — `FarmScene` · `BattleScene`. 둘 다 빌드 설정에 등록됨 |
| 씬 무결성 검사 | **통과** — 두 씬을 각각 열어 검사한다. 농장 버튼 32/32, 전투 버튼 3/3, 슬롯 4+8 |
| 게임 플레이 | 육성 · 장착 · 진화 · 파티 편성 · **전투 씬 자동전투** · 저장까지 **한 바퀴 가능** |
| Play 확인 | ⚠️ **전투 화면은 아직 손으로 확인하지 않았다.** 8절 참조 |
| git | **저장소 있음** (2026-08-14 에 만듦). 기본 브랜치 `main`, 작업 브랜치 `feature/battle-screen` |

> **2026-08-14 에 막힌 버그를 하나 고쳤다.** 그 전까지 씬에는 창고 [장착] 버튼과
> 전투터 상대 버튼이 **한 개도 없었다** — 즉 장착도 전투도 화면에서 할 수 없었다.
> 원인은 3.9 함정이다. 검사·테스트가 전부 통과하는 상태로 숨어 있었다.

### 무엇이 되고 무엇이 안 되나

| 건물 / 진입점 | 상태 |
|---|---|
| 헛간 (x −9) | ✅ 보유 목록 · 파티 편성 · 육성 대상 선택 |
| 부화소 (x −3) | ❌ **패널 없음.** "알 시스템 준비 중" 로그만 |
| 창고 (x +3) | ✅ 장착 / 해제 |
| 상점 (x +9) | ⚠️ **전투터 패널이 임시로 붙어 있다.** 인카운터를 고르면 전투 씬으로 간다. 상점 기능은 없다 |
| 전투 씬 | ✅ 탑뷰 맵 · 유닛 · 체력바 · 로그 · 배속(1x/2x) · 나가기 |
| 육성 패널 | ✅ 헛간 줄의 `[육성]` 버튼으로 연다 (먹이 · 훈련 3종 · 저장) |
| 필드 케어 클릭 | ✅ **헛간으로 돌려보낸다** (육성 패널이 아니다) |

**화면에 상시 버튼이 없다.** 좌상단 상태 패널과 하단 로그 한 줄만 상시로 보이고,
조작은 전부 클릭해서 여는 패널 안에 있다.

---

## 2. 지금까지 만든 것

### 2.1 코어 (2026-08-06, 전부 테스트로 검증)

**3계층 분리** — Data(ScriptableObject) / Core(순수 C#) / Presentation(MonoBehaviour)

- **육성**: 먹이주기(포만도+25, 친밀도+2, 경험치+5), 훈련(스탯+2, 경험치+15, 포만도−15,
  친밀도−1, 포만도 20 미만이면 거부)
- **장착**: 슬롯 **1칸**. 끼면 보너스, 빼면 사라짐, **소모되지 않음**. 교체 시 이전 장비는 인벤토리로 복귀
- **스탯 2층 구조**: 기본 스탯(훈련으로 영구 상승, 세이브 대상) + 장비 보너스(장착 중에만)
- **진화**: 조건을 AND 로 조합. `ExpAtLeast` / `StatAtLeast`(장비 보너스 포함) /
  `RequiresEquipped`(**장착 중**이어야 함, 보유만으로는 불가)

**진화 라인**: 스프리글링 → (경험치100 + 공격15) 와일드비스트 / (경험치100 + 블룸스톤 장착) 블로섬 가디언

**아이템(전부 장착형)**: 무쇠 발톱(공격+3, id `hearty_feed`) / 돌 껍질(방어+3, id `iron_grain`)
/ 블룸 스톤(체력+5, id `bloom_stone`)
> id 는 초기 이름 그대로다. 표시 이름만 바꿨다.

### 2.2 패널 · 전투 · 로스터 (2026-08-13)

설계 근거는 **`docs/superpowers/specs/2026-08-13-battle-and-roster-design.md`** 에 있다.
여기서는 재개에 필요한 것만 적는다.

- **로스터**: 보유 최대 8 ⊇ 파티 최대 4 = 필드에 나온 케어 = 전투 출전 멤버.
  **보유 목록 순서는 절대 바뀌지 않는다** (버튼이 인덱스를 들고 있다)
- **`GameState`**: `Roster` + `Inventory` + `Gold` + `Target`(육성 대상) 한 덩어리
- **전투**: 자동전투, **난수 없음**(같은 입력 → 같은 결과). 피해 = `공격 − 방어/2`, 최소 1.
  종마다 고유 스킬 1개(진화하면 바뀐다). 적 3종: 들쥐(8) / 가시멧돼지(18) / 바위골렘(40)
- **전투는 케어에게 아무것도 남기지 않는다** — 깎인 체력은 전투 중에만 존재. 부상·사망 없음
- **세이브 v3**: 보유 배열 + 파티 인덱스 + 대상 인덱스 + 재화.
  **v2 이하는 새 게임으로 폴백**. 여전히 **기본 스탯만** 저장한다

### 2.3 화면 (측면 뷰)

- 땅 윗면(`y = −2.0`) 위에서 **좌우로만** 이동. 플레이어 A/D·←/→, 파티원은 자동 배회
- 맵 반너비 13, 16:9 화면 반너비 8.89 → **드래그/스와이프로 좌우 슬라이드** (관성 + 경계 클램프)
- 12px 이상 끌면 슬라이드로 간주해 건물/케어 클릭이 발동하지 않음
- 건물 4동 — **개수가 고정**이다. `SceneIntegrityCheck` 와 `GameRoot.farmBuildings` 가 4를 단언한다

### 2.4 전투 화면 (2026-08-14)

설계 근거는 **`docs/superpowers/specs/2026-08-14-battle-screen-design.md`**,
구현 계획은 **`docs/superpowers/plans/2026-08-14-battle-screen.md`** 에 있다.

- **별도 전투 씬**이다. 농장에서 인카운터를 고르면 `BattleScene` 으로 넘어가고,
  [나가기]를 누르면 농장으로 돌아온다.
- **좌표는 표시 전용이다.** 유닛은 맵의 스폰 영역 안 랜덤 좌표에 서지만
  `BattleSimulator` 는 좌표를 한 번도 읽지 않는다 — 그래서 **스폰이 랜덤이어도 결과는 결정적**이고,
  시뮬레이터와 그 테스트 13개는 **한 줄도 바뀌지 않았다.**
- **배속은 공짜로 얻었다.** 시뮬레이터가 시간을 바깥에서만 받으므로 `Time.deltaTime * speed` 한 줄이다.
- **`BattleHandoff`** — 순수 C# 정적 전달자가 씬 사이로 상태를 나른다.
  요청은 `{GameState, BattleEncounter}`, 결과는 `{BattleConclusion, GoldReward, GameState}`.
  **꺼내면 비워진다** — 안 그러면 농장에 돌아올 때마다 지난 결과가 다시 적용된다.
- **결과에 `GameState` 를 함께 싣는 이유:** 농장 씬이 다시 로드되면 `GameRoot.Start` 가 처음부터 돈다.
  거기서 세이브를 읽으면 **저장 버튼을 누르지 않은 진행(먹이·훈련·장착)이 전부 날아간다.**
  그래서 들려보낸 상태 객체를 그대로 돌려받아 이어 쓴다.
- **재화가 들어오는 지점은 `GameRoot.ApplyBattleResult` 한 곳뿐이다.** 전투 씬은 액수만 실어 보낸다.
  씬이 중간에 바뀌어도 이중 지급이 없다.
- **인카운터** = 표시 이름 + `{적, 마리수}` 목록 + 맵. 적 총원은 `BattleField.MaxEnemies`(8)를 넘을 수 없다.
- 체력바·이름표는 uGUI 가 아니라 **`SpriteRenderer` / `TextMesh`** 다. 월드 좌표에 uGUI 를 붙이려면
  World Space Canvas 가 필요한데, 농장이 이미 건물 이름표를 `TextMesh` 로 처리하고 있어 같은 방식을 따랐다.
  덤으로 무결성 검사 1번(모든 `SpriteRenderer` 가 에셋을 참조하는가)이 체력바까지 덮는다.
- **전투 씬을 단독으로 Play 하면** 연습 전투(들쥐 떼 + 스프리글링 1마리)로 시작하고 로그로 알린다.

---

## 3. 함정 — 지금도 전부 유효하다

### 3.1 코드로 만든 Sprite 는 씬에 저장되지 않는다 ★가장 크게 물린 것

`HideFlags.HideAndDontSave` 로 만든 런타임 Sprite 는 에셋이 아니라 직렬화되지 않는다.
BuildAll 을 실행한 그 세션에서는 멀쩡히 보이지만, **에디터를 껐다 켜면 SpriteRenderer 가
전부 `m_Sprite: {fileID: 0}`** 이 되어 땅·건물·캐릭터가 전부 사라진다.

→ `Assets/GameData/Sprites/Square.png`(8×8), `Circle.png`(64×64) 를 **실제 PNG 에셋**으로
생성하도록 고침. PPU = 텍스처 크기라 스케일 1 이 정확히 1 월드 유닛.

**교훈: 같은 세션에서 캡처해 확인하는 것으로는 저장 문제를 절대 못 잡는다.**
그래서 `SceneIntegrityCheck` 를 만들었다 (5.3).

**따라오는 규칙: UI 행·필드 슬롯을 런타임에 생성하지 않는다.** 패널 행 개수는
`ItemDatabase` 크기·`Roster.MaxMembers` 로, 필드 슬롯은 `Roster.MaxParty` 로
**씬 생성 시점에 고정**된다. 동적 생성 오브젝트는 스프라이트 참조가 씬에 남지 않고,
그 버튼은 persistent listener 가 아니라 무결성 검사도 볼 수 없다.
**아이템이나 상한을 늘리면 `BuildAll` 재실행이 필요하다.**

### 3.2 Play 모드에서는 씬 재생성이 불가능

`EditorSceneManager.NewScene` 이 예외를 던진다. BuildAll 전에 `EditorApplication.isPlaying` 확인이 필요하다.

### 3.3 스크립트 재컴파일 중에는 unity-mcp 브리지가 끊긴다

`Unity not detected (no fresh discovery files found)` 가 뜬다. 버그가 아니라 정상 동작이다.
`~/.unity/mcp/connections/bridge-*.json` 의 수정 시각이 12초 이내로 갱신되면 복구된 것이다.

### 3.4 Unity 재시작 시 빈 씬으로 열릴 수 있다

배치모드 실행이 "마지막으로 연 씬" 기록을 건드리는 듯하다. Project 창에서
`Assets/Scenes/FarmScene.unity` 를 **더블클릭**해서 열어야 한다.

### 3.5 배치모드 실행은 Unity 에디터가 닫혀 있어야 한다

프로젝트 잠금 때문이다. **종료 코드만 믿지 말고 생성물을 직접 검사할 것** —
버튼 리스너 미등록과 폰트 누락을 종료 코드 0 인 상태에서 놓친 적이 있다.

### 3.6 uGUI persistent listener 는 Object 인자를 제대로 직렬화하지 못한다

`UnityEventTools.AddObjectPersistentListener` 는 `m_MethodName` 이 빈 항목만 남긴다.
**int 인덱스**를 넘기는 방식(`AddIntPersistentListener`)만 정상 동작한다.

**이것이 로스터 설계를 결정했다.** 헛간 버튼이 보유 목록 인덱스를 들고 있어서
**보유 목록의 순서를 절대 바꿀 수 없다** (정렬·삭제 시 주의).

### 3.7 한글 UI 폰트는 런타임에 주입해야 한다

`Font.CreateDynamicFontFromOSFont` 결과는 에셋이 아니라 씬에 저장되지 않는다.
Canvas 의 `UiFontApplier` 가 `Awake` 에서 모든 `Text` 에 주입한다 (비활성 패널 포함).
**Play 전에는 텍스트가 안 보이는 것이 정상이다.**
단 **패널은 반드시 Canvas 하위**에 둬야 한다 — 형제로 두면 폰트가 안 붙어 한글이 안 보인다.

### 3.8 패널은 저장 시점에 닫혀 있어야 한다

켜 둔 채로 저장하면 게임을 시작하자마자 패널이 떠 있다.
`GameBootstrap` 이 배선을 전부 끝낸 **뒤에** 닫고, `SceneIntegrityCheck` 가 4개 전부
닫혀 있는지 검사한다.

### 3.9 NewScene 은 그 이전에 얻은 에셋 참조를 죽인다 ★2026-08-14 에 물린 것

`EditorSceneManager.NewScene(..., Single)` 은 참조되지 않는 에셋을 언로드하면서
**그 전에 로드해 둔 ScriptableObject 의 네이티브 객체를 파괴**한다. 그러면

```csharp
ReferenceEquals(itemDb, null)  // false — 관리 래퍼는 살아 있다
itemDb != null                 // false — Unity 의 == 연산자는 죽은 객체를 null 로 본다
```

가 되어, 다음 코드가 **예외 없이 조용히 0** 을 돌려준다.

```csharp
int rowCount = itemDb != null && itemDb.items != null ? itemDb.items.Count : 0;
```

그 결과 창고 [장착] 버튼 3개와 전투터 상대 버튼 3개가 **한 개도 만들어지지 않은 채**
씬이 저장됐다. 장착도 전투도 불가능한 게임이 종료 코드 0 으로 나왔다.

**왜 아무 검사도 못 잡았나** — 이게 이 함정의 진짜 교훈이다.
- 테스트 122개는 순수 C# 이라 씬을 보지 않는다.
- 무결성 검사는 "버튼 26개 중 26개 연결됨" 으로 통과시켰다.
  **있는 버튼의 리스너만 봤지, 있어야 할 버튼이 있는지는 보지 않았다.**
- 참조 검사도 전부 통과했다. 죽은 래퍼도 **인스턴스 ID 는 남아 있어 직렬화는 정상**이라,
  `careManager.itemDatabase` 는 런타임에 멀쩡히 살아난다. 빌드 때 읽은 쪽만 0 이었다.

**대응**
1. `BuildAll` 이 **에셋을 만지기 전에** `NewScene` 을 부르고, 씬을 `BuildScene` 에 넘긴다.
2. 개수가 0 이면 `Debug.LogError` 로 크게 실패시킨다 — 0 은 정상 상황이 아니다.
3. 무결성 검사 12번이 `Equip{i}` / `Row{i}` / `Enemy{i}` / `RowButtons{i}` 개수를
   데이터베이스 크기와 대조한다.

**일반화: 개수가 데이터에서 나오는 UI 는 "존재 자체"를 단언해야 한다.**
루프가 0회 돌아도 예외는 나지 않는다.

### 3.10 `BuildAll` 이 `NewScene` 을 두 번 부른다 ★2026-08-14 에 추가

씬이 둘이 되면서 `NewScene` 도 두 번 불린다. **두 번째 호출이 첫 번째 이후에 얻은
에셋 참조를 다시 죽인다** (3.9와 같은 원리). 그래서 전투 씬을 채우기 전에
`ReloadSpriteAssets()` 와 `AssetDatabase.LoadAssetAtPath` 로 필요한 것을 **전부 다시 로드한다.**

이 재로드를 빠뜨리면 슬롯도 버튼도 없는 전투 씬이 **종료 코드 0 으로** 조용히 저장된다.

> 무결성 검사가 이 실패를 실제로 잡는지 확인해 뒀다: 적 슬롯 끄기 루프를 빼고 재생성하면
> `BuildAll` 은 종료 코드 0 으로 성공하지만 무결성 검사가 8건을 잡아 1 로 실패한다.

### 3.11 전투 데이터는 seed-only 다 — 코드로 초기값을 바꿔도 반영되지 않는다 ★2026-08-14 에 추가

`EnemyData` · `BattleMapData` · `BattleEncounter` · `BattleEncounterDatabase` 는
`LoadOrSeed` 로 만든다. **이미 있으면 손대지 않는다.** 인스펙터에서 만진 밸런스를
보존하기 위한 것이지만(설계 2.2), 반대로 `GameBootstrap` 의 초기값을 고쳐도
**기존 에셋에는 안 들어간다.**

초기값을 다시 깔려면 해당 `.asset` 을 지우고 `BuildAll` 을 돌린다.
종·아이템·씬은 여전히 `LoadOrCreate` 로 코드가 전권을 쥔다.

---

## 4. 알려진 문제 / 결정 대기

### 4.1 상점 자리를 전투가 쓰고 있다 ★다음 작업의 걸림돌

전투 전용 건물이 없어 `GameBootstrap` 이 `BuildingKind.Shop` → `BattlePanel` 로 임시 배선했다.
전투가 별도 씬으로 나갔어도 **진입점은 그대로 상점 건물**이다 — 씬 분리는 이 문제를 풀지 않는다.
**상점을 만들려면 전투 진입점을 먼저 옮겨야 한다.**
선택지: 건물 5동으로 늘리기 / 필드 바깥 별도 진입점 / 상점 안에 전투 탭. **미결.**

건물을 5동으로 늘리는 쪽은 `SceneIntegrityCheck` 의 "4동" 단언, `GameRoot.farmBuildings`,
건물 x 좌표 배치(−9/−3/+3/+9)를 함께 건드린다.

### 4.2 재화에 쓸 곳이 없다

`Gold` 는 전투로 쌓이기만 한다. `TrySpendGold` 를 부르는 곳이 없어 바깥 루프가 닫히지 않는다.

### 4.3 알을 어디서 얻을지 미정

부화소 착수의 선행 조건이다. 상점 구매 / 전투 드랍 / 둘 다 — **미결.**

### 4.4 밸런스 — 장비가 진화 속도에 영향을 못 준다

와일드비스트 경로에서 **경험치가 병목**이다. 공격 조건(15)은 6번째 행동에 충족되는데
경험치 100 은 8번째에야 찬다. 그래서 공격 장비를 껴도 진화 시점이 그대로다(둘 다 8행동).

현재 상태는 `WildbeastPath_IsGatedByExpNotAttack` 테스트가 고정해 두었다.
밸런스를 바꾸려면 (예: 공격 조건 19 로 상향, 또는 경험치 70 으로 하향) 그 테스트도 갱신할 것.

전투가 생기면서 장비가 **전투력**에는 영향을 주게 됐지만, 진화 시점에는 여전히 영향이 없다.

### 4.5 피해 공식은 초안이다

`공격 − 방어/2`(최소 1)는 적 3종에 맞춘 값일 뿐 검증된 곡선이 아니다. 상대가 늘면 다시 볼 것.

### 4.6 파티원 초기 배치가 맵 밖으로 계산된다 (사소)

`PartyField.SpawnX` 가 `HalfWidth * 1.4`(= 18.2)까지 펼치는데 맵 반너비는 13 이다.
바깥쪽 두 명은 `FarmArea.OnGround` 의 클램프에 걸려 **맵 가장자리에 붙어 선다**.
게다가 `PlaceAt` 이 클램프 전 좌표를 목표로 잡아, 도착 판정이 서지 않고 재탐색 타이머가
돌 때까지 잠깐 멈춰 있는다. 곧 정상 배회로 넘어가므로 눈에 잘 띄지는 않는다.
고치려면 `usable` 을 `HalfWidth` 안쪽으로 잡고 `PlaceAt` 에서 클램프된 값을 목표로 쓰면 된다.

### 4.7 git 저장소 없음

커밋 이력이 없어 되돌릴 지점이 없다. 사용자가 보류한 상태.

---

## 5. 자주 쓰는 명령

에디터 경로: `C:\Program Files\Unity\Hub\Editor\6000.2.8f1\Editor\Unity.exe`
**아래 셋 다 Unity 에디터가 닫혀 있어야 한다.**

### 5.1 테스트 (148개)

```bash
Unity.exe -batchmode -runTests \
  -projectPath "C:\Users\이지민\UnityProjects\MCPTest" \
  -testPlatform EditMode \
  -testResults "<경로>\results.xml" -logFile "<경로>\test.log"
```
종료 코드 0 = 전체 통과, 2 = 실패. `results.xml` 의 `<test-run total= passed= failed=>` 확인.

| 픽스처 | 개수 |  | 픽스처 | 개수 |
|---|---|---|---|---|
| `CareActionsTests` | 18 | | `PanelLabelsTests` | 21 |
| `SaveSerializerTests` | 17 | | `BattleSimulatorTests` | 13 |
| `EvolutionEvaluatorTests` | 11 | | `GameDataIntegrationTests` | 11 |
| `CreatureInstanceTests` | 10 | | `RosterTests` | 10 |
| `GameStateTests` | 8 | | `DataLayerTests` | 5 |
| `BattleDataTests` | 8 | | `SpawnPointPickerTests` | 7 |
| `BattleHandoffTests` | 7 | | `PipelineSmokeTests` | 2 |
| | | | **합계** | **148** |

> `BattleHandoffTests` 는 정적 상태를 쓴다. `SetUp`/`TearDown` 에서 `BattleHandoff.Clear()`
> 를 부르지 않으면 테스트끼리 서로를 오염시킨다.

### 5.2 에셋 + 씬 전량 재생성

```bash
Unity.exe -batchmode -quit \
  -projectPath "C:\Users\이지민\UnityProjects\MCPTest" \
  -executeMethod FarmCreature.EditorTools.GameBootstrap.BuildAll \
  -logFile "<경로>\rebuild.log"
```
에디터 안에서는 메뉴 **FarmCreature → Build All**.

### 5.3 씬 무결성 검사 ★씬을 바꿨으면 반드시 실행 (두 씬을 검사한다)

```bash
Unity.exe -batchmode \
  -projectPath "C:\Users\이지민\UnityProjects\MCPTest" \
  -executeMethod FarmCreature.EditorTools.SceneIntegrityCheck.Run \
  -logFile "<경로>\integrity.log"
```
저장된 씬을 **새 프로세스에서 열어** 검사한다. 세 덩어리다:

1. **빌드 설정** — 두 씬이 등록되고 켜져 있는가. 빠지면 `LoadScene` 이 **런타임에** 실패한다
2. **농장 씬** — 스프라이트 에셋 참조 / 건물 4동+콜라이더 / GameRoot·CareManager 배선 /
   **패널 4종이 닫혀 있는지** / `battlePanel.encounterDatabase` / HUD 텍스트 /
   버튼 리스너(**비활성 포함**) / 데이터 기반 버튼 개수
3. **전투 씬** — 스프라이트가 전부 에셋인가 / `BattleSceneRoot` 배선 7종 /
   아군 슬롯 4 · 적 슬롯 8 이 **존재하고 조각이 다 붙어 있고 꺼져 있는지** / 버튼 3개 리스너 / 카메라

통과 = 종료 코드 0, 실패 = 1.

### 5.4 세이브 파일 위치

```
C:\Users\이지민\AppData\LocalLow\DefaultCompany\MCPTest\save.json
```
초기화하려면 지우면 된다. v2 이하 세이브는 자동 거부되고 새 게임으로 시작한다.

---

## 6. 파일 구조

```
Assets/
  GameData/
    Species/   Sprigling · Wildbeast · BlossomGuardian (.asset)
    Items/     HeartyFeed · IronGrain · BloomStone (.asset)
    Enemies/   FieldRat · ThornBoar · StoneGolem (.asset)      ← seed-only (함정 3.11)
    Battle/    FieldMap · RatField · BoarThicket · GolemRidge  ← seed-only (함정 3.11)
    Sprites/   Square.png (8×8) · Circle.png (64×64)   ← 실제 에셋이어야 한다
    SpeciesDatabase.asset · ItemDatabase.asset · EnemyDatabase.asset
    BattleEncounterDatabase.asset                              ← seed-only
  Scenes/FarmScene.unity            ← 손으로 편집하지 말 것. BuildAll 이 전량 생성
  Scenes/BattleScene.unity          ← 위와 같음
  Scripts/
    Runtime/
      Data/          StatType · StatBlock · CreatureSpeciesData · ItemData
                     SkillDefinition · EnemyData · EnemyDatabase
                     EvolutionRequirement · EvolutionCondition
                     SpeciesDatabase · ItemDatabase
                     BattleMapData · BattleEncounter(+EncounterEntry) · BattleEncounterDatabase
      Core/          CreatureInstance · Inventory · ActionResult · CareActions
                     EvolutionEvaluator · Roster · GameState
                     BattleUnit · BattleSimulator          ← 전투 화면 작업에서 손대지 않았다
                     SpawnPointPicker · BattleHandoff(+BattleConclusion/Request/Result)
                     SaveData · SaveSerializer
      Presentation/  GameRoot · CareManager · GameHud · UiFontApplier
                     GamePanel(추상) · StoragePanel · RaisingPanel · BarnPanel · BattlePanel
                     PanelLabels · PartyField
                     CreatureController · PlayerController · CameraPanController
                     FarmArea · FarmBuilding · PlaceholderSprites · SaveSystem
                     BattleField · BattleUnitView · BattleSceneRoot    ← 전투 씬
    Editor/          GameBootstrap · SceneIntegrityCheck
    Tests/EditMode/  15개 파일(픽스처 14 + TestFixtures 헬퍼), 테스트 148개
docs/
  PRD.md                                              제품 요구사항 (v1.1, 코드와 동기화됨)
  HANDOFF.md                                          이 문서
  superpowers/specs/2026-08-06-...-design.md          MVP 설계
  superpowers/specs/2026-08-13-building-panels-design.md   창고·육성 패널 설계
  superpowers/specs/2026-08-13-battle-and-roster-design.md 전투·로스터 설계(사후 기록)
  superpowers/specs/2026-08-14-battle-screen-design.md     전투 화면 설계 ✅ 구현됨
  superpowers/plans/2026-08-06-...-mvp.md             MVP 구현 계획 + 실행 기록
  superpowers/plans/2026-08-14-battle-screen.md       전투 화면 구현 계획 + 실행 기록
```

**씬을 손으로 편집하지 말 것.** `GameBootstrap.BuildAll()` 이 전부 생성하므로,
씬을 바꾸려면 `GameBootstrap.cs` 를 고치고 재생성한다.

---

## 7. 다음 할 것

```
✅ 전투 + 전투 화면 (재화 획득)  ──▶  ⬜ 상점 (알·장비 구매)
                                          │
                                          ├──▶  ⬜ 부화소 (알 → 몬스터)
                                          │              │
                                          │              ▼
                                          │         ✅ 헛간
                                          │
                                          └──▶  ✅ 창고
```

**0순위 — 전투 화면 Play 확인.** 구현은 끝났고 자동 검증(테스트 148 · 무결성 검사 2씬)도
통과했지만, **실제로 눌러 본 적이 없다.** 8절의 전투 화면 항목을 먼저 지운다.
이 프로젝트는 "테스트·검사 전부 통과인데 게임은 안 되는" 사고를 이미 한 번 겪었다(3.9).

**1순위 — 상점.** 재화가 이미 쌓이므로 경제 순환을 닫는 가장 빠른 길이다.
단 **4.1(전투 진입점 이동)을 먼저 정해야 한다.** 이건 코드가 아니라 결정의 문제다.

**2순위 — 부화소.** 4.3(알 획득 경로)이 정해져야 착수할 수 있다.
상점이 먼저 생기면 "상점에서 알을 산다" 로 자연히 풀린다.

**나중에**: 상호작용 아이템 + "장착 후 특정 상호작용" 진화 경로, 소모품 별도 계통 재도입,
다단 진화, 진화 선택 UI, 실시간 경과, 실제 아트, 자동 저장, 밸런스 재조정(4.4).

---

## 8. 미검증 항목 — 실제로 Play 해서 확인할 것

자동 테스트와 씬 검사로는 덮이지 않는다.

**2026-08-14 에 Play 로 확인한 것** (unity-mcp 로 실제 버튼 `onClick` 을 눌러 검증)

- [x] 창고에서 장착 → 공격 5 → 8, 행 텍스트가 "장착 중 · 여분 2" 로 즉시 갱신
- [x] 해제 → 공격 8 → 5 로 원복
- [x] 전투터에서 들쥐 선택 → 3.6초 만에 승리, **재화 8 획득**, 보상 1회만 지급
  — ⚠️ 이건 **패널 안에서 굴리던 옛 전투**다. 지금은 별도 씬이라 다시 확인해야 한다
- [x] 장착 보너스가 전투에 실제로 반영됨 (평타 피해 8 = 공격 8 − 방어 1/2)
- [x] 한글 UI 폰트(맑은 고딕)가 붙고 깨지지 않음
- [x] 패널 4종이 저장 시점에 닫혀 있고, 열면 뜸

> **주의:** 에디터 창이 비활성이면 `runInBackground=false` 라 플레이어 루프가 멈춘다.
> 전투가 0.55초에서 멈춘 것처럼 보이는 건 게임 버그가 아니다.
> 배경에서 굴리려면 `Application.runInBackground = true` 를 켜라.

**아직 손으로 확인 안 한 것**

**8/14 전투 화면 — ★0순위. 자동 검증은 전부 통과했으나 눌러 본 적이 없다**

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

> `BattleSceneRoot.Start` 가 `Application.runInBackground = true` 를 켜므로
> 전투 씬은 에디터 창이 비활성이어도 흐른다. 농장 씬은 여전히 영향을 받는다.

**8/06 부터 남아 있던 것**

- [ ] 드래그/스와이프 좌우 슬라이드가 부드럽게 동작하는가
- [ ] 건물 클릭 시 패널이 뜨는가 (드래그와 구분되는가)
- [ ] 마우스 올렸을 때 건물이 밝아지는가
- [ ] A/D·←/→ 좌우 이동, 맵 경계에서 멈추는가
- [ ] 파티원이 땅 위를 배회하고 방향에 따라 뒤집히는가
- [ ] 한글 UI 가 깨지지 않고 보이는가 (Play 후에만 폰트가 붙는다)

**8/13 에 들어온 것**

- [ ] 헛간에서 파티에 넣고 빼면 필드의 케어가 실제로 늘고 주는가
- [ ] 필드의 케어를 클릭하면 헛간으로 돌아가는가
- [ ] 헛간 `[육성]` 으로 대상을 바꾸면 창고·상태 패널이 같은 케어를 가리키는가
- [ ] 먹이·훈련으로 수치가 즉시 갱신되고, 포만도 부족 시 거부 메시지가 뜨는가
- [ ] **진화가 실제로 일어나고 필드의 겉모습(색·크기)이 즉시 바뀌는가** — 처음으로 손으로 확인 가능해졌다
- [ ] 저장 후 재시작하면 보유·파티·육성 대상·재화가 그대로 복원되는가

> 전투 관련 항목은 위의 "8/14 전투 화면" 목록으로 옮겼다 — 전투가 별도 씬이 되면서 확인할 것이 달라졌다.
