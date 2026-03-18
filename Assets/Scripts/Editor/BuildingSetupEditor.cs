using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

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

        // ─────────────────────────────────────────────────────────────────
        // Task 19 & 20: Setup Inventory Scene
        // ─────────────────────────────────────────────────────────────────

        [MenuItem("Tools/Building System/Setup Inventory Scene")]
        public static void SetupInventoryScene()
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Inventory Scene");

            // Step 1: Create Board BuildingItemSO asset
            var boardItemSO = CreateBoardItemAsset();

            // Step 2: Find or create singleton scene GameObjects
            var inventoryManagerGO = FindOrCreateSingleton<Inventory.InventoryManager>("InventoryManager");
            var mouseManagerGO = FindOrCreateSingleton<UI.MouseManager>("MouseManager");

            // Step 3: Configure InventoryManager with starting items
            ConfigureInventoryManager(inventoryManagerGO, boardItemSO);

            // Step 4: Find BuildingController in scene for the bridge
            var buildingController = Object.FindFirstObjectByType<Building.BuildingController>();
            if (buildingController == null)
            {
                Debug.LogWarning("[InventorySetup] No BuildingController found in scene. " +
                    "BuildingInventoryBridge will need its reference assigned manually.");
            }

            // Step 5: Create BuildingInventoryBridge
            var bridgeGO = FindOrCreateSingleton<Building.BuildingInventoryBridge>("BuildingInventoryBridge");
            ConfigureBridge(bridgeGO, buildingController);

            // Step 6: Create InventoryCanvas with full UI hierarchy
            CreateInventoryCanvas();

            // Step 7: Verify BuildingController has no boardPrefab field (Task 20)
            VerifyNoBoardPrefabField();

            Undo.CollapseUndoOperations(undoGroup);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log("[InventorySetup] Inventory scene setup complete. " +
                "Review the hierarchy and assign any missing references in the Inspector.");
        }

        // ── Asset Creation ──────────────────────────────────────────────

        private static Inventory.BuildingItemSO CreateBoardItemAsset()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Items"))
                AssetDatabase.CreateFolder("Assets", "Items");

            string assetPath = "Assets/Items/Board.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Inventory.BuildingItemSO>(assetPath);
            if (existing != null)
            {
                Debug.Log("[InventorySetup] Board.asset already exists.");
                return existing;
            }

            var boardItem = ScriptableObject.CreateInstance<Inventory.BuildingItemSO>();
            boardItem.itemName = "Board";
            boardItem.maxStackSize = 64;
            boardItem.placementMode = Inventory.PlacementMode.Oriented;

            // Try to assign the Board prefab
            var boardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Board.prefab");
            if (boardPrefab != null)
            {
                boardItem.prefab = boardPrefab;
            }
            else
            {
                Debug.LogWarning("[InventorySetup] Board.prefab not found at Assets/Prefabs/Board.prefab. " +
                    "Assign the prefab manually on the Board BuildingItemSO asset.");
            }

            // Icon will need to be assigned manually (or imported separately)
            boardItem.icon = null;

            AssetDatabase.CreateAsset(boardItem, assetPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[InventorySetup] Created Board.asset at Assets/Items/Board.asset.");
            return boardItem;
        }

        // ── Scene GameObject Helpers ────────────────────────────────────

        private static GameObject FindOrCreateSingleton<T>(string name) where T : MonoBehaviour
        {
            var existing = Object.FindFirstObjectByType<T>();
            if (existing != null)
            {
                Debug.Log($"[InventorySetup] {name} already exists in scene.");
                return existing.gameObject;
            }

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            go.AddComponent<T>();
            Debug.Log($"[InventorySetup] Created {name} GameObject.");
            return go;
        }

        private static void ConfigureInventoryManager(GameObject go, Inventory.BuildingItemSO boardItem)
        {
            var mgr = go.GetComponent<Inventory.InventoryManager>();
            if (mgr == null) return;

            // Use SerializedObject to set the starting items list
            var so = new SerializedObject(mgr);
            var startingItems = so.FindProperty("_startingItems");

            // Check if already configured
            bool alreadyHasBoard = false;
            for (int i = 0; i < startingItems.arraySize; i++)
            {
                var element = startingItems.GetArrayElementAtIndex(i);
                var itemProp = element.FindPropertyRelative("item");
                if (itemProp.objectReferenceValue == boardItem)
                {
                    alreadyHasBoard = true;
                    break;
                }
            }

            if (!alreadyHasBoard && boardItem != null)
            {
                int idx = startingItems.arraySize;
                startingItems.InsertArrayElementAtIndex(idx);
                var element = startingItems.GetArrayElementAtIndex(idx);
                element.FindPropertyRelative("item").objectReferenceValue = boardItem;
                element.FindPropertyRelative("count").intValue = 64;
                so.ApplyModifiedProperties();
                Debug.Log("[InventorySetup] Configured InventoryManager with 64x Board starting item.");
            }
        }

        private static void ConfigureBridge(GameObject go, Building.BuildingController controller)
        {
            var bridge = go.GetComponent<Building.BuildingInventoryBridge>();
            if (bridge == null) return;
            if (controller == null) return;

            var so = new SerializedObject(bridge);
            var controllerProp = so.FindProperty("_buildingController");
            if (controllerProp.objectReferenceValue == null)
            {
                controllerProp.objectReferenceValue = controller;
                so.ApplyModifiedProperties();
                Debug.Log("[InventorySetup] Assigned BuildingController to BuildingInventoryBridge.");
            }
        }

        // ── UI Creation ─────────────────────────────────────────────────

        private static void CreateInventoryCanvas()
        {
            // Check if an InventoryCanvas already exists
            var existingCanvas = GameObject.Find("InventoryCanvas");
            if (existingCanvas != null)
            {
                Debug.Log("[InventorySetup] InventoryCanvas already exists in scene.");
                return;
            }

            // Create Canvas
            var canvasGO = new GameObject("InventoryCanvas");
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create InventoryCanvas");

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            // Ensure EventSystem exists
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var eventSystemGO = new GameObject("EventSystem");
                Undo.RegisterCreatedObjectUndo(eventSystemGO, "Create EventSystem");
                eventSystemGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystemGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                Debug.Log("[InventorySetup] Created EventSystem.");
            }

            // ── Hotbar UI ───────────────────────────────────────────
            var hotbarGO = CreateHotbarUI(canvasGO.transform);

            // ── Inventory Panel ─────────────────────────────────────
            CreateInventoryPanel(canvasGO.transform, canvas);

            Debug.Log("[InventorySetup] Created InventoryCanvas with HotbarUI and InventoryPanel.");
        }

        private static GameObject CreateHotbarUI(Transform parent)
        {
            // Container for hotbar
            var hotbarGO = CreateUIObject("HotbarUI", parent);
            var hotbarRT = hotbarGO.GetComponent<RectTransform>();

            // Anchor to bottom center
            hotbarRT.anchorMin = new Vector2(0.5f, 0f);
            hotbarRT.anchorMax = new Vector2(0.5f, 0f);
            hotbarRT.pivot = new Vector2(0.5f, 0f);
            hotbarRT.anchoredPosition = new Vector2(0f, 10f);

            float slotSize = 60f;
            float spacing = 4f;
            float totalWidth = 9 * slotSize + 8 * spacing;
            hotbarRT.sizeDelta = new Vector2(totalWidth, slotSize);

            // Add background
            var hotbarBg = hotbarGO.AddComponent<Image>();
            hotbarBg.color = new Color(0f, 0f, 0f, 0.5f);

            // Add HorizontalLayoutGroup
            var layout = hotbarGO.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            // Create 9 hotbar slots
            var slotUIs = new UI.SlotUI[9];
            for (int i = 0; i < 9; i++)
            {
                var slotGO = CreateSlotUIObject($"HotbarSlot_{i}", hotbarGO.transform, slotSize);
                slotUIs[i] = slotGO.GetComponent<UI.SlotUI>();
            }

            // Add HotbarUI component and wire slots
            var hotbarUI = hotbarGO.AddComponent<UI.HotbarUI>();
            var hotbarSO = new SerializedObject(hotbarUI);
            var slotsProp = hotbarSO.FindProperty("_slots");
            slotsProp.arraySize = 9;
            for (int i = 0; i < 9; i++)
            {
                slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotUIs[i];
            }
            hotbarSO.ApplyModifiedProperties();

            return hotbarGO;
        }

        private static void CreateInventoryPanel(Transform canvasTransform, Canvas canvas)
        {
            // Root object for InventoryPanelUI component
            var panelRootGO = CreateUIObject("InventoryPanelUI", canvasTransform);
            var panelRootRT = panelRootGO.GetComponent<RectTransform>();
            panelRootRT.anchorMin = Vector2.zero;
            panelRootRT.anchorMax = Vector2.one;
            panelRootRT.sizeDelta = Vector2.zero;

            // The actual panel container (toggled on/off)
            var panelGO = CreateUIObject("Panel", panelRootGO.transform);
            var panelRT = panelGO.GetComponent<RectTransform>();

            // Center the panel
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);

            float slotSize = 60f;
            float spacing = 4f;
            float gridWidth = 9 * slotSize + 8 * spacing;
            float gridHeight = 3 * slotSize + 2 * spacing;
            float hotbarHeight = slotSize;
            float sectionGap = 16f;
            float panelPadding = 12f;
            float totalHeight = gridHeight + sectionGap + hotbarHeight + 2 * panelPadding;
            float totalWidth = gridWidth + 2 * panelPadding;

            panelRT.sizeDelta = new Vector2(totalWidth, totalHeight);

            // Panel background
            var panelBg = panelGO.AddComponent<Image>();
            panelBg.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

            // ── Label ───────────────────────────────────────────────
            var labelGO = CreateUIObject("Label", panelGO.transform);
            var labelRT = labelGO.GetComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0f, 1f);
            labelRT.anchorMax = new Vector2(1f, 1f);
            labelRT.pivot = new Vector2(0.5f, 1f);
            labelRT.anchoredPosition = new Vector2(0f, 20f);
            labelRT.sizeDelta = new Vector2(0f, 30f);
            var labelText = labelGO.AddComponent<TextMeshProUGUI>();
            labelText.text = "Inventory";
            labelText.fontSize = 20;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.color = Color.white;

            // ── Main Grid (3 rows x 9 cols = 27 slots) ─────────────
            var gridContainerGO = CreateUIObject("GridContainer", panelGO.transform);
            var gridContainerRT = gridContainerGO.GetComponent<RectTransform>();
            gridContainerRT.anchorMin = new Vector2(0.5f, 1f);
            gridContainerRT.anchorMax = new Vector2(0.5f, 1f);
            gridContainerRT.pivot = new Vector2(0.5f, 1f);
            gridContainerRT.anchoredPosition = new Vector2(0f, -panelPadding);
            gridContainerRT.sizeDelta = new Vector2(gridWidth, gridHeight);

            var gridLayout = gridContainerGO.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(slotSize, slotSize);
            gridLayout.spacing = new Vector2(spacing, spacing);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 9;
            gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            gridLayout.childAlignment = TextAnchor.UpperLeft;

            var gridSlotUIs = new UI.SlotUI[27];
            for (int i = 0; i < 27; i++)
            {
                var slotGO = CreateSlotUIObject($"GridSlot_{i}", gridContainerGO.transform, slotSize);
                gridSlotUIs[i] = slotGO.GetComponent<UI.SlotUI>();
            }

            // ── Hotbar Mirror (9 slots at bottom of panel) ──────────
            var hotbarMirrorGO = CreateUIObject("HotbarMirror", panelGO.transform);
            var hotbarMirrorRT = hotbarMirrorGO.GetComponent<RectTransform>();
            hotbarMirrorRT.anchorMin = new Vector2(0.5f, 0f);
            hotbarMirrorRT.anchorMax = new Vector2(0.5f, 0f);
            hotbarMirrorRT.pivot = new Vector2(0.5f, 0f);
            hotbarMirrorRT.anchoredPosition = new Vector2(0f, panelPadding);
            hotbarMirrorRT.sizeDelta = new Vector2(gridWidth, slotSize);

            var mirrorLayout = hotbarMirrorGO.AddComponent<HorizontalLayoutGroup>();
            mirrorLayout.spacing = spacing;
            mirrorLayout.childAlignment = TextAnchor.MiddleCenter;
            mirrorLayout.childForceExpandWidth = false;
            mirrorLayout.childForceExpandHeight = false;
            mirrorLayout.childControlWidth = false;
            mirrorLayout.childControlHeight = false;

            var mirrorSlotUIs = new UI.SlotUI[9];
            for (int i = 0; i < 9; i++)
            {
                var slotGO = CreateSlotUIObject($"MirrorSlot_{i}", hotbarMirrorGO.transform, slotSize);
                mirrorSlotUIs[i] = slotGO.GetComponent<UI.SlotUI>();
            }

            // ── CursorItem ──────────────────────────────────────────
            var cursorGO = CreateUIObject("CursorItem", panelRootGO.transform);
            var cursorRT = cursorGO.GetComponent<RectTransform>();
            cursorRT.sizeDelta = new Vector2(slotSize, slotSize);
            // CursorItem should render on top of everything
            cursorGO.transform.SetAsLastSibling();

            var cursorIconGO = CreateUIObject("Icon", cursorGO.transform);
            var cursorIconRT = cursorIconGO.GetComponent<RectTransform>();
            cursorIconRT.anchorMin = Vector2.zero;
            cursorIconRT.anchorMax = Vector2.one;
            cursorIconRT.sizeDelta = new Vector2(-8f, -8f); // small padding
            var cursorIconImage = cursorIconGO.AddComponent<Image>();
            cursorIconImage.raycastTarget = false;

            var cursorCountGO = CreateUIObject("Count", cursorGO.transform);
            var cursorCountRT = cursorCountGO.GetComponent<RectTransform>();
            cursorCountRT.anchorMin = Vector2.zero;
            cursorCountRT.anchorMax = new Vector2(1f, 0.4f);
            cursorCountRT.sizeDelta = Vector2.zero;
            var cursorCountText = cursorCountGO.AddComponent<TextMeshProUGUI>();
            cursorCountText.fontSize = 14;
            cursorCountText.alignment = TextAlignmentOptions.BottomRight;
            cursorCountText.color = Color.white;
            cursorCountText.raycastTarget = false;

            var cursorItem = cursorGO.AddComponent<UI.CursorItem>();
            var cursorSO = new SerializedObject(cursorItem);
            cursorSO.FindProperty("_iconImage").objectReferenceValue = cursorIconImage;
            cursorSO.FindProperty("_countText").objectReferenceValue = cursorCountText;
            cursorSO.FindProperty("_parentCanvas").objectReferenceValue = canvas;
            cursorSO.ApplyModifiedProperties();

            // ── Wire InventoryPanelUI ───────────────────────────────
            var panelUI = panelRootGO.AddComponent<UI.InventoryPanelUI>();
            var panelUISO = new SerializedObject(panelUI);
            panelUISO.FindProperty("_panel").objectReferenceValue = panelGO;
            panelUISO.FindProperty("_cursorItem").objectReferenceValue = cursorItem;

            var gridSlotsProp = panelUISO.FindProperty("_gridSlots");
            gridSlotsProp.arraySize = 27;
            for (int i = 0; i < 27; i++)
            {
                gridSlotsProp.GetArrayElementAtIndex(i).objectReferenceValue = gridSlotUIs[i];
            }

            var hotbarSlotsProp = panelUISO.FindProperty("_hotbarSlots");
            hotbarSlotsProp.arraySize = 9;
            for (int i = 0; i < 9; i++)
            {
                hotbarSlotsProp.GetArrayElementAtIndex(i).objectReferenceValue = mirrorSlotUIs[i];
            }

            panelUISO.ApplyModifiedProperties();
        }

        // ── Slot UI Factory ─────────────────────────────────────────

        private static GameObject CreateSlotUIObject(string name, Transform parent, float size)
        {
            var slotGO = CreateUIObject(name, parent);
            var slotRT = slotGO.GetComponent<RectTransform>();
            slotRT.sizeDelta = new Vector2(size, size);

            // Background
            var bg = slotGO.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            // Highlight border (child image, initially disabled)
            var highlightGO = CreateUIObject("Highlight", slotGO.transform);
            var highlightRT = highlightGO.GetComponent<RectTransform>();
            highlightRT.anchorMin = Vector2.zero;
            highlightRT.anchorMax = Vector2.one;
            highlightRT.sizeDelta = Vector2.zero;
            var highlightImage = highlightGO.AddComponent<Image>();
            highlightImage.color = new Color(1f, 1f, 1f, 0.5f);
            highlightImage.raycastTarget = false;
            highlightImage.enabled = false;

            // Icon (child image)
            var iconGO = CreateUIObject("Icon", slotGO.transform);
            var iconRT = iconGO.GetComponent<RectTransform>();
            iconRT.anchorMin = Vector2.zero;
            iconRT.anchorMax = Vector2.one;
            iconRT.sizeDelta = new Vector2(-8f, -8f); // padding
            var iconImage = iconGO.AddComponent<Image>();
            iconImage.raycastTarget = false;
            iconImage.enabled = false;

            // Count text (child TMP)
            var countGO = CreateUIObject("Count", slotGO.transform);
            var countRT = countGO.GetComponent<RectTransform>();
            countRT.anchorMin = Vector2.zero;
            countRT.anchorMax = new Vector2(1f, 0.4f);
            countRT.sizeDelta = Vector2.zero;
            countRT.anchoredPosition = new Vector2(-4f, 2f);
            var countText = countGO.AddComponent<TextMeshProUGUI>();
            countText.fontSize = 14;
            countText.alignment = TextAlignmentOptions.BottomRight;
            countText.color = Color.white;
            countText.raycastTarget = false;

            // Add SlotUI component and wire references
            var slotUI = slotGO.AddComponent<UI.SlotUI>();
            var slotSO = new SerializedObject(slotUI);
            slotSO.FindProperty("_iconImage").objectReferenceValue = iconImage;
            slotSO.FindProperty("_countText").objectReferenceValue = countText;
            slotSO.FindProperty("_highlightBorder").objectReferenceValue = highlightImage;
            slotSO.FindProperty("_background").objectReferenceValue = bg;
            slotSO.ApplyModifiedProperties();

            return slotGO;
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        // ── Task 20: Verify no boardPrefab field ────────────────────

        private static void VerifyNoBoardPrefabField()
        {
            var controller = Object.FindFirstObjectByType<Building.BuildingController>();
            if (controller == null) return;

            var so = new SerializedObject(controller);
            var prop = so.FindProperty("boardPrefab");
            if (prop != null)
            {
                Debug.LogWarning("[InventorySetup] BuildingController still has a 'boardPrefab' " +
                    "serialized field. This should be removed — the prefab is now set dynamically " +
                    "via ActivePrefab from BuildingInventoryBridge.");
            }
            else
            {
                Debug.Log("[InventorySetup] Verified: BuildingController has no boardPrefab field (Task 20 OK).");
            }
        }
    }
}
