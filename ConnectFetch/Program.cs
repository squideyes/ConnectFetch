// Copyright 2017 Louis S. Berman.  All rights reserved.

using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace ConnectFetch
{
    class Program
    {
        private static Uri BaseUri = new Uri("https://channel9.msdn.com");

        static async Task Main(string[] args)
        {
            try
            {
                var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("Settings.json");

                var config = builder.Build();

                await DoWork(config);
            }
            catch (Exception error)
            {
                Console.WriteLine("Error: " + error.Message);
            }

            Console.WriteLine();
            Console.Write("Press any key to terminate...");

            Console.ReadKey(true);
        }

        private static async Task DoWork(IConfigurationRoot config)
        {
            var jobs = await GetJobsAsync();

            var fetcher = new ActionBlock<Job>(
                async job =>
                {
                    var (exists, video) = await GetVideoAsync(job);

                    var saveTo = config["SaveTo"];

                    if (!exists)
                    {
                        Console.WriteLine($"NO-VIDEOS: {job}");
                    }
                    else if (video.Exists(saveTo))
                    {
                        Console.WriteLine($"SKIPPED: {video}");
                    }
                    else
                    {
                        await video.DownloadAsync(saveTo);

                        Console.WriteLine($"FETCHED: {video}");
                    }
                },
                new ExecutionDataflowBlockOptions()
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                });

            jobs.ForEach(job => fetcher.Post(job));

            fetcher.Complete();

            await fetcher.Completion;
        }

        private static async Task<(bool, Video)> GetVideoAsync(Job job)
        {
            var client = new HttpClient();

            var uri = new Uri(BaseUri, job.Href);

            var doc = new HtmlDocument();

            var html = await client.GetStringAsync(uri);

            doc.LoadHtml(html);

            var q1 = (from a in doc.DocumentNode.SelectNodes("//a[@download]")
                      select a).ToList();

            if (q1.Count == 0)
                return (false, null);

            var q2 = (from a in q1
                      where a.Attributes["download"].Value.EndsWith(".mp4")
                      select new
                      {
                          Href = a.Attributes["href"].Value,
                          KnownAs = Cleanup(job.Name),
                          Download = a.Attributes["download"].Value
                      }).ToList();

            if (q2.Count == 0)
                return (false, null);

            var videos = from v in q2
                         select new Video
                         {
                             Uri = new Uri(v.Href),
                             KnownAs = v.KnownAs,
                             Code = v.Download.Split('_')[0],
                             Quality = (Quality)Enum.Parse(typeof(Quality),
                                Path.GetFileNameWithoutExtension(v.Download).Split('_')[1], true),
                             Extension = Path.GetExtension(v.Download).Substring(1)
                         };

            return (true, videos.OrderByDescending(l => l.Quality).First());
        }

        private static string Cleanup(string value) =>
            Regex.Replace(value, @"[-,/\s:]", "_").Replace("__", "_");

        private static async Task<List<Job>> GetJobsAsync()
        {
            const string BASEURL =
                "https://channel9.msdn.com/Events/Connect/2017?sort=status&direction=desc&d=System.Int32%5B%5D&y%5B0%5D=On-demand&page={0}";

            var jobs = new List<Job>();

            var client = new HttpClient();

            int count = 0;

            while (true)
            {
                var uri = string.Format(BASEURL, ++count);

                var response = await client.GetAsync(uri);

                if (response.StatusCode != HttpStatusCode.OK)
                    throw new WebException($"The \"{uri}\" URI is invalid!");

                var doc = new HtmlDocument();

                doc.LoadHtml(await response.Content.ReadAsStringAsync());

                if (doc.DocumentNode.SelectNodes("//h2")
                    .Where(n => n.InnerText == "No Results Found").Any())
                {
                    break;
                }

                jobs.AddRange(from a in doc.DocumentNode.SelectNodes("//article/header/h3/a")
                              select new Job
                              {
                                  Href = a.Attributes["href"].Value,
                                  Name = WebUtility.HtmlDecode(a.InnerText)
                              });
            }

            return jobs;
        }
    }
}
