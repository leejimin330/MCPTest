using FarmCreature.Data;

namespace FarmCreature.Core
{
    /// <summary>
    /// 부화 규칙. <see cref="ShopService"/> 와 같은 자리·같은 성격이다 —
    /// 순수 C# 이라 EditMode 테스트가 에셋 없이 돈다.
    ///
    /// <b>검사를 전부 끝낸 뒤에 알을 소모한다.</b> 부화는 인벤토리와 로스터 두 곳을
    /// 건드리므로, 순서를 뒤집으면 "알은 사라졌는데 케어는 없다" 가 실재한다 (설계 2.1).
    /// 상점은 "차감 먼저, 지급 나중" 으로 같은 보장을 얻었다 — 순서만 반대이고 보장은 같다.
    /// </summary>
    public static class HatchService
    {
        public static ActionResult TryHatch(GameState state, EggData egg)
        {
            if (state == null || state.Inventory == null || state.Roster == null
                || egg == null || string.IsNullOrEmpty(egg.eggId) || egg.hatchesInto == null)
            {
                return ActionResult.Fail("부화시킬 수 없는 알이다.");
            }

            if (state.Inventory.GetCount(egg.eggId) < 1)
            {
                return ActionResult.Fail("가진 알이 없다.");
            }

            // 자리를 '알을 깨기 전에' 본다. 뒤에 보면 알만 사라진다.
            if (state.Roster.IsFull)
            {
                return ActionResult.Fail("헛간이 가득 찼다. (최대 " + Roster.MaxMembers + "마리)");
            }

            state.Inventory.TryConsume(egg.eggId, 1);

            // 위 두 검사를 통과했으므로 Add 는 실패할 수 없다 — 새 인스턴스라 중복이 아니고 자리도 있다.
            var hatched = CreatureInstance.CreateNew(egg.hatchesInto);
            state.Roster.Add(hatched);

            return ActionResult.Ok(EggName(egg) + "에서 " + SpeciesName(egg) + "이(가) 나왔다.");
        }

        private static string EggName(EggData egg)
        {
            return string.IsNullOrEmpty(egg.displayName) ? egg.eggId : egg.displayName;
        }

        private static string SpeciesName(EggData egg)
        {
            return string.IsNullOrEmpty(egg.hatchesInto.displayName)
                ? egg.hatchesInto.speciesId
                : egg.hatchesInto.displayName;
        }
    }
}
