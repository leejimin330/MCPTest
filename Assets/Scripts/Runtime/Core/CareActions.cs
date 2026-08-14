using FarmCreature.Data;

namespace FarmCreature.Core
{
    /// <summary>
    /// 육성 액션과 장착 액션의 규칙. 진화는 알지 못한다 — 진화 평가는 호출자(CareManager)가 조율한다.
    /// <b>실패하면 상태를 하나도 바꾸지 않는다</b> (부분 적용 금지).
    /// </summary>
    public static class CareActions
    {
        public const int FeedFullness = 25;
        public const int FeedBond = 2;
        public const int FeedExp = 5;

        public const int TrainStatGain = 2;
        public const int TrainExp = 15;
        public const int TrainFullnessCost = 15;
        public const int TrainBondCost = 1;
        public const int TrainMinFullness = 20;

        /// <summary>장착은 친밀도만 소폭 올린다. 경험치는 주지 않는다 (육성 행위가 아니므로).</summary>
        public const int EquipBond = 3;

        public static ActionResult Feed(CreatureInstance creature)
        {
            if (creature == null)
            {
                return ActionResult.Fail("케어가 없습니다.");
            }

            if (creature.Fullness >= CreatureInstance.MaxCare)
            {
                return ActionResult.Fail("배가 불러서 더 먹지 않는다.");
            }

            creature.AddFullness(FeedFullness);
            creature.AddBond(FeedBond);
            creature.AddExp(FeedExp);

            return ActionResult.Ok("먹이를 주었다. 기분이 좋아 보인다.");
        }

        public static ActionResult Train(CreatureInstance creature, StatType stat)
        {
            if (creature == null)
            {
                return ActionResult.Fail("케어가 없습니다.");
            }

            if (creature.Fullness < TrainMinFullness)
            {
                return ActionResult.Fail("너무 배가 고파서 훈련할 수 없다. 먼저 먹이를 주자.");
            }

            creature.AddStat(stat, TrainStatGain);
            creature.AddExp(TrainExp);
            creature.AddFullness(-TrainFullnessCost);
            creature.AddBond(-TrainBondCost);

            return ActionResult.Ok(StatName(stat) + " 훈련을 마쳤다.");
        }

        /// <summary>
        /// 아이템을 장착한다. 슬롯은 1칸이라 이미 차 있으면 <b>기존 장비를 인벤토리로 되돌리고</b> 교체한다.
        /// 아이템은 소모되지 않는다 — 빼면 그대로 돌아온다.
        /// </summary>
        public static ActionResult Equip(CreatureInstance creature, Inventory inventory, ItemData item)
        {
            if (creature == null || inventory == null)
            {
                return ActionResult.Fail("케어가 없습니다.");
            }

            if (item == null)
            {
                return ActionResult.Fail("장착할 아이템이 없습니다.");
            }

            if (creature.EquippedItem != null && creature.EquippedItem.itemId == item.itemId)
            {
                return ActionResult.Fail(item.displayName + "은(는) 이미 장착 중이다.");
            }

            // 먼저 꺼내본다. 실패하면 아무것도 바꾸지 않는다.
            if (!inventory.TryConsume(item.itemId))
            {
                return ActionResult.Fail(item.displayName + "이(가) 없다.");
            }

            var previous = creature.EquippedItem;
            if (previous != null)
            {
                inventory.Add(previous.itemId, 1);
            }

            creature.SetEquipped(item);
            creature.AddBond(EquipBond);

            string message = previous != null
                ? previous.displayName + " 대신 " + item.displayName + "을(를) 장착했다."
                : item.displayName + "을(를) 장착했다.";

            return ActionResult.Ok(message);
        }

        /// <summary>장착을 해제하고 인벤토리로 되돌린다.</summary>
        public static ActionResult Unequip(CreatureInstance creature, Inventory inventory)
        {
            if (creature == null || inventory == null)
            {
                return ActionResult.Fail("케어가 없습니다.");
            }

            var equipped = creature.EquippedItem;
            if (equipped == null)
            {
                return ActionResult.Fail("장착 중인 아이템이 없다.");
            }

            inventory.Add(equipped.itemId, 1);
            creature.SetEquipped(null);

            return ActionResult.Ok(equipped.displayName + "을(를) 해제했다.");
        }

        public static string StatName(StatType stat)
        {
            switch (stat)
            {
                case StatType.Attack: return "공격";
                case StatType.Defense: return "방어";
                case StatType.Health: return "체력";
                default: return stat.ToString();
            }
        }
    }
}
