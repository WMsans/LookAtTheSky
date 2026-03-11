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
