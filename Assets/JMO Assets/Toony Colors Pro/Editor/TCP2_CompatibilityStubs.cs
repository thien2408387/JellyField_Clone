using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ToonyColorsPro.Utilities
{
    internal static class Utils
    {
        public static bool IsUsingURP()
        {
            RenderPipelineAsset pipelineAsset = GraphicsSettings.currentRenderPipeline;
            if (pipelineAsset == null)
            {
                return false;
            }

            string pipelineTypeName = pipelineAsset.GetType().FullName;
            return !string.IsNullOrEmpty(pipelineTypeName) && pipelineTypeName.Contains("Universal");
        }
    }
}

namespace ToonyColorsPro.ShaderGenerator
{
    internal static class TCP2_GUI
    {
        private static GUIStyle _helpBoxRichTextStyle;

        public static GUIContent TempContent(string text)
        {
            return new GUIContent(text);
        }

        public static GUIStyle HelpBoxRichTextStyle
        {
            get
            {
                if (_helpBoxRichTextStyle == null)
                {
                    _helpBoxRichTextStyle = new GUIStyle(EditorStyles.helpBox)
                    {
                        richText = true,
                        wordWrap = true
                    };
                }

                return _helpBoxRichTextStyle;
            }
        }

        public static void SeparatorSimple()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.2f));
        }
    }

    internal static class SGUILayout
    {
        internal static class Styles
        {
            private static GUIStyle _grayMiniLabelWrap;
            private static GUIStyle _orangeBoldLabel;

            public static GUIStyle GrayMiniLabelWrap
            {
                get
                {
                    if (_grayMiniLabelWrap == null)
                    {
                        _grayMiniLabelWrap = new GUIStyle(EditorStyles.miniLabel)
                        {
                            wordWrap = true
                        };
                    }

                    return _grayMiniLabelWrap;
                }
            }

            public static GUIStyle OrangeBoldLabel
            {
                get
                {
                    if (_orangeBoldLabel == null)
                    {
                        _orangeBoldLabel = new GUIStyle(EditorStyles.boldLabel)
                        {
                            normal =
                            {
                                textColor = new Color(1f, 0.55f, 0f)
                            }
                        };
                    }

                    return _orangeBoldLabel;
                }
            }
        }
    }
}
