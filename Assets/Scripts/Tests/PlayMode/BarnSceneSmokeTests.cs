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
    /// 헛간 씬을 실제로 로드해 돌린다.
    ///
    /// <b>여기서 지키는 것은 하나다: 건물을 다녀와도 저장하지 않은 진행이 남는가.</b>
    /// 건물마다 씬이 생기면 그 자리가 건물 수만큼 늘어나고, 실패하면 증상이
    /// "돌아와 보니 먹인 것이 사라졌다" 뿐이라 원인을 짚기 어렵다.
    /// EditMode 테스트는 <see cref="SceneHandoff"/> 규칙만 보고, 실제 씬 왕복은 보지 못한다.
    ///
    /// 함께 지키는 것: <b>[육성] 팝업이 방 화면을 꺼뜨리지 않는가</b>.
    /// <see cref="GamePanel.Open"/> 이 형제 패널을 닫으므로, 부모를 나눠 둔 것이 풀리면
    /// 화면이 통째로 검게 변한다 — 무결성 검사와 여기 양쪽에서 잡는다.
    /// </summary>
    public class BarnSceneSmokeTests
    {
        private const string FarmSceneName = "FarmScene";
        private const string BarnSceneName = "BarnScene";

        private const float TimeoutSeconds = 10f;

        [SetUp]
        public void SetUp()
        {
            // 정적 전달자는 테스트 사이에 살아남는다. 비우지 않으면 서로를 오염시킨다.
            BattleHandoff.Clear();
            SceneHandoff.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            BattleHandoff.Clear();
            SceneHandoff.Clear();
        }

        // ---------- 씬이 뜨는가 ----------

        [UnityTest]
        public IEnumerator BarnScene_OpensWithTheRoomShownAndRaisingClosed()
        {
            yield return LoadScene(BarnSceneName);

            var room = Object.FindFirstObjectByType<BarnRoomPanel>(FindObjectsInactive.Include);
            var raising = Object.FindFirstObjectByType<RaisingPanel>(FindObjectsInactive.Include);

            Assert.IsNotNull(room, "헛간 방 화면이 없다");
            Assert.IsNotNull(raising, "육성 패널이 헛간 씬에 없다");

            Assert.IsTrue(room.gameObject.activeSelf,
                "방 화면이 꺼진 채로 열렸다 — 들어가면 빈 방이 뜬다");
            Assert.IsFalse(raising.gameObject.activeSelf,
                "들어가자마자 육성창이 방을 덮고 있다");
        }

        [UnityTest]
        public IEnumerator BarnScene_StandalonePlay_StillHasAState()
        {
            // 농장을 거치지 않고 이 씬만 Play 하는 일이 잦다. 죽지 않고 열려야 한다.
            yield return LoadScene(BarnSceneName);

            var root = FindBarnRoot();

            Assert.IsNotNull(root.State,
                "단독 Play 에서 상태가 비었다 — 세이브 폴백이 동작하지 않는다");
        }

        [UnityTest]
        public IEnumerator RaisingPopup_DoesNotHideTheRoom()
        {
            yield return LoadScene(BarnSceneName);

            var root = FindBarnRoot();
            var room = root.roomPanel;
            var raising = Object.FindFirstObjectByType<RaisingPanel>(FindObjectsInactive.Include);

            raising.Open();
            yield return null;

            Assert.IsTrue(raising.gameObject.activeSelf, "육성 패널이 열리지 않았다");
            Assert.IsTrue(room.gameObject.activeSelf,
                "[육성]을 열었더니 방 화면이 함께 꺼졌다 — 방과 팝업의 부모가 같다");
        }

        // ---------- 농장 ↔ 헛간 왕복 ----------

        [UnityTest]
        public IEnumerator RoundTrip_KeepsUnsavedProgress()
        {
            // ★ 제일 중요한 검사. 헛간 씬과 농장 씬 어느 쪽이든 세이브를 다시 읽으면
            // 저장 버튼을 누르지 않은 진행이 전부 날아간다.
            yield return LoadScene(FarmSceneName);

            var care = Object.FindFirstObjectByType<CareManager>();
            Assert.IsNotNull(care, "농장에 CareManager 가 없다");

            var state = care.State;
            Assert.IsNotNull(state.Target, "돌볼 케어가 없어 이 검사를 할 수 없다");

            // 저장하지 않은 진행을 하나 만든다.
            var fed = CareActions.Feed(state.Target);
            Assert.IsTrue(fed.Success, "먹이주기가 실패하면 이 테스트가 무의미해진다");
            int fullnessBefore = state.Target.Fullness;

            care.EnterScene(BarnSceneName);
            yield return null;
            yield return RunUntil(() => SceneManager.GetActiveScene().name == BarnSceneName);
            yield return null;

            var root = FindBarnRoot();
            Assert.AreSame(state, root.State,
                "헛간이 세이브를 다시 읽었다 — 농장에서 한 것이 전부 날아간다");

            root.ExitToFarm();
            yield return null;
            yield return RunUntil(() => SceneManager.GetActiveScene().name == FarmSceneName);
            yield return null;

            var backHome = Object.FindFirstObjectByType<CareManager>();
            Assert.IsNotNull(backHome, "농장으로 돌아오지 못했다");

            Assert.AreSame(state, backHome.State,
                "농장이 세이브를 다시 읽었다 — 헛간에 다녀온 사이에 진행이 날아간다");
            Assert.AreEqual(fullnessBefore, backHome.State.Target.Fullness,
                "헛간을 다녀오니 먹인 것이 사라졌다");
        }

        [UnityTest]
        public IEnumerator RoundTrip_CarriesPartyChangesBackToTheFarmField()
        {
            // 헛간에서 내보낸 케어는 농장 필드에 실제로 서 있어야 한다.
            // 상태만 건너가고 필드가 다시 그려지지 않으면 화면과 데이터가 어긋난다.
            yield return LoadScene(FarmSceneName);

            var care = Object.FindFirstObjectByType<CareManager>();
            var state = care.State;
            Assert.Greater(state.Roster.Count, 1, "보유가 2마리 미만이면 이 검사를 할 수 없다");

            care.EnterScene(BarnSceneName);
            yield return null;
            yield return RunUntil(() => SceneManager.GetActiveScene().name == BarnSceneName);
            yield return null;

            var room = FindBarnRoot().roomPanel;

            // 파티를 전부 비운 뒤 한 마리만 내보낸다 — 시작 파티 수에 기대지 않는다.
            while (state.Roster.Party.Count > 0)
            {
                state.Roster.LeaveParty(state.Roster.Party[0]);
            }

            room.SelectSlot(0);
            room.TogglePartyOfSelected();
            yield return null;

            Assert.AreEqual(1, state.Roster.Party.Count,
                "헛간에서 내보내기가 동작하지 않았다");

            FindBarnRoot().ExitToFarm();
            yield return null;
            yield return RunUntil(() => SceneManager.GetActiveScene().name == FarmSceneName);
            yield return null;

            var field = Object.FindFirstObjectByType<PartyField>();
            Assert.IsNotNull(field, "농장에 PartyField 가 없다");

            Assert.AreEqual(1, ActiveSlots(field),
                "헛간에서 내보낸 케어가 농장 필드에 서 있지 않다");
        }

        // ---------- 방 화면의 조작 ----------

        [UnityTest]
        public IEnumerator SelectingASlot_ChangesTheRaisingTarget()
        {
            yield return LoadScene(FarmSceneName);

            var care = Object.FindFirstObjectByType<CareManager>();
            var state = care.State;
            Assert.Greater(state.Roster.Count, 1, "보유가 2마리 미만이면 이 검사를 할 수 없다");

            care.EnterScene(BarnSceneName);
            yield return null;
            yield return RunUntil(() => SceneManager.GetActiveScene().name == BarnSceneName);
            yield return null;

            var room = FindBarnRoot().roomPanel;

            room.SelectSlot(1);
            yield return null;

            Assert.AreSame(state.Roster.GetMember(1), state.Target,
                "칸을 골랐는데 육성 대상이 따라오지 않았다 — 창고·상태창이 다른 케어를 가리킨다");
        }

        [UnityTest]
        public IEnumerator SelectingAnEmptySlot_ChangesNothing()
        {
            yield return LoadScene(BarnSceneName);

            var root = FindBarnRoot();
            var state = root.State;
            var before = state.Target;

            // 보유 상한 바로 뒤 칸은 반드시 비어 있다.
            root.roomPanel.SelectSlot(Roster.MaxMembers - 1 >= state.Roster.Count
                ? Roster.MaxMembers - 1
                : state.Roster.Count);
            yield return null;

            Assert.AreSame(before, state.Target,
                "빈 칸을 골랐는데 대상이 바뀌었다");
        }

        // ---------- 헬퍼 ----------

        private static IEnumerator LoadScene(string name)
        {
            yield return SceneManager.LoadSceneAsync(name, LoadSceneMode.Single);

            // Start() 가 한 번 돌 기회를 준다.
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

        private static BarnSceneRoot FindBarnRoot()
        {
            var root = Object.FindFirstObjectByType<BarnSceneRoot>();
            Assert.IsNotNull(root, "BarnSceneRoot 가 없다 — 헛간 씬이 열리지 않았다");
            return root;
        }

        private static int ActiveSlots(PartyField field)
        {
            int count = 0;

            for (int i = 0; i < field.slots.Count; i++)
            {
                if (field.slots[i] != null && field.slots[i].gameObject.activeSelf)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
