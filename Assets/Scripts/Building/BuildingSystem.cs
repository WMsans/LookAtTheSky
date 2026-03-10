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

            Vector3 normal = hit.normal;
            BoardFace hitFace = BoardVisuals.NormalToFace(normal);

            int boardLayer = LayerMask.NameToLayer("Board");
            if (hit.collider.gameObject.layer == boardLayer)
            {
                // Hit an existing board — target the adjacent cell in the direction of the normal
                Vector3 boardPos = hit.collider.transform.position;
                Vector3Int boardCell = BoardVisuals.WorldToCell(boardPos);

                _targetCell = boardCell + BoardVisuals.FaceToOffset(hitFace);
                _targetFace = BoardVisuals.OppositeFace(hitFace);
            }
            else
            {
                // Hit ground or other surface
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

            if (grid.TryGetKeyForBoard(hit.collider.gameObject, out BoardKey key))
            {
                grid.Remove(key.Cell, key.Face);
            }
        }
    }
}
