# Building System Design

## Summary

A Rust-style building system where the player places and removes flat board pieces that snap to a 3D grid. Boards can serve as floors, walls, or ceilings -- there is no distinction between them. The only placement constraint is that a new board must be adjacent to an existing board (except the very first board, which can go anywhere on the ground).

## Parameters

| Parameter | Value |
|---|---|
| Grid cell size | 4m x 4m x 4m |
| Board size | 4m x 4m flat quad |
| Faces per cell | 6 (Top, Bottom, North, South, East, West) |
| Placement range | 16 meters from camera |
| Camera | First-person |
| Input | New Input System (left click = place, right click = remove) |

## Grid Data Model

The world is divided into a virtual grid of 4m cubic cells. Cell `(0,0,0)` starts at world origin. Cell `(x, y, z)` occupies world space from `(x*4, y*4, z*4)` to `((x+1)*4, (y+1)*4, (z+1)*4)`.

Each cell has 6 faces:

```csharp
enum BoardFace { Top = 0, Bottom = 1, North = 2, South = 3, East = 4, West = 5 }
```

Boards are stored in a dictionary keyed by `(Vector3Int cell, BoardFace face)`.

### Shared Face Normalization

A face between two adjacent cells is stored once under a canonical key. The canonical form always uses the cell with the lower coordinate value along the relevant axis:

- Top of `(1,0,0)` and Bottom of `(1,1,0)` are the same face. Stored as `(cell: (1,0,0), face: Top)`.
- North of `(1,0,0)` and South of `(1,0,1)` are the same face. Stored as `(cell: (1,0,0), face: North)`.
- East of `(1,0,0)` and West of `(2,0,0)` are the same face. Stored as `(cell: (1,0,0), face: East)`.

This prevents double-placement on shared boundaries.

### Neighbor Constraint

A board at `(cell, face)` is valid if at least one adjacent face already has a board. Adjacent faces include:

1. Other faces of the same cell (e.g., placing a wall next to an existing floor in the same cell).
2. The same face on an adjacent cell (e.g., extending a floor to the neighboring cell).
3. Faces of adjacent cells that share an edge with this face.

The first board placed is exempt from this constraint.

## Placement Flow

Each frame:

1. Raycast from camera center forward, max 16m. Hit targets: existing boards (on "Board" layer) and a ground plane collider at y=0.
2. From the hit point and normal, compute the target `(cell, face)`:
   - If hitting the ground plane: cell is at `FloorToInt(hitPoint / 4)` with y=0, face is Top.
   - If hitting an existing board: use the hit normal to determine the face of the adjacent cell on the other side of the board.
3. Normalize the `(cell, face)` to canonical form.
4. Check validity: face not occupied AND has at least one neighbor (or grid is empty).
5. Show preview board at the snap position with green (valid) or red (invalid) material.
6. On left click, if valid: instantiate board prefab, add to dictionary.

## Removal Flow

1. Raycast from camera center, max 16m, targeting only the "Board" layer.
2. On right click: look up the board in the dictionary, remove it, destroy the GameObject.
3. No structural integrity checks -- any board can be removed freely.

## Preview

A single reusable GameObject with a MeshRenderer. Updated every frame to match the current target position and rotation. Uses a semi-transparent green material when placement is valid, red when invalid. Hidden when the player is not aiming at a valid grid position.

## Component Architecture

### Scripts

**`BuildingGrid.cs`** -- Singleton MonoBehaviour. Owns the dictionary. Methods: `TryPlace(cell, face) -> bool`, `Remove(cell, face)`, `HasBoard(cell, face) -> bool`, `HasNeighbor(cell, face) -> bool`, `IsEmpty() -> bool`. Handles canonical face normalization. Pure data and instantiation logic, no input or raycasting.

**`BuildingSystem.cs`** -- MonoBehaviour on the player. References camera and BuildingGrid. Each frame: raycasts, computes target, updates preview, handles place/remove input via New Input System.

**`BoardVisuals.cs`** -- Static utility. Methods: `GetWorldPosition(cell, face) -> Vector3`, `GetWorldRotation(face) -> Quaternion`. Translates grid coordinates to world transforms.

**`FirstPersonController.cs`** -- MonoBehaviour on the player. WASD movement via CharacterController, mouse look, gravity. Uses existing Move and Look actions from the Player action map. Locks cursor.

### Prefabs

- **Board.prefab** -- 4m x 4m flat quad with a BoxCollider. Layer: "Board".
- **Player** -- GameObject with Camera (child), CharacterController, FirstPersonController, BuildingSystem.

### Materials

- `BoardMaterial.mat` -- Opaque material for placed boards.
- `BoardPreviewValid.mat` -- Semi-transparent green for valid placement preview.
- `BoardPreviewInvalid.mat` -- Semi-transparent red for invalid placement preview.

### Layers

- "Board" -- for placed boards (raycast filtering for removal).

### Input Actions

Modify existing `InputSystem_Actions.inputactions`:

- Reuse `Attack` action (left mouse button) for placement.
- Add `Remove` action (right mouse button) to the Player action map.
- Existing `Move` and `Look` actions used for FPS controller.

## Edge Cases

1. **First board:** When grid is empty, skip neighbor check. Board can be placed anywhere on the ground plane.
2. **Aiming at nothing:** Hide preview when raycast hits nothing.
3. **Aiming at ground:** Raycast hits ground plane collider at y=0. Target is the Top face of the cell at ground level.
4. **Aiming at existing board:** Hit normal determines which adjacent cell face to target, allowing outward building.
5. **Occupied face:** Dictionary lookup prevents double-placement. Preview shows red.
6. **Ground plane:** Invisible plane collider at y=0 catches raycasts for ground-level placement.
