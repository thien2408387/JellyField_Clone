namespace NexZap.Data
{
    public static class PixelColorIds
    {
        public const string Empty = "";

        /// <summary>Ô 2 màu được lưu dạng "idChính/idPhụ".</summary>
        public const char Separator = '/';

        public static bool IsDual(string cellColorId)
        {
            return !string.IsNullOrEmpty(cellColorId) && cellColorId.IndexOf(Separator) > 0;
        }

        public static string GetPrimary(string cellColorId)
        {
            if (string.IsNullOrEmpty(cellColorId))
            {
                return Empty;
            }

            var separatorIndex = cellColorId.IndexOf(Separator);
            return separatorIndex < 0 ? cellColorId : cellColorId.Substring(0, separatorIndex);
        }

        public static string GetSecondary(string cellColorId)
        {
            if (string.IsNullOrEmpty(cellColorId))
            {
                return Empty;
            }

            var separatorIndex = cellColorId.IndexOf(Separator);
            return separatorIndex < 0 ? Empty : cellColorId.Substring(separatorIndex + 1);
        }

        public static string[] Split(string cellColorId)
        {
            if (string.IsNullOrEmpty(cellColorId))
            {
                return new string[0];
            }

            return IsDual(cellColorId)
                ? new[] { GetPrimary(cellColorId), GetSecondary(cellColorId) }
                : new[] { cellColorId };
        }

        public static bool Contains(string cellColorId, string colorId)
        {
            if (string.IsNullOrEmpty(cellColorId) || string.IsNullOrEmpty(colorId))
            {
                return false;
            }

            return GetPrimary(cellColorId) == colorId || GetSecondary(cellColorId) == colorId;
        }

        /// <summary>Bỏ 1 lớp màu khỏi ô; ô 2 màu còn lại id của màu kia, ô 1 màu thành rỗng.</summary>
        public static string Remove(string cellColorId, string colorId)
        {
            if (!Contains(cellColorId, colorId))
            {
                return cellColorId ?? Empty;
            }

            var primaryColorId = GetPrimary(cellColorId);
            return primaryColorId == colorId ? GetSecondary(cellColorId) : primaryColorId;
        }

        public static string Combine(string primaryColorId, string secondaryColorId)
        {
            if (string.IsNullOrEmpty(primaryColorId))
            {
                return secondaryColorId ?? Empty;
            }

            if (string.IsNullOrEmpty(secondaryColorId))
            {
                return primaryColorId;
            }

            return primaryColorId + Separator + secondaryColorId;
        }
    }
}
