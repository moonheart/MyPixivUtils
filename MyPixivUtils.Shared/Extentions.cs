using System;

namespace MyPixivUtils.Shared
{
    public static class Extentions
    {
        public static int ToInt(this string s)
        {
            return int.TryParse(s, out int x) ? x : 0;
        }
        public static int? ToIntNullable(this string s)
        {
            return int.TryParse(s, out int x) ? x : (int?)null;
        }
    }
}
