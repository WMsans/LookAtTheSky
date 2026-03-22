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

        private bool _hasTarget;
        private Vector3Int _targetPos;
        private BoardOrientation _targetOrient;
        private Quaternion _targetRotation;

        private GameObject _activePrefab;
        private Inventory.PlacementMode _activePlacementMode;
        private bool _inputEnabled = true;

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

        public Inventory.PlacementMode ActivePlacementMode
        {
            get => _activePlacementMode;
            set => _activePlacementMode = value;
        }

        public event Action<PlaceCancelEventArgs> OnBeforePlace;
        public event Action<IOccupant> OnOccupantPlaced;
        public event Action<Vector3Int, BoardOrientation> OnPlaceFailed;
        public event Action<IOccupant> OnBeforeRemove;

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

            HandleHotbarInput();

            if (!_inputEnabled) return;

            _hasTarget = false;

            if (_activePrefab != null)
            {
                if (_activePlacementMode == Inventory.PlacementMode.SmallBlock)
                {
                    HandleSmallBlockTargeting();
                }
                else if (_grid.IsEmpty())
                {
                    HandleFirstBoardTargeting();
                }
                else
                {
                    HandleNormalTargeting();
                }
            }

            if (_hasTarget)
            {
                Vector3 worldPos;
                Quaternion rotation;

                if (_activePlacementMode == Inventory.PlacementMode.SmallBlock)
                {
                    worldPos = GridConstants.SmallBlockGridToWorld(_targetPos);
                    rotation = _targetRotation;
                }
                else
                {
                    worldPos = PlacementTriggerManager.GridToWorld(_targetPos, _targetOrient);
                    rotation = PlacementTriggerManager.GetBoardRotation(_targetOrient);
                }

                boardPreview.ShowAt(worldPos, rotation);
            }
            else
            {
                boardPreview.Hide();
            }

            if (_hasTarget && _attackAction.WasPressedThisFrame())
            {
                if (_activePlacementMode == Inventory.PlacementMode.SmallBlock)
                    PlaceSmallBlock(_targetPos, _targetRotation);
                else
                    PlaceBoard(_targetPos, _targetOrient);
            }

            if (_removeAction.WasPressedThisFrame())
            {
                HandleRemoval();
            }
        }

        private void HandleHotbarInput()
        {
            for (int i = 0; i < 9; i++)
            {
                if (Keyboard.current != null && Keyboard.current[Key.Digit1 + i].wasPressedThisFrame)
                {
                    Inventory.InventoryManager.Instance?.SetActiveSlot(i);
                    return;
                }
            }

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

        private void HandleSmallBlockTargeting()
        {
            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

            if (!Physics.Raycast(ray, out RaycastHit hit, maxPlacementDistance, boardLayer))
                return;

            Vector3Int targetCell = WorldToGrid(hit.point + hit.normal * 0.5f);

            if (!_grid.HasAnyFaceNeighbor(targetCell))
                return;

            if (_grid.HasOccupant(targetCell, OccupantType.SmallBlock))
                return;

            Vector3 blockWorldPos = GridConstants.SmallBlockGridToWorld(targetCell);
            Quaternion rotation = SmallBlockRotation.ComputeRotation(hit.normal, cameraTransform.position, blockWorldPos);

            _hasTarget = true;
            _targetPos = targetCell;
            _targetRotation = rotation;
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

            var args = new PlaceCancelEventArgs(pos, orient);
            OnBeforePlace?.Invoke(args);
            if (args.Cancel) return;

            Vector3 worldPos = PlacementTriggerManager.GridToWorld(pos, orient);
            Quaternion rotation = PlacementTriggerManager.GetBoardRotation(orient);

            GameObject board = Instantiate(_activePrefab, worldPos, rotation);
            board.name = $"Board_{pos}_{orient}";

            if (_grid.AddBoard(pos, orient, board))
            {
                var occupant = _grid.GetBoardOccupant(pos, orient);
                OnOccupantPlaced?.Invoke(occupant);
            }
            else
            {
                Destroy(board);
                OnPlaceFailed?.Invoke(pos, orient);
            }
        }

        private void PlaceSmallBlock(Vector3Int pos, Quaternion rotation)
        {
            if (_activePrefab == null)
            {
                Debug.LogError("[BuildingController] No active prefab assigned.");
                return;
            }

            var args = new PlaceCancelEventArgs(pos, BoardOrientation.X);
            OnBeforePlace?.Invoke(args);
            if (args.Cancel) return;

            Vector3 worldPos = GridConstants.SmallBlockGridToWorld(pos);

            GameObject block = Instantiate(_activePrefab, worldPos, rotation);
            block.name = $"SmallBlock_{pos}";

            if (_grid.AddSmallBlock(pos, rotation, block))
            {
                var occupant = _grid.GetOccupants(pos);
                foreach (var occ in occupant)
                {
                    if (occ.Type == OccupantType.SmallBlock)
                    {
                        OnOccupantPlaced?.Invoke(occ);
                        break;
                    }
                }
            }
            else
            {
                Destroy(block);
                OnPlaceFailed?.Invoke(pos, BoardOrientation.X);
            }
        }

        private void HandleRemoval()
        {
            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, maxPlacementDistance, boardLayer))
            {
                Vector3Int cell = WorldToGrid(hit.point);

                IOccupant occupant = _grid.FindOccupantByGameObject(cell, hit.collider.gameObject);
                if (occupant != null)
                {
                    OnBeforeRemove?.Invoke(occupant);
                    _grid.RemoveOccupant(occupant);
                    return;
                }

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            if (dx == 0 && dy == 0 && dz == 0) continue;
                            Vector3Int checkCell = cell + new Vector3Int(dx, dy, dz);
                            occupant = _grid.FindOccupantByGameObject(checkCell, hit.collider.gameObject);
                            if (occupant != null)
                            {
                                OnBeforeRemove?.Invoke(occupant);
                                _grid.RemoveOccupant(occupant);
                                return;
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
