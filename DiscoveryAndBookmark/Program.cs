using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using MyPixivUtils.Shared;

namespace MyPixivUtils.DiscoveryAndBookmark
{
    public class Program
    {
        public static IConfiguration Configuration { get; set; }
        static void Main(string[] args)
        {
            BuildConfiguration();
            using (var pixivBookmarkTool = new PixivClient())
            {
                pixivBookmarkTool.RunSearchList("極上の女体");
                Console.ReadKey();
                Console.WriteLine("exiting...");
            }
        }

        static void BuildConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json");

            Configuration = builder.Build();
        }
    }
}
