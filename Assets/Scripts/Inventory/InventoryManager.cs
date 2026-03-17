using System;
using System.Collections.Generic;
using UnityEngine;

namespace Inventory
{
    public class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }

        [Header("Starting Items")]
        [SerializeField] private List<StartingItem> _startingItems = new();

        public Inventory Inventory { get; private set; }

        public event Action OnChanged;
        public event Action<int> OnActiveSlotChanged;

        [Serializable]
        public struct StartingItem
        {
            public BuildingItemSO item;
            public int count;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            Inventory = new Inventory();
            Inventory.OnChanged += () => OnChanged?.Invoke();
            Inventory.OnActiveSlotChanged += (index) => OnActiveSlotChanged?.Invoke(index);
        }

        private void Start()
        {
            foreach (var startingItem in _startingItems)
            {
                if (startingItem.item != null && startingItem.count > 0)
                {
                    int overflow = Inventory.AddItem(startingItem.item, startingItem.count);
                    if (overflow > 0)
                        Debug.LogWarning($"[InventoryManager] Could not fit {overflow}x {startingItem.item.itemName} in starting inventory.");
                }
            }
        }

        // Convenience pass-through methods
        public BuildingItemSO GetActiveItem() => Inventory.GetActiveItem();
        public void SetActiveSlot(int index) => Inventory.SetActiveSlot(index);
        public int AddItem(BuildingItemSO item, int count) => Inventory.AddItem(item, count);
        public bool RemoveItem(BuildingItemSO item, int count) => Inventory.RemoveItem(item, count);
    }
}
