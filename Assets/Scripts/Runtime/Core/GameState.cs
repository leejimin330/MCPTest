namespace FarmCreature.Core
{
    /// <summary>
    /// 저장 대상이 되는 플레이어 상태 전부를 한 덩어리로 묶는다.
    /// 케어가 1마리이던 시절에는 <c>CreatureInstance</c> 와 <c>Inventory</c> 를 따로 들고 다녔지만,
    /// 파티와 재화가 생기면서 넘겨야 할 것이 늘어 하나로 모았다.
    ///
    /// <b>육성 대상</b>(<see cref="Target"/>)이 여기 있는 이유: 먹이·훈련·장착은 전부
    /// "누구에게" 가 필요한데, 그 선택은 헛간에서 바뀌고 창고·육성 패널이 함께 본다.
    /// </summary>
    public class GameState
    {
        public Roster Roster { get; private set; }
        public Inventory Inventory { get; private set; }

        /// <summary>전투 보상으로만 늘어난다.</summary>
        public int Gold { get; private set; }

        /// <summary>먹이·훈련·장착이 적용될 케어. 헛간에서 고른다.</summary>
        public CreatureInstance Target { get; private set; }

        public GameState(Roster roster, Inventory inventory, int gold)
        {
            Roster = roster ?? new Roster();
            Inventory = inventory ?? new Inventory();
            Gold = gold < 0 ? 0 : gold;
            Target = Roster.Count > 0 ? Roster.GetMember(0) : null;
        }

        /// <summary>보유 중인 케어만 대상이 될 수 있다.</summary>
        public bool SetTarget(CreatureInstance creature)
        {
            if (!Roster.Has(creature))
            {
                return false;
            }

            Target = creature;
            return true;
        }

        public void AddGold(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Gold += amount;
        }

        /// <summary>재화가 충분할 때만 차감한다. 부족하면 아무것도 바꾸지 않는다.</summary>
        public bool TrySpendGold(int amount)
        {
            if (amount <= 0 || Gold < amount)
            {
                return false;
            }

            Gold -= amount;
            return true;
        }

        /// <summary>대상이 사라졌거나 비어 있으면 보유 목록의 첫 번째로 되돌린다.</summary>
        public void EnsureTarget()
        {
            if (Roster.Has(Target))
            {
                return;
            }

            Target = Roster.Count > 0 ? Roster.GetMember(0) : null;
        }
    }
}
