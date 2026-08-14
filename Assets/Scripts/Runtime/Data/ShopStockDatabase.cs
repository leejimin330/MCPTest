using System.Collections.Generic;
using UnityEngine;

namespace FarmCreature.Data
{
    /// <summary>파는 물건이 어느 계통인가. 표시 이름을 어디서 찾을지만 정한다 (설계 3.2).</summary>
    public enum ShopEntryKind
    {
        Equipment = 0,
        Egg = 1
    }

    /// <summary>판매 목록 한 줄.</summary>
    [System.Serializable]
    public class ShopEntry
    {
        public ShopEntryKind kind = ShopEntryKind.Equipment;

        [Tooltip("장비면 ItemData.itemId, 알이면 EggData.eggId. 인벤토리에 이 문자열이 그대로 담긴다.")]
        public string targetId;

        [Tooltip("판매가. 0 이하는 데이터 실수로 본다 — ShopService 가 거부한다.")]
        public int price = 1;
    }

    /// <summary>
    /// 상점 판매 목록. <b>상점 패널의 행 개수가 여기서 나온다</b> —
    /// 항목을 늘리면 <c>GameBootstrap.BuildAll</c> 을 다시 돌려야 한다 (함정 3.1).
    /// </summary>
    [CreateAssetMenu(fileName = "ShopStockDatabase", menuName = "FarmCreature/Shop Stock Database")]
    public class ShopStockDatabase : ScriptableObject
    {
        public List<ShopEntry> entries = new List<ShopEntry>();

        public int Count
        {
            get { return entries != null ? entries.Count : 0; }
        }

        /// <summary>범위를 벗어나면 null. 버튼이 인덱스를 들고 있어 방어가 필요하다 (함정 3.6).</summary>
        public ShopEntry Get(int index)
        {
            if (entries == null || index < 0 || index >= entries.Count)
            {
                return null;
            }

            return entries[index];
        }
    }
}
