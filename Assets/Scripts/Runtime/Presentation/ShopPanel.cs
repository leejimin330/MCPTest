using FarmCreature.Core;
using FarmCreature.Data;
using UnityEngine;
using UnityEngine.UI;

namespace FarmCreature.Presentation
{
    /// <summary>
    /// 상점 — 장비와 알을 산다. 행은 <see cref="ShopStockDatabase"/> 인덱스와 1:1 이고
    /// 개수가 씬 생성 시점에 고정된다 (함정 3.1). 구매 버튼은 <b>int 인자 persistent listener</b>
    /// 로 그 인덱스를 넘긴다 (함정 3.6).
    ///
    /// 규칙은 <see cref="ShopService"/> 한 곳에만 있다 — 이 패널은 표시와 배선만 한다.
    /// </summary>
    public class ShopPanel : GamePanel
    {
        public Text titleText;

        [Tooltip("ShopStockDatabase 인덱스 순서와 같아야 한다. GameBootstrap 이 채운다.")]
        public Text[] rowTexts;

        [Tooltip("판매 목록. 행 개수가 여기서 나온다. GameBootstrap 이 채운다.")]
        public ShopStockDatabase stockDatabase;

        [Tooltip("알 이름을 id 로 찾을 때 쓴다. GameBootstrap 이 채운다.")]
        public EggDatabase eggDatabase;

        [Tooltip("로그와 전체 갱신을 맡길 조율자. GameBootstrap 이 채운다.")]
        public CareManager careManager;

        /// <summary>uGUI 버튼이 재고 인덱스를 넘긴다.</summary>
        public void Buy(int entryIndex)
        {
            var entry = stockDatabase != null ? stockDatabase.Get(entryIndex) : null;
            if (entry == null)
            {
                Log("그 물건은 팔지 않는다.");
                return;
            }

            var result = ShopService.TryBuy(state, entry, DisplayNameOf(entry));

            // 성공이든 실패든 다시 그린다 — 재화 표시가 HUD 와 패널 양쪽에 있다.
            if (careManager != null)
            {
                careManager.RefreshAll();
            }
            else
            {
                Refresh();
            }

            Log(result.Message);
        }

        public override void Refresh()
        {
            if (titleText != null)
            {
                titleText.text = PanelLabels.ShopTitle(state);
            }

            if (rowTexts == null)
            {
                return;
            }

            int count = stockDatabase != null ? stockDatabase.Count : 0;
            var inventory = Inventory;

            for (int i = 0; i < rowTexts.Length; i++)
            {
                if (rowTexts[i] == null)
                {
                    continue;
                }

                var entry = i < count ? stockDatabase.Get(i) : null;
                if (entry == null)
                {
                    rowTexts[i].text = string.Empty;
                    continue;
                }

                int owned = inventory != null ? inventory.GetCount(entry.targetId) : 0;

                rowTexts[i].text = entry.kind == ShopEntryKind.Egg
                    ? PanelLabels.ShopRow(FindEgg(entry.targetId), entry.price, owned)
                    : PanelLabels.ShopRow(FindItem(entry.targetId), entry.price, owned);
            }
        }

        private string DisplayNameOf(ShopEntry entry)
        {
            if (entry.kind == ShopEntryKind.Egg)
            {
                var egg = FindEgg(entry.targetId);
                return egg != null ? egg.displayName : entry.targetId;
            }

            var item = FindItem(entry.targetId);
            return item != null ? item.displayName : entry.targetId;
        }

        private EggData FindEgg(string eggId)
        {
            return eggDatabase != null ? eggDatabase.Find(eggId) : null;
        }

        private ItemData FindItem(string itemId)
        {
            return itemDatabase != null ? itemDatabase.Find(itemId) : null;
        }

        private void Log(string message)
        {
            if (careManager != null)
            {
                careManager.SetLog(message);
            }
        }
    }
}
