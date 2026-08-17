using FarmCreature.Data;

namespace FarmCreature.Core
{
    /// <summary>
    /// 인벤토리 키(문자열 id) → 사람이 읽는 이름.
    ///
    /// <b>왜 두 계통을 다 보는가</b>: 인벤토리는 문자열 키 하나로 아이템과 알을 함께 담는다
    /// (설계 4.3). 그래서 이름을 찾으려면 <see cref="ItemDatabase"/> 와 <see cref="EggDatabase"/>
    /// 양쪽을 봐야 한다.
    ///
    /// <b>못 찾으면 id 를 그대로 돌려준다.</b> 감추면 소지품이 조용히 사라져
    /// "산 것이 어디 갔나" 가 되므로, 못생기더라도 보이는 편이 낫다.
    ///
    /// <b>Core 에 있는 이유</b>: 화면(<c>PanelLabels</c>)과 전투 보상(<see cref="BattleReward"/>)이
    /// 같은 규칙을 써야 한다. 양쪽에 복제하면 둘이 어긋났을 때 어느 쪽이 맞는지 알 수 없다.
    /// 아이템 id 는 표시 이름과 어긋나 있어(<c>hearty_feed</c> = 무쇠 발톱) 그대로 내보내면 헷갈린다.
    /// </summary>
    public static class ItemNames
    {
        public static string Of(string itemId, ItemDatabase items, EggDatabase eggs)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return "???";
            }

            var item = items != null ? items.Find(itemId) : null;
            if (item != null && !string.IsNullOrEmpty(item.displayName))
            {
                return item.displayName;
            }

            var egg = eggs != null ? eggs.Find(itemId) : null;
            if (egg != null && !string.IsNullOrEmpty(egg.displayName))
            {
                return egg.displayName;
            }

            return itemId;
        }
    }
}
