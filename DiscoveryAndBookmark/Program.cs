using System;
using System.IO;
using CommandLine;
using Microsoft.Extensions.Configuration;
using MyPixivUtils.Shared;

namespace MyPixivUtils.DiscoveryAndBookmark
{
    public class Program
    {
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<PixivClient.Settings>(args)
                .WithParsed(option =>
                {
                    option.StartPage = 1;
                    using (var pixivClient = new PixivClient(option))
                    {
                        pixivClient.RunSearchList("極上の女体 000");
                        Console.ReadKey();
                        Console.WriteLine("exiting...");
                    }
                });
        }

    }
}
