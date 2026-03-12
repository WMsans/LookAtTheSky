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
