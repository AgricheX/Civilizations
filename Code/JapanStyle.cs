namespace Civilizations
{
    internal static class JapanStyle
    {
        public static bool IsJapanese(string id)
        {
            return id != null && id.Contains("_jp_");
        }

        public static bool Uses(Kingdom k)
        {
            if (k == null || k.data == null) return false;
            NameCulture c = RealNames.Of(k);
            if (c != null && c.Id == "ja") return true;
            long h = k.data.id * 2654435761L;
            return ((h >> 7) & 7) == 0;
        }

        public static bool Allows(Kingdom k, string id)
        {
            return !IsJapanese(id) || Uses(k);
        }
    }
}
