using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using CsQuery;
using Newtonsoft.Json.Linq;

namespace MyPixivUtils.Shared
{
    public class PixivClient : IDisposable
    {

        public class Settings
        {
            [Option("mincount", Default = 5000, HelpText = "最小收藏量")]
            public int MinCount { get; set; }

            [Option("maxpage",Default = 3, HelpText = "最大作品页数")]
            public int MaxIllustPageCount { get; set; }

            [Option("r18",Default = true, HelpText = "R18作品收藏到私人")]
            public bool R18InPrivate { get; set; }

            [Option("tag",Default = false, HelpText = "启用标签过滤")]
            public bool EnableTagFilter { get; set; }

            [Option("startpage",Default = 1, HelpText = "开始页码")]
            public int StartPage { get; set; }

            [Option("search",Default = "", HelpText = "搜索词")]
            public string SearchWord { get; set; }


        }
        public HttpClient _HttpClient;
        //private ConcurrentQueue<string> _illustQueue = new ConcurrentQueue<string>();
        private CookieContainer _container;
        private Uri _pixiv = new Uri("https://www.pixiv.net/");
        private bool _disposing;
        private bool _running;
        private string[] _badTags;
        private string _postKey;
        public Func<string, Uri> DetailUriBuilder = id =>
            new Uri($"https://www.pixiv.net/member_illust.php?mode=medium&illust_id={id}");

        private Settings _settings;
        public PixivClient(Settings settings)
        {
            _settings = settings;
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
            _badTags = LocalSetting.Instance["badtag", typeof(string[])];
            RefreshPostKey();
        }

        private void RefreshPostKey()
        {
            var html = HttpGetHtml(_pixiv);
            _postKey = Regex.Match(html, @"""pixiv.context.postKey"":""(?<tt>[0-9a-f]{32})""", RegexOptions.IgnoreCase)
                .Groups["tt"].Value;
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

        private void RunList(string[] ids)
        {
            var random = new Random();
            Console.WriteLine($"get {ids.Length}");
            foreach (var id in ids)
            {
                if (_disposing) break;
                //Thread.Sleep(random.Next(300, 1000));
                Console.Write($"processing: {id}");
                var info = GetIllustInfo(id, HttpGetHtml(DetailUriBuilder(id), "https://www.pixiv.net/discovery"));
                ProcessIllust(info);
                Console.Write("\r\n");
            }
        }

        private void ProcessIllust(IllustInfo info)
        {
            Console.Write($", bookmark: {info.IsBookmarked}:{info.BookmarkCount}, page: {info.pagecount}");
            var badtags = info.tags.Intersect(_badTags).ToList();
            if (_settings.EnableTagFilter && badtags.Any())
            {
                Console.Write($", skiped {string.Join(",", badtags)}");
            }
            else if (info.IsBookmarked)
            {
                Console.Write($", skiped bookmarked.");
            }
            else if (info.BookmarkCount >= _settings.MinCount && info.pagecount <= _settings.MaxIllustPageCount)
            {
                int restrict = 0;
                if (_settings.R18InPrivate && Regex.IsMatch(string.Join(",", info.tags), @"r(-)?18", RegexOptions.IgnoreCase))
                {
                    restrict = 1;
                }
                Console.Write($", restrict: {restrict}");
                var re = AddBookmark(info.Id, restrict);
                if (re)
                {
                    Console.Write(", succeed");
                }
                else
                {
                    Console.Write(", failed");
                }
            }
            else
            {
                Console.Write(", skiped");
            }
        }

        private Task Run(Action run)
        {
            return Task.Run(() =>
            {
                _running = true;
                run();
                _running = false;
            });
        }
        public Task RunSearchList(string searchWord)
        {
            return Run(() =>
            {
                int page = _settings.StartPage;
                Console.WriteLine($"start! start page: {page}");
                while (!_disposing)
                {
                    Console.WriteLine($"page: {page}");
                    var html = HttpGetReturnJson(
                        new Uri(
                            $"https://www.pixiv.net/touch/ajax_api/search_api.php?endpoint=search&mode=search_illust&word={WebUtility.UrlEncode(searchWord)}&order=&p={page}&type=&scd=&ecd=&circle_list=0&s_mode=s_tag&blt=&bgt=&adult_mode="),
                        $"https://www.pixiv.net/search.php?word={WebUtility.UrlEncode(searchWord)}");
                    var ids = Regex.Matches(html, @"""illust_id"":""(?<id>\d+)""")
                        .Select(x => x.Groups["id"].Value).ToArray();
                    RunList(ids);
                    if (ids.Length < 12) break;
                    page++;
                }

                Console.WriteLine("finished");
            });
        }

        private bool AddBookmark(string id, int restrict)
        {
            var json = HttpPostReturnJson(new Uri("https://www.pixiv.net/touch/ajax_api/ajax_api.php"),
                () => new StringContent(
                    $"mode=add_bookmark_illust&tt={_postKey}&id={id}&restrict={restrict}&tag=&comment=",
                    Encoding.UTF8, "application/x-www-form-urlencoded"),
                DetailUriBuilder(id).ToString());
            try
            {
                return JObject.Parse(json)["isSucceed"].ToObject<bool>();
            }
            catch (Exception e)
            {
                return false;
            }
        }

        private IllustInfo GetIllustInfo(string id, string html)
        {
            CQ cq = html;
            var illustInfo = new IllustInfo();
            illustInfo.Id = id;
            illustInfo.BookmarkCount = cq[".bookmark-count"].Eq(0).Text().ToInt();
            illustInfo.IsBookmarked = cq["span.button.btn-like.done"].Length > 0;
            var ids = Regex.Matches(html, @"<li id=""il(?<id>\d+)""").Cast<Match>().Select(x => x.Groups["id"].Value).Distinct();
            illustInfo.ids = ids.Where(d => d != id).ToArray();
            illustInfo.pagecount = cq[".img-box .page-count"].Text().ToIntNullable() ?? 1;
            illustInfo.tags = cq[".tag"].Selection.Select(x => x.TextContent.Replace("* ", "")).ToArray();
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
}
