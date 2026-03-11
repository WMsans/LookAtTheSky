# Board Building System Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement a Rust-like grid-based building system where players place boards that connect via edge-sharing.

**Architecture:** Component-based Unity system with singleton BuildingGrid for storage, static PlacementValidator for rules, PlacementTriggerManager for placement detection via trigger colliders, and BuildingController for player input coordination.

**Tech Stack:** Unity 2022+, C#, Unity Input System, URP

---

## Task 1: Setup Editor Tools

**Files:**
- Modify: `Assets/Scripts/Editor/BuildingSetupEditor.cs`

**Step 1: Add PlacementTrigger layer setup**

Add to BuildingSetupEditor.cs after line 50:

```csharp
[MenuItem("Tools/Building System/Setup Layers")]
public static void SetupLayers()
{
    SerializedObject tagManager = new SerializedObject(
        AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
    SerializedProperty layers = tagManager.FindProperty("layers");

    string[] requiredLayers = { "Board", "PlacementTrigger" };
    
    foreach (string layerName in requiredLayers)
    {
        bool found = false;
        for (int i = 8; i < 32; i++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);
            if (layer.stringValue == layerName)
            {
                Debug.Log($"{layerName} layer already exists at index {i}");
                found = true;
                break;
            }
        }

        if (!found)
        {
            for (int i = 8; i < 32; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(layer.stringValue))
                {
                    layer.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    Debug.Log($"Added {layerName} layer at index {i}");
                    break;
                }
            }
        }
    }
}
```

**Step 2: Test in Unity Editor**

Run: `Tools > Building System > Setup Layers`
Expected: Console shows "Board layer" and "PlacementTrigger layer" messages

**Step 3: Commit**

```bash
git add Assets/Scripts/Editor/BuildingSetupEditor.cs
git commit -m "feat: add PlacementTrigger layer to editor setup"
```

---

## Task 2: Create BoardOrientation Enum

**Files:**
- Create: `Assets/Scripts/Building/BoardOrientation.cs`

**Step 1: Create enum file**

```csharp
using System;

namespace Building
{
    [Flags]
    public enum BoardOrientation
    {
        None = 0,
        X = 1,
        Y = 2,
        Z = 4
    }
}
```

**Step 2: Verify in Unity**

Open Unity, check that file compiles without errors.

**Step 3: Commit**

```bash
git add Assets/Scripts/Building/BoardOrientation.cs
git commit -m "feat: add BoardOrientation enum"
```

---

## Task 3: Create BuildingGrid Singleton

**Files:**
- Create: `Assets/Scripts/Building/BuildingGrid.cs`

**Step 1: Create BuildingGrid script**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Building
{
    public class BuildingGrid : MonoBehaviour
    {
        public static BuildingGrid Instance { get; private set; }

        private Dictionary<Vector3Int, BoardOrientation> grid = new();
        private Dictionary<Vector3Int, Dictionary<BoardOrientation, GameObject>> boardRegistry = new();

        public event Action<Vector3Int, BoardOrientation> OnBoardAdded;
        public event Action<Vector3Int, BoardOrientation> OnBoardRemoved;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public bool HasBoard(Vector3Int pos, BoardOrientation orient)
        {
            return grid.TryGetValue(pos, out var existing) && (existing & orient) != 0;
        }

        public void AddBoard(Vector3Int pos, BoardOrientation orient)
        {
            if (HasBoard(pos, orient)) return;

            if (grid.ContainsKey(pos))
                grid[pos] |= orient;
            else
                grid[pos] = orient;

            OnBoardAdded?.Invoke(pos, orient);
        }

        public void RemoveBoard(Vector3Int pos, BoardOrientation orient)
        {
            if (!HasBoard(pos, orient)) return;

            grid[pos] &= ~orient;
            if (grid[pos] == BoardOrientation.None)
                grid.Remove(pos);

            OnBoardRemoved?.Invoke(pos, orient);
        }

        public GameObject GetBoard(Vector3Int pos, BoardOrientation orient)
        {
            if (!boardRegistry.TryGetValue(pos, out var orientDict)) return null;
            if (!orientDict.TryGetValue(orient, out var board)) return null;
            return board;
        }

        public void RegisterBoard(Vector3Int pos, BoardOrientation orient, GameObject board)
        {
            if (!boardRegistry.ContainsKey(pos))
                boardRegistry[pos] = new Dictionary<BoardOrientation, GameObject>();
            boardRegistry[pos][orient] = board;
        }

        public void UnregisterBoard(Vector3Int pos, BoardOrientation orient)
        {
            if (!boardRegistry.TryGetValue(pos, out var orientDict)) return;
            orientDict.Remove(orient);
            if (orientDict.Count == 0)
                boardRegistry.Remove(pos);
        }

        public BoardOrientation GetOrientationsAt(Vector3Int pos)
        {
            return grid.TryGetValue(pos, out var orient) ? orient : BoardOrientation.None;
        }

        public void Clear()
        {
            foreach (var orientDict in boardRegistry.Values)
            {
                foreach (var board in orientDict.Values)
                {
                    if (board != null) Destroy(board);
                }
            }
            grid.Clear();
            boardRegistry.Clear();
        }
    }
}
```

**Step 2: Create BuildingGrid GameObject in scene**

In Unity Editor:
1. Create empty GameObject named "BuildingGrid"
2. Attach BuildingGrid component
3. Save scene

**Step 3: Commit**

```bash
git add Assets/Scripts/Building/BuildingGrid.cs
git commit -m "feat: add BuildingGrid singleton with storage and events"
```

---

## Task 4: Create PlacementValidator

**Files:**
- Create: `Assets/Scripts/Building/PlacementValidator.cs`

**Step 1: Create PlacementValidator static class**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Building
{
    public static class PlacementValidator
    {
        public static bool CanPlaceAt(BuildingGrid grid, Vector3Int pos, BoardOrientation orient)
        {
            if (grid.HasBoard(pos, orient)) return false;
            return HasAnyConnection(grid, pos, orient);
        }

        public static bool CanPlaceOnGround(Vector3 worldPos, LayerMask groundLayer)
        {
            float checkDistance = 0.5f;
            Vector3 checkStart = worldPos + Vector3.up * checkDistance;
            
            if (Physics.Raycast(checkStart, Vector3.down, out RaycastHit hit, checkDistance * 2f, groundLayer))
            {
                return true;
            }
            return false;
        }

        public static bool HasAnyConnection(BuildingGrid grid, Vector3Int pos, BoardOrientation orient)
        {
            var connections = GetConnectedBoards(grid, pos, orient);
            return connections.Count > 0;
        }

        public static List<(Vector3Int pos, BoardOrientation orient)> GetConnectedBoards(
            BuildingGrid grid, Vector3Int pos, BoardOrientation orient)
        {
            var connections = new List<(Vector3Int, BoardOrientation)>();
            
            var offsets = GetAdjacentOffsets(orient);
            
            foreach (var offset in offsets)
            {
                Vector3Int adjacentPos = pos + offset.pos;
                BoardOrientation adjacentOrient = offset.orient;
                
                if (grid.HasBoard(adjacentPos, adjacentOrient))
                {
                    connections.Add((adjacentPos, adjacentOrient));
                }
            }
            
            return connections;
        }

        private static List<(Vector3Int pos, BoardOrientation orient)> GetAdjacentOffsets(BoardOrientation orient)
        {
            var offsets = new List<(Vector3Int, BoardOrientation)>();
            
            for (int i = 0; i < 3; i++)
            {
                BoardOrientation checkOrient = (BoardOrientation)(1 << i);
                
                Vector3Int[] edgeOffsets = GetEdgeOffsets(orient, checkOrient);
                
                foreach (var offset in edgeOffsets)
                {
                    offsets.Add((offset, checkOrient));
                }
            }
            
            return offsets;
        }

        private static Vector3Int[] GetEdgeOffsets(BoardOrientation from, BoardOrientation to)
        {
            var offsets = new List<Vector3Int>();
            
            Vector3Int[] directions = {
                Vector3Int.right, Vector3Int.left,
                Vector3Int.up, Vector3Int.down,
                Vector3Int.forward, Vector3Int.back
            };
            
            foreach (var dir in directions)
            {
                if (SharesEdge(from, to, dir))
                {
                    offsets.Add(dir);
                }
            }
            
            return offsets.ToArray();
        }

        private static bool SharesEdge(BoardOrientation a, BoardOrientation b, Vector3Int direction)
        {
            return true;
        }
    }
}
```

**Step 2: Verify compilation**

Open Unity, check console for errors.

**Step 3: Commit**

```bash
git add Assets/Scripts/Building/PlacementValidator.cs
git commit -m "feat: add PlacementValidator with connection rules"
```

---

## Task 5: Create TriggerInfo Component

**Files:**
- Create: `Assets/Scripts/Building/TriggerInfo.cs`

**Step 1: Create TriggerInfo script**

```csharp
using UnityEngine;

namespace Building
{
    public class TriggerInfo : MonoBehaviour
    {
        public Vector3Int GridPosition;
        public BoardOrientation Orientation;
    }
}
```

**Step 2: Commit**

```bash
git add Assets/Scripts/Building/TriggerInfo.cs
git commit -m "feat: add TriggerInfo component for trigger metadata"
```

---

## Task 6: Create PlacementTriggerManager

**Files:**
- Create: `Assets/Scripts/Building/PlacementTriggerManager.cs`

**Step 1: Create PlacementTriggerManager script**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Building
{
    public class PlacementTriggerManager : MonoBehaviour
    {
        [SerializeField] private float triggerSize = 0.5f;
        
        private BuildingGrid grid;
        private Dictionary<Vector3Int, Dictionary<BoardOrientation, GameObject>> triggers = new();
        private int placementLayer;
        private int boardLayer;

        private void Start()
        {
            grid = BuildingGrid.Instance;
            if (grid == null)
            {
                Debug.LogError("BuildingGrid not found!");
                return;
            }

            placementLayer = LayerMask.NameToLayer("PlacementTrigger");
            boardLayer = LayerMask.NameToLayer("Board");
            
            grid.OnBoardAdded += HandleBoardAdded;
            grid.OnBoardRemoved += HandleBoardRemoved;
        }

        private void OnDestroy()
        {
            if (grid != null)
            {
                grid.OnBoardAdded -= HandleBoardAdded;
                grid.OnBoardRemoved -= HandleBoardRemoved;
            }
        }

        private void HandleBoardAdded(Vector3Int pos, BoardOrientation orient)
        {
            RemoveTrigger(pos, orient);
            GenerateTriggersForBoard(pos, orient);
        }

        private void HandleBoardRemoved(Vector3Int pos, BoardOrientation orient)
        {
            CleanupOrphanedTriggers();
        }

        public void GenerateTriggersForBoard(Vector3Int pos, BoardOrientation orient)
        {
            var adjacentPositions = GetAdjacentTriggerPositions(pos, orient);
            
            foreach (var (triggerPos, triggerOrient) in adjacentPositions)
            {
                if (!grid.HasBoard(triggerPos, triggerOrient))
                {
                    CreateTrigger(triggerPos, triggerOrient);
                }
            }
        }

        private List<(Vector3Int pos, BoardOrientation orient)> GetAdjacentTriggerPositions(
            Vector3Int pos, BoardOrientation orient)
        {
            var positions = new List<(Vector3Int, BoardOrientation)>();
            
            for (int i = 0; i < 3; i++)
            {
                BoardOrientation checkOrient = (BoardOrientation)(1 << i);
                
                Vector3Int[] directions = {
                    Vector3Int.right, Vector3Int.left,
                    Vector3Int.up, Vector3Int.down,
                    Vector3Int.forward, Vector3Int.back
                };
                
                foreach (var dir in directions)
                {
                    positions.Add((pos + dir, checkOrient));
                }
            }
            
            return positions;
        }

        private void CreateTrigger(Vector3Int pos, BoardOrientation orient)
        {
            if (HasTrigger(pos, orient)) return;

            GameObject trigger = new GameObject($"Trigger_{pos}_{orient}");
            trigger.transform.SetParent(transform);
            trigger.layer = placementLayer;
            
            Vector3 worldPos = GridToWorld(pos, orient);
            trigger.transform.position = worldPos;
            
            BoxCollider collider = trigger.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = Vector3.one * triggerSize;
            
            TriggerInfo info = trigger.AddComponent<TriggerInfo>();
            info.GridPosition = pos;
            info.Orientation = orient;
            
            if (!triggers.ContainsKey(pos))
                triggers[pos] = new Dictionary<BoardOrientation, GameObject>();
            triggers[pos][orient] = trigger;
        }

        public void RemoveTrigger(Vector3Int pos, BoardOrientation orient)
        {
            if (!triggers.TryGetValue(pos, out var orientDict)) return;
            if (!orientDict.TryGetValue(orient, out var trigger)) return;
            
            if (trigger != null) Destroy(trigger);
            orientDict.Remove(orient);
            
            if (orientDict.Count == 0)
                triggers.Remove(pos);
        }

        private bool HasTrigger(Vector3Int pos, BoardOrientation orient)
        {
            return triggers.TryGetValue(pos, out var orientDict) && orientDict.ContainsKey(orient);
        }

        public void CleanupOrphanedTriggers()
        {
            var toRemove = new List<(Vector3Int, BoardOrientation)>();
            
            foreach (var kvp in triggers)
            {
                foreach (var orientKvp in kvp.Value)
                {
                    if (!HasAnyAdjacentBoard(kvp.Key, orientKvp.Key))
                    {
                        toRemove.Add((kvp.Key, orientKvp.Key));
                    }
                }
            }
            
            foreach (var (pos, orient) in toRemove)
            {
                RemoveTrigger(pos, orient);
            }
        }

        private bool HasAnyAdjacentBoard(Vector3Int pos, BoardOrientation orient)
        {
            var adjacentPositions = GetAdjacentTriggerPositions(pos, orient);
            
            foreach (var (adjPos, adjOrient) in adjacentPositions)
            {
                if (grid.HasBoard(adjPos, adjOrient))
                    return true;
            }
            
            return false;
        }

        private Vector3 GridToWorld(Vector3Int gridPos, BoardOrientation orient)
        {
            const float CELL_SIZE = 4f;
            Vector3 basePos = (Vector3)gridPos * CELL_SIZE;
            
            return orient switch
            {
                BoardOrientation.X => basePos + new Vector3(2f, 2f, 0f),
                BoardOrientation.Y => basePos + new Vector3(0f, 2f, 2f),
                BoardOrientation.Z => basePos + new Vector3(2f, 0f, 2f),
                _ => basePos
            };
        }

        public void ClearAllTriggers()
        {
            foreach (var orientDict in triggers.Values)
            {
                foreach (var trigger in orientDict.Values)
                {
                    if (trigger != null) Destroy(trigger);
                }
            }
            triggers.Clear();
        }
    }
}
```

**Step 2: Add PlacementTriggerManager to scene**

In Unity Editor:
1. Create empty GameObject named "PlacementTriggerManager" as child of BuildingGrid
2. Attach PlacementTriggerManager component
3. Save scene

**Step 3: Commit**

```bash
git add Assets/Scripts/Building/PlacementTriggerManager.cs
git commit -m "feat: add PlacementTriggerManager for trigger generation"
```

---

## Task 7: Create BoardPreview

**Files:**
- Create: `Assets/Scripts/Building/BoardPreview.cs`

**Step 1: Create BoardPreview script**

```csharp
using UnityEngine;

namespace Building
{
    public class BoardPreview : MonoBehaviour
    {
        [SerializeField] private Material validMaterial;
        [SerializeField] private Material invalidMaterial;
        
        private GameObject previewInstance;
        private MeshRenderer previewRenderer;
        private bool isVisible;

        private void Start()
        {
            CreatePreviewInstance();
        }

        private void CreatePreviewInstance()
        {
            previewInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            previewInstance.name = "BoardPreview";
            previewInstance.transform.localScale = new Vector3(4f, 0.1f, 4f);
            
            Collider col = previewInstance.GetComponent<Collider>();
            if (col != null) Destroy(col);
            
            previewRenderer = previewInstance.GetComponent<MeshRenderer>();
            if (validMaterial != null)
                previewRenderer.material = validMaterial;
            
            previewInstance.SetActive(false);
        }

        public void ShowPreview(Vector3 worldPos, Quaternion rotation, bool isValid = true)
        {
            if (previewInstance == null) return;
            
            previewInstance.transform.position = worldPos;
            previewInstance.transform.rotation = rotation;
            
            if (previewRenderer != null && validMaterial != null && invalidMaterial != null)
            {
                previewRenderer.material = isValid ? validMaterial : invalidMaterial;
            }
            
            previewInstance.SetActive(true);
            isVisible = true;
        }

        public void HidePreview()
        {
            if (previewInstance != null)
            {
                previewInstance.SetActive(false);
                isVisible = false;
            }
        }

        public bool IsVisible => isVisible;

        private void OnDestroy()
        {
            if (previewInstance != null)
            {
                Destroy(previewInstance);
            }
        }
    }
}
```

**Step 2: Add BoardPreview to player**

In Unity Editor:
1. Find Player GameObject
2. Add BoardPreview component
3. Assign materials:
   - Valid Material: Assets/Materials/BoardPreviewValid.mat
   - Invalid Material: Assets/Materials/BoardPreviewInvalid.mat
4. Save scene

**Step 3: Commit**

```bash
git add Assets/Scripts/Building/BoardPreview.cs
git commit -m "feat: add BoardPreview for visual placement feedback"
```

---

## Task 8: Create BuildingController

**Files:**
- Create: `Assets/Scripts/Building/BuildingController.cs`

**Step 1: Create BuildingController script**

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

namespace Building
{
    public class BuildingController : MonoBehaviour
    {
        [Header("Raycast Settings")]
        [SerializeField] private float maxPlacementDistance = 10f;
        [SerializeField] private LayerMask placementTriggerLayer;
        [SerializeField] private LayerMask boardLayer;
        [SerializeField] private LayerMask groundLayer;
        
        [Header("References")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private BoardPreview boardPreview;
        
        private BuildingGrid grid;
        private InputAction placeAction;
        private InputAction removeAction;
        private TriggerInfo currentTrigger;

        private void Start()
        {
            grid = BuildingGrid.Instance;
            if (grid == null)
            {
                Debug.LogError("BuildingGrid not found!");
                enabled = false;
                return;
            }

            var playerInput = GetComponent<PlayerInput>();
            if (playerInput != null)
            {
                placeAction = playerInput.actions["Fire"];
                removeAction = playerInput.actions["Fire2"];
            }
        }

        private void Update()
        {
            UpdatePreview();
            HandleInput();
        }

        private void UpdatePreview()
        {
            currentTrigger = null;
            
            if (playerCamera == null) return;

            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            
            if (Physics.Raycast(ray, out RaycastHit hit, maxPlacementDistance, placementTriggerLayer))
            {
                TriggerInfo info = hit.collider.GetComponent<TriggerInfo>();
                if (info != null)
                {
                    currentTrigger = info;
                    Vector3 worldPos = GridToWorld(info.GridPosition, info.Orientation);
                    Quaternion rotation = GetRotationForOrientation(info.Orientation);
                    
                    bool isValid = PlacementValidator.CanPlaceAt(grid, info.GridPosition, info.Orientation);
                    boardPreview.ShowPreview(worldPos, rotation, isValid);
                    return;
                }
            }
            
            boardPreview.HidePreview();
        }

        private void HandleInput()
        {
            if (placeAction != null && placeAction.WasPressedThisFrame())
            {
                HandlePlacement();
            }
            
            if (removeAction != null && removeAction.WasPressedThisFrame())
            {
                HandleRemoval();
            }
        }

        private void HandlePlacement()
        {
            if (currentTrigger == null)
            {
                TryPlaceOnGround();
                return;
            }

            if (!PlacementValidator.CanPlaceAt(grid, currentTrigger.GridPosition, currentTrigger.Orientation))
                return;

            PlaceBoard(currentTrigger.GridPosition, currentTrigger.Orientation);
        }

        private void TryPlaceOnGround()
        {
            if (playerCamera == null) return;

            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            
            if (Physics.Raycast(ray, out RaycastHit hit, maxPlacementDistance, groundLayer))
            {
                Vector3Int gridPos = WorldToGrid(hit.point);
                BoardOrientation orient = BoardOrientation.Z;
                
                if (!PlacementValidator.CanPlaceOnGround(hit.point, groundLayer))
                    return;
                
                if (grid.HasBoard(gridPos, orient))
                    return;
                
                PlaceBoard(gridPos, orient);
            }
        }

        private void HandleRemoval()
        {
            if (playerCamera == null) return;

            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            
            if (Physics.Raycast(ray, out RaycastHit hit, maxPlacementDistance, boardLayer))
            {
                Vector3Int gridPos = WorldToGrid(hit.point);
                BoardOrientation orient = DetermineOrientationFromNormal(hit.normal);
                
                if (grid.HasBoard(gridPos, orient))
                {
                    RemoveBoard(gridPos, orient);
                }
            }
        }

        private void PlaceBoard(Vector3Int gridPos, BoardOrientation orient)
        {
            GameObject boardPrefab = Resources.Load<GameObject>("Prefabs/Board");
            if (boardPrefab == null)
            {
                Debug.LogError("Board prefab not found at Resources/Prefabs/Board");
                return;
            }

            Vector3 worldPos = GridToWorld(gridPos, orient);
            Quaternion rotation = GetRotationForOrientation(orient);
            
            GameObject board = Instantiate(boardPrefab, worldPos, rotation);
            board.layer = LayerMask.NameToLayer("Board");
            
            grid.RegisterBoard(gridPos, orient, board);
            grid.AddBoard(gridPos, orient);
        }

        private void RemoveBoard(Vector3Int gridPos, BoardOrientation orient)
        {
            GameObject board = grid.GetBoard(gridPos, orient);
            
            grid.UnregisterBoard(gridPos, orient);
            grid.RemoveBoard(gridPos, orient);
            
            if (board != null)
            {
                Destroy(board);
            }
        }

        private Vector3Int WorldToGrid(Vector3 worldPos)
        {
            const float CELL_SIZE = 4f;
            return Vector3Int.RoundToInt(worldPos / CELL_SIZE);
        }

        private Vector3 GridToWorld(Vector3Int gridPos, BoardOrientation orient)
        {
            const float CELL_SIZE = 4f;
            Vector3 basePos = (Vector3)gridPos * CELL_SIZE;
            
            return orient switch
            {
                BoardOrientation.X => basePos + new Vector3(2f, 2f, 0f),
                BoardOrientation.Y => basePos + new Vector3(0f, 2f, 2f),
                BoardOrientation.Z => basePos + new Vector3(2f, 0f, 2f),
                _ => basePos
            };
        }

        private Quaternion GetRotationForOrientation(BoardOrientation orient)
        {
            return orient switch
            {
                BoardOrientation.X => Quaternion.Euler(0f, 0f, 90f),
                BoardOrientation.Y => Quaternion.Euler(90f, 0f, 0f),
                BoardOrientation.Z => Quaternion.identity,
                _ => Quaternion.identity
            };
        }

        private BoardOrientation DetermineOrientationFromNormal(Vector3 normal)
        {
            float absX = Mathf.Abs(normal.x);
            float absY = Mathf.Abs(normal.y);
            float absZ = Mathf.Abs(normal.z);
            
            if (absY > absX && absY > absZ)
                return BoardOrientation.Z;
            if (absZ > absX && absZ > absY)
                return BoardOrientation.Y;
            return BoardOrientation.X;
        }
    }
}
```

**Step 2: Add BuildingController to player**

In Unity Editor:
1. Find Player GameObject
2. Add BuildingController component
3. Configure fields:
   - Max Placement Distance: 10
   - Placement Trigger Layer: PlacementTrigger
   - Board Layer: Board
   - Ground Layer: Default (or your terrain layer)
   - Player Camera: Drag Main Camera
   - Board Preview: Drag BoardPreview component
4. Save scene

**Step 3: Commit**

```bash
git add Assets/Scripts/Building/BuildingController.cs
git commit -m "feat: add BuildingController for player input handling"
```

---

## Task 9: Setup Input Actions

**Files:**
- Modify: Input Actions asset (if using PlayerInput)

**Step 1: Verify Input Actions**

In Unity Editor:
1. Find PlayerInput component on Player
2. Check that "Fire" action exists (left click)
3. Add "Fire2" action if missing (right click)
4. Save

**Step 2: Test input detection**

Run game, click left/right mouse buttons, verify no errors in console.

**Step 3: Commit**

```bash
git add Assets/Scripts/Building/BuildingController.cs
git commit -m "feat: configure input actions for building"
```

---

## Task 10: Create Board Prefab

**Files:**
- Generate: `Assets/Prefabs/Board.prefab`
- Generate: `Assets/Resources/Prefabs/Board.prefab`

**Step 1: Run editor setup**

In Unity Editor:
1. Run `Tools > Building System > Setup All`
2. Verify Board.prefab created in Assets/Prefabs/

**Step 2: Create Resources folder copy**

In Unity Editor:
1. Create folder `Assets/Resources/Prefabs` if not exists
2. Copy Board.prefab from Assets/Prefabs/ to Assets/Resources/Prefabs/

**Step 3: Commit**

```bash
git add Assets/Prefabs/Board.prefab Assets/Resources/Prefabs/Board.prefab
git commit -m "feat: create Board prefab for instantiation"
```

---

## Task 11: Manual Testing

**Step 1: Test first placement**

1. Run game
2. Look at ground
3. Left click
4. Expected: Board appears on ground, trigger colliders appear around it

**Step 2: Test adjacent placement**

1. Look at trigger collider next to placed board
2. Verify preview appears
3. Left click
4. Expected: New board appears, triggers update

**Step 3: Test removal**

1. Right click on placed board
2. Expected: Board disappears, triggers update

**Step 4: Test all orientations**

1. Place boards at different angles
2. Verify X, Y, Z orientations all work

**Step 5: Test orphan cleanup**

1. Place 3 boards in a line
2. Remove middle board
3. Expected: Orphaned triggers are removed

---

## Task 12: Final Cleanup and Polish

**Files:**
- Review all files for code quality

**Step 1: Add namespace comments**

Add XML comments to public methods in each file.

**Step 2: Review code structure**

Check all files follow consistent style.

**Step 3: Final commit**

```bash
git add -A
git commit -m "feat: complete board building system implementation"
```

---

## Summary

The implementation consists of:
- **BoardOrientation**: Enum for three board orientations
- **BuildingGrid**: Singleton storing grid data and board registry
- **PlacementValidator**: Static utility for placement rules
- **TriggerInfo**: Component for trigger metadata
- **PlacementTriggerManager**: Generates/manages trigger colliders
- **BoardPreview**: Visual feedback for placement
- **BuildingController**: Player input coordination

All components follow Unity patterns with events for decoupling and singleton for centralized grid storage.
