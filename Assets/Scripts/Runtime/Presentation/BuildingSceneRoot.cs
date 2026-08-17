using FarmCreature.Core;
using FarmCreature.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FarmCreature.Presentation
{
    /// <summary>
    /// 건물 안 씬의 공통 뼈대 — <b>들어올 때 상태를 받고, 나갈 때 그대로 돌려준다.</b>
    ///
    /// <b>왜 base 클래스인가</b>: 건물이 5동이면 "저장 안 한 진행이 날아가는" 자리가 5곳 생긴다.
    /// 전투 씬에서 한 번 겪은 문제라(<see cref="BattleHandoff"/> 가 그것 때문에 상태를 실어 날랐다)
    /// 같은 코드를 다섯 번 적는 대신 규칙을 여기 한 곳에 두었다.
    /// 새 건물 씬은 <see cref="DisplayName"/> 만 적으면 왕복이 끝난다.
    ///
    /// 상태 자체는 <see cref="SceneHandoff"/> 가 나른다. 건물은 전투와 달리 따로 실어 보낼 짐이 없다.
    /// </summary>
    public abstract class BuildingSceneRoot : MonoBehaviour
    {
        public const string FarmSceneName = "FarmScene";

        [Header("단독 Play 폴백 — 씬만 열어봤을 때 세이브를 읽는다")]
        public SpeciesDatabase speciesDatabase;
        public ItemDatabase itemDatabase;

        [Header("씬 참조")]
        public CareManager careManager;
        public GameHud hud;

        protected GameState state;

        /// <summary>로그와 제목에 쓰는 건물 이름. "헛간" 처럼 조사 없이 적는다.</summary>
        protected abstract string DisplayName { get; }

        public GameState State
        {
            get { return state; }
        }

        private void Start()
        {
            state = SceneHandoff.TakeState();
            string message = SceneHandoff.TakePendingMessage();

            if (state == null)
            {
                // 이 씬만 단독으로 Play 한 경우다. 전투 씬이 연습 전투로 시작하는 것과 같은 자리 —
                // 오류로 죽는 대신 세이브를 읽어 열어준다. 개발 중 이 씬만 여는 일이 잦다.
                string error;

                if (!SaveSystem.TryLoad(speciesDatabase, itemDatabase, out state, out error))
                {
                    Debug.Log("[FarmCreature] " + DisplayName
                              + " 씬 단독 Play — 읽을 세이브가 없어 빈 상태로 엽니다. " + error);
                    state = new GameState(new Roster(), new Inventory(), 0);
                }

                message = DisplayName + " — 단독 Play (농장을 거치지 않았다)";
            }
            else if (string.IsNullOrEmpty(message))
            {
                message = DisplayName + "에 들어왔다.";
            }

            // 대상이 헛간에서 빠졌거나 비어 있을 수 있다. 패널들이 대상을 읽기 전에 바로잡는다.
            state.EnsureTarget();

            if (hud != null)
            {
                hud.Bind(state);
            }

            if (careManager != null)
            {
                careManager.Bind(state, hud);
                careManager.SetLog(message);
            }

            OnEnter();
        }

        /// <summary>바인딩이 끝난 뒤 건물마다 따로 할 일. 없으면 비워둔다.</summary>
        protected virtual void OnEnter()
        {
        }

        /// <summary>
        /// 농장으로 돌아간다. <b>상태를 그대로 들려 보낸다</b> — 여기서 놓치면
        /// 안에서 한 파티 편성·육성이 전부 사라진다.
        /// </summary>
        public void ExitToFarm()
        {
            SceneHandoff.Carry(state, DisplayName + "에서 나왔다.");
            SceneManager.LoadScene(FarmSceneName);
        }
    }
}
