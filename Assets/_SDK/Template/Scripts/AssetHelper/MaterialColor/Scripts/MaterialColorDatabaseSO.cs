using System;
using System.Collections.Generic;
using UnityEngine;

namespace KingCat.Base.Assets
{
    public enum ColorType : byte
    {
        Blue = 0,
        Yellow = 1,
        Green = 2,
        Red = 3,
        Orange = 4,
        Purple = 5,
        Pink = 6,
        Cyan = 7,
        DarkBlue = 8,
        White = 9,
        Black = 10,
        Grey = 11,
        Brown = 12,
        Beige = 13,
        DarkPurple = 14,
        SkyBlue = 15,
        DarkGreen = 16,
        Set2Beige = 17,
        Set2Black_1 = 18,
        Set2Black_2 = 19,
        Set2Brown = 20,
        Set2Cyan_1 = 21,
        Set2DarkGreen = 22,
        Set2DarkGreen_1 = 23,
        Set2Green_1 = 24,
        Set2Green_2 = 25,
        Set2Green_3 = 26,
        Set2Green_3_1 = 27,
        Set2Green_4 = 28,
        Set2Green_5 = 29,
        Set2Purple_1 = 30,
        Set2Purple_2 = 31,
        Set2Purple_3 = 32,
        Set2Red_1 = 33,
        Set2Red_1_1 = 34,
        Set2Red_1_3 = 35,
        Set2Red_2 = 36,
        Set2Red_3 = 37,
        Set2Red_4 = 38,
        Set2Red_5 = 39,
        Set2Red_6 = 40,
        Set2Red_7 = 41,
        Set2SkyBlue = 42,
        Set2Teal = 43,
        Set2Yellow2 = 44,
        Set2Yellow_1 = 45,
        Set2Yellow_1_1 = 46,
        Set2Yellow_3 = 47,
        Set2Yellow_4 = 48,
        Set2Yellow_5 = 49
    }

    [CreateAssetMenu(fileName = "MaterialColorDatabase", menuName = "KingCat/Materials/Material Color Database")]
    public class MaterialColorDatabaseSO : ScriptableObject
    {
        [Serializable]
        public class MaterialColorEntry
        {
            public ColorType colorType;
            public Material material;
        }

        [SerializeField] private List<MaterialColorEntry> entries = new List<MaterialColorEntry>();

        private Dictionary<ColorType, Material> _cache;

        public IReadOnlyList<MaterialColorEntry> Entries => entries;

        private void BuildCache()
        {
            if (_cache != null) return;

            _cache = new Dictionary<ColorType, Material>();

            for (int i = 0; i < entries.Count; i++)
            {
                MaterialColorEntry entry = entries[i];
                if (entry == null) continue;

                if (_cache.ContainsKey(entry.colorType))
                {
                    Debug.LogWarning($"[MaterialColorDatabaseSO] Duplicate ColorType: {entry.colorType} in {name}");
                    continue;
                }

                _cache.Add(entry.colorType, entry.material);
            }
        }

        public Material GetMaterial(ColorType colorType)
        {
            BuildCache();

            if (_cache.TryGetValue(colorType, out Material material))
                return material;

            Debug.LogWarning($"[MaterialColorDatabaseSO] Material not found for ColorType: {colorType} in {name}");
            return null;
        }

        public bool TryGetMaterial(ColorType colorType, out Material material)
        {
            BuildCache();
            return _cache.TryGetValue(colorType, out material);
        }

        public bool HasColor(ColorType colorType)
        {
            BuildCache();
            return _cache.ContainsKey(colorType);
        }

        public void ClearCache()
        {
            _cache = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _cache = null;
        }
#endif
    }
}
