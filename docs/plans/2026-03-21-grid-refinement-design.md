# Grid Refinement: 4-unit to 2-unit Cells

**Date:** 2026-03-21
**Status:** Approved
**Motivation:** Prepare the grid for future smaller (2x2-unit) building blocks by halving the cell size. Existing 4x4 boards now span a 2x2 footprint of cells.

## Overview

Change `CELL_SIZE` from 4 to 2 world units. Boards remain 4x4x0.1 in world space but register across 4 grid cells (2x2 in their planar axes). Adjacency stays at full 4-unit edge granularity.

## Approach

Multi-cell board registration (Approach A). Each board writes its orientation flag into all cells it occupies. A reverse-lookup anchor map supports efficient removal. Single source of truth in the grid dictionary.

Alternatives considered and rejected:
- **Single anchor + virtual expansion:** Every query must expand coordinates; error-prone and harder to extend.
- **Two-layer grid:** Parallel data structures add synchronization complexity without clear benefit.

## Grid Constants

A new static class `GridConstants` centralizes the duplicated constant:

```
CELL_SIZE = 2f        // world units per cell edge
BOARD_SPAN = 2        // cells per board edge (board = BOARD_SPAN x BOARD_SPAN cells)
BOARD_WORLD_SIZE = 4f // CELL_SIZE * BOARD_SPAN, world units per board edge
```

`GetOccupiedCells(Vector3Int anchor, BoardOrientation orient)` returns the 4 (or 8 for FullCell) grid positions a board occupies:

| Orientation | Plane | Expansion axes | Cells from anchor (ax,ay,az) |
|---|---|---|---|
| Z (XZ) | Horizontal | X, Z | (ax,ay,az), (ax+1,ay,az), (ax,ay,az+1), (ax+1,ay,az+1) |
| X (XY) | Vertical, faces Z | X, Y | (ax,ay,az), (ax+1,ay,az), (ax,ay+1,az), (ax+1,ay+1,az) |
| Y (YZ) | Vertical, faces X | Y, Z | (ax,ay,az), (ax,ay+1,az), (ax,ay,az+1), (ax,ay+1,az+1) |
| Full | All | X, Y, Z | 8 cells: (ax..ax+1, ay..ay+1, az..az+1) |

The anchor is always the minimum-coordinate corner of the board's cell footprint.

## BuildingGrid: Multi-Cell Registration

### Data structures

```csharp
Dictionary<Vector3Int, BoardOrientation> _grid;                              // per-cell flags (unchanged type)
Dictionary<Vector3Int, Dictionary<BoardOrientation, GameObject>> _boardRegistry; // anchor -> orient -> GO
Dictionary<(Vector3Int, BoardOrientation), Vector3Int> _anchorMap;           // (cell, orient) -> anchor
```

### AddBoard(anchor, orient, boardObj)

1. Compute `cells = GetOccupiedCells(anchor, orient)`.
2. For each cell, verify the orientation bit is clear and no FullCell conflicts exist. If any cell fails, reject.
3. Set the orientation bit in all cells.
4. Store `boardObj` in `_boardRegistry` at the anchor only.
5. For each cell, write `_anchorMap[(cell, orient)] = anchor`.
6. Fire `OnBoardAdded(anchor, orient)`.

### RemoveBoard(anchor, orient)

1. Compute `cells = GetOccupiedCells(anchor, orient)`.
2. Clear the orientation bit in all cells. Remove empty entries.
3. Remove `_anchorMap` entries for all cells.
4. Destroy the `GameObject` from `_boardRegistry`.
5. Fire `OnBoardRemoved(anchor, orient)`.

### GetAnchor(cell, orient)

Returns the anchor for whatever board occupies `cell` at `orient`, or null. Used by removal and trigger queries.

### HasBoard / HasAnyNeighbor

`HasBoard(pos, orient)` checks the per-cell bit flag. Unchanged in signature.

`HasAnyNeighbor(anchor, orient)` iterates the adjacency table (which now uses anchor-to-anchor offsets) and calls `HasBoard` on the neighbor anchor's first occupied cell.

## Adjacency Tables

Same structure: 12 neighbors per orientation (4 coplanar + 8 perpendicular). Offsets change to reflect 2-cell board spans.

### Z-board (XZ plane) neighbors

Anchor at (0,0,0), board spans cells (0,0,0)(1,0,0)(0,0,1)(1,0,1).

**Coplanar Z-neighbors** (offset from anchor to neighbor anchor):
- Left: (-2, 0, 0)
- Right: (+2, 0, 0)
- Back: (0, 0, -2)
- Front: (0, 0, +2)

**Perpendicular X-boards** (XY plane, sharing back/front edges):
- Back-below: (0, -2, 0) orient X
- Back-above: (0, 0, 0) orient X
- Front-below: (0, -2, 2) orient X
- Front-above: (0, 0, 2) orient X

**Perpendicular Y-boards** (YZ plane, sharing left/right edges):
- Left-below: (0, -2, 0) orient Y
- Left-above: (0, 0, 0) orient Y
- Right-below: (2, -2, 0) orient Y
- Right-above: (2, 0, 0) orient Y

X-board and Y-board tables follow the same geometric derivation with rotated axes.

### FullCell neighbors

Union of all three orientation neighbor sets, deduplicated (same approach as current code).

## Placement Triggers

### Trigger collider size

Stays `(4, 0.1, 4)` world units. A trigger represents a valid board-sized placement slot, not a single cell.

### GridToWorld(anchor, orient)

Centers the board across its 2x2 cell footprint:

| Orient | World center |
|---|---|
| Z | anchor * 2 + (2, 0, 2) |
| X | anchor * 2 + (2, 2, 0) |
| Y | anchor * 2 + (0, 2, 2) |
| Full | anchor * 2 + (1, 1, 1) — note: FullCell is 2x2x2 world units, collider size (2, 2, 2) |

Wait — FullCell. Currently a FullCell is a single 4x4x4 block. With CELL_SIZE=2, a FullCell occupying 2x2x2 cells = 4x4x4 world units. The center is `anchor * 2 + (2, 2, 2)`. The FullCell collider/prefab needs to be 4x4x4 (unchanged from current behavior).

### TriggerInfo

No change. Stores anchor position and orientation.

### Trigger generation / cleanup

Same event-driven logic. `HandleBoardAdded` spawns triggers at empty neighbor anchors. `HandleBoardRemoved` re-evaluates and cleans up orphans. All using the recalculated adjacency offsets.

## BuildingController Changes

### WorldToGrid

```csharp
FloorToInt(worldPos / GridConstants.CELL_SIZE)  // now divides by 2 instead of 4
```

### HandleRemoval (simplified)

1. Raycast hit → `WorldToGrid(hit.point)` → cell position.
2. Try all orientations: `anchor = grid.GetAnchor(cell, orient)`.
3. If found and the `GameObject` matches the hit collider, remove via `grid.RemoveBoard(anchor, orient)`.

This replaces the current brute-force 3x3x3 neighborhood search.

### First board targeting

Same logic, uses updated `WorldToGrid`.

## Unchanged Components

- **BoardOrientation.cs** — enum values unchanged.
- **BoardPreview.cs** — board world size unchanged, preview scale stays (4, 0.1, 4).
- **BuildingSetupEditor.cs** — prefab generation unchanged, board scale stays (4, 0.1, 4).
- **TriggerInfo.cs** — data fields unchanged.
- **BuildingInventoryBridge.cs** — operates on grid positions, no coordinate math.
- **Inventory system** — no changes.
- **Player controllers / camera / UI** — no changes.

## File Change Summary

| File | Change |
|---|---|
| **New: `GridConstants.cs`** | `CELL_SIZE=2f`, `BOARD_SPAN=2`, `GetOccupiedCells()` |
| **`BoardAdjacency.cs`** | Rewrite offset tables for 2-unit cells |
| **`BuildingGrid.cs`** | Multi-cell add/remove, anchor map, `GetAnchor()` |
| **`PlacementTriggerManager.cs`** | Use `GridConstants.CELL_SIZE`, update `GridToWorld` |
| **`BuildingController.cs`** | Use `GridConstants.CELL_SIZE`, simplify removal |

## Testing Strategy

1. Place a single board and verify it registers in 4 grid cells.
2. Place two adjacent boards and verify triggers appear at all valid neighbor positions.
3. Remove a board and verify all 4 cells are cleared and triggers update correctly.
4. Place boards of all 3 orientations meeting at an edge and verify adjacency works.
5. Place a FullCell and verify 8-cell registration and trigger generation.
6. Verify existing inventory integration still works (place/remove with item consumption/refund).
