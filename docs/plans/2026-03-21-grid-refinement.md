# Grid Refinement Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Change the building grid from 4-unit cells to 2-unit cells, with boards spanning 2x2 cells, preparing for future smaller building blocks.

**Architecture:** Halve CELL_SIZE to 2. Each 4x4 board registers its orientation flag in all 4 cells it occupies (2x2 in its plane). A reverse anchor map enables efficient lookups. Adjacency offsets are recalculated for the new geometry.

**Tech Stack:** Unity 6, C#, existing Building namespace

**Design Doc:** `docs/plans/2026-03-21-grid-refinement-design.md`

---

### Task 1: Create GridConstants

**Files:**
- Create: `Assets/Scripts/Building/GridConstants.cs`

**Step 1: Create the constants file**

```csharp
namespace Building
{
    public static class GridConstants
    {
        public const float CELL_SIZE = 2f;
        public const int BOARD_SPAN = 2;
        public const float BOARD_WORLD_SIZE = CELL_SIZE * BOARD_SPAN; // 4f

        private const BoardOrientation FULL = BoardOrientation.X | BoardOrientation.Y | BoardOrientation.Z;

        /// <summary>
        /// Returns all grid cells occupied by a board anchored at the given position.
        /// Boards span BOARD_SPAN cells in each of their two planar axes.
        /// FullCell spans BOARD_SPAN cells in all three axes.
        /// </summary>
        public static UnityEngine.Vector3Int[] GetOccupiedCells(
            UnityEngine.Vector3Int anchor, BoardOrientation orient)
        {
            if (orient == FULL)
            {
                // 2x2x2 = 8 cells
                return new UnityEngine.Vector3Int[]
                {
                    anchor,
                    anchor + new UnityEngine.Vector3Int(1, 0, 0),
                    anchor + new UnityEngine.Vector3Int(0, 1, 0),
                    anchor + new UnityEngine.Vector3Int(0, 0, 1),
                    anchor + new UnityEngine.Vector3Int(1, 1, 0),
                    anchor + new UnityEngine.Vector3Int(1, 0, 1),
                    anchor + new UnityEngine.Vector3Int(0, 1, 1),
                    anchor + new UnityEngine.Vector3Int(1, 1, 1),
                };
            }

            // 2x2 = 4 cells in the board's planar axes
            return orient switch
            {
                // Z-board (XZ plane): expand along X and Z
                BoardOrientation.Z => new UnityEngine.Vector3Int[]
                {
                    anchor,
                    anchor + new UnityEngine.Vector3Int(1, 0, 0),
                    anchor + new UnityEngine.Vector3Int(0, 0, 1),
                    anchor + new UnityEngine.Vector3Int(1, 0, 1),
                },
                // X-board (XY plane): expand along X and Y
                BoardOrientation.X => new UnityEngine.Vector3Int[]
                {
                    anchor,
                    anchor + new UnityEngine.Vector3Int(1, 0, 0),
                    anchor + new UnityEngine.Vector3Int(0, 1, 0),
                    anchor + new UnityEngine.Vector3Int(1, 1, 0),
                },
                // Y-board (YZ plane): expand along Y and Z
                BoardOrientation.Y => new UnityEngine.Vector3Int[]
                {
                    anchor,
                    anchor + new UnityEngine.Vector3Int(0, 1, 0),
                    anchor + new UnityEngine.Vector3Int(0, 0, 1),
                    anchor + new UnityEngine.Vector3Int(0, 1, 1),
                },
                _ => new UnityEngine.Vector3Int[] { anchor }
            };
        }
    }
}
```

**Step 2: Verify it compiles**

Open Unity or run the project build. No errors expected since nothing references this file yet.

**Step 3: Commit**

```bash
git add Assets/Scripts/Building/GridConstants.cs
git commit -m "feat(building): add GridConstants with CELL_SIZE=2 and GetOccupiedCells"
```

---

### Task 2: Rewrite BoardAdjacency for 2-unit cells

**Files:**
- Modify: `Assets/Scripts/Building/BoardAdjacency.cs` (full rewrite of offset tables)

The adjacency table structure stays the same (12 neighbors per orientation), but all offsets change because boards now span 2 cells in their planar axes.

**Step 1: Replace the adjacency tables**

Replace the entire contents of `BoardAdjacency.cs` with:

```csharp
using UnityEngine;

namespace Building
{
    public static class BoardAdjacency
    {
        public struct Neighbor
        {
            public Vector3Int Offset;
            public BoardOrientation Orientation;

            public Neighbor(int x, int y, int z, BoardOrientation orient)
            {
                Offset = new Vector3Int(x, y, z);
                Orientation = orient;
            }
        }

        // Z-board at anchor (0,0,0): XZ horizontal plane
        // Board spans cells (0,0,0)(1,0,0)(0,0,1)(1,0,1)
        // World footprint: x[0..4], z[0..4] at y=0
        private static readonly Neighbor[] ZNeighbors = new Neighbor[]
        {
            // 4 coplanar Z-neighbors (offset by 2 = BOARD_SPAN along planar axes)
            new(-2,  0,  0, BoardOrientation.Z),  // shares left edge
            new( 2,  0,  0, BoardOrientation.Z),  // shares right edge
            new( 0,  0, -2, BoardOrientation.Z),  // shares back edge
            new( 0,  0,  2, BoardOrientation.Z),  // shares front edge
            // 4 perpendicular X-boards (XY plane, sharing back/front edges)
            new( 0, -2,  0, BoardOrientation.X),  // X-board top edge = Z back edge
            new( 0,  0,  0, BoardOrientation.X),  // X-board bottom edge = Z back edge
            new( 0, -2,  2, BoardOrientation.X),  // X-board top edge = Z front edge
            new( 0,  0,  2, BoardOrientation.X),  // X-board bottom edge = Z front edge
            // 4 perpendicular Y-boards (YZ plane, sharing left/right edges)
            new( 0, -2,  0, BoardOrientation.Y),  // Y-board top edge = Z left edge
            new( 0,  0,  0, BoardOrientation.Y),  // Y-board bottom edge = Z left edge
            new( 2, -2,  0, BoardOrientation.Y),  // Y-board top edge = Z right edge
            new( 2,  0,  0, BoardOrientation.Y),  // Y-board bottom edge = Z right edge
        };

        // X-board at anchor (0,0,0): XY vertical plane (faces Z axis)
        // Board spans cells (0,0,0)(1,0,0)(0,1,0)(1,1,0)
        // World footprint: x[0..4], y[0..4] at z=0
        private static readonly Neighbor[] XNeighbors = new Neighbor[]
        {
            // 4 coplanar X-neighbors
            new(-2,  0,  0, BoardOrientation.X),  // shares left edge
            new( 2,  0,  0, BoardOrientation.X),  // shares right edge
            new( 0, -2,  0, BoardOrientation.X),  // shares bottom edge
            new( 0,  2,  0, BoardOrientation.X),  // shares top edge
            // 4 perpendicular Z-boards (XZ plane, sharing bottom/top edges)
            new( 0,  0, -2, BoardOrientation.Z),  // Z front edge = X bottom edge
            new( 0,  0,  0, BoardOrientation.Z),  // Z back edge = X bottom edge
            new( 0,  2, -2, BoardOrientation.Z),  // Z front edge = X top edge
            new( 0,  2,  0, BoardOrientation.Z),  // Z back edge = X top edge
            // 4 perpendicular Y-boards (YZ plane, sharing left/right edges)
            new( 0,  0, -2, BoardOrientation.Y),  // Y front edge = X left edge
            new( 0,  0,  0, BoardOrientation.Y),  // Y back edge = X left edge
            new( 2,  0, -2, BoardOrientation.Y),  // Y front edge = X right edge
            new( 2,  0,  0, BoardOrientation.Y),  // Y back edge = X right edge
        };

        // Y-board at anchor (0,0,0): YZ vertical plane (faces X axis)
        // Board spans cells (0,0,0)(0,1,0)(0,0,1)(0,1,1)
        // World footprint: y[0..4], z[0..4] at x=0
        private static readonly Neighbor[] YNeighbors = new Neighbor[]
        {
            // 4 coplanar Y-neighbors
            new( 0,  0, -2, BoardOrientation.Y),  // shares back edge
            new( 0,  0,  2, BoardOrientation.Y),  // shares front edge
            new( 0, -2,  0, BoardOrientation.Y),  // shares bottom edge
            new( 0,  2,  0, BoardOrientation.Y),  // shares top edge
            // 4 perpendicular Z-boards (XZ plane, sharing bottom/top edges)
            new(-2,  0,  0, BoardOrientation.Z),  // Z right edge = Y bottom edge
            new( 0,  0,  0, BoardOrientation.Z),  // Z left edge = Y bottom edge
            new(-2,  2,  0, BoardOrientation.Z),  // Z right edge = Y top edge
            new( 0,  2,  0, BoardOrientation.Z),  // Z left edge = Y top edge
            // 4 perpendicular X-boards (XY plane, sharing back/front edges)
            new(-2,  0,  0, BoardOrientation.X),  // X right edge = Y back edge
            new( 0,  0,  0, BoardOrientation.X),  // X left edge = Y back edge
            new(-2,  0,  2, BoardOrientation.X),  // X right edge = Y front edge
            new( 0,  0,  2, BoardOrientation.X),  // X left edge = Y front edge
        };

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
    }
}
```

**Step 2: Verify it compiles**

Open Unity or trigger recompilation. The file has the same public API so all callers still compile.

**Step 3: Commit**

```bash
git add Assets/Scripts/Building/BoardAdjacency.cs
git commit -m "feat(building): rewrite adjacency tables for 2-unit cell geometry"
```

---

### Task 3: Update BuildingGrid for multi-cell registration

**Files:**
- Modify: `Assets/Scripts/Building/BuildingGrid.cs`

This is the core change. `AddBoard` and `RemoveBoard` now write/clear flags in multiple cells, and a new anchor map supports reverse lookups.

**Step 1: Rewrite BuildingGrid.cs**

Replace the entire contents of `BuildingGrid.cs` with:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Building
{
    public class BuildingGrid : MonoBehaviour
    {
        public static BuildingGrid Instance { get; private set; }

        private const BoardOrientation FULL = BoardOrientation.X | BoardOrientation.Y | BoardOrientation.Z;

        public event Action<Vector3Int, BoardOrientation> OnBoardAdded;
        public event Action<Vector3Int, BoardOrientation> OnBoardRemoved;

        // Per-cell orientation flags (every cell a board touches has its flag set)
        private Dictionary<Vector3Int, BoardOrientation> _grid = new();

        // Anchor-only registry: only the anchor cell maps to the GameObject
        private Dictionary<Vector3Int, Dictionary<BoardOrientation, GameObject>> _boardRegistry = new();

        // Reverse lookup: (cell, orient) -> anchor position
        private Dictionary<(Vector3Int, BoardOrientation), Vector3Int> _anchorMap = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public bool IsEmpty() => _boardRegistry.Count == 0;

        /// <summary>
        /// Returns true if the given cell has the given orientation flag set.
        /// This is a per-cell query — any cell a board occupies will return true.
        /// </summary>
        public bool HasBoard(Vector3Int pos, BoardOrientation orient)
        {
            return _grid.TryGetValue(pos, out var flags) && (flags & orient) != 0;
        }

        /// <summary>
        /// Returns the anchor position of the board occupying cell `pos` at `orient`,
        /// or null if no board occupies that cell at that orientation.
        /// </summary>
        public Vector3Int? GetAnchor(Vector3Int cell, BoardOrientation orient)
        {
            if (_anchorMap.TryGetValue((cell, orient), out var anchor))
                return anchor;
            return null;
        }

        /// <summary>
        /// Place a board at the given anchor position and orientation.
        /// The board will occupy multiple cells (2x2 for panels, 2x2x2 for FullCell).
        /// </summary>
        public bool AddBoard(Vector3Int anchor, BoardOrientation orient, GameObject boardObj)
        {
            var cells = GridConstants.GetOccupiedCells(anchor, orient);

            bool isFull = orient == FULL;

            // Validate all cells are free
            foreach (var cell in cells)
            {
                if (_grid.TryGetValue(cell, out var existingFlags))
                {
                    bool existingHasFull = existingFlags == FULL;

                    // Can't place a panel where a FullCell exists
                    if (existingHasFull && !isFull) return false;
                    // Can't place a FullCell where any panel exists
                    if (isFull && existingFlags != BoardOrientation.None) return false;
                    // Can't place if this orientation is already occupied
                    if ((existingFlags & orient) != 0) return false;
                }
            }

            // Set orientation flag in all occupied cells
            foreach (var cell in cells)
            {
                if (_grid.ContainsKey(cell))
                    _grid[cell] |= orient;
                else
                    _grid[cell] = orient;

                _anchorMap[(cell, orient)] = anchor;
            }

            // Store GameObject at anchor only
            if (!_boardRegistry.ContainsKey(anchor))
                _boardRegistry[anchor] = new Dictionary<BoardOrientation, GameObject>();
            _boardRegistry[anchor][orient] = boardObj;

            OnBoardAdded?.Invoke(anchor, orient);
            return true;
        }

        /// <summary>
        /// Remove a board at the given anchor position and orientation.
        /// Clears flags in all occupied cells.
        /// </summary>
        public bool RemoveBoard(Vector3Int anchor, BoardOrientation orient)
        {
            // Verify the board exists at this anchor
            if (!_boardRegistry.TryGetValue(anchor, out var orientDict) ||
                !orientDict.ContainsKey(orient))
                return false;

            var cells = GridConstants.GetOccupiedCells(anchor, orient);

            // Clear orientation flag in all occupied cells
            foreach (var cell in cells)
            {
                if (_grid.TryGetValue(cell, out var flags))
                {
                    flags &= ~orient;
                    if (flags == BoardOrientation.None)
                        _grid.Remove(cell);
                    else
                        _grid[cell] = flags;
                }

                _anchorMap.Remove((cell, orient));
            }

            // Destroy GameObject and clean up registry
            if (orientDict.TryGetValue(orient, out var obj))
            {
                if (obj != null) Destroy(obj);
                orientDict.Remove(orient);
            }
            if (orientDict.Count == 0)
                _boardRegistry.Remove(anchor);

            OnBoardRemoved?.Invoke(anchor, orient);
            return true;
        }

        /// <summary>
        /// Get the GameObject for the board at the given anchor and orientation.
        /// </summary>
        public GameObject GetBoard(Vector3Int anchor, BoardOrientation orient)
        {
            if (_boardRegistry.TryGetValue(anchor, out var orientDict) &&
                orientDict.TryGetValue(orient, out var obj))
                return obj;
            return null;
        }

        /// <summary>
        /// Returns true if any neighbor of the board at anchor/orient exists.
        /// Uses anchor-to-anchor adjacency offsets.
        /// </summary>
        public bool HasAnyNeighbor(Vector3Int anchor, BoardOrientation orient)
        {
            var neighbors = BoardAdjacency.GetNeighbors(orient);
            foreach (var n in neighbors)
            {
                Vector3Int neighborAnchor = anchor + n.Offset;
                // Check if a board exists at that anchor by checking the anchor cell
                if (HasBoard(neighborAnchor, n.Orientation))
                {
                    // Verify it's actually an anchor (not just an occupied cell from a different board)
                    var foundAnchor = GetAnchor(neighborAnchor, n.Orientation);
                    if (foundAnchor.HasValue && foundAnchor.Value == neighborAnchor)
                        return true;
                }
            }
            return false;
        }
    }
}
```

**Step 2: Verify it compiles**

Open Unity. The public API is mostly compatible. `RemoveBoard` and `AddBoard` have the same signatures. `GetBoard` now expects anchor positions (callers will be updated in later tasks).

**Step 3: Commit**

```bash
git add Assets/Scripts/Building/BuildingGrid.cs
git commit -m "feat(building): multi-cell registration with anchor map in BuildingGrid"
```

---

### Task 4: Update PlacementTriggerManager

**Files:**
- Modify: `Assets/Scripts/Building/PlacementTriggerManager.cs`

Replace the local `CELL_SIZE` constant with `GridConstants.CELL_SIZE`. Update `GridToWorld` to center boards across their 2x2 cell footprint. Trigger collider stays 4x4.

**Step 1: Rewrite PlacementTriggerManager.cs**

Replace the entire contents with:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Building
{
    public class PlacementTriggerManager : MonoBehaviour
    {
        private const BoardOrientation FULL = BoardOrientation.X | BoardOrientation.Y | BoardOrientation.Z;

        private BuildingGrid _grid;
        private Dictionary<Vector3Int, Dictionary<BoardOrientation, GameObject>> _triggers = new();
        private int _placementLayer;

        private void Start()
        {
            _grid = BuildingGrid.Instance;
            if (_grid == null)
            {
                Debug.LogError("[PlacementTriggerManager] BuildingGrid.Instance not found.");
                return;
            }

            _placementLayer = LayerMask.NameToLayer("PlacementTrigger");
            if (_placementLayer == -1)
            {
                Debug.LogError("[PlacementTriggerManager] Layer 'PlacementTrigger' not found. Run Tools > Building System > Setup Layers.");
                return;
            }

            _grid.OnBoardAdded += HandleBoardAdded;
            _grid.OnBoardRemoved += HandleBoardRemoved;
        }

        private void OnDestroy()
        {
            if (_grid != null)
            {
                _grid.OnBoardAdded -= HandleBoardAdded;
                _grid.OnBoardRemoved -= HandleBoardRemoved;
            }
        }

        private void HandleBoardAdded(Vector3Int anchor, BoardOrientation orient)
        {
            bool isFull = orient == FULL;

            if (isFull)
            {
                // Remove all triggers at this anchor
                RemoveTrigger(anchor, BoardOrientation.X);
                RemoveTrigger(anchor, BoardOrientation.Y);
                RemoveTrigger(anchor, BoardOrientation.Z);
            }
            else
            {
                RemoveTrigger(anchor, orient);
            }

            // Generate triggers at each valid empty neighbor anchor
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

        private void HandleBoardRemoved(Vector3Int anchor, BoardOrientation orient)
        {
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

        private void CreateTrigger(Vector3Int anchor, BoardOrientation orient)
        {
            if (HasTrigger(anchor, orient)) return;

            GameObject trigger = new GameObject($"Trigger_{anchor}_{orient}");
            trigger.transform.SetParent(transform);
            trigger.layer = _placementLayer;

            trigger.transform.position = GridToWorld(anchor, orient);
            trigger.transform.rotation = GetBoardRotation(orient);

            BoxCollider collider = trigger.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            // Trigger collider matches board world size (4x0.1x4), not cell size
            collider.size = new Vector3(GridConstants.BOARD_WORLD_SIZE, 0.1f, GridConstants.BOARD_WORLD_SIZE);

            TriggerInfo info = trigger.AddComponent<TriggerInfo>();
            info.GridPosition = anchor;
            info.Orientation = orient;

            if (!_triggers.ContainsKey(anchor))
                _triggers[anchor] = new Dictionary<BoardOrientation, GameObject>();
            _triggers[anchor][orient] = trigger;
        }

        private void RemoveTrigger(Vector3Int anchor, BoardOrientation orient)
        {
            if (!_triggers.TryGetValue(anchor, out var orientDict)) return;
            if (!orientDict.TryGetValue(orient, out var trigger)) return;

            if (trigger != null) Destroy(trigger);
            orientDict.Remove(orient);

            if (orientDict.Count == 0)
                _triggers.Remove(anchor);
        }

        private bool HasTrigger(Vector3Int anchor, BoardOrientation orient)
        {
            return _triggers.TryGetValue(anchor, out var orientDict) && orientDict.ContainsKey(orient);
        }

        private void CleanupOrphanedTriggers()
        {
            var toRemove = new List<(Vector3Int, BoardOrientation)>();

            foreach (var kvp in _triggers)
            {
                foreach (var orientKvp in kvp.Value)
                {
                    if (!_grid.HasAnyNeighbor(kvp.Key, orientKvp.Key))
                    {
                        toRemove.Add((kvp.Key, orientKvp.Key));
                    }
                }
            }

            foreach (var (anchor, orient) in toRemove)
            {
                RemoveTrigger(anchor, orient);
            }
        }

        public void ClearAllTriggers()
        {
            foreach (var orientDict in _triggers.Values)
            {
                foreach (var trigger in orientDict.Values)
                {
                    if (trigger != null) Destroy(trigger);
                }
            }
            _triggers.Clear();
        }

        /// <summary>
        /// Convert an anchor grid position to world-space center of the board.
        /// Board spans BOARD_SPAN cells (2) in each planar axis, so center is offset
        /// by half the board world size (2 units) along each planar axis.
        /// </summary>
        public static Vector3 GridToWorld(Vector3Int anchor, BoardOrientation orient)
        {
            float cs = GridConstants.CELL_SIZE;
            float half = GridConstants.BOARD_WORLD_SIZE / 2f; // 2 units

            Vector3 basePos = new Vector3(anchor.x * cs, anchor.y * cs, anchor.z * cs);

            return orient switch
            {
                // X-board (XY plane): center in X and Y, at z face
                BoardOrientation.X => basePos + new Vector3(half, half, 0f),
                // Y-board (YZ plane): center in Y and Z, at x face
                BoardOrientation.Y => basePos + new Vector3(0f, half, half),
                // Z-board (XZ plane): center in X and Z, at y face
                BoardOrientation.Z => basePos + new Vector3(half, 0f, half),
                // FullCell: center in all axes
                _ => basePos + new Vector3(half, half, half)
            };
        }

        public static Quaternion GetBoardRotation(BoardOrientation orient)
        {
            return orient switch
            {
                BoardOrientation.X => Quaternion.Euler(90f, 0f, 0f),
                BoardOrientation.Y => Quaternion.Euler(0f, 0f, 90f),
                BoardOrientation.Z => Quaternion.identity,
                _ => Quaternion.identity
            };
        }
    }
}
```

**Step 2: Verify it compiles**

Open Unity. This file's public API (`GridToWorld`, `GetBoardRotation`) is unchanged in signature.

**Step 3: Commit**

```bash
git add Assets/Scripts/Building/PlacementTriggerManager.cs
git commit -m "feat(building): update PlacementTriggerManager for 2-unit cells"
```

---

### Task 5: Update BuildingController

**Files:**
- Modify: `Assets/Scripts/Building/BuildingController.cs`

Replace the local `CELL_SIZE` constant in `WorldToGrid` with `GridConstants.CELL_SIZE`. Simplify `HandleRemoval` to use the anchor map instead of brute-force 3x3x3 search.

**Step 1: Update WorldToGrid**

In `BuildingController.cs`, find the `WorldToGrid` method (line 289-297) and replace:

```csharp
        private static Vector3Int WorldToGrid(Vector3 worldPos)
        {
            const float CELL_SIZE = 4f;
            return new Vector3Int(
                Mathf.FloorToInt(worldPos.x / CELL_SIZE),
                Mathf.FloorToInt(worldPos.y / CELL_SIZE),
                Mathf.FloorToInt(worldPos.z / CELL_SIZE)
            );
        }
```

with:

```csharp
        private static Vector3Int WorldToGrid(Vector3 worldPos)
        {
            return new Vector3Int(
                Mathf.FloorToInt(worldPos.x / GridConstants.CELL_SIZE),
                Mathf.FloorToInt(worldPos.y / GridConstants.CELL_SIZE),
                Mathf.FloorToInt(worldPos.z / GridConstants.CELL_SIZE)
            );
        }
```

**Step 2: Simplify HandleRemoval**

Replace the `HandleRemoval` method (lines 246-275) with:

```csharp
        private void HandleRemoval()
        {
            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, maxPlacementDistance, boardLayer))
            {
                Vector3Int cell = WorldToGrid(hit.point);

                // Try each orientation to find which board owns this cell
                foreach (BoardOrientation orient in new[] { BoardOrientation.X, BoardOrientation.Y, BoardOrientation.Z, FULL })
                {
                    Vector3Int? anchor = _grid.GetAnchor(cell, orient);
                    if (!anchor.HasValue) continue;

                    GameObject board = _grid.GetBoard(anchor.Value, orient);
                    if (board != null && board == hit.collider.gameObject)
                    {
                        OnBeforeRemove?.Invoke(anchor.Value, orient, board);
                        _grid.RemoveBoard(anchor.Value, orient);
                        return;
                    }
                }

                // Fallback: check immediate neighboring cells (hit point may land on cell boundary)
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            if (dx == 0 && dy == 0 && dz == 0) continue;
                            Vector3Int checkCell = cell + new Vector3Int(dx, dy, dz);
                            foreach (BoardOrientation orient in new[] { BoardOrientation.X, BoardOrientation.Y, BoardOrientation.Z, FULL })
                            {
                                Vector3Int? anchor = _grid.GetAnchor(checkCell, orient);
                                if (!anchor.HasValue) continue;

                                GameObject board = _grid.GetBoard(anchor.Value, orient);
                                if (board != null && board == hit.collider.gameObject)
                                {
                                    OnBeforeRemove?.Invoke(anchor.Value, orient, board);
                                    _grid.RemoveBoard(anchor.Value, orient);
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }
```

Note: We keep a small neighborhood fallback because `WorldToGrid(hit.point)` may land on a cell boundary due to floating-point precision. With 2-unit cells the margin is tighter, so checking ±1 in each axis covers edge cases.

**Step 3: Verify it compiles**

Open Unity. No new dependencies introduced.

**Step 4: Commit**

```bash
git add Assets/Scripts/Building/BuildingController.cs
git commit -m "feat(building): update BuildingController for 2-unit cells and anchor-based removal"
```

---

### Task 6: Manual Play-Mode Testing

No automated tests exist in this project (Unity play-mode tests are not set up). Verify the changes manually in the Unity Editor.

**Step 1: Open the scene and enter Play Mode**

**Step 2: Place first board**

- Equip a board from the hotbar
- Look forward and left-click to place the first board
- **Verify:** Board appears at the expected position, sized 4x4x0.1 world units
- **Verify:** Placement triggers appear around the board (green transparent planes)

**Step 3: Place adjacent boards**

- Look at a trigger and left-click to place a second board
- **Verify:** Second board connects edge-to-edge with the first (4-unit shared edge, no gap)
- **Verify:** New triggers appear at valid empty positions
- Repeat for all 3 orientations: place horizontal (Z), then vertical wall facing Z (X), then vertical wall facing X (Y)
- **Verify:** Perpendicular boards meet at shared 4-unit edges

**Step 4: Remove a board**

- Right-click on a placed board
- **Verify:** Board is removed, inventory item refunded
- **Verify:** Triggers update correctly (orphaned triggers removed, new triggers appear where neighbors exist)

**Step 5: Place FullCell**

- Switch to a FullCell item if available
- Place and verify it occupies the expected volume
- Place a board adjacent to the FullCell and verify adjacency works

**Step 6: Commit if all tests pass**

```bash
git add -A
git commit -m "test: manual verification of 2-unit cell grid refinement"
```

If any test fails, debug and fix before committing. The most likely issues are:
- Adjacency offset errors (boards don't connect, or triggers appear at wrong positions)
- GridToWorld positioning errors (boards appear offset from where they should be)
- Removal not finding the correct board (anchor map lookup misses)
