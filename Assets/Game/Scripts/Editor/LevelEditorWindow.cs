#if UNITY_EDITOR
using System.IO;
using System.Linq;
using NexZap.Data;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;
namespace NexZap.EditorTools
{
    public class LevelEditorWindow : OdinMenuEditorWindow
    {
        private const string LevelFolder = "Assets/Game/Data/Levels";

        private bool autoSave = true;

        [MenuItem("NexZap/Level Editor")]
        private static void Open()
        {
            var window = GetWindow<LevelEditorWindow>();
            window.titleContent = new GUIContent("Level Editor");
            window.minSize = new Vector2(840, 560);
        }

            // Cây menu bên trái = tất cả asset BaseLevel -> click vào là "load" để edit.
             protected override OdinMenuTree BuildMenuTree()
        {
            var tree = new OdinMenuTree(supportsMultiSelect: false);
            tree.Config.DrawSearchToolbar = true;
            tree.AddAllAssetsAtPath("Levels", LevelFolder, typeof(BaseLevel), includeSubDirectories: true);
            return tree;
        }

        // Thanh công cụ phía trên: nút Add / Delete + toggle Auto Save.
        protected override void OnBeginDrawEditors()
        {
            var selected = MenuTree?.Selection?.FirstOrDefault();
            SirenixEditorGUI.BeginHorizontalToolbar();
            {
                if (SirenixEditorGUI.ToolbarButton(new GUIContent(" + Tạo Level ")))
                {
                    CreateLevel();
                }
                if (selected != null && SirenixEditorGUI.ToolbarButton(new GUIContent(" Xoá Level ")))
                {
                    DeleteLevel(selected.Value as BaseLevel);
                }
                if (SirenixEditorGUI.ToolbarButton(new GUIContent(" Pixel Colors ")))
                {
                    PixelColorMaterialGenerator.CreateMaterialLibrary();
                }
                GUILayout.FlexibleSpace();
                autoSave = SirenixEditorGUI.ToolbarToggle(autoSave, "Auto Save");
            }
            SirenixEditorGUI.EndHorizontalToolbar();
        }

        // Auto save: phát hiện có thay đổi -> đánh dấu dirty + ghi asset.
        protected override void DrawEditors()
        {
            EditorGUI.BeginChangeCheck();
            base.DrawEditors();
            if (EditorGUI.EndChangeCheck() && autoSave)
            {
                if (MenuTree?.Selection?.SelectedValue is BaseLevel level)
                {
                    EditorUtility.SetDirty(level);
                    AssetDatabase.SaveAssetIfDirty(level);
                }
            }
        }

             private void CreateLevel()
        {
            if (!Directory.Exists(LevelFolder))
            {
                Directory.CreateDirectory(LevelFolder);
                AssetDatabase.Refresh();
            }
            var level = CreateInstance<BaseLevel>();
            var path = AssetDatabase.GenerateUniqueAssetPath($"{LevelFolder}/Level_New.asset");
            AssetDatabase.CreateAsset(level, path);
            AssetDatabase.SaveAssets();
            ForceMenuTreeRebuild();
            TrySelectMenuItemWithObject(level);
        }

        private void DeleteLevel(BaseLevel level)
        {
            if (level == null)
            {
                return;
            }
            if (!EditorUtility.DisplayDialog("Xoá Level", $"Xoá '{level.name}'?", "Xoá", "Huỷ"))
            {
                return;
            }
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(level));
            AssetDatabase.SaveAssets();
            ForceMenuTreeRebuild();
        }
    }
}


#endif
