#if UNITY_EDITOR
using NexZap.Data;
using UnityEditor;
using UnityEngine;

namespace NexZap.EditorTools
{
    public static class PixelColorMaterialGenerator
    {
        [MenuItem("NexZap/Pixel Colors/Tạo Material Library")]
        public static void CreateMaterialLibrary()
        {
            var library = PixelMaterialLibrary.LoadOrCreateDefault();
            library.GenerateMaterials();
            Selection.activeObject = library;
            EditorGUIUtility.PingObject(library);
        }

        [MenuItem("NexZap/Pixel Colors/Tạo / Cập nhật tất cả Materials")]
        public static void GenerateAllFromMenu()
        {
            var library = PixelMaterialLibrary.LoadOrCreateDefault();
            library.GenerateMaterials();
        }

        public static void Generate(PixelMaterialLibrary library)
        {
            library?.GenerateMaterials();
        }
    }
}
#endif
