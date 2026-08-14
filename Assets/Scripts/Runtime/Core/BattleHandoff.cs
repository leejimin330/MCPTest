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

    /// <summary>농장 → 전투 씬으로 넘기는 것.</summary>
    public class BattleRequest
    {
        public GameState State;
        public BattleEncounter Encounter;
    }

    /// <summary>
    /// 전투 씬 → 농장으로 돌려보내는 것.
    /// <see cref="State"/> 를 함께 돌려주는 이유는 <see cref="BattleHandoff"/> 주석에 있다.
    /// </summary>
    public class BattleResult
    {
        public BattleConclusion Conclusion;

        /// <summary>승리했을 때 줄 재화. <b>여기서 지급하지 않는다</b> — 농장이 더한다 (설계 3.3).</summary>
        public int GoldReward;

        public GameState State;
    }

    /// <summary>
    /// 씬을 넘나들며 상태를 나르는 정적 전달자.
    ///
    /// <b>왜 <c>DontDestroyOnLoad</c> 싱글턴이 아닌가</b>: <see cref="GameState"/> 는 이미 순수 C# 이라
    /// <c>MonoBehaviour</c> 에 실어 나를 이유가 없고, 정적 클래스는 EditMode 에서 그대로 테스트된다.
    ///
    /// <b>왜 결과에도 상태를 싣는가</b>: 농장 씬이 다시 로드되면 <c>GameRoot.Start</c> 가 처음부터 돈다.
    /// 거기서 세이브를 읽으면 저장하지 않은 진행(먹이·훈련·장착)이 전부 날아간다.
    /// 그래서 들려보낸 상태 객체를 그대로 돌려받아 이어 쓴다.
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

        public static void SetRequest(GameState state, BattleEncounter encounter)
        {
            request = new BattleRequest { State = state, Encounter = encounter };
        }

        /// <summary>요청을 꺼내고 비운다. 없으면 <c>null</c> — 전투 씬을 단독으로 Play 한 경우다.</summary>
        public static BattleRequest TakeRequest()
        {
            var taken = request;
            request = null;
            return taken;
        }

        public static void SetResult(BattleConclusion conclusion, int goldReward, GameState state)
        {
            result = new BattleResult
            {
                Conclusion = conclusion,
                GoldReward = goldReward < 0 ? 0 : goldReward,
                State = state
            };
        }

        /// <summary>결과를 꺼내고 비운다. 없으면 <c>null</c> — 전투를 거치지 않고 농장을 연 경우다.</summary>
        public static BattleResult TakeResult()
        {
            var taken = result;
            result = null;
            return taken;
        }

        /// <summary>양쪽을 모두 비운다. 테스트와 새 게임 시작에 쓴다.</summary>
        public static void Clear()
        {
            request = null;
            result = null;
        }
    }
}
