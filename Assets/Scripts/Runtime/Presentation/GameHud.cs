using System.Text;
using FarmCreature.Core;
using FarmCreature.Data;
using UnityEngine;
using UnityEngine.UI;

namespace FarmCreature.Presentation
{
    /// <summary>상태 패널과 로그 한 줄. 값을 읽어 표시만 한다.</summary>
    public class GameHud : MonoBehaviour
    {
        public Text statusText;
        public Text logText;

        private GameState state;
        private readonly StringBuilder builder = new StringBuilder();

        public void Bind(GameState gameState)
        {
            state = gameState;
            Refresh();
        }

        public void Refresh()
        {
            if (statusText == null || state == null)
            {
                return;
            }

            builder.Length = 0;
            builder.AppendLine(PanelLabels.GoldLine(state.Gold));
            builder.AppendLine("헛간 " + state.Roster.Count + "   밖 " + state.Roster.Party.Count);
            builder.AppendLine();

            var creature = state.Target;
            if (creature == null)
            {
                builder.AppendLine("돌볼 케어가 없다.");
                statusText.text = builder.ToString();
                return;
            }

            builder.AppendLine("[ " + PanelLabels.CreatureName(creature) + " ]");
            builder.AppendLine("경험치  " + creature.Exp);
            builder.AppendLine(StatLine(creature, "공격", StatType.Attack)
                               + "   " + StatLine(creature, "방어", StatType.Defense)
                               + "   " + StatLine(creature, "체력", StatType.Health));

            // 포만도·친밀도는 여기 띄우지 않는다 (2026-08-14 결정).
            // 규칙 자체는 살아 있다 — 훈련은 포만도 20 미만이면 여전히 거부된다.
            // 그 수치는 육성 패널이 보여주므로, 훈련이 막히는 이유는 거기서 확인한다.

            builder.AppendLine();
            builder.AppendLine("장착  " + (creature.EquippedItem != null
                ? creature.EquippedItem.displayName
                : "(없음)"));

            var inventory = state.Inventory;
            if (inventory != null && inventory.ItemIds.Count > 0)
            {
                builder.AppendLine("소지품");
                for (int i = 0; i < inventory.ItemIds.Count; i++)
                {
                    string id = inventory.ItemIds[i];
                    builder.AppendLine("  " + id + " x" + inventory.GetCount(id));
                }
            }

            statusText.text = builder.ToString();
        }

        public void SetLog(string message)
        {
            if (logText != null)
            {
                logText.text = message;
            }
        }

        /// <summary>장비 보너스가 있으면 "공격 7(+3)" 처럼 따로 보여준다.</summary>
        private static string StatLine(CreatureInstance creature, string label, StatType stat)
        {
            int bonus = creature.GetEquipmentBonus(stat);
            string suffix = bonus != 0 ? "(+" + bonus + ")" : string.Empty;
            return label + "  " + creature.GetStat(stat) + suffix;
        }
    }
}
