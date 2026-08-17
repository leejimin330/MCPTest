using System.Collections;
using FarmCreature.Core;
using FarmCreature.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FarmCreature.Tests
{
    /// <summary>
    /// 건물 씬 넷(부화소·창고·상점·전투터)의 왕복. 헛간은 화면이 달라 따로 본다
    /// (<see cref="BarnSceneSmokeTests"/>).
    ///
    /// <b>같은 것을 네 번 검사하는 이유</b>: 왕복 규칙은 <see cref="BuildingSceneRoot"/> 하나지만,
    /// <b>배선은 씬마다 따로</b> 만들어진다. 한 씬만 검사하면 나머지 셋에서 조율자를 빠뜨려도
    /// 통과한다 — 그 실패의 증상은 "클릭해도 아무 일이 없다" 뿐이라 원인을 짚기 어렵다.
    /// </summary>
    public class BuildingSceneSmokeTests
    {
        private const string FarmSceneName = "FarmScene";
        private const float TimeoutSeconds = 10f;

        private static readonly string[] BuildingScenes =
        {
            HatcherySceneRoot.SceneName,
            StorageSceneRoot.SceneName,
            ShopSceneRoot.SceneName,
            BattleGroundSceneRoot.SceneName
        };

        [SetUp]
        public void SetUp()
        {
            BattleHandoff.Clear();
            SceneHandoff.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            BattleHandoff.Clear();
            SceneHandoff.Clear();
        }

        [UnityTest]
        public IEnumerator EveryBuildingScene_OpensWithItsRoomShown()
        {
            for (int i = 0; i < BuildingScenes.Length; i++)
            {
                yield return LoadScene(BuildingScenes[i]);

                var root = Object.FindFirstObjectByType<BuildingSceneRoot>();
                Assert.IsNotNull(root, BuildingScenes[i] + " 에 BuildingSceneRoot 가 없다");
                Assert.IsNotNull(root.State,
                    BuildingScenes[i] + " 단독 Play 에서 상태가 비었다 — 세이브 폴백이 동작하지 않는다");

                var panel = Object.FindFirstObjectByType<GamePanel>(FindObjectsInactive.Include);
                Assert.IsNotNull(panel, BuildingScenes[i] + " 에 방 화면이 없다");
                Assert.IsTrue(panel.gameObject.activeSelf,
                    BuildingScenes[i] + " 의 방 화면이 꺼진 채로 열렸다 — 빈 방이 뜬다");
            }
        }

        [UnityTest]
        public IEnumerator EveryBuildingScene_KeepsUnsavedProgressOnTheRoundTrip()
        {
            // ★ 제일 중요한 검사. 어느 건물이든 다녀오는 사이에 상태가 갈리면
            // 저장 버튼을 누르지 않은 진행이 전부 날아간다.
            for (int i = 0; i < BuildingScenes.Length; i++)
            {
                yield return LoadScene(FarmSceneName);

                var care = Object.FindFirstObjectByType<CareManager>();
                Assert.IsNotNull(care, "농장에 CareManager 가 없다");

                var state = care.State;
                Assert.IsNotNull(state.Target, "돌볼 케어가 없어 이 검사를 할 수 없다");

                var fed = CareActions.Feed(state.Target);
                Assert.IsTrue(fed.Success, "먹이주기가 실패하면 이 테스트가 무의미해진다");
                int fullnessBefore = state.Target.Fullness;

                care.EnterScene(BuildingScenes[i]);
                yield return null;
                yield return RunUntil(() => SceneManager.GetActiveScene().name == BuildingScenes[i]);
                yield return null;

                Assert.AreEqual(BuildingScenes[i], SceneManager.GetActiveScene().name,
                    BuildingScenes[i] + " 로 들어가지 못했다 — 건물의 sceneName 이나 배선이 틀렸다");

                var root = Object.FindFirstObjectByType<BuildingSceneRoot>();
                Assert.AreSame(state, root.State,
                    BuildingScenes[i] + " 가 세이브를 다시 읽었다 — 농장에서 한 것이 날아간다");

                root.ExitToFarm();
                yield return null;
                yield return RunUntil(() => SceneManager.GetActiveScene().name == FarmSceneName);
                yield return null;

                var backHome = Object.FindFirstObjectByType<CareManager>();
                Assert.IsNotNull(backHome, BuildingScenes[i] + " 에서 농장으로 돌아오지 못했다");

                Assert.AreSame(state, backHome.State,
                    "농장이 세이브를 다시 읽었다 — " + BuildingScenes[i] + " 에 다녀온 사이에 진행이 날아간다");
                Assert.AreEqual(fullnessBefore, backHome.State.Target.Fullness,
                    BuildingScenes[i] + " 을(를) 다녀오니 먹인 것이 사라졌다");
            }
        }

        [UnityTest]
        public IEnumerator FarmBuildings_AllPointAtAScene()
        {
            // 농장에는 패널이 하나도 없다. 다섯 동이 전부 씬으로 들어가야 한다.
            yield return LoadScene(FarmSceneName);

            var buildings = Object.FindObjectsByType<FarmBuilding>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            Assert.AreEqual(5, buildings.Length, "건물이 5동이 아니다");

            for (int i = 0; i < buildings.Length; i++)
            {
                Assert.IsFalse(string.IsNullOrEmpty(buildings[i].sceneName),
                    buildings[i].displayName + " 에 sceneName 이 없다 — 눌러도 아무 일이 없다");
                Assert.IsNotNull(buildings[i].careManager,
                    buildings[i].displayName + " 에 careManager 가 없다 — 상태를 들려 보낼 수 없다");
                Assert.IsNull(buildings[i].panel,
                    buildings[i].displayName + " 에 패널이 남아 있다 — 같은 화면이 두 곳에 있다");
            }
        }

        [UnityTest]
        public IEnumerator Shop_BuyingInsideTheSceneChangesTheCarriedState()
        {
            // 건물 안에서 한 일이 나갈 때 실제로 따라 나오는가. 상점이 가장 확인하기 쉽다.
            yield return LoadScene(FarmSceneName);

            var care = Object.FindFirstObjectByType<CareManager>();
            var state = care.State;

            // 살 수 있을 만큼 재화를 넣어준다. 씨앗 가격에 기대지 않는다.
            state.AddGold(1000);
            int goldBefore = state.Gold;

            care.EnterScene(ShopSceneRoot.SceneName);
            yield return null;
            yield return RunUntil(() =>
                SceneManager.GetActiveScene().name == ShopSceneRoot.SceneName);
            yield return null;

            var shop = Object.FindFirstObjectByType<ShopPanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(shop, "상점 씬에 ShopPanel 이 없다");

            shop.Buy(0);
            yield return null;

            Assert.Less(state.Gold, goldBefore, "상점에서 샀는데 재화가 줄지 않았다");

            var root = Object.FindFirstObjectByType<BuildingSceneRoot>();
            root.ExitToFarm();
            yield return null;
            yield return RunUntil(() => SceneManager.GetActiveScene().name == FarmSceneName);
            yield return null;

            var backHome = Object.FindFirstObjectByType<CareManager>();
            Assert.AreEqual(state.Gold, backHome.State.Gold,
                "상점에서 쓴 재화가 농장에 반영되지 않았다");
        }

        // ---------- 헬퍼 ----------

        private static IEnumerator LoadScene(string name)
        {
            yield return SceneManager.LoadSceneAsync(name, LoadSceneMode.Single);
            yield return null;
        }

        private static IEnumerator RunUntil(System.Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;

            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }
    }
}
