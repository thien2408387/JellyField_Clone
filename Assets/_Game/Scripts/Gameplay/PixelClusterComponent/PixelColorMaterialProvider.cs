using System.Collections.Generic;
using KingCat.Base;
using KingCat.Base.Assets;
using UnityEngine;

public static class PixelColorMaterialProvider
{
    private const string ResourceFolder = "ColorMaterials/";
    private static readonly Dictionary<PixelCubeColor, Material> Cache = new Dictionary<PixelCubeColor, Material>();
    private static readonly Dictionary<int, Color> ShadeVariantColorCache = new Dictionary<int, Color>();
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static Material cubeCellSharedMaterial;

    /// <summary>
    /// Single white shared material instance for all tintable renderers (per-color tint via <see cref="MaterialPropertyBlock"/>).
    /// </summary>
    public static Material GetSharedTintMaterial()
    {
        if (cubeCellSharedMaterial != null)
        {
            return cubeCellSharedMaterial;
        }

        cubeCellSharedMaterial = Resources.Load<Material>(ResourceFolder + "White");
        return cubeCellSharedMaterial;
    }

    /// <summary>
    /// Backward-compatible alias for cube renderers.
    /// </summary>
    public static Material GetCubeCellSharedMaterial()
    {
        return GetSharedTintMaterial();
    }

    /// <summary>
    /// Base tint for a cube color, parsed from the same material as <see cref="GetMaterial"/> (controller database first, then Resources).
    /// </summary>
    public static Color GetCanonicalVisualColor(PixelCubeColor color)
    {
        if (color == PixelCubeColor.None)
        {
            return Color.white;
        }

        Material mat = GetMaterial(color);
        if (mat == null)
        {
            return Color.gray;
        }

        return GetTintColorFromMaterial(mat);
    }

    /// <summary>
    /// Reads tint from material the same way as the editor jelly/cell drawers: <c>_BaseColor</c>, else <c>material.color</c> when <c>_Color</c> exists.
    /// </summary>
    private static Color GetTintColorFromMaterial(Material material)
    {
        if (material == null)
        {
            return Color.gray;
        }

        if (material.HasProperty("_BaseColor"))
        {
            return material.GetColor("_BaseColor");
        }

        if (material.HasProperty("_Color"))
        {
            return material.color;
        }

        return Color.gray;
    }

    /// <summary>
    /// Resolves <see cref="MaterialColorController"/> even if singleton cache is not set yet (Awake order) or the component sits on an inactive object.
    /// </summary>
    private static MaterialColorController ResolveMaterialColorController()
    {
        if (MaterialColorController.TryGetInstance(out MaterialColorController controller))
        {
            return controller;
        }

        if (!Application.isPlaying)
        {
            return null;
        }

        return Object.FindFirstObjectByType<MaterialColorController>(FindObjectsInactive.Include);
    }

    /// <summary>
    /// Same material as <see cref="MaterialColorController.GetMaterial"/> / <see cref="MaterialColorController.TryGetMaterial"/> (reads <c>Database</c> without error spam when unset).
    /// </summary>
    private static bool TryGetMaterialFromColorController(ColorType colorType, out Material material)
    {
        material = null;
        MaterialColorController controller = ResolveMaterialColorController();
        if (controller == null || controller.Database == null)
        {
            return false;
        }

        return controller.Database.TryGetMaterial(colorType, out material) && material != null;
    }

    private static bool TryMapPixelColorToColorType(PixelCubeColor pixelColor, out ColorType colorType)
    {
        switch (pixelColor)
        {
            case PixelCubeColor.Red:
                colorType = ColorType.Red;
                return true;
            case PixelCubeColor.Blue:
                colorType = ColorType.Blue;
                return true;
            case PixelCubeColor.Yellow:
                colorType = ColorType.Yellow;
                return true;
            case PixelCubeColor.Green:
                colorType = ColorType.Green;
                return true;
            case PixelCubeColor.Purple:
                colorType = ColorType.Purple;
                return true;
            case PixelCubeColor.Orange:
                colorType = ColorType.Orange;
                return true;
            case PixelCubeColor.Black:
                colorType = ColorType.Black;
                return true;
            case PixelCubeColor.White:
                colorType = ColorType.White;
                return true;
            case PixelCubeColor.Brown:
                colorType = ColorType.Brown;
                return true;
            case PixelCubeColor.Beige:
                colorType = ColorType.Beige;
                return true;
            case PixelCubeColor.DarkPurple:
                colorType = ColorType.DarkPurple;
                return true;
            case PixelCubeColor.SkyBlue:
                colorType = ColorType.SkyBlue;
                return true;
            case PixelCubeColor.DarkGreen:
                colorType = ColorType.DarkGreen;
                return true;
            case PixelCubeColor.Pink:
                colorType = ColorType.Pink;
                return true;
            case PixelCubeColor.Set2Beige:
                colorType = ColorType.Set2Beige;
                return true;
            case PixelCubeColor.Set2Black_1:
                colorType = ColorType.Set2Black_1;
                return true;
            case PixelCubeColor.Set2Black_2:
                colorType = ColorType.Set2Black_2;
                return true;
            case PixelCubeColor.Set2Brown:
                colorType = ColorType.Set2Brown;
                return true;
            case PixelCubeColor.Set2Cyan_1:
                colorType = ColorType.Set2Cyan_1;
                return true;
            case PixelCubeColor.Set2DarkGreen:
                colorType = ColorType.Set2DarkGreen;
                return true;
            case PixelCubeColor.Set2DarkGreen_1:
                colorType = ColorType.Set2DarkGreen_1;
                return true;
            case PixelCubeColor.Set2Green_1:
                colorType = ColorType.Set2Green_1;
                return true;
            case PixelCubeColor.Set2Green_2:
                colorType = ColorType.Set2Green_2;
                return true;
            case PixelCubeColor.Set2Green_3:
                colorType = ColorType.Set2Green_3;
                return true;
            case PixelCubeColor.Set2Green_3_1:
                colorType = ColorType.Set2Green_3_1;
                return true;
            case PixelCubeColor.Set2Green_4:
                colorType = ColorType.Set2Green_4;
                return true;
            case PixelCubeColor.Set2Green_5:
                colorType = ColorType.Set2Green_5;
                return true;
            case PixelCubeColor.Set2Purple_1:
                colorType = ColorType.Set2Purple_1;
                return true;
            case PixelCubeColor.Set2Purple_2:
                colorType = ColorType.Set2Purple_2;
                return true;
            case PixelCubeColor.Set2Purple_3:
                colorType = ColorType.Set2Purple_3;
                return true;
            case PixelCubeColor.Set2Red_1:
                colorType = ColorType.Set2Red_1;
                return true;
            case PixelCubeColor.Set2Red_1_1:
                colorType = ColorType.Set2Red_1_1;
                return true;
            case PixelCubeColor.Set2Red_1_3:
                colorType = ColorType.Set2Red_1_3;
                return true;
            case PixelCubeColor.Set2Red_2:
                colorType = ColorType.Set2Red_2;
                return true;
            case PixelCubeColor.Set2Red_3:
                colorType = ColorType.Set2Red_3;
                return true;
            case PixelCubeColor.Set2Red_4:
                colorType = ColorType.Set2Red_4;
                return true;
            case PixelCubeColor.Set2Red_5:
                colorType = ColorType.Set2Red_5;
                return true;
            case PixelCubeColor.Set2Red_6:
                colorType = ColorType.Set2Red_6;
                return true;
            case PixelCubeColor.Set2Red_7:
                colorType = ColorType.Set2Red_7;
                return true;
            case PixelCubeColor.Set2SkyBlue:
                colorType = ColorType.Set2SkyBlue;
                return true;
            case PixelCubeColor.Set2Teal:
                colorType = ColorType.Set2Teal;
                return true;
            case PixelCubeColor.Set2Yellow2:
                colorType = ColorType.Set2Yellow2;
                return true;
            case PixelCubeColor.Set2Yellow_1:
                colorType = ColorType.Set2Yellow_1;
                return true;
            case PixelCubeColor.Set2Yellow_1_1:
                colorType = ColorType.Set2Yellow_1_1;
                return true;
            case PixelCubeColor.Set2Yellow_3:
                colorType = ColorType.Set2Yellow_3;
                return true;
            case PixelCubeColor.Set2Yellow_4:
                colorType = ColorType.Set2Yellow_4;
                return true;
            case PixelCubeColor.Set2Yellow_5:
                colorType = ColorType.Set2Yellow_5;
                return true;
            default:
                colorType = default;
                return false;
        }
    }

    public static Material GetMaterial(PixelCubeColor color)
    {
        if (color == PixelCubeColor.None)
        {
            return null;
        }

        if (Cache.TryGetValue(color, out Material cached))
        {
            return cached;
        }

        if (TryMapPixelColorToColorType(color, out ColorType colorType)
            && TryGetMaterialFromColorController(colorType, out Material controllerMaterial))
        {
            Cache[color] = controllerMaterial;
            return controllerMaterial;
        }

        string materialPath = GetMaterialPath(color);
        if (string.IsNullOrEmpty(materialPath))
        {
            return null;
        }

        Material loaded = Resources.Load<Material>(materialPath);
        Cache[color] = loaded;
        return loaded;
    }

    /// <summary>
    /// Returns the quantized tint color for brightness jitter while keeping a shared base material for batching.
    /// </summary>
    public static Material GetBatchedShadeMaterial(
        PixelCubeColor color,
        int shadeIndex,
        int shadeSteps,
        float brightnessJitter,
        out Color resultingColor)
    {
        resultingColor = GetCanonicalVisualColor(color);
        Material baseMaterial = GetSharedTintMaterial();
        if (baseMaterial == null)
        {
            return null;
        }

        int safeSteps = Mathf.Max(1, shadeSteps);
        if (safeSteps <= 1 || brightnessJitter <= 0f)
        {
            return baseMaterial;
        }

        int clampedIndex = Mathf.Clamp(shadeIndex, 0, safeSteps - 1);
        float t = safeSteps <= 1 ? 0.5f : (float)clampedIndex / (safeSteps - 1);
        float brightnessScale = Mathf.Lerp(1f - brightnessJitter, 1f + brightnessJitter, t);
        brightnessScale = Mathf.Clamp(brightnessScale, 0.05f, 2f);

        int key = (((int)color & 0xFF) << 24)
                  ^ ((safeSteps & 0xFF) << 16)
                  ^ ((Mathf.RoundToInt(brightnessJitter * 1000f) & 0xFFFF) << 0)
                  ^ (clampedIndex << 8);

        if (ShadeVariantColorCache.TryGetValue(key, out Color cachedColor))
        {
            resultingColor = cachedColor;
            return baseMaterial;
        }

        Color shaded = ScaleColorValue(resultingColor, brightnessScale);
        resultingColor = shaded;
        ShadeVariantColorCache[key] = shaded;
        return baseMaterial;
    }

    public static Color ScaleColorValue(Color source, float scale)
    {
        float h;
        float s;
        float v;
        Color.RGBToHSV(source, out h, out s, out v);
        v = Mathf.Clamp01(v * scale);
        return Color.HSVToRGB(h, s, v);
    }

    private static string GetMaterialPath(PixelCubeColor color)
    {
        switch (color)
        {
            case PixelCubeColor.Red:
                return ResourceFolder + "Red";
            case PixelCubeColor.Blue:
                return ResourceFolder + "Blue";
            case PixelCubeColor.Yellow:
                return ResourceFolder + "Yellow";
            case PixelCubeColor.Green:
                return ResourceFolder + "Green";
            case PixelCubeColor.Purple:
                return ResourceFolder + "Purple";
            case PixelCubeColor.Orange:
                return ResourceFolder + "Orange";
            case PixelCubeColor.Black:
                return ResourceFolder + "Black";
            case PixelCubeColor.White:
                return ResourceFolder + "White";
            case PixelCubeColor.Brown:
                return ResourceFolder + "Brown";
            case PixelCubeColor.Beige:
                return ResourceFolder + "Beige";
            case PixelCubeColor.DarkPurple:
                return ResourceFolder + "DarkPurple";
            case PixelCubeColor.SkyBlue:
                return ResourceFolder + "SkyBlue";
            case PixelCubeColor.DarkGreen:
                return ResourceFolder + "DarkGreen";
            case PixelCubeColor.Pink:
                return ResourceFolder + "Pink";
            case PixelCubeColor.Set2Beige:
                return "ColorMaterials2/Beige";
            case PixelCubeColor.Set2Black_1:
                return "ColorMaterials2/Black_1";
            case PixelCubeColor.Set2Black_2:
                return "ColorMaterials2/Black_2";
            case PixelCubeColor.Set2Brown:
                return "ColorMaterials2/Brown";
            case PixelCubeColor.Set2Cyan_1:
                return "ColorMaterials2/Cyan_1";
            case PixelCubeColor.Set2DarkGreen:
                return "ColorMaterials2/DarkGreen";
            case PixelCubeColor.Set2DarkGreen_1:
                return "ColorMaterials2/DarkGreen_1";
            case PixelCubeColor.Set2Green_1:
                return "ColorMaterials2/Green_1";
            case PixelCubeColor.Set2Green_2:
                return "ColorMaterials2/Green_2";
            case PixelCubeColor.Set2Green_3:
                return "ColorMaterials2/Green_3";
            case PixelCubeColor.Set2Green_3_1:
                return "ColorMaterials2/Green_3_1";
            case PixelCubeColor.Set2Green_4:
                return "ColorMaterials2/Green_4";
            case PixelCubeColor.Set2Green_5:
                return "ColorMaterials2/Green_5";
            case PixelCubeColor.Set2Purple_1:
                return "ColorMaterials2/Purple_1";
            case PixelCubeColor.Set2Purple_2:
                return "ColorMaterials2/Purple_2";
            case PixelCubeColor.Set2Purple_3:
                return "ColorMaterials2/Purple_3";
            case PixelCubeColor.Set2Red_1:
                return "ColorMaterials2/Red_1";
            case PixelCubeColor.Set2Red_1_1:
                return "ColorMaterials2/Red_1_1";
            case PixelCubeColor.Set2Red_1_3:
                return "ColorMaterials2/Red_1_3";
            case PixelCubeColor.Set2Red_2:
                return "ColorMaterials2/Red_2";
            case PixelCubeColor.Set2Red_3:
                return "ColorMaterials2/Red_3";
            case PixelCubeColor.Set2Red_4:
                return "ColorMaterials2/Red_4";
            case PixelCubeColor.Set2Red_5:
                return "ColorMaterials2/Red_5";
            case PixelCubeColor.Set2Red_6:
                return "ColorMaterials2/Red_6";
            case PixelCubeColor.Set2Red_7:
                return "ColorMaterials2/Red_7";
            case PixelCubeColor.Set2SkyBlue:
                return "ColorMaterials2/SkyBlue";
            case PixelCubeColor.Set2Teal:
                return "ColorMaterials2/Teal";
            case PixelCubeColor.Set2Yellow2:
                return "ColorMaterials2/Yellow2";
            case PixelCubeColor.Set2Yellow_1:
                return "ColorMaterials2/Yellow_1";
            case PixelCubeColor.Set2Yellow_1_1:
                return "ColorMaterials2/Yellow_1_1";
            case PixelCubeColor.Set2Yellow_3:
                return "ColorMaterials2/Yellow_3";
            case PixelCubeColor.Set2Yellow_4:
                return "ColorMaterials2/Yellow_4";
            case PixelCubeColor.Set2Yellow_5:
                return "ColorMaterials2/Yellow_5";
            default:
                return null;
        }
    }
}
