using System.Collections.Generic;

namespace FarmCreature.Core
{
    /// <summary>아이템 ID → 개수. 삽입 순서를 유지해 UI 나열이 안정적이다.</summary>
    public class Inventory
    {
        private readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        private readonly List<string> order = new List<string>();

        public IReadOnlyList<string> ItemIds
        {
            get { return order; }
        }

        public int GetCount(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return 0;
            }

            int count;
            return counts.TryGetValue(itemId, out count) ? count : 0;
        }

        public bool Has(string itemId)
        {
            return GetCount(itemId) > 0;
        }

        public void Add(string itemId, int amount)
        {
            if (string.IsNullOrEmpty(itemId) || amount <= 0)
            {
                return;
            }

            if (!counts.ContainsKey(itemId))
            {
                counts[itemId] = 0;
                order.Add(itemId);
            }

            counts[itemId] += amount;
        }

        /// <summary>충분할 때만 차감한다. 부족하면 아무것도 바꾸지 않고 false.</summary>
        public bool TryConsume(string itemId, int amount = 1)
        {
            if (amount <= 0 || GetCount(itemId) < amount)
            {
                return false;
            }

            counts[itemId] -= amount;
            return true;
        }
    }
}
