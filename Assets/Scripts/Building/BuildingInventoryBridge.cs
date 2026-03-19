using System.Collections.Generic;
using UnityEngine;

namespace Building
{
    /// <summary>
    /// Mediator between InventoryManager and BuildingController.
    /// The only component that references both systems.
    /// </summary>
    public class BuildingInventoryBridge : MonoBehaviour
    {
        [SerializeField] private BuildingController _buildingController;

        private Inventory.InventoryManager _inventoryManager;
        private BuildingGrid _grid;

        // Track which item was placed at each GameObject so we can restore on removal
        private Dictionary<GameObject, Inventory.BuildingItemSO> _placedItemTracker = new();

        // Temporarily stores the item being placed between OnBeforePlace and OnBoardPlaced
        private Inventory.BuildingItemSO _pendingPlaceItem;

        private void Start()
        {
            _inventoryManager = Inventory.InventoryManager.Instance;
            _grid = BuildingGrid.Instance;

            if (_inventoryManager == null)
            {
                Debug.LogError("[BuildingInventoryBridge] InventoryManager.Instance not found.");
                return;
            }
            if (_buildingController == null)
            {
                Debug.LogError("[BuildingInventoryBridge] BuildingController not assigned.");
                return;
            }
            if (_grid == null)
            {
                Debug.LogError("[BuildingInventoryBridge] BuildingGrid.Instance not found.");
                return;
            }

            _inventoryManager.OnActiveSlotChanged += HandleActiveSlotChanged;
            _inventoryManager.OnChanged += HandleInventoryChanged;

            _buildingController.OnBeforePlace += HandleBeforePlace;
            _buildingController.OnBoardPlaced += HandleBoardPlaced;
            _buildingController.OnPlaceFailed += HandlePlaceFailed;
            _buildingController.OnBeforeRemove += HandleBeforeRemove;

            SyncActiveItem();
        }

        private void OnDestroy()
        {
            if (_inventoryManager != null)
            {
                _inventoryManager.OnActiveSlotChanged -= HandleActiveSlotChanged;
                _inventoryManager.OnChanged -= HandleInventoryChanged;
            }
            if (_buildingController != null)
            {
                _buildingController.OnBeforePlace -= HandleBeforePlace;
                _buildingController.OnBoardPlaced -= HandleBoardPlaced;
                _buildingController.OnPlaceFailed -= HandlePlaceFailed;
                _buildingController.OnBeforeRemove -= HandleBeforeRemove;
            }
        }

        private void HandleActiveSlotChanged(int index)
        {
            SyncActiveItem();
        }

        private void HandleInventoryChanged()
        {
            SyncActiveItem();
        }

        private void SyncActiveItem()
        {
            var item = _inventoryManager.GetActiveItem();
            if (item != null)
            {
                _buildingController.ActivePrefab = item.prefab;
                _buildingController.ActivePlacementMode = item.placementMode;
            }
            else
            {
                _buildingController.ActivePrefab = null;
                _buildingController.ActivePlacementMode = Inventory.PlacementMode.Oriented;
            }
        }

        private void HandleBeforePlace(PlaceCancelEventArgs args)
        {
            var item = _inventoryManager.GetActiveItem();
            if (item == null)
            {
                args.Cancel = true;
                _pendingPlaceItem = null;
                return;
            }

            if (!_inventoryManager.RemoveItem(item, 1))
            {
                args.Cancel = true;
                _pendingPlaceItem = null;
                return;
            }

            // Store reference so HandleBoardPlaced can track it
            _pendingPlaceItem = item;
        }

        private void HandleBoardPlaced(Vector3Int pos, BoardOrientation orient, GameObject board)
        {
            if (_pendingPlaceItem != null)
            {
                _placedItemTracker[board] = _pendingPlaceItem;
                _pendingPlaceItem = null;
            }
        }

        private void HandlePlaceFailed(Vector3Int pos, BoardOrientation orient)
        {
            // Refund the item that was consumed in HandleBeforePlace
            if (_pendingPlaceItem != null)
            {
                _inventoryManager.AddItem(_pendingPlaceItem, 1);
                _pendingPlaceItem = null;
            }
        }

        private void HandleBeforeRemove(Vector3Int pos, BoardOrientation orient, GameObject board)
        {
            if (_placedItemTracker.TryGetValue(board, out var item))
            {
                _inventoryManager.AddItem(item, 1);
                _placedItemTracker.Remove(board);
            }
        }
    }
}
