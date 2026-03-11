# Task 11: Manual Testing Guide

## Prerequisites

Before testing, ensure the following setup is complete:

### 1. Run Editor Setup
In Unity Editor, go to: **Tools > Building System > Setup All**

This will:
- Create required layers: `Board` and `PlacementTrigger`
- Generate materials in `Assets/Materials/`:
  - `BoardMaterial.mat` (solid brown)
  - `BoardPreviewValid.mat` (green transparent)
  - `BoardPreviewInvalid.mat` (red transparent)
- Generate `Assets/Prefabs/Board.prefab` (4x0.1x4 cube)

### 2. Create Resources Folder for Prefab
The Board prefab must be at: `Assets/Resources/Prefabs/Board.prefab`

If not there, move or copy the generated prefab.

### 3. Scene Setup
The scene must contain:
- **BuildingGrid** - A GameObject with `BuildingGrid` component (singleton)
- **PlacementTriggerManager** - A GameObject with `PlacementTriggerManager` component
- **Player** - A GameObject with:
  - `FirstPersonController` component
  - `BuildingController` component
  - `PlayerInput` component
  - Camera reference assigned
  - `BoardPreview` reference assigned
- **Ground** - A plane or terrain with:
  - Collider
  - Layer set to `Ground` (or configured in `groundLayer` on BuildingController)

### 4. Layer Configuration
Verify BuildingController has these layers assigned:
- `placementTriggerLayer` → `PlacementTrigger`
- `boardLayer` → `Board`
- `groundLayer` → `Ground`

### 5. Input System
Ensure Input System actions are configured:
- `Fire` → Left Click (for placement)
- `Fire2` → Right Click (for removal)

---

## Test 1: First Placement

### Steps
1. Enter Play Mode
2. Move player and look at the ground
3. Left click

### Expected Results
- [ ] Board appears on the ground at the clicked location
- [ ] Board is oriented flat (Z orientation)
- [ ] Green trigger colliders appear around the board (visible in Scene view)
- [ ] Console shows no errors

### Debug Checks
- In Scene view, verify trigger objects are created under `PlacementTriggerManager`
- Triggers should be named `Trigger_[pos]_[orient]`

---

## Test 2: Adjacent Placement

### Steps
1. Look at a trigger collider next to the placed board
2. Verify a green preview appears
3. Left click

### Expected Results
- [ ] Preview appears when looking at trigger (green if valid)
- [ ] New board appears at the trigger location
- [ ] Old trigger at that position is removed
- [ ] New triggers are generated around the new board
- [ ] Boards connect visually

---

## Test 3: Removal

### Steps
1. Aim at a placed board
2. Right click

### Expected Results
- [ ] Board disappears immediately
- [ ] Triggers update (orphaned triggers removed)
- [ ] Remaining boards stay in place

---

## Test 4: All Orientations

### Steps
1. Place boards at different positions to trigger X, Y, Z orientations
2. Verify each orientation displays correctly

### Expected Results
- [ ] **Z Orientation**: Board lies flat (default ground placement)
- [ ] **Y Orientation**: Board stands vertical (rotated 90° on X axis)
- [ ] **X Orientation**: Board stands vertical (rotated 90° on Z axis)
- [ ] Preview shows correct rotation for each orientation

### Notes
- Z orientation: `Quaternion.identity`
- Y orientation: `Quaternion.Euler(90°, 0°, 0°)`
- X orientation: `Quaternion.Euler(0°, 0°, 90°)`

---

## Test 5: Orphan Cleanup

### Steps
1. Place 3 boards in a line (adjacent to each other)
2. Remove the middle board

### Expected Results
- [ ] Middle board is removed
- [ ] Orphaned triggers (not adjacent to any board) are cleaned up
- [ ] Triggers adjacent to remaining boards stay

### Debug Checks
- In Hierarchy, check `PlacementTriggerManager` children
- Orphaned triggers should be destroyed
- Only triggers with adjacent boards should remain

---

## Test 6: Preview Feedback

### Steps
1. Aim at a valid trigger position
2. Observe preview color
3. (If possible) Aim at an invalid position

### Expected Results
- [ ] Valid placement shows green preview
- [ ] Invalid placement shows red preview
- [ ] Preview follows camera aim smoothly

---

## Test 7: Ground Placement Validation

### Steps
1. Aim at ground and place board
2. Aim at air (no ground below) and try to place

### Expected Results
- [ ] Board places on valid ground
- [ ] Board does NOT place when no ground detected
- [ ] No errors in console

---

## Known Issues / Concerns

### 1. Missing Resources Folder
The code loads prefab from `Resources/Prefabs/Board`. If this path doesn't exist, placement will fail silently (error logged).

**Fix**: Ensure `Assets/Resources/Prefabs/Board.prefab` exists.

### 2. Layer Mask Configuration
If layers aren't set correctly, raycasts will fail. Double-check:
- Board layer is assigned to Board prefab
- PlacementTrigger layer is assigned to triggers (done in code)
- Ground layer is assigned to ground objects

### 3. Singleton Pattern
`BuildingGrid` uses singleton pattern. Only one should exist in scene.

### 4. Preview Materials
`BoardPreview` requires `validMaterial` and `invalidMaterial` to be assigned in Inspector. Without these, preview will show default material.

---

## Test Results Template

| Test | Pass/Fail | Notes |
|------|-----------|-------|
| 1. First Placement | | |
| 2. Adjacent Placement | | |
| 3. Removal | | |
| 4. All Orientations | | |
| 5. Orphan Cleanup | | |
| 6. Preview Feedback | | |
| 7. Ground Validation | | |

---

## Quick Reference

### Controls
- **Left Click**: Place board
- **Right Click**: Remove board
- **WASD**: Move
- **Mouse**: Look around
- **Space**: Jump

### Key Files
- `BuildingController.cs:86-98` - Placement logic
- `BuildingController.cs:121-137` - Removal logic
- `PlacementTriggerManager.cs:40-62` - Trigger generation
- `PlacementTriggerManager.cs:129-148` - Orphan cleanup
