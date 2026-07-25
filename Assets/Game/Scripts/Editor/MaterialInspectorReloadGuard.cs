#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NexZap.Editor
{
    /// <summary>
    /// Unity 6000.0 can rebuild a selected Material inspector during domain reload,
    /// before a GUI skin exists. That initializes MaterialEditor.Styles too early
    /// and produces "Unable to use a named GUIStyle without a current skin".
    /// Remove only Material assets from the selection before the reload begins.
    /// </summary>
    [InitializeOnLoad]
    internal static class MaterialInspectorReloadGuard
    {
        static MaterialInspectorReloadGuard()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= ClearSelectedMaterials;
            AssemblyReloadEvents.beforeAssemblyReload += ClearSelectedMaterials;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                ClearSelectedMaterials();
            }
        }

        private static void ClearSelectedMaterials()
        {
            var selection = Selection.objects;
            if (selection == null || !selection.Any(item => item is Material))
            {
                return;
            }

            Selection.objects = selection.Where(item => item != null && item is not Material).ToArray();
        }
    }
}
#endif
