using UnityEngine;

namespace Building
{
    public class BoardPreview : MonoBehaviour
    {
        [SerializeField] private Material previewMaterial;

        private GameObject _previewObj;

        private void Awake()
        {
            // Start with default cube preview
            CreateDefaultPreview();
        }

        private void CreateDefaultPreview()
        {
            if (_previewObj != null)
            {
                Destroy(_previewObj);
            }

            _previewObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _previewObj.name = "BoardPreview";
            _previewObj.transform.SetParent(transform);
            _previewObj.transform.localScale = new Vector3(4f, 0.1f, 4f);

            // Remove collider so it doesn't interfere with raycasts
            var collider = _previewObj.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            ApplyPreviewMaterial(_previewObj);
            _previewObj.SetActive(false);
        }

        /// <summary>
        /// Switch the preview to match the given prefab's mesh and scale.
        /// If prefab is null, hides the preview.
        /// </summary>
        public void SetPreviewPrefab(GameObject prefab)
        {
            if (_previewObj != null)
            {
                Destroy(_previewObj);
                _previewObj = null;
            }

            if (prefab == null) return;

            _previewObj = Instantiate(prefab);
            _previewObj.name = "BoardPreview";
            _previewObj.transform.SetParent(transform);

            // Remove all colliders so the preview doesn't interfere with raycasts
            foreach (var col in _previewObj.GetComponentsInChildren<Collider>())
            {
                Destroy(col);
            }

            // Apply preview material to all renderers
            ApplyPreviewMaterial(_previewObj);

            // Remove any non-visual components (Rigidbody, scripts, etc.)
            foreach (var rb in _previewObj.GetComponentsInChildren<Rigidbody>())
                Destroy(rb);

            // Set layer to Default so it doesn't interact with building raycasts
            SetLayerRecursive(_previewObj, 0);

            _previewObj.SetActive(false);
        }

        private void ApplyPreviewMaterial(GameObject obj)
        {
            if (previewMaterial == null) return;
            foreach (var renderer in obj.GetComponentsInChildren<MeshRenderer>())
            {
                var materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = previewMaterial;
                renderer.materials = materials;
            }
        }

        private void SetLayerRecursive(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        public void ShowAt(Vector3 position, Quaternion rotation)
        {
            if (_previewObj == null) return;
            _previewObj.transform.position = position;
            _previewObj.transform.rotation = rotation;
            _previewObj.SetActive(true);
        }

        public void Hide()
        {
            if (_previewObj != null)
                _previewObj.SetActive(false);
        }
    }
}
