# Inventory System Design

## Overview

A Minecraft-style inventory system integrated with the existing building system. Players have a hotbar (9 slots) and a full inventory grid (27 slots) with item stacking, drag/swap interaction, and building integration where placing blocks consumes items and removing blocks restores them.

## Requirements

- 9-slot hotbar (always visible) + 27-slot inventory grid (toggleable panel)
- Fixed max stack size per item type (default 64)
- Minecraft-style drag/swap slot interaction: pick up, place, merge, split
- ScriptableObject-based item definitions — adding new items requires no code changes
- Building integration: placing consumes 1 item, removing restores 1 item
- Multiple building piece types with different prefabs and placement modes
- Orientation-aware placement: some items use X/Y/Z panel orientation, others occupy full cells
- Decoupled architecture: inventory, building, and mouse management are independent systems
- Reference-counted mouse/cursor manager for UI state

## Architecture

Three independent systems connected by a thin mediator:

```
InventoryManager <--events--> BuildingInventoryBridge <--events--> BuildingController
                                       |
MouseManager <---events---> InventoryPanelUI, PlayerControllers, BuildingController
```

No system references another directly except the bridge.

## Item Definition

```csharp
[CreateAssetMenu(menuName = "Building/Building Item")]
public class BuildingItemSO : ScriptableObject
{
    public string itemName;
    public Sprite icon;
    public int maxStackSize = 64;
    public GameObject prefab;
    public PlacementMode placementMode;
}

public enum PlacementMode
{
    Oriented,  // Uses existing X/Y/Z orientation (panels, ramps)
    FullCell   // Occupies entire cell, no orientation (blocks, pillars)
}
```

Each item type is a ScriptableObject asset in `Assets/Items/`. New items are added by creating a new SO and assigning a prefab — no code changes required.

## Inventory Data Model

Pure C# data classes with no MonoBehaviour or UI dependency.

### ItemSlot

Holds one stack of items:
- `BuildingItemSO item` — null when empty
- `int count` — 0 when empty
- Methods: `CanAccept(item)`, `AddToStack(count)`, `RemoveFromStack(count)`, `Swap(otherSlot)`

### Inventory

Manages the full slot collection:
- `ItemSlot[] hotbar` — 9 slots
- `ItemSlot[] mainGrid` — 27 slots (3 rows x 9 columns)
- `int activeHotbarIndex` — 0-8
- `event Action OnChanged`

Key methods:
- `AddItem(BuildingItemSO, count)` — fills existing stacks first, then empty slots. Returns overflow count
- `RemoveItem(BuildingItemSO, count)` — removes from any slot. Returns false if insufficient
- `GetActiveItem()` — returns item in `hotbar[activeHotbarIndex]`, or null
- `SetActiveSlot(int)` — changes active hotbar index

### InventoryManager (MonoBehaviour singleton)

Hosts the `Inventory` instance. Configures starting items via inspector (list of `BuildingItemSO` + count pairs populated on `Start`).

## Building System Integration

### BuildingInventoryBridge (MonoBehaviour)

The sole coupling point between inventory and building. Subscribes to events from both systems:

1. **`InventoryManager.OnActiveItemChanged`** — updates `BuildingController.ActivePrefab` and `ActivePlacementMode` from the selected item's SO. Sets null if empty slot.

2. **`BuildingController.OnBeforePlace`** — calls `inventory.RemoveItem(activeItem, 1)`. If insufficient stock, cancels placement.

3. **`BuildingController.OnBoardPlaced`** — records `(GameObject, BuildingItemSO)` in a tracking dictionary so removal knows which item to return.

4. **`BuildingGrid.OnBoardRemoved`** — looks up the removed GameObject in the tracking dictionary, calls `inventory.AddItem(itemSO, 1)`. If inventory is full, the item is lost (removal is never blocked).

### BuildingController Changes

New public API:
- `BuildingItemSO ActiveItem { set; }` — the item to place (null = disabled)
- `PlacementMode ActivePlacementMode { set; }` — oriented vs full cell
- `event Action<Vector3Int, BoardOrientation, CancelEventArgs> OnBeforePlace`
- `event Action<Vector3Int, BoardOrientation, GameObject> OnBoardPlaced`

When `ActiveItem` is null, placement is disabled (no preview, no raycast for triggers). Removal always works regardless of active item.

### Preview Changes

`BoardPreview` supports dynamic prefab switching. When the active item changes, it rebuilds its preview mesh from the new item's prefab geometry, applying the semi-transparent preview material.

## FullCell Grid Extension

`BoardOrientation.Full` = `X | Y | Z` = 7 (all flags set). Not a new flag value — it's the composite of all three.

Behavior:
- `AddBoard(pos, Full, obj)` sets all three flags, blocking any panel placement at that cell
- A cell with any existing panel cannot accept a FullCell item
- A FullCell item cannot be placed where any panel exists
- `BoardAdjacency.GetNeighbors(Full)` returns the deduplicated union of X, Y, and Z neighbor arrays

`PlacementTriggerManager` handles FullCell by treating it as occupying all 3 orientation slots and generating triggers for all unique neighbors from the union set.

## Inventory UI

Built with uGUI (Canvas, Image, TMP_Text) for consistency with the existing lock indicator UI.

### HotbarUI (always visible)

Bottom-of-screen horizontal bar with 9 `SlotUI` children. Each shows item icon, count, and selection highlight. Refreshes via `InventoryManager.OnChanged`. Hotbar slot selected via number keys 1-9 and scroll wheel (using existing `Previous`/`Next` input actions).

### InventoryPanelUI (toggleable)

Fullscreen panel toggled by Tab/E. Contains 27-slot main grid (3x9) plus 9 hotbar slots mirrored at the bottom. When open, cursor is unlocked and gameplay input is disabled. When closed, cursor is locked and gameplay resumes.

### Slot Interaction (Minecraft-style)

State machine with "held item" cursor state:
- **Left-click occupied slot**: pick up entire stack, attach to cursor
- **Left-click empty slot while holding**: drop held stack
- **Left-click occupied slot while holding**: merge if same type and room, swap if different
- **Right-click while holding**: place 1 item from held stack
- **Right-click occupied slot**: pick up half the stack (ceiling division)
- **Close panel while holding**: item returns to original slot

`CursorItem` component renders the held item icon at mouse position.

### Component Hierarchy

```
Canvas (Screen Space - Overlay)
├── HotbarUI
│   ├── SlotUI[0..8]
│   └── SelectionHighlight
├── InventoryPanelUI (toggled)
│   ├── MainGridUI
│   │   └── SlotUI[0..26]
│   ├── HotbarMirrorUI
│   │   └── SlotUI[0..8]
│   └── CursorItem
```

## Mouse Manager

`MouseManager` singleton with reference-counted cursor locking. Decoupled from all game systems.

```csharp
public class MouseManager : MonoBehaviour
{
    public void RequestUnlock(object requester);
    public void ReleaseLock(object requester);
    public bool IsCursorFree { get; }
    public event Action<bool> OnCursorStateChanged; // true = free
}
```

Cursor is locked when no requesters are active. Any system can request/release independently.

Integration via events:
- `InventoryPanelUI` requests unlock on open, releases on close
- `FirstPersonController` / `ZeroGPlayerController` disables look input when cursor is free
- `BuildingController` disables placement raycasting when cursor is free

## Error Handling

- **Empty hotbar slot**: no preview, placement disabled, removal still works
- **Insufficient stock**: placement cancelled via `OnBeforePlace` cancel mechanism
- **Inventory full on removal**: removal proceeds, item is lost (not blocked)
- **Panel/FullCell conflict**: grid flag checks prevent incompatible placement naturally
- **Item switch mid-placement**: preview immediately updates to new prefab
- **Close inventory while holding item**: item returns to original slot

## File Structure

### New files

```
Assets/Scripts/Inventory/
├── BuildingItemSO.cs
├── PlacementMode.cs
├── ItemSlot.cs
├── Inventory.cs
└── InventoryManager.cs

Assets/Scripts/UI/
├── SlotUI.cs
├── HotbarUI.cs
├── InventoryPanelUI.cs
├── CursorItem.cs
└── MouseManager.cs

Assets/Scripts/Building/
└── BuildingInventoryBridge.cs

Assets/Items/
└── Board.asset (ScriptableObject)
```

### Modified files

```
Assets/Scripts/Building/
├── BoardAdjacency.cs          — add GetNeighbors(Full) union logic
├── BuildingController.cs      — add ActiveItem, events, FullCell support
├── PlacementTriggerManager.cs — handle FullCell adjacency
└── BoardPreview.cs            — support dynamic prefab switching
```
