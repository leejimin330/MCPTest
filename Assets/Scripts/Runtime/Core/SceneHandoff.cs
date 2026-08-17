namespace FarmCreature.Core
{
    /// <summary>
    /// 씬을 넘나드는 <see cref="GameState"/> 를 나르는 전달자. <b>씬 전환은 전부 여기를 거친다.</b>
    ///
    /// <b>왜 필요한가</b>: 씬이 다시 로드되면 그 씬의 루트가 처음부터 돈다. 거기서 세이브를 읽으면
    /// 저장 버튼을 누르지 않은 진행(먹이·훈련·장착·파티 편성)이 전부 날아간다.
    /// 전투 하나뿐일 때는 <see cref="BattleHandoff"/> 가 결과에 상태를 얹어 그것을 막았지만,
    /// 건물마다 씬이 생기면 그 자리가 건물 수만큼 늘어난다. 그래서 <b>상태의 이동만</b> 떼어
    /// 여기 한 곳에 모았다.
    ///
    /// <b>짐과 상태를 나눈 이유</b>: 전투는 상태 말고도 인카운터·전장을 들고 가고 승패·보상을 들고 온다.
    /// 그건 전투만의 짐이라 <see cref="BattleHandoff"/> 에 남고, 모든 씬이 공통으로 나르는
    /// 상태만 여기 있다. 건물 씬은 짐이 없어 이것만 쓴다.
    ///
    /// <b>꺼내면 비워진다.</b> 남겨두면 다음에 그 씬을 열 때 지난 상태가 되살아난다.
    ///
    /// <c>DontDestroyOnLoad</c> 싱글턴이 아닌 이유는 <see cref="BattleHandoff"/> 와 같다 —
    /// <see cref="GameState"/> 가 순수 C# 이라 <c>MonoBehaviour</c> 에 실을 이유가 없고,
    /// 정적 클래스는 EditMode 에서 그대로 테스트된다.
    /// </summary>
    public static class SceneHandoff
    {
        private static GameState state;
        private static string pendingMessage;

        public static bool HasState
        {
            get { return state != null; }
        }

        /// <summary>
        /// 씬을 떠나기 직전에 상태를 맡긴다. <paramref name="message"/> 는 도착한 씬이
        /// 로그에 띄울 한 줄이다 — 없으면 도착한 쪽이 알아서 정한다.
        /// </summary>
        public static void Carry(GameState gameState, string message)
        {
            state = gameState;
            pendingMessage = message;
        }

        public static void Carry(GameState gameState)
        {
            Carry(gameState, null);
        }

        /// <summary>
        /// 상태를 꺼내고 비운다. 없으면 <c>null</c> — 그 씬을 단독으로 Play 한 경우다.
        /// 도착한 씬의 루트는 이것이 <c>null</c> 일 때 세이브를 읽거나 새 게임을 만든다.
        /// </summary>
        public static GameState TakeState()
        {
            var taken = state;
            state = null;
            return taken;
        }

        /// <summary>
        /// 함께 온 로그 한 줄을 꺼내고 비운다. 없으면 <c>null</c>.
        /// <see cref="TakeState"/> 와 따로 비우는 이유는 상태만 필요한 호출자가 있기 때문이다.
        /// </summary>
        public static string TakePendingMessage()
        {
            var taken = pendingMessage;
            pendingMessage = null;
            return taken;
        }

        /// <summary>전부 비운다. 테스트와 새 게임 시작에 쓴다.</summary>
        public static void Clear()
        {
            state = null;
            pendingMessage = null;
        }
    }
}
