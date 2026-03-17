# Inventory System Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement a Minecraft-style inventory system (9-slot hotbar + 27-slot grid) integrated with the existing building system via a decoupled event-based bridge, with a reference-counted mouse manager.

**Architecture:** Three independent systems (Inventory, Building, MouseManager) connected by a thin `BuildingInventoryBridge` mediator. Item types are defined as ScriptableObjects. The inventory is a pure C# data model. The UI uses uGUI (Canvas, Image, TMP_Text). The building system gains events for placement/removal that the bridge subscribes to.

**Tech Stack:** Unity 2022+, C#, uGUI (Canvas/Image/TMP_Text), Unity Input System, ScriptableObjects

**Design doc:** `docs/plans/2026-03-16-inventory-system-design.md`

---

### Task 1: PlacementMode Enum

**Files:**
- Create: `Assets/Scripts/Inventory/PlacementMode.cs`

**Step 1: Create the enum**

```csharp
namespace Inventory
{
    public enum PlacementMode
    {
        Oriented,  // Uses existing X/Y/Z orientation (panels, ramps)
        FullCell   // Occupies entire cell, no orientation (blocks, pillars)
    }
}
```

**Step 2: Commit**

```bash
git add Assets/Scripts/Inventory/PlacementMode.cs
git commit -m "feat: add PlacementMode enum for oriented vs full-cell building items"
```

---

### Task 2: BuildingItemSO ScriptableObject

**Files:**
- Create: `Assets/Scripts/Inventory/BuildingItemSO.cs`

**Step 1: Create the ScriptableObject**

```csharp
using UnityEngine;

namespace Inventory
{
    [CreateAssetMenu(fileName = "NewBuildingItem", menuName = "Building/Building Item")]
    public class BuildingItemSO : ScriptableObject
    {
        public string itemName;
        public Sprite icon;
        public int maxStackSize = 64;
        public GameObject prefab;
        public PlacementMode placementMode;
    }
}
```

**Step 2: Commit**

```bash
git add Assets/Scripts/Inventory/BuildingItemSO.cs
git commit -m "feat: add BuildingItemSO ScriptableObject for item definitions"
```

---

### Task 3: ItemSlot Data Class

**Files:**
- Create: `Assets/Scripts/Inventory/ItemSlot.cs`

**Step 1: Create the class**

```csharp
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
```

**Step 2: Commit**

```bash
git add Assets/Scripts/Inventory/ItemSlot.cs
git commit -m "feat: add ItemSlot data class with stack operations"
```

---

### Task 4: Inventory Data Model

**Files:**
- Create: `Assets/Scripts/Inventory/Inventory.cs`

**Step 1: Create the class**

The `Inventory` class is a pure C# data model. No MonoBehaviour, no UI awareness.

```csharp
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
```

**Step 2: Commit**

```bash
git add Assets/Scripts/Inventory/Inventory.cs
git commit -m "feat: add Inventory data model with add/remove/count operations"
```

---

### Task 5: InventoryManager MonoBehaviour

**Files:**
- Create: `Assets/Scripts/Inventory/InventoryManager.cs`

**Step 1: Create the singleton manager**

```csharp
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
```

**Step 2: Commit**

```bash
git add Assets/Scripts/Inventory/InventoryManager.cs
git commit -m "feat: add InventoryManager singleton with starting item configuration"
```

---

### Task 6: MouseManager

**Files:**
- Create: `Assets/Scripts/UI/MouseManager.cs`

**Step 1: Create the mouse manager**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UI
{
    public class MouseManager : MonoBehaviour
    {
        public static MouseManager Instance { get; private set; }

        private HashSet<object> _unlockRequesters = new();

        public bool IsCursorFree => _unlockRequesters.Count > 0;
        public event Action<bool> OnCursorStateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            UpdateCursorState();
        }

        public void RequestUnlock(object requester)
        {
            if (_unlockRequesters.Add(requester))
            {
                UpdateCursorState();
            }
        }

        public void ReleaseLock(object requester)
        {
            if (_unlockRequesters.Remove(requester))
            {
                UpdateCursorState();
            }
        }

        private void UpdateCursorState()
        {
            bool isFree = IsCursorFree;
            Cursor.lockState = isFree ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = isFree;
            OnCursorStateChanged?.Invoke(isFree);
        }
    }
}
```

**Step 2: Commit**

```bash
git add Assets/Scripts/UI/MouseManager.cs
git commit -m "feat: add MouseManager with reference-counted cursor locking"
```

---

### Task 7: Extend BoardAdjacency for FullCell

**Files:**
- Modify: `Assets/Scripts/Building/BoardAdjacency.cs:79-88`

**Step 1: Add FullCell neighbor support**

The `GetNeighbors` method currently returns an empty array for any orientation other than X, Y, Z. We need to handle `Full` (which is `X | Y | Z = 7`) by returning the union of all three neighbor arrays, deduplicated.

Add a static field and modify `GetNeighbors`:

```csharp
// Add after line 77 (after YNeighbors closing brace), before GetNeighbors:
private static Neighbor[] _fullNeighbors;

private static Neighbor[] BuildFullNeighbors()
{
    var set = new System.Collections.Generic.HashSet<(int, int, int, BoardOrientation)>();
    var result = new System.Collections.Generic.List<Neighbor>();

    void AddUnique(Neighbor[] neighbors)
    {
        foreach (var n in neighbors)
        {
            var key = (n.Offset.x, n.Offset.y, n.Offset.z, n.Orientation);
            if (set.Add(key))
                result.Add(n);
        }
    }

    AddUnique(XNeighbors);
    AddUnique(YNeighbors);
    AddUnique(ZNeighbors);

    return result.ToArray();
}
```

Modify `GetNeighbors` (replacing lines 79-88):

```csharp
public static Neighbor[] GetNeighbors(BoardOrientation orient)
{
    if (orient == (BoardOrientation.X | BoardOrientation.Y | BoardOrientation.Z))
    {
        _fullNeighbors ??= BuildFullNeighbors();
        return _fullNeighbors;
    }

    return orient switch
    {
        BoardOrientation.X => XNeighbors,
        BoardOrientation.Y => YNeighbors,
        BoardOrientation.Z => ZNeighbors,
        _ => System.Array.Empty<Neighbor>()
    };
}
```

**Step 2: Commit**

```bash
git add Assets/Scripts/Building/BoardAdjacency.cs
git commit -m "feat: add FullCell neighbor support to BoardAdjacency"
```

---

### Task 8: Extend BuildingGrid for FullCell validation

**Files:**
- Modify: `Assets/Scripts/Building/BuildingGrid.cs:34-48`

**Step 1: Add FullCell validation to AddBoard**

The existing `AddBoard` checks `HasBoard(pos, orient)` to prevent duplicates. For FullCell placement, we also need to check that no individual panels exist at that position. And for individual panel placement, we need to check that no FullCell exists.

Add a constant and a helper:

```csharp
// Add after line 6 (inside class, before Instance property):
private const BoardOrientation FULL = BoardOrientation.X | BoardOrientation.Y | BoardOrientation.Z;
```

Modify `AddBoard` (replacing lines 34-48):

```csharp
public bool AddBoard(Vector3Int pos, BoardOrientation orient, GameObject boardObj)
{
    // Check if this exact orientation is already occupied
    if (HasBoard(pos, orient)) return false;

    // FullCell conflict checks
    if (_grid.TryGetValue(pos, out var existingFlags))
    {
        bool placingFull = orient == FULL;
        bool existingHasFull = existingFlags == FULL;

        // Can't place a panel where a FullCell exists
        if (existingHasFull && !placingFull) return false;
        // Can't place a FullCell where any panel exists
        if (placingFull && existingFlags != BoardOrientation.None) return false;
    }

    if (_grid.ContainsKey(pos))
        _grid[pos] |= orient;
    else
        _grid[pos] = orient;

    if (!_boardRegistry.ContainsKey(pos))
        _boardRegistry[pos] = new Dictionary<BoardOrientation, GameObject>();
    _boardRegistry[pos][orient] = boardObj;

    OnBoardAdded?.Invoke(pos, orient);
    return true;
}
```

**Step 2: Commit**

```bash
git add Assets/Scripts/Building/BuildingGrid.cs
git commit -m "feat: add FullCell conflict validation to BuildingGrid.AddBoard"
```

---

### Task 9: Extend BuildingController with Events and ActiveItem

**Files:**
- Modify: `Assets/Scripts/Building/BuildingController.cs`

This is the largest modification. The controller needs:
1. `ActivePrefab` and `ActivePlacementMode` settable properties (set by bridge)
2. `OnBeforePlace` event (lets bridge veto placement)
3. `OnBoardPlaced` event (lets bridge track placed items)
4. FullCell placement support
5. Disable placement when `ActivePrefab` is null
6. Disable placement/look when `MouseManager.IsCursorFree`
7. Hotbar slot switching via number keys and scroll wheel

**Step 1: Rewrite BuildingController.cs**

Replace the entire file content:

```csharp
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Building
{
    public class BuildingController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private BoardPreview boardPreview;

        [Header("Settings")]
        [SerializeField] private float maxPlacementDistance = 16f;
        [SerializeField] private float firstBoardDistance = 8f;
        [SerializeField] private LayerMask placementTriggerLayer;
        [SerializeField] private LayerMask boardLayer;

        private const BoardOrientation FULL = BoardOrientation.X | BoardOrientation.Y | BoardOrientation.Z;

        private BuildingGrid _grid;
        private InputAction _attackAction;
        private InputAction _removeAction;
        private InputAction _previousAction;
        private InputAction _nextAction;

        // Current target state
        private bool _hasTarget;
        private Vector3Int _targetPos;
        private BoardOrientation _targetOrient;

        // Active item state (set externally by bridge)
        private GameObject _activePrefab;
        private Inventory.PlacementMode _activePlacementMode;
        private bool _inputEnabled = true;

        /// <summary>Set by BuildingInventoryBridge when the active hotbar item changes.</summary>
        public GameObject ActivePrefab
        {
            get => _activePrefab;
            set
            {
                _activePrefab = value;
                if (boardPreview != null)
                    boardPreview.SetPreviewPrefab(value);
            }
        }

        /// <summary>Set by BuildingInventoryBridge when the active hotbar item changes.</summary>
        public Inventory.PlacementMode ActivePlacementMode
        {
            get => _activePlacementMode;
            set => _activePlacementMode = value;
        }

        // Events for decoupled integration
        public event Action<PlaceCancelEventArgs> OnBeforePlace;
        public event Action<Vector3Int, BoardOrientation, GameObject> OnBoardPlaced;
        public event Action<Vector3Int, BoardOrientation, GameObject> OnBeforeRemove;

        private void Awake()
        {
            var playerInput = GetComponent<PlayerInput>();
            _attackAction = playerInput.actions["Attack"];
            _removeAction = playerInput.actions["Remove"];
            _previousAction = playerInput.actions["Previous"];
            _nextAction = playerInput.actions["Next"];
        }

        private void Start()
        {
            _grid = BuildingGrid.Instance;
            if (_grid == null)
                Debug.LogError("[BuildingController] BuildingGrid.Instance not found.");

            // Subscribe to MouseManager for input toggling
            if (UI.MouseManager.Instance != null)
                UI.MouseManager.Instance.OnCursorStateChanged += HandleCursorStateChanged;
        }

        private void OnDestroy()
        {
            if (UI.MouseManager.Instance != null)
                UI.MouseManager.Instance.OnCursorStateChanged -= HandleCursorStateChanged;
        }

        private void HandleCursorStateChanged(bool isCursorFree)
        {
            _inputEnabled = !isCursorFree;
            if (!_inputEnabled)
            {
                _hasTarget = false;
                boardPreview.Hide();
            }
        }

        private void Update()
        {
            if (_grid == null) return;

            // Handle hotbar switching (always active)
            HandleHotbarInput();

            if (!_inputEnabled) return;

            _hasTarget = false;

            // Only target for placement if we have an active prefab
            if (_activePrefab != null)
            {
                if (_grid.IsEmpty())
                {
                    HandleFirstBoardTargeting();
                }
                else
                {
                    HandleNormalTargeting();
                }
            }

            // Update preview
            if (_hasTarget)
            {
                Vector3 worldPos = PlacementTriggerManager.GridToWorld(_targetPos, _targetOrient);
                Quaternion rotation = PlacementTriggerManager.GetBoardRotation(_targetOrient);
                boardPreview.ShowAt(worldPos, rotation);
            }
            else
            {
                boardPreview.Hide();
            }

            // Handle placement
            if (_hasTarget && _attackAction.WasPressedThisFrame())
            {
                PlaceBoard(_targetPos, _targetOrient);
            }

            // Handle removal (works even without active prefab)
            if (_removeAction.WasPressedThisFrame())
            {
                HandleRemoval();
            }
        }

        private void HandleHotbarInput()
        {
            // Number keys 1-9
            for (int i = 0; i < 9; i++)
            {
                if (Keyboard.current != null && Keyboard.current[Key.Digit1 + i].wasPressedThisFrame)
                {
                    Inventory.InventoryManager.Instance?.SetActiveSlot(i);
                    return;
                }
            }

            // Scroll wheel
            if (Mouse.current != null)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (scroll > 0 && _previousAction.WasPressedThisFrame())
                {
                    // Handled via Previous action
                }
                else if (scroll < 0 && _nextAction.WasPressedThisFrame())
                {
                    // Handled via Next action
                }
            }

            // Previous/Next actions (keyboard 1/2 or gamepad dpad)
            if (_previousAction.WasPressedThisFrame())
            {
                var mgr = Inventory.InventoryManager.Instance;
                if (mgr != null)
                {
                    int current = mgr.Inventory.ActiveHotbarIndex;
                    mgr.SetActiveSlot((current - 1 + Inventory.Inventory.HOTBAR_SIZE) % Inventory.Inventory.HOTBAR_SIZE);
                }
            }
            if (_nextAction.WasPressedThisFrame())
            {
                var mgr = Inventory.InventoryManager.Instance;
                if (mgr != null)
                {
                    int current = mgr.Inventory.ActiveHotbarIndex;
                    mgr.SetActiveSlot((current + 1) % Inventory.Inventory.HOTBAR_SIZE);
                }
            }
        }

        private void HandleFirstBoardTargeting()
        {
            Vector3 targetWorld = cameraTransform.position + cameraTransform.forward * firstBoardDistance;
            Vector3Int gridPos = WorldToGrid(targetWorld);

            BoardOrientation orient;
            if (_activePlacementMode == Inventory.PlacementMode.FullCell)
                orient = FULL;
            else
                orient = GetOrientationFromCamera();

            _hasTarget = true;
            _targetPos = gridPos;
            _targetOrient = orient;
        }

        private void HandleNormalTargeting()
        {
            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, maxPlacementDistance, placementTriggerLayer))
            {
                TriggerInfo info = hit.collider.GetComponent<TriggerInfo>();
                if (info != null)
                {
                    _hasTarget = true;
                    _targetPos = info.GridPosition;

                    if (_activePlacementMode == Inventory.PlacementMode.FullCell)
                        _targetOrient = FULL;
                    else
                        _targetOrient = info.Orientation;
                }
            }
        }

        private void PlaceBoard(Vector3Int pos, BoardOrientation orient)
        {
            if (_activePrefab == null)
            {
                Debug.LogError("[BuildingController] No active prefab assigned.");
                return;
            }

            // Fire OnBeforePlace to allow cancellation (e.g., inventory check)
            var args = new PlaceCancelEventArgs(pos, orient);
            OnBeforePlace?.Invoke(args);
            if (args.Cancel) return;

            Vector3 worldPos = PlacementTriggerManager.GridToWorld(pos, orient);
            Quaternion rotation = PlacementTriggerManager.GetBoardRotation(orient);

            GameObject board = Instantiate(_activePrefab, worldPos, rotation);
            board.name = $"Board_{pos}_{orient}";

            if (_grid.AddBoard(pos, orient, board))
            {
                OnBoardPlaced?.Invoke(pos, orient, board);
            }
            else
            {
                Destroy(board);
            }
        }

        private void HandleRemoval()
        {
            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, maxPlacementDistance, boardLayer))
            {
                Vector3Int gridPos = WorldToGrid(hit.point);

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            Vector3Int checkPos = gridPos + new Vector3Int(dx, dy, dz);
                            foreach (BoardOrientation orient in new[] { BoardOrientation.X, BoardOrientation.Y, BoardOrientation.Z, FULL })
                            {
                                GameObject board = _grid.GetBoard(checkPos, orient);
                                if (board != null && board == hit.collider.gameObject)
                                {
                                    OnBeforeRemove?.Invoke(checkPos, orient, board);
                                    _grid.RemoveBoard(checkPos, orient);
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }

        private BoardOrientation GetOrientationFromCamera()
        {
            Vector3 forward = cameraTransform.forward;
            float absX = Mathf.Abs(forward.x);
            float absY = Mathf.Abs(forward.y);
            float absZ = Mathf.Abs(forward.z);

            if (absZ >= absX && absZ >= absY) return BoardOrientation.X;
            if (absX >= absY && absX >= absZ) return BoardOrientation.Y;
            return BoardOrientation.Z;
        }

        private static Vector3Int WorldToGrid(Vector3 worldPos)
        {
            const float CELL_SIZE = 4f;
            return new Vector3Int(
                Mathf.FloorToInt(worldPos.x / CELL_SIZE),
                Mathf.FloorToInt(worldPos.y / CELL_SIZE),
                Mathf.FloorToInt(worldPos.z / CELL_SIZE)
            );
        }
    }

    public class PlaceCancelEventArgs : EventArgs
    {
        public Vector3Int Position { get; }
        public BoardOrientation Orientation { get; }
        public bool Cancel { get; set; }

        public PlaceCancelEventArgs(Vector3Int pos, BoardOrientation orient)
        {
            Position = pos;
            Orientation = orient;
        }
    }
}
```

**Step 2: Commit**

```bash
git add Assets/Scripts/Building/BuildingController.cs
git commit -m "feat: extend BuildingController with events, ActivePrefab, FullCell, and input gating"
```

---

### Task 10: Extend BoardPreview for Dynamic Prefab

**Files:**
- Modify: `Assets/Scripts/Building/BoardPreview.cs`

**Step 1: Rewrite BoardPreview to support dynamic prefab switching**

Replace the entire file:

```csharp
using UnityEngine;

namespace Building
{
    public class BoardPreview : MonoBehaviour
    {
        [SerializeField] private Material previewMaterial;

        private GameObject _previewObj;

        private void Awake()
        {
            // Start with default cube preview
            CreateDefaultPreview();
        }

        private void CreateDefaultPreview()
        {
            if (_previewObj != null)
            {
                Destroy(_previewObj);
            }

            _previewObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _previewObj.name = "BoardPreview";
            _previewObj.transform.SetParent(transform);
            _previewObj.transform.localScale = new Vector3(4f, 0.1f, 4f);

            // Remove collider so it doesn't interfere with raycasts
            var collider = _previewObj.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            ApplyPreviewMaterial(_previewObj);
            _previewObj.SetActive(false);
        }

        /// <summary>
        /// Switch the preview to match the given prefab's mesh and scale.
        /// If prefab is null, hides the preview.
        /// </summary>
        public void SetPreviewPrefab(GameObject prefab)
        {
            if (_previewObj != null)
            {
                Destroy(_previewObj);
                _previewObj = null;
            }

            if (prefab == null) return;

            _previewObj = Instantiate(prefab);
            _previewObj.name = "BoardPreview";
            _previewObj.transform.SetParent(transform);

            // Remove all colliders so the preview doesn't interfere with raycasts
            foreach (var col in _previewObj.GetComponentsInChildren<Collider>())
            {
                Destroy(col);
            }

            // Apply preview material to all renderers
            ApplyPreviewMaterial(_previewObj);

            // Remove any non-visual components (Rigidbody, scripts, etc.)
            foreach (var rb in _previewObj.GetComponentsInChildren<Rigidbody>())
                Destroy(rb);

            // Set layer to Default so it doesn't interact with building raycasts
            SetLayerRecursive(_previewObj, 0);

            _previewObj.SetActive(false);
        }

        private void ApplyPreviewMaterial(GameObject obj)
        {
            if (previewMaterial == null) return;
            foreach (var renderer in obj.GetComponentsInChildren<MeshRenderer>())
            {
                var materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = previewMaterial;
                renderer.materials = materials;
            }
        }

        private void SetLayerRecursive(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        public void ShowAt(Vector3 position, Quaternion rotation)
        {
            if (_previewObj == null) return;
            _previewObj.transform.position = position;
            _previewObj.transform.rotation = rotation;
            _previewObj.SetActive(true);
        }

        public void Hide()
        {
            if (_previewObj != null)
                _previewObj.SetActive(false);
        }
    }
}
```

**Step 2: Commit**

```bash
git add Assets/Scripts/Building/BoardPreview.cs
git commit -m "feat: extend BoardPreview with dynamic prefab switching"
```

---

### Task 11: Extend PlacementTriggerManager for FullCell

**Files:**
- Modify: `Assets/Scripts/Building/PlacementTriggerManager.cs:43-60`

**Step 1: Update HandleBoardAdded for FullCell**

When a FullCell board is added, we need to remove all triggers at that position (all orientations) and generate triggers from the full neighbor union.

Replace `HandleBoardAdded` (lines 43-59):

```csharp
private void HandleBoardAdded(Vector3Int pos, BoardOrientation orient)
{
    bool isFull = orient == (BoardOrientation.X | BoardOrientation.Y | BoardOrientation.Z);

    if (isFull)
    {
        // Remove all triggers at this position
        RemoveTrigger(pos, BoardOrientation.X);
        RemoveTrigger(pos, BoardOrientation.Y);
        RemoveTrigger(pos, BoardOrientation.Z);
    }
    else
    {
        RemoveTrigger(pos, orient);
    }

    // Generate triggers at each valid empty neighbor
    var neighbors = BoardAdjacency.GetNeighbors(orient);
    foreach (var n in neighbors)
    {
        Vector3Int neighborPos = pos + n.Offset;
        BoardOrientation neighborOrient = n.Orientation;

        if (!_grid.HasBoard(neighborPos, neighborOrient))
        {
            CreateTrigger(neighborPos, neighborOrient);
        }
    }
}
```

**Step 2: Update HandleBoardRemoved for FullCell**

Replace `HandleBoardRemoved` (lines 62-72):

```csharp
private void HandleBoardRemoved(Vector3Int pos, BoardOrientation orient)
{
    bool isFull = orient == (BoardOrientation.X | BoardOrientation.Y | BoardOrientation.Z);

    if (isFull)
    {
        // Check each individual orientation for neighbors and create triggers
        foreach (var o in new[] { BoardOrientation.X, BoardOrientation.Y, BoardOrientation.Z })
        {
            if (_grid.HasAnyNeighbor(pos, o))
            {
                CreateTrigger(pos, o);
            }
        }
    }
    else
    {
        if (_grid.HasAnyNeighbor(pos, orient))
        {
            CreateTrigger(pos, orient);
        }
    }

    CleanupOrphanedTriggers();
}
```

**Step 3: Commit**

```bash
git add Assets/Scripts/Building/PlacementTriggerManager.cs
git commit -m "feat: extend PlacementTriggerManager for FullCell board handling"
```

---

### Task 12: BuildingInventoryBridge

**Files:**
- Create: `Assets/Scripts/Building/BuildingInventoryBridge.cs`

**Step 1: Create the bridge**

```csharp
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

            // Subscribe to inventory changes
            _inventoryManager.OnActiveSlotChanged += HandleActiveSlotChanged;
            _inventoryManager.OnChanged += HandleInventoryChanged;

            // Subscribe to building events
            _buildingController.OnBeforePlace += HandleBeforePlace;
            _buildingController.OnBoardPlaced += HandleBoardPlaced;
            _buildingController.OnBeforeRemove += HandleBeforeRemove;

            // Initialize active item
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
                return;
            }

            // Try to consume the item
            if (!_inventoryManager.RemoveItem(item, 1))
            {
                args.Cancel = true;
            }
        }

        private void HandleBoardPlaced(Vector3Int pos, BoardOrientation orient, GameObject board)
        {
            var item = _inventoryManager.GetActiveItem();
            // The item was already consumed in HandleBeforePlace, but we need to track
            // what was placed. Since RemoveItem already happened, we need to get the item
            // reference from before removal. Store it based on the active slot's item type.
            // Edge case: the active item might have changed between Remove and this event,
            // but since this all happens in the same frame, it's safe.

            // We need to look up what item type is in the active slot.
            // Since we already removed 1, the slot might be empty now.
            // We should track the item reference before removal.
            // Fix: store the pending item in HandleBeforePlace.
            if (_pendingPlaceItem != null)
            {
                _placedItemTracker[board] = _pendingPlaceItem;
                _pendingPlaceItem = null;
            }
        }

        private Inventory.BuildingItemSO _pendingPlaceItem;

        // Revised HandleBeforePlace to store pending item:
        // (This replaces the HandleBeforePlace above — see note)

        private void HandleBeforeRemove(Vector3Int pos, BoardOrientation orient, GameObject board)
        {
            if (_placedItemTracker.TryGetValue(board, out var item))
            {
                // Return item to inventory. If full, item is lost.
                _inventoryManager.AddItem(item, 1);
                _placedItemTracker.Remove(board);
            }
        }
    }
}
```

Wait — the code above has a structural issue with `HandleBeforePlace` and `_pendingPlaceItem`. Let me fix this properly. The bridge needs to remember which item is being placed before the placement happens. Here is the corrected version:

```csharp
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
```

**Step 2: Commit**

```bash
git add Assets/Scripts/Building/BuildingInventoryBridge.cs
git commit -m "feat: add BuildingInventoryBridge mediator for inventory-building integration"
```

---

### Task 13: Modify FirstPersonController to respect MouseManager

**Files:**
- Modify: `Assets/Scripts/Player/FirstPersonController.cs`

**Step 1: Add MouseManager integration**

The controller currently locks the cursor in `Start()` and always handles look input. We need it to:
- Remove the cursor lock in `Start()` (MouseManager handles it now)
- Skip look input when `MouseManager.IsCursorFree`

Remove cursor lock from `Start()` (line 38-39):

```csharp
// Replace Start() with:
private void Start()
{
    // Cursor state is now managed by MouseManager
}
```

Add a check in `HandleLook()` — replace lines 48-57:

```csharp
private void HandleLook()
{
    // Don't process look when cursor is free (UI is open)
    if (UI.MouseManager.Instance != null && UI.MouseManager.Instance.IsCursorFree) return;

    Vector2 lookDelta = _lookAction.ReadValue<Vector2>();

    _cameraPitch -= lookDelta.y * mouseSensitivity;
    _cameraPitch = Mathf.Clamp(_cameraPitch, -90f, 90f);

    cameraTransform.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
    transform.Rotate(Vector3.up * lookDelta.x * mouseSensitivity);
}
```

**Step 2: Commit**

```bash
git add Assets/Scripts/Player/FirstPersonController.cs
git commit -m "feat: integrate FirstPersonController with MouseManager for cursor gating"
```

---

### Task 14: SlotUI Component

**Files:**
- Create: `Assets/Scripts/UI/SlotUI.cs`

**Step 1: Create the slot UI component**

Each slot displays an item icon, count text, and a highlight border. It handles click events for the inventory interaction state machine.

```csharp
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace UI
{
    public class SlotUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image _iconImage;
        [SerializeField] private TMP_Text _countText;
        [SerializeField] private Image _highlightBorder;
        [SerializeField] private Image _background;

        [Header("Colors")]
        [SerializeField] private Color _normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        [SerializeField] private Color _highlightColor = new Color(1f, 1f, 1f, 0.5f);

        private Inventory.ItemSlot _slot;
        private int _slotIndex;
        private bool _isHotbar;

        public event Action<SlotUI, PointerEventData.InputButton> OnSlotClicked;

        public Inventory.ItemSlot Slot => _slot;
        public int SlotIndex => _slotIndex;
        public bool IsHotbar => _isHotbar;

        public void Initialize(Inventory.ItemSlot slot, int index, bool isHotbar)
        {
            _slot = slot;
            _slotIndex = index;
            _isHotbar = isHotbar;
            Refresh();
        }

        public void Refresh()
        {
            if (_slot == null || _slot.IsEmpty)
            {
                _iconImage.enabled = false;
                _countText.text = "";
            }
            else
            {
                _iconImage.enabled = true;
                _iconImage.sprite = _slot.Item.icon;
                _countText.text = _slot.Count > 1 ? _slot.Count.ToString() : "";
            }
        }

        public void SetHighlight(bool active)
        {
            if (_highlightBorder != null)
                _highlightBorder.enabled = active;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            OnSlotClicked?.Invoke(this, eventData.button);
        }
    }
}
```

**Step 2: Commit**

```bash
git add Assets/Scripts/UI/SlotUI.cs
git commit -m "feat: add SlotUI component for inventory slot display and click handling"
```

---

### Task 15: CursorItem Component

**Files:**
- Create: `Assets/Scripts/UI/CursorItem.cs`

**Step 1: Create the cursor item component**

Renders the held item icon at the mouse position when dragging items.

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UI
{
    public class CursorItem : MonoBehaviour
    {
        [SerializeField] private Image _iconImage;
        [SerializeField] private TMP_Text _countText;
        [SerializeField] private Canvas _parentCanvas;

        private Inventory.BuildingItemSO _heldItem;
        private int _heldCount;
        private RectTransform _rectTransform;

        public Inventory.BuildingItemSO HeldItem => _heldItem;
        public int HeldCount => _heldCount;
        public bool IsHolding => _heldItem != null && _heldCount > 0;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            Hide();
        }

        private void Update()
        {
            if (!IsHolding) return;

            // Follow mouse position
            Vector2 mousePos = Input.mousePosition;
            if (_parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                _rectTransform.position = mousePos;
            }
            else
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _parentCanvas.transform as RectTransform,
                    mousePos,
                    _parentCanvas.worldCamera,
                    out Vector2 localPoint);
                _rectTransform.localPosition = localPoint;
            }
        }

        public void Show(Inventory.BuildingItemSO item, int count)
        {
            _heldItem = item;
            _heldCount = count;

            _iconImage.enabled = true;
            _iconImage.sprite = item.icon;
            _countText.text = count > 1 ? count.ToString() : "";
            gameObject.SetActive(true);
        }

        public void UpdateCount(int count)
        {
            _heldCount = count;
            _countText.text = count > 1 ? count.ToString() : "";
            if (count <= 0) Hide();
        }

        public void Hide()
        {
            _heldItem = null;
            _heldCount = 0;
            _iconImage.enabled = false;
            _countText.text = "";
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Take the held item data and clear it. Returns (item, count).
        /// </summary>
        public (Inventory.BuildingItemSO item, int count) Take()
        {
            var result = (_heldItem, _heldCount);
            Hide();
            return result;
        }
    }
}
```

**Step 2: Commit**

```bash
git add Assets/Scripts/UI/CursorItem.cs
git commit -m "feat: add CursorItem component for held-item cursor display"
```

---

### Task 16: HotbarUI Component

**Files:**
- Create: `Assets/Scripts/UI/HotbarUI.cs`

**Step 1: Create the hotbar UI**

Always-visible bottom-of-screen bar with 9 slots and a selection highlight.

```csharp
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
```

**Step 2: Commit**

```bash
git add Assets/Scripts/UI/HotbarUI.cs
git commit -m "feat: add HotbarUI with slot display and selection highlight"
```

---

### Task 17: InventoryPanelUI Component

**Files:**
- Create: `Assets/Scripts/UI/InventoryPanelUI.cs`

**Step 1: Create the inventory panel UI**

Toggleable full inventory panel with Minecraft-style slot interaction.

```csharp
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
```

**Step 2: Commit**

```bash
git add Assets/Scripts/UI/InventoryPanelUI.cs
git commit -m "feat: add InventoryPanelUI with Minecraft-style drag/swap interaction"
```

---

### Task 18: Add Inventory Toggle to Input System

**Files:**
- Modify: `Assets/InputSystem_Actions.inputactions`

**Step 1: Add Inventory action**

Add a new action to the Player map for toggling inventory. Bind to `Tab` key.

Open the file in Unity's Input Actions editor or add the JSON entries:
- Action: `Inventory`, Type: Button
- Binding: `<Keyboard>/tab`

Note: This step might be easier done in the Unity editor. If editing JSON directly, add the action and binding entries to the Player action map following the existing pattern.

**Step 2: Commit**

```bash
git add Assets/InputSystem_Actions.inputactions
git commit -m "feat: add Inventory toggle action to input system"
```

---

### Task 19: Scene Setup and Integration Testing

**Files:**
- Scene work (Unity editor)

**Step 1: Add required GameObjects to scene**

In the building system scene, create the following GameObjects:

1. **InventoryManager** — Empty GameObject with `InventoryManager` component. Configure starting items in inspector (e.g., 64x Board item).

2. **MouseManager** — Empty GameObject with `MouseManager` component.

3. **BuildingInventoryBridge** — Empty GameObject with `BuildingInventoryBridge` component. Assign the `BuildingController` reference in inspector.

4. **InventoryCanvas** — Canvas (Screen Space - Overlay) with:
   - **HotbarUI** child with `HotbarUI` component and 9 `SlotUI` children
   - **InventoryPanel** child with `InventoryPanelUI` component, 27 grid `SlotUI` children, 9 hotbar mirror `SlotUI` children, and `CursorItem`

5. **Board ScriptableObject** — Create `Assets/Items/Board.asset` via Create > Building > Building Item. Assign the Board.prefab, set name to "Board", stack size 64, placement mode Oriented.

6. Remove the direct `boardPrefab` serialized field reference from `BuildingController` (it's now set dynamically via `ActivePrefab`).

**Step 2: Create Board item asset**

In Unity: Right-click `Assets/Items/` > Create > Building > Building Item
- itemName: "Board"
- icon: (create or import a board icon sprite)
- maxStackSize: 64
- prefab: Board.prefab
- placementMode: Oriented

**Step 3: Test the flow**

1. Enter play mode
2. Verify hotbar shows 64x Board in first slot
3. Place boards — count should decrease
4. Remove boards — count should increase
5. Press Tab — inventory panel opens, cursor unlocks, can't look around
6. Press Tab again — closes, cursor locks, gameplay resumes
7. Drag items between slots in inventory

**Step 4: Commit**

```bash
git add -A
git commit -m "feat: scene setup and integration for inventory system"
```

---

### Task 20: Remove boardPrefab serialized field from BuildingController

**Files:**
- Modify: `Assets/Scripts/Building/BuildingController.cs`

**Step 1: Clean up**

Remove the `[SerializeField] private GameObject boardPrefab;` field from `BuildingController` since the prefab is now set dynamically via `ActivePrefab` from the bridge. This was already done in Task 9's rewrite — verify it's not present.

**Step 2: Verify no compilation errors**

Open Unity and check the Console for any errors. Fix any remaining issues.

**Step 3: Final commit**

```bash
git add -A
git commit -m "chore: clean up obsolete boardPrefab field from BuildingController"
```
