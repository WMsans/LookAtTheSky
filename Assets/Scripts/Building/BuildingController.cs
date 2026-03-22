using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Building
{
    public class BuildingController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private BoardPreview boardPreview;

        [Header("Settings")]
        [SerializeField] private float maxPlacementDistance = 16f;
        [SerializeField] private float firstBoardDistance = 8f;
        [SerializeField] private LayerMask placementTriggerLayer;
        [SerializeField] private LayerMask boardLayer;

        private const BoardOrientation FULL = BoardOrientation.X | BoardOrientation.Y | BoardOrientation.Z;

        private BuildingGrid _grid;
        private InputAction _attackAction;
        private InputAction _removeAction;
        private InputAction _previousAction;
        private InputAction _nextAction;

        // Current target state
        private bool _hasTarget;
        private Vector3Int _targetPos;
        private BoardOrientation _targetOrient;

        // Active item state (set externally by bridge)
        private GameObject _activePrefab;
        private Inventory.PlacementMode _activePlacementMode;
        private bool _inputEnabled = true;

        /// <summary>Set by BuildingInventoryBridge when the active hotbar item changes.</summary>
        public GameObject ActivePrefab
        {
            get => _activePrefab;
            set
            {
                _activePrefab = value;
                if (boardPreview != null)
                    boardPreview.SetPreviewPrefab(value);
            }
        }

        /// <summary>Set by BuildingInventoryBridge when the active hotbar item changes.</summary>
        public Inventory.PlacementMode ActivePlacementMode
        {
            get => _activePlacementMode;
            set => _activePlacementMode = value;
        }

        // Events for decoupled integration
        public event Action<PlaceCancelEventArgs> OnBeforePlace;
        public event Action<Vector3Int, BoardOrientation, GameObject> OnBoardPlaced;
        public event Action<Vector3Int, BoardOrientation> OnPlaceFailed;
        public event Action<Vector3Int, BoardOrientation, GameObject> OnBeforeRemove;

        private void Awake()
        {
            var playerInput = GetComponent<PlayerInput>();
            _attackAction = playerInput.actions["Attack"];
            _removeAction = playerInput.actions["Remove"];
            _previousAction = playerInput.actions["Previous"];
            _nextAction = playerInput.actions["Next"];
        }

        private void Start()
        {
            _grid = BuildingGrid.Instance;
            if (_grid == null)
                Debug.LogError("[BuildingController] BuildingGrid.Instance not found.");

            // Subscribe to MouseManager for input toggling
            if (UI.MouseManager.Instance != null)
                UI.MouseManager.Instance.OnCursorStateChanged += HandleCursorStateChanged;
        }

        private void OnDestroy()
        {
            if (UI.MouseManager.Instance != null)
                UI.MouseManager.Instance.OnCursorStateChanged -= HandleCursorStateChanged;
        }

        private void HandleCursorStateChanged(bool isCursorFree)
        {
            _inputEnabled = !isCursorFree;
            if (!_inputEnabled)
            {
                _hasTarget = false;
                boardPreview.Hide();
            }
        }

        private void Update()
        {
            if (_grid == null) return;

            // Handle hotbar switching (always active)
            HandleHotbarInput();

            if (!_inputEnabled) return;

            _hasTarget = false;

            // Only target for placement if we have an active prefab
            if (_activePrefab != null)
            {
                if (_grid.IsEmpty())
                {
                    HandleFirstBoardTargeting();
                }
                else
                {
                    HandleNormalTargeting();
                }
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

            // Handle placement
            if (_hasTarget && _attackAction.WasPressedThisFrame())
            {
                PlaceBoard(_targetPos, _targetOrient);
            }

            // Handle removal (works even without active prefab)
            if (_removeAction.WasPressedThisFrame())
            {
                HandleRemoval();
            }
        }

        private void HandleHotbarInput()
        {
            // Number keys 1-9
            for (int i = 0; i < 9; i++)
            {
                if (Keyboard.current != null && Keyboard.current[Key.Digit1 + i].wasPressedThisFrame)
                {
                    Inventory.InventoryManager.Instance?.SetActiveSlot(i);
                    return;
                }
            }

            // Previous/Next actions (scroll wheel, keyboard, or gamepad dpad)
            if (_previousAction.WasPressedThisFrame())
            {
                var mgr = Inventory.InventoryManager.Instance;
                if (mgr != null)
                {
                    int current = mgr.Inventory.ActiveHotbarIndex;
                    mgr.SetActiveSlot((current - 1 + Inventory.Inventory.HOTBAR_SIZE) % Inventory.Inventory.HOTBAR_SIZE);
                }
            }
            if (_nextAction.WasPressedThisFrame())
            {
                var mgr = Inventory.InventoryManager.Instance;
                if (mgr != null)
                {
                    int current = mgr.Inventory.ActiveHotbarIndex;
                    mgr.SetActiveSlot((current + 1) % Inventory.Inventory.HOTBAR_SIZE);
                }
            }
        }

        private void HandleFirstBoardTargeting()
        {
            Vector3 targetWorld = cameraTransform.position + cameraTransform.forward * firstBoardDistance;
            Vector3Int gridPos = WorldToGrid(targetWorld);

            BoardOrientation orient;
            if (_activePlacementMode == Inventory.PlacementMode.FullCell)
                orient = FULL;
            else
                orient = GetOrientationFromCamera();

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

                    if (_activePlacementMode == Inventory.PlacementMode.FullCell)
                        _targetOrient = FULL;
                    else
                        _targetOrient = info.Orientation;
                }
            }
        }

        private void PlaceBoard(Vector3Int pos, BoardOrientation orient)
        {
            if (_activePrefab == null)
            {
                Debug.LogError("[BuildingController] No active prefab assigned.");
                return;
            }

            // Fire OnBeforePlace to allow cancellation (e.g., inventory check)
            var args = new PlaceCancelEventArgs(pos, orient);
            OnBeforePlace?.Invoke(args);
            if (args.Cancel) return;

            Vector3 worldPos = PlacementTriggerManager.GridToWorld(pos, orient);
            Quaternion rotation = PlacementTriggerManager.GetBoardRotation(orient);

            GameObject board = Instantiate(_activePrefab, worldPos, rotation);
            board.name = $"Board_{pos}_{orient}";

            if (_grid.AddBoard(pos, orient, board))
            {
                OnBoardPlaced?.Invoke(pos, orient, board);
            }
            else
            {
                Destroy(board);
                OnPlaceFailed?.Invoke(pos, orient);
            }
        }

        private void HandleRemoval()
        {
            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, maxPlacementDistance, boardLayer))
            {
                Vector3Int cell = WorldToGrid(hit.point);

                // Try each orientation to find which board owns this cell
                foreach (BoardOrientation orient in new[] { BoardOrientation.X, BoardOrientation.Y, BoardOrientation.Z, FULL })
                {
                    Vector3Int? anchor = _grid.GetAnchor(cell, orient);
                    if (!anchor.HasValue) continue;

                    GameObject board = _grid.GetBoard(anchor.Value, orient);
                    if (board != null && board == hit.collider.gameObject)
                    {
                        OnBeforeRemove?.Invoke(anchor.Value, orient, board);
                        _grid.RemoveBoard(anchor.Value, orient);
                        return;
                    }
                }

                // Fallback: check immediate neighboring cells (hit point may land on cell boundary)
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            if (dx == 0 && dy == 0 && dz == 0) continue;
                            Vector3Int checkCell = cell + new Vector3Int(dx, dy, dz);
                            foreach (BoardOrientation orient in new[] { BoardOrientation.X, BoardOrientation.Y, BoardOrientation.Z, FULL })
                            {
                                Vector3Int? anchor = _grid.GetAnchor(checkCell, orient);
                                if (!anchor.HasValue) continue;

                                GameObject board = _grid.GetBoard(anchor.Value, orient);
                                if (board != null && board == hit.collider.gameObject)
                                {
                                    OnBeforeRemove?.Invoke(anchor.Value, orient, board);
                                    _grid.RemoveBoard(anchor.Value, orient);
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

            if (absZ >= absX && absZ >= absY) return BoardOrientation.X;
            if (absX >= absY && absX >= absZ) return BoardOrientation.Y;
            return BoardOrientation.Z;
        }

        private static Vector3Int WorldToGrid(Vector3 worldPos)
        {
            return new Vector3Int(
                Mathf.FloorToInt(worldPos.x / GridConstants.CELL_SIZE),
                Mathf.FloorToInt(worldPos.y / GridConstants.CELL_SIZE),
                Mathf.FloorToInt(worldPos.z / GridConstants.CELL_SIZE)
            );
        }
    }

    public class PlaceCancelEventArgs : EventArgs
    {
        public Vector3Int Position { get; }
        public BoardOrientation Orientation { get; }
        public bool Cancel { get; set; }

        public PlaceCancelEventArgs(Vector3Int pos, BoardOrientation orient)
        {
            Position = pos;
            Orientation = orient;
        }
    }
}
