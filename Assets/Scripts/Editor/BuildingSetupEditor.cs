using UnityEngine;
using UnityEditor;

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
            Debug.Log("[BuildingSetup] Setup complete.");
        }

        [MenuItem("Tools/Building System/Setup Layers")]
        public static void SetupLayers()
        {
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            SetLayer(layers, 8, "Board");
            SetLayer(layers, 9, "PlacementTrigger");

            tagManager.ApplyModifiedProperties();
            Debug.Log("[BuildingSetup] Layers configured: Board=8, PlacementTrigger=9.");
        }

        private static void SetLayer(SerializedProperty layers, int index, string name)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(index);
            if (string.IsNullOrEmpty(layer.stringValue) || layer.stringValue == name)
            {
                layer.stringValue = name;
            }
            else
            {
                Debug.LogWarning($"[BuildingSetup] Layer {index} already in use as '{layer.stringValue}'. Cannot set to '{name}'.");
            }
        }

        [MenuItem("Tools/Building System/Generate Materials")]
        public static void GenerateMaterials()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");

            // Board material — opaque tan
            CreateMaterialIfMissing("Assets/Materials/BoardMaterial.mat",
                new Color(0.7f, 0.6f, 0.5f, 1f), false);

            // Preview valid — transparent green
            CreateMaterialIfMissing("Assets/Materials/BoardPreviewValid.mat",
                new Color(0f, 1f, 0f, 0.4f), true);

            AssetDatabase.SaveAssets();
            Debug.Log("[BuildingSetup] Materials generated.");
        }

        private static void CreateMaterialIfMissing(string path, Color color, bool transparent)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[BuildingSetup] URP/Lit shader not found.");
                return;
            }

            Material mat = new Material(shader);
            mat.color = color;

            if (transparent)
            {
                mat.SetFloat("_Surface", 1); // Transparent
                mat.SetFloat("_Blend", 0);   // Alpha
                mat.SetFloat("_AlphaClip", 0);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            AssetDatabase.CreateAsset(mat, path);
        }

        [MenuItem("Tools/Building System/Generate Board Prefab")]
        public static void GenerateBoardPrefab()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            string prefabPath = "Assets/Prefabs/Board.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                Debug.Log("[BuildingSetup] Board prefab already exists.");
                return;
            }

            GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Board";
            board.transform.localScale = new Vector3(4f, 0.1f, 4f);
            board.layer = LayerMask.NameToLayer("Board");

            Material boardMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/BoardMaterial.mat");
            if (boardMat != null)
                board.GetComponent<MeshRenderer>().sharedMaterial = boardMat;

            PrefabUtility.SaveAsPrefabAsset(board, prefabPath);
            Object.DestroyImmediate(board);

            AssetDatabase.SaveAssets();
            Debug.Log("[BuildingSetup] Board prefab generated.");
        }
    }
}
