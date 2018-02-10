using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace MyPixivUtils.DiscoveryAndBookmark
{
    public class Program
    {
        public static IConfiguration Configuration { get; set; }
        static void Main(string[] args)
        {
            BuildConfiguration();
            using (var pixivBookmarkTool = new PixivBookmarkTool())
            {
                pixivBookmarkTool.Start();
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
