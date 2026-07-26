using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NexZap.Data
{
    public enum ColorBlockType
    {
        SingleColor,
        DualColor
    }

    [Serializable]
    public class ColorBlockData
    {
#if UNITY_EDITOR
        [FormerlySerializedAs("color")]
        [SerializeField, HideInInspector]
        private BlockColor legacyColor;
#endif

        [HorizontalGroup("Type", 100f), HideLabel]
        public ColorBlockType blockType = ColorBlockType.SingleColor;

        [HorizontalGroup("Colors"), HideLabel]
#if UNITY_EDITOR
        [CustomValueDrawer("DrawColorIdValue")]
#endif
        public string colorId = PixelColorIds.Empty;

        [HorizontalGroup("Colors"), HideLabel]
        [ShowIf("blockType", ColorBlockType.DualColor)]
#if UNITY_EDITOR
        [CustomValueDrawer("DrawColorIdValue2")]
#endif
        public string secondaryColorId = PixelColorIds.Empty;

        [HorizontalGroup, LabelText("Sức chứa"), LabelWidth(60f), MinValue(1)]
        public int capacity = GameplayConstants.DefaultBlockCapacity;

#if UNITY_EDITOR
        private static string DrawColorIdValue(string value, GUIContent label)
        {
            return PixelColorIdFieldDrawer.Draw(value, label);
        }

        private static string DrawColorIdValue2(string value, GUIContent label)
        {
            return PixelColorIdFieldDrawer.Draw(value, label);
        }

        public void MigrateLegacyColor(PixelMaterialLibrary library)
        {
            if (library == null || !string.IsNullOrEmpty(colorId))
            {
                return;
            }

            if (legacyColor != BlockColor.None)
            {
                colorId = library.GetOrCreateIdForLegacyBlockColor(legacyColor);
                legacyColor = BlockColor.None;
            }
        }
#endif

        public string[] GetColorIds()
        {
            if (blockType == ColorBlockType.DualColor && !string.IsNullOrEmpty(secondaryColorId))
            {
                return new[] { colorId, secondaryColorId };
            }
            return new[] { colorId };
        }
    }

    [Serializable]
    public class SelectionLineData
    {
        [ListDrawerSettings(DraggableItems = true, ShowFoldout = false)]
        public List<ColorBlockData> blocks = new();
    }

    [CreateAssetMenu(fileName = "Level_New", menuName = "NexZap/Base Level")]
    public class BaseLevel : SerializedScriptableObject
    {
        public event Action Changed;

        // ===================== 1) CÀI ĐẶT GRID =====================
        [BoxGroup("1) Cài đặt Grid"), MinValue(1), OnValueChanged(nameof(ResizeGrid))]
        public int width = 8;

        [BoxGroup("1) Cài đặt Grid"), MinValue(1), OnValueChanged(nameof(ResizeGrid))]
        public int height = 8;

        [BoxGroup("1) Cài đặt Grid"), MinValue(0.01f)]
        [Tooltip("Khoảng cách giữa các pixel khi spawn ra game. Chỉnh tay, KHÔNG auto")]
        public float spacing = 0.5f;

        [BoxGroup("1) Cài đặt Grid"), MinValue(0.01f)]
        [Tooltip("Kích thước (scale) RIÊNG của từng pixel theo trục X và Y, tách biệt với spacing.")]
        public Vector2 pixelScale = Vector2.one;

        [BoxGroup("1) Cài đặt Grid"), AssetsOnly]
        public GameObject idlePixelPrefab;

        [BoxGroup("1) Cài đặt Grid"), AssetsOnly]
        public GameObject fillPixelPrefab;

        // ===================== 2) VẼ PIXEL =====================
        [Title("2) Vẽ Pixel")]
        [BoxGroup("2) Vẽ Pixel")]
        [FoldoutGroup("2) Vẽ Pixel/Bảng màu", expanded: true)]
        [AssetsOnly, HideLabel]
        [InlineEditor(InlineEditorModes.GUIOnly, DrawHeader = false)]
        [Tooltip("Thêm màu, đặt tên, chọn tint và tạo material 3D.")]
        public PixelMaterialLibrary pixelMaterialLibrary;

#if UNITY_EDITOR
        [BoxGroup("2) Vẽ Pixel")]
        [FoldoutGroup("2) Vẽ Pixel/Bảng màu")]
        [Button("Tạo / mở thư viện màu", ButtonSizes.Small)]
        private void CreatePixelColorLibrary()
        {
            pixelMaterialLibrary = PixelMaterialLibrary.LoadOrCreateDefault();
            EnsureValidBrushColorId();
            MarkDirty();
        }
#endif

        [BoxGroup("2) Vẽ Pixel")]
        [HorizontalGroup("2) Vẽ Pixel/BrushType", 100f), HideLabel]
        public ColorBlockType brushType = ColorBlockType.SingleColor;

        [BoxGroup("2) Vẽ Pixel")]
        [PropertySpace(8)]
#if UNITY_EDITOR
        [CustomValueDrawer("DrawBrushColorId")]
#endif
        [LabelText("Màu cọ")]
        [HorizontalGroup("2) Vẽ Pixel/Colors"), HideLabel]
        public string brushColorId = PixelColorIds.Empty;

        [BoxGroup("2) Vẽ Pixel")]
        [ShowIf("brushType", ColorBlockType.DualColor)]
#if UNITY_EDITOR
        [CustomValueDrawer("DrawBrushColorId2")]
#endif
        [LabelText("Màu phụ")]
        [HorizontalGroup("2) Vẽ Pixel/Colors"), HideLabel]
        public string secondaryBrushColorId = PixelColorIds.Empty;

        [BoxGroup("2) Vẽ Pixel")]
        [ToggleLeft, LabelText("Cục tẩy (Eraser)")]
        [Tooltip("Bật: click trái = xoá ô, bất kể màu cọ. Chuột phải luôn xoá.")]
        public bool eraser;

        [BoxGroup("2) Vẽ Pixel")]
        [TableMatrix(SquareCells = true, DrawElementMethod = "DrawCell",
            HideColumnIndices = true, HideRowIndices = true, ResizableColumns = false)]
        [SerializeField]
        private string[,] grid;

#if UNITY_EDITOR
        [FormerlySerializedAs("grid")]
        [SerializeField, HideInInspector]
        private BlockColor[,] legacyBlockColorGrid;
#endif

        // ===================== 3) COLOR BLOCK =====================
        [Title("3) Color Block")]
        [LabelText("Các line block của người chơi")]
        [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = true)]
        public List<SelectionLineData> selectionLines = new();

#if UNITY_EDITOR
        [Title("4) Thống kê pixel")]
        [ShowInInspector, DisplayAsString, LabelText("Tổng số pixel đã tô")]
        private int FilledPixelCount => CountByColor().total;

        [ShowInInspector, LabelText("Chi tiết theo màu")]
        [DictionaryDrawerSettings(KeyLabel = "Tên màu", ValueLabel = "Số pixel", IsReadOnly = true)]
        private Dictionary<string, int> PixelCountByColor => CountByColor().byColor;

        private (int total, Dictionary<string, int> byColor) CountByColor()
        {
            var library = ResolvePixelMaterialLibrary();
            var byColor = new Dictionary<string, int>();
            var total = 0;
            if (grid == null)
            {
                return (total, byColor);
            }

            for (var x = 0; x < grid.GetLength(0); x++)
            {
                for (var y = 0; y < grid.GetLength(1); y++)
                {
                    var cellValue = grid[x, y];
                    if (string.IsNullOrEmpty(cellValue))
                    {
                        continue;
                    }

                    var ids = cellValue.Split('/');
                    foreach (var colorId in ids)
                    {
                        if (string.IsNullOrEmpty(colorId)) continue;
                        total++;
                        var label = library != null ? library.GetDisplayName(colorId) : colorId;
                        byColor.TryGetValue(label, out var n);
                        byColor[label] = n + 1;
                    }
                }
            }

            return (total, byColor);
        }

        [Button("Phân bổ Color Block theo thống kê", ButtonHeight = 28)]
        private void BuildBlocksFromCounts()
        {
            var library = ResolvePixelMaterialLibrary();
            var line = new SelectionLineData();

            if (library != null && grid != null)
            {
                var idCounts = new Dictionary<string, int>();
                for (var x = 0; x < grid.GetLength(0); x++)
                {
                    for (var y = 0; y < grid.GetLength(1); y++)
                    {
                        var cellValue = grid[x, y];
                        if (string.IsNullOrEmpty(cellValue))
                        {
                            continue;
                        }

                        var ids = cellValue.Split('/');
                        foreach (var colorId in ids)
                        {
                            if (string.IsNullOrEmpty(colorId)) continue;
                            idCounts.TryGetValue(colorId, out var n);
                            idCounts[colorId] = n + 1;
                        }
                    }
                }

                foreach (var kv in idCounts)
                {
                    line.blocks.Add(new ColorBlockData { colorId = kv.Key, capacity = kv.Value });
                }
            }

            selectionLines = new List<SelectionLineData> { line };
            MarkDirty();
        }

        private string DrawBrushColorId(string value, GUIContent label)
        {
            return PixelColorIdFieldDrawer.Draw(value, label);
        }

        private string DrawBrushColorId2(string value, GUIContent label)
        {
            return PixelColorIdFieldDrawer.Draw(value, label);
        }

        [OnInspectorInit]
        private void EditorInit()
        {
            MigrateLegacyDataIfNeeded();
            ResolvePixelMaterialLibrary();
            EnsureValidBrushColorId();
        }
#endif

        [NonSerialized] private readonly List<string[,]> undoHistory = new();
        private const int MaxUndo = 30;

        public string GetCellColorId(int x, int y)
        {
            EnsureGrid();
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                return PixelColorIds.Empty;
            }

            return grid[x, height - 1 - y] ?? PixelColorIds.Empty;
        }

        public void SetCellColorId(int x, int y, string colorId)
        {
            EnsureGrid();
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                return;
            }

            grid[x, height - 1 - y] = colorId ?? PixelColorIds.Empty;
            MarkDirty();
        }

        public PixelMaterialLibrary ResolvePixelMaterialLibrary()
        {
#if UNITY_EDITOR
            if (pixelMaterialLibrary == null)
            {
                pixelMaterialLibrary = AssetDatabase.LoadAssetAtPath<PixelMaterialLibrary>(
                    PixelMaterialLibrary.DefaultAssetPath);
            }
#endif
            return pixelMaterialLibrary;
        }

        private void EnsureGrid()
        {
            if (grid == null || grid.GetLength(0) != width || grid.GetLength(1) != height)
            {
                ResizeGrid();
            }
        }

        private void ResizeGrid()
        {
            var w = Mathf.Max(1, width);
            var h = Mathf.Max(1, height);
            var newGrid = new string[w, h];
            if (grid != null)
            {
                var cw = Mathf.Min(w, grid.GetLength(0));
                var ch = Mathf.Min(h, grid.GetLength(1));
                for (var x = 0; x < cw; x++)
                {
                    for (var y = 0; y < ch; y++)
                    {
                        newGrid[x, y] = grid[x, y];
                    }
                }
            }

            grid = newGrid;
            MarkDirty();
        }

        private void MarkDirty()
        {
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
            Changed?.Invoke();
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            MigrateLegacyDataIfNeeded();
#endif
            EnsureGrid();
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            MigrateLegacyDataIfNeeded();
            EnsureValidBrushColorId();
#endif
            EnsureGrid();
            Changed?.Invoke();
        }

#if UNITY_EDITOR
        private void MigrateLegacyDataIfNeeded()
        {
            var library = ResolvePixelMaterialLibrary() ?? PixelMaterialLibrary.LoadOrCreateDefault();
            pixelMaterialLibrary = library;

            if (legacyBlockColorGrid != null)
            {
                EnsureGrid();
                var w = legacyBlockColorGrid.GetLength(0);
                var h = legacyBlockColorGrid.GetLength(1);
                for (var x = 0; x < w && x < grid.GetLength(0); x++)
                {
                    for (var y = 0; y < h && y < grid.GetLength(1); y++)
                    {
                        grid[x, y] = library.GetOrCreateIdForLegacyBlockColor(legacyBlockColorGrid[x, y]);
                    }
                }

                legacyBlockColorGrid = null;
                EditorUtility.SetDirty(this);
            }

            MigrateSelectionLineColors(library);
        }

        private void MigrateSelectionLineColors(PixelMaterialLibrary library)
        {
            if (selectionLines == null)
            {
                return;
            }

            foreach (var line in selectionLines)
            {
                if (line.blocks == null)
                {
                    continue;
                }

                foreach (var block in line.blocks)
                {
                    block.MigrateLegacyColor(library);
                }
            }
        }

        private void EnsureValidBrushColorId()
        {
            var library = ResolvePixelMaterialLibrary();
            if (library == null || library.Count == 0)
            {
                brushColorId = PixelColorIds.Empty;
                return;
            }

            if (string.IsNullOrEmpty(brushColorId) || !library.HasColor(brushColorId))
            {
                brushColorId = library.colors[0].id;
            }
        }

        [ButtonGroup("Tools"), Button("Tô toàn bộ bằng cọ")]
        private void FillAll()
        {
            PushUndo();
            EnsureGrid();
            var paintId = eraser ? PixelColorIds.Empty : brushColorId;
            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    grid[x, y] = paintId;
                }
            }

            MarkDirty();
        }

        [ButtonGroup("Tools"), Button("Xoá toàn bộ")]
        private void ClearAll()
        {
            PushUndo();
            grid = new string[Mathf.Max(1, width), Mathf.Max(1, height)];
            MarkDirty();
        }

        [ButtonGroup("Tools"), Button("↶ Undo")]
        private void Undo()
        {
            if (undoHistory.Count == 0)
            {
                return;
            }

            grid = undoHistory[undoHistory.Count - 1];
            undoHistory.RemoveAt(undoHistory.Count - 1);
            MarkDirty();
        }

        private void PushUndo()
        {
            EnsureGrid();
            undoHistory.Add((string[,])grid.Clone());
            if (undoHistory.Count > MaxUndo)
            {
                undoHistory.RemoveAt(0);
            }
        }

        private string DrawCell(Rect rect, string value)
        {
            value ??= PixelColorIds.Empty;
            var e = Event.current;
            var isPaintEvent = e.type == EventType.MouseDown || e.type == EventType.MouseDrag;
            if (isPaintEvent && rect.Contains(e.mousePosition))
            {
                if (e.type == EventType.MouseDown)
                {
                    PushUndo();
                }

                var paintValue = brushColorId;
                if (brushType == ColorBlockType.DualColor && !string.IsNullOrEmpty(secondaryBrushColorId))
                {
                    paintValue = $"{brushColorId}/{secondaryBrushColorId}";
                }

                value = (eraser || e.button == 1) ? PixelColorIds.Empty : paintValue;
                GUI.changed = true;
                MarkDirty();
                e.Use();
            }

            var inner = new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2);
            var library = ResolvePixelMaterialLibrary();

            if (string.IsNullOrEmpty(value))
            {
                EditorGUI.DrawRect(inner, new Color(0.18f, 0.18f, 0.2f));
            }
            else if (value.Contains('/'))
            {
                var parts = value.Split('/');
                var c1 = library != null ? library.GetTint(parts[0]) : Color.gray;
                var c2 = parts.Length > 1 && library != null ? library.GetTint(parts[1]) : Color.gray;

                var topHalf = new Rect(inner.x, inner.y, inner.width, inner.height / 2f);
                var bottomHalf = new Rect(inner.x, inner.y + inner.height / 2f, inner.width, inner.height / 2f);

                EditorGUI.DrawRect(topHalf, c1);
                EditorGUI.DrawRect(bottomHalf, c2);
            }
            else
            {
                var color = library != null ? library.GetTint(value) : Color.gray;
                EditorGUI.DrawRect(inner, color);
            }

            return value;
        }
#endif
    }

#if UNITY_EDITOR
    internal static class PixelColorIdFieldDrawer
    {
        public static string Draw(string currentId, GUIContent label)
        {
            label ??= GUIContent.none;

            var library = PixelMaterialLibrary.LoadOrCreateDefault();
            if (library == null || library.Count == 0)
            {
                if (label == GUIContent.none)
                {
                    EditorGUILayout.LabelField(new GUIContent("(Chưa có màu)"));
                }
                else
                {
                    EditorGUILayout.LabelField(label, new GUIContent("(Chưa có màu)"));
                }

                return currentId ?? PixelColorIds.Empty;
            }

            var names = new List<string>();
            var ids = new List<string>();
            var selected = 0;
            foreach (var color in library.colors)
            {
                names.Add(color.displayName);
                ids.Add(color.id);
                if (color.id == currentId)
                {
                    selected = ids.Count - 1;
                }
            }

            if (string.IsNullOrEmpty(currentId) || !library.HasColor(currentId))
            {
                selected = 0;
            }

            selected = label == GUIContent.none
                ? EditorGUILayout.Popup(selected, names.ToArray())
                : EditorGUILayout.Popup(label, selected, names.ToArray());
            return ids[selected];
        }
    }
#endif
}
