// Copyright 2017 Louis S. Berman.  All rights reserved.

using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace ConnectFetch
{
    public class Video
    {
        public Uri Uri { get; set; }
        public string KnownAs { get; set; }
        public string Code { get; set; }
        public Quality Quality { get; set; }
        public string Extension { get; set; }

        public string GetFileName(string saveTo) => Path.Combine(saveTo, ToString());

        public bool Exists(string saveTo) => File.Exists(GetFileName(saveTo));

        public async Task DownloadAsync(string saveTo)
        {
            var client = new HttpClient();

            if (!Directory.Exists(saveTo))
                Directory.CreateDirectory(saveTo);

            using (var fileStream = File.Create(GetFileName(saveTo)))
            {
                var dataStream = await client.GetStreamAsync(Uri);

                await dataStream.CopyToAsync(fileStream);
            }
        }

        public override string ToString() => $"{Code}_{KnownAs}.{Extension}";
    }
}
