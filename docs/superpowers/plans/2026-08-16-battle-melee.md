# 위치 기반 전투 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 유닛이 가장 가까운 적을 향해 움직이고 사거리 안에 들면 멈춰 싸우게 만든다. 같은 파티라도 배치에 따라 승패가 갈린다.

**Architecture:** 좌표를 표현 계층에서 **코어로 올린다.** `BattleUnit` 이 위치·이동속도·사거리를 갖고 `BattleSimulator` 가 매 틱 갱신한다. 시뮬레이터는 **난수를 부르지 않는다** — 스폰 좌표를 생성자 인자로 받으므로 게임은 매번 다르고 테스트는 완전히 결정적이다. `BattleUnitView` 는 유닛의 위치를 읽어 그리기만 한다.

**Tech Stack:** Unity 6000.2.8f1 (2D), C#, NUnit(EditMode/PlayMode), uGUI. 씬은 `GameBootstrap.BuildAll()` 이 전량 생성한다.

**Spec:** `docs/superpowers/specs/2026-08-16-battle-melee-design.md`

## Global Constraints

- **씬을 손으로 편집하지 않는다.** `GameBootstrap.cs` 를 고치고 `BuildAll` 로 재생성한다.
- **배치모드 실행은 Unity 에디터가 닫혀 있어야 한다** (함정 3.5). 열려 있으면 프로젝트 잠금으로 실패한다.
- **종료 코드 0 을 믿지 않는다.** `results.xml` 의 `<test-run total= passed= failed=>` 숫자를 직접 읽는다.
- **시뮬레이터 안에서 난수를 부르지 않는다.** `Random`·`System.Random`·시간 기반 값 전부 금지. 배치를 고르는 일은 `BattleSceneRoot` 가 한다.
- **`DefaultMinSpacing` 은 `DefaultAttackRange` 보다 작아야 한다.** 크면 유닛이 사거리 안에 들기 전에 서로 밀어내 영원히 못 때린다 (설계 2절).
- **분리(밀어내기)는 `dt` 로 스케일한다.** 안 하면 틱 길이에 따라 결과가 갈려 결정성이 깨진다 (설계 4절).
- Core 계층(`Assets/Scripts/Runtime/Core/`)은 UI·MonoBehaviour 에 의존하지 않는다. `Vector2`·`Rect`·`Mathf` 는 허용된다 (`SpawnPointPicker` 선례).
- **밸런스 수치(피해 공식, 적 3종 스탯)는 건드리지 않는다** (설계 8절).
- 브랜치: `feature/battle-melee` (이미 만들어져 있고 설계 문서가 `main` 에 커밋돼 있다).

## 사전 준비 — 로그 폴더와 명령 별칭

모든 배치모드 명령이 아래 경로를 쓴다. **각 세션에서 한 번 실행한다.**

```powershell
$UNITY = "C:\Program Files\Unity\Hub\Editor\6000.2.8f1\Editor\Unity.exe"
$PROJ  = "C:\Users\이지민\UnityProjects\MCPTest"
$LOG   = "$env:TEMP\mcptest"
New-Item -ItemType Directory -Force $LOG | Out-Null
```

**테스트 실행 헬퍼** — Unity 는 배치모드에서도 제어를 일찍 돌려주므로 `Start-Process -Wait` 로 기다린다.

```powershell
function Run-EditMode($filter, $name) {
  $args = @('-batchmode','-runTests','-projectPath',$PROJ,'-testPlatform','EditMode',
            '-testResults',"$LOG\$name.xml",'-logFile',"$LOG\$name.log")
  if ($filter) { $args += @('-testFilter',$filter) }
  $p = Start-Process -FilePath $UNITY -ArgumentList $args -PassThru -Wait
  "EXIT=$($p.ExitCode)"
  if (Test-Path "$LOG\$name.xml") {
    ([xml](Get-Content "$LOG\$name.xml")).'test-run' |
      ForEach-Object { "total=$($_.total) passed=$($_.passed) failed=$($_.failed)" }
  } else { "results.xml 없음 — 컴파일 실패일 수 있다. $LOG\$name.log 를 볼 것" }
}
```

## 파일 구조

| 파일 | 책임 | 태스크 |
|---|---|---|
| `Runtime/Core/BattleUnit.cs` | 유닛 한 명의 상태. **위치·이동속도·사거리가 는다** | 1 |
| `Runtime/Core/BattleSimulator.cs` | 전투 규칙. **좌표를 받고 갱신한다** | 2·3·4·5·6 |
| `Runtime/Presentation/BattleSceneRoot.cs` | 스폰 좌표를 골라 시뮬레이터에 넘긴다 | 2·7 |
| `Runtime/Presentation/BattleUnitView.cs` | 유닛 위치를 **읽어** 그린다 | 7 |
| `Tests/EditMode/BattleSimulatorTests.cs` | 규칙 검증. 기존 13 + 새 15 | 1~8 |
| `Tests/EditMode/PanelLabelsTests.cs` | 생성자 변경에 딸린 호출부 하나 | 2 |
| `Runtime/Core/SpawnPointPicker.cs` | **건드리지 않는다** | — |

## 태스크별 누적 테스트 수

| 태스크 | 새 테스트 | 누적 |
|---|---|---|
| 시작 | — | 187 |
| 1 | 2 | 189 |
| 2 | 1 | 190 |
| 3 | 3 | 193 |
| 4 | 2 | 195 |
| 5 | 2 | 197 |
| 6 | 2 | 199 |
| 7 | 0 (표현 계층) | 199 |
| 8 | 3 | **202** |

---

### Task 1: `BattleUnit` — 위치·이동속도·사거리

**Files:**
- Modify: `Assets/Scripts/Runtime/Core/BattleUnit.cs`
- Test: `Assets/Scripts/Tests/EditMode/BattleSimulatorTests.cs` (파일 끝, 마지막 `}` 두 개 앞)

**Interfaces:**
- Consumes: 없음
- Produces: `BattleUnit.Position`(`Vector2`) / `BattleUnit.MoveSpeed`(`float`) / `BattleUnit.AttackRange`(`float`), 상수 `BattleUnit.DefaultMoveSpeed`(2.0f) / `BattleUnit.DefaultAttackRange`(1.2f) — Task 2~8 이 전부 쓴다

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`BattleSimulatorTests.cs` 의 마지막 `}` 두 개 **앞에** 붙인다.

```csharp
        // ---------- 위치·이동속도·사거리 ----------

        [Test]
        public void FromCreature_GivesDefaultMovementValues()
        {
            var species = TestFixtures.MakeSpecies("sprigling", "스프리글링", 5, 4, 20);
            var unit = BattleUnit.FromCreature(CreatureInstance.CreateNew(species));

            Assert.AreEqual(BattleUnit.DefaultMoveSpeed, unit.MoveSpeed, 0.001f);
            Assert.AreEqual(BattleUnit.DefaultAttackRange, unit.AttackRange, 0.001f);
            Assert.AreEqual(Vector2.zero, unit.Position, "위치는 시뮬레이터가 배치한다");
        }

        [Test]
        public void FromEnemy_GivesDefaultMovementValues()
        {
            var enemy = TestFixtures.MakeEnemy("rat", maxHp: 8, attack: 3, defense: 1,
                attackInterval: 1.6f, goldReward: 4);
            var unit = BattleUnit.FromEnemy(enemy);

            Assert.AreEqual(BattleUnit.DefaultMoveSpeed, unit.MoveSpeed, 0.001f);
            Assert.AreEqual(BattleUnit.DefaultAttackRange, unit.AttackRange, 0.001f);
        }
```

파일 상단에 `using UnityEngine;` 가 없으면 추가한다 (`Vector2` 를 쓴다).

> **픽스처 시그니처** (`Assets/Scripts/Tests/EditMode/TestFixtures.cs`, 확인함). 이 계획의 모든
> 테스트가 이것을 쓴다. 기존 테스트를 따라 **이름 붙인 인자**로 쓴다.
>
> ```csharp
> MakeSpecies(string speciesId, string displayName = null,
>             int attack = 5, int defense = 5, int health = 20)
>
> // 전투용. 평타 간격과 고유 스킬을 직접 지정한다 — 스킬 픽스처를 따로 만들 필요가 없다.
> MakeFighter(string speciesId, int attack, int defense, int health,
>             float attackInterval = 1f, SkillEffect effect = SkillEffect.Damage,
>             float cooldownSeconds = 5f, float multiplier = 2f, string skillName = "테스트 스킬")
>
> MakeEnemy(string enemyId, int maxHp, int attack, int defense,
>           float attackInterval = 1.5f, int goldReward = 10, string displayName = null)
> ```
>
> `MakeSpecies` 는 `attackInterval` 을 안 세운다 — `BattleUnit` 이 1.2f 로 물러난다.
> **전투 동작을 검사할 때는 `MakeFighter` 를 쓴다.**

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```powershell
Run-EditMode "FarmCreature.Tests.BattleSimulatorTests" "t1"
```

기대: **0 이 아니다.** `$LOG\t1.log` 에 `MoveSpeed`/`AttackRange`/`Position` 정의가 없다는 컴파일 오류가 있어야 한다.

- [ ] **Step 3: 최소 구현을 쓴다**

`BattleUnit.cs` 상단에 `using UnityEngine;` 를 추가하고, `IsAlly` 필드 **뒤에** 넣는다.

```csharp
        /// <summary>초당 이동 거리(월드 유닛). 지금은 전 유닛이 같다 (설계 1.5).</summary>
        public float MoveSpeed = DefaultMoveSpeed;

        /// <summary>이 거리 안에 들어야 때린다(월드 유닛).</summary>
        public float AttackRange = DefaultAttackRange;

        /// <summary>
        /// 전장 좌표. <b>전투 중에만 존재한다.</b> 생성자가 아니라
        /// <see cref="BattleSimulator"/> 가 스폰 시점에 배치한다.
        /// </summary>
        public Vector2 Position;
```

같은 클래스 안, 필드 선언부 맨 위에 상수를 둔다.

```csharp
        /// <summary>전 유닛 공통 이동속도. 종별 차등은 두지 않는다 (설계 1.5).</summary>
        public const float DefaultMoveSpeed = 2.0f;

        /// <summary>전 유닛 공통 사거리. BattleSimulator.DefaultMinSpacing 보다 커야 한다 (설계 2절).</summary>
        public const float DefaultAttackRange = 1.2f;
```

`FromCreature` · `FromEnemy` 의 객체 초기화 블록에는 **아무것도 더하지 않는다** — 필드 초기값이 이미 기본값이다.

- [ ] **Step 4: 테스트가 통과하는지 확인한다**

```powershell
Run-EditMode "FarmCreature.Tests.BattleSimulatorTests" "t1"
```

기대: 종료 코드 **0**, `total="15" passed="15"` (기존 13 + 2).

- [ ] **Step 5: 커밋**

```powershell
git add Assets/Scripts/Runtime/Core/BattleUnit.cs Assets/Scripts/Tests/EditMode/BattleSimulatorTests.cs
git commit -m "feat: BattleUnit 에 위치·이동속도·사거리를 더한다"
```

---

### Task 2: 생성자가 좌표를 받는다 — **기존 13개를 살린 채로**

**이 태스크는 동작을 바꾸지 않는다.** 좌표를 받아 저장만 하고, 아직 아무도 읽지 않는다.
목적은 **호출부를 전부 옮기고도 기존 단언 13개가 그대로 초록인 것을 보는 것**이다.

**Files:**
- Modify: `Assets/Scripts/Runtime/Core/BattleSimulator.cs`
- Modify: `Assets/Scripts/Runtime/Presentation/BattleSceneRoot.cs` (`StartBattle`, `PlaceUnits`)
- Modify: `Assets/Scripts/Tests/EditMode/BattleSimulatorTests.cs` (호출 9곳 + 헬퍼)
- Modify: `Assets/Scripts/Tests/EditMode/PanelLabelsTests.cs` (호출 1곳)

**Interfaces:**
- Consumes: Task 1 의 `BattleUnit.Position`
- Produces: `new BattleSimulator(party, foes, allyPositions, enemyPositions, minSpacing = DefaultMinSpacing)`, 상수 `BattleSimulator.DefaultMinSpacing`(0.9f), 테스트 헬퍼 `Clustered(int)` — Task 3~8 이 전부 쓴다

- [ ] **Step 1: 테스트 헬퍼와 새 테스트를 쓴다**

`BattleSimulatorTests.cs` 의 `Party(...)` 헬퍼 **바로 뒤에** 헬퍼를 넣는다.

```csharp
        /// <summary>
        /// 전원을 원점에 겹쳐 세운다. 거리가 0 이라 <b>항상 사거리 안</b>이므로 이동 구간이 없고,
        /// 타겟 동률이 목록 순서로 깨져 예전 <c>FirstAlive</c> 와 같은 유닛을 고른다 (설계 6.1).
        /// 좌표를 보지 않는 기존 테스트는 전부 이것을 쓴다.
        /// </summary>
        private static List<Vector2> Clustered(int count)
        {
            var points = new List<Vector2>();
            for (int i = 0; i < count; i++)
            {
                points.Add(Vector2.zero);
            }

            return points;
        }
```

그리고 파일 끝(마지막 `}` 두 개 앞)에 새 테스트를 더한다.

```csharp
        [Test]
        public void Constructor_PlacesUnitsAtGivenPositions()
        {
            var hero = TestFixtures.MakeFighter("hero", attack: 10, defense: 0, health: 100,
                attackInterval: Never, cooldownSeconds: Never);
            var dummy = TestFixtures.MakeEnemy("dummy", maxHp: 50, attack: 0, defense: 0,
                attackInterval: Never);

            var battle = new BattleSimulator(
                Party(hero), new[] { dummy },
                new List<Vector2> { new Vector2(1f, 2f) },
                new List<Vector2> { new Vector2(-3f, 4f) });

            Assert.AreEqual(new Vector2(1f, 2f), battle.Allies[0].Position);
            Assert.AreEqual(new Vector2(-3f, 4f), battle.Enemies[0].Position);
        }
```

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```powershell
Run-EditMode "FarmCreature.Tests.BattleSimulatorTests" "t2"
```

기대: **0 이 아니다.** 인자 4개짜리 생성자가 없다는 컴파일 오류.

- [ ] **Step 3: 생성자를 고친다**

`BattleSimulator.cs` 상단에 `using UnityEngine;` 를 추가한다. 클래스 상수에 다음을 더한다.

```csharp
        /// <summary>
        /// 유닛끼리 이 거리보다 가까우면 밀어낸다 (설계 4절).
        /// <b><see cref="BattleUnit.DefaultAttackRange"/> 보다 작아야 한다</b> —
        /// 크면 사거리 안에 들기 전에 서로 밀어내 영원히 못 때린다.
        /// </summary>
        public const float DefaultMinSpacing = 0.9f;
```

생성자를 바꾼다. `party`/`foes` 를 도는 루프는 그대로 두고, **루프 뒤에** 좌표를 입힌다.

```csharp
        private readonly float minSpacing;

        public BattleSimulator(
            IList<CreatureInstance> party,
            IList<EnemyData> foes,
            IList<Vector2> allyPositions,
            IList<Vector2> enemyPositions,
            float minSpacing = DefaultMinSpacing)
        {
            this.minSpacing = minSpacing;

            // ... 기존 party/foes 루프 그대로 ...

            Place(allies, allyPositions);
            Place(enemies, enemyPositions);

            // ... 기존 Resolve()/Append 부분 그대로 ...
        }

        /// <summary>
        /// 좌표가 유닛 수보다 적으면 남는 유닛은 원점에 선다. 짧은 목록은 호출부의 실수지만
        /// 예외로 죽이지 않는다 — 전투가 안 열리는 것보다 겹쳐 서는 편이 낫다 (설계 3.1).
        /// </summary>
        private static void Place(List<BattleUnit> units, IList<Vector2> positions)
        {
            for (int i = 0; i < units.Count; i++)
            {
                units[i].Position = positions != null && i < positions.Count
                    ? positions[i]
                    : Vector2.zero;
            }
        }
```

**`Place` 는 `Resolve()` 호출보다 앞에 있어야 한다** — 순서가 뒤바뀌어도 지금은 결과가 같지만, Task 3 부터 `Resolve` 앞에 좌표가 서 있어야 한다.

- [ ] **Step 4: `BattleSimulatorTests` 의 호출 9곳을 고친다**

아래 9줄을 전부 고친다. **유닛 수에 맞는 `Clustered(n)` 을 넘긴다.**

```csharp
// 59행:  new BattleSimulator(Party(hero), new[] { dummy })
        new BattleSimulator(Party(hero), new[] { dummy }, Clustered(1), Clustered(1))

// 76행:  new BattleSimulator(Party(weakling), new[] { brute })
        new BattleSimulator(Party(weakling), new[] { brute }, Clustered(1), Clustered(1))

// 88행:  new BattleSimulator(new List<CreatureInstance>(), new[] { dummy })
        new BattleSimulator(new List<CreatureInstance>(), new[] { dummy }, Clustered(0), Clustered(1))

// 102행: new BattleSimulator(Party(hero), new[] { dummy })
        new BattleSimulator(Party(hero), new[] { dummy }, Clustered(1), Clustered(1))

// 126행: new BattleSimulator(Party(hero), new[] { wall })
        new BattleSimulator(Party(hero), new[] { wall }, Clustered(1), Clustered(1))

// 145행: new BattleSimulator(Party(hero), new[] { wall })
        new BattleSimulator(Party(hero), new[] { wall }, Clustered(1), Clustered(1))

// 164행: new BattleSimulator(Party(healer), new[] { biter })
        new BattleSimulator(Party(healer), new[] { biter }, Clustered(1), Clustered(1))

// 217행: new BattleSimulator(Party(hero), new[] { wall })
        new BattleSimulator(Party(hero), new[] { wall }, Clustered(1), Clustered(1))

// 244행: new BattleSimulator(Party(species), new[] { enemy })
        new BattleSimulator(Party(species), new[] { enemy }, Clustered(1), Clustered(1))
```

> 줄 번호는 **헬퍼를 넣기 전 기준**이다. 헬퍼가 앞쪽에 들어가면 뒤가 밀린다.
> `Party(` 로 시작하는 인자 개수를 세어 맞춘다 — `Party(a, b)` 면 `Clustered(2)` 다.

`PanelLabelsTests.cs` 169행도 같은 방식으로 고친다.

```csharp
// 169행: new BattleSimulator(party, new[] { dummy })
        new BattleSimulator(party, new[] { dummy }, Clustered(party.Count), Clustered(1))
```

`PanelLabelsTests` 에는 `Clustered` 가 없다. **거기서는 인라인으로 쓴다.**

```csharp
        var zeros = new List<Vector2>();
        for (int i = 0; i < party.Count; i++) { zeros.Add(Vector2.zero); }

        var battle = new BattleSimulator(
            party, new[] { dummy }, zeros, new List<Vector2> { Vector2.zero });
```

`PanelLabelsTests.cs` 상단에 `using UnityEngine;` 가 없으면 추가한다.

- [ ] **Step 5: `BattleSceneRoot` 를 고친다 — 좌표를 먼저 고르고 넘긴다**

`StartBattle` 을 통째로 바꾼다.

```csharp
        private void StartBattle(BattleEncounter encounter)
        {
            var party = LivePartyOf(state);
            var foes = FoesOf(encounter);

            var map = encounter != null && encounter.map != null ? encounter.map : defaultMap;

            // 좌표를 시뮬레이터보다 먼저 고른다 — 이제 배치가 규칙이다 (설계 1.1).
            List<Vector2> allyPoints;
            List<Vector2> enemyPoints;
            PickSpawns(map, party.Count, foes.Count, out allyPoints, out enemyPoints);

            battle = new BattleSimulator(
                party, foes, allyPoints, enemyPoints,
                map != null ? map.minSpacing : BattleSimulator.DefaultMinSpacing);

            ApplyMap(map);
            PlaceUnits(party, foes, allyPoints, enemyPoints);

            RefreshTexts();
        }

        /// <summary>
        /// 스폰 좌표를 고른다. <b>여기서만 난수를 쓴다</b> — 시뮬레이터는 받은 좌표를 그대로 쓴다.
        /// 매 전투 배치가 달라야 하고, 이제 그것이 결과까지 바꾼다 (설계 3.1).
        /// </summary>
        private static void PickSpawns(
            BattleMapData map, int allyCount, int foeCount,
            out List<Vector2> allyPoints, out List<Vector2> enemyPoints)
        {
            if (map == null)
            {
                Debug.LogError("[FarmCreature] 맵이 없어 유닛을 세울 수 없다. defaultMap 배선을 확인할 것.");
                allyPoints = new List<Vector2>();
                enemyPoints = new List<Vector2>();
                return;
            }

            int seed = Random.Range(int.MinValue, int.MaxValue);

            allyPoints = SpawnPointPicker.PickSpawnPoints(
                map.AllySpawnArea, allyCount, map.minSpacing, seed);
            enemyPoints = SpawnPointPicker.PickSpawnPoints(
                map.EnemySpawnArea, foeCount, map.minSpacing, seed + 1);
        }
```

`PlaceUnits` 는 좌표를 **받는다.** 시그니처와 앞부분만 바꾸고 나머지 루프 본문은 그대로 둔다.

```csharp
        /// <summary>슬롯을 켜고 유닛을 담는다. 좌표는 <see cref="StartBattle"/> 이 이미 골랐다.</summary>
        private void PlaceUnits(
            List<CreatureInstance> party, List<EnemyData> foes,
            List<Vector2> allyPoints, List<Vector2> enemyPoints)
        {
            shownAllies.Clear();
            shownEnemies.Clear();

            for (int i = 0; i < party.Count && i < allySlots.Count && i < allyPoints.Count; i++)
            {
                // ... 기존 본문 그대로 ...
            }

            for (int i = 0; i < foes.Count && i < enemySlots.Count && i < enemyPoints.Count; i++)
            {
                // ... 기존 본문 그대로 ...
            }
        }
```

**루프 조건에 `< allyPoints.Count` / `< enemyPoints.Count` 를 반드시 더한다** — 맵이 없어 빈 목록이 오면 인덱스 예외가 난다. 예전에는 `map == null` 일 때 일찍 반환해서 막았는데, 그 반환이 `PickSpawns` 로 옮겨갔다.

- [ ] **Step 6: 전량 회귀 — 13개가 살아 있는지 본다**

```powershell
Run-EditMode $null "t2-all"
```

기대: 종료 코드 **0**, `total="190" passed="190" failed="0"` (187 + Task1 2 + Task2 1).

**13개 중 하나라도 깨지면 여기서 멈춘다.** 설계 6.1 의 전제(원점에 겹치면 예전과 같은 타겟을 고른다)가 틀렸다는 뜻이고, 그 전제가 이 작업 전체를 떠받치고 있다.

- [ ] **Step 7: 커밋**

```powershell
git add Assets/Scripts/Runtime/Core/BattleSimulator.cs `
        Assets/Scripts/Runtime/Presentation/BattleSceneRoot.cs `
        Assets/Scripts/Tests/EditMode/BattleSimulatorTests.cs `
        Assets/Scripts/Tests/EditMode/PanelLabelsTests.cs
git commit -m "refactor: 시뮬레이터가 스폰 좌표를 받는다 — 동작은 아직 그대로"
```

---

### Task 3: 가장 가까운 적을 노리고, 사거리 밖이면 때리지 않는다

**Files:**
- Modify: `Assets/Scripts/Runtime/Core/BattleSimulator.cs`
- Test: `Assets/Scripts/Tests/EditMode/BattleSimulatorTests.cs`

**Interfaces:**
- Consumes: Task 2 의 생성자와 `Clustered`
- Produces: 없음 (내부 규칙). Task 4~6 이 이 타겟 선택을 이어 쓴다

- [ ] **Step 1: 실패하는 테스트를 쓴다**

파일 끝에 붙인다.

```csharp
        // ---------- 타겟과 사거리 ----------

        [Test]
        public void OutOfRange_NoOneTakesDamage()
        {
            var hero = TestFixtures.MakeFighter("hero", attack: 10, defense: 0, health: 100,
                attackInterval: 0.5f, cooldownSeconds: Never);
            var dummy = TestFixtures.MakeEnemy("dummy", maxHp: 50, attack: 5, defense: 0,
                attackInterval: 0.5f);

            var battle = new BattleSimulator(
                Party(hero), new[] { dummy },
                new List<Vector2> { Vector2.zero },
                new List<Vector2> { new Vector2(50f, 0f) });

            battle.Tick(1f);

            Assert.AreEqual(50, battle.Enemies[0].Hp, "사거리 밖인데 맞았다");
            Assert.AreEqual(battle.Allies[0].MaxHp, battle.Allies[0].Hp, "사거리 밖인데 때렸다");
        }

        [Test]
        public void Targets_TheNearestEnemy()
        {
            var hero = TestFixtures.MakeFighter("hero", attack: 10, defense: 0, health: 100,
                attackInterval: 0.5f, cooldownSeconds: Never);
            var far = TestFixtures.MakeEnemy("far", maxHp: 50, attack: 0, defense: 0,
                attackInterval: Never);
            var near = TestFixtures.MakeEnemy("near", maxHp: 50, attack: 0, defense: 0,
                attackInterval: Never);

            var battle = new BattleSimulator(
                Party(hero), new[] { far, near },      // 목록 순서는 먼 쪽이 앞
                new List<Vector2> { Vector2.zero },
                new List<Vector2> { new Vector2(0.9f, 0f), new Vector2(0.3f, 0f) });

            battle.Tick(1f);

            Assert.AreEqual(50, battle.Enemies[0].Hp, "먼 쪽이 맞았다 — 목록 순서를 따라간 것이다");
            Assert.Less(battle.Enemies[1].Hp, 50, "가까운 쪽이 맞아야 한다");
        }

        [Test]
        public void EqualDistance_BreaksTieByListOrder()
        {
            var hero = TestFixtures.MakeFighter("hero", attack: 10, defense: 0, health: 100,
                attackInterval: 0.5f, cooldownSeconds: Never);
            var first = TestFixtures.MakeEnemy("first", maxHp: 50, attack: 0, defense: 0,
                attackInterval: Never);
            var second = TestFixtures.MakeEnemy("second", maxHp: 50, attack: 0, defense: 0,
                attackInterval: Never);

            var battle = new BattleSimulator(
                Party(hero), new[] { first, second },
                Clustered(1), Clustered(2));       // 셋 다 원점 — 거리가 전부 0

            battle.Tick(1f);

            Assert.Less(battle.Enemies[0].Hp, 50, "동률은 목록에서 앞선 쪽이 맞아야 한다");
            Assert.AreEqual(50, battle.Enemies[1].Hp);
        }
```

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```powershell
Run-EditMode "FarmCreature.Tests.BattleSimulatorTests" "t3"
```

기대: `OutOfRange_NoOneTakesDamage` 와 `Targets_TheNearestEnemy` 가 **실패**한다 (아직 좌표를 안 읽으므로 멀어도 때린다). `EqualDistance_BreaksTieByListOrder` 는 지금도 통과한다 — `FirstAlive` 가 이미 목록 순서다.

- [ ] **Step 3: 구현한다**

`BattleSimulator.cs` 에 헬퍼를 더한다.

```csharp
        /// <summary>
        /// <paramref name="from"/> 에서 가장 가까운 생존자. <b>거리가 같으면 목록에서 앞선 쪽</b>이다 —
        /// 동률에 순서를 못 박아야 결과가 재현된다 (설계 3.3).
        /// </summary>
        private static BattleUnit NearestAlive(List<BattleUnit> units, Vector2 from)
        {
            BattleUnit best = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < units.Count; i++)
            {
                if (!units[i].IsAlive)
                {
                    continue;
                }

                float sqr = (units[i].Position - from).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = units[i];
                }
            }

            return best;
        }

        private static bool InRange(BattleUnit unit, BattleUnit target)
        {
            return (target.Position - unit.Position).sqrMagnitude
                   <= unit.AttackRange * unit.AttackRange;
        }
```

`Advance` 를 바꾼다. 유닛마다 타겟을 먼저 정하고, 사거리 안일 때만 타이머를 소비한다.

```csharp
        private void Advance(List<BattleUnit> side, List<BattleUnit> opponents, float deltaTime)
        {
            for (int i = 0; i < side.Count; i++)
            {
                var unit = side[i];
                if (!unit.IsAlive)
                {
                    continue;
                }

                var target = NearestAlive(opponents, unit.Position);
                if (target == null)
                {
                    continue;
                }

                // 타이머는 사거리 밖에서도 찬다 — 붙는 순간 바로 나가야 공백이 안 느껴진다 (설계 3.2).
                if (unit.Skill != null && unit.Skill.cooldownSeconds > 0f)
                {
                    unit.SkillTimer += deltaTime;
                }

                unit.AttackTimer += deltaTime;

                if (!InRange(unit, target))
                {
                    continue;   // 이동은 Task 4 에서. 지금은 다가가지 못하고 멈춰 있다
                }

                if (unit.Skill != null && unit.Skill.cooldownSeconds > 0f)
                {
                    int used = 0;
                    while (unit.SkillTimer >= unit.Skill.cooldownSeconds
                           && used < MaxActionsPerTickPerUnit
                           && unit.IsAlive
                           && AnyAlive(opponents))
                    {
                        unit.SkillTimer -= unit.Skill.cooldownSeconds;
                        UseSkill(unit, side, opponents);
                        used++;
                    }
                }

                int swings = 0;
                while (unit.AttackTimer >= unit.AttackInterval
                       && swings < MaxActionsPerTickPerUnit
                       && unit.IsAlive
                       && AnyAlive(opponents))
                {
                    unit.AttackTimer -= unit.AttackInterval;
                    BasicAttack(unit, target, opponents);
                    swings++;
                }
            }
        }
```

`BasicAttack` 이 타겟을 인자로 받도록 바꾼다. **목록 첫 번째가 아니라 정해진 타겟을 친다.**

```csharp
        private void BasicAttack(BattleUnit attacker, BattleUnit target, List<BattleUnit> opponents)
        {
            // 앞선 타격으로 타겟이 죽었으면 다시 고른다.
            if (target == null || !target.IsAlive)
            {
                target = NearestAlive(opponents, attacker.Position);
            }

            if (target == null)
            {
                return;
            }

            int damage = DamageAgainst(attacker, target);
            target.TakeDamage(damage);

            Append(attacker.Name + " → " + target.Name + " 평타 " + damage);
            NoteIfDown(target);
        }
```

`UseSkill` 의 `var target = FirstAlive(opponents);` 도 바꾼다.

```csharp
            var target = NearestAlive(opponents, user.Position);
```

`FirstAlive` 는 이제 아무도 안 쓴다. **지운다** — 남겨두면 다음 사람이 어느 쪽이 규칙인지 헷갈린다.

- [ ] **Step 4: 테스트가 통과하는지 확인한다**

```powershell
Run-EditMode $null "t3-all"
```

기대: 종료 코드 **0**, `total="193" passed="193" failed="0"`.

기존 13개가 여전히 초록이어야 한다 — 전부 `Clustered` 라 거리 0 이고, 동률은 목록 순서로 깨져 예전과 같은 타겟이다.

- [ ] **Step 5: 커밋**

```powershell
git add Assets/Scripts/Runtime/Core/BattleSimulator.cs Assets/Scripts/Tests/EditMode/BattleSimulatorTests.cs
git commit -m "feat: 가장 가까운 적을 노리고 사거리 밖에서는 때리지 않는다"
```

---

### Task 4: 이동 — 사거리 밖이면 다가간다

**Files:**
- Modify: `Assets/Scripts/Runtime/Core/BattleSimulator.cs` (`Advance` 의 `continue` 자리)
- Test: `Assets/Scripts/Tests/EditMode/BattleSimulatorTests.cs`

**Interfaces:**
- Consumes: Task 3 의 `NearestAlive` / `InRange`
- Produces: 없음

- [ ] **Step 1: 실패하는 테스트를 쓴다**

```csharp
        // ---------- 이동 ----------

        [Test]
        public void WalksIntoRange_ThenFights()
        {
            var hero = TestFixtures.MakeFighter("hero", attack: 10, defense: 0, health: 100,
                attackInterval: 0.5f, cooldownSeconds: Never);
            var dummy = TestFixtures.MakeEnemy("dummy", maxHp: 500, attack: 0, defense: 0,
                attackInterval: Never);

            var battle = new BattleSimulator(
                Party(hero), new[] { dummy },
                new List<Vector2> { Vector2.zero },
                new List<Vector2> { new Vector2(6f, 0f) });

            // 6 유닛 거리, 속도 2/초 → 사거리(1.2)까지 약 2.4초. 넉넉히 굴린다.
            for (int i = 0; i < 40; i++)
            {
                battle.Tick(0.1f);
            }

            float distance = (battle.Enemies[0].Position - battle.Allies[0].Position).magnitude;
            Assert.LessOrEqual(distance, battle.Allies[0].AttackRange + 0.01f, "붙지 못했다");
            Assert.Less(battle.Enemies[0].Hp, 500, "붙었는데 때리지 않았다");
        }

        [Test]
        public void Retargets_WhenACloserEnemyArrives()
        {
            var hero = TestFixtures.MakeFighter("hero", attack: 10, defense: 0, health: 100,
                attackInterval: 0.5f, cooldownSeconds: Never);
            var far = TestFixtures.MakeEnemy("far", maxHp: 500, attack: 0, defense: 0,
                attackInterval: Never);
            var rusher = TestFixtures.MakeEnemy("rusher", maxHp: 500, attack: 0, defense: 0,
                attackInterval: Never);

            var battle = new BattleSimulator(
                Party(hero), new[] { far, rusher },
                new List<Vector2> { Vector2.zero },
                new List<Vector2> { new Vector2(3f, 0f), new Vector2(-8f, 0f) });

            // 처음엔 far(3) 가 가깝다. rusher 는 −8 에서 다가온다.
            // 아군도 far 쪽으로 가므로 둘이 중간에서 만난다. 충분히 굴리면 rusher 가 더 가까워지는 시점이 온다.
            for (int i = 0; i < 100; i++)
            {
                battle.Tick(0.1f);
            }

            Assert.Less(battle.Enemies[1].Hp, 500, "달려든 적이 한 대도 안 맞았다 — 타겟을 다시 안 골랐다");
        }
```

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```powershell
Run-EditMode "FarmCreature.Tests.BattleSimulatorTests" "t4"
```

기대: 두 테스트 모두 **실패**. 아직 이동이 없어 영원히 사거리 밖이다.

- [ ] **Step 3: 구현한다**

Task 3 에서 `continue` 만 있던 자리를 채운다.

```csharp
                if (!InRange(unit, target))
                {
                    MoveToward(unit, target, deltaTime);
                    continue;
                }
```

메서드를 더한다.

```csharp
        /// <summary>
        /// 타겟 쪽으로 <c>MoveSpeed × dt</c> 만큼 간다. <b>이동 중에는 때리지 않는다</b> (설계 1.3).
        /// 겹침 밀어내기는 Task 5 에서 여기에 더해진다.
        /// </summary>
        private void MoveToward(BattleUnit unit, BattleUnit target, float deltaTime)
        {
            Vector2 toTarget = target.Position - unit.Position;

            // 정확히 같은 좌표면 방향이 없다. 지어내면 그 선택이 숨은 난수가 된다 (설계 4절).
            if (toTarget.sqrMagnitude <= 0f)
            {
                return;
            }

            unit.Position += toTarget.normalized * unit.MoveSpeed * deltaTime;
        }
```

- [ ] **Step 4: 테스트가 통과하는지 확인한다**

```powershell
Run-EditMode $null "t4-all"
```

기대: 종료 코드 **0**, `total="195" passed="195" failed="0"`.

- [ ] **Step 5: 커밋**

```powershell
git add Assets/Scripts/Runtime/Core/BattleSimulator.cs Assets/Scripts/Tests/EditMode/BattleSimulatorTests.cs
git commit -m "feat: 사거리 밖이면 타겟 쪽으로 다가간다"
```

---

### Task 5: 겹침 — 이동 중에만 밀어낸다

**Files:**
- Modify: `Assets/Scripts/Runtime/Core/BattleSimulator.cs` (`MoveToward`)
- Test: `Assets/Scripts/Tests/EditMode/BattleSimulatorTests.cs`

**Interfaces:**
- Consumes: Task 2 의 `minSpacing` 필드, Task 4 의 `MoveToward`
- Produces: 없음

- [ ] **Step 1: 실패하는 테스트를 쓴다**

```csharp
        // ---------- 겹침 ----------

        [Test]
        public void SplittingTicks_GivesTheSamePositions()
        {
            var hero = TestFixtures.MakeFighter("hero", attack: 10, defense: 0, health: 100,
                attackInterval: Never, cooldownSeconds: Never);
            var dummy = TestFixtures.MakeEnemy("dummy", maxHp: 500, attack: 0, defense: 0,
                attackInterval: Never);

            var coarse = new BattleSimulator(
                Party(hero, hero), new[] { dummy },
                new List<Vector2> { new Vector2(0f, 0f), new Vector2(0.2f, 0.1f) },
                new List<Vector2> { new Vector2(9f, 0f) });

            var fine = new BattleSimulator(
                Party(hero, hero), new[] { dummy },
                new List<Vector2> { new Vector2(0f, 0f), new Vector2(0.2f, 0.1f) },
                new List<Vector2> { new Vector2(9f, 0f) });

            coarse.Tick(1f);
            for (int i = 0; i < 10; i++)
            {
                fine.Tick(0.1f);
            }

            for (int i = 0; i < 2; i++)
            {
                float gap = (coarse.Allies[i].Position - fine.Allies[i].Position).magnitude;
                Assert.Less(gap, 0.05f,
                    "틱을 쪼갰더니 위치가 달라졌다 — 밀어내기를 dt 로 안 곱한 것이다");
            }
        }

        [Test]
        public void ExactlyOverlappingUnits_AreNotPushed()
        {
            var hero = TestFixtures.MakeFighter("hero", attack: 10, defense: 0, health: 100,
                attackInterval: Never, cooldownSeconds: Never);
            var dummy = TestFixtures.MakeEnemy("dummy", maxHp: 500, attack: 0, defense: 0,
                attackInterval: Never);

            var battle = new BattleSimulator(
                Party(hero, hero), new[] { dummy },
                Clustered(2),                                   // 아군 둘이 정확히 같은 좌표
                new List<Vector2> { new Vector2(9f, 0f) });

            battle.Tick(0.5f);

            Assert.AreEqual(battle.Allies[0].Position, battle.Allies[1].Position,
                "겹친 좌표는 방향이 없다 — 지어내면 숨은 난수가 된다");
        }
```

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```powershell
Run-EditMode "FarmCreature.Tests.BattleSimulatorTests" "t5"
```

기대: `SplittingTicks_GivesTheSamePositions` 는 **지금도 통과한다** (아직 밀어내기가 없어 이동이 선형이다). `ExactlyOverlappingUnits_AreNotPushed` 도 통과한다.

**둘 다 통과하는 것이 정상이다.** 이 테스트들은 새 동작을 요구하는 것이 아니라, Step 3 에서 밀어내기를 넣을 때 **깨지지 않게 잡아두는 그물**이다. Step 3 을 잘못 쓰면 여기서 빨개진다.

- [ ] **Step 3: 밀어내기를 더한다**

`MoveToward` 를 바꾼다. 시그니처에 같은 편·상대편 목록을 더한다.

```csharp
        private void MoveToward(
            BattleUnit unit, BattleUnit target,
            List<BattleUnit> side, List<BattleUnit> opponents, float deltaTime)
        {
            Vector2 toTarget = target.Position - unit.Position;
            if (toTarget.sqrMagnitude <= 0f)
            {
                return;
            }

            Vector2 velocity = toTarget.normalized * unit.MoveSpeed + Separation(unit, side, opponents);

            // 밀어내기도 dt 로 스케일한다 — 안 하면 틱 길이에 따라 결과가 갈린다 (설계 4절).
            unit.Position += velocity * deltaTime;
        }

        /// <summary>
        /// <see cref="minSpacing"/> 안에 들어온 다른 살아있는 유닛들에게서 밀려나는 성분.
        /// 아군·적을 가리지 않는다 — 겹쳐 보이는 것은 편을 안 가린다.
        /// </summary>
        private Vector2 Separation(BattleUnit unit, List<BattleUnit> side, List<BattleUnit> opponents)
        {
            if (minSpacing <= 0f)
            {
                return Vector2.zero;
            }

            Vector2 push = Vector2.zero;
            push += SeparationFrom(unit, side);
            push += SeparationFrom(unit, opponents);
            return push;
        }

        private Vector2 SeparationFrom(BattleUnit unit, List<BattleUnit> others)
        {
            Vector2 push = Vector2.zero;

            for (int i = 0; i < others.Count; i++)
            {
                var other = others[i];
                if (other == unit || !other.IsAlive)
                {
                    continue;
                }

                Vector2 away = unit.Position - other.Position;
                float distance = away.magnitude;

                // 정확히 겹치면 방향이 없다. 밀지 않는다 (설계 4절).
                if (distance <= 0f || distance >= minSpacing)
                {
                    continue;
                }

                push += away.normalized * (minSpacing - distance);
            }

            return push;
        }
```

Task 4 에서 만든 호출부도 인자를 맞춘다.

```csharp
                if (!InRange(unit, target))
                {
                    MoveToward(unit, target, side, opponents, deltaTime);
                    continue;
                }
```

- [ ] **Step 4: 테스트가 통과하는지 확인한다**

```powershell
Run-EditMode $null "t5-all"
```

기대: 종료 코드 **0**, `total="197" passed="197" failed="0"`.

`SplittingTicks_GivesTheSamePositions` 가 빨개지면 **밀어내기를 `dt` 로 안 곱한 것이다.**

- [ ] **Step 5: 커밋**

```powershell
git add Assets/Scripts/Runtime/Core/BattleSimulator.cs Assets/Scripts/Tests/EditMode/BattleSimulatorTests.cs
git commit -m "feat: 이동 중 겹치면 밀어낸다 — dt 로 스케일해 결정성을 지킨다"
```

---

### Task 6: 스킬 사거리 — 공격은 지키고 회복은 무시한다

**Files:**
- Modify: `Assets/Scripts/Runtime/Core/BattleSimulator.cs` (`Advance`, `UseSkill`)
- Test: `Assets/Scripts/Tests/EditMode/BattleSimulatorTests.cs`

**Interfaces:**
- Consumes: Task 3 의 `InRange`
- Produces: 없음

- [ ] **Step 1: 실패하는 테스트를 쓴다**

```csharp
        // ---------- 스킬 사거리 ----------

        [Test]
        public void HealSkill_IgnoresDistance()
        {
            var healer = TestFixtures.MakeFighter("healer", attack: 10, defense: 0, health: 100,
                attackInterval: Never, effect: SkillEffect.Heal,
                cooldownSeconds: 1f, multiplier: 1.5f);
            var wounded = TestFixtures.MakeFighter("wounded", attack: 1, defense: 0, health: 100,
                attackInterval: Never, cooldownSeconds: Never);
            var biter = TestFixtures.MakeEnemy("biter", maxHp: 500, attack: 20, defense: 0,
                attackInterval: 0.5f);

            var battle = new BattleSimulator(
                Party(healer, wounded), new[] { biter },
                new List<Vector2> { new Vector2(-40f, 0f), Vector2.zero },   // 회복사는 멀리
                new List<Vector2> { Vector2.zero });

            // 원점의 부상자가 맞아 체력이 깎이고, 멀리 있는 회복사가 회복시킨다.
            for (int i = 0; i < 20; i++)
            {
                battle.Tick(0.1f);
            }

            StringAssert.Contains("회복", string.Join("\n", battle.Log),
                "멀리 있는 회복사가 한 번도 회복시키지 않았다");
        }

        [Test]
        public void DamageSkill_RespectsRange()
        {
            var caster = TestFixtures.MakeFighter("caster", attack: 10, defense: 0, health: 100,
                attackInterval: Never, effect: SkillEffect.Damage,
                cooldownSeconds: 0.5f, multiplier: 2f);
            var dummy = TestFixtures.MakeEnemy("dummy", maxHp: 500, attack: 0, defense: 0,
                attackInterval: Never);

            var battle = new BattleSimulator(
                Party(caster), new[] { dummy },
                new List<Vector2> { Vector2.zero },
                new List<Vector2> { new Vector2(500f, 0f) });   // 걸어도 못 닿는 거리

            battle.Tick(2f);   // 쿨다운(0.5)은 네 번 찼다

            Assert.AreEqual(500, battle.Enemies[0].Hp, "사거리 밖인데 스킬이 나갔다");
        }
```

> **새 픽스처는 필요 없다.** `MakeFighter` 가 이미 `effect`·`cooldownSeconds`·`multiplier` 를 받는다
> (Task 1 의 시그니처 표). 기존 회복 테스트(`HealSkill_RestoresLowestAlly_AndNeverExceedsMax`)가
> 같은 방식을 쓰고 있으니 그것을 따른다.
>
> `caster` 의 `attackInterval` 을 `Never` 로 둔 것은 **평타를 지워 스킬만 남기기 위해서다.**
> 어차피 사거리 밖이라 둘 다 안 나가지만, 평타가 섞이면 무엇이 막혔는지 단언이 흐려진다.

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```powershell
Run-EditMode "FarmCreature.Tests.BattleSimulatorTests" "t6"
```

기대: `HealSkill_IgnoresDistance` **실패** (Task 3 이후 사거리 밖에서는 스킬 자체가 안 나간다).
`DamageSkill_RespectsRange` 는 통과한다 — 같은 이유로 이미 막혀 있다.

- [ ] **Step 3: 구현한다**

`Advance` 에서 스킬을 **사거리 검사보다 앞으로** 뺀다. 회복은 거리를 안 보기 때문이다.

Task 3 에서 만든 `Advance` 의 타이머 구간을 이렇게 바꾼다.

```csharp
                // 타이머는 사거리 밖에서도 찬다 (설계 3.2).
                if (unit.Skill != null && unit.Skill.cooldownSeconds > 0f)
                {
                    unit.SkillTimer += deltaTime;

                    // 회복은 거리를 무시한다. 공격 스킬만 사거리를 지킨다 (설계 3.4).
                    bool skillReaches = unit.Skill.effect == SkillEffect.Heal || InRange(unit, target);

                    int used = 0;
                    while (skillReaches
                           && unit.SkillTimer >= unit.Skill.cooldownSeconds
                           && used < MaxActionsPerTickPerUnit
                           && unit.IsAlive
                           && AnyAlive(opponents))
                    {
                        unit.SkillTimer -= unit.Skill.cooldownSeconds;
                        UseSkill(unit, side, opponents);
                        used++;
                    }
                }

                unit.AttackTimer += deltaTime;

                if (!InRange(unit, target))
                {
                    MoveToward(unit, target, side, opponents, deltaTime);
                    continue;
                }
```

그리고 사거리 안쪽 블록에서는 **스킬 처리를 지운다** — 위로 옮겼으므로 두 번 돌면 안 된다.
평타 `while` 만 남긴다.

`UseSkill` 의 회복 갈래는 이미 거리를 안 본다(`LowestHealth(side)`). 그대로 둔다.

- [ ] **Step 4: 테스트가 통과하는지 확인한다**

```powershell
Run-EditMode $null "t6-all"
```

기대: 종료 코드 **0**, `total="199" passed="199" failed="0"`.

기존 회복 테스트(164행 근처, `Party(healer)`)도 초록이어야 한다.

- [ ] **Step 5: 커밋**

```powershell
git add Assets/Scripts/Runtime/Core/BattleSimulator.cs Assets/Scripts/Tests/EditMode/BattleSimulatorTests.cs
git commit -m "feat: 공격 스킬은 사거리를 지키고 회복은 거리를 무시한다"
```

---

### Task 7: 난전 스폰 + 뷰가 위치를 읽는다

**Files:**
- Modify: `Assets/Scripts/Runtime/Presentation/BattleSceneRoot.cs` (`PickSpawns`)
- Modify: `Assets/Scripts/Runtime/Presentation/BattleUnitView.cs` (`Refresh`)

**Interfaces:**
- Consumes: Task 1 의 `BattleUnit.Position`, Task 2 의 `PickSpawns`
- Produces: 없음 (표현 계층)

이 태스크에는 EditMode 테스트가 없다. `BattleSceneRoot`·`BattleUnitView` 는 MonoBehaviour 다.
**검증은 PlayMode 스모크와 씬 무결성 검사가 한다 (Task 8).**

- [ ] **Step 1: 스폰을 한 영역으로 합친다**

`PickSpawns` 를 바꾼다. `BattleMapData` 는 **건드리지 않는다** — seed-only 라 지워야 반영되고,
맵 데이터는 다음 프로젝트에서 어차피 다시 짠다 (설계 5절).

```csharp
        private static void PickSpawns(
            BattleMapData map, int allyCount, int foeCount,
            out List<Vector2> allyPoints, out List<Vector2> enemyPoints)
        {
            allyPoints = new List<Vector2>();
            enemyPoints = new List<Vector2>();

            if (map == null)
            {
                Debug.LogError("[FarmCreature] 맵이 없어 유닛을 세울 수 없다. defaultMap 배선을 확인할 것.");
                return;
            }

            // 두 구역을 감싸는 사각형이 전장이다. 양측을 여기에 섞어 뿌린다 (설계 5절).
            var battlefield = Rect.MinMaxRect(
                Mathf.Min(map.AllySpawnArea.xMin, map.EnemySpawnArea.xMin),
                Mathf.Min(map.AllySpawnArea.yMin, map.EnemySpawnArea.yMin),
                Mathf.Max(map.AllySpawnArea.xMax, map.EnemySpawnArea.xMax),
                Mathf.Max(map.AllySpawnArea.yMax, map.EnemySpawnArea.yMax));

            int seed = Random.Range(int.MinValue, int.MaxValue);

            // 한 번에 함께 뽑는다. 나눠 뽑으면 아군과 적 사이의 간격이 보장되지 않는다 (설계 5절).
            var all = SpawnPointPicker.PickSpawnPoints(
                battlefield, allyCount + foeCount, map.minSpacing, seed);

            for (int i = 0; i < all.Count; i++)
            {
                if (i < allyCount)
                {
                    allyPoints.Add(all[i]);
                }
                else
                {
                    enemyPoints.Add(all[i]);
                }
            }
        }
```

- [ ] **Step 2: 뷰가 매 프레임 위치를 읽게 한다**

`BattleUnitView.Refresh()` 의 맨 앞(`unit == null` 검사 **뒤**)에 넣는다.

```csharp
            // 유닛이 움직인다. 위치는 시뮬레이터가 정하고 여기서는 따라 그리기만 한다 (설계 1.1).
            transform.position = BattleField.ToWorld(unit.Position);

            if (body != null)
            {
                body.sortingOrder = BattleField.SortingOrderFor(unit.Position.y);
                ApplySortingOffset(healthBarBack, 1);
                ApplySortingOffset(healthBarFill, 2);

                if (nameLabel != null)
                {
                    var labelRenderer = nameLabel.GetComponent<MeshRenderer>();
                    if (labelRenderer != null)
                    {
                        labelRenderer.sortingOrder = body.sortingOrder + 3;
                    }
                }
            }
```

**정렬 순서도 매 프레임 다시 계산해야 한다** — 유닛이 위아래로 움직이면 앞뒤 관계가 바뀐다.
안 하면 아래에 있는 유닛이 위 유닛에 가린다.

`Show` 는 그대로 둔다 — `position` 인자로 첫 프레임을 세우고 끝에서 `Refresh()` 를 부르므로 이어진다.

- [ ] **Step 3: 컴파일과 회귀를 확인한다**

```powershell
Run-EditMode $null "t7-all"
```

기대: 종료 코드 **0**, `total="199" passed="199" failed="0"` (새 테스트 없음).

- [ ] **Step 4: 커밋**

```powershell
git add Assets/Scripts/Runtime/Presentation/BattleSceneRoot.cs `
        Assets/Scripts/Runtime/Presentation/BattleUnitView.cs
git commit -m "feat: 난전 스폰과 매 프레임 위치 반영"
```

---

### Task 8: 핵심 단언 · 전량 회귀 · 문서

**Files:**
- Test: `Assets/Scripts/Tests/EditMode/BattleSimulatorTests.cs`
- Modify: `docs/HANDOFF.md`
- Modify: `docs/PRD.md`

**Interfaces:**
- Consumes: 앞선 태스크 전부
- Produces: 없음

- [ ] **Step 1: 이 작업의 핵심 단언 3개를 쓴다**

**앞의 것이 없으면 결정성이 깨진 것을 못 잡고, 뒤의 것이 없으면 좌표가 정말 결과를 정하는지 아무도 확인하지 않은 채로 끝난다.**

```csharp
        // ---------- 결정성과 배치의 영향 ----------

        [Test]
        public void MinSpacing_IsTighterThanAttackRange()
        {
            Assert.Less(BattleSimulator.DefaultMinSpacing, BattleUnit.DefaultAttackRange,
                "분리 간격이 사거리보다 넓으면 유닛이 붙기 전에 밀려나 영원히 못 때린다");
        }

        [Test]
        public void SamePositions_GiveTheSameLog()
        {
            var hero = TestFixtures.MakeFighter("hero", attack: 6, defense: 1, health: 40,
                attackInterval: 1f, cooldownSeconds: 3f);
            var foe = TestFixtures.MakeEnemy("foe", maxHp: 30, attack: 4, defense: 2,
                attackInterval: 1.1f, goldReward: 5);

            var allyAt = new List<Vector2> { new Vector2(-2f, 1f), new Vector2(-1f, -2f) };
            var foeAt = new List<Vector2> { new Vector2(3f, 0f), new Vector2(4f, 2f) };

            var first = new BattleSimulator(Party(hero, hero), new[] { foe, foe }, allyAt, foeAt);
            var second = new BattleSimulator(Party(hero, hero), new[] { foe, foe }, allyAt, foeAt);

            for (int i = 0; i < 200; i++)
            {
                first.Tick(0.1f);
                second.Tick(0.1f);
            }

            Assert.AreEqual(first.Outcome, second.Outcome);
            CollectionAssert.AreEqual(first.Log, second.Log,
                "같은 좌표인데 로그가 다르다 — 시뮬레이터 안에 난수가 들어갔다");
        }

        [Test]
        public void DifferentPositions_CanFlipTheResult()
        {
            var hero = TestFixtures.MakeFighter("hero", attack: 7, defense: 1, health: 30,
                attackInterval: 1f, cooldownSeconds: Never);
            var foe = TestFixtures.MakeEnemy("foe", maxHp: 22, attack: 5, defense: 1,
                attackInterval: 1f, goldReward: 5);

            // 붙어서 시작: 아군 둘이 함께 싸운다
            var together = new BattleSimulator(
                Party(hero, hero), new[] { foe, foe },
                new List<Vector2> { new Vector2(0f, 0f), new Vector2(1f, 0f) },
                new List<Vector2> { new Vector2(2f, 0f), new Vector2(3f, 0f) });

            // 갈라져서 시작: 한 명이 멀리 떨어져 늦게 합류한다
            var split = new BattleSimulator(
                Party(hero, hero), new[] { foe, foe },
                new List<Vector2> { new Vector2(0f, 0f), new Vector2(-30f, 0f) },
                new List<Vector2> { new Vector2(2f, 0f), new Vector2(3f, 0f) });

            for (int i = 0; i < 400; i++)
            {
                together.Tick(0.1f);
                split.Tick(0.1f);
            }

            Assert.AreNotEqual(together.ElapsedSeconds, split.ElapsedSeconds,
                "배치를 바꿔도 전투가 똑같이 흘렀다 — 좌표가 결과에 영향을 주지 않는다");
        }
```

> `DifferentPositions_CanFlipTheResult` 가 승패까지 뒤집히지 않으면 **경과 시간 차이로 만족한다.**
> 이 스탯으로 승패가 갈리게 맞추려고 밸런스 수치를 고치지 않는다 (Global Constraints).
> 단언이 통과하지 않으면 **거리를 더 벌려서** 다시 시도한다.

- [ ] **Step 2: EditMode 전량**

```powershell
Run-EditMode $null "final-edit"
```

기대: 종료 코드 **0**, `total="202" passed="202" failed="0"`.

- [ ] **Step 3: PlayMode 스모크 — 실패를 예상하고 본다**

```powershell
$p = Start-Process -FilePath $UNITY -ArgumentList @('-batchmode','-runTests','-projectPath',$PROJ,
  '-testPlatform','PlayMode','-testResults',"$LOG\final-play.xml",'-logFile',"$LOG\final-play.log") -PassThru -Wait
"EXIT=$($p.ExitCode)"
([xml](Get-Content "$LOG\final-play.xml")).'test-run' |
  ForEach-Object { "total=$($_.total) passed=$($_.passed) failed=$($_.failed)" }
```

기대: `total="7"`. **7개가 다 통과하지 않을 수 있다** — 설계 6.4 가 예고한 두 가지다.

1. **"체력바가 줄어든다"가 늦어진다.** 이제 붙기까지 시간이 걸린다.
   → **굴리는 시간을 늘린다.** 단언을 약하게 만들지 않는다.
2. **"유닛이 스폰 영역 안에 선다"** — 이제 유닛이 움직이므로 시작 시점에만 성립한다.
   → 스폰 **직후**에 재도록 옮기거나, 두 구역을 합친 전장 사각형 기준으로 고친다.

실패한 테스트 이름을 `final-play.xml` 에서 확인하고 위 방침대로 고친 뒤 다시 돌린다.

- [ ] **Step 4: 씬 무결성 검사**

전투 씬 구조는 안 바뀌었으므로 그대로 통과해야 한다.

```powershell
$p = Start-Process -FilePath $UNITY -ArgumentList @('-batchmode','-projectPath',$PROJ,
  '-executeMethod','FarmCreature.EditorTools.SceneIntegrityCheck.Run','-logFile',"$LOG\integrity.log") -PassThru -Wait
"EXIT=$($p.ExitCode)"
Select-String -Path "$LOG\integrity.log" -Pattern '\[실패\]'
```

기대: 종료 코드 **0**, `[실패]` 한 줄도 없음.

- [ ] **Step 5: 커밋**

```powershell
git add Assets/Scripts/Tests/EditMode/BattleSimulatorTests.cs Assets/Scripts/Tests/PlayMode/
git commit -m "test: 좌표가 결과를 정하는지, 같은 좌표면 같은 결과인지 못박는다"
```

- [ ] **Step 6: `HANDOFF.md` 를 갱신한다**

1. 상단 날짜와 머리말에 **2026-08-16 위치 기반 전투** 한 줄 추가
2. **1절 표** — EditMode `187/187` → `202/202`
3. **2.2 전투 항목** — "자동전투, **난수 없음**(같은 입력 → 같은 결과)" 을 이렇게 고친다:
   *자동전투. 유닛이 가장 가까운 적으로 다가가 사거리 안에서 싸운다. **시뮬레이터는 난수를 안 쓰지만
   스폰 좌표를 받으므로 매 전투 결과가 다르다** — 좌표를 고정하면 완전히 결정적이다*
4. **2.4 전투 화면** — "**좌표는 표시 전용이다**" 문단을 뒤집는다. 좌표가 규칙이 됐고,
   `BattleSimulator` 가 위치를 갱신하며 `BattleUnitView` 는 읽어 그리기만 한다고 쓴다
5. **4.5 피해 공식** — 접근 시간과 배치가 생겨 체감이 달라졌다는 한 줄 추가. 수치는 안 건드렸다고 명시
6. **5.1 테스트 표** — `BattleSimulatorTests` 13 → 28, 합계 202
7. **7절 다음 할 것** — 다음 순번을 **맵/스테이지 + 건물 씬 분리**(묶어서 한 프로젝트)로 적는다
8. **8절 미검증 항목** — 전투 화면 목록에 더한다:
   - [ ] 유닛이 서로에게 걸어가 붙는 것이 보인다
   - [ ] 같은 상대에게 몰릴 때 겹쳐 보이지 않는다
   - [ ] 같은 파티로 다시 싸우면 결과가 달라진다
   - [ ] 위아래로 움직여도 앞뒤 가림이 자연스럽다

- [ ] **Step 7: `PRD.md` 를 갱신한다**

**문서 버전 1.3 → 1.4** (3행). **아래에서 위로** 작업한다.

| 위치 | 바꿀 것 |
|---|---|
| Q6 (전투 변동성) | **결정됨 (2026-08-16)** — 난수를 넣지 않고 **스폰 좌표 주입**으로 해결. 게임은 매번 다르고 시뮬레이터는 결정적 |
| 9절 백로그 | 다음 순번을 **맵/스테이지 + 건물 씬 분리**로. 케어 방출은 그 뒤로 민다 |
| 6.x 전투 절 | 이동·사거리·가장 가까운 적·난전 스폰을 규칙으로 기술. **회복만 거리를 무시**한다는 것 명시 |
| 2.2 구현 목록 | **위치 기반 전투 (2026-08-16)** 줄 신설 |
| 머리말 | 1.4 에서 바뀐 것 — 좌표가 표시 전용에서 규칙이 됐다 |

> HANDOFF 7절과 PRD 9절의 "다음 순번"이 서로 달라지면 안 된다.

- [ ] **Step 8: 커밋**

```powershell
git add docs/HANDOFF.md docs/PRD.md
git commit -m "docs: 위치 기반 전투를 반영해 HANDOFF·PRD 갱신"
```

- [ ] **Step 9: 눈으로 확인할 것을 사람에게 넘긴다**

**자동 검사가 전부 통과해도 사람이 본 적은 없다.** Step 6 의 8번 목록을 사용자에게 전달하고,
`superpowers:finishing-a-development-branch` 로 병합 여부를 묻는다.

---

## 실행 순서 요약

| 태스크 | 끝났을 때 확인되는 것 | EditMode 합계 |
|---|---|---|
| 1 | `BattleUnit` 에 위치·속도·사거리가 생겼다 | 189 |
| 2 | **호출부를 다 옮겼는데 기존 13개가 초록이다** ★ | 190 |
| 3 | 가장 가까운 적을 노리고 사거리 밖은 안 때린다 | 193 |
| 4 | 걸어가서 붙는다 | 195 |
| 5 | 겹치면 밀리고, 틱을 쪼개도 결과가 같다 | 197 |
| 6 | 회복은 거리를 무시하고 공격 스킬은 지킨다 | 199 |
| 7 | 난전 스폰 + 매 프레임 위치 반영 (표현 계층) | 199 |
| 8 | 좌표가 결과를 정한다 + PlayMode + 문서 | **202** |

**Task 2 가 이 계획의 관문이다.** 거기서 13개가 깨지면 설계 6.1 의 전제가 틀린 것이고,
그 전제가 나머지 전부를 떠받치고 있다. 깨지면 멈추고 원인을 찾는다.

**Task 8 Step 3(PlayMode)은 실패가 예상된 유일한 자리다.** 나머지에서 빨간 것이 나오면 멈춘다.
