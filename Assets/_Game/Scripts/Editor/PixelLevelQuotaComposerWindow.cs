using System;
using System.Collections.Generic;
using KingCat.Base.Assets;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor window: quota + top pixel paint + jelly layout + export level package (prefab + JSON).
/// </summary>
/// <remarks>
/// <para><b>Partial layout (SRP by file)</b></para>
/// <list type="bullet">
/// <item><see cref="PixelLevelQuotaComposerWindow"/> — shell: menu, lifecycle, <see cref="OnGUI"/> wiring.</item>
/// <item><c>PixelLevelQuotaComposerWindow.SharedGUI.cs</c> — shared drawing / quota UI / color helpers.</item>
/// <item><c>PixelLevelQuotaComposerWindow.Top.cs</c> + <c>TopGUI.cs</c> — top cube grid model + IMGUI.</item>
/// <item><c>PixelLevelQuotaComposerWindow.Jelly.cs</c> + <c>JellyGUI.cs</c> — jelly grid model + IMGUI.</item>
/// <item><c>PixelLevelQuotaComposerWindow.Export.cs</c> — paths, JSON snapshot, level prefab generation, validation helpers.</item>
/// </list>
/// <para>Convention: private instance fields use a leading underscore (<c>_fieldName</c>) across all partials of this window.</para>
/// </remarks>
public partial class PixelLevelQuotaComposerWindow : EditorWindow
{
    [Serializable]
    private struct ColorQuotaEntry
    {
        public PixelCubeColor Color;
        public int TargetCount;
    }

    private static readonly PixelCubeColor[] ManagedColors =
    {
        PixelCubeColor.Red,
        PixelCubeColor.Blue,
        PixelCubeColor.Yellow,
        PixelCubeColor.Green,
        PixelCubeColor.Purple,
        PixelCubeColor.Orange,
        PixelCubeColor.Black,
        PixelCubeColor.White,
        PixelCubeColor.Brown,
        PixelCubeColor.Beige,
        PixelCubeColor.DarkPurple,
        PixelCubeColor.SkyBlue,
        PixelCubeColor.DarkGreen,
        PixelCubeColor.Pink,
        PixelCubeColor.Set2Beige,
        PixelCubeColor.Set2Black_1,
        PixelCubeColor.Set2Black_2,
        PixelCubeColor.Set2Brown,
        PixelCubeColor.Set2Cyan_1,
        PixelCubeColor.Set2DarkGreen,
        PixelCubeColor.Set2DarkGreen_1,
        PixelCubeColor.Set2Green_1,
        PixelCubeColor.Set2Green_2,
        PixelCubeColor.Set2Green_3,
        PixelCubeColor.Set2Green_3_1,
        PixelCubeColor.Set2Green_4,
        PixelCubeColor.Set2Green_5,
        PixelCubeColor.Set2Purple_1,
        PixelCubeColor.Set2Purple_2,
        PixelCubeColor.Set2Purple_3,
        PixelCubeColor.Set2Red_1,
        PixelCubeColor.Set2Red_1_1,
        PixelCubeColor.Set2Red_1_3,
        PixelCubeColor.Set2Red_2,
        PixelCubeColor.Set2Red_3,
        PixelCubeColor.Set2Red_4,
        PixelCubeColor.Set2Red_5,
        PixelCubeColor.Set2Red_6,
        PixelCubeColor.Set2Red_7,
        PixelCubeColor.Set2SkyBlue,
        PixelCubeColor.Set2Teal,
        PixelCubeColor.Set2Yellow2,
        PixelCubeColor.Set2Yellow_1,
        PixelCubeColor.Set2Yellow_1_1,
        PixelCubeColor.Set2Yellow_3,
        PixelCubeColor.Set2Yellow_4,
        PixelCubeColor.Set2Yellow_5,
    };

    private const string MaterialColorDatabaseAssetPath =
        "Assets/_SDK/Template/Scripts/AssetHelper/MaterialColor/SO/MaterialColorDatabase.asset";

    private Vector2 _scrollPosition;
    private MaterialColorDatabaseSO _colorDatabase;
    private ColorQuotaEntry[] _colorQuotas;

    [MenuItem("Tools/Pixel Fever/Level Quota Composer")]
    public static void ShowWindow()
    {
        PixelLevelQuotaComposerWindow window = GetWindow<PixelLevelQuotaComposerWindow>("Level Quota Composer");
        window.minSize = new Vector2(620f, 720f);
        window.InitializeState();
    }

    private void OnEnable()
    {
        InitializeState();
    }

    private void InitializeState()
    {
        EnsureColorQuotas();
        InitializeTopGridIfNeeded();
        InitializeJellyGridIfNeeded();
        TryAutoAssignLevelRootPrefab();
        TryAutoAssignKeyPrefab();
    }

    private void OnGUI()
    {
        if (!_colorDatabase)
        {
            _colorDatabase = AssetDatabase.LoadAssetAtPath<MaterialColorDatabaseSO>(MaterialColorDatabaseAssetPath);
        }

        EnsureColorQuotas();
        InitializeTopGridIfNeeded();
        InitializeJellyGridIfNeeded();

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        DrawQuotaSection();
        EditorGUILayout.Space(10f);
        DrawTopSection();
        EditorGUILayout.Space(10f);
        DrawJellySection();
        EditorGUILayout.Space(10f);
        DrawValidationSection();

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(6f);
        DrawBottomActions();
    }
}
