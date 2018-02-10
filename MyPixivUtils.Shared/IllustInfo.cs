using System;
using System.Collections.Generic;
using System.Text;

namespace MyPixivUtils.Shared
{
    public class IllustInfo
    {
        public string Id { get; set; }
        public int BookmarkCount { get; set; }
        public bool IsBookmarked { get; set; }
        public string[] ids { get; set; }
        public int pagecount { get; set; }
        public string[] tags { get; set; }
    }
}
