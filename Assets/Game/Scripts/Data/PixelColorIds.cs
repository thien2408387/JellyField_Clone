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
