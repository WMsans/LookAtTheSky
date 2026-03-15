# Board Building System Design (v3)

Third iteration of the Rust-like building system. Previous attempts on `feat/building` (6-face model) and `feat/grid-building` (3-orientation with triggers) had bugs in the adjacency/connection logic. This design fixes that with hardcoded adjacency tables derived from geometry.

## Summary

Players place flat boards on a 3D grid. All boards are identical regardless of orientation. Boards must connect to an existing board by sharing a full edge. Placement positions are represented by trigger colliders that the player raycasts against.

## Parameters

| Parameter | Value |
|---|---|
| Cell size | 4 units |
| Board dimensions | 4 x 0.1 x 4 (world units) |
| Max placement distance | 16 units |
| First-board distance | 8 units from camera |
| Trigger collider size | 4 x 0.1 x 4 (matches board) |
| Camera | First-person |
| Input | New Input System (left click = place, right click = remove) |

## Grid Data Model

The world is divided into a virtual grid with 4-unit cell size. Cell `(x,y,z)` occupies world space from `(4x, 4y, 4z)` to `(4(x+1), 4(y+1), 4(z+1))`.

### Board Orientations

```csharp
[Flags]
public enum BoardOrientation
{
    None = 0,
    X = 1,  // Board from (x,y,z) to (x+1,y+1,z) — XY plane, vertical
    Y = 2,  // Board from (x,y,z) to (x,y+1,z+1) — YZ plane, vertical
    Z = 4   // Board from (x,y,z) to (x+1,y,z+1) — XZ plane, horizontal
}
```

Each grid cell stores up to 3 boards using bit flags.

### Storage

```csharp
Dictionary<Vector3Int, BoardOrientation>  // bit flags per cell
Dictionary<Vector3Int, Dictionary<BoardOrientation, GameObject>>  // board registry
```

### World Positions

Board center positions for cell `(x,y,z)`:

- **X-board** (XY plane): `(4x + 2, 4y + 2, 4z)`, rotation faces Z axis
- **Y-board** (YZ plane): `(4x, 4y + 2, 4z + 2)`, rotation faces X axis
- **Z-board** (XZ plane): `(4x + 2, 4y, 4z + 2)`, rotation faces Y axis

## Adjacency Tables

Each board has 12 neighbors: 4 coplanar (same orientation), 4 perpendicular on one side, 4 perpendicular on the other side. A neighbor is valid if the two boards share an entire edge.

### Z-board at (0,0,0)

Corners: `(0,0,0), (1,0,0), (1,0,1), (0,0,1)` (XZ plane)

| # | Neighbor | Shared Edge |
|---|---|---|
| 1 | Z at (-1,0,0) | (0,0,0)-(0,0,1) left |
| 2 | Z at (1,0,0) | (1,0,0)-(1,0,1) right |
| 3 | Z at (0,0,-1) | (0,0,0)-(1,0,0) back |
| 4 | Z at (0,0,1) | (0,0,1)-(1,0,1) front |
| 5 | X at (0,-1,0) | (0,0,0)-(1,0,0) below-back |
| 6 | X at (0,-1,1) | (0,0,1)-(1,0,1) below-front |
| 7 | Y at (0,-1,0) | (0,0,0)-(0,0,1) below-left |
| 8 | Y at (1,-1,0) | (1,0,0)-(1,0,1) below-right |
| 9 | X at (0,0,0) | (0,0,0)-(1,0,0) above-back |
| 10 | X at (0,0,1) | (0,0,1)-(1,0,1) above-front |
| 11 | Y at (0,0,0) | (0,0,0)-(0,0,1) above-left |
| 12 | Y at (1,0,0) | (1,0,0)-(1,0,1) above-right |

### X-board at (0,0,0)

Corners: `(0,0,0), (1,0,0), (1,1,0), (0,1,0)` (XY plane)

| # | Neighbor | Shared Edge |
|---|---|---|
| 1 | X at (-1,0,0) | (0,0,0)-(0,1,0) left |
| 2 | X at (1,0,0) | (1,0,0)-(1,1,0) right |
| 3 | X at (0,-1,0) | (0,0,0)-(1,0,0) bottom |
| 4 | X at (0,1,0) | (0,1,0)-(1,1,0) top |
| 5 | Z at (0,0,-1) | (0,0,0)-(1,0,0) z- bottom |
| 6 | Z at (0,1,-1) | (0,1,0)-(1,1,0) z- top |
| 7 | Y at (0,0,-1) | (0,0,0)-(0,1,0) z- left |
| 8 | Y at (1,0,-1) | (1,0,0)-(1,1,0) z- right |
| 9 | Z at (0,0,0) | (0,0,0)-(1,0,0) z+ bottom |
| 10 | Z at (0,1,0) | (0,1,0)-(1,1,0) z+ top |
| 11 | Y at (0,0,0) | (0,0,0)-(0,1,0) z+ left |
| 12 | Y at (1,0,0) | (1,0,0)-(1,1,0) z+ right |

### Y-board at (0,0,0)

Corners: `(0,0,0), (0,1,0), (0,1,1), (0,0,1)` (YZ plane)

| # | Neighbor | Shared Edge |
|---|---|---|
| 1 | Y at (0,0,-1) | (0,0,0)-(0,1,0) back |
| 2 | Y at (0,0,1) | (0,0,1)-(0,1,1) front |
| 3 | Y at (0,-1,0) | (0,0,0)-(0,0,1) bottom |
| 4 | Y at (0,1,0) | (0,1,0)-(0,1,1) top |
| 5 | Z at (-1,0,0) | (0,0,0)-(0,0,1) x- bottom |
| 6 | Z at (-1,1,0) | (0,1,0)-(0,1,1) x- top |
| 7 | X at (-1,0,0) | (0,0,0)-(0,1,0) x- back |
| 8 | X at (-1,0,1) | (0,0,1)-(0,1,1) x- front |
| 9 | Z at (0,0,0) | (0,0,0)-(0,0,1) x+ bottom |
| 10 | Z at (0,1,0) | (0,1,0)-(0,1,1) x+ top |
| 11 | X at (0,0,0) | (0,0,0)-(0,1,0) x+ back |
| 12 | X at (0,0,1) | (0,0,1)-(0,1,1) x+ front |

## Component Architecture

### BoardOrientation.cs

Flags enum: `None = 0, X = 1, Y = 2, Z = 4`.

### BoardAdjacency.cs

Static class with the 3 hardcoded adjacency tables. Single method: `GetNeighbors(BoardOrientation orient)` returns a `ReadOnlySpan<(Vector3Int offset, BoardOrientation orient)>` of 12 entries. Used by both the validator and the trigger manager so adjacency logic lives in one place.

### BuildingGrid.cs

Singleton MonoBehaviour. Owns grid dictionary and board registry.

Methods: `HasBoard`, `AddBoard`, `RemoveBoard`, `GetBoard`, `IsEmpty`.

Events: `OnBoardAdded(Vector3Int, BoardOrientation)`, `OnBoardRemoved(Vector3Int, BoardOrientation)`.

Pure data -- no raycasting, no input, no visuals.

### PlacementTriggerManager.cs

MonoBehaviour. Listens to grid events.

On board added: removes trigger at that position, then for each of the 12 neighbors from the adjacency table, creates a board-sized trigger collider (4x0.1x4) if no board and no trigger exists there.

On board removed: creates a trigger at the removed position if it has any adjacent board, then runs orphan cleanup -- removes any trigger that has no adjacent board.

Triggers are on the "PlacementTrigger" layer and carry a `TriggerInfo` component.

### TriggerInfo.cs

MonoBehaviour on trigger GameObjects. Stores `Vector3Int gridPosition` and `BoardOrientation orientation`.

### BoardPreview.cs

Single reusable preview GameObject (scaled cube 4x0.1x4). Updated each frame to match current target. Green semi-transparent material when showing. Hidden when no target. Not on any physics layer (does not block raycasts).

### BuildingController.cs

MonoBehaviour on the player. Each frame:

1. If grid is empty: compute target at fixed distance (8 units) in front of camera, snapped to nearest grid cell, orientation auto-selected based on camera facing direction (axis most aligned with camera forward).
2. If grid is not empty: raycast from camera center, max 16 units, against PlacementTrigger and Board layers.
   - Hit trigger: show preview, left-click places board.
   - Hit board: right-click removes board.
   - Hit nothing: hide preview.

## Placement Flow

### First Board (Grid Empty)

```
Camera forward * 8m → snap to grid → auto-select orientation
  → Preview at snapped position
  → Left click → BuildingGrid.AddBoard(pos, orient)
    → OnBoardAdded → TriggerManager generates 12 neighbor triggers
```

### Subsequent Boards

```
Raycast hits trigger → read TriggerInfo(pos, orient)
  → Preview at trigger position
  → Left click → BuildingGrid.AddBoard(pos, orient)
    → OnBoardAdded fires
    → TriggerManager: remove trigger at placed position
    → TriggerManager: generate triggers at valid empty neighbor positions
```

### Removal

```
Raycast hits board on Board layer
  → Right click → find grid pos/orient from board
    → BuildingGrid.RemoveBoard(pos, orient)
    → OnBoardRemoved fires
    → TriggerManager: create trigger at removed position if adjacent boards exist
    → TriggerManager: cleanup orphaned triggers (no adjacent boards)
    → Board GameObject destroyed
```

## Layers

- **Board** (layer 8): placed board GameObjects, for removal raycast.
- **PlacementTrigger** (layer 9): trigger colliders, for placement raycast.

## Input Actions

Modify `InputSystem_Actions.inputactions`:
- Reuse **Attack** (left mouse) for placement.
- Add **Remove** (right mouse) for removal.
- Existing **Move**, **Look**, **Jump** for FPS controller.

## Materials & Prefabs (Generated by Editor Script)

- `Board.prefab`: Cube scaled to (4, 0.1, 4), BoxCollider, layer "Board"
- `BoardMaterial.mat`: Opaque URP/Lit, tan (0.7, 0.6, 0.5)
- `BoardPreviewValid.mat`: Transparent green (0, 1, 0, 0.4)

## Error Handling

- Placing on occupied position: silently ignored (triggers don't exist at occupied positions).
- Removing non-existent board: silently ignored.
- No structural integrity checks in this iteration. Free removal.
- No explicit grid bounds. Dictionary grows dynamically. Negative coordinates supported.

## Future Considerations (Not In Scope)

- Structural integrity: splitting disconnected structures into separate rigidbodies (space setting).
- Board materials/textures.
- Multiplayer synchronization.
- Save/load.
- Performance optimization for large structures (trigger pooling, spatial partitioning).

## File Structure

```
Assets/Scripts/
  Building/
    BoardOrientation.cs
    BoardAdjacency.cs
    BuildingGrid.cs
    PlacementTriggerManager.cs
    TriggerInfo.cs
    BoardPreview.cs
    BuildingController.cs
  Player/
    FirstPersonController.cs
  Editor/
    BuildingSetupEditor.cs
```
