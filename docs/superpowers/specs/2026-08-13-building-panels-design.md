# 설계 — 창고 패널 · 육성 패널 (2026-08-13)

**전제:** `docs/HANDOFF.md` 의 2026-08-06 상태에서 이어진다.
코어(육성/장착/진화/세이브)는 완성돼 있고 **호출할 UI 만 없다.** 이 설계는 그 UI 를 채운다.

---

## 1. 범위

### 만든다

| 대상 | 진입점 | 호출하는 기존 API |
|---|---|---|
| 창고 패널 | 창고 건물 클릭 | `CareManager.DoEquip(int)` / `DoUnequip()` |
| 육성 패널 | **케어 직접 클릭** | `CareManager.DoFeed()` / `DoTrain(int)` / `DoSave()` |

### 만들지 않는다 — 이유를 명시한다

전투 · 상점 · 부화소 · 헛간은 **전투 명세가 도착할 때까지 착수하지 않는다.**
HANDOFF 7절의 의존 그래프상 재화 획득 수단이 전투 하나뿐이라,
명세 없이 만들면 재화 규모 · 알 가격 · 드랍 종류를 전부 추측으로 정하게 되고
명세가 오면 그 추측이 통째로 폐기된다. 건물 클릭 시 "준비 중" 로그는 그대로 둔다.

> **후기 (2026-08-14 추가):** 이 판단은 **같은 날 밤에 뒤집혔다.**
> 전투 명세가 도착해 전투 · 헛간까지 이어서 구현됐고, 케어 1마리 전제가
> 로스터 + 파티로 바뀌면서 세이브도 v3 로 교체됐다.
> 그 작업은 `2026-08-13-battle-and-roster-design.md` 에 사후 기록으로 남겼다.
> **여전히 안 만든 것은 상점과 부화소뿐이다.** 이 문서의 나머지 절(창고 · 육성 패널,
> 저장 함정 대응, 검증 절차)은 지금도 유효하다.
>
> **단 2.5 절은 뒤집혔다.** "케어를 클릭하면 육성 패널이 열린다" 로 설계했으나,
> 파티가 여러 마리가 되면서 **필드의 케어를 클릭하면 헛간으로 돌아가는** 동작이 됐다.
> 육성 패널의 진입점은 **헛간 줄의 `[육성]` 버튼**이다 (대상 변경과 패널 열기를 한 번에 한다).
> 슬라이드 제스처를 걸러내는 부분은 설계대로 유지됐다.

### 육성 UI 위치 — HANDOFF 4.2 결정

**케어 직접 클릭**으로 확정. 근거:

- 육성 대상이 곧 케어라 대상과 조작 위치가 일치한다.
- 건물 수가 4동으로 고정돼 있다. `SceneIntegrityCheck` 가 "건물 4동"을,
  `GameRoot.farmBuildings` 검사가 4개를 단언하므로 5번째 건물은 두 검사와
  건물 x 좌표 배치(-9/-3/+3/+9)를 함께 건드린다. 클릭 진입점은 그 비용이 없다.

---

## 2. 구조

기존 3계층(Data / Core / Presentation)을 그대로 따른다.
**Core 는 건드리지 않는다** — 규칙은 이미 `CareActions` 에 있고 검증도 끝나 있다.
추가는 전부 Presentation 과 Editor 레이어다.

```
Presentation/
  GamePanel.cs      (추상) 열기/닫기/갱신 + 패널 공통 뼈대
  StoragePanel.cs   : GamePanel   장착/해제
  RaisingPanel.cs   : GamePanel   먹이·훈련·저장
  PanelLabels.cs    (static) 행 라벨 문자열 생성 — 순수 함수, 테스트 대상
```

### 2.1 GamePanel (추상)

```
Open()              게임오브젝트 활성화 + Refresh()
Close()             비활성화
abstract Refresh()  현재 상태를 화면에 다시 그린다
IsOpen              활성 여부
```

닫기 버튼은 `Close()` 를 **무인자 persistent listener** 로 부른다.

### 2.2 StoragePanel

`ItemDatabase.items` 를 순회해 아이템 1종당 행 1개를 만든다. 행 구성:

```
무쇠 발톱   공격 +3   소지 3      [장착]
```

- `[장착]` 은 `CareManager.DoEquip(i)` — i 는 `ItemDatabase` 인덱스.
  **int 인자 persistent listener 만 쓴다** (함정 3.6: Object 인자는 직렬화되지 않는다).
- 하단에 `[해제]` 하나 — `CareManager.DoUnequip()`.
- 행 개수는 `ItemDatabase` 크기로 **씬 생성 시점에 고정**된다. 아이템이 늘면 BuildAll 재실행이 필요하다.
  런타임 동적 생성을 쓰지 않는 이유: 동적 생성 행의 버튼은 persistent listener 가 아니라
  `SceneIntegrityCheck` 의 리스너 검사가 볼 수 없다.

### 2.3 RaisingPanel

버튼 6개 — 먹이주기 / 공격 훈련 / 방어 훈련 / 체력 훈련 / 저장 / 닫기.
훈련은 `DoTrain(int)` 에 `StatType` 인덱스(0=공격, 1=방어, 2=체력)를 넘긴다.
`CareActions.Train` 이 이미 포만도 20 미만이면 거부하므로 **패널은 버튼을 막지 않는다.**
거부 사유는 실패 메시지로 로그에 뜬다 — 규칙을 두 곳에 복제하지 않기 위함이다.

### 2.4 갱신 배선 — 이게 없으면 숫자가 안 움직인다

`CareManager.Apply()` 는 현재 `hud.Refresh()` 만 부른다.
장착해도 열려 있는 창고 패널의 "소지 3" 이 그대로 남는다.

→ `CareManager` 에 `public List<GamePanel> panels` 를 추가하고,
`Apply()` 성공/실패 양쪽 뒤에 **열려 있는 패널만** `Refresh()` 한다.
`GameBootstrap` 이 이 목록을 채운다.

### 2.5 케어 클릭 진입점

`CreatureController` 에 `CircleCollider2D` 를 붙이고 `OnMouseUpAsButton` 을 받는다.
`FarmBuilding` 과 **동일하게** `CameraPanController.DraggedThisGesture` 로
좌우 슬라이드 제스처를 걸러낸다 — 걸러내지 않으면 화면을 넘길 때마다 패널이 열린다.

케어는 배회하며 움직이지만 콜라이더가 같은 트랜스폼에 있어 따라간다.
진화로 스케일이 커지면(`RefreshVisual`) 콜라이더 반경도 함께 갱신한다.

---

## 3. 저장 함정 대응 (HANDOFF 3절)

패널은 **기본 비활성**이다. 이 사실이 기존 두 곳을 조용히 무력화한다:

### 3.1 UiFontApplier — 이미 안전하나 확인 필요

`GetComponentsInChildren<Text>(true)` 로 이미 비활성을 포함한다. **수정 불필요.**
단 패널이 Canvas **하위**에 있어야 한다 — 형제로 두면 폰트가 안 붙어 한글이 안 보인다.

### 3.2 SceneIntegrityCheck — 수정 필요 ★

`Object.FindObjectsByType<Button>(FindObjectsSortMode.None)` 는 기본값이
`FindObjectsInactive.Exclude` 라 **비활성 패널 안의 버튼을 한 개도 보지 않는다.**
지금 이대로면 패널 버튼 리스너가 전부 빠져 있어도 검사는 통과한다.

→ `FindObjectsInactive.Include` 오버로드로 바꾸고, 다음을 추가 검사한다:

- 패널 2개(`StoragePanel`, `RaisingPanel`)가 존재하고 **저장 시점에 비활성**인가
- `CareManager.panels` 가 2개를 참조하는가
- 케어에 콜라이더가 있는가
- 버튼 리스너 검사가 비활성 버튼까지 포함하는가

### 3.3 스프라이트

패널 배경은 `Square.png` **에셋**을 쓴다(`Image.sprite`).
런타임 생성 스프라이트는 씬에 `fileID: 0` 으로 저장된다(함정 3.1).

---

## 4. 테스트

패널 배선 자체는 씬 무결성 검사가 담당한다. 자동 테스트는 **순수 로직만** 덮는다.

`PanelLabels` 를 static 순수 함수로 분리해 EditMode 로 검증한다:

- 장착 중인 아이템 행에 표시가 붙는가
- 소지 0 인 아이템의 표시
- 스탯 보너스 부호 표기(`공격 +3`)

패널 자체의 열기/닫기는 EditMode 에서 `GameObject` 를 만들어 `Open()`/`Close()` 후
`IsOpen` 을 확인하는 수준까지만 한다. uGUI 레이아웃은 테스트하지 않는다.

**기존 68개는 전부 유지된다.** Core 를 건드리지 않으므로 회귀 위험이 없다.

---

## 5. 검증 절차 (이 순서로)

1. EditMode 테스트 전량 — 68 + 신규
2. `GameBootstrap.BuildAll` 재생성
3. `SceneIntegrityCheck.Run` — **수정된 버전**으로
4. 저장된 `FarmScene.unity` YAML 을 직접 열어 확인
   (함정 3.5: 종료 코드 0 을 믿지 않는다. 리스너 미등록과 폰트 누락을 코드 0 에서 놓친 적이 있다)

---

## 6. 미해결로 남기는 것

- **전투 명세** — 사용자 전달 대기. 이후 상점 → 부화소 → 헛간.
- **밸런스 4.1** — 장비가 진화 속도에 영향을 못 주는 문제. 이번 범위 밖.
  다만 육성 패널이 생기면 처음으로 손으로 확인할 수 있게 된다.
- **git 저장소 없음** — 되돌릴 지점이 여전히 없다. 이 문서도 커밋되지 않는다.
