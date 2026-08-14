using UnityEngine;
using UnityEngine.UI;

namespace FarmCreature.Presentation
{
    /// <summary>
    /// 창고 — 장비 장착/해제. 장착 대상은 <b>헛간에서 고른 케어</b>다.
    /// 행은 <see cref="Data.ItemDatabase"/> 인덱스와 1:1 로 대응하고 씬 생성 시점에 개수가 고정된다.
    ///
    /// 런타임 동적 생성을 쓰지 않는 이유: 동적으로 만든 버튼의 리스너는 persistent 가 아니라
    /// <c>SceneIntegrityCheck</c> 의 리스너 검사에 잡히지 않는다. 아이템이 늘어나면 BuildAll 을 다시 돌린다.
    /// </summary>
    public class StoragePanel : GamePanel
    {
        public Text titleText;
        public Text equippedText;

        [Tooltip("ItemDatabase 인덱스 순서와 같아야 한다. GameBootstrap 이 채운다.")]
        public Text[] rowTexts;

        public override void Refresh()
        {
            var target = Target;

            if (titleText != null)
            {
                titleText.text = PanelLabels.StorageTitle(target);
            }

            if (equippedText != null)
            {
                equippedText.text = PanelLabels.EquippedLine(
                    target != null ? target.EquippedItem : null);
            }

            if (rowTexts == null)
            {
                return;
            }

            int itemCount = itemDatabase != null && itemDatabase.items != null
                ? itemDatabase.items.Count
                : 0;

            var inventory = Inventory;

            for (int i = 0; i < rowTexts.Length; i++)
            {
                if (rowTexts[i] == null)
                {
                    continue;
                }

                if (i >= itemCount)
                {
                    rowTexts[i].text = string.Empty;
                    continue;
                }

                var item = itemDatabase.items[i];
                bool equipped = item != null
                                && target != null
                                && target.EquippedItem != null
                                && target.EquippedItem.itemId == item.itemId;
                int owned = item != null && inventory != null ? inventory.GetCount(item.itemId) : 0;

                rowTexts[i].text = PanelLabels.StorageRow(item, owned, equipped);
            }
        }
    }
}
