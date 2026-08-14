using System.Collections.Generic;
using UnityEngine;

namespace FarmCreature.Data
{
    /// <summary>세이브 로드와 UI 나열을 위한 아이템 목록.</summary>
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "FarmCreature/Item Database")]
    public class ItemDatabase : ScriptableObject
    {
        public List<ItemData> items = new List<ItemData>();

        public ItemData Find(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || items == null)
            {
                return null;
            }

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && items[i].itemId == itemId)
                {
                    return items[i];
                }
            }

            return null;
        }
    }
}
