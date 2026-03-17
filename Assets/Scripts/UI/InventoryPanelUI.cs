using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace UI
{
    public class InventoryPanelUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private SlotUI[] _gridSlots;     // 27 main grid slots
        [SerializeField] private SlotUI[] _hotbarSlots;    // 9 hotbar mirror slots
        [SerializeField] private CursorItem _cursorItem;

        [Header("Input")]
        [SerializeField] private InputActionReference _toggleAction;

        private Inventory.InventoryManager _inventoryManager;
        private bool _isOpen;

        // Held item state for drag/swap (stored separately from CursorItem display)
        private Inventory.ItemSlot _heldSlotSource; // The slot the item was picked from
        private Inventory.BuildingItemSO _heldItem;
        private int _heldCount;

        private void Start()
        {
            _inventoryManager = Inventory.InventoryManager.Instance;
            if (_inventoryManager == null)
            {
                Debug.LogError("[InventoryPanelUI] InventoryManager.Instance not found.");
                return;
            }

            InitializeSlots();
            _inventoryManager.OnChanged += RefreshAll;

            Close();
        }

        private void OnDestroy()
        {
            if (_inventoryManager != null)
            {
                _inventoryManager.OnChanged -= RefreshAll;
            }
            UnsubscribeSlots();
        }

        private void Update()
        {
            // Toggle panel with Tab or E
            if (Keyboard.current != null &&
                (Keyboard.current[Key.Tab].wasPressedThisFrame || Keyboard.current[Key.E].wasPressedThisFrame))
            {
                if (_isOpen) Close();
                else Open();
            }
        }

        private void InitializeSlots()
        {
            var inventory = _inventoryManager.Inventory;

            for (int i = 0; i < _gridSlots.Length && i < Inventory.Inventory.GRID_SIZE; i++)
            {
                _gridSlots[i].Initialize(inventory.MainGrid[i], i, false);
                _gridSlots[i].OnSlotClicked += HandleSlotClicked;
            }

            for (int i = 0; i < _hotbarSlots.Length && i < Inventory.Inventory.HOTBAR_SIZE; i++)
            {
                _hotbarSlots[i].Initialize(inventory.Hotbar[i], i, true);
                _hotbarSlots[i].OnSlotClicked += HandleSlotClicked;
            }
        }

        private void UnsubscribeSlots()
        {
            if (_gridSlots != null)
                foreach (var slot in _gridSlots)
                    if (slot != null) slot.OnSlotClicked -= HandleSlotClicked;

            if (_hotbarSlots != null)
                foreach (var slot in _hotbarSlots)
                    if (slot != null) slot.OnSlotClicked -= HandleSlotClicked;
        }

        public void Open()
        {
            _isOpen = true;
            _panel.SetActive(true);

            if (MouseManager.Instance != null)
                MouseManager.Instance.RequestUnlock(this);

            RefreshAll();
        }

        public void Close()
        {
            // Return held item to inventory if holding
            if (_heldItem != null && _heldCount > 0)
            {
                _inventoryManager.AddItem(_heldItem, _heldCount);
                _heldItem = null;
                _heldCount = 0;
                _cursorItem.Hide();
            }

            _isOpen = false;
            _panel.SetActive(false);

            if (MouseManager.Instance != null)
                MouseManager.Instance.ReleaseLock(this);
        }

        private void HandleSlotClicked(SlotUI slotUI, PointerEventData.InputButton button)
        {
            if (!_isOpen) return;

            var slot = slotUI.Slot;

            if (button == PointerEventData.InputButton.Left)
            {
                HandleLeftClick(slot);
            }
            else if (button == PointerEventData.InputButton.Right)
            {
                HandleRightClick(slot);
            }

            RefreshAll();
        }

        private void HandleLeftClick(Inventory.ItemSlot slot)
        {
            if (_heldItem == null)
            {
                // Not holding anything — pick up from slot
                if (!slot.IsEmpty)
                {
                    _heldItem = slot.Item;
                    _heldCount = slot.Count;
                    slot.Clear();
                    _cursorItem.Show(_heldItem, _heldCount);
                }
            }
            else
            {
                // Holding something
                if (slot.IsEmpty)
                {
                    // Drop into empty slot
                    slot.AddToStack(_heldItem, _heldCount);
                    _heldItem = null;
                    _heldCount = 0;
                    _cursorItem.Hide();
                }
                else if (slot.Item == _heldItem)
                {
                    // Same type — merge
                    int overflow = slot.AddToStack(_heldItem, _heldCount);
                    if (overflow <= 0)
                    {
                        _heldItem = null;
                        _heldCount = 0;
                        _cursorItem.Hide();
                    }
                    else
                    {
                        _heldCount = overflow;
                        _cursorItem.UpdateCount(_heldCount);
                    }
                }
                else
                {
                    // Different type — swap
                    var tempItem = slot.Item;
                    var tempCount = slot.Count;
                    slot.Clear();
                    slot.AddToStack(_heldItem, _heldCount);
                    _heldItem = tempItem;
                    _heldCount = tempCount;
                    _cursorItem.Show(_heldItem, _heldCount);
                }
            }
        }

        private void HandleRightClick(Inventory.ItemSlot slot)
        {
            if (_heldItem != null)
            {
                // Holding something — place 1 item
                if (slot.IsEmpty || (slot.Item == _heldItem && slot.Count < _heldItem.maxStackSize))
                {
                    slot.AddToStack(_heldItem, 1);
                    _heldCount--;
                    if (_heldCount <= 0)
                    {
                        _heldItem = null;
                        _heldCount = 0;
                        _cursorItem.Hide();
                    }
                    else
                    {
                        _cursorItem.UpdateCount(_heldCount);
                    }
                }
            }
            else
            {
                // Not holding — pick up half
                if (!slot.IsEmpty)
                {
                    int halfCount = Mathf.CeilToInt(slot.Count / 2f);
                    _heldItem = slot.Item;
                    _heldCount = halfCount;
                    slot.RemoveFromStack(halfCount);
                    _cursorItem.Show(_heldItem, _heldCount);
                }
            }
        }

        private void RefreshAll()
        {
            if (_gridSlots != null)
                foreach (var slot in _gridSlots) slot.Refresh();
            if (_hotbarSlots != null)
                foreach (var slot in _hotbarSlots) slot.Refresh();
        }
    }
}
