#if UNITY_EDITOR
using NexZap.Data;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace NexZap.EditorTools
{
    [CustomEditor(typeof(PixelMaterialLibrary))]
    public class PixelMaterialLibraryEditor : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "Thêm màu, đặt tên và chọn tint trong danh sách phía trên.\n" +
                "Bấm \"Tạo / Cập nhật Materials\" để sinh file .mat.\n" +
                $"Materials lưu tại: {PixelMaterialLibrary.DefaultMaterialsFolder}",
                MessageType.Info);
        }
    }
}
#endif
