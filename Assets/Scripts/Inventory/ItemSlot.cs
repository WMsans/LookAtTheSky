using System;

namespace Inventory
{
    [Serializable]
    public class ItemSlot
    {
        public BuildingItemSO Item;
        public int Count;

        public bool IsEmpty => Item == null || Count <= 0;

        public bool CanAccept(BuildingItemSO item)
        {
            if (IsEmpty) return true;
            return Item == item && Count < Item.maxStackSize;
        }

        public int SpaceRemaining()
        {
            if (IsEmpty) return int.MaxValue;
            if (Item == null) return 0;
            return Item.maxStackSize - Count;
        }

        public int AddToStack(BuildingItemSO item, int amount)
        {
            if (IsEmpty)
            {
                Item = item;
                int toAdd = Math.Min(amount, item.maxStackSize);
                Count = toAdd;
                return amount - toAdd;
            }

            if (Item != item) return amount;

            int space = SpaceRemaining();
            int added = Math.Min(amount, space);
            Count += added;
            return amount - added;
        }

        public int RemoveFromStack(int amount)
        {
            int removed = Math.Min(amount, Count);
            Count -= removed;
            if (Count <= 0)
            {
                Item = null;
                Count = 0;
            }
            return removed;
        }

        public void Swap(ItemSlot other)
        {
            (Item, other.Item) = (other.Item, Item);
            (Count, other.Count) = (other.Count, Count);
        }

        public void Clear()
        {
            Item = null;
            Count = 0;
        }
    }
}
