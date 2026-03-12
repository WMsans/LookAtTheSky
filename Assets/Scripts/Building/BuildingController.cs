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
