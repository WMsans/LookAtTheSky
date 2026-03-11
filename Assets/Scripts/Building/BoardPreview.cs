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
