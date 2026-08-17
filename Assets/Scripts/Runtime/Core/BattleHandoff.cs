using FarmCreature.Data;

namespace FarmCreature.Core
{
    /// <summary>
    /// 전투가 어떻게 끝났는가. <see cref="BattleOutcome"/> 과 달리 '진행 중'이 없고
    /// '중도 이탈'이 있다. <b>따로 둔 이유</b>: <see cref="BattleSimulator"/> 는 손대지 않기로 했고
    /// (설계 1.1), 나가기는 규칙이 아니라 화면의 사정이다.
    /// </summary>
    public enum BattleConclusion
    {
        Victory,
        Defeat,

        /// <summary>전투 중에 나갔다. 보상 없음, 페널티도 없음.</summary>
        Aborted
    }

    /// <summary>
    /// 농장 → 전투 씬으로 넘기는 <b>전투만의 짐</b>.
    /// <see cref="GameState"/> 는 여기 없다 — 그것은 모든 씬이 공통으로 나르는 것이라
    /// <see cref="SceneHandoff"/> 가 맡는다.
    /// </summary>
    public class BattleRequest
    {
        public BattleEncounter Encounter;

        /// <summary>
        /// 스테이지가 속한 맵의 전장. 비어 있으면 인카운터의 맵, 그것도 없으면 기본 맵 (설계 2.1).
        /// </summary>
        public BattleMapData Field;
    }

    /// <summary>
    /// 전투 씬 → 농장으로 돌려보내는 <b>전투만의 짐</b>.
    /// 상태는 <see cref="SceneHandoff"/> 로 따로 건너간다.
    /// </summary>
    public class BattleResult
    {
        public BattleConclusion Conclusion;

        /// <summary>승리했을 때 줄 재화. <b>여기서 지급하지 않는다</b> — 농장이 더한다 (설계 3.3).</summary>
        public int GoldReward;

        /// <summary>
        /// 이긴 스테이지의 id. 농장이 진행도를 기록할 때 쓴다.
        /// 연습 전투는 빈 문자열이라 아무것도 기록되지 않는다.
        /// </summary>
        public string StageId;
    }

    /// <summary>
    /// 전투 씬이 오갈 때의 짐을 나르는 전달자.
    ///
    /// <b>상태는 여기 없다.</b> 예전에는 요청·결과에 <see cref="GameState"/> 를 실었지만,
    /// 건물마다 씬이 생기면서 "상태를 잃지 않고 씬을 넘는다"가 전투만의 문제가 아니게 됐다.
    /// 그 절반은 <see cref="SceneHandoff"/> 로 옮겼고, 여기에는 전투만의 짐이 남았다.
    ///
    /// <b>그래도 상태를 인자로 받는 이유</b>: 짐과 상태를 따로 맡기게 하면 한쪽을 빠뜨릴 수 있고,
    /// 그 실패는 "돌아와 보니 진행이 날아갔다" 로만 드러나 원인을 짚기 어렵다.
    /// 그래서 여기서 <see cref="SceneHandoff"/> 에 함께 맡긴다 — 호출자는 한 번만 부르면 된다.
    ///
    /// <b>꺼내면 비워진다.</b> 남겨두면 농장에 돌아올 때마다 지난 결과가 다시 적용된다.
    /// </summary>
    public static class BattleHandoff
    {
        private static BattleRequest request;
        private static BattleResult result;

        public static bool HasRequest
        {
            get { return request != null; }
        }

        public static bool HasResult
        {
            get { return result != null; }
        }

        /// <summary>
        /// 전투를 시작한다. 짐은 여기 담고, 상태는 <see cref="SceneHandoff"/> 로 함께 맡긴다.
        /// </summary>
        public static void SetRequest(GameState state, BattleEncounter encounter, BattleMapData field)
        {
            request = new BattleRequest { Encounter = encounter, Field = field };
            SceneHandoff.Carry(state);
        }

        /// <summary>요청을 꺼내고 비운다. 없으면 <c>null</c> — 전투 씬을 단독으로 Play 한 경우다.</summary>
        public static BattleRequest TakeRequest()
        {
            var taken = request;
            request = null;
            return taken;
        }

        /// <summary>
        /// 전투를 끝낸다. 짐은 여기 담고, 상태는 <see cref="SceneHandoff"/> 로 함께 돌려보낸다.
        /// </summary>
        public static void SetResult(
            BattleConclusion conclusion, int goldReward, GameState state, string stageId)
        {
            result = new BattleResult
            {
                Conclusion = conclusion,
                GoldReward = goldReward < 0 ? 0 : goldReward,
                StageId = stageId
            };

            SceneHandoff.Carry(state);
        }

        /// <summary>결과를 꺼내고 비운다. 없으면 <c>null</c> — 전투를 거치지 않고 농장을 연 경우다.</summary>
        public static BattleResult TakeResult()
        {
            var taken = result;
            result = null;
            return taken;
        }

        /// <summary>
        /// 양쪽을 모두 비운다. 테스트와 새 게임 시작에 쓴다.
        /// <see cref="SceneHandoff"/> 도 함께 비운다 — 짐만 지우고 상태를 남기면
        /// 다음 씬이 지난 상태를 이어받는다.
        /// </summary>
        public static void Clear()
        {
            request = null;
            result = null;
            SceneHandoff.Clear();
        }
    }
}
