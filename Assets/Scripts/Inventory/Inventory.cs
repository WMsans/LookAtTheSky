using System;

namespace Inventory
{
    public class Inventory
    {
        public const int HOTBAR_SIZE = 9;
        public const int GRID_ROWS = 3;
        public const int GRID_COLS = 9;
        public const int GRID_SIZE = GRID_ROWS * GRID_COLS; // 27

        public ItemSlot[] Hotbar { get; private set; }
        public ItemSlot[] MainGrid { get; private set; }

        private int _activeHotbarIndex;
        public int ActiveHotbarIndex
        {
            get => _activeHotbarIndex;
            private set
            {
                if (value < 0 || value >= HOTBAR_SIZE) return;
                _activeHotbarIndex = value;
                OnActiveSlotChanged?.Invoke(_activeHotbarIndex);
                OnChanged?.Invoke();
            }
        }

        public event Action OnChanged;
        public event Action<int> OnActiveSlotChanged;

        public Inventory()
        {
            Hotbar = new ItemSlot[HOTBAR_SIZE];
            MainGrid = new ItemSlot[GRID_SIZE];
            for (int i = 0; i < HOTBAR_SIZE; i++) Hotbar[i] = new ItemSlot();
            for (int i = 0; i < GRID_SIZE; i++) MainGrid[i] = new ItemSlot();
        }

        public void SetActiveSlot(int index)
        {
            ActiveHotbarIndex = index;
        }

        public BuildingItemSO GetActiveItem()
        {
            var slot = Hotbar[_activeHotbarIndex];
            return slot.IsEmpty ? null : slot.Item;
        }

        public int GetActiveItemCount()
        {
            var slot = Hotbar[_activeHotbarIndex];
            return slot.IsEmpty ? 0 : slot.Count;
        }

        /// <summary>
        /// Add items to the inventory. Fills existing stacks in hotbar first,
        /// then main grid, then empty slots.
        /// Returns the number of items that could not fit (overflow).
        /// </summary>
        public int AddItem(BuildingItemSO item, int count)
        {
            int remaining = count;

            // First pass: fill existing stacks in hotbar
            remaining = FillExistingStacks(Hotbar, item, remaining);
            if (remaining <= 0) { OnChanged?.Invoke(); return 0; }

            // Second pass: fill existing stacks in main grid
            remaining = FillExistingStacks(MainGrid, item, remaining);
            if (remaining <= 0) { OnChanged?.Invoke(); return 0; }

            // Third pass: fill empty slots in hotbar
            remaining = FillEmptySlots(Hotbar, item, remaining);
            if (remaining <= 0) { OnChanged?.Invoke(); return 0; }

            // Fourth pass: fill empty slots in main grid
            remaining = FillEmptySlots(MainGrid, item, remaining);

            OnChanged?.Invoke();
            return remaining;
        }

        /// <summary>
        /// Remove items from the inventory. Searches hotbar first, then main grid.
        /// Returns true if the full amount was removed.
        /// </summary>
        public bool RemoveItem(BuildingItemSO item, int count)
        {
            int available = CountItem(item);
            if (available < count) return false;

            int remaining = count;

            // Remove from hotbar first
            remaining = RemoveFromSlots(Hotbar, item, remaining);
            if (remaining <= 0) { OnChanged?.Invoke(); return true; }

            // Then main grid
            remaining = RemoveFromSlots(MainGrid, item, remaining);

            OnChanged?.Invoke();
            return remaining <= 0;
        }

        public int CountItem(BuildingItemSO item)
        {
            int total = 0;
            foreach (var slot in Hotbar)
                if (!slot.IsEmpty && slot.Item == item) total += slot.Count;
            foreach (var slot in MainGrid)
                if (!slot.IsEmpty && slot.Item == item) total += slot.Count;
            return total;
        }

        public bool HasItem(BuildingItemSO item, int count = 1)
        {
            return CountItem(item) >= count;
        }

        private int FillExistingStacks(ItemSlot[] slots, BuildingItemSO item, int remaining)
        {
            for (int i = 0; i < slots.Length && remaining > 0; i++)
            {
                if (!slots[i].IsEmpty && slots[i].Item == item && slots[i].Count < item.maxStackSize)
                {
                    remaining = slots[i].AddToStack(item, remaining);
                }
            }
            return remaining;
        }

        private int FillEmptySlots(ItemSlot[] slots, BuildingItemSO item, int remaining)
        {
            for (int i = 0; i < slots.Length && remaining > 0; i++)
            {
                if (slots[i].IsEmpty)
                {
                    remaining = slots[i].AddToStack(item, remaining);
                }
            }
            return remaining;
        }

        private int RemoveFromSlots(ItemSlot[] slots, BuildingItemSO item, int remaining)
        {
            for (int i = 0; i < slots.Length && remaining > 0; i++)
            {
                if (!slots[i].IsEmpty && slots[i].Item == item)
                {
                    remaining -= slots[i].RemoveFromStack(remaining);
                }
            }
            return remaining;
        }
    }
}
