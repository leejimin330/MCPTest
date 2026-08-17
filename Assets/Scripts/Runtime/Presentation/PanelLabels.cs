using FarmCreature.Core;
using FarmCreature.Data;

namespace FarmCreature.Presentation
{
    /// <summary>
    /// 패널에 찍히는 문자열을 만든다. <b>순수 함수만 둔다</b> — UnityEngine.UI 를 참조하지 않으므로
    /// EditMode 테스트에서 그대로 검증된다. 패널 배선(버튼·레이아웃)은 씬 무결성 검사가 담당하고,
    /// 표시 규칙은 여기서 테스트한다.
    /// </summary>
    public static class PanelLabels
    {
        public static string CreatureName(CreatureInstance creature)
        {
            if (creature == null || creature.CurrentSpecies == null)
            {
                return "???";
            }

            return creature.CurrentSpecies.displayName;
        }

        /// <summary>
        /// 소지품 한 줄. <b>id 가 아니라 표시 이름을 쓴다.</b>
        ///
        /// 인벤토리는 문자열 키 하나로 아이템과 알을 함께 담는다(설계 4.3). 그래서 이름을 찾으려면
        /// <b>두 계통을 다 봐야</b> 한다 — 아이템에서 못 찾으면 알에서 찾는다.
        ///
        /// 어느 쪽에도 없으면 <b>id 를 그대로 보여준다.</b> 감추면 소지품이 조용히 사라져
        /// "산 것이 어디 갔나" 가 되므로, 못생기더라도 보이는 편이 낫다.
        ///
        /// 아이템 id 는 표시 이름과 어긋나 있다(`hearty_feed` = 무쇠 발톱). 초기 이름 그대로 둔
        /// 것이라 id 를 화면에 그대로 내보내면 읽는 사람이 헷갈린다.
        /// </summary>
        public static string InventoryLine(
            string itemId, int count, ItemDatabase items, EggDatabase eggs)
        {
            return InventoryName(itemId, items, eggs) + " x" + count;
        }

        private static string InventoryName(string itemId, ItemDatabase items, EggDatabase eggs)
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

        // ---------- 창고 ----------

        public static string StorageTitle(CreatureInstance target)
        {
            return target != null
                ? "창고 — " + CreatureName(target) + "의 장비"
                : "창고 — 장비 (대상 없음)";
        }

        /// <summary>
        /// 창고의 아이템 한 줄. 장착 중인 아이템은 인벤토리에서 빠져 있으므로
        /// <paramref name="owned"/> 는 <b>여분</b>(같은 아이템을 더 갖고 있는 수)을 뜻한다.
        /// </summary>
        public static string StorageRow(ItemData item, int owned, bool equipped)
        {
            if (item == null)
            {
                return "(빈 칸)";
            }

            string bonus = CareActions.StatName(item.targetStat) + " +" + item.amount;

            string state;
            if (equipped)
            {
                state = owned > 0 ? "장착 중 · 여분 " + owned : "장착 중";
            }
            else
            {
                state = "소지 " + owned;
            }

            return item.displayName + "   " + bonus + "   " + state;
        }

        public static string EquippedLine(ItemData equipped)
        {
            return equipped != null
                ? "장착 중 — " + equipped.displayName
                : "장착 중 — (없음)";
        }

        // ---------- 육성 ----------

        public static string RaisingTitle(CreatureInstance creature)
        {
            if (creature == null)
            {
                return "육성";
            }

            return CreatureName(creature) + " 육성";
        }

        /// <summary>
        /// 포만도·친밀도·경험치 한 줄. 훈련이 거부될 상태면 이유를 덧붙인다.
        /// <b>거부 판정 자체는 하지 않는다</b> — 규칙은 <see cref="CareActions.Train"/> 한 곳에만 있고,
        /// 여기서는 같은 임계값을 읽어 안내만 한다.
        /// </summary>
        public static string RaisingStatus(CreatureInstance creature)
        {
            if (creature == null)
            {
                return "육성할 케어를 헛간에서 고르자.";
            }

            string line = "포만도 " + creature.Fullness
                          + "   친밀도 " + creature.Bond
                          + "   경험치 " + creature.Exp;

            if (creature.Fullness < CareActions.TrainMinFullness)
            {
                line += "\n배가 고파 훈련을 거부한다. 먼저 먹이를 주자.";
            }

            return line;
        }

        // ---------- 헛간 ----------

        public static string BarnTitle(Roster roster)
        {
            if (roster == null)
            {
                return "헛간";
            }

            return "헛간 — 보유 " + roster.Count + "/" + Roster.MaxMembers
                   + "   파티 " + roster.Party.Count + "/" + Roster.MaxParty;
        }

        /// <summary>
        /// 헛간의 케어 한 줄. 밖에 나가 있는지, 지금 육성 대상인지를 함께 보여준다 —
        /// 파티가 여러 마리가 되면 "누구를 키우는 중인지"가 화면에서 사라지기 때문이다.
        /// </summary>
        public static string BarnRow(CreatureInstance creature, bool inParty, bool isTarget)
        {
            if (creature == null)
            {
                return string.Empty;
            }

            string line = CreatureName(creature)
                          + "   공 " + creature.GetStat(StatType.Attack)
                          + " 방 " + creature.GetStat(StatType.Defense)
                          + " 체 " + creature.GetStat(StatType.Health)
                          + "   경험치 " + creature.Exp;

            if (inParty)
            {
                line += "   [밖]";
            }

            if (isTarget)
            {
                line += " ★육성중";
            }

            return line;
        }

        /// <summary>파티 합류/복귀를 한 버튼으로 토글한다.</summary>
        public static string PartyButton(bool inParty)
        {
            return inParty ? "복귀" : "내보내기";
        }

        // ---------- 헛간 씬 (보관함) ----------

        public static string BoxTitle(Roster roster)
        {
            int count = roster != null ? roster.Count : 0;
            return "보관함  " + count + " / " + Roster.MaxMembers;
        }

        /// <summary>
        /// 보관함 한 칸. 좁은 칸에 들어가야 해서 <see cref="BarnRow"/> 보다 짧게 줄을 나눈다.
        /// 빈 칸은 <b>빈 문자열이 아니라 "비어 있음"</b>이다 — 격자는 칸이 거기 있다는 것 자체가
        /// 정보라서, 목록과 달리 빈 칸을 감추지 않는다.
        /// </summary>
        public static string BoxCell(CreatureInstance creature, bool inParty)
        {
            if (creature == null)
            {
                return "비어 있음";
            }

            string line = CreatureName(creature)
                          + "\n공 " + creature.GetStat(StatType.Attack)
                          + "  방 " + creature.GetStat(StatType.Defense)
                          + "\n체 " + creature.GetStat(StatType.Health)
                          + "  Exp " + creature.Exp;

            return inParty ? line + "\n[밖]" : line;
        }

        /// <summary>왼쪽에 세워두는 파티 한 줄. 빈 자리는 자리가 있다는 것을 보여준다.</summary>
        public static string PartySlotRow(Roster roster, int index)
        {
            var party = roster != null ? roster.Party : null;

            if (party == null || index < 0 || index >= party.Count)
            {
                return (index + 1) + ".  (빈 자리)";
            }

            var creature = party[index];
            return (index + 1) + ".  " + CreatureName(creature)
                   + "   체 " + creature.GetStat(StatType.Health);
        }

        /// <summary>헛간지기의 한마디. 상태를 읽어 지금 무엇이 막혀 있는지 알려준다.</summary>
        public static string KeeperLine(Roster roster)
        {
            if (roster == null || roster.Count == 0)
            {
                return "헛간지기\n\n\"텅 비었구먼.\n알이라도 하나 깨 오게.\"";
            }

            if (roster.IsFull)
            {
                return "헛간지기\n\n\"자리가 꽉 찼네.\n더는 못 받아.\"";
            }

            if (roster.Party.Count >= Roster.MaxParty)
            {
                return "헛간지기\n\n\"밖에 넷이나 나가 있어.\n그 이상은 무리야.\"";
            }

            return "헛간지기\n\n\"고르게. 내보낼 놈도,\n키울 놈도 자네가 정해.\"";
        }

        // ---------- 전투 ----------

        public static string BattleTitle(BattleSimulator battle)
        {
            if (battle == null)
            {
                return "전투터 — 상대를 고르자";
            }

            switch (battle.Outcome)
            {
                case BattleOutcome.Victory:
                    return "전투 종료 — 승리!";
                case BattleOutcome.Defeat:
                    return "전투 종료 — 패배";
                default:
                    return "전투 중...";
            }
        }

        /// <summary>
        /// 전투터 버튼 한 칸. 몇 마리와 붙고 얼마를 버는지 들어가기 전에 보여준다.
        ///
        /// 예전에는 적 한 마리가 곧 한 판이라 <c>EnemyButton</c> 이었지만,
        /// 전투가 인카운터 단위가 되면서 여러 마리를 한 줄로 요약한다.
        /// </summary>
        public static string EncounterButton(BattleEncounter encounter)
        {
            if (encounter == null)
            {
                return "(없음)";
            }

            return encounter.displayName
                   + "\n적 " + encounter.TotalEnemyCount
                   + " · 재화 " + encounter.TotalGoldReward;
        }

        /// <summary>전투터를 열었을 때의 안내 한 줄. 파티가 비어 있으면 어디서 내보내는지 알려준다.</summary>
        public static string BattleReadyLine(GameState state)
        {
            if (state == null || state.Roster.Party.Count == 0)
            {
                return "밖에 나와 있는 케어가 없다. 헛간에서 먼저 내보내자.";
            }

            return "출전 " + state.Roster.Party.Count + "/" + Roster.MaxParty
                   + "   " + GoldLine(state.Gold);
        }

        /// <summary>전투 화면의 배속 표시. 1x / 2x 두 단계뿐이다 (설계 8.3).</summary>
        public static string SpeedLabel(float speed)
        {
            return "배속 " + (speed >= 2f ? "2x" : "1x");
        }

        // ---------- 상점 ----------

        public static string ShopTitle(GameState state)
        {
            return state != null ? "상점   " + GoldLine(state.Gold) : "상점";
        }

        /// <summary>장비 한 줄. 창고와 달리 <b>가격</b>이 붙고, 장착 여부는 보이지 않는다.</summary>
        public static string ShopRow(ItemData item, int price, int owned)
        {
            if (item == null)
            {
                return "(빈 칸)";
            }

            return item.displayName
                   + "   " + CareActions.StatName(item.targetStat) + " +" + item.amount
                   + "   " + price + "G"
                   + "   보유 " + owned;
        }

        /// <summary>알 한 줄. 알은 스탯이 없으므로 이름·가격·보유 수만 보인다.</summary>
        public static string ShopRow(EggData egg, int price, int owned)
        {
            if (egg == null)
            {
                return "(빈 칸)";
            }

            return egg.displayName + "   " + price + "G   보유 " + owned;
        }

        // ---------- 부화소 ----------

        /// <summary>
        /// 부화소 제목. <b>헛간 점유율을 상시로 보여준다</b> — 부화가 거부되는 유일한 이유가
        /// 자리이므로, 버튼을 누르기 전에 보이는 편이 회색 버튼보다 정보가 많다 (설계 1.2).
        /// </summary>
        public static string HatchTitle(GameState state)
        {
            if (state == null || state.Roster == null)
            {
                return "부화소";
            }

            return "부화소   헛간 " + state.Roster.Count + "/" + Roster.MaxMembers;
        }

        /// <summary>알 한 줄. 상점 행과 달리 가격이 없고, 대신 <b>무엇이 나오는지</b>가 붙는다.</summary>
        public static string HatchRow(EggData egg, int owned)
        {
            if (egg == null)
            {
                return "(빈 칸)";
            }

            string result = egg.hatchesInto != null && !string.IsNullOrEmpty(egg.hatchesInto.displayName)
                ? egg.hatchesInto.displayName
                : "???";

            return egg.displayName + "   보유 " + owned + "   →  " + result;
        }

        // ---------- 맵 / 스테이지 ----------

        /// <summary>맵 한 줄. 어디까지 왔는지를 <c>1/3</c> 로 보여준다.</summary>
        public static string MapRow(StageMap map, StageProgress progress)
        {
            if (map == null)
            {
                return "(빈 칸)";
            }

            return map.displayName + "   " + map.ClearedCount(progress) + "/" + map.Count;
        }

        /// <summary>스테이지 목록의 제목. 맵 이름과 진척을 함께 보여준다.</summary>
        public static string StageMapTitle(StageMap map, StageProgress progress)
        {
            if (map == null)
            {
                return "전투터";
            }

            return map.displayName + "   " + map.ClearedCount(progress) + "/" + map.Count + " 클리어";
        }

        /// <summary>
        /// 스테이지 한 줄. <b>잠겼으면 왜 잠겼는지 함께 적는다</b> —
        /// 회색 버튼만 있는 것보다 정보가 많다 (설계 1.3).
        /// </summary>
        public static string StageRow(
            StageMap map, int stageIndex, StageProgress progress,
            Inventory inventory, ItemDatabase itemDatabase)
        {
            var stage = map != null ? map.Get(stageIndex) : null;
            if (stage == null)
            {
                return "(빈 칸)";
            }

            string head = stage.displayName + "   적 " + stage.TotalEnemyCount
                          + " · 재화 " + stage.TotalGoldReward;

            string reason = StageUnlockEvaluator.LockReason(
                map, stageIndex, progress, inventory, itemDatabase);

            if (reason != null)
            {
                return head + "   [잠김] " + reason;
            }

            if (progress != null && progress.IsCleared(stage.stageId))
            {
                return head + "   ✓ 클리어";
            }

            return head;
        }

        /// <summary>페이지가 하나뿐이면 아무것도 보여주지 않는다.</summary>
        public static string PageIndicator(int page, int pageCount)
        {
            if (pageCount <= 1)
            {
                return string.Empty;
            }

            return (page + 1) + "/" + pageCount;
        }

        public static string GoldLine(int gold)
        {
            return "재화 " + gold;
        }
    }
}
