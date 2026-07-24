using System;
using System.Collections.Generic;
using System.Text;
using Sirenix.OdinInspector;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NexZap.Data
{
    [CreateAssetMenu(fileName = "PixelMaterialLibrary", menuName = "NexZap/Pixel Material Library")]
    public class PixelMaterialLibrary : SerializedScriptableObject
    {
        public const string DefaultMaterialsFolder = "Assets/_NexZap/Art/Materials/PixelColors";
        public const string DefaultAssetPath = "Assets/_NexZap/Data/PixelMaterialLibrary.asset";

        [Serializable]
        public class PixelColorDefinition
        {
            [ReadOnly, HideLabel, HorizontalGroup(80f)]
            [Tooltip("ID nội bộ — không đổi sau khi tạo để level đã vẽ vẫn khớp.")]
            public string id = Guid.NewGuid().ToString("N");

            [HorizontalGroup, LabelText("Tên"), LabelWidth(28f)]
            public string displayName = "Màu mới";

            [HorizontalGroup(120f), HideLabel, ColorUsage(false, false)]
            public Color tint = Color.white;

            [HorizontalGroup(40f), HideLabel, PreviewField(40, ObjectFieldAlignment.Center)]
            public Material material;
        }

        [Title("Bảng màu")]
        [ListDrawerSettings(ShowFoldout = false, DraggableItems = true, ShowIndexLabels = false)]
        public List<PixelColorDefinition> colors = new();

        public int Count => colors.Count;

        public bool HasColor(string colorId)
        {
            return !string.IsNullOrEmpty(colorId) && TryGetColor(colorId, out _);
        }

        public bool TryGetColor(string colorId, out PixelColorDefinition definition)
        {
            definition = null;
            if (string.IsNullOrEmpty(colorId))
            {
                return false;
            }

            foreach (var color in colors)
            {
                if (color.id == colorId)
                {
                    definition = color;
                    return true;
                }
            }

            return false;
        }

        public Material GetMaterial(string colorId)
        {
            return TryGetColor(colorId, out var definition) ? definition.material : null;
        }

        public Color GetTint(string colorId)
        {
            return TryGetColor(colorId, out var definition) ? definition.tint : new Color(0.75f, 0.75f, 0.75f);
        }

        public string GetDisplayName(string colorId)
        {
            return TryGetColor(colorId, out var definition) ? definition.displayName : "?";
        }

        public PixelColorDefinition AddColor(string displayName, Color tint)
        {
            var definition = new PixelColorDefinition
            {
                id = Guid.NewGuid().ToString("N"),
                displayName = displayName,
                tint = tint
            };
            colors.Add(definition);
            return definition;
        }

#if UNITY_EDITOR
        [ButtonGroup("Actions")]
        [Button("Thêm màu", ButtonSizes.Medium)]
        [GUIColor(0.55f, 0.85f, 1f)]
        private void AddColorButton()
        {
            AddColor($"Màu {colors.Count + 1}", Color.white);
            EditorUtility.SetDirty(this);
        }

        [ButtonGroup("Actions")]
        [Button("Bộ màu mẫu", ButtonSizes.Medium)]
        private void AddStarterPaletteButton()
        {
            AddStarterPaletteIfEmpty();
            EditorUtility.SetDirty(this);
        }

        [ButtonGroup("Actions")]
        [Button("Tạo / Cập nhật Materials", ButtonSizes.Medium)]
        [GUIColor(0.4f, 0.85f, 0.55f)]
        private void GenerateMaterialsButton()
        {
            GenerateMaterials();
        }

        public void AddStarterPaletteIfEmpty()
        {
            if (colors.Count > 0)
            {
                return;
            }

            AddColor("Đỏ", ColorPalette.GetUnityColor(BlockColor.Red));
            AddColor("Xanh dương", ColorPalette.GetUnityColor(BlockColor.Blue));
            AddColor("Xanh lá", ColorPalette.GetUnityColor(BlockColor.Green));
            AddColor("Vàng", ColorPalette.GetUnityColor(BlockColor.Yellow));
            AddColor("Tím", ColorPalette.GetUnityColor(BlockColor.Purple));
            AddColor("Cam", ColorPalette.GetUnityColor(BlockColor.Orange));
            AddColor("Hồng", ColorPalette.GetUnityColor(BlockColor.Pink));
            AddColor("Lục lam", ColorPalette.GetUnityColor(BlockColor.Cyan));
            AddColor("Đen", ColorPalette.GetUnityColor(BlockColor.Black));
            AddColor("Trắng", ColorPalette.GetUnityColor(BlockColor.White));
            AddColor("Xám", ColorPalette.GetUnityColor(BlockColor.Gray));
            AddColor("Xám đậm", ColorPalette.GetUnityColor(BlockColor.DarkGray));
        }

        public string GetOrCreateIdForLegacyBlockColor(BlockColor legacyColor)
        {
            if (legacyColor == BlockColor.None)
            {
                return PixelColorIds.Empty;
            }

            var legacyName = legacyColor.ToString();
            foreach (var color in colors)
            {
                if (string.Equals(color.displayName, legacyName, StringComparison.OrdinalIgnoreCase))
                {
                    return color.id;
                }
            }

            return AddColor(legacyName, ColorPalette.GetUnityColor(legacyColor)).id;
        }

        public static IEnumerable<ValueDropdownItem<string>> GetColorDropdownItems()
        {
            var library = LoadOrCreateDefault();
            foreach (var color in library.colors)
            {
                yield return new ValueDropdownItem<string>(color.displayName, color.id);
            }
        }

        public void GenerateMaterials()
        {
            EnsureDefaultFolders();

            var shader = FindPixelShader();
            if (shader == null)
            {
                Debug.LogError("Không tìm thấy shader URP Lit. Hãy cài Universal RP.");
                return;
            }

            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var color in colors)
            {
                var baseName = SanitizeFileName(color.displayName);
                if (string.IsNullOrEmpty(baseName))
                {
                    baseName = "Color";
                }

                var fileName = baseName;
                var suffix = 1;
                while (!usedNames.Add(fileName))
                {
                    fileName = $"{baseName}_{suffix++}";
                }

                var assetPath = $"{DefaultMaterialsFolder}/Pixel_{fileName}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                if (material == null)
                {
                    material = new Material(shader) { name = $"Pixel_{color.displayName}" };
                    AssetDatabase.CreateAsset(material, assetPath);
                }
                else if (material.shader != shader)
                {
                    material.shader = shader;
                }

                ApplyMaterialColor(material, color.tint);
                color.material = material;
                EditorUtility.SetDirty(material);
            }

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Đã tạo/cập nhật {colors.Count} material pixel tại {DefaultMaterialsFolder}");
        }

        public static PixelMaterialLibrary LoadOrCreateDefault()
        {
            var library = AssetDatabase.LoadAssetAtPath<PixelMaterialLibrary>(DefaultAssetPath);
            if (library != null)
            {
                return library;
            }

            EnsureDefaultFolders();
            library = CreateInstance<PixelMaterialLibrary>();
            library.AddStarterPaletteIfEmpty();
            AssetDatabase.CreateAsset(library, DefaultAssetPath);
            AssetDatabase.SaveAssets();
            return library;
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "Color";
            }

            var builder = new StringBuilder(name.Length);
            foreach (var c in name.Trim())
            {
                builder.Append(char.IsLetterOrDigit(c) || c is '_' or '-' ? c : '_');
            }

            return builder.ToString();
        }

        private static void EnsureDefaultFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_NexZap/Art"))
            {
                AssetDatabase.CreateFolder("Assets/_NexZap", "Art");
            }

            if (!AssetDatabase.IsValidFolder("Assets/_NexZap/Art/Materials"))
            {
                AssetDatabase.CreateFolder("Assets/_NexZap/Art", "Materials");
            }

            if (!AssetDatabase.IsValidFolder(DefaultMaterialsFolder))
            {
                AssetDatabase.CreateFolder("Assets/_NexZap/Art/Materials", "PixelColors");
            }

            if (!AssetDatabase.IsValidFolder("Assets/_NexZap/Data"))
            {
                AssetDatabase.CreateFolder("Assets/_NexZap", "Data");
            }
        }

        private static Shader FindPixelShader()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                return shader;
            }

            shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader != null)
            {
                return shader;
            }

            return Shader.Find("Standard");
        }

        private static void ApplyMaterialColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.35f);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }
        }
#endif
    }
}
