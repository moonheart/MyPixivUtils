using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CsQuery;
using MyPixivUtils.Shared;
using Newtonsoft.Json.Linq;

namespace MyPixivUtils.DiscoveryAndBookmark
{
    public class PixivBookmarkTool : IDisposable
    {
        public HttpClient _HttpClient;
        //private ConcurrentQueue<string> _illustQueue = new ConcurrentQueue<string>();
        private int _minCount = Program.Configuration["minCount"].ToInt();
        private CookieContainer _container;
        private Uri _pixiv = new Uri("https://www.pixiv.net/");
        private bool _disposing;
        private bool _running;
        public PixivBookmarkTool()
        {
            string cookieText = LocalSetting.Instance["cookie"] ?? "";

            var httpClientHandler = new HttpClientHandler();
            httpClientHandler.CookieContainer.Add(StringToCookie(cookieText));
            httpClientHandler.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
            _HttpClient = new HttpClient(httpClientHandler)
            {
                DefaultRequestHeaders =
                {
                    {
                        "User-Agent",
                        "Mozilla/5.0 (iPhone; CPU iPhone OS 11_0 like Mac OS X) AppleWebKit/604.1.38 (KHTML, like Gecko) Version/11.0 Mobile/15A372 Safari/604.1"
                    },
                    {
                        "Accept-Encoding", "gzip, deflate, br"
                    },
                    {
                        "Accept-Language", "ja-JP,ja;q=0.9,zh-CN;q=0.8,zh;q=0.7,en-US;q=0.6,en;q=0.5"
                    }
                },
                Timeout = TimeSpan.FromSeconds(10)
            };
            _container = httpClientHandler.CookieContainer;
        }

        public Task Start(params string[] illustids)
        {
            return Task.Run(() => RunDiscovery());
        }

        public void Dispose()
        {
            _disposing = true;
            while (_running)
            {
                Thread.Sleep(100);
            }
            var cookies = _container.GetCookies(_pixiv);
            LocalSetting.Instance["cookie"] = CookieToString(cookies);
            _HttpClient.Dispose();
        }

        private void RunDiscovery()
        {
            _running = true;
            var random = new Random();
            while (!_disposing)
            {
                var json = HttpGetReturnJson(
                    new Uri("https://www.pixiv.net/touch/ajax/recommender?type=illust&mode=all"),
                    "https://www.pixiv.net/discovery");
                var jo = JObject.Parse(json);
                if (jo["isSucceed"].ToObject<bool>())
                {
                    var ids = jo["recommended_work_ids"].ToArray().Select(x => x.ToString()).ToList();
                    Console.WriteLine($"get {ids.Count}");
                    int succeed = 0;
                    int failed = 0;
                    int skiped = 0;
                    for (var index = 0; index < ids.Count; index++)
                    {
                        var id = ids[index];
                        if (_disposing) break;
                        Thread.Sleep(random.Next(300, 1000));
                        Console.Write($"[{index + 1}/{succeed}/{skiped}/{failed}/{ids.Count}] processing: {id}");
                        var detailUri = new Uri($"https://www.pixiv.net/member_illust.php?mode=medium&illust_id={id}");
                        var info = GetIllustInfo(id, HttpGetHtml(detailUri, "https://www.pixiv.net/discovery"));
                        Console.Write($", bookmark:{info.BookmarkCount}, page: {info.pagecount}, tags:{""}");
                        var blackTags = new[] { "動物", "食べ物", "建物", "ご飯", "鳥", "猫", "犬" };
                        if (info.tags.Intersect(blackTags).Any())
                        {
                            Console.Write(", skiped bad tag");
                        }
                        else if (info.BookmarkCount > _minCount && info.pagecount < 4)
                        {
                            int restrict = 0;
                            if (Regex.IsMatch(string.Join(",", info.tags), @"r(-)?18", RegexOptions.IgnoreCase))
                            {
                                restrict = 1;
                            }
                            Console.Write($", restrict: {restrict}");
                            json = HttpPostReturnJson(new Uri("https://www.pixiv.net/touch/ajax_api/ajax_api.php"),
                                () => new StringContent(
                                    $"mode=add_bookmark_illust&tt={info.tt}&id={info.Id}&restrict={restrict}&tag=&comment=",
                                    Encoding.UTF8, "application/x-www-form-urlencoded"),
                                detailUri.ToString());
                            jo = JObject.Parse(json);
                            if (jo != null && jo["isSucceed"].ToObject<bool>())
                            {
                                Console.Write(", succeed");
                                succeed++;
                            }
                            else
                            {
                                Console.Write(", failed");
                                failed++;
                            }
                        }
                        else
                        {
                            Console.Write(", skiped");
                            skiped++;
                        }
                        Console.Write("\r\n");
                    }
                }
            }
            _running = false;
        }

        private IllustInfo GetIllustInfo(string id, string html)
        {
            CQ cq = html;
            var illustInfo = new IllustInfo();
            illustInfo.Id = id;
            illustInfo.BookmarkCount = cq[".bookmark-count"].Eq(0).Text().ToInt();
            illustInfo.tt = Regex.Match(html, @"""pixiv.context.postKey"":""(?<tt>[0-9a-f]{32})""", RegexOptions.IgnoreCase).Groups["tt"].Value;
            //var activeMark = cq["#bookmark img.active"].Attr("src");
            //illustInfo.IsBookmarked = activeMark.Contains("button-bookmark-active");
            var ids = Regex.Matches(html, @"<li id=""il(?<id>\d+)""").Cast<Match>().Select(x => x.Groups["id"].Value).Distinct();
            illustInfo.ids = ids.Where(d => d != id).ToArray();
            illustInfo.pagecount = cq[".img-box .page-count"].Text().ToIntNullable() ?? 1;
            illustInfo.tags = cq[".tag"].Selection.Select(x => x.TextContent.Replace("* ", "")).ToArray();
            return illustInfo;
        }

        private string HttpGetHtml(Uri uri, string referer = null)
        {
            return Retry(() =>
            {
                var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, uri)
                {
                    Headers =
                    {
                        {
                            "Accept",
                            "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8"
                        },
                        {"Referer", referer ?? _pixiv.ToString()}
                    }
                };
                return _HttpClient.SendAsync(httpRequestMessage).Result.Content.ReadAsStringAsync().Result;
            });
        }

        private T Retry<T>(Func<T> fuc)
        {
            int maxRetry = 5;
            while (maxRetry-- > 0)
            {
                try
                {
                    return fuc();
                }
                catch (Exception e)
                {
                }
            }

            throw new Exception("max retry times exceed");
        }

        private string HttpPostReturnJson(Uri uri, Func<HttpContent> content, string referer)
        {
            return Retry(() =>
            {
                var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, uri)
                {
                    Headers =
                {
                    {"Accept", "application/json, text/javascript, */*; q=0.01"},
                    {"Referer", referer}
                },
                    Content = content()
                };
                return _HttpClient.SendAsync(httpRequestMessage).Result.Content.ReadAsStringAsync().Result;
            });
        }
        private string HttpGetReturnJson(Uri uri, string referer)
        {
            return Retry(() =>
            {
                var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, uri)
                {
                    Headers =
                {
                    {"Accept", "application/json"},
                    {"Referer", referer},
                    {"X-Requested-With","XMLHttpRequest" }
                }
                };
                return _HttpClient.SendAsync(httpRequestMessage).Result.Content.ReadAsStringAsync().Result;
            });
        }

        private CookieCollection StringToCookie(string s)
        {
            var x = new CookieCollection();
            var arrs = s.Split(';')
                .Select(d => d?.Trim() ?? "")
                .Where(d => d.Length > 0)
                .Where(d => d.Split('=').Length > 0)
                .Select(d => new { k = d.Split('=')[0], v = d.Split('=')[1] })
                .Select(d => new Cookie(d.k, d.v, "/", "www.pixiv.net"));
            foreach (var cookie in arrs)
            {
                x.Add(cookie);
            }
            return x;
        }

        private string CookieToString(CookieCollection cookies)
        {
            return string.Join("; ", cookies.Cast<Cookie>().Select(d => $"{d.Name}={d.Value}"));
        }
    }

    public class IllustInfo
    {
        public string Id { get; set; }
        public int BookmarkCount { get; set; }
        public string tt { get; set; }
        public bool IsBookmarked { get; set; }
        public string[] ids { get; set; }
        public int pagecount { get; set; }
        public string[] tags { get; set; }
    }


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
