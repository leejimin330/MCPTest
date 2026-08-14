using FarmCreature.Data;

namespace FarmCreature.Core
{
    /// <summary>
    /// 상점 구매 규칙. <b>순수 C# 이다</b> — 표시 이름을 인자로 받아서 데이터베이스를 뒤지지 않는다.
    /// 그래야 EditMode 테스트가 에셋 없이 돈다.
    ///
    /// 차감이 먼저이고 지급이 나중이다. <see cref="GameState.TrySpendGold"/> 는 모자라면
    /// 아무것도 바꾸지 않으므로 <b>절반만 적용된 상태가 생기지 않는다</b> (설계 4절).
    /// </summary>
    public static class ShopService
    {
        public static ActionResult TryBuy(GameState state, ShopEntry entry, string displayName)
        {
            if (state == null || state.Inventory == null || entry == null
                || string.IsNullOrEmpty(entry.targetId))
            {
                return ActionResult.Fail("살 수 없는 물건이다.");
            }

            // 0 이하는 "무료 증정"이 아니라 데이터를 잘못 적었다는 뜻이다. 조용히 넘기지 않는다.
            if (entry.price <= 0)
            {
                return ActionResult.Fail("가격이 정해지지 않은 물건이다.");
            }

            if (!state.TrySpendGold(entry.price))
            {
                return ActionResult.Fail("재화가 모자란다. (" + entry.price + " 필요)");
            }

            state.Inventory.Add(entry.targetId, 1);

            string name = string.IsNullOrEmpty(displayName) ? entry.targetId : displayName;
            return ActionResult.Ok(name + "을(를) 샀다. (−" + entry.price + ")");
        }
    }
}
