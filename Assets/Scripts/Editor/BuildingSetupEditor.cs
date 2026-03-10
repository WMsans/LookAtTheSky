#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

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
            Debug.Log("Building system setup complete!");
        }

        [MenuItem("Tools/Building System/Setup Layers")]
        public static void SetupLayers()
        {
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            bool found = false;
            for (int i = 8; i < 32; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                if (layer.stringValue == "Board")
                {
                    Debug.Log($"Board layer already exists at index {i}");
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                for (int i = 8; i < 32; i++)
                {
                    SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(layer.stringValue))
                    {
                        layer.stringValue = "Board";
                        tagManager.ApplyModifiedProperties();
                        Debug.Log($"Added Board layer at index {i}");
                        break;
                    }
                }
            }
        }

        [MenuItem("Tools/Building System/Generate Materials")]
        public static void GenerateMaterials()
        {
            string folder = "Assets/Materials";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", "Materials");

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("URP Lit shader not found!");
                return;
            }

            CreateMaterial(folder, "BoardMaterial", shader,
                new Color(0.7f, 0.6f, 0.5f, 1f), transparent: false);
            CreateMaterial(folder, "BoardPreviewValid", shader,
                new Color(0f, 1f, 0f, 0.4f), transparent: true);
            CreateMaterial(folder, "BoardPreviewInvalid", shader,
                new Color(1f, 0f, 0f, 0.4f), transparent: true);

            AssetDatabase.SaveAssets();
            Debug.Log("Materials generated in Assets/Materials/");
        }

        [MenuItem("Tools/Building System/Generate Board Prefab")]
        public static void GenerateBoardPrefab()
        {
            string folder = "Assets/Prefabs";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            string prefabPath = $"{folder}/Board.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                Debug.Log("Board prefab already exists");
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
            Debug.Log("Board prefab generated at Assets/Prefabs/Board.prefab");
        }

        private static void CreateMaterial(string folder, string name, Shader shader, Color color, bool transparent)
        {
            string path = $"{folder}/{name}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;

            Material mat = new Material(shader);
            mat.SetColor("_BaseColor", color);

            if (transparent)
            {
                mat.SetFloat("_Surface", 1);
                mat.SetFloat("_AlphaClip", 0);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }

            AssetDatabase.CreateAsset(mat, path);
        }
    }
}
#endif
