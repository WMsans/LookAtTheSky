# Small Block Design

## Overview

Add a "small block" building piece that occupies a single grid cell (2x2x2 world units). Unlike boards, small blocks are placed by raycasting directly against existing geometry rather than using placement triggers. They can co-exist with boards in the same cell. Their rotation is determined Minecraft-style: bottom face toward the raycasted surface, front face toward the player.

## Grid Storage Refactor

### Motivation

The current grid stores per-cell `BoardOrientation` flags. Small blocks need to co-exist with boards in the same cell, which the flag model can't express cleanly. We refactor to a unified occupant model.

### New Types

```csharp
public enum OccupantType { Board, SmallBlock }

public interface IOccupant
{
    OccupantType Type { get; }
    Vector3Int Anchor { get; }
    GameObject GameObject { get; }
    Vector3Int[] GetOccupiedCells();
}
```

**BoardOccupant** wraps existing board data:
- `Anchor`: min-corner grid position
- `Orientation`: `BoardOrientation` (X, Y, Z, or FULL)
- `GetOccupiedCells()`: delegates to `GridConstants.GetOccupiedCells(anchor, orient)`
- Multi-cell: 4 cells for panels, 8 for FullCell

**SmallBlockOccupant** is new:
- `Anchor` = single cell position
- `Rotation`: a `Quaternion` (one of 24 discrete orientations)
- `GetOccupiedCells()`: returns `{ Anchor }`

### Storage Change

Replace the three dictionaries in `BuildingGrid`:

```
OLD:
  _grid:          Dict<Vector3Int, BoardOrientation>           -- flags per cell
  _boardRegistry: Dict<Vector3Int, Dict<BoardOrientation, GO>> -- anchor -> GO
  _anchorMap:     Dict<(Vector3Int, BoardOrientation), V3I>    -- cell -> anchor

NEW:
  _cellOccupants:    Dict<Vector3Int, List<IOccupant>> -- per-cell occupant list
  _occupantRegistry: HashSet<IOccupant>                -- all unique occupants
```

### Co-existence Rules

A cell can contain:
- Multiple boards at different orientations (X + Y + Z, same as before)
- A board and a small block (new)
- At most one small block

Validation:
- **Board placement**: check no conflicting board orientation exists in any occupied cell (same logic as current, querying occupant list for `BoardOccupant` instances)
- **Small block placement**: check no other `SmallBlockOccupant` exists in the target cell

### Events

```
OLD: OnBoardAdded(Vector3Int, BoardOrientation)
     OnBoardRemoved(Vector3Int, BoardOrientation)

NEW: OnOccupantAdded(IOccupant)
     OnOccupantRemoved(IOccupant)
```

`PlacementTriggerManager` subscribes and filters for `OccupantType.Board` only.

## Small Block Placement

### PlacementMode Extension

```csharp
public enum PlacementMode
{
    Oriented,   // boards via placement triggers
    FullCell,   // full-cell blocks via placement triggers
    SmallBlock  // small blocks via direct raycast
}
```

### Targeting Flow

When `ActivePlacementMode == SmallBlock`, `BuildingController` uses a different targeting path:

1. **Raycast** from camera forward against `boardLayer` (shared by boards and small blocks), max 16 units.
2. **Compute target cell**: `WorldToGrid(hit.point + hit.normal * 0.5f)`. The half-cell nudge along the surface normal places the block in the adjacent empty cell outside the hit surface.
3. **Adjacency validation**: at least one of the 6 face-neighbors of the target cell must contain a board or small block.
4. **Co-existence validation**: target cell must not already contain a small block.
5. **Compute rotation**: `SmallBlockRotation.ComputeRotation(hit.normal, playerPos, blockWorldPos)`.

No placement triggers are generated for small blocks.

### Preview

`BoardPreview.ShowAt(position, rotation)` already accepts arbitrary position and rotation. No changes needed.

## Rotation: 24 Discrete Orientations

### Algorithm

```
bottomDir = -hit.normal, snapped to nearest cardinal axis
toPlayer  = normalize(playerPosition - blockWorldPosition)
frontDir  = project toPlayer onto plane perpendicular to bottomDir
frontDir  = snap to nearest cardinal axis in that plane
rotation  = Quaternion.LookRotation(frontDir, -bottomDir)
```

- **Bottom face** (`-Y` local): points into the raycasted surface. Snapping the normal to the nearest axis handles floating-point imprecision.
- **Front face** (`+Z` local): points toward the player, constrained to the plane perpendicular to the bottom direction, snapped to one of 4 cardinal axes in that plane.
- **Result**: 6 bottom directions x 4 front directions = 24 orientations.

### Utility Class

`SmallBlockRotation` static class with:
- `ComputeRotation(Vector3 hitNormal, Vector3 playerPosition, Vector3 blockWorldPosition) -> Quaternion`
- `SnapToAxis(Vector3 dir) -> Vector3` (nearest of 6 cardinal directions)
- `ProjectAndSnapToPlane(Vector3 dir, Vector3 planeNormal) -> Vector3`

## Removal

Same raycast-against-`boardLayer` approach as current board removal:

1. Raycast against `boardLayer`
2. `WorldToGrid(hit.point)` -> cell
3. Query `_grid.GetOccupants(cell)`, find occupant whose `GameObject == hit.collider.gameObject`
4. Remove via `_grid.RemoveOccupant(occupant)`
5. Keep the 3x3x3 neighbor fallback for cell-boundary edge cases

## Small Block Prefab

Placeholder: a 2x2x2 world-unit cube.
- `BoxCollider` size `(2, 2, 2)`
- `Board` layer (shared with boards)
- Unit cube mesh scaled to `(2, 2, 2)`

### World Position

A small block at cell `p` has world-space center:
```
center = p * CELL_SIZE + (CELL_SIZE/2, CELL_SIZE/2, CELL_SIZE/2)
       = p * 2 + (1, 1, 1)
```

This differs from board `GridToWorld` which offsets by `BOARD_WORLD_SIZE/2` along planar axes. A `SmallBlockGridToWorld` helper (or occupant-type-aware `GridToWorld`) is needed.

## File Changes

### New Files
- `IOccupant.cs` -- interface + OccupantType enum
- `BoardOccupant.cs` -- board occupant implementation
- `SmallBlockOccupant.cs` -- small block occupant implementation
- `SmallBlockRotation.cs` -- rotation computation utility

### Modified Files
- `BuildingGrid.cs` -- replace 3 dictionaries with occupant model, update all methods and events
- `BuildingController.cs` -- add small block targeting path, update placement/removal to use occupants
- `PlacementTriggerManager.cs` -- subscribe to new events, filter for boards only
- `PlacementMode.cs` -- add SmallBlock value
- `GridConstants.cs` -- add SmallBlockGridToWorld helper
- `BuildingInventoryBridge.cs` -- update event signatures to use IOccupant

### Unchanged Files
- `BoardOrientation.cs`
- `BoardAdjacency.cs`
- `TriggerInfo.cs`
- `BoardPreview.cs`
- `BuildingItemSO.cs`
