using System.Text;
using FarmCreature.Data;

namespace FarmCreature.Core
{
    /// <summary>
    /// 전투 결과를 상태에 새기는 <b>유일한 지점</b>. 재화도 진행도도 드랍도 여기서만 늘어난다.
    ///
    /// <b>왜 떼어냈나</b>: 예전에는 <c>GameRoot.ApplyBattleResult</c> 한 곳이었다.
    /// 전투터가 씬이 되면서 전투에서 돌아오는 곳이 농장일 수도, 전투터일 수도 있게 됐고
    /// 그 순간 지급 지점이 둘이 됐다. 규칙을 순수 C# 로 빼서 <b>부르는 곳은 둘이어도
    /// 규칙은 하나</b>가 되게 했다. 덤으로 EditMode 에서 그대로 테스트된다.
    ///
    /// <b>전투 씬은 여전히 액수와 굴려진 결과만 실어 보낸다.</b> 씬이 중간에 바뀌어도
    /// 이중 지급이 없다.
    /// </summary>
    public static class BattleReward
    {
        /// <summary>
        /// 결과를 적용하고 로그에 띄울 한 줄을 돌려준다.
        /// <paramref name="result"/> 가 <c>null</c> 이면 아무것도 하지 않는다 —
        /// 전투를 거치지 않고 그 씬을 연 경우다.
        ///
        /// <paramref name="items"/> · <paramref name="eggs"/> 는 드랍 이름을 적는 데만 쓴다.
        /// 없으면 id 가 그대로 적힌다 — 인벤토리에 넣는 것 자체는 영향을 받지 않는다.
        /// </summary>
        public static string Apply(
            GameState state, BattleResult result, ItemDatabase items, EggDatabase eggs)
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

                    // 드랍도 여기서만 들어간다. 확률은 이미 DropResolver 가 굴렸다.
                    string dropLine = GiveDrops(state, result, items, eggs);

                    var message = new StringBuilder();
                    message.Append("승리했다! 재화 ").Append(result.GoldReward).Append(" 획득.");

                    if (dropLine != null)
                    {
                        message.Append(' ').Append(dropLine);
                    }

                    if (firstClear)
                    {
                        message.Append(" 새 길이 열렸다.");
                    }

                    return message.ToString();

                case BattleConclusion.Defeat:
                    return "패배했다. 케어는 다치지 않는다 — 더 키워서 다시 오자.";

                default:
                    return "전투에서 물러났다.";
            }
        }

        /// <summary>이름을 적을 데이터베이스 없이 부른다. 드랍은 id 로 적힌다.</summary>
        public static string Apply(GameState state, BattleResult result)
        {
            return Apply(state, result, null, null);
        }

        /// <summary>
        /// 드랍을 인벤토리에 넣고 로그 한 조각을 만든다. 없으면 <c>null</c>.
        /// </summary>
        private static string GiveDrops(
            GameState state, BattleResult result, ItemDatabase items, EggDatabase eggs)
        {
            if (result.Drops == null || result.Drops.Count == 0)
            {
                return null;
            }

            var line = new StringBuilder();

            for (int i = 0; i < result.Drops.Count; i++)
            {
                var award = result.Drops[i];

                if (string.IsNullOrEmpty(award.ItemId) || award.Count <= 0)
                {
                    continue;
                }

                state.Inventory.Add(award.ItemId, award.Count);

                if (line.Length > 0)
                {
                    line.Append(", ");
                }

                line.Append(ItemNames.Of(award.ItemId, items, eggs))
                    .Append(" x")
                    .Append(award.Count);
            }

            return line.Length > 0 ? line + " 을(를) 얻었다." : null;
        }
    }
}
