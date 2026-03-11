# Board Building System Design

## Overview

A Rust-like building system where players can place boards in a 3D grid. All boards are identical regardless of orientation (wall, ceiling, floor are the same object). Boards must connect to existing structures or be placed on the ground.

## Core Requirements

- Grid-based placement with 4-unit cell size
- Three board orientations per grid cell (X, Y, Z planes)
- Connection rule: boards must share at edge with existing board or touch ground
- Visual preview before placement
- Removal support
- Trigger collider system for placement detection via raycast

## Data Structures

### BoardOrientation (Enum)

```csharp
[Flags]
public enum BoardOrientation
{
    None = 0,
    X = 1,  // Board from (x,y,z) to (x+1,y+1,z)
    Y = 2,  // Board from (x,y,z) to (x,y+1,z+1)
    Z = 4   // Board from (x,y,z) to (x+1,y,z+1)
}
```

Each grid cell can store up to 3 boards (one per orientation) using bit flags.

### Grid Storage

- Dictionary<Vector3Int, BoardOrientation> stores occupied boards
- Vector3Int represents grid cell coordinate
- World position = gridCoord * 4 (4-unit cell size)
- No bounds, grows dynamically with negative coordinates supported

### Board Data

Each board stores:
- Grid position (Vector3Int)
- Orientation (BoardOrientation)
- Reference to spawned GameObject (in separate registry)

## Component Architecture

### BuildingGrid (Singleton MonoBehaviour)

**Responsibility:** Central data storage for all placed boards

**Data:**
- Dictionary<Vector3Int, BoardOrientation> grid
- Dictionary<Vector3Int, Dictionary<BoardOrientation, GameObject>> boardRegistry

**Methods:**
- HasBoard(Vector3Int pos, BoardOrientation orient): bool
- AddBoard(Vector3Int pos, BoardOrientation orient): void
- RemoveBoard(Vector3Int pos, BoardOrientation orient): void
- GetBoard(Vector3Int pos, BoardOrientation orient): GameObject
- GetAdjacentBoards(Vector3Int pos, BoardOrientation orient): List of (position, orientation)
- Clear(): void

**Events:**
- OnBoardAdded(Vector3Int pos, BoardOrientation orient)
- OnBoardRemoved(Vector3Int pos, BoardOrientation orient)

**Access:** BuildingGrid.Instance

### PlacementValidator (Static Utility Class)

**Responsibility:** Validate placement rules and connection logic

**Methods:**
- CanPlaceAt(BuildingGrid grid, Vector3Int pos, BoardOrientation orient): bool
- CanPlaceOnGround(Vector3 worldPos): bool
- GetConnectedBoards(BuildingGrid grid, Vector3Int pos, BoardOrientation orient): List<(Vector3Int, BoardOrientation)>
- HasAnyConnection(BuildingGrid grid, Vector3Int pos, BoardOrientation orient): bool

**Connection Rules:**
- 12 potential adjacent positions: 4 same-level, 4 above, 4 below
- Edge-sharing: boards share at least one edge (not just corner/vertex)
- First placement exception: must be on ground

### PlacementTriggerManager (MonoBehaviour)

**Responsibility:** Generate and manage trigger colliders for valid placement positions

**Data:**
- Dictionary<Vector3Int, Dictionary<BoardOrientation, GameObject>> triggers
- Reference to BuildingGrid singleton

**Methods:**
- GenerateTriggersForBoard(Vector3Int pos, BoardOrientation orient): void
- RemoveTrigger(Vector3Int pos, BoardOrientation orient): void
- CleanupOrphanedTriggers(): void

**Trigger Properties:**
- Small box collider (size ~0.5 units)
- Centered on board's center position
- Layer: "PlacementTrigger" (setup via BuildingSetupEditor)
- Each trigger has TriggerInfo component storing: gridPosition, orientation

**Event Handling:**
- Listen to BuildingGrid.OnBoardAdded → generate adjacent triggers, remove trigger at placed position
- Listen to BuildingGrid.OnBoardRemoved → cleanup orphaned triggers

### TriggerInfo (MonoBehaviour)

**Responsibility:** Store metadata on trigger colliders

**Data:**
- Vector3Int gridPosition
- BoardOrientation orientation

### BoardPreview (MonoBehaviour)

**Responsibility:** Visual preview of board at current target position

**Data:**
- GameObject previewInstance (single reused instance)
- Material validMaterial (semi-transparent green)
- Material invalidMaterial (semi-transparent red)

**Methods:**
- ShowPreview(Vector3 worldPos, Quaternion rotation, bool isValid): void
- HidePreview(): void

**Behavior:**
- Single preview object, moved each frame to current target
- Enabled/disabled based on valid target detection
- Material changes based on validity (future use)

### BuildingController (MonoBehaviour on Player)

**Responsibility:** Handle player input and coordinate building system

**Data:**
- LayerMask placementTriggerLayer
- LayerMask boardLayer
- LayerMask groundLayer
- float maxPlacementDistance
- Reference to BuildingGrid singleton
- Reference to BoardPreview component

**Input (Unity Input System):**
- Left click: place board
- Right click: remove board

**Methods:**
- Update(): raycast to detect trigger/board, update preview
- HandlePlacement(): place board at current target
- HandleRemoval(): remove board at raycast hit

## Placement Flow

### Initial Placement (Ground)

1. Player raycasts from camera toward ground
2. Ray hits ground terrain
3. Calculate grid position from hit point (snap to nearest 4-unit grid)
4. Determine orientation based on hit normal (floor on horizontal, wall on vertical)
5. PlacementValidator.CanPlaceOnGround() checks validity
6. PlacementValidator.CanPlaceAt() checks no existing board
7. BuildingGrid.AddBoard() called
8. OnBoardAdded event fires
9. PlacementTriggerManager generates triggers for adjacent positions
10. Board GameObject spawned and registered

### Subsequent Placements

1. PlacementTriggerManager has triggers at all valid positions
2. Player raycasts from camera
3. Ray hits trigger collider (on PlacementTrigger layer)
4. Read TriggerInfo component for position/orientation
5. BoardPreview.ShowPreview() at trigger position
6. On left click, BuildingGrid.AddBoard() called
7. OnBoardAdded event fires
8. PlacementTriggerManager: removes trigger at placed position, generates new adjacent triggers
9. Board GameObject spawned and registered

### Removal

1. Player raycasts from camera
2. Ray hits board GameObject (on Board layer, not trigger)
3. Calculate grid position from hit point
4. On right click, BuildingGrid.RemoveBoard() called
5. OnBoardRemoved event fires
6. PlacementTriggerManager cleans up orphaned triggers
7. Board GameObject destroyed and unregistered

## Trigger Management Details

### Generation Strategy

When board added at position P with orientation O:
1. Calculate 12 adjacent positions (4 same-level, 4 above, 4 below)
2. For each adjacent position:
   - Check if board already exists at that position + orientation
   - If not, create trigger GameObject with box collider
   - Add TriggerInfo component with position/orientation
   - Store in triggers dictionary

### Cleanup Strategy

When board removed:
1. Get all 12 adjacent trigger positions
2. For each trigger position:
   - Check if it still has any adjacent board (any orientation)
   - If no adjacent boards, remove trigger
3. This prevents orphaned triggers floating in space

### Storage

Dictionary<Vector3Int, Dictionary<BoardOrientation, GameObject>> triggers:
- Outer key: grid cell position
- Inner key: board orientation
- Value: trigger GameObject

## Board Spawning & Visuals

### Board Prefab (Generated by BuildingSetupEditor)

- Primitive cube scaled to 4x0.1x4
- BoxCollider (non-trigger, for removal raycast)
- MeshRenderer with BoardMaterial
- Layer: "Board"

### Materials (Generated by BuildingSetupEditor)

- BoardMaterial: Opaque, tan/brown color (0.7, 0.6, 0.5, 1.0)
- BoardPreviewValid: Transparent green (0, 1, 0, 0.4)
- BoardPreviewInvalid: Transparent red (1, 0, 0, 0.4)

### BoardRegistry

Part of BuildingGrid, maps (position, orientation) → GameObject:
- Used for removal (find object to destroy)
- Used for collision detection
- Updated on add/remove operations

### Preview System

- Single preview GameObject instance
- Reused each frame (not instantiated repeatedly)
- Moved to target position when player looks at trigger
- Hidden when no valid target

## Editor Setup (BuildingSetupEditor)

Located at: Assets/Scripts/Editor/BuildingSetupEditor.cs

**Menu Items:**
- Tools/Building System/Setup All: Runs all setup steps
- Tools/Building System/Setup Layers: Creates "Board" layer at index 8+
- Tools/Building System/Generate Materials: Creates board and preview materials
- Tools/Building System/Generate Board Prefab: Creates board prefab with collider

**Setup Requirements:**
1. Add "PlacementTrigger" layer (manually or extend editor script)
2. Materials created in Assets/Materials/
3. Prefab created in Assets/Prefabs/Board.prefab

## Error Handling

### Invalid Placements

- Board exists at position + orientation: silently ignored
- No ground detected: no action
- Player too far from target: no action (optional maxDistance check)

### Removal Edge Cases

- Removing board with connected boards: allowed (no structural integrity)
- Removing last board: allowed, all triggers cleared
- Non-existent board removal: silently ignored

### Trigger Cleanup

- Orphaned triggers (no adjacent boards) are removed on board removal
- Prevents floating triggers in empty space

### Grid Bounds

- No explicit bounds
- Dictionary grows dynamically
- Negative coordinates supported (building below ground)

## Testing Approach

### Manual Test Scenarios

1. **First placement:** Place board on ground → verify trigger appears
2. **Adjacent placement:** Place board next to first → verify triggers update
3. **Structure building:** Build floor + walls + ceiling
4. **Removal:** Remove boards in various orders
5. **Orientation test:** Test X, Y, Z orientations
6. **Orphan cleanup:** Remove middle board → verify orphaned triggers removed

### Validation Checks

- No overlapping boards at same position + orientation
- Triggers only at valid connected positions
- Preview at correct position/orientation
- Removal affects only targeted board

### Debug Visualization (Optional)

- Gizmos for grid lines in Scene view
- Debug.Log for placement/removal events
- OnDrawGizmos for trigger positions

## File Structure

```
Assets/
├── Scripts/
│   ├── Building/
│   │   ├── BuildingGrid.cs
│   │   ├── PlacementValidator.cs
│   │   ├── PlacementTriggerManager.cs
│   │   ├── TriggerInfo.cs
│   │   ├── BoardPreview.cs
│   │   └── BuildingController.cs
│   └── Editor/
│       └── BuildingSetupEditor.cs
├── Materials/
│   ├── BoardMaterial.mat
│   ├── BoardPreviewValid.mat
│   └── BoardPreviewInvalid.mat
└── Prefabs/
    └── Board.prefab
```

## Performance Considerations

- Trigger updates only on board add/remove (not every frame)
- Preview object reused (not instantiated repeatedly)
- Dictionary lookups O(1) for grid operations
- Suitable for small-medium structures (up to 1000 boards)
