using NexZap.Data;
using NexZap.Gameplay.Items;
using NexZap.Gameplay.Level;
using NexZap.Gameplay.Mechanics;
using NexZap.Managers;
using UnityEditor;
using UnityEngine;

namespace NexZap.EditorTools
{
    public static class GameplayPrototypeSetup
    {
        private const string RootName = "---GameplayRoot---";
        private const string PrefabFolder = "Assets/_NexZap/Prefabs/Gameplay";
        private const string DataFolder = "Assets/_NexZap/Data/Levels";
        private const string MaterialLibraryPath = PixelMaterialLibrary.DefaultAssetPath;

        [MenuItem("NexZap/Setup Gameplay Prototype")]
        public static void SetupGameplayPrototype()
        {
            EnsureFolders();

            var colorBlockPrefab = GetOrCreateColorBlockPrefab();
            var pixelCellPrefab = GetOrCreatePixelCellPrefab3D();
            var materialLibrary = GetOrCreateMaterialLibrary();
            var selectionLinePrefab = GetOrCreateSelectionLinePrefab();

            var root = GameObject.Find(RootName) ?? new GameObject(RootName);

            var board = GetOrCreateComponent<PixelBoard>(root.transform, "PixelBoard");
            var path = GetOrCreateComponent<CircularPath>(root.transform, "CircularPath");
            SetupCircularPathRenderer(path.gameObject);
            var queue = GetOrCreateComponent<BlockQueue>(root.transform, "BlockQueue");
            var lines = GetOrCreateComponent<SelectionLineManager>(root.transform, "SelectionLines");
            var pool = GetOrCreateComponent<ColorBlockPool>(root.transform, "ColorBlockPool");
            var controller = GetOrCreateComponent<GameplayController>(root.transform, "GameplayController");

            AssignPrivateField(board, "cellPrefab", pixelCellPrefab);
            AssignPrivateField(board, "materialLibrary", materialLibrary);
            AssignPrivateField(pool, "prefab", colorBlockPrefab);
            AssignPrivateField(lines, "linePrefab", selectionLinePrefab);
            AssignPrivateField(lines, "blockPool", pool);

            AssignPrivateField(controller, "pixelBoard", board);
            AssignPrivateField(controller, "circularPath", path);
            AssignPrivateField(controller, "blockQueue", queue);
            AssignPrivateField(controller, "selectionLineManager", lines);
            AssignPrivateField(controller, "blockPool", pool);

            var levelManagerGo = GameObject.Find("LevelManager") ?? new GameObject("LevelManager");
            var levelManager = GetOrAddComponent<LevelManager>(levelManagerGo);
            AssignPrivateField(levelManager, "gameplayController", controller);

            var sampleLevel = GetOrCreateSampleLevel();
            AssignPrivateField(levelManager, "currentLevel", sampleLevel);

            var gameManagerGo = GameObject.Find("---GameManager---");
            if (gameManagerGo != null)
            {
                var gameManager = GetOrAddComponent<GameManager>(gameManagerGo);
                AssignPrivateField(gameManager, "levelManager", levelManager);
                AssignPrivateField(gameManager, "gameplayController", controller);

                levelManagerGo.transform.SetParent(gameManagerGo.transform, false);
                root.transform.SetParent(gameManagerGo.transform, false);
            }

            PositionAreas(root.transform, board.transform, queue.transform, lines.transform);
            SetupGameplayCamera();

            Selection.activeGameObject = root;
            EditorUtility.SetDirty(root);
            Debug.Log("Gameplay prototype setup complete.");
        }

        [MenuItem("NexZap/Create Sample Level")]
        public static void CreateSampleLevelMenu()
        {
            EnsureFolders();
            var level = GetOrCreateSampleLevel();
            Selection.activeObject = level;
            EditorGUIUtility.PingObject(level);
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_NexZap/Data"))
            {
                AssetDatabase.CreateFolder("Assets/_NexZap", "Data");
            }

            if (!AssetDatabase.IsValidFolder(DataFolder))
            {
                AssetDatabase.CreateFolder("Assets/_NexZap/Data", "Levels");
            }

            if (!AssetDatabase.IsValidFolder(PrefabFolder))
            {
                AssetDatabase.CreateFolder("Assets/_NexZap/Prefabs", "Gameplay");
            }
        }

        private static LevelData GetOrCreateSampleLevel()
        {
            var path = $"{DataFolder}/SampleLevel.asset";
            var existing = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (existing != null)
            {
                return existing;
            }

            var level = ScriptableObject.CreateInstance<LevelData>();
            level.width = 6;
            level.height = 6;
            level.EnsureGridSize();

            for (var y = 0; y < level.height; y++)
            {
                for (var x = 0; x < level.width; x++)
                {
                    if (x < 2 && y < 2)
                    {
                        level.SetCell(x, y, BlockColor.Red);
                    }
                    else if (x >= 4 && y < 2)
                    {
                        level.SetCell(x, y, BlockColor.Blue);
                    }
                    else if (x < 2 && y >= 4)
                    {
                        level.SetCell(x, y, BlockColor.Green);
                    }
                    else if (x >= 4 && y >= 4)
                    {
                        level.SetCell(x, y, BlockColor.Yellow);
                    }
                    else
                    {
                        level.SetCell(x, y, BlockColor.None);
                    }
                }
            }

            level.selectionLines = new[]
            {
                new SelectionLineConfig { blocks = new[] { BlockColor.Red, BlockColor.Red } },
                new SelectionLineConfig { blocks = new[] { BlockColor.Blue, BlockColor.Blue } },
                new SelectionLineConfig { blocks = new[] { BlockColor.Green } },
                new SelectionLineConfig { blocks = new[] { BlockColor.Yellow, BlockColor.Yellow, BlockColor.Yellow } }
            };

            AssetDatabase.CreateAsset(level, path);
            AssetDatabase.SaveAssets();
            return level;
        }

        private static ColorBlock GetOrCreateColorBlockPrefab()
        {
            var path = $"{PrefabFolder}/ColorBlock.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<ColorBlock>(path);
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject("ColorBlock");

            // Collider ở root để chuyển động (DOPath) tách khỏi animation scale của Visual.
            var collider = go.AddComponent<CircleCollider2D>();
            collider.radius = 0.35f;

            var visualGo = new GameObject("Visual");
            visualGo.transform.SetParent(go.transform, false);

            var highlightGo = new GameObject("Highlight");
            highlightGo.transform.SetParent(visualGo.transform, false);
            highlightGo.transform.localScale = Vector3.one * 1.25f;
            var highlightRenderer = highlightGo.AddComponent<SpriteRenderer>();
            highlightRenderer.sprite = CreateSquareSprite();
            highlightRenderer.sortingOrder = 9;
            highlightRenderer.enabled = false;

            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(visualGo.transform, false);
            var spriteRenderer = bodyGo.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = CreateSquareSprite();
            spriteRenderer.sortingOrder = 10;

            var labelGo = new GameObject("CapacityLabel");
            labelGo.transform.SetParent(visualGo.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0f, -0.1f);

            var text = labelGo.AddComponent<TMPro.TextMeshPro>();
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.fontSize = 4;
            text.color = Color.white;
            var textRenderer = text.GetComponent<Renderer>();
            if (textRenderer != null)
            {
                textRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;
            }

            var block = go.AddComponent<ColorBlock>();
            AssignPrivateField(block, "visualRoot", visualGo.transform);
            AssignPrivateField(block, "spriteRenderer", spriteRenderer);
            AssignPrivateField(block, "capacityLabel", text);
            AssignPrivateField(block, "highlightRenderer", highlightRenderer);
            AssignPrivateField(block, "bodyCollider", collider);

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<ColorBlock>();
        }

        [MenuItem("NexZap/Upgrade Pixel Prefab to 3D")]
        public static void UpgradePixelPrefabTo3D()
        {
            EnsureFolders();
            var prefab = GetOrCreatePixelCellPrefab3D();
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log("PixelCell prefab đã nâng cấp sang 3D (cube + MeshRenderer).");
        }

        private static PixelMaterialLibrary GetOrCreateMaterialLibrary()
        {
            return PixelMaterialLibrary.LoadOrCreateDefault();
        }

        private static PixelCell GetOrCreatePixelCellPrefab3D()
        {
            var path = $"{PrefabFolder}/PixelCell.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<PixelCell>(path);
            if (existing != null && existing.GetComponentInChildren<MeshRenderer>() != null)
            {
                return existing;
            }

            if (existing != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            var go = new GameObject("PixelCell");

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(go.transform, false);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            var bodyRenderer = body.GetComponent<MeshRenderer>();

            var fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fill.name = "Fill";
            fill.transform.SetParent(go.transform, false);
            Object.DestroyImmediate(fill.GetComponent<Collider>());
            var fillRenderer = fill.GetComponent<MeshRenderer>();
            fill.SetActive(false);

            var cell = go.AddComponent<PixelCell>();
            AssignPrivateField(cell, "bodyRenderer", bodyRenderer);
            AssignPrivateField(cell, "fillRenderer", fillRenderer);

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<PixelCell>();
        }

        private static PixelCell GetOrCreatePixelCellPrefab()
        {
            return GetOrCreatePixelCellPrefab3D();
        }

        private static SelectionLine GetOrCreateSelectionLinePrefab()
        {
            var path = $"{PrefabFolder}/SelectionLine.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<SelectionLine>(path);
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject("SelectionLine");
            var line = go.AddComponent<SelectionLine>();
            AssignPrivateField(line, "blocksRoot", go.transform);

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<SelectionLine>();
        }

        private static Sprite CreateSquareSprite()
        {
            var texture = new Texture2D(32, 32);
            var pixels = new Color32[32 * 32];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            texture.filterMode = FilterMode.Point;

            return Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
        }

        private static void SetupCircularPathRenderer(GameObject pathObject)
        {
            var lineRenderer = pathObject.GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                lineRenderer = pathObject.AddComponent<LineRenderer>();
            }

            lineRenderer.loop = true;
            lineRenderer.useWorldSpace = true;
            lineRenderer.startWidth = 0.08f;
            lineRenderer.endWidth = 0.08f;
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = new Color(1f, 1f, 1f, 0.35f);
            lineRenderer.endColor = new Color(1f, 1f, 1f, 0.35f);
            lineRenderer.sortingOrder = 5;

            var path = pathObject.GetComponent<CircularPath>();
            AssignPrivateField(path, "lineRenderer", lineRenderer);
        }

        private static void SetupGameplayCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            camera.orthographic = false;
            camera.fieldOfView = 45f;
            camera.transform.position = new Vector3(0f, 2f, -8f);
            camera.transform.rotation = Quaternion.Euler(15f, 0f, 0f);

            var light = Object.FindFirstObjectByType<Light>();
            if (light == null)
            {
                var lightGo = new GameObject("Directional Light");
                light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
            }

            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            light.intensity = 1.1f;
        }

        private static void PositionAreas(Transform root, Transform board, Transform queue, Transform lines)
        {
            root.localPosition = Vector3.zero;
            board.localPosition = new Vector3(0f, 1.5f, 0f);
            queue.localPosition = new Vector3(0f, -0.5f, 0f);
            lines.localPosition = new Vector3(0f, -3.5f, 0f);
        }

        private static T GetOrCreateComponent<T>(Transform parent, string objectName) where T : Component
        {
            var child = parent.Find(objectName);
            GameObject go;
            if (child == null)
            {
                go = new GameObject(objectName);
                go.transform.SetParent(parent, false);
            }
            else
            {
                go = child.gameObject;
            }

            return GetOrAddComponent<T>(go);
        }

        private static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        private static void AssignPrivateField(Object target, string fieldName, Object value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(fieldName);
            if (property != null)
            {
                property.objectReferenceValue = value;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
