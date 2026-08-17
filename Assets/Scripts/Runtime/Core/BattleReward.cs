namespace FarmCreature.Core
{
    /// <summary>
    /// 전투 결과를 상태에 새기는 <b>유일한 지점</b>. 재화도 진행도도 여기서만 늘어난다.
    ///
    /// <b>왜 떼어냈나</b>: 예전에는 <c>GameRoot.ApplyBattleResult</c> 한 곳이었다.
    /// 전투터가 씬이 되면서 전투에서 돌아오는 곳이 농장일 수도, 전투터일 수도 있게 됐고
    /// 그 순간 지급 지점이 둘이 됐다. 규칙을 순수 C# 로 빼서 <b>부르는 곳은 둘이어도
    /// 규칙은 하나</b>가 되게 했다. 덤으로 EditMode 에서 그대로 테스트된다.
    ///
    /// <b>전투 씬은 여전히 액수만 실어 보낸다.</b> 씬이 중간에 바뀌어도 이중 지급이 없다.
    /// </summary>
    public static class BattleReward
    {
        /// <summary>
        /// 결과를 적용하고 로그에 띄울 한 줄을 돌려준다.
        /// <paramref name="result"/> 가 <c>null</c> 이면 아무것도 하지 않는다 —
        /// 전투를 거치지 않고 그 씬을 연 경우다.
        /// </summary>
        public static string Apply(GameState state, BattleResult result)
        {
            if (state == null || result == null)
            {
                return null;
            }

            switch (result.Conclusion)
            {
                case BattleConclusion.Victory:
                    state.AddGold(result.GoldReward);

                    // 빈 id 는 StageProgress 가 알아서 무시한다. 연습 전투가 그 경우다.
                    bool firstClear = state.Progress.MarkCleared(result.StageId);

                    return firstClear
                        ? "승리했다! 재화 " + result.GoldReward + " 획득. 새 길이 열렸다."
                        : "승리했다! 재화 " + result.GoldReward + " 획득.";

                case BattleConclusion.Defeat:
                    return "패배했다. 케어는 다치지 않는다 — 더 키워서 다시 오자.";

                default:
                    return "전투에서 물러났다.";
            }
        }
    }
}
