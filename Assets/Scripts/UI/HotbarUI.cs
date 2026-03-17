using UnityEngine;

namespace UI
{
    public class HotbarUI : MonoBehaviour
    {
        [SerializeField] private SlotUI[] _slots;

        private Inventory.InventoryManager _inventoryManager;

        private void Start()
        {
            _inventoryManager = Inventory.InventoryManager.Instance;
            if (_inventoryManager == null)
            {
                Debug.LogError("[HotbarUI] InventoryManager.Instance not found.");
                return;
            }

            InitializeSlots();
            _inventoryManager.OnChanged += RefreshAll;
            _inventoryManager.OnActiveSlotChanged += UpdateHighlight;
        }

        private void OnDestroy()
        {
            if (_inventoryManager != null)
            {
                _inventoryManager.OnChanged -= RefreshAll;
                _inventoryManager.OnActiveSlotChanged -= UpdateHighlight;
            }
        }

        private void InitializeSlots()
        {
            var inventory = _inventoryManager.Inventory;
            for (int i = 0; i < _slots.Length && i < Inventory.Inventory.HOTBAR_SIZE; i++)
            {
                _slots[i].Initialize(inventory.Hotbar[i], i, true);
            }
            UpdateHighlight(_inventoryManager.Inventory.ActiveHotbarIndex);
        }

        private void RefreshAll()
        {
            foreach (var slot in _slots)
            {
                slot.Refresh();
            }
        }

        private void UpdateHighlight(int activeIndex)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i].SetHighlight(i == activeIndex);
            }
        }
    }
}
