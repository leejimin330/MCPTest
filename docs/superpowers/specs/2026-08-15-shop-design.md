# 설계 — 상점 (2026-08-15)

**상태:** 설계 승인됨. 구현 전.
**전제:** `2026-08-14-battle-screen-design.md` 에서 이어진다. HANDOFF 4.1(전투 진입점)과
4.3(알 획득 경로)을 여기서 닫는다.

전투로 재화가 쌓이지만 쓸 곳이 없다. `TrySpendGold` 를 부르는 코드가 한 줄도 없어
바깥 루프가 열린 채다. 이 문서는 그 루프를 닫는 상점의 설계다.

동시에 전투 진입점을 상점 건물에서 떼어낸다. 지금 `GameBootstrap` 이
`BuildingKind.Shop` → `BattlePanel` 로 임시 배선해 둔 탓에, **전투를 옮기지 않으면
상점을 만들 자리가 없다.**

---

## 1. 확정된 결정

| 항목 | 결정 |
|---|---|
| 전투 진입점 | **전투터 건물 신설.** 농장 건물이 4동 → **5동** 이 된다 |
| 상점이 파는 것 | **장비 + 알.** 알 획득 경로(4.3)가 "상점에서 산다" 로 정해진다 |
| 이번 범위 | **상점까지.** 산 알은 인벤토리에 쌓이고, 부화는 다음 작업이다 |
| 알의 계통 | **`ItemData` 와 별개인 `EggData`.** 알은 장착 아이템이 아니다 |
| 가격이 사는 곳 | **`ShopStockDatabase`** — 판매 목록과 가격을 한곳에 모은다 |
| 재고 | **무한.** 되팔기 없음 |
| 세이브 | **v3 그대로.** 포맷을 바꾸지 않는다 |

### 1.1 알을 별도 계통으로 두는 이유

`ItemData` 는 "끼면 스탯이 오르는 것" 이다 — `targetStat` 과 `amount` 가 그 전부다.
알은 끼는 물건이 아니다. 같은 목록에 넣으면 두 가지가 따라온다.

1. **창고에 "알 [장착]" 행이 생긴다.** 창고 행은 `ItemDatabase` 인덱스와 1:1 이고
   개수가 씬 생성 시점에 고정된다(함정 3.1). 알을 거르려면 창고가 쓰는 인덱스를
   "장비만 걸러낸 인덱스" 로 바꿔야 하는데, 그 인덱스 계약이 바로 함정 3.6·3.9 가
   물었던 자리다. **검증이 끝난 경로를 이유 없이 흔드는 셈이다.**
2. 장비 에셋에 `hatchesInto` 같은 무의미한 필드가 붙는다.

`EggData` 를 따로 두면 **알을 장착할 수 없다는 것이 타입으로 보장되고,**
창고 코드는 한 줄도 바뀌지 않는다.

### 1.2 세이브를 안 건드리는 이유

`Inventory` 는 `문자열 id → 개수` 이고, `SaveSerializer` 는 그 목록을 그대로 쓴다
(`itemIds` / `itemCounts`). **id 가 무엇을 가리키는지 세이브는 모른다.**
그래서 알을 같은 인벤토리에 담으면 저장·복원이 공짜로 따라오고 v3 가 유지된다.

대신 id 공간이 겹치면 안 된다 → **알 id 는 `egg_` 로 시작한다.**

> 복원 시 `itemDatabase.Find` 를 부르는 곳은 **장착 아이템 한 곳뿐**이다
> (`TryRestoreCreature`). 인벤토리 항목은 id 를 그대로 담기만 하므로
> 알 id 가 섞여도 복원이 실패하지 않는다.

---

## 2. 맵과 건물

### 2.1 맵을 넓힌다 (`HalfWidth` 13 → 15)

건물 본체 폭은 3.4 다. 지금 폭(13)에 5동을 넣으면 양 끝 건물이 화면 밖으로 잘린다 —
카메라는 `HalfWidth − 화면 반너비(8.89)` 로 클램프되므로 **보이는 한계가 곧 `HalfWidth`** 이고,
+12 에 선 건물의 오른쪽 가장자리는 13.7 이다.

`FarmArea.HalfWidth` 는 상수 하나이고 땅·잔디 스케일, 배회 범위, 카메라 클램프가
전부 그것을 읽는다. **15 로 올리면 나머지는 따라온다.**

### 2.2 배치

| 건물 | x | 패널 |
|---|---|---|
| 헛간 | −12 | `BarnPanel` |
| 부화소 | −6 | 없음 (다음 작업) |
| 창고 | 0 | `StoragePanel` |
| 상점 | +6 | **`ShopPanel`** (신규) |
| 전투터 | +12 | `BattlePanel` (임시 배선 해소) |

`BuildingKind` 에 `BattleGround` 를 추가한다. `FarmBuilding` 의 안내 문구 switch,
`GameBootstrap` 의 건물↔패널 배선, `SceneIntegrityCheck` 의 "4동" 단언이 함께 바뀐다.

### 2.3 곁들여 고치는 것 — HANDOFF 4.6

`PartyField.SpawnX` 가 `HalfWidth * 1.4` 까지 펼쳐 맵 밖을 목표로 잡는다.
`FarmArea.OnGround` 의 클램프에 걸려 파티원이 가장자리에 붙어 서고, `PlaceAt` 이
클램프 전 좌표를 목표로 삼아 도착 판정이 서지 않는다.

맵 폭을 건드리는 작업이므로 여기서 같이 고친다: **펼침 범위를 `HalfWidth` 안쪽으로 잡고,
`PlaceAt` 은 클램프된 좌표를 목표로 쓴다.**

---

## 3. 데이터 — 새 에셋 2계통

전부 `LoadOrCreate` 로 만든다. **seed-only 가 아니다**(함정 3.11) — 코드에서 가격을
고치면 기존 에셋에도 반영된다. 밸런스가 아직 초안이라 코드가 전권을 쥐는 편이 낫다.

### 3.1 `EggData` / `EggDatabase`

```
EggData (ScriptableObject)
  eggId        "egg_sprigling"      ← 인벤토리·세이브가 쓰는 안정적 식별자
  displayName  "스프리글링 알"
  hatchesInto  CreatureSpeciesData  ← 부화소가 다음 작업에서 읽는다

EggDatabase
  eggs : List<EggData>
  Find(eggId) → EggData
```

이번 작업에서 `hatchesInto` 를 읽는 코드는 **없다.** 부화가 다음 작업이기 때문이다.
그래도 지금 넣는 이유는 알 에셋에 "무엇이 나오는가" 가 빠지면 알이 아니기 때문이고,
데이터 검사가 이 참조를 단언한다(6.1).

### 3.2 `ShopStockDatabase`

```
ShopEntry (직렬화 가능한 클래스)
  kind      Equipment | Egg
  targetId  "hearty_feed" | "egg_sprigling"
  price     int

ShopStockDatabase
  entries : List<ShopEntry>
```

**상점 행 개수가 이 목록에서 나온다.** `kind` 는 **표시 이름을 어느 데이터베이스에서 찾을지**만
정한다 — 구매 자체는 `targetId` 를 인벤토리에 더하는 것이라 둘이 다르지 않다(4절).

가격을 `ItemData` 가 아니라 여기 두는 이유:
아이템 정의는 "무엇인가" 이고 가격은 "상점이 얼마에 파는가" 다. 나중에 할인·품절·
해금 조건이 붙는다면 전부 이쪽이다.

초안 재고:

| 항목 | 가격 | 근거 |
|---|---|---|
| 무쇠 발톱 (공격+3) | 30 | 들쥐 전투 보상이 8 이므로 약 4회 |
| 돌 껍질 (방어+3) | 30 | 위와 같음 |
| 블룸 스톤 (체력+5) | 45 | 진화 조건에 쓰이므로 조금 비싸다 |
| 스프리글링 알 | 60 | 약 8회. 새 케어가 가장 비싼 물건이다 |

밸런스는 검증된 곡선이 아니다. 4.5(피해 공식)와 같은 성격의 초안이다.

---

## 4. 코어 — 구매

```csharp
public static class ShopService
{
    public static ActionResult TryBuy(GameState state, ShopEntry entry);
}
```

순수 C# 이고 `UnityEngine` 에 의존하지 않는다 — 그래야 EditMode 테스트가 그냥 돈다.

**규칙**

1. `state` 나 `entry` 가 없으면 실패.
2. `price <= 0` 이면 실패. **공짜 항목은 정상 상황이 아니다** — 데이터 실수를 조용히 넘기지 않는다.
3. `state.TrySpendGold(price)` 가 false 면 실패하고 **아무것도 바꾸지 않는다.**
4. 성공 시에만 `state.Inventory.Add(entry.targetId, 1)`.

차감이 먼저이고 지급이 나중이다. `TrySpendGold` 는 부족하면 아무것도 바꾸지 않으므로
**절반만 적용되는 상태가 생기지 않는다.**

`Message` 는 로그에 그대로 나간다("무쇠 발톱을 샀다. (−30)" / "재화가 모자란다.").

> 보유 상한은 두지 않는다. 알을 몇 개 사든 인벤토리에 쌓일 뿐이고,
> 케어 8마리 상한은 **부화** 시점에 걸리는 문제다 — 다음 작업의 몫이다.

---

## 5. 화면 — `ShopPanel`

`GamePanel` 을 상속한다. 창고와 같은 뼈대다.

- 행은 `ShopStockDatabase` 인덱스와 **1:1**, 개수는 씬 생성 시점에 고정된다(함정 3.1).
- `[구매]` 버튼은 **int 인자 persistent listener** 로 인덱스를 넘긴다(함정 3.6).
- 표시 문자열은 전부 `PanelLabels` 에 모은다 — 그래야 `PanelLabelsTests` 가 덮는다.

```
[상점]                          재화 128

무쇠 발톱 · 공격+3 · 30G · 보유 3      [구매]
돌 껍질  · 방어+3 · 30G · 보유 3      [구매]
블룸 스톤 · 체력+5 · 45G · 보유 1      [구매]
스프리글링 알 · 60G · 보유 0           [구매]

                                      [닫기]
```

구매 결과는 `CareManager.SetLog` 로 하단 로그 한 줄에 띄우고 패널을 즉시 `Refresh` 한다
(성공·실패 모두). 재화 표시는 `PanelLabels.GoldLine` 을 재사용한다.

패널은 배선이 끝난 **뒤에** 닫아서 저장한다(함정 3.8). `careManager.panels` 가 5개가 된다.

**`ShopPanel` 이 알 이름을 아는 방법:** `EggDatabase` 참조를 들고 `targetId` 로 찾는다.
`GamePanel` 이 이미 들고 있는 `ItemDatabase` 와 나란히, `GameBootstrap` 이 채운다.

---

## 6. 검증

### 6.1 EditMode 테스트 (신규)

| 픽스처 | 덮는 것 |
|---|---|
| `ShopServiceTests` | 성공 시 재화 차감 + 인벤토리 증가 / 잔액 부족 시 **아무것도 안 바뀜** / 가격 0 이하 거부 / null 인자 |
| `GameDataIntegrationTests` (확장) | 재고 항목의 `targetId` 가 실제 아이템·알을 가리키는가 / 가격 > 0 / 알의 `hatchesInto` 가 있는가 / **알 id 가 `egg_` 로 시작하고 아이템 id 와 겹치지 않는가** |
| `PanelLabelsTests` (확장) | 상점 행·제목 문자열 |
| `SaveSerializerTests` (확장) | **알이 담긴 인벤토리가 v3 로 라운드트립되는가** |

### 6.2 씬 무결성 검사 (확장)

- 건물 **5동** 과 콜라이더
- 패널 **5종** 이 전부 닫힌 채 저장됐는가
- **상점 행·버튼 개수 = `ShopStockDatabase` 크기.** 0 이면 실패 —
  함정 3.9 가 정확히 이 검사의 부재로 숨었다
- 상점 건물 → `ShopPanel`, 전투터 건물 → `BattlePanel` 배선
- `shopPanel.stockDatabase` / `shopPanel.eggDatabase` 가 채워져 있는가

### 6.3 실행 순서

1. `SceneIntegrityCheck` 를 먼저 5동·행 개수로 올린다 (검사가 실패하는 것을 확인)
2. `BuildAll` 재실행 → 무결성 검사 통과 확인
3. EditMode 전량 → PlayMode 스모크 전량

**`BuildAll` 에서 새 데이터베이스는 `NewScene` 이후에 다시 로드한다**(함정 3.10).
빠뜨리면 행이 하나도 없는 상점이 **종료 코드 0 으로** 저장된다.

---

## 7. 범위 밖

- **부화소 / 알을 깨는 것** — 다음 작업. 이번에는 알이 인벤토리에 쌓이기만 한다
- 되팔기, 재고 한정, 할인, 해금 조건
- 알에서 스프리글링 외의 종이 나오는 것
- 전투 드랍으로 아이템을 얻는 것 (재화만 나온다)

---

## 8. 남는 위험

| 위험 | 대응 |
|---|---|
| 씬 재생성 중 에셋 참조 사망 (3.9/3.10) | 새 데이터베이스를 `NewScene` 이후 재로드. 개수 0 이면 `LogError` |
| 행 개수가 데이터에서 나오는 UI | 무결성 검사가 "있어야 할 개수" 를 단언 (6.2) |
| 맵 폭 변경의 파급 | `HalfWidth` 를 읽는 곳(땅·잔디·배회·카메라·`PartyField`)을 전수 확인 |
| 가격 밸런스 | 초안이다. 4.5 와 같은 성격 — 상대가 늘면 다시 본다 |
