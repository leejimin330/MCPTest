# 작업 인수인계 — 2026-08-16

**프로젝트:** `C:\Users\이지민\UnityProjects\MCPTest` (Unity 6000.2.8f1, 2D)
**이 문서:** 지금 상태 / 함정 / 다음 할 것. **재개할 때 이것부터 읽는다.**

> 이전 판(2026-08-06)은 "육성 조작 수단이 화면에 없다 / 창고 UI 가 1순위" 상태였다.
> 그 뒤 8/13 에 **창고·육성·헛간 패널과 전투**가 전부 들어왔고,
> 8/14 에 **전투가 별도 씬으로 나가면서 실제 전투 화면**이 생겼고,
> 8/15 에 **상점이 들어오면서 건물이 4동 → 5동**(전투터 신설)이 되고
> 전투 진입점이 상점 건물에서 전투터 건물로 옮겨져 내용이 또 바뀌었다.
> 8/16 에 **부화소**가 들어오면서 다섯 동이 전부 패널을 갖게 됐고,
> **알 → 새 케어**가 이어져 MVP 순환이 닫혔다.
> 같은 날 **전투가 위치 기반으로 바뀌었다** — 유닛이 가장 가까운 적으로 걸어가 사거리 안에서
> 싸운다. **"좌표는 표시 전용"이라는 8/14 의 결정이 뒤집혔다**(2.4절).

---

## 1. 지금 상태 한눈에

| 항목 | 상태 |
|---|---|
| EditMode 테스트 | **203/203 통과** (2026-08-16, 187 + 위치 기반 전투 16) |
| PlayMode 스모크 테스트 | **7/7 통과** — 씬을 실제로 로드해 전투가 도는지 본다 (5.4절). **스폰이 랜덤이라 세 번 연속 통과로 확인했다** |
| 씬 | **2개** — `FarmScene` · `BattleScene`. 둘 다 빌드 설정에 등록됨 |
| 씬 무결성 검사 | **통과** — 두 씬을 각각 열어 검사한다. **건물 5동이 전부 패널을 연다**, 패널 6종 닫힘, 상점 행·버튼 개수 = 재고 4, **부화 행·버튼 개수 = 알 종류 1**, 건물 종류 5종, 진입점 배선(상점→ShopPanel, 전투터→BattlePanel, 부화소→HatchPanel), 건물 x 좌표가 맵 안 |
| 게임 플레이 | 육성 · 장착 · 진화 · 파티 편성 · **전투 씬 자동전투** · **상점 구매**(장비·알) · **알 부화** · 저장까지 **한 바퀴 가능** |
| Play 확인 | ⚠️ **눈으로는 아직 안 봤다.** 동작은 PlayMode 테스트가 덮고, 보기 좋은가는 8절 |
| git | **저장소 있음** (2026-08-14 에 만듦). 기본 브랜치 `main`, 작업 브랜치 `feature/hatchery` |

> **2026-08-14 에 막힌 버그를 하나 고쳤다.** 그 전까지 씬에는 창고 [장착] 버튼과
> 전투터 상대 버튼이 **한 개도 없었다** — 즉 장착도 전투도 화면에서 할 수 없었다.
> 원인은 3.9 함정이다. 검사·테스트가 전부 통과하는 상태로 숨어 있었다.

### 무엇이 되고 무엇이 안 되나

| 건물 / 진입점 | 상태 |
|---|---|
| 헛간 (x −12) | ✅ 보유 목록 · 파티 편성 · 육성 대상 선택 |
| 부화소 (x −6) | ✅ **`HatchPanel`.** 알을 깨서 헛간에 새 케어를 넣는다. 헛간이 가득 차면 **거부하고 알은 남는다** |
| 창고 (x 0) | ✅ 장착 / 해제 |
| 상점 (x +6) | ✅ **`ShopPanel`.** 장비 3종(무쇠 발톱 30 / 돌 껍질 30 / 블룸 스톤 45) + 스프리글링 알(60) 구매. 재고 무한, 되팔기 없음 |
| 전투터 (x +12) | ✅ **신설.** 인카운터를 고르면 전투 씬으로 간다 (4.1 해소 — 상점 건물에서 옮겨왔다) |
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
- **전투**: 자동전투. 유닛이 **가장 가까운 적**으로 걸어가 **사거리 안에서** 싸운다(2.5절).
  **시뮬레이터는 난수를 안 쓰지만 스폰 좌표를 받으므로 매 전투 결과가 다르다** —
  좌표를 고정하면 완전히 결정적이다. 피해 = `공격 − 방어/2`, 최소 1.
  종마다 고유 스킬 1개(진화하면 바뀐다). 적 3종: 들쥐(8) / 가시멧돼지(18) / 바위골렘(40)
- **전투는 케어에게 아무것도 남기지 않는다** — 깎인 체력은 전투 중에만 존재. 부상·사망 없음
- **세이브 v3**: 보유 배열 + 파티 인덱스 + 대상 인덱스 + 재화.
  **v2 이하는 새 게임으로 폴백**. 여전히 **기본 스탯만** 저장한다

### 2.3 화면 (측면 뷰)

- 땅 윗면(`y = −2.0`) 위에서 **좌우로만** 이동. 플레이어 A/D·←/→, 파티원은 자동 배회
- 맵 반너비 **15**(2026-08-15 에 13에서 늘림), 16:9 화면 반너비 8.89
  → **드래그/스와이프로 좌우 슬라이드** (관성 + 경계 클램프)
- 12px 이상 끌면 슬라이드로 간주해 건물/케어 클릭이 발동하지 않음
- 건물 **5동**(2026-08-15 에 전투터 신설로 4동에서 늘림) — **개수가 고정**이다.
  `SceneIntegrityCheck` 와 `GameRoot.farmBuildings` 가 5를 단언한다

### 2.4 전투 화면 (2026-08-14)

설계 근거는 **`docs/superpowers/specs/2026-08-14-battle-screen-design.md`**,
구현 계획은 **`docs/superpowers/plans/2026-08-14-battle-screen.md`** 에 있다.

- **별도 전투 씬**이다. 농장에서 인카운터를 고르면 `BattleScene` 으로 넘어가고,
  [나가기]를 누르면 농장으로 돌아온다.
- ~~**좌표는 표시 전용이다.**~~ → **2026-08-16 에 뒤집혔다. 2.5절을 본다.**
  그때는 시뮬레이터가 좌표를 안 읽어서 스폰이 랜덤이어도 결과가 같았고, 덕분에
  시뮬레이터와 그 테스트 13개를 한 줄도 안 고치고 전투 화면을 만들 수 있었다.
  이제는 좌표가 승패를 정한다.
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

### 2.5 위치 기반 전투 (2026-08-16)

설계 근거는 **`docs/superpowers/specs/2026-08-16-battle-melee-design.md`**,
구현 계획은 **`docs/superpowers/plans/2026-08-16-battle-melee.md`** 에 있다.

- **좌표가 규칙이 됐다.** `BattleUnit` 이 `Position`·`MoveSpeed`·`AttackRange` 를 갖고
  `BattleSimulator` 가 매 틱 위치를 갱신한다. `BattleUnitView` 는 **읽어서 그리기만** 한다
  (예전에는 반대로 뷰가 위치를 정했다).
- **타겟은 가장 가까운 적이고 매 틱 다시 고른다.** 거리가 같으면 목록에서 앞선 쪽 —
  동률에 순서를 못 박아야 결과가 재현된다.
- **이동 중에는 때리지 않는다.** 사거리 밖이면 그 틱은 이동만 한다. 단 **타이머는 계속 차서**
  붙는 순간 평타·스킬이 바로 나간다.
- **회복 스킬만 사거리를 무시한다.** 공격 스킬은 평타와 같은 사거리를 지킨다.
  블로섬 가디언은 회복이 존재 이유인 진화 분기라, 거리 운에 값어치를 맡기지 않았다.
- **스폰은 난전이다.** 아군·적 구역을 나누지 않고 두 구역을 감싸는 사각형에 **한 번에 함께** 뽑는다
  (`BattleSceneRoot.BattlefieldOf`). 나눠 뽑으면 아군 위에 적이 겹쳐 설 수 있다.
  **`BattleMapData` 는 손대지 않았다** — seed-only 라서(함정 3.11) 다음 프로젝트에서 다시 짠다.
- **시뮬레이터는 난수를 부르지 않는다.** 스폰 좌표를 생성자 인자로 받는다. 게임은
  `BattleSceneRoot` 가 굴린 랜덤 좌표를 넘기고, 테스트는 좌표를 손으로 놓는다 →
  **게임은 매번 다르고 테스트는 완전히 결정적**이다.
- **기존 `BattleSimulatorTests` 13개는 단언이 하나도 안 바뀌었다.** 전원을 원점에 겹쳐 놓으면
  거리가 0 이라 항상 사거리 안이고, 동률이 목록 순서로 깨져 **예전 `FirstAlive` 와 같은
  유닛을 고르기 때문**이다. 테스트 헬퍼 `Clustered(n)` 이 그 역할을 한다.

**함정 3.12 를 함께 볼 것** — 밀어내기 간격이 사거리보다 넓으면 전투가 조용히 멎는다.

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

### 3.12 밀어내기 간격이 사거리보다 넓으면 전투가 조용히 멎는다 ★2026-08-16 에 물린 것

유닛은 사거리(`BattleUnit.DefaultAttackRange` = 1.2) 안에 들어야 때리고, 겹치지 않도록
`minSpacing` 안에 들어온 상대에게서 밀려난다. **`minSpacing` 이 사거리보다 넓으면
사거리 안에 들기 전에 밀려나 영원히 못 때린다.**

실제로 `FieldMap.asset` 의 `minSpacing` 은 **1.4** 였다. 맵 데이터의 이 값은 원래
**스폰 간격**용이라 사거리와 무관하게 정해진 것인데, 전투가 위치 기반이 되면서
그대로 전투 규칙에 흘러들었다. 결과는 **20초 동안 아무도 피해를 안 입는 전투**다 —
예외도 로그도 없다.

**대응:** `BattleSimulator` 가 받은 `minSpacing` 을 **가장 짧은 사거리 × 0.75** 로 잘라낸다
(`ClampSpacing`). 데이터 규율에 기대지 않고 규칙 쪽에서 보장한다.
`WideMapSpacing_DoesNotPreventFighting` 이 맵의 실제 값 1.4 로 이것을 고정한다.

**일반화: 다른 목적으로 정해진 데이터가 새 규칙에 흘러들면, 그 데이터는 새 규칙의
불변조건을 모른다.** 규칙 쪽에서 잘라내거나 크게 실패시켜야 한다.

> **이걸 잡은 것은 PlayMode 스모크뿐이다.** EditMode 202개는 전부 통과했다 —
> 테스트가 기본값(0.9)을 쓰고 맵 값을 안 썼기 때문이다. 씬을 실제로 굴리는 검사가
> 왜 필요한지 보여주는 사례다.

---

## 4. 알려진 문제 / 결정 대기

### 4.1 상점 자리를 전투가 쓰고 있다 — **해결됨 (2026-08-15)**

건물을 4동 → **5동**으로 늘려 전투 전용 건물(전투터, x +12)을 새로 세웠다.
`GameBootstrap` 의 배선이 `BuildingKind.Shop` → `ShopPanel`, `BuildingKind.BattleGround` → `BattlePanel`
로 바뀌었다. `SceneIntegrityCheck` 8-3 이 이 배선을 직접 단언한다(상점 건물이 `ShopPanel` 이 아니면,
전투터 건물이 `BattlePanel` 이 아니면 실패).

건물 x 좌표는 −12(헛간) / −6(부화소) / 0(창고) / +6(상점) / +12(전투터)로 바뀌었고,
`FarmArea.HalfWidth` 는 13 → **15**로 늘었다.

### 4.2 재화에 쓸 곳이 없다 — **해결됨 (2026-08-15)**

상점에서 장비 3종과 알을 살 수 있게 됐다. 구매 규칙은 `ShopService.TryBuy` 한 곳에 있다 —
`GameState.TrySpendGold` 로 차감을 먼저 시도하고, 모자라면 **아무것도 바뀌지 않는다**
(절반만 적용된 상태가 생기지 않는다). 가격이 0 이하인 항목은 데이터 실수로 보고 거부한다.

### 4.3 알을 어디서 얻을지 미정 — **결정됨: 상점 구매 (2026-08-15)**

`EggData`/`EggDatabase` 라는 **`ItemData` 와 별개 계통**으로 만들었다 — 창고 행이
`ItemDatabase` 인덱스와 1:1 로 묶여 있어서, 알을 아이템과 같은 계통에 넣으면 창고에 알 행이
생겨버리기 때문이다. 산 알은 인벤토리에 `egg_sprigling` 키로 쌓이지만 **창고에는 안 나타난다.**

세이브 포맷은 **v3 그대로다** — 인벤토리가 문자열 키 기반이라 알 항목이 추가돼도 포맷을 안 바꿔도 된다.

**부화소도 들어왔다 — 해결됨 (2026-08-16).** 규칙은 `HatchService.TryHatch` 한 곳에 있다.
**즉시 부화**(대기 시간 없음)이고, **검사를 전부 끝낸 뒤에 알을 소모한다** — 순서를 뒤집으면
"알은 사라졌는데 케어는 없다" 가 실재한다. 상점(차감 먼저·지급 나중)과 순서만 반대이고 보장은 같다.
세이브 포맷은 **v3 그대로다** — 부화가 바꾸는 인벤토리와 로스터를 v3 가 이미 저장한다.

**대신 새 한계가 생겼다: 헛간 8마리가 차면 부화가 영구히 막힌다.** 케어를 내보내는 기능이 없어서,
한 번 가득 차면 산 알이 인벤토리에 영영 쌓인다. 7절에서 **맵/스테이지 다음 순서**로 잡아 뒀다.

### 4.4 밸런스 — 장비가 진화 속도에 영향을 못 준다

와일드비스트 경로에서 **경험치가 병목**이다. 공격 조건(15)은 6번째 행동에 충족되는데
경험치 100 은 8번째에야 찬다. 그래서 공격 장비를 껴도 진화 시점이 그대로다(둘 다 8행동).

현재 상태는 `WildbeastPath_IsGatedByExpNotAttack` 테스트가 고정해 두었다.
밸런스를 바꾸려면 (예: 공격 조건 19 로 상향, 또는 경험치 70 으로 하향) 그 테스트도 갱신할 것.

전투가 생기면서 장비가 **전투력**에는 영향을 주게 됐지만, 진화 시점에는 여전히 영향이 없다.

### 4.5 피해 공식은 초안이다

`공격 − 방어/2`(최소 1)는 적 3종에 맞춘 값일 뿐 검증된 곡선이 아니다. 상대가 늘면 다시 볼 것.

**2026-08-16 에 체감이 달라졌다.** 접근 시간이 생기고 집중포화가 배치에 따라 갈리면서
같은 수치라도 전투가 다르게 흐른다. **이번 작업에서 수치는 한 개도 안 건드렸다** —
먼저 눈으로 보고, 필요하면 따로 잡는다.

### 4.6 파티원 초기 배치가 맵 밖으로 계산된다 — **해결됨 (2026-08-15)**

`PartyField.SpawnX` 가 예전에는 `HalfWidth * 1.4` 로 맵 바깥을 목표로 잡아, 바깥쪽 파티원이
클램프에 걸려 가장자리에 붙어 서고 도착 판정도 안 서던 문제였다. 이제 **맵 안쪽**
(`FarmArea.HalfWidth − 2`)을 목표로 잡도록 고쳤다. 맵을 15로 넓히면서 함께 손봤다.

### 4.7 git 저장소 없음 — **해결됨 (2026-08-14)**

기본 브랜치 `main`. 기능마다 `feature/*` 브랜치를 파서 작업하고 `--no-ff` 로 병합한다.
**원격이 없다**(GitHub 미연결) — 되돌릴 지점은 있지만 이 PC 밖에는 사본이 없다.

---

## 5. 자주 쓰는 명령

에디터 경로: `C:\Program Files\Unity\Hub\Editor\6000.2.8f1\Editor\Unity.exe`
**아래 셋 다 Unity 에디터가 닫혀 있어야 한다.**

### 5.1 테스트 (203개, 2026-08-16)

```bash
Unity.exe -batchmode -runTests \
  -projectPath "C:\Users\이지민\UnityProjects\MCPTest" \
  -testPlatform EditMode \
  -testResults "<경로>\results.xml" -logFile "<경로>\test.log"
```
종료 코드 0 = 전체 통과, 2 = 실패. `results.xml` 의 `<test-run total= passed= failed=>` 확인.

| 픽스처 | 개수 |  | 픽스처 | 개수 |
|---|---|---|---|---|
| `BattleSimulatorTests` | 29 | | `PanelLabelsTests` | 30 |
| `CareActionsTests` | 18 | | `SaveSerializerTests` | 18 |
| `GameDataIntegrationTests` | 16 | | `EvolutionEvaluatorTests` | 11 |
| `CreatureInstanceTests` | 10 | | `RosterTests` | 10 |
| `GameStateTests` | 8 | | `BattleDataTests` | 8 |
| `SpawnPointPickerTests` | 7 | | `BattleHandoffTests` | 7 |
| `HatchServiceTests` | 7 | | `ShopDataTests` | 6 |
| `ShopServiceTests` | 6 | | `DataLayerTests` | 5 |
| `FarmLayoutTests` | 5 | | `PipelineSmokeTests` | 2 |
| | | | **합계** | **203** |

> `BattleSimulatorTests` 가 8/16 위치 기반 전투 작업에서 **13 → 29** 로 늘었다.
> **기존 13개는 단언이 하나도 안 바뀌었다** — 좌표만 `Clustered(n)` 으로 넘긴다 (2.5절).
>
> 같은 날 부화소 작업에서 `HatchServiceTests`(7) 가 생기고
> `PanelLabelsTests`(26→30) · `GameDataIntegrationTests`(15→16) 이 늘었다.
> 그 전 8/15 상점 작업에서는 `ShopDataTests`(6) · `ShopServiceTests`(6) · `FarmLayoutTests`(5) 가 생겼다.

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
2. **농장 씬** — 스프라이트 에셋 참조 / **건물 5동**+콜라이더 / **다섯 동이 전부 패널을 연다** /
   GameRoot·CareManager 배선(`panels` **6개**) /
   **패널 6종(헛간·창고·육성·전투·상점·부화소)이 닫혀 있는지** / `battlePanel.encounterDatabase` /
   **`shopPanel.stockDatabase`·`shopPanel.eggDatabase` 배선** /
   **`hatchPanel.eggDatabase`·`careManager`·`titleText` 배선** / HUD 텍스트 /
   버튼 리스너(**비활성 포함**) / 데이터 기반 버튼 개수(**상점 [구매] 버튼·판매 행 = `ShopStockDatabase.Count`**,
   **부화소 [부화] 버튼·알 행 = `EggDatabase.eggs.Count`**) /
   **건물 종류 5종이 한 번씩 다 있는가** / **진입점 배선**(상점 건물 → `ShopPanel`,
   전투터 건물 → `BattlePanel`, **부화소 건물 → `HatchPanel`**) /
   **건물 x 좌표가 맵(`FarmArea.HalfWidth`=15) 밖으로 안 나갔는가**
3. **전투 씬** — 스프라이트가 전부 에셋인가 / `BattleSceneRoot` 배선 7종 /
   아군 슬롯 4 · 적 슬롯 8 이 **존재하고 조각이 다 붙어 있고 꺼져 있는지** / 버튼 3개 리스너 / 카메라

통과 = 종료 코드 0, 실패 = 1.

### 5.4 PlayMode 스모크 테스트 (7개) ★전투 씬을 바꿨으면 실행

```bash
Unity.exe -batchmode -runTests \
  -projectPath "C:\Users\이지민\UnityProjects\MCPTest" \
  -testPlatform PlayMode \
  -testResults "<경로>\play-results.xml" -logFile "<경로>\play.log"
```

**씬을 실제로 로드해 프레임을 돌린다.** EditMode 테스트(순수 C#)와 무결성 검사(저장된 씬의 참조)가
덮지 못하는 구멍 — "다 통과하는데 게임은 안 돈다" — 를 메운다.

**스폰이 랜덤이고 이제 그것이 결과를 바꾸므로, 한 번 통과는 증거가 약하다.**
전투를 건드렸으면 **두세 번 돌려** 배치 운에 흔들리지 않는지 본다 (8/16 에 실제로
어떤 배치에서만 실패하는 상태를 겪었다 — 함정 3.12).

단언하는 것: 양쪽에 유닛이 서는가 / **전장 안에 서는가**(난전이라 구역이 하나다) /
**어느 체력바든 줄어드는가**(누가 먼저 맞을지는 배치가 정하므로 특정 슬롯을 지목하지 않는다) /
전투가 끝나 결과가 전달되는가 / **전투 씬이 재화를 직접 건드리지 않는가** /
**왕복해도 저장 전 진행이 남고 보상이 한 번만 들어오는가** / 파티가 비면 씬이 안 바뀌는가.

시간은 `Time.timeScale = 10` 으로 압축한다 — `BattleSceneRoot` 가 `Time.deltaTime` 만 쓰므로
배속과 같은 원리로 그냥 빨라진다.

> **이 테스트가 잡는다는 것을 확인해 뒀다:** `battle.Tick(...)` 을 지우면
> 7개 중 4개가 실패한다(진행에 의존하는 것들). 나머지 3개는 스폰·배치·빈 파티라 통과하는 것이 맞다.

### 5.5 세이브 파일 위치

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
    Eggs/      SpriglingEgg.asset                                        ← 8/15 신설
    Sprites/   Square.png (8×8) · Circle.png (64×64)   ← 실제 에셋이어야 한다
    SpeciesDatabase.asset · ItemDatabase.asset · EnemyDatabase.asset
    BattleEncounterDatabase.asset                              ← seed-only
    EggDatabase.asset · ShopStockDatabase.asset                          ← 8/15 신설
  Scenes/FarmScene.unity            ← 손으로 편집하지 말 것. BuildAll 이 전량 생성
  Scenes/BattleScene.unity          ← 위와 같음
  Scripts/
    Runtime/
      Data/          StatType · StatBlock · CreatureSpeciesData · ItemData
                     SkillDefinition · EnemyData · EnemyDatabase
                     EvolutionRequirement · EvolutionCondition
                     SpeciesDatabase · ItemDatabase
                     BattleMapData · BattleEncounter(+EncounterEntry) · BattleEncounterDatabase
                     EggData · EggDatabase · ShopStockDatabase(+ShopEntry/ShopEntryKind)  ← 8/15 신설
      Core/          CreatureInstance · Inventory · ActionResult · CareActions
                     EvolutionEvaluator · Roster · GameState
                     BattleUnit · BattleSimulator          ← 8/16 위치 기반으로 다시 씀 (2.5절)
                     SpawnPointPicker · BattleHandoff(+BattleConclusion/Request/Result)
                     SaveData · SaveSerializer
                     ShopService                                                          ← 8/15 신설, TryBuy 한 곳
                     HatchService                                                         ← 8/16 신설, TryHatch 한 곳
      Presentation/  GameRoot · CareManager · GameHud · UiFontApplier
                     GamePanel(추상) · StoragePanel · RaisingPanel · BarnPanel · BattlePanel · ShopPanel
                     HatchPanel                                                          ← 8/16 신설
                     PanelLabels · PartyField
                     CreatureController · PlayerController · CameraPanController
                     FarmArea · FarmBuilding · PlaceholderSprites · SaveSystem
                     BattleField · BattleUnitView · BattleSceneRoot    ← 전투 씬
    Editor/          GameBootstrap · SceneIntegrityCheck
    Tests/EditMode/  19개 파일(픽스처 18 + TestFixtures 헬퍼), 테스트 203개
    Tests/PlayMode/  BattleSceneSmokeTests — 씬을 실제로 로드해 돌린다, 테스트 7개
docs/
  PRD.md                                              제품 요구사항 (v1.1, 코드와 동기화됨)
  HANDOFF.md                                          이 문서
  superpowers/specs/2026-08-06-...-design.md          MVP 설계
  superpowers/specs/2026-08-13-building-panels-design.md   창고·육성 패널 설계
  superpowers/specs/2026-08-13-battle-and-roster-design.md 전투·로스터 설계(사후 기록)
  superpowers/specs/2026-08-14-battle-screen-design.md     전투 화면 설계 ✅ 구현됨
  superpowers/specs/2026-08-15-shop-design.md               상점 설계 ✅ 구현됨
  superpowers/specs/2026-08-16-hatchery-design.md           부화소 설계 ✅ 구현됨
  superpowers/specs/2026-08-16-battle-melee-design.md       위치 기반 전투 설계 ✅ 구현됨
  superpowers/plans/2026-08-06-...-mvp.md             MVP 구현 계획 + 실행 기록
  superpowers/plans/2026-08-14-battle-screen.md       전투 화면 구현 계획 + 실행 기록
  superpowers/plans/2026-08-15-shop.md                상점 구현 계획(9 태스크) + 실행 기록
  superpowers/plans/2026-08-16-hatchery.md            부화소 구현 계획(7 태스크) + 실행 기록
  superpowers/plans/2026-08-16-battle-melee.md        위치 기반 전투 구현 계획(8 태스크) + 실행 기록
```

**씬을 손으로 편집하지 말 것.** `GameBootstrap.BuildAll()` 이 전부 생성하므로,
씬을 바꾸려면 `GameBootstrap.cs` 를 고치고 재생성한다.

---

## 7. 다음 할 것

```
✅ 전투 + 전투 화면 (재화 획득)  ──▶  ✅ 상점 (알·장비 구매)
                                          │
                                          ├──▶  ✅ 부화소 (알 → 몬스터)
                                          │              │
                                          │              ▼
                                          │         ✅ 헛간 ──▶ ⬜ 케어 방출  ← 0순위
                                          │
                                          └──▶  ✅ 창고
```

**MVP 순환이 닫혔다.** 전투로 재화를 벌고 → 상점에서 알을 사고 → 부화소에서 깨고 →
헛간에서 키우고 → 다시 전투에 내보낸다. 모든 건물이 제 일을 한다.

**0순위 — 맵/스테이지 + 건물 씬 분리 (한 프로젝트로 묶는다).** 사용자가 2026-08-16 에 요청했다.

- **맵/스테이지**: 전투터에 들어가면 맵을 고르고, 맵마다 스테이지가 나뉜다.
  지금은 `BattleEncounter` 가 맵을 **부속으로** 들고 있어(인카운터 3개가 각자 맵을 가리킨다)
  맵이 상위가 되려면 `BattleEncounter`/`BattleEncounterDatabase` 계통을 다시 짜야 한다.
  진행도(어디까지 깼나)를 저장할 거면 **세이브 v3 도 손댄다.**
- **건물 씬**: 건물을 클릭하면 패널이 아니라 개별 씬으로 들어간다.
  지금은 `FarmScene` 하나에 패널 6종이 얹혀 있고 `GameBootstrap` 이 전량 생성한다.

**둘을 묶는 이유: 전투터가 개별 씬이 되면 그 씬이 곧 맵 선택 화면이다.** 따로 만들면
같은 화면을 두 번 만들게 된다.

> 착수 전에 볼 것: `BattleMapData` 는 **seed-only** 라(함정 3.11) 구조를 바꾸려면 `.asset` 을
> 지워야 한다. 위치 기반 전투 작업에서 이 데이터를 일부러 안 건드린 것이 이것 때문이다
> (설계 5절) — 여기서 한 번에 다시 짠다.

**미룬 것 — 케어 방출.** 부화소가 만든 한계다(4.3). 헛간 8마리가 차면 부화가 **영구히 막히고**
산 알이 인벤토리에 영영 쌓인다. 8/16 에 한 번 0순위로 정했다가 위 요청이 들어와 뒤로 밀었다.

> 주의: 함정 3.6 때문에 **보유 목록 순서를 바꿀 수 없다** — 헛간 버튼이 인덱스를 들고 있다.
> 방출은 "목록에서 빼기"가 아니라 **빈 슬롯을 남기는** 방식이 되어야 할 가능성이 크다.

**나머지 후보**: 밸런스 재조정(4.4·4.5 — 위치 기반이 되면서 체감이 달라졌다) /
알 종류 추가 / 진화 선택 UI / 종별 사거리·이동속도.

**틈날 때 — 상점·부화소·전투 화면을 눈으로 한 번 볼 것.** 동작은 PlayMode 테스트가 지키지만
"보기 좋은가"는 사람만 안다. 8절의 남은 항목이 그것이다.

**나중에**: 상호작용 아이템 + "장착 후 특정 상호작용" 진화 경로, 소모품 별도 계통 재도입,
다단 진화, 실시간 경과(부화 대기 시간 포함), 실제 아트, 자동 저장.

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

**8/16 부화소 — 사람이 눈으로 본 적이 없다.** EditMode 203개·PlayMode 7개·씬 무결성 검사가
전부 통과했지만, Play 해서 확인한 것은 하나도 없다.

- [ ] 부화소를 누르면 패널이 뜨고 제목에 `헛간 n/8` 이 보인다
- [ ] 알을 사서 부화시키면 헛간 목록에 새 케어가 늘고, 알 보유 수가 준다
- [ ] 알이 0개일 때 누르면 "가진 알이 없다" 로그가 뜬다
- [ ] 헛간이 8마리일 때 누르면 거부되고 알이 그대로다
- [ ] 부화 패널(900×420)이 화면에 보기 좋은가

**8/15 상점 — 사람이 눈으로 본 적이 없다.** EditMode·PlayMode 테스트와 씬 무결성 검사는
전부 통과했지만, 실제로 Play 해서 확인한 것은 하나도 없다.

- [ ] 상점에서 장비를 사면 재화가 줄고 창고에 보유 수가 늘어난다
- [ ] 재화가 모자랄 때 "재화가 모자란다" 로그가 뜨고 아무것도 바뀌지 않는다
- [ ] 알을 사면 보유 수가 늘어난다 (아직 쓸 곳은 없다 — 부화소가 다음 작업)
- [ ] 건물 5동이 맵 안에 다 들어오고, 슬라이드로 전부 볼 수 있다
- [ ] 전투터를 눌러야 전투가 열리고, 상점은 상점이 열린다
- [ ] 상점 패널이 화면에 보기 좋은가 (행 4개 + 닫기 버튼이 900×620 안에 들어오는가)
- [ ] 건물 5동이 넓어진 맵(반너비 15)에서 어떻게 보이는가

**8/14 전투 화면 — 남은 것은 "보기 좋은가" 뿐이다**

동작은 PlayMode 스모크 테스트(5.4)가 덮는다. **이미 기계가 확인한 것:**

- [x] 양쪽에 유닛이 서고, 적은 연습 인카운터대로 3마리
- [x] 유닛이 각자의 스폰 영역 안에 선다
- [x] 체력바가 실제로 줄어든다 (시뮬레이터가 돈다)
- [x] 전투가 끝나 결과가 농장으로 건너간다
- [x] 전투 씬이 재화를 직접 건드리지 않는다
- [x] **왕복해도 저장 전 진행이 남고 보상이 한 번만 들어온다**
- [x] 파티가 빈 채로 인카운터를 누르면 씬이 바뀌지 않는다
- [x] `BattleScene` 단독 Play 시 연습 전투로 시작하고 로그로 알린다

**8/16 위치 기반 전투 — 기계는 통과했지만 사람이 본 적이 없다**

- [ ] 유닛이 서로에게 **걸어가서 붙는 것**이 보인다 (멈춰 서서 싸우는 리듬이 읽히는가)
- [ ] 같은 상대에게 몰릴 때 겹쳐 보이지 않는다 (밀어내기가 눈에 자연스러운가)
- [ ] 같은 파티로 다시 싸우면 **결과가 달라진다** (배치 운이 재미로 느껴지는가, 억울한가)
- [ ] 위아래로 움직여도 앞뒤 가림이 자연스럽다 (정렬 순서를 매 프레임 다시 계산한다)
- [ ] 이동 속도(2)와 사거리(1.2)가 화면에서 적당한가 — 너무 느리거나 붙자마자 끝나지 않는가
- [ ] 난전 스폰이 보기에 어지럽지 않은가

**사람만 판단할 수 있는 것 — 눈으로 볼 것:**

- [ ] 유닛 이름이 한글로 보인다 (안 보이면 농장의 건물 이름표도 같은 상태인지 확인 — `TextMesh` 폰트 문제다)
- [ ] 쓰러진 유닛이 반투명으로 남아 무슨 일이 났는지 보인다
- [ ] 유닛이 서로 겹쳐 보이지 않고, 매 전투 배치가 눈에 띄게 달라진다
- [ ] 2x 를 누르면 눈에 띄게 빨라지고, 1x 로 돌아온다
- [ ] 로그가 읽을 만한 속도로 흐른다 (2x 에서 너무 빨라 못 읽지 않는가)
- [ ] 바닥·유닛·체력바 크기가 화면에 알맞다 (카메라 `orthographicSize` 5.6 이 맞는가)
- [ ] 전투 중에 [나가기] → "물러났다" 로그가 농장에 뜬다

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
