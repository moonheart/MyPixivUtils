using System;
using System.Collections.Generic;
using System.Text;

namespace MyPixivUtils.Shared
{
    public class illust
    {
        public string illust_id { get; set; }
        public string illust_user_id { get; set; }
        public string illust_title { get; set; }
        public string illust_ext { get; set; }
        public string illust_width { get; set; }
        public string illust_height { get; set; }
        public string illust_restrict { get; set; }
        public string illust_x_restrict { get; set; }
        public string illust_create_date { get; set; }
        public string illust_upload_date { get; set; }
        public string illust_server_id { get; set; }
        public object illust_hash { get; set; }
        public string illust_type { get; set; }
        public int illust_sanity_level { get; set; }
        public string illust_book_style { get; set; }
        public string illust_page_count { get; set; }
        public string illust_tag_full_lock { get; set; }
        public string user_account { get; set; }
        public string user_name { get; set; }
        public string url { get; set; }
        public bool illust_series { get; set; }
        public bool is_muted { get; set; }
    }
}
