using UnityEngine;

namespace NexZap.Data
{
    public static class ColorPalette
    {
        public static UnityEngine.Color GetUnityColor(BlockColor color)
        {
            return color switch
            {
                BlockColor.Red => new UnityEngine.Color(0.93f, 0.26f, 0.26f),
                BlockColor.Blue => new UnityEngine.Color(0.25f, 0.47f, 0.96f),
                BlockColor.Green => new UnityEngine.Color(0.30f, 0.78f, 0.38f),
                BlockColor.Yellow => new UnityEngine.Color(0.98f, 0.82f, 0.20f),
                BlockColor.Purple => new UnityEngine.Color(0.67f, 0.33f, 0.92f),
                BlockColor.Orange => new UnityEngine.Color(0.98f, 0.55f, 0.18f),
                BlockColor.Pink => new UnityEngine.Color(0.98f, 0.45f, 0.72f),
                BlockColor.Cyan => new UnityEngine.Color(0.22f, 0.82f, 0.88f),
                BlockColor.Black => new UnityEngine.Color(0.10f, 0.10f, 0.12f),
                BlockColor.White => new UnityEngine.Color(0.95f, 0.95f, 0.95f),
                BlockColor.Gray => new UnityEngine.Color(0.60f, 0.60f, 0.62f),
                BlockColor.DarkGray => new UnityEngine.Color(0.30f, 0.30f, 0.33f),
                _ => new UnityEngine.Color(0.75f, 0.75f, 0.75f, 0.35f)
            };
        }
    }
}
