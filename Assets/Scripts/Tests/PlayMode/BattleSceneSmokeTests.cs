using System.Collections;
using FarmCreature.Core;
using FarmCreature.Data;
using FarmCreature.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FarmCreature.Tests
{
    /// <summary>
    /// <b>씬을 실제로 로드해 프레임을 돌린다.</b> EditMode 테스트와 씬 무결성 검사가
    /// 덮지 못하는 구멍을 메우려고 만들었다 — 이 프로젝트는 "테스트도 검사도 전부 통과하는데
    /// 게임은 안 되는" 사고를 이미 한 번 겪었다 (HANDOFF 함정 3.9).
    ///
    /// 여기서 단언하는 것은 <b>연출이 아니라 동작</b>이다: 씬이 뜨는가, 유닛이 서는가,
    /// 시뮬레이터가 실제로 도는가, 결과가 농장으로 건너가는가, 재화가 한 번만 들어오는가.
    /// 색·배치·읽기 좋은가는 여전히 사람이 봐야 한다 (HANDOFF 8절).
    ///
    /// 시간을 <see cref="Time.timeScale"/> 로 압축한다. <see cref="BattleSceneRoot"/> 는
    /// <c>Time.deltaTime</c> 만 쓰므로 배속과 같은 원리로 그냥 빨라진다.
    /// </summary>
    public class BattleSceneSmokeTests
    {
        private const string FarmSceneName = "FarmScene";
        private const string BattleSceneName = "BattleScene";

        /// <summary>전투가 끝나기를 기다리는 실제 시간 상한. 넘으면 테스트가 실패한다.</summary>
        private const float TimeoutSeconds = 20f;

        private float originalTimeScale;

        [SetUp]
        public void SetUp()
        {
            originalTimeScale = Time.timeScale;
            Time.timeScale = 10f;

            // 정적 전달자는 테스트 사이에 살아남는다. 비우지 않으면 서로를 오염시킨다.
            BattleHandoff.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = originalTimeScale;
            BattleHandoff.Clear();
        }

        // ---------- 전투 씬 ----------

        [UnityTest]
        public IEnumerator BattleScene_ShowsUnitsForBothSides()
        {
            yield return LoadScene(BattleSceneName);

            var root = FindRoot();

            Assert.Greater(ActiveCount(root.allySlots), 0,
                "아군이 한 명도 화면에 서지 않았다 — 슬롯 배선이나 파티 구성이 깨졌다");
            Assert.Greater(ActiveCount(root.enemySlots), 0,
                "적이 한 마리도 화면에 서지 않았다 — 인카운터가 비었거나 슬롯이 안 켜졌다");

            // 마리 수는 인카운터 데이터에서 읽는다. 숫자를 박아 두면 씨앗 밸런스를 만질 때마다
            // 테스트가 깨진다 — 실제로 스테이지를 다시 깔면서 3 → 2 가 되어 그랬다.
            Assert.IsNotNull(root.practiceEncounter, "연습 인카운터가 배선되지 않았다");
            Assert.AreEqual(root.practiceEncounter.TotalEnemyCount, ActiveCount(root.enemySlots),
                "화면에 선 적 수가 연습 인카운터의 마리 수와 다르다");
        }

        [UnityTest]
        public IEnumerator BattleScene_PlacesUnitsInsideTheBattlefield()
        {
            yield return LoadScene(BattleSceneName);

            var root = FindRoot();

            // 난전이라 아군·적이 한 전장을 함께 쓴다 (설계 5절). 구역별로 나눠 보지 않는다.
            var battlefield = BattleSceneRoot.BattlefieldOf(root.defaultMap);

            AssertAllInside(root.allySlots, battlefield, "아군");
            AssertAllInside(root.enemySlots, battlefield, "적");
        }

        [UnityTest]
        public IEnumerator BattleScene_ActuallyRunsTheSimulator()
        {
            yield return LoadScene(BattleSceneName);

            var root = FindRoot();

            // 특정 슬롯을 지목하지 않는다. 타겟은 '가장 가까운 적'이라
            // 누가 먼저 맞을지 배치에 따라 달라진다 (설계 3.3) — 슬롯 0 을 보면 배치 운에 따라
            // 멀쩡한 게임에서도 실패한다. 여기서 볼 것은 '전투가 도는가'이지 '누가 맞는가'가 아니다.
            var watched = new System.Collections.Generic.List<BattleUnitView>();
            watched.AddRange(ActiveOnly(root.allySlots));
            watched.AddRange(ActiveOnly(root.enemySlots));

            Assert.IsNotEmpty(watched, "양쪽에 유닛이 하나도 서지 않았다");

            var fullWidths = new System.Collections.Generic.List<float>();
            for (int i = 0; i < watched.Count; i++)
            {
                fullWidths.Add(watched[i].healthBarFill.transform.localScale.x);
            }

            System.Func<bool> anyShrank = () =>
            {
                for (int i = 0; i < watched.Count; i++)
                {
                    if (watched[i].healthBarFill.transform.localScale.x < fullWidths[i])
                    {
                        return true;
                    }
                }

                return false;
            };

            // 체력바가 줄어들면 시뮬레이터가 실제로 돌고 뷰가 그것을 읽고 있다는 뜻이다.
            yield return RunUntil(anyShrank);

            Assert.IsTrue(anyShrank(),
                "어느 체력바도 " + TimeoutSeconds + "초 동안 줄지 않았다 — "
                + "전투가 돌지 않거나 뷰가 갱신되지 않는다");
        }

        [UnityTest]
        public IEnumerator BattleScene_ReachesAConclusionAndHandsOffTheResult()
        {
            yield return LoadScene(BattleSceneName);

            yield return RunUntil(() => BattleHandoff.HasResult);

            Assert.IsTrue(BattleHandoff.HasResult,
                "전투가 " + TimeoutSeconds + "초 안에 끝나지 않았다");

            var result = BattleHandoff.TakeResult();
            Assert.AreNotEqual(BattleConclusion.Aborted, result.Conclusion,
                "나가기를 누르지 않았는데 중도 이탈로 끝났다");
        }

        [UnityTest]
        public IEnumerator BattleScene_DoesNotPayTheRewardItself()
        {
            // 재화가 들어오는 지점은 농장 한 곳뿐이어야 한다 (설계 3.3).
            yield return LoadScene(BattleSceneName);

            var encounter = FindRoot().practiceEncounter;
            var species = FindRoot().practiceSpecies;

            var state = MakeState(species, gold: 30);
            BattleHandoff.SetRequest(state, encounter, null);

            yield return LoadScene(BattleSceneName);
            yield return RunUntil(() => BattleHandoff.HasResult);

            Assert.IsTrue(BattleHandoff.HasResult, "전투가 끝나지 않았다");
            Assert.AreEqual(30, state.Gold,
                "전투 씬이 재화를 직접 건드렸다 — 지급 지점이 둘이 되면 이중 지급이 난다");
        }

        // ---------- 농장 ↔ 전투 왕복 ----------

        [UnityTest]
        public IEnumerator RoundTrip_KeepsUnsavedProgressAndPaysRewardOnce()
        {
            // ★ 제일 중요한 검사. 농장 씬이 다시 로드될 때 세이브를 읽으면
            // 저장 버튼을 누르지 않은 진행이 전부 날아간다.
            yield return LoadScene(BattleSceneName);

            var root = FindRoot();
            var encounter = root.practiceEncounter;
            var species = root.practiceSpecies;

            var state = MakeState(species, gold: 30);

            // 저장하지 않은 진행을 하나 만든다.
            var fed = CareActions.Feed(state.Target);
            Assert.IsTrue(fed.Success, "먹이주기가 실패하면 이 테스트가 무의미해진다");
            int fullnessBefore = state.Target.Fullness;

            BattleHandoff.SetRequest(state, encounter, null);
            yield return LoadScene(BattleSceneName);
            yield return RunUntil(() => BattleHandoff.HasResult);
            Assert.IsTrue(BattleHandoff.HasResult, "전투가 끝나지 않았다");

            int reward = BattleHandoff.HasResult ? PeekReward() : 0;

            FindRoot().ExitBattle();
            yield return null;
            yield return RunUntil(() => SceneManager.GetActiveScene().name == FarmSceneName);
            yield return null;

            var care = Object.FindFirstObjectByType<CareManager>();
            Assert.IsNotNull(care, "농장으로 돌아오지 못했다");

            Assert.AreSame(state, care.State,
                "농장이 세이브를 다시 읽었다 — 저장하지 않은 진행이 전부 날아간다");
            Assert.AreEqual(fullnessBefore, care.State.Target.Fullness,
                "전투를 다녀오니 먹인 것이 사라졌다");
            Assert.AreEqual(30 + reward, care.State.Gold,
                "보상이 안 들어왔거나 두 번 들어왔다");
            Assert.IsFalse(BattleHandoff.HasResult,
                "결과가 남아 있으면 농장에 돌아올 때마다 보상이 다시 들어간다");
        }

        [UnityTest]
        public IEnumerator Victory_CarriesAResolvedDropList()
        {
            // 드랍 규칙과 지급은 EditMode 가 덮는다. 여기서 지키는 것은 <b>배선</b>이다 —
            // 전투 씬이 이겼을 때 실제로 굴려서 실어 보내는가. 안 실으면 증상이
            // "드랍이 영원히 안 나온다" 뿐이라 확률 탓으로 오해하기 쉽다.
            yield return LoadScene(BattleSceneName);

            var root = FindRoot();
            var state = MakeState(root.practiceSpecies, gold: 0);

            BattleHandoff.SetRequest(state, root.practiceEncounter, null);
            yield return LoadScene(BattleSceneName);
            yield return RunUntil(() => BattleHandoff.HasResult);
            Assert.IsTrue(BattleHandoff.HasResult, "전투가 끝나지 않았다");

            var result = BattleHandoff.TakeResult();

            if (result.Conclusion == BattleConclusion.Victory)
            {
                Assert.IsNotNull(result.Drops,
                    "이겼는데 드랍 목록이 null 이다 — 전투 씬이 굴리지 않았다");
            }
            else
            {
                Assert.IsNull(result.Drops, "졌는데 드랍 목록이 실려 왔다 — 재화와 규칙이 다르다");
            }
        }

        [UnityTest]
        public IEnumerator BattlePanel_RefusesToLeaveWithAnEmptyParty()
        {
            // 전투터는 8/17 에 농장에서 자기 씬으로 나갔다. 농장에는 이제 패널이 없다.
            yield return LoadScene(FarmSceneName);

            var care = Object.FindFirstObjectByType<CareManager>();
            Assert.IsNotNull(care, "CareManager 가 없다");

            // 밖에 나와 있는 케어를 전부 헛간으로 돌려보낸다.
            while (care.State.Roster.Party.Count > 0)
            {
                care.State.Roster.LeaveParty(care.State.Roster.Party[0]);
            }

            care.EnterScene(BattleGroundSceneRoot.SceneName);
            yield return null;
            yield return RunUntil(() =>
                SceneManager.GetActiveScene().name == BattleGroundSceneRoot.SceneName);
            yield return null;

            var panel = Object.FindFirstObjectByType<BattlePanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(panel, "전투터 씬에 BattlePanel 이 없다");

            // 전투터는 2단계다 — 먼저 맵을 열고, 그다음 스테이지를 고른다 (설계 5절).
            panel.SelectRow(0);
            yield return null;
            Assert.AreEqual(BattleGroundSceneRoot.SceneName, SceneManager.GetActiveScene().name,
                "맵을 고른 것만으로 씬이 바뀌면 안 된다");

            panel.SelectRow(0);
            yield return null;

            Assert.AreEqual(BattleGroundSceneRoot.SceneName, SceneManager.GetActiveScene().name,
                "파티가 빈 채로 전투 씬에 들어갔다 — 들어가자마자 지고 돌아오게 된다");
        }

        [UnityTest]
        public IEnumerator Battle_ReturnsToTheBattleGroundAndPaysThereOnce()
        {
            // ★ 8/17 에 바뀐 것. 전투가 끝나면 농장이 아니라 들어온 곳(전투터)으로 돌아오고,
            // 보상은 거기서 한 번만 들어간다. 지급 규칙은 BattleReward 한 곳에 있다.
            yield return LoadScene(BattleSceneName);

            var root = FindRoot();
            var encounter = root.practiceEncounter;
            var state = MakeState(root.practiceSpecies, gold: 30);

            BattleHandoff.SetRequest(state, encounter, null, BattleGroundSceneRoot.SceneName);
            yield return LoadScene(BattleSceneName);
            yield return RunUntil(() => BattleHandoff.HasResult);
            Assert.IsTrue(BattleHandoff.HasResult, "전투가 끝나지 않았다");

            int reward = PeekReward();

            FindRoot().ExitBattle();
            yield return null;
            yield return RunUntil(() =>
                SceneManager.GetActiveScene().name == BattleGroundSceneRoot.SceneName);
            yield return null;

            Assert.AreEqual(BattleGroundSceneRoot.SceneName, SceneManager.GetActiveScene().name,
                "전투가 끝나고 농장으로 튕겨 나갔다 — 들어온 곳으로 돌아와야 한다");

            var ground = Object.FindFirstObjectByType<BattleGroundSceneRoot>();
            Assert.IsNotNull(ground, "전투터 씬이 열리지 않았다");

            Assert.AreSame(state, ground.State,
                "전투터가 세이브를 다시 읽었다 — 저장 전 진행이 날아간다");
            Assert.AreEqual(30 + reward, ground.State.Gold,
                "보상이 안 들어왔거나 두 번 들어왔다");
            Assert.IsFalse(BattleHandoff.HasResult,
                "결과가 남아 있으면 돌아올 때마다 보상이 다시 들어간다");
        }

        // ---------- 헬퍼 ----------

        private static IEnumerator LoadScene(string name)
        {
            yield return SceneManager.LoadSceneAsync(name, LoadSceneMode.Single);

            // Start() 가 한 번 돌 기회를 준다.
            yield return null;
        }

        /// <summary>조건이 참이 되거나 시간이 다할 때까지 프레임을 돌린다.</summary>
        private static IEnumerator RunUntil(System.Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;

            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        private static BattleSceneRoot FindRoot()
        {
            var root = Object.FindFirstObjectByType<BattleSceneRoot>();
            Assert.IsNotNull(root, "BattleSceneRoot 가 없다 — 전투 씬이 열리지 않았다");
            return root;
        }

        private static GameState MakeState(CreatureSpeciesData species, int gold)
        {
            var roster = new Roster();
            roster.Add(CreatureInstance.CreateNew(species));
            roster.JoinParty(roster.GetMember(0));
            return new GameState(roster, new Inventory(), gold);
        }

        /// <summary>
        /// 결과를 꺼내지 않고 보상액만 본다 — 꺼내면 농장이 받을 것이 사라진다.
        /// 상태는 이제 <see cref="SceneHandoff"/> 로 따로 오므로 그쪽도 함께 되돌린다.
        /// </summary>
        private static int PeekReward()
        {
            var result = BattleHandoff.TakeResult();
            var carried = SceneHandoff.TakeState();

            BattleHandoff.SetResult(result.Conclusion, result.GoldReward, carried, result.StageId);
            return result.Conclusion == BattleConclusion.Victory ? result.GoldReward : 0;
        }

        private static int ActiveCount(System.Collections.Generic.List<BattleUnitView> slots)
        {
            int count = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null && slots[i].gameObject.activeSelf)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>켜진 슬롯만 추린다. 꺼진 슬롯은 이번 전투에 안 나온 여분이다.</summary>
        private static System.Collections.Generic.List<BattleUnitView> ActiveOnly(
            System.Collections.Generic.List<BattleUnitView> slots)
        {
            var active = new System.Collections.Generic.List<BattleUnitView>();
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null && slots[i].gameObject.activeSelf)
                {
                    active.Add(slots[i]);
                }
            }

            return active;
        }

        private static BattleUnitView FirstActive(System.Collections.Generic.List<BattleUnitView> slots)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null && slots[i].gameObject.activeSelf)
                {
                    return slots[i];
                }
            }

            Assert.Fail("켜진 슬롯이 하나도 없다");
            return null;
        }

        /// <summary>
        /// 유닛이 <b>움직이므로</b> "영역 안"은 스폰 시점에만 정확하다. 이 검사는 씬을 로드하고
        /// 몇 프레임 뒤에 도는데 <c>Time.timeScale</c> 이 10 이라 그동안 꽤 간다.
        /// 그래서 이동 여유를 두고 본다 — 잡으려는 것은 "몇 유닛 흘렀나"가 아니라
        /// <b>스폰이 통째로 어긋났나</b>(원점에 몰림, 맵 밖, 배치 안 됨)이고, 그건 전장 크기만큼
        /// 벗어나므로 이 여유로도 그대로 걸린다.
        /// </summary>
        private const float MovementAllowance = 2f;

        private static void AssertAllInside(
            System.Collections.Generic.List<BattleUnitView> slots, Rect area, string label)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] == null || !slots[i].gameObject.activeSelf)
                {
                    continue;
                }

                var position = slots[i].transform.position;

                Assert.GreaterOrEqual(position.x, area.xMin - MovementAllowance, label + " " + i + " 가 전장 왼쪽 밖에 섰다");
                Assert.LessOrEqual(position.x, area.xMax + MovementAllowance, label + " " + i + " 가 전장 오른쪽 밖에 섰다");
                Assert.GreaterOrEqual(position.y, area.yMin - MovementAllowance, label + " " + i + " 가 전장 아래 밖에 섰다");
                Assert.LessOrEqual(position.y, area.yMax + MovementAllowance, label + " " + i + " 가 전장 위 밖에 섰다");
            }
        }
    }
}
