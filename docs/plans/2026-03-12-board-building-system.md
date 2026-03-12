# Board Building System Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement a Rust-like building system where players place flat boards on a 3D grid using trigger colliders for placement detection, with hardcoded adjacency tables for connection validation.

**Architecture:** 3-orientation grid (X/Y/Z planes per cell) with bit-flag storage. Trigger colliders spawned at valid placement positions after each board is placed. Player raycasts against triggers to place and against boards to remove. Adjacency logic lives in a single static lookup table shared by the trigger manager and validator.

**Tech Stack:** Unity 6 (URP), C#, New Input System, Physics raycasting

**Design doc:** `docs/plans/2026-03-12-board-building-system-design.md`

---

### Task 1: BoardOrientation enum

**Files:**
- Create: `Assets/Scripts/Building/BoardOrientation.cs`

**Step 1: Create the enum**

```csharp
using System;

namespace Building
{
    [Flags]
    public enum BoardOrientation
    {
        None = 0,
        X = 1,  // XY plane: (x,y,z) to (x+1,y+1,z)
        Y = 2,  // YZ plane: (x,y,z) to (x,y+1,z+1)
        Z = 4   // XZ plane: (x,y,z) to (x+1,y,z+1)
    }
}
```

**Step 2: Verify**

Open Unity, confirm no compilation errors in the Console window.

**Step 3: Commit**

```bash
git add Assets/Scripts/Building/BoardOrientation.cs
git commit -m "feat: add BoardOrientation flags enum"
```

---

### Task 2: BoardAdjacency static lookup table

**Files:**
- Create: `Assets/Scripts/Building/BoardAdjacency.cs`

**Step 1: Create the adjacency table class**

This is the critical piece that was broken in previous iterations. Each orientation has exactly 12 neighbors derived from the geometry in the design doc.

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

        // Z-board at (0,0,0): corners (0,0,0)(1,0,0)(1,0,1)(0,0,1) — XZ horizontal plane
        private static readonly Neighbor[] ZNeighbors = new Neighbor[]
        {
            // 4 coplanar Z-neighbors
            new(-1, 0, 0, BoardOrientation.Z),  // shares left edge
            new( 1, 0, 0, BoardOrientation.Z),  // shares right edge
            new( 0, 0,-1, BoardOrientation.Z),  // shares back edge
            new( 0, 0, 1, BoardOrientation.Z),  // shares front edge
            // 4 perpendicular below (y-1)
            new( 0,-1, 0, BoardOrientation.X),  // X-board top = Z back edge
            new( 0,-1, 1, BoardOrientation.X),  // X-board top = Z front edge
            new( 0,-1, 0, BoardOrientation.Y),  // Y-board top = Z left edge
            new( 1,-1, 0, BoardOrientation.Y),  // Y-board top = Z right edge
            // 4 perpendicular above (y)
            new( 0, 0, 0, BoardOrientation.X),  // X-board bottom = Z back edge
            new( 0, 0, 1, BoardOrientation.X),  // X-board bottom = Z front edge
            new( 0, 0, 0, BoardOrientation.Y),  // Y-board bottom = Z left edge
            new( 1, 0, 0, BoardOrientation.Y),  // Y-board bottom = Z right edge
        };

        // X-board at (0,0,0): corners (0,0,0)(1,0,0)(1,1,0)(0,1,0) — XY vertical plane
        private static readonly Neighbor[] XNeighbors = new Neighbor[]
        {
            // 4 coplanar X-neighbors
            new(-1, 0, 0, BoardOrientation.X),  // shares left edge
            new( 1, 0, 0, BoardOrientation.X),  // shares right edge
            new( 0,-1, 0, BoardOrientation.X),  // shares bottom edge
            new( 0, 1, 0, BoardOrientation.X),  // shares top edge
            // 4 perpendicular on z- side
            new( 0, 0,-1, BoardOrientation.Z),  // Z front = X bottom edge
            new( 0, 1,-1, BoardOrientation.Z),  // Z front = X top edge
            new( 0, 0,-1, BoardOrientation.Y),  // Y right = X left edge
            new( 1, 0,-1, BoardOrientation.Y),  // Y right = X right edge
            // 4 perpendicular on z+ side
            new( 0, 0, 0, BoardOrientation.Z),  // Z back = X bottom edge
            new( 0, 1, 0, BoardOrientation.Z),  // Z back = X top edge
            new( 0, 0, 0, BoardOrientation.Y),  // Y left = X left edge
            new( 1, 0, 0, BoardOrientation.Y),  // Y left = X right edge
        };

        // Y-board at (0,0,0): corners (0,0,0)(0,1,0)(0,1,1)(0,0,1) — YZ vertical plane
        private static readonly Neighbor[] YNeighbors = new Neighbor[]
        {
            // 4 coplanar Y-neighbors
            new( 0, 0,-1, BoardOrientation.Y),  // shares back edge
            new( 0, 0, 1, BoardOrientation.Y),  // shares front edge
            new( 0,-1, 0, BoardOrientation.Y),  // shares bottom edge
            new( 0, 1, 0, BoardOrientation.Y),  // shares top edge
            // 4 perpendicular on x- side
            new(-1, 0, 0, BoardOrientation.Z),  // Z right = Y bottom edge
            new(-1, 1, 0, BoardOrientation.Z),  // Z right = Y top edge
            new(-1, 0, 0, BoardOrientation.X),  // X right = Y back edge
            new(-1, 0, 1, BoardOrientation.X),  // X right = Y front edge
            // 4 perpendicular on x+ side
            new( 0, 0, 0, BoardOrientation.Z),  // Z left = Y bottom edge
            new( 0, 1, 0, BoardOrientation.Z),  // Z left = Y top edge
            new( 0, 0, 0, BoardOrientation.X),  // X left = Y back edge
            new( 0, 0, 1, BoardOrientation.X),  // X left = Y front edge
        };

        public static Neighbor[] GetNeighbors(BoardOrientation orient)
        {
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

**Step 2: Verify**

Open Unity, confirm no compilation errors. Visually review each table entry against the design doc adjacency tables.

**Step 3: Commit**

```bash
git add Assets/Scripts/Building/BoardAdjacency.cs
git commit -m "feat: add BoardAdjacency static lookup tables for 12 neighbors per orientation"
```

---

### Task 3: BuildingGrid singleton

**Files:**
- Create: `Assets/Scripts/Building/BuildingGrid.cs`

**Step 1: Create the grid class**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Building
{
    public class BuildingGrid : MonoBehaviour
    {
        public static BuildingGrid Instance { get; private set; }

        public event Action<Vector3Int, BoardOrientation> OnBoardAdded;
        public event Action<Vector3Int, BoardOrientation> OnBoardRemoved;

        private Dictionary<Vector3Int, BoardOrientation> _grid = new();
        private Dictionary<Vector3Int, Dictionary<BoardOrientation, GameObject>> _boardRegistry = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public bool IsEmpty() => _grid.Count == 0;

        public bool HasBoard(Vector3Int pos, BoardOrientation orient)
        {
            return _grid.TryGetValue(pos, out var flags) && (flags & orient) != 0;
        }

        public bool AddBoard(Vector3Int pos, BoardOrientation orient, GameObject boardObj)
        {
            if (HasBoard(pos, orient)) return false;

            if (_grid.ContainsKey(pos))
                _grid[pos] |= orient;
            else
                _grid[pos] = orient;

            if (!_boardRegistry.ContainsKey(pos))
                _boardRegistry[pos] = new Dictionary<BoardOrientation, GameObject>();
            _boardRegistry[pos][orient] = boardObj;

            OnBoardAdded?.Invoke(pos, orient);
            return true;
        }

        public bool RemoveBoard(Vector3Int pos, BoardOrientation orient)
        {
            if (!HasBoard(pos, orient)) return false;

            _grid[pos] &= ~orient;
            if (_grid[pos] == BoardOrientation.None)
                _grid.Remove(pos);

            if (_boardRegistry.TryGetValue(pos, out var orientDict))
            {
                if (orientDict.TryGetValue(orient, out var obj))
                {
                    if (obj != null) Destroy(obj);
                    orientDict.Remove(orient);
                }
                if (orientDict.Count == 0)
                    _boardRegistry.Remove(pos);
            }

            OnBoardRemoved?.Invoke(pos, orient);
            return true;
        }

        public GameObject GetBoard(Vector3Int pos, BoardOrientation orient)
        {
            if (_boardRegistry.TryGetValue(pos, out var orientDict) &&
                orientDict.TryGetValue(orient, out var obj))
                return obj;
            return null;
        }

        public bool HasAnyNeighbor(Vector3Int pos, BoardOrientation orient)
        {
            var neighbors = BoardAdjacency.GetNeighbors(orient);
            foreach (var n in neighbors)
            {
                if (HasBoard(pos + n.Offset, n.Orientation))
                    return true;
            }
            return false;
        }
    }
}
```

**Step 2: Verify**

Open Unity, confirm no compilation errors.

**Step 3: Commit**

```bash
git add Assets/Scripts/Building/BuildingGrid.cs
git commit -m "feat: add BuildingGrid singleton with board storage and events"
```

---

### Task 4: TriggerInfo component

**Files:**
- Create: `Assets/Scripts/Building/TriggerInfo.cs`

**Step 1: Create the component**

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

**Step 2: Verify**

Open Unity, confirm no compilation errors.

**Step 3: Commit**

```bash
git add Assets/Scripts/Building/TriggerInfo.cs
git commit -m "feat: add TriggerInfo component for trigger metadata"
```

---

### Task 5: PlacementTriggerManager

**Files:**
- Create: `Assets/Scripts/Building/PlacementTriggerManager.cs`

**Step 1: Create the trigger manager**

This component listens to BuildingGrid events and manages trigger collider lifecycle. It uses the adjacency table from BoardAdjacency to determine where to place triggers.

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Building
{
    public class PlacementTriggerManager : MonoBehaviour
    {
        private const float CELL_SIZE = 4f;

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

        private void HandleBoardAdded(Vector3Int pos, BoardOrientation orient)
        {
            // Remove trigger at placed position (board now occupies it)
            RemoveTrigger(pos, orient);

            // Generate triggers at each valid empty neighbor
            var neighbors = BoardAdjacency.GetNeighbors(orient);
            foreach (var n in neighbors)
            {
                Vector3Int neighborPos = pos + n.Offset;
                BoardOrientation neighborOrient = n.Orientation;

                if (!_grid.HasBoard(neighborPos, neighborOrient))
                {
                    CreateTrigger(neighborPos, neighborOrient);
                }
            }
        }

        private void HandleBoardRemoved(Vector3Int pos, BoardOrientation orient)
        {
            // Create trigger at removed position if it still has neighbors
            if (_grid.HasAnyNeighbor(pos, orient))
            {
                CreateTrigger(pos, orient);
            }

            // Cleanup orphaned triggers (triggers with no adjacent boards)
            CleanupOrphanedTriggers();
        }

        private void CreateTrigger(Vector3Int pos, BoardOrientation orient)
        {
            if (HasTrigger(pos, orient)) return;

            GameObject trigger = new GameObject($"Trigger_{pos}_{orient}");
            trigger.transform.SetParent(transform);
            trigger.layer = _placementLayer;

            trigger.transform.position = GridToWorld(pos, orient);
            trigger.transform.rotation = GetBoardRotation(orient);

            BoxCollider collider = trigger.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(CELL_SIZE, 0.1f, CELL_SIZE);

            TriggerInfo info = trigger.AddComponent<TriggerInfo>();
            info.GridPosition = pos;
            info.Orientation = orient;

            if (!_triggers.ContainsKey(pos))
                _triggers[pos] = new Dictionary<BoardOrientation, GameObject>();
            _triggers[pos][orient] = trigger;
        }

        private void RemoveTrigger(Vector3Int pos, BoardOrientation orient)
        {
            if (!_triggers.TryGetValue(pos, out var orientDict)) return;
            if (!orientDict.TryGetValue(orient, out var trigger)) return;

            if (trigger != null) Destroy(trigger);
            orientDict.Remove(orient);

            if (orientDict.Count == 0)
                _triggers.Remove(pos);
        }

        private bool HasTrigger(Vector3Int pos, BoardOrientation orient)
        {
            return _triggers.TryGetValue(pos, out var orientDict) && orientDict.ContainsKey(orient);
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

            foreach (var (pos, orient) in toRemove)
            {
                RemoveTrigger(pos, orient);
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

        public static Vector3 GridToWorld(Vector3Int gridPos, BoardOrientation orient)
        {
            Vector3 basePos = (Vector3)gridPos * CELL_SIZE;

            return orient switch
            {
                BoardOrientation.X => basePos + new Vector3(CELL_SIZE / 2f, CELL_SIZE / 2f, 0f),
                BoardOrientation.Y => basePos + new Vector3(0f, CELL_SIZE / 2f, CELL_SIZE / 2f),
                BoardOrientation.Z => basePos + new Vector3(CELL_SIZE / 2f, 0f, CELL_SIZE / 2f),
                _ => basePos
            };
        }

        public static Quaternion GetBoardRotation(BoardOrientation orient)
        {
            return orient switch
            {
                BoardOrientation.X => Quaternion.identity,                        // XY plane faces Z
                BoardOrientation.Y => Quaternion.Euler(0f, 90f, 0f),             // YZ plane faces X
                BoardOrientation.Z => Quaternion.Euler(90f, 0f, 0f),             // XZ plane faces Y
                _ => Quaternion.identity
            };
        }
    }
}
```

**Step 2: Verify**

Open Unity, confirm no compilation errors. Note: the `GridToWorld` and `GetBoardRotation` methods are `public static` so other components (BoardPreview, BuildingController) can use them without duplicating the logic.

**Step 3: Commit**

```bash
git add Assets/Scripts/Building/PlacementTriggerManager.cs
git commit -m "feat: add PlacementTriggerManager with adjacency-based trigger generation"
```

---

### Task 6: BoardPreview

**Files:**
- Create: `Assets/Scripts/Building/BoardPreview.cs`

**Step 1: Create the preview component**

```csharp
using UnityEngine;

namespace Building
{
    public class BoardPreview : MonoBehaviour
    {
        [SerializeField] private Material previewMaterial;

        private GameObject _previewObj;
        private MeshRenderer _renderer;

        private void Awake()
        {
            _previewObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _previewObj.name = "BoardPreview";
            _previewObj.transform.SetParent(transform);
            _previewObj.transform.localScale = new Vector3(4f, 0.1f, 4f);

            // Remove collider so it doesn't interfere with raycasts
            var collider = _previewObj.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            _renderer = _previewObj.GetComponent<MeshRenderer>();
            if (previewMaterial != null)
                _renderer.material = previewMaterial;

            _previewObj.SetActive(false);
        }

        public void ShowAt(Vector3 position, Quaternion rotation)
        {
            _previewObj.transform.position = position;
            _previewObj.transform.rotation = rotation;
            _previewObj.SetActive(true);
        }

        public void Hide()
        {
            _previewObj.SetActive(false);
        }
    }
}
```

**Step 2: Verify**

Open Unity, confirm no compilation errors.

**Step 3: Commit**

```bash
git add Assets/Scripts/Building/BoardPreview.cs
git commit -m "feat: add BoardPreview for visual placement feedback"
```

---

### Task 7: Add Remove action to Input System

**Files:**
- Modify: `Assets/InputSystem_Actions.inputactions`

**Step 1: Add the Remove action**

Add a new "Remove" action (Button type) to the Player action map, bound to right mouse button and gamepad button east (B). This must be done by editing the JSON directly since we don't have the Unity editor GUI.

Add the action entry to the `"actions"` array of the Player map (after the Attack action), and add bindings for it.

The action to add:
```json
{
    "name": "Remove",
    "type": "Button",
    "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "expectedControlType": "Button",
    "processors": "",
    "interactions": "",
    "initialStateCheck": false
}
```

Bindings to add to the Player map bindings array:
```json
{
    "name": "",
    "id": "d4c3b2a1-6f5e-0987-dcba-0987654321fe",
    "path": "<Mouse>/rightButton",
    "interactions": "",
    "processors": "",
    "groups": "Keyboard&Mouse",
    "action": "Remove",
    "isComposite": false,
    "isPartOfComposite": false
}
```

**Step 2: Verify**

Open Unity, select the InputSystem_Actions asset, confirm "Remove" action appears in the Player map with right mouse button binding.

**Step 3: Commit**

```bash
git add Assets/InputSystem_Actions.inputactions
git commit -m "feat: add Remove action to Player input map"
```

---

### Task 8: BuildingController

**Files:**
- Create: `Assets/Scripts/Building/BuildingController.cs`

**Step 1: Create the building controller**

This is the player-facing component that ties everything together: raycasting, preview, placement, and removal.

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

namespace Building
{
    public class BuildingController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private BoardPreview boardPreview;
        [SerializeField] private GameObject boardPrefab;

        [Header("Settings")]
        [SerializeField] private float maxPlacementDistance = 16f;
        [SerializeField] private float firstBoardDistance = 8f;
        [SerializeField] private LayerMask placementTriggerLayer;
        [SerializeField] private LayerMask boardLayer;

        private BuildingGrid _grid;
        private InputAction _attackAction;
        private InputAction _removeAction;

        // Current target state
        private bool _hasTarget;
        private Vector3Int _targetPos;
        private BoardOrientation _targetOrient;

        private void Awake()
        {
            var playerInput = GetComponent<PlayerInput>();
            _attackAction = playerInput.actions["Attack"];
            _removeAction = playerInput.actions["Remove"];
        }

        private void Start()
        {
            _grid = BuildingGrid.Instance;
            if (_grid == null)
                Debug.LogError("[BuildingController] BuildingGrid.Instance not found.");
        }

        private void Update()
        {
            if (_grid == null) return;

            _hasTarget = false;

            if (_grid.IsEmpty())
            {
                HandleFirstBoardTargeting();
            }
            else
            {
                HandleNormalTargeting();
            }

            // Update preview
            if (_hasTarget)
            {
                Vector3 worldPos = PlacementTriggerManager.GridToWorld(_targetPos, _targetOrient);
                Quaternion rotation = PlacementTriggerManager.GetBoardRotation(_targetOrient);
                boardPreview.ShowAt(worldPos, rotation);
            }
            else
            {
                boardPreview.Hide();
            }

            // Handle input
            if (_hasTarget && _attackAction.WasPressedThisFrame())
            {
                PlaceBoard(_targetPos, _targetOrient);
            }

            if (_removeAction.WasPressedThisFrame())
            {
                HandleRemoval();
            }
        }

        private void HandleFirstBoardTargeting()
        {
            // Place at fixed distance in front of camera, snapped to grid
            Vector3 targetWorld = cameraTransform.position + cameraTransform.forward * firstBoardDistance;
            Vector3Int gridPos = WorldToGrid(targetWorld);
            BoardOrientation orient = GetOrientationFromCamera();

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
                    _targetOrient = info.Orientation;
                }
            }
        }

        private void PlaceBoard(Vector3Int pos, BoardOrientation orient)
        {
            if (boardPrefab == null)
            {
                Debug.LogError("[BuildingController] Board prefab not assigned.");
                return;
            }

            Vector3 worldPos = PlacementTriggerManager.GridToWorld(pos, orient);
            Quaternion rotation = PlacementTriggerManager.GetBoardRotation(orient);

            GameObject board = Instantiate(boardPrefab, worldPos, rotation);
            board.name = $"Board_{pos}_{orient}";

            _grid.AddBoard(pos, orient, board);
        }

        private void HandleRemoval()
        {
            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, maxPlacementDistance, boardLayer))
            {
                // Find which board was hit by checking all boards near the hit point
                Vector3Int gridPos = WorldToGrid(hit.point);

                // Check this cell and immediate neighbors for the hit board
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            Vector3Int checkPos = gridPos + new Vector3Int(dx, dy, dz);
                            foreach (BoardOrientation orient in new[] { BoardOrientation.X, BoardOrientation.Y, BoardOrientation.Z })
                            {
                                GameObject board = _grid.GetBoard(checkPos, orient);
                                if (board != null && board == hit.collider.gameObject)
                                {
                                    _grid.RemoveBoard(checkPos, orient);
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }

        private BoardOrientation GetOrientationFromCamera()
        {
            Vector3 forward = cameraTransform.forward;
            float absX = Mathf.Abs(forward.x);
            float absY = Mathf.Abs(forward.y);
            float absZ = Mathf.Abs(forward.z);

            // Pick orientation whose normal most aligns with camera forward
            // X-board normal is Z, Y-board normal is X, Z-board normal is Y
            if (absZ >= absX && absZ >= absY) return BoardOrientation.X;  // looking along Z → XY wall
            if (absX >= absY && absX >= absZ) return BoardOrientation.Y;  // looking along X → YZ wall
            return BoardOrientation.Z;                                      // looking along Y → XZ floor
        }

        private static Vector3Int WorldToGrid(Vector3 worldPos)
        {
            const float CELL_SIZE = 4f;
            return new Vector3Int(
                Mathf.FloorToInt(worldPos.x / CELL_SIZE),
                Mathf.FloorToInt(worldPos.y / CELL_SIZE),
                Mathf.FloorToInt(worldPos.z / CELL_SIZE)
            );
        }
    }
}
```

**Step 2: Verify**

Open Unity, confirm no compilation errors.

**Step 3: Commit**

```bash
git add Assets/Scripts/Building/BuildingController.cs
git commit -m "feat: add BuildingController for player input and raycast-based placement/removal"
```

---

### Task 9: Editor setup script

**Files:**
- Create: `Assets/Scripts/Editor/BuildingSetupEditor.cs`

**Step 1: Create the editor utility**

This provides menu items to configure layers, create materials, and generate the board prefab without manual asset creation.

```csharp
using UnityEngine;
using UnityEditor;

namespace BuildingEditor
{
    public static class BuildingSetupEditor
    {
        [MenuItem("Tools/Building System/Setup All")]
        public static void SetupAll()
        {
            SetupLayers();
            GenerateMaterials();
            GenerateBoardPrefab();
            Debug.Log("[BuildingSetup] Setup complete.");
        }

        [MenuItem("Tools/Building System/Setup Layers")]
        public static void SetupLayers()
        {
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            SetLayer(layers, 8, "Board");
            SetLayer(layers, 9, "PlacementTrigger");

            tagManager.ApplyModifiedProperties();
            Debug.Log("[BuildingSetup] Layers configured: Board=8, PlacementTrigger=9.");
        }

        private static void SetLayer(SerializedProperty layers, int index, string name)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(index);
            if (string.IsNullOrEmpty(layer.stringValue) || layer.stringValue == name)
            {
                layer.stringValue = name;
            }
            else
            {
                Debug.LogWarning($"[BuildingSetup] Layer {index} already in use as '{layer.stringValue}'. Cannot set to '{name}'.");
            }
        }

        [MenuItem("Tools/Building System/Generate Materials")]
        public static void GenerateMaterials()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");

            // Board material — opaque tan
            CreateMaterialIfMissing("Assets/Materials/BoardMaterial.mat",
                new Color(0.7f, 0.6f, 0.5f, 1f), false);

            // Preview valid — transparent green
            CreateMaterialIfMissing("Assets/Materials/BoardPreviewValid.mat",
                new Color(0f, 1f, 0f, 0.4f), true);

            AssetDatabase.SaveAssets();
            Debug.Log("[BuildingSetup] Materials generated.");
        }

        private static void CreateMaterialIfMissing(string path, Color color, bool transparent)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[BuildingSetup] URP/Lit shader not found.");
                return;
            }

            Material mat = new Material(shader);
            mat.color = color;

            if (transparent)
            {
                mat.SetFloat("_Surface", 1); // Transparent
                mat.SetFloat("_Blend", 0);   // Alpha
                mat.SetFloat("_AlphaClip", 0);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            AssetDatabase.CreateAsset(mat, path);
        }

        [MenuItem("Tools/Building System/Generate Board Prefab")]
        public static void GenerateBoardPrefab()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            string prefabPath = "Assets/Prefabs/Board.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                Debug.Log("[BuildingSetup] Board prefab already exists.");
                return;
            }

            GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Board";
            board.transform.localScale = new Vector3(4f, 0.1f, 4f);
            board.layer = LayerMask.NameToLayer("Board");

            Material boardMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/BoardMaterial.mat");
            if (boardMat != null)
                board.GetComponent<MeshRenderer>().sharedMaterial = boardMat;

            PrefabUtility.SaveAsPrefabAsset(board, prefabPath);
            Object.DestroyImmediate(board);

            AssetDatabase.SaveAssets();
            Debug.Log("[BuildingSetup] Board prefab generated.");
        }
    }
}
```

**Step 2: Verify**

Open Unity. Go to menu Tools > Building System > Setup All. Confirm:
- Layers 8 and 9 are set to "Board" and "PlacementTrigger" in Project Settings > Tags and Layers
- Materials appear in Assets/Materials/
- Board prefab appears in Assets/Prefabs/

**Step 3: Commit**

```bash
git add Assets/Scripts/Editor/BuildingSetupEditor.cs
git commit -m "feat: add editor utility for building system setup (layers, materials, prefab)"
```

---

### Task 10: Scene setup and integration testing

**Files:**
- Modify: `Assets/Scenes/SampleScene.unity` (via Unity editor, not manually)

**Step 1: Run the editor setup**

In Unity menu: Tools > Building System > Setup All

**Step 2: Set up the scene**

Create the following hierarchy in SampleScene:

1. **Player** (empty GameObject at position 0, 2, -5):
   - Add `CharacterController` component (radius 0.5, height 2, center 0,0,0)
   - Add `PlayerInput` component (assign InputSystem_Actions asset, default map = Player)
   - Add `FirstPersonController` script (assign Camera transform reference)
   - Add `BuildingController` script:
     - Assign camera transform
     - Assign BoardPreview (see below)
     - Assign Board prefab from Assets/Prefabs/Board.prefab
     - Set placementTriggerLayer to "PlacementTrigger"
     - Set boardLayer to "Board"

2. **Main Camera** (child of Player, position 0, 0.8, 0):
   - Existing Camera + AudioListener
   - Add `BoardPreview` script (assign BoardPreviewValid material)

3. **BuildingSystem** (empty GameObject at origin):
   - Add `BuildingGrid` script
   - Add `PlacementTriggerManager` script

**Step 3: Manual test**

1. Enter Play mode
2. Look around with mouse, move with WASD — FPS controller should work
3. A green preview board should appear 8 units in front of camera, snapping to grid as you move/look
4. Left-click to place the first board — preview disappears, solid board appears, trigger colliders should spawn at 12 neighbor positions
5. Look at a trigger position — preview appears there
6. Left-click to place a second board adjacent to the first
7. Right-click on an existing board to remove it
8. Verify triggers update correctly after removal

**Step 4: Commit**

```bash
git add -A
git commit -m "feat: scene setup for building system integration"
```

---

### Task Summary

| Task | Component | Dependencies |
|---|---|---|
| 1 | BoardOrientation enum | None |
| 2 | BoardAdjacency lookup tables | Task 1 |
| 3 | BuildingGrid singleton | Tasks 1, 2 |
| 4 | TriggerInfo component | Task 1 |
| 5 | PlacementTriggerManager | Tasks 1, 2, 3, 4 |
| 6 | BoardPreview | None |
| 7 | Add Remove input action | None |
| 8 | BuildingController | Tasks 1, 3, 4, 5, 6, 7 |
| 9 | Editor setup script | None |
| 10 | Scene setup + integration test | All above |

Tasks 1, 4, 6, 7, 9 have no code dependencies and could be implemented in parallel. Tasks 2-3-5-8-10 form the critical path.
