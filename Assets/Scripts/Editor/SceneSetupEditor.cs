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
                ground.transform.localScale = new Vector3(100f, 1f, 100f);
                ground.GetComponent<MeshRenderer>().enabled = false;
            }

            // --- Building Grid ---
            GameObject gridObj = GameObject.Find("BuildingGrid");
            if (gridObj == null)
            {
                gridObj = new GameObject("BuildingGrid");
                var grid = gridObj.AddComponent<BuildingGrid>();

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

                var cc = player.AddComponent<CharacterController>();
                cc.height = 1.8f;
                cc.center = new Vector3(0f, 0.9f, 0f);
                cc.radius = 0.3f;

                // Remove existing Main Camera
                GameObject oldCam = GameObject.FindGameObjectWithTag("MainCamera");
                if (oldCam != null)
                    Object.DestroyImmediate(oldCam);

                GameObject camObj = new GameObject("PlayerCamera");
                camObj.transform.SetParent(player.transform);
                camObj.transform.localPosition = new Vector3(0f, 1.6f, 0f);
                camObj.tag = "MainCamera";
                camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();

                // Preview object
                GameObject preview = GameObject.CreatePrimitive(PrimitiveType.Cube);
                preview.name = "BoardPreview";
                preview.transform.localScale = new Vector3(4f, 0.1f, 4f);
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

                int defaultLayer = 1 << 0;
                int boardLayer = 1 << LayerMask.NameToLayer("Board");
                bsSO.FindProperty("buildableLayers").intValue = defaultLayer | boardLayer;

                bsSO.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(GameObject.Find("BuildingGrid"));
            Debug.Log("Scene setup complete! Press Play to test.");
        }
    }
}
#endif
