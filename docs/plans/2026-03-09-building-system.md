Building System Implementation Plan
> For Claude: REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.
Goal: Implement a Rust-style grid-based building system with first-person controls, board placement/removal via raycasting, and a ghost preview.
Architecture: Dictionary-based virtual grid where each cell is 4m cubed with 6 faces. A BuildingGrid singleton owns the data, BuildingSystem handles input/raycasting/preview, BoardVisuals provides static position math, and a FirstPersonController handles movement. Boards snap to cell faces; placement requires adjacency to an existing board.
Tech Stack: Unity 6 (6000.3.10f1), URP, New Input System 1.18.0, C#
---
Task 1: Add "Remove" Action to Input System
Files:
- Modify: Assets/InputSystem_Actions.inputactions
Step 1: Add the Remove action
Add a new Remove action to the Player action map, type Button, bound to <Mouse>/rightButton in the Keyboard&Mouse group. Insert it after the existing Attack action in the JSON. Use a new unique GUID for the action and binding IDs.
{
    "name": "Remove",
    "type": "Button",
    "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "expectedControlType": "Button",
    "processors": "",
    "interactions": "",
    "initialStateCheck": false
}
Binding:
{
    "name": "",
    "id": "f0e1d2c3-b4a5-6789-0abc-def123456789",
    "path": "<Mouse>/rightButton",
    "interactions": "",
    "processors": "",
    "groups": ";Keyboard&Mouse",
    "action": "Remove",
    "isComposite": false,
    "isPartOfComposite": false
}
Step 2: Commit
git add Assets/InputSystem_Actions.inputactions
git commit -m "feat: add Remove action (right click) to Player input map"
---
Task 2: Create BoardFace Enum and BoardVisuals Utility
Files:
- Create: Assets/Scripts/Building/BoardFace.cs
- Create: Assets/Scripts/Building/BoardVisuals.cs
Step 1: Create directory structure
Create Assets/Scripts/Building/ directory.
Step 2: Write BoardFace.cs
namespace Building
{
    public enum BoardFace
    {
        Top = 0,
        Bottom = 1,
        North = 2,   // +Z
        South = 3,   // -Z
        East = 4,    // +X
        West = 5     // -X
    }
}
Step 3: Write BoardVisuals.cs
Static utility that converts grid coordinates to world transforms.
using UnityEngine;
namespace Building
{
    public static class BoardVisuals
    {
        public const float CellSize = 4f;
        /// Returns the world-space center of a board placed on the given face of the given cell.
        public static Vector3 GetWorldPosition(Vector3Int cell, BoardFace face)
        {
            Vector3 cellCenter = new Vector3(
                (cell.x + 0.5f) * CellSize,
                (cell.y + 0.5f) * CellSize,
                (cell.z + 0.5f) * CellSize
            );
            float half = CellSize * 0.5f;
            return face switch
            {
                BoardFace.Top    => cellCenter + new Vector3(0, half, 0),
                BoardFace.Bottom => cellCenter + new Vector3(0, -half, 0),
                BoardFace.North  => cellCenter + new Vector3(0, 0, half),
                BoardFace.South  => cellCenter + new Vector3(0, 0, -half),
                BoardFace.East   => cellCenter + new Vector3(half, 0, 0),
                BoardFace.West   => cellCenter + new Vector3(-half, 0, 0),
                _ => cellCenter
            };
        }
        /// Returns the rotation for a board on the given face.
        /// Boards are 4x4 quads. Default orientation is flat on XZ plane (floor).
        public static Quaternion GetWorldRotation(BoardFace face)
        {
            return face switch
            {
                BoardFace.Top    => Quaternion.identity,                          // flat, normal up
                BoardFace.Bottom => Quaternion.Euler(180f, 0f, 0f),              // flat, normal down
                BoardFace.North  => Quaternion.Euler(90f, 0f, 0f),               // vertical, normal +Z
                BoardFace.South  => Quaternion.Euler(-90f, 0f, 0f),              // vertical, normal -Z
                BoardFace.East   => Quaternion.Euler(0f, 0f, -90f),              // vertical, normal +X
                BoardFace.West   => Quaternion.Euler(0f, 0f, 90f),               // vertical, normal -X
                _ => Quaternion.identity
            };
        }
        /// Converts a world position to the grid cell it falls within.
        public static Vector3Int WorldToCell(Vector3 worldPos)
        {
            return new Vector3Int(
                Mathf.FloorToInt(worldPos.x / CellSize),
                Mathf.FloorToInt(worldPos.y / CellSize),
                Mathf.FloorToInt(worldPos.z / CellSize)
            );
        }
        /// Converts a world-space normal vector to the closest BoardFace.
        public static BoardFace NormalToFace(Vector3 normal)
        {
            Vector3 abs = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
            if (abs.y >= abs.x && abs.y >= abs.z)
                return normal.y >= 0 ? BoardFace.Top : BoardFace.Bottom;
            if (abs.x >= abs.y && abs.x >= abs.z)
                return normal.x >= 0 ? BoardFace.East : BoardFace.West;
            return normal.z >= 0 ? BoardFace.North : BoardFace.South;
        }
        /// Returns the opposite face.
        public static BoardFace OppositeFace(BoardFace face)
        {
            return face switch
            {
                BoardFace.Top    => BoardFace.Bottom,
                BoardFace.Bottom => BoardFace.Top,
                BoardFace.North  => BoardFace.South,
                BoardFace.South  => BoardFace.North,
                BoardFace.East   => BoardFace.West,
                BoardFace.West   => BoardFace.East,
                _ => face
            };
        }
        /// Returns the neighbor cell offset for a given face direction.
        public static Vector3Int FaceToOffset(BoardFace face)
        {
            return face switch
            {
                BoardFace.Top    => Vector3Int.up,
                BoardFace.Bottom => Vector3Int.down,
                BoardFace.North  => new Vector3Int(0, 0, 1),
                BoardFace.South  => new Vector3Int(0, 0, -1),
                BoardFace.East   => Vector3Int.right,
                BoardFace.West   => Vector3Int.left,
                _ => Vector3Int.zero
            };
        }
    }
}
Step 4: Commit
git add Assets/Scripts/
git commit -m "feat: add BoardFace enum and BoardVisuals utility"
---
Task 3: Create BuildingGrid Singleton
Files:
- Create: Assets/Scripts/Building/BuildingGrid.cs
Step 1: Write BuildingGrid.cs
using System.Collections.Generic;
using UnityEngine;
namespace Building
{
    public struct BoardKey
    {
        public Vector3Int Cell;
        public BoardFace Face;
        public BoardKey(Vector3Int cell, BoardFace face)
        {
            Cell = cell;
            Face = face;
        }
    }
    public class BuildingGrid : MonoBehaviour
    {
        public static BuildingGrid Instance { get; private set; }
        [SerializeField] private GameObject boardPrefab;
        private readonly Dictionary<BoardKey, GameObject> _boards = new();
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }
        public bool IsEmpty() => _boards.Count == 0;
        /// Normalizes a (cell, face) pair to canonical form.
        /// The canonical form stores a shared face on the cell with the lower coordinate.
        /// e.g. Top of (1,0,0) is canonical; Bottom of (1,1,0) normalizes to Top of (1,0,0).
        public static BoardKey Canonicalize(Vector3Int cell, BoardFace face)
        {
            switch (face)
            {
                case BoardFace.Bottom:
                    return new BoardKey(cell + Vector3Int.down, BoardFace.Top);
                case BoardFace.South:
                    return new BoardKey(cell + new Vector3Int(0, 0, -1), BoardFace.North);
                case BoardFace.West:
                    return new BoardKey(cell + Vector3Int.left, BoardFace.East);
                default:
                    return new BoardKey(cell, face);
            }
        }
        public bool HasBoard(Vector3Int cell, BoardFace face)
        {
            var key = Canonicalize(cell, face);
            return _boards.ContainsKey(key);
        }
        /// Checks if the given face has at least one neighboring board.
        /// Neighbors: other faces of the same cell, or faces of adjacent cells that share an edge.
        public bool HasNeighbor(Vector3Int cell, BoardFace face)
        {
            // Check all 6 faces of the same cell (excluding the target face itself)
            for (int i = 0; i < 6; i++)
            {
                BoardFace f = (BoardFace)i;
                if (f == face) continue;
                if (HasBoard(cell, f)) return true;
            }
            // Check the same face on each of the 6 neighboring cells
            for (int i = 0; i < 6; i++)
            {
                BoardFace f = (BoardFace)i;
                Vector3Int neighborCell = cell + BoardVisuals.FaceToOffset(f);
                // Check if the neighbor cell has a board on the same face type
                if (HasBoard(neighborCell, face)) return true;
            }
            return false;
        }
        /// Attempts to place a board. Returns true if successful.
        public bool TryPlace(Vector3Int cell, BoardFace face)
        {
            var key = Canonicalize(cell, face);
            if (_boards.ContainsKey(key)) return false;
            if (!IsEmpty() && !HasNeighbor(cell, face)) return false;
            Vector3 pos = BoardVisuals.GetWorldPosition(key.Cell, key.Face);
            Quaternion rot = BoardVisuals.GetWorldRotation(key.Face);
            GameObject board = Instantiate(boardPrefab, pos, rot);
            _boards[key] = board;
            return true;
        }
        /// Removes the board at the given face. Returns true if a board was removed.
        public bool Remove(Vector3Int cell, BoardFace face)
        {
            var key = Canonicalize(cell, face);
            if (!_boards.TryGetValue(key, out GameObject board)) return false;
            _boards.Remove(key);
            Destroy(board);
            return true;
        }
        /// Finds the BoardKey for a placed board GameObject. Used for removal by raycast hit.
        public bool TryGetKeyForBoard(GameObject boardObj, out BoardKey key)
        {
            foreach (var kvp in _boards)
            {
                if (kvp.Value == boardObj)
                {
                    key = kvp.Key;
                    return true;
                }
            }
            key = default;
            return false;
        }
        /// Returns whether placement would be valid at this location.
        public bool IsValidPlacement(Vector3Int cell, BoardFace face)
        {
            var key = Canonicalize(cell, face);
            if (_boards.ContainsKey(key)) return false;
            if (IsEmpty()) return true;
            return HasNeighbor(cell, face);
        }
    }
}
Step 2: Commit
git add Assets/Scripts/Building/BuildingGrid.cs
git commit -m "feat: add BuildingGrid singleton with dictionary storage and neighbor validation"
---
Task 4: Create BuildingSystem (Raycast + Preview + Input)
Files:
- Create: Assets/Scripts/Building/BuildingSystem.cs
Step 1: Write BuildingSystem.cs
using UnityEngine;
using UnityEngine.InputSystem;
namespace Building
{
    public class BuildingSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BuildingGrid grid;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private GameObject previewObject;
        [Header("Materials")]
        [SerializeField] private Material previewValidMaterial;
        [SerializeField] private Material previewInvalidMaterial;
        [Header("Settings")]
        [SerializeField] private float maxRange = 16f;
        [SerializeField] private LayerMask buildableLayers;  // Ground + Board layers
        private InputAction _placeAction;
        private InputAction _removeAction;
        private MeshRenderer _previewRenderer;
        private bool _hasTarget;
        private Vector3Int _targetCell;
        private BoardFace _targetFace;
        private bool _targetValid;
        private void Awake()
        {
            var playerInput = GetComponent<PlayerInput>();
            _placeAction = playerInput.actions["Attack"];
            _removeAction = playerInput.actions["Remove"];
            if (previewObject != null)
            {
                _previewRenderer = previewObject.GetComponent<MeshRenderer>();
                previewObject.SetActive(false);
            }
        }
        private void OnEnable()
        {
            _placeAction.performed += OnPlace;
            _removeAction.performed += OnRemove;
        }
        private void OnDisable()
        {
            _placeAction.performed -= OnPlace;
            _removeAction.performed -= OnRemove;
        }
        private void Update()
        {
            UpdateTarget();
            UpdatePreview();
        }
        private void UpdateTarget()
        {
            _hasTarget = false;
            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, maxRange, buildableLayers))
                return;
            // Determine which face of which cell we're targeting
            Vector3 normal = hit.normal;
            BoardFace hitFace = BoardVisuals.NormalToFace(normal);
            // If we hit an existing board, we want to place on the adjacent cell
            // If we hit the ground plane, we place on the cell at that position
            int boardLayer = LayerMask.NameToLayer("Board");
            if (hit.collider.gameObject.layer == boardLayer)
            {
                // Hit an existing board — target the adjacent cell in the direction of the normal
                // First, figure out which cell/face this board belongs to
                Vector3 boardPos = hit.collider.transform.position;
                Vector3Int boardCell = BoardVisuals.WorldToCell(boardPos);
                // The new board goes on the neighboring cell in the hit normal direction
                _targetCell = boardCell + BoardVisuals.FaceToOffset(hitFace);
                _targetFace = BoardVisuals.OppositeFace(hitFace);
            }
            else
            {
                // Hit ground or other surface
                // Nudge the hit point slightly inward along the normal to get the correct cell
                Vector3 samplePoint = hit.point - normal * 0.01f;
                _targetCell = BoardVisuals.WorldToCell(samplePoint);
                _targetFace = hitFace;
            }
            _hasTarget = true;
            _targetValid = grid.IsValidPlacement(_targetCell, _targetFace);
        }
        private void UpdatePreview()
        {
            if (previewObject == null) return;
            if (!_hasTarget)
            {
                previewObject.SetActive(false);
                return;
            }
            previewObject.SetActive(true);
            // Use canonical position for preview so it matches where the board will actually be placed
            var canonical = BuildingGrid.Canonicalize(_targetCell, _targetFace);
            previewObject.transform.position = BoardVisuals.GetWorldPosition(canonical.Cell, canonical.Face);
            previewObject.transform.rotation = BoardVisuals.GetWorldRotation(canonical.Face);
            _previewRenderer.sharedMaterial = _targetValid ? previewValidMaterial : previewInvalidMaterial;
        }
        private void OnPlace(InputAction.CallbackContext ctx)
        {
            if (!_hasTarget || !_targetValid) return;
            grid.TryPlace(_targetCell, _targetFace);
        }
        private void OnRemove(InputAction.CallbackContext ctx)
        {
            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
            int boardLayerMask = 1 << LayerMask.NameToLayer("Board");
            if (!Physics.Raycast(ray, out RaycastHit hit, maxRange, boardLayerMask))
                return;
            // Find this board in the grid and remove it
            if (grid.TryGetKeyForBoard(hit.collider.gameObject, out BoardKey key))
            {
                grid.Remove(key.Cell, key.Face);
            }
        }
    }
}
Step 2: Commit
git add Assets/Scripts/Building/BuildingSystem.cs
git commit -m "feat: add BuildingSystem with raycast targeting, preview, and place/remove input"
---
Task 5: Create FirstPersonController
Files:
- Create: Assets/Scripts/Player/FirstPersonController.cs
Step 1: Write FirstPersonController.cs
using UnityEngine;
using UnityEngine.InputSystem;
namespace Player
{
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float jumpHeight = 1.5f;
        [Header("Mouse Look")]
        [SerializeField] private float mouseSensitivity = 0.15f;
        [SerializeField] private Transform cameraTransform;
        private CharacterController _controller;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _jumpAction;
        private float _verticalVelocity;
        private float _cameraPitch;
        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            var playerInput = GetComponent<PlayerInput>();
            _moveAction = playerInput.actions["Move"];
            _lookAction = playerInput.actions["Look"];
            _jumpAction = playerInput.actions["Jump"];
        }
        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        private void Update()
        {
            HandleLook();
            HandleMovement();
        }
        private void HandleLook()
        {
            Vector2 lookDelta = _lookAction.ReadValue<Vector2>();
            _cameraPitch -= lookDelta.y * mouseSensitivity;
            _cameraPitch = Mathf.Clamp(_cameraPitch, -90f, 90f);
            cameraTransform.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
            transform.Rotate(Vector3.up * lookDelta.x * mouseSensitivity);
        }
        private void HandleMovement()
        {
            Vector2 moveInput = _moveAction.ReadValue<Vector2>();
            Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
            move *= moveSpeed;
            if (_controller.isGrounded)
            {
                _verticalVelocity = -2f; // Small downward force to keep grounded
                if (_jumpAction.WasPressedThisFrame())
                {
                    _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }
            }
            else
            {
                _verticalVelocity += gravity * Time.deltaTime;
            }
            move.y = _verticalVelocity;
            _controller.Move(move * Time.deltaTime);
        }
    }
}
Step 2: Commit
git add Assets/Scripts/Player/
git commit -m "feat: add first-person controller with WASD movement, mouse look, and jump"
---
Task 6: Create Materials (via C# editor script or manual)
Since Unity materials are binary .mat assets that can't be created purely from text, we need a small editor script to generate them on first run.
Files:
- Create: Assets/Scripts/Editor/MaterialGenerator.cs
Step 1: Write MaterialGenerator.cs
This editor utility creates the 3 materials we need if they don't already exist. Run it once from the Unity menu.
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
namespace BuildingEditor
{
    public static class MaterialGenerator
    {
        [MenuItem("Tools/Building System/Generate Materials")]
        public static void GenerateMaterials()
        {
            string folder = "Assets/Materials";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", "Materials");
            // Board material - opaque, light gray
            CreateMaterial(folder, "BoardMaterial",
                new Color(0.7f, 0.6f, 0.5f, 1f),
                renderingMode: 0); // Opaque
            // Preview valid - semi-transparent green
            CreateMaterial(folder, "BoardPreviewValid",
                new Color(0f, 1f, 0f, 0.4f),
                renderingMode: 3); // Transparent
            // Preview invalid - semi-transparent red
            CreateMaterial(folder, "BoardPreviewInvalid",
                new Color(1f, 0f, 0f, 0.4f),
                renderingMode: 3); // Transparent
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Building materials generated in Assets/Materials/");
        }
        private static void CreateMaterial(string folder, string name, Color color, int renderingMode)
        {
            string path = $"{folder}/{name}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
                return;
            // Use URP Lit shader
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("URP Lit shader not found");
                return;
            }
            Material mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            if (renderingMode == 3) // Transparent
            {
                mat.SetFloat("_Surface", 1); // 0 = Opaque, 1 = Transparent
                mat.SetFloat("_Blend", 0);   // Alpha blend
                mat.SetFloat("_AlphaClip", 0);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            AssetDatabase.CreateAsset(mat, path);
        }
    }
}
#endif
Step 2: Commit
git add Assets/Scripts/Editor/
git commit -m "feat: add editor utility to generate building materials"
---
Task 7: Create Board Prefab Generator (Editor Script)
Since prefabs are binary Unity assets, we also need an editor script.
Files:
- Modify: Assets/Scripts/Editor/MaterialGenerator.cs (rename to BuildingSetupEditor.cs and expand)
Actually, let's make a separate script for clarity:
Files:
- Create: Assets/Scripts/Editor/BuildingSetupEditor.cs
Replace MaterialGenerator.cs with a combined setup script:
Step 1: Write BuildingSetupEditor.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
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
            Debug.Log("Building system setup complete!");
        }
        [MenuItem("Tools/Building System/Setup Layers")]
        public static void SetupLayers()
        {
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            // Find an empty layer slot (skip 0-7 which are built-in)
            // Layer 8 is already "Socket", use next available
            bool found = false;
            for (int i = 8; i < 32; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                if (layer.stringValue == "Board")
                {
                    Debug.Log($"Board layer already exists at index {i}");
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
                        layer.stringValue = "Board";
                        tagManager.ApplyModifiedProperties();
                        Debug.Log($"Added Board layer at index {i}");
                        break;
                    }
                }
            }
        }
        [MenuItem("Tools/Building System/Generate Materials")]
        public static void GenerateMaterials()
        {
            string folder = "Assets/Materials";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", "Materials");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("URP Lit shader not found!");
                return;
            }
            CreateMaterial(folder, "BoardMaterial", shader,
                new Color(0.7f, 0.6f, 0.5f, 1f), transparent: false);
            CreateMaterial(folder, "BoardPreviewValid", shader,
                new Color(0f, 1f, 0f, 0.4f), transparent: true);
            CreateMaterial(folder, "BoardPreviewInvalid", shader,
                new Color(1f, 0f, 0f, 0.4f), transparent: true);
            AssetDatabase.SaveAssets();
            Debug.Log("Materials generated in Assets/Materials/");
        }
        [MenuItem("Tools/Building System/Generate Board Prefab")]
        public static void GenerateBoardPrefab()
        {
            string folder = "Assets/Prefabs";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            string prefabPath = $"{folder}/Board.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                Debug.Log("Board prefab already exists");
                return;
            }
            // Create a 4x4 board as a scaled cube (thin box)
            GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Board";
            board.transform.localScale = new Vector3(4f, 0.1f, 4f);
            board.layer = LayerMask.NameToLayer("Board");
            // Apply material
            Material boardMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/BoardMaterial.mat");
            if (boardMat != null)
                board.GetComponent<MeshRenderer>().sharedMaterial = boardMat;
            // Save as prefab
            PrefabUtility.SaveAsPrefabAsset(board, prefabPath);
            Object.DestroyImmediate(board);
            Debug.Log("Board prefab generated at Assets/Prefabs/Board.prefab");
        }
        private static void CreateMaterial(string folder, string name, Shader shader, Color color, bool transparent)
        {
            string path = $"{folder}/{name}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            Material mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            if (transparent)
            {
                mat.SetFloat("_Surface", 1);
                mat.SetFloat("_AlphaClip", 0);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            AssetDatabase.CreateAsset(mat, path);
        }
    }
}
#endif
Step 2: Delete the old MaterialGenerator.cs if it was created in Task 6 (skip Task 6 entirely, this replaces it)
Step 3: Commit
git add Assets/Scripts/Editor/
git commit -m "feat: add editor setup utility for layers, materials, and board prefab"
---
Task 8: Set Up the Scene
Files:
- Create: Assets/Scripts/Editor/SceneSetupEditor.cs
This editor script sets up the SampleScene with the Player and BuildingGrid when run from the menu.
Step 1: Write SceneSetupEditor.cs
#if UNITY_EDITOR
using Building;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Player;
namespace BuildingEditor
{
    public static class SceneSetupEditor
    {
        [MenuItem("Tools/Building System/Setup Scene")]
        public static void SetupScene()
        {
            // --- Ground Plane (invisible raycast target) ---
            GameObject ground = GameObject.Find("GroundPlane");
            if (ground == null)
            {
                ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "GroundPlane";
                ground.transform.position = Vector3.zero;
                ground.transform.localScale = new Vector3(100f, 1f, 100f); // 1000x1000 meter plane
                // Make invisible but keep collider
                ground.GetComponent<MeshRenderer>().enabled = false;
            }
            // --- Building Grid ---
            GameObject gridObj = GameObject.Find("BuildingGrid");
            if (gridObj == null)
            {
                gridObj = new GameObject("BuildingGrid");
                var grid = gridObj.AddComponent<BuildingGrid>();
                // Assign board prefab
                GameObject boardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Board.prefab");
                if (boardPrefab != null)
                {
                    SerializedObject so = new SerializedObject(grid);
                    so.FindProperty("boardPrefab").objectReferenceValue = boardPrefab;
                    so.ApplyModifiedProperties();
                }
            }
            // --- Player ---
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                player = new GameObject("Player");
                player.transform.position = new Vector3(8f, 1.5f, 8f);
                // CharacterController
                var cc = player.AddComponent<CharacterController>();
                cc.height = 1.8f;
                cc.center = new Vector3(0f, 0.9f, 0f);
                cc.radius = 0.3f;
                // Camera (child)
                // Remove existing Main Camera if present
                GameObject oldCam = GameObject.FindGameObjectWithTag("MainCamera");
                if (oldCam != null)
                    Object.DestroyImmediate(oldCam);
                GameObject camObj = new GameObject("PlayerCamera");
                camObj.transform.SetParent(player.transform);
                camObj.transform.localPosition = new Vector3(0f, 1.6f, 0f);
                camObj.tag = "MainCamera";
                var cam = camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();
                // Preview object (child of camera, but we'll un-parent it at runtime)
                GameObject preview = GameObject.CreatePrimitive(PrimitiveType.Cube);
                preview.name = "BoardPreview";
                preview.transform.localScale = new Vector3(4f, 0.1f, 4f);
                // Remove collider so it doesn't interfere with raycasts
                Object.DestroyImmediate(preview.GetComponent<Collider>());
                Material previewMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/BoardPreviewValid.mat");
                if (previewMat != null)
                    preview.GetComponent<MeshRenderer>().sharedMaterial = previewMat;
                // PlayerInput component
                var playerInput = player.AddComponent<PlayerInput>();
                var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
                if (inputActions != null)
                {
                    playerInput.actions = inputActions;
                    playerInput.defaultActionMap = "Player";
                }
                // FirstPersonController
                var fps = player.AddComponent<FirstPersonController>();
                SerializedObject fpsSO = new SerializedObject(fps);
                fpsSO.FindProperty("cameraTransform").objectReferenceValue = camObj.transform;
                fpsSO.ApplyModifiedProperties();
                // BuildingSystem
                var buildSys = player.AddComponent<BuildingSystem>();
                SerializedObject bsSO = new SerializedObject(buildSys);
                bsSO.FindProperty("grid").objectReferenceValue =
                    GameObject.Find("BuildingGrid").GetComponent<BuildingGrid>();
                bsSO.FindProperty("cameraTransform").objectReferenceValue = camObj.transform;
                bsSO.FindProperty("previewObject").objectReferenceValue = preview;
                Material validMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/BoardPreviewValid.mat");
                Material invalidMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/BoardPreviewInvalid.mat");
                bsSO.FindProperty("previewValidMaterial").objectReferenceValue = validMat;
                bsSO.FindProperty("previewInvalidMaterial").objectReferenceValue = invalidMat;
                // Set buildable layers to Default + Board
                int defaultLayer = 1 << 0;
                int boardLayer = 1 << LayerMask.NameToLayer("Board");
                bsSO.FindProperty("buildableLayers").intValue = defaultLayer | boardLayer;
                bsSO.ApplyModifiedProperties();
            }
            EditorUtility.SetDirty(gridObj);
            Debug.Log("Scene setup complete! Press Play to test.");
        }
    }
}
#endif
Step 2: Commit
git add Assets/Scripts/Editor/SceneSetupEditor.cs
git commit -m "feat: add scene setup editor script to wire up player, grid, and ground"
---
Task 9: Manual Testing Checklist & Final Commit
Step 1: Open Unity, run the setup
1. Open the project in Unity 6
2. Menu: Tools > Building System > Setup All (creates layers, materials, board prefab)
3. Menu: Tools > Building System > Setup Scene (wires up the scene)
4. Press Play
Step 2: Test these scenarios
- [ ] WASD moves the player, mouse looks around
- [ ] Cursor is locked to center
- [ ] Looking at the ground shows a green preview board
- [ ] Left clicking places a board on the ground
- [ ] Looking at a placed board and aiming outward shows preview on adjacent face
- [ ] Placing next to an existing board works (neighbor check passes)
- [ ] Trying to place with no neighbor shows red preview
- [ ] Right clicking on a placed board removes it
- [ ] Can build walls by aiming at the side of a floor board
- [ ] Can build ceiling by aiming at the top of a wall board
- [ ] Preview disappears when looking at the sky
- [ ] Can't place two boards on the same face (red preview)
Step 3: Fix any issues found during testing, then final commit
git add -A
git commit -m "feat: building system complete — place, remove, preview, FPS controller"
---
Task Execution Order Summary
Task	Description
1	Add Remove input action
2	BoardFace enum + BoardVisuals
3	BuildingGrid singleton
4	BuildingSystem (raycast/preview/input)
5	FirstPersonController
6	(Merged into Task 7)
7	Editor setup: layers, materials, prefab
8	Scene setup editor script
9	Manual testing in Unity
Tasks 1, 2, 5, and 7 can run in parallel. Task 3 depends on 2. Task 4 depends on 1+2+3. Task 8 depends on everything. Task 9 is manual.
