# Small Block Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add small blocks (single-cell, 2x2x2 world units) that are placed via direct raycast against existing geometry with Minecraft-style rotation, and refactor the grid to a unified occupant model to allow co-existence with boards.

**Architecture:** Replace BuildingGrid's per-cell orientation flags with a per-cell list of IOccupant objects. Board and SmallBlock both implement IOccupant. BuildingController gets a new targeting path for SmallBlock placement mode that raycasts against the Board layer directly (no placement triggers). A SmallBlockRotation utility computes one of 24 discrete rotations from hit normal and player position.

**Tech Stack:** Unity 6, C#, New Input System, URP

---

### Task 1: Create IOccupant Interface and OccupantType Enum

**Files:**
- Create: `Assets/Scripts/Building/IOccupant.cs`

**Step 1: Create the IOccupant interface file**

```csharp
using UnityEngine;

namespace Building
{
    public enum OccupantType
    {
        Board,
        SmallBlock
    }

    public interface IOccupant
    {
        OccupantType Type { get; }
        Vector3Int Anchor { get; }
        GameObject GameObject { get; }
        Vector3Int[] GetOccupiedCells();
    }
}
```

**Step 2: Commit**

```bash
git add Assets/Scripts/Building/IOccupant.cs
git commit -m "feat(building): add IOccupant interface and OccupantType enum"
```

---

### Task 2: Create BoardOccupant

**Files:**
- Create: `Assets/Scripts/Building/BoardOccupant.cs`

**Step 1: Create the BoardOccupant class**

This wraps existing board data into the IOccupant interface. It is NOT a MonoBehaviour — it's a plain C# class that holds references.

```csharp
using UnityEngine;

namespace Building
{
    public class BoardOccupant : IOccupant
    {
        public OccupantType Type => OccupantType.Board;
        public Vector3Int Anchor { get; }
        public GameObject GameObject { get; }
        public BoardOrientation Orientation { get; }

        public BoardOccupant(Vector3Int anchor, BoardOrientation orientation, GameObject gameObject)
        {
            Anchor = anchor;
            Orientation = orientation;
            GameObject = gameObject;
        }

        public Vector3Int[] GetOccupiedCells()
        {
            return GridConstants.GetOccupiedCells(Anchor, Orientation);
        }
    }
}
```

**Step 2: Commit**

```bash
git add Assets/Scripts/Building/BoardOccupant.cs
git commit -m "feat(building): add BoardOccupant implementing IOccupant"
```

---

### Task 3: Create SmallBlockOccupant

**Files:**
- Create: `Assets/Scripts/Building/SmallBlockOccupant.cs`

**Step 1: Create the SmallBlockOccupant class**

Single-cell occupant with a rotation quaternion.

```csharp
using UnityEngine;

namespace Building
{
    public class SmallBlockOccupant : IOccupant
    {
        public OccupantType Type => OccupantType.SmallBlock;
        public Vector3Int Anchor { get; }
        public GameObject GameObject { get; }
        public Quaternion Rotation { get; }

        public SmallBlockOccupant(Vector3Int position, Quaternion rotation, GameObject gameObject)
        {
            Anchor = position;
            Rotation = rotation;
            GameObject = gameObject;
        }

        public Vector3Int[] GetOccupiedCells()
        {
            return new Vector3Int[] { Anchor };
        }
    }
}
```

**Step 2: Commit**

```bash
git add Assets/Scripts/Building/SmallBlockOccupant.cs
git commit -m "feat(building): add SmallBlockOccupant implementing IOccupant"
```

---

### Task 4: Add SmallBlock to PlacementMode enum

**Files:**
- Modify: `Assets/Scripts/Inventory/PlacementMode.cs:1-8`

**Step 1: Add the SmallBlock value**

Replace the full file content:

```csharp
namespace Inventory
{
    public enum PlacementMode
    {
        Oriented,   // Uses existing X/Y/Z orientation (panels, ramps)
        FullCell,   // Occupies entire cell, no orientation (blocks, pillars)
        SmallBlock  // Single-cell block placed via direct raycast
    }
}
```

**Step 2: Commit**

```bash
git add Assets/Scripts/Inventory/PlacementMode.cs
git commit -m "feat(inventory): add SmallBlock placement mode"
```

---

### Task 5: Add SmallBlockGridToWorld to GridConstants

**Files:**
- Modify: `Assets/Scripts/Building/GridConstants.cs:1-66`

**Step 1: Add the helper method**

Add after the `GetOccupiedCells` method (after line 64, before the closing brace on line 65):

```csharp
        /// <summary>
        /// Convert a small block's single-cell grid position to its world-space center.
        /// Small blocks occupy 1 cell = CELL_SIZE^3 world units.
        /// </summary>
        public static UnityEngine.Vector3 SmallBlockGridToWorld(UnityEngine.Vector3Int cell)
        {
            float half = CELL_SIZE / 2f;
            return new UnityEngine.Vector3(
                cell.x * CELL_SIZE + half,
                cell.y * CELL_SIZE + half,
                cell.z * CELL_SIZE + half
            );
        }
```

**Step 2: Commit**

```bash
git add Assets/Scripts/Building/GridConstants.cs
git commit -m "feat(building): add SmallBlockGridToWorld helper to GridConstants"
```

---

### Task 6: Create SmallBlockRotation utility

**Files:**
- Create: `Assets/Scripts/Building/SmallBlockRotation.cs`

**Step 1: Create the rotation utility**

```csharp
using UnityEngine;

namespace Building
{
    /// <summary>
    /// Computes one of 24 discrete rotations for small blocks.
    /// Bottom face (-Y local) points into the raycasted surface.
    /// Front face (+Z local) points toward the player.
    /// </summary>
    public static class SmallBlockRotation
    {
        private static readonly Vector3[] CardinalAxes =
        {
            Vector3.right,   // +X
            Vector3.left,    // -X
            Vector3.up,      // +Y
            Vector3.down,    // -Y
            Vector3.forward, // +Z
            Vector3.back     // -Z
        };

        /// <summary>
        /// Compute the rotation for a small block given the hit surface normal,
        /// player position, and the block's world-space center.
        /// </summary>
        public static Quaternion ComputeRotation(
            Vector3 hitNormal, Vector3 playerPosition, Vector3 blockWorldPosition)
        {
            // Bottom face points into the surface (opposite of normal)
            Vector3 bottomDir = SnapToAxis(-hitNormal);
            Vector3 upDir = -bottomDir;

            // Front face points toward the player, projected onto the plane
            // perpendicular to the bottom direction
            Vector3 toPlayer = (playerPosition - blockWorldPosition).normalized;
            Vector3 frontDir = ProjectAndSnapToPlane(toPlayer, upDir);

            // Fallback: if toPlayer is parallel to upDir, pick an arbitrary front
            if (frontDir.sqrMagnitude < 0.01f)
            {
                frontDir = GetArbitraryPerpendicular(upDir);
            }

            return Quaternion.LookRotation(frontDir, upDir);
        }

        /// <summary>
        /// Snap a direction vector to the nearest cardinal axis.
        /// </summary>
        public static Vector3 SnapToAxis(Vector3 dir)
        {
            Vector3 best = CardinalAxes[0];
            float bestDot = Vector3.Dot(dir, best);

            for (int i = 1; i < CardinalAxes.Length; i++)
            {
                float dot = Vector3.Dot(dir, CardinalAxes[i]);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = CardinalAxes[i];
                }
            }

            return best;
        }

        /// <summary>
        /// Project a direction onto the plane defined by planeNormal,
        /// then snap the result to the nearest cardinal axis in that plane.
        /// </summary>
        public static Vector3 ProjectAndSnapToPlane(Vector3 dir, Vector3 planeNormal)
        {
            // Project onto plane
            Vector3 projected = dir - Vector3.Dot(dir, planeNormal) * planeNormal;

            if (projected.sqrMagnitude < 0.001f)
                return Vector3.zero;

            projected.Normalize();

            // Snap to nearest cardinal axis that lies in the plane
            Vector3 best = Vector3.zero;
            float bestDot = -2f;

            for (int i = 0; i < CardinalAxes.Length; i++)
            {
                // Skip axes that are parallel to the plane normal
                if (Mathf.Abs(Vector3.Dot(CardinalAxes[i], planeNormal)) > 0.9f)
                    continue;

                float dot = Vector3.Dot(projected, CardinalAxes[i]);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = CardinalAxes[i];
                }
            }

            return best;
        }

        private static Vector3 GetArbitraryPerpendicular(Vector3 normal)
        {
            // Pick the axis least parallel to normal
            Vector3 candidate = Mathf.Abs(Vector3.Dot(normal, Vector3.right)) < 0.9f
                ? Vector3.right
                : Vector3.forward;

            return SnapToAxis(Vector3.Cross(normal, candidate));
        }
    }
}
```

**Step 2: Commit**

```bash
git add Assets/Scripts/Building/SmallBlockRotation.cs
git commit -m "feat(building): add SmallBlockRotation utility for 24 discrete orientations"
```

---

### Task 7: Refactor BuildingGrid to IOccupant Model

This is the largest task. Replace the three dictionaries with the occupant model while preserving all existing behavior.

**Files:**
- Modify: `Assets/Scripts/Building/BuildingGrid.cs:1-177` (full rewrite)

**Step 1: Rewrite BuildingGrid**

Replace the entire file with:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Building
{
    public class BuildingGrid : MonoBehaviour
    {
        public static BuildingGrid Instance { get; private set; }

        public event Action<IOccupant> OnOccupantAdded;
        public event Action<IOccupant> OnOccupantRemoved;

        // Per-cell list of occupants (a cell can have multiple: e.g., board + small block)
        private Dictionary<Vector3Int, List<IOccupant>> _cellOccupants = new();

        // All unique occupants for iteration and count
        private HashSet<IOccupant> _occupantRegistry = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public bool IsEmpty() => _occupantRegistry.Count == 0;

        /// <summary>
        /// Returns true if the given cell contains any occupant of the specified type.
        /// </summary>
        public bool HasOccupant(Vector3Int cell, OccupantType type)
        {
            if (!_cellOccupants.TryGetValue(cell, out var list)) return false;
            foreach (var occ in list)
            {
                if (occ.Type == type) return true;
            }
            return false;
        }

        /// <summary>
        /// Returns true if the given cell contains a board with the specified orientation.
        /// Equivalent to the old HasBoard(pos, orient) for board-specific queries.
        /// </summary>
        public bool HasBoard(Vector3Int cell, BoardOrientation orient)
        {
            if (!_cellOccupants.TryGetValue(cell, out var list)) return false;
            foreach (var occ in list)
            {
                if (occ is BoardOccupant board && board.Orientation == orient)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Returns true if the given cell contains any occupant (board or small block).
        /// </summary>
        public bool HasAnyOccupant(Vector3Int cell)
        {
            return _cellOccupants.ContainsKey(cell) && _cellOccupants[cell].Count > 0;
        }

        /// <summary>
        /// Get all occupants in a cell.
        /// </summary>
        public IReadOnlyList<IOccupant> GetOccupants(Vector3Int cell)
        {
            if (_cellOccupants.TryGetValue(cell, out var list))
                return list;
            return Array.Empty<IOccupant>();
        }

        /// <summary>
        /// Find the BoardOccupant at a cell with the given orientation, and return its anchor.
        /// Equivalent to the old GetAnchor(cell, orient).
        /// </summary>
        public Vector3Int? GetBoardAnchor(Vector3Int cell, BoardOrientation orient)
        {
            if (!_cellOccupants.TryGetValue(cell, out var list)) return null;
            foreach (var occ in list)
            {
                if (occ is BoardOccupant board && board.Orientation == orient)
                    return board.Anchor;
            }
            return null;
        }

        /// <summary>
        /// Get the BoardOccupant at the given anchor and orientation (anchor-based lookup).
        /// </summary>
        public BoardOccupant GetBoardOccupant(Vector3Int anchor, BoardOrientation orient)
        {
            if (!_cellOccupants.TryGetValue(anchor, out var list)) return null;
            foreach (var occ in list)
            {
                if (occ is BoardOccupant board &&
                    board.Orientation == orient &&
                    board.Anchor == anchor)
                    return board;
            }
            return null;
        }

        /// <summary>
        /// Get the GameObject for a board at the given anchor and orientation.
        /// Equivalent to the old GetBoard(anchor, orient).
        /// </summary>
        public GameObject GetBoard(Vector3Int anchor, BoardOrientation orient)
        {
            return GetBoardOccupant(anchor, orient)?.GameObject;
        }

        /// <summary>
        /// Add a board occupant. Validates no conflicting orientations exist.
        /// </summary>
        public bool AddBoard(Vector3Int anchor, BoardOrientation orient, GameObject boardObj)
        {
            var occupant = new BoardOccupant(anchor, orient, boardObj);
            var cells = occupant.GetOccupiedCells();

            bool isFull = orient == (BoardOrientation.X | BoardOrientation.Y | BoardOrientation.Z);

            // Validate all cells are free of conflicting boards
            foreach (var cell in cells)
            {
                if (!_cellOccupants.TryGetValue(cell, out var list)) continue;
                foreach (var existing in list)
                {
                    if (existing is not BoardOccupant existingBoard) continue;

                    bool existingIsFull = existingBoard.Orientation ==
                        (BoardOrientation.X | BoardOrientation.Y | BoardOrientation.Z);

                    // Can't place a panel where a FullCell board exists
                    if (existingIsFull && !isFull) return false;
                    // Can't place a FullCell board where any board exists
                    if (isFull) return false;
                    // Can't place if this orientation is already occupied
                    if (existingBoard.Orientation == orient) return false;
                }
            }

            // Register occupant in all occupied cells
            foreach (var cell in cells)
            {
                if (!_cellOccupants.ContainsKey(cell))
                    _cellOccupants[cell] = new List<IOccupant>();
                _cellOccupants[cell].Add(occupant);
            }
            _occupantRegistry.Add(occupant);

            OnOccupantAdded?.Invoke(occupant);
            return true;
        }

        /// <summary>
        /// Remove a board at the given anchor and orientation.
        /// </summary>
        public bool RemoveBoard(Vector3Int anchor, BoardOrientation orient)
        {
            var occupant = GetBoardOccupant(anchor, orient);
            if (occupant == null) return false;

            return RemoveOccupant(occupant);
        }

        /// <summary>
        /// Add a small block occupant. Validates no other small block exists in the cell.
        /// </summary>
        public bool AddSmallBlock(Vector3Int cell, Quaternion rotation, GameObject blockObj)
        {
            // Check for existing small block in this cell
            if (HasOccupant(cell, OccupantType.SmallBlock))
                return false;

            var occupant = new SmallBlockOccupant(cell, rotation, blockObj);

            if (!_cellOccupants.ContainsKey(cell))
                _cellOccupants[cell] = new List<IOccupant>();
            _cellOccupants[cell].Add(occupant);
            _occupantRegistry.Add(occupant);

            OnOccupantAdded?.Invoke(occupant);
            return true;
        }

        /// <summary>
        /// Remove any occupant from the grid. Cleans up all cell references.
        /// </summary>
        public bool RemoveOccupant(IOccupant occupant)
        {
            if (!_occupantRegistry.Remove(occupant))
                return false;

            var cells = occupant.GetOccupiedCells();
            foreach (var cell in cells)
            {
                if (_cellOccupants.TryGetValue(cell, out var list))
                {
                    list.Remove(occupant);
                    if (list.Count == 0)
                        _cellOccupants.Remove(cell);
                }
            }

            if (occupant.GameObject != null)
                Destroy(occupant.GameObject);

            OnOccupantRemoved?.Invoke(occupant);
            return true;
        }

        /// <summary>
        /// Returns true if any neighbor of the board at anchor/orient exists.
        /// Uses anchor-to-anchor adjacency offsets. Used by PlacementTriggerManager.
        /// </summary>
        public bool HasAnyNeighbor(Vector3Int anchor, BoardOrientation orient)
        {
            var neighbors = BoardAdjacency.GetNeighbors(orient);
            foreach (var n in neighbors)
            {
                Vector3Int neighborAnchor = anchor + n.Offset;
                if (HasBoard(neighborAnchor, n.Orientation))
                {
                    // Verify it's actually an anchor (not just an occupied cell from a different board)
                    var foundAnchor = GetBoardAnchor(neighborAnchor, n.Orientation);
                    if (foundAnchor.HasValue && foundAnchor.Value == neighborAnchor)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns true if any of the 6 face-neighbors of the given cell
        /// contain a board or small block. Used for small block adjacency validation.
        /// </summary>
        public bool HasAnyFaceNeighbor(Vector3Int cell)
        {
            Vector3Int[] offsets =
            {
                Vector3Int.right, Vector3Int.left,
                Vector3Int.up, Vector3Int.down,
                new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
            };

            foreach (var offset in offsets)
            {
                if (HasAnyOccupant(cell + offset))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Find an occupant in the given cell whose GameObject matches the specified object.
        /// Used for removal by raycast hit.
        /// </summary>
        public IOccupant FindOccupantByGameObject(Vector3Int cell, GameObject obj)
        {
            if (!_cellOccupants.TryGetValue(cell, out var list)) return null;
            foreach (var occ in list)
            {
                if (occ.GameObject == obj)
                    return occ;
            }
            return null;
        }
    }
}
```

**Step 2: Verify compilation**

Open Unity Editor. Check Console for compilation errors. All errors at this point will be in files that consume the old API (PlacementTriggerManager, BuildingController, etc.) — that's expected and will be fixed in subsequent tasks.

**Step 3: Commit**

```bash
git add Assets/Scripts/Building/BuildingGrid.cs
git commit -m "feat(building): refactor BuildingGrid to IOccupant model"
```

---

### Task 8: Update PlacementTriggerManager for New Events

**Files:**
- Modify: `Assets/Scripts/Building/PlacementTriggerManager.cs:1-209`

**Step 1: Update event subscriptions and handlers**

The manager needs to:
1. Subscribe to `OnOccupantAdded`/`OnOccupantRemoved` instead of `OnBoardAdded`/`OnBoardRemoved`
2. Filter for `OccupantType.Board` only
3. Extract anchor and orientation from `BoardOccupant`

Replace lines 30-31 (event subscriptions):
```csharp
// OLD:
_grid.OnBoardAdded += HandleBoardAdded;
_grid.OnBoardRemoved += HandleBoardRemoved;

// NEW:
_grid.OnOccupantAdded += HandleOccupantAdded;
_grid.OnOccupantRemoved += HandleOccupantRemoved;
```

Replace lines 38-39 (unsubscriptions):
```csharp
// OLD:
_grid.OnBoardAdded -= HandleBoardAdded;
_grid.OnBoardRemoved -= HandleBoardRemoved;

// NEW:
_grid.OnOccupantAdded -= HandleOccupantAdded;
_grid.OnOccupantRemoved -= HandleOccupantRemoved;
```

Replace `HandleBoardAdded` (lines 43-71) and `HandleBoardRemoved` (lines 73-96) with:

```csharp
private void HandleOccupantAdded(IOccupant occupant)
{
    if (occupant is not BoardOccupant board) return;

    var anchor = board.Anchor;
    var orient = board.Orientation;
    bool isFull = orient == FULL;

    if (isFull)
    {
        RemoveTrigger(anchor, BoardOrientation.X);
        RemoveTrigger(anchor, BoardOrientation.Y);
        RemoveTrigger(anchor, BoardOrientation.Z);
    }
    else
    {
        RemoveTrigger(anchor, orient);
    }

    var neighbors = BoardAdjacency.GetNeighbors(orient);
    foreach (var n in neighbors)
    {
        Vector3Int neighborAnchor = anchor + n.Offset;
        BoardOrientation neighborOrient = n.Orientation;

        if (!_grid.HasBoard(neighborAnchor, neighborOrient))
        {
            CreateTrigger(neighborAnchor, neighborOrient);
        }
    }
}

private void HandleOccupantRemoved(IOccupant occupant)
{
    if (occupant is not BoardOccupant board) return;

    var anchor = board.Anchor;
    var orient = board.Orientation;
    bool isFull = orient == FULL;

    if (isFull)
    {
        foreach (var o in new[] { BoardOrientation.X, BoardOrientation.Y, BoardOrientation.Z })
        {
            if (_grid.HasAnyNeighbor(anchor, o))
            {
                CreateTrigger(anchor, o);
            }
        }
    }
    else
    {
        if (_grid.HasAnyNeighbor(anchor, orient))
        {
            CreateTrigger(anchor, orient);
        }
    }

    CleanupOrphanedTriggers();
}
```

**Step 2: Commit**

```bash
git add Assets/Scripts/Building/PlacementTriggerManager.cs
git commit -m "feat(building): update PlacementTriggerManager for IOccupant events"
```

---

### Task 9: Update BuildingController for Small Block Placement and Removal

This is the second largest task. Add small block targeting, update placement to handle both modes, and update removal to use the occupant model.

**Files:**
- Modify: `Assets/Scripts/Building/BuildingController.cs:1-331`

**Step 1: Rewrite BuildingController**

Key changes:
1. Add `_targetRotation` field for small block rotation
2. Add `HandleSmallBlockTargeting()` method
3. Update `PlaceBoard` to handle small blocks via `PlaceSmallBlock`
4. Update `HandleRemoval()` to use `FindOccupantByGameObject`
5. Update events to use `IOccupant`
6. Update preview to use correct world position for small blocks

Replace the entire file:

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
        private Quaternion _targetRotation; // Used for small blocks

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
        public event Action<IOccupant> OnOccupantPlaced;
        public event Action<Vector3Int> OnPlaceFailed;
        public event Action<IOccupant> OnBeforeRemove;

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

            HandleHotbarInput();

            if (!_inputEnabled) return;

            _hasTarget = false;

            if (_activePrefab != null)
            {
                if (_activePlacementMode == Inventory.PlacementMode.SmallBlock)
                {
                    // Small blocks require existing geometry to raycast against
                    if (!_grid.IsEmpty())
                        HandleSmallBlockTargeting();
                }
                else if (_grid.IsEmpty())
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
                Vector3 worldPos;
                Quaternion rotation;

                if (_activePlacementMode == Inventory.PlacementMode.SmallBlock)
                {
                    worldPos = GridConstants.SmallBlockGridToWorld(_targetPos);
                    rotation = _targetRotation;
                }
                else
                {
                    worldPos = PlacementTriggerManager.GridToWorld(_targetPos, _targetOrient);
                    rotation = PlacementTriggerManager.GetBoardRotation(_targetOrient);
                }

                boardPreview.ShowAt(worldPos, rotation);
            }
            else
            {
                boardPreview.Hide();
            }

            // Handle placement
            if (_hasTarget && _attackAction.WasPressedThisFrame())
            {
                if (_activePlacementMode == Inventory.PlacementMode.SmallBlock)
                    PlaceSmallBlock(_targetPos, _targetRotation);
                else
                    PlaceBoard(_targetPos, _targetOrient);
            }

            // Handle removal
            if (_removeAction.WasPressedThisFrame())
            {
                HandleRemoval();
            }
        }

        private void HandleHotbarInput()
        {
            for (int i = 0; i < 9; i++)
            {
                if (Keyboard.current != null && Keyboard.current[Key.Digit1 + i].wasPressedThisFrame)
                {
                    Inventory.InventoryManager.Instance?.SetActiveSlot(i);
                    return;
                }
            }

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

        private void HandleSmallBlockTargeting()
        {
            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, maxPlacementDistance, boardLayer))
            {
                // Nudge hit point along normal to land in the adjacent empty cell
                Vector3Int targetCell = WorldToGrid(hit.point + hit.normal * 0.5f);

                // Adjacency validation: at least one face-neighbor must be occupied
                if (!_grid.HasAnyFaceNeighbor(targetCell))
                    return;

                // Co-existence validation: no other small block in this cell
                if (_grid.HasOccupant(targetCell, OccupantType.SmallBlock))
                    return;

                // Compute rotation
                Vector3 blockWorldPos = GridConstants.SmallBlockGridToWorld(targetCell);
                Quaternion rotation = SmallBlockRotation.ComputeRotation(
                    hit.normal, cameraTransform.position, blockWorldPos);

                _hasTarget = true;
                _targetPos = targetCell;
                _targetRotation = rotation;
            }
        }

        private void PlaceBoard(Vector3Int pos, BoardOrientation orient)
        {
            if (_activePrefab == null)
            {
                Debug.LogError("[BuildingController] No active prefab assigned.");
                return;
            }

            var args = new PlaceCancelEventArgs(pos, orient);
            OnBeforePlace?.Invoke(args);
            if (args.Cancel) return;

            Vector3 worldPos = PlacementTriggerManager.GridToWorld(pos, orient);
            Quaternion rotation = PlacementTriggerManager.GetBoardRotation(orient);

            GameObject board = Instantiate(_activePrefab, worldPos, rotation);
            board.name = $"Board_{pos}_{orient}";

            if (_grid.AddBoard(pos, orient, board))
            {
                var occupant = _grid.GetBoardOccupant(pos, orient);
                OnOccupantPlaced?.Invoke(occupant);
            }
            else
            {
                Destroy(board);
                OnPlaceFailed?.Invoke(pos);
            }
        }

        private void PlaceSmallBlock(Vector3Int pos, Quaternion rotation)
        {
            if (_activePrefab == null)
            {
                Debug.LogError("[BuildingController] No active prefab assigned.");
                return;
            }

            var args = new PlaceCancelEventArgs(pos, BoardOrientation.None);
            OnBeforePlace?.Invoke(args);
            if (args.Cancel) return;

            Vector3 worldPos = GridConstants.SmallBlockGridToWorld(pos);

            GameObject block = Instantiate(_activePrefab, worldPos, rotation);
            block.name = $"SmallBlock_{pos}";

            if (_grid.AddSmallBlock(pos, rotation, block))
            {
                // Find the occupant we just added
                var occupants = _grid.GetOccupants(pos);
                IOccupant placed = null;
                foreach (var occ in occupants)
                {
                    if (occ.GameObject == block)
                    {
                        placed = occ;
                        break;
                    }
                }
                OnOccupantPlaced?.Invoke(placed);
            }
            else
            {
                Destroy(block);
                OnPlaceFailed?.Invoke(pos);
            }
        }

        private void HandleRemoval()
        {
            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, maxPlacementDistance, boardLayer))
            {
                Vector3Int cell = WorldToGrid(hit.point);

                // Try to find the occupant by matching the hit GameObject
                IOccupant occupant = _grid.FindOccupantByGameObject(cell, hit.collider.gameObject);

                // Fallback: check immediate neighboring cells (hit point may land on cell boundary)
                if (occupant == null)
                {
                    for (int dx = -1; dx <= 1 && occupant == null; dx++)
                    {
                        for (int dy = -1; dy <= 1 && occupant == null; dy++)
                        {
                            for (int dz = -1; dz <= 1 && occupant == null; dz++)
                            {
                                if (dx == 0 && dy == 0 && dz == 0) continue;
                                Vector3Int checkCell = cell + new Vector3Int(dx, dy, dz);
                                occupant = _grid.FindOccupantByGameObject(checkCell, hit.collider.gameObject);
                            }
                        }
                    }
                }

                if (occupant != null)
                {
                    OnBeforeRemove?.Invoke(occupant);
                    _grid.RemoveOccupant(occupant);
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

        public static Vector3Int WorldToGrid(Vector3 worldPos)
        {
            return new Vector3Int(
                Mathf.FloorToInt(worldPos.x / GridConstants.CELL_SIZE),
                Mathf.FloorToInt(worldPos.y / GridConstants.CELL_SIZE),
                Mathf.FloorToInt(worldPos.z / GridConstants.CELL_SIZE)
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

**Step 2: Verify compilation in Unity**

Expect remaining errors in `BuildingInventoryBridge.cs` — that's the next task.

**Step 3: Commit**

```bash
git add Assets/Scripts/Building/BuildingController.cs
git commit -m "feat(building): update BuildingController for small block placement and IOccupant model"
```

---

### Task 10: Update BuildingInventoryBridge for New Event Signatures

**Files:**
- Modify: `Assets/Scripts/Building/BuildingInventoryBridge.cs:1-145`

**Step 1: Update the bridge**

Changes:
1. Subscribe to `OnOccupantPlaced` and `OnBeforeRemove` (new signatures using `IOccupant`)
2. Track placed items by `GameObject` (unchanged, but event handler signatures change)
3. `OnPlaceFailed` now takes `Vector3Int` only

Replace the entire file:

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

        // Temporarily stores the item being placed between OnBeforePlace and OnOccupantPlaced
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
            _buildingController.OnOccupantPlaced += HandleOccupantPlaced;
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
                _buildingController.OnOccupantPlaced -= HandleOccupantPlaced;
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

            _pendingPlaceItem = item;
        }

        private void HandleOccupantPlaced(IOccupant occupant)
        {
            if (_pendingPlaceItem != null && occupant?.GameObject != null)
            {
                _placedItemTracker[occupant.GameObject] = _pendingPlaceItem;
                _pendingPlaceItem = null;
            }
        }

        private void HandlePlaceFailed(Vector3Int pos)
        {
            if (_pendingPlaceItem != null)
            {
                _inventoryManager.AddItem(_pendingPlaceItem, 1);
                _pendingPlaceItem = null;
            }
        }

        private void HandleBeforeRemove(IOccupant occupant)
        {
            if (occupant?.GameObject != null &&
                _placedItemTracker.TryGetValue(occupant.GameObject, out var item))
            {
                _inventoryManager.AddItem(item, 1);
                _placedItemTracker.Remove(occupant.GameObject);
            }
        }
    }
}
```

**Step 2: Verify full compilation in Unity**

Open Unity Editor. Console should show zero compilation errors. All systems should compile cleanly.

**Step 3: Commit**

```bash
git add Assets/Scripts/Building/BuildingInventoryBridge.cs
git commit -m "feat(building): update BuildingInventoryBridge for IOccupant events"
```

---

### Task 11: Create Small Block Placeholder Prefab

**Files:**
- Modify: Editor tooling or manual prefab creation

**Step 1: Create the prefab manually in Unity**

1. In the Unity Editor, create a new GameObject: `GameObject > 3D Object > Cube`
2. Rename it to `SmallBlock`
3. Set Transform scale to `(2, 2, 2)`
4. Verify the BoxCollider size is `(1, 1, 1)` (Unity auto-scales this with the transform, so the effective world-space size will be `(2, 2, 2)`)
5. Set the layer to `Board` (same layer as boards)
6. Drag it into `Assets/Prefabs/Building/` (or wherever board prefabs live) to create a prefab
7. Delete the scene instance

**Step 2: Create a BuildingItemSO asset**

1. In Project window: `Right-click > Create > Building > Building Item`
2. Name it `SmallBlockItem`
3. Set fields:
   - `itemName`: "Small Block"
   - `icon`: any placeholder sprite
   - `maxStackSize`: 64
   - `prefab`: the SmallBlock prefab from step 1
   - `placementMode`: SmallBlock

**Step 3: Commit**

```bash
git add Assets/Prefabs/ Assets/ScriptableObjects/
git commit -m "feat(building): add small block placeholder prefab and item SO"
```

---

### Task 12: Manual Play-Mode Verification

**Step 1: Verify board placement still works**

1. Enter Play Mode
2. Select a board item in the hotbar
3. Place the first board (should work as before — projects 8 units forward)
4. Place adjacent boards using placement triggers
5. Remove a board with the remove action
6. Verify placement triggers appear and disappear correctly

**Step 2: Verify small block placement**

1. Add the SmallBlockItem to the inventory/hotbar
2. Select it
3. Look at an existing board surface
4. Press the attack/place action
5. Verify the small block appears adjacent to the board surface
6. Verify rotation: bottom face should point into the board, front face should point toward you

**Step 3: Verify small block stacking**

1. Look at a placed small block
2. Place another small block on top of it
3. Verify it appears in the correct adjacent cell

**Step 4: Verify co-existence**

1. Place a board that passes through a cell occupied by a small block
2. Verify both the board and small block remain visible and functional

**Step 5: Verify removal**

1. Look at a small block and press remove
2. Verify it disappears and the item returns to inventory

---

### Task 13: Final Commit and Cleanup

**Step 1: Review all changes**

```bash
git diff HEAD~12 --stat
```

Verify the file change list matches expectations from the design doc.

**Step 2: Verify no leftover debug code or TODOs**

Search for `Debug.Log` calls that should be removed, any `// TODO` comments that need addressing.

**Step 3: Final commit if any cleanup was needed**

```bash
git add -A
git commit -m "chore: small block cleanup and final review"
```
