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

        public static string GoldLine(int gold)
        {
            return "재화 " + gold;
        }
    }
}
