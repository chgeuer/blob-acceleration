namespace ParallelDownload
{
    using Azure.Identity;
    using Azure.Storage.Blobs; // https://github.com/Azure/azure-sdk-for-net/tree/main/sdk/storage/Azure.Storage.Blobs/src
    using Azure.Storage.Blobs.Models;
    using Azure.Storage.Blobs.Specialized;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;

    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length != 1) 
            {
                Console.Error.WriteLine("Please indicate number of concurrent downloads");
                return;
            }
            int parallelDownloads = int.Parse(args[0]);
            await Console.Out.WriteLineAsync($"Running {parallelDownloads} downloads in parallel");

            var (accountName, containerName) = ("chgeuerperf", "container1");
            var blobName = "1gb.randombin";

            var cred = new DefaultAzureCredential(includeInteractiveCredentials: true);
            BlobContainerClient containerClient = new(new Uri($"https://{accountName}.blob.core.windows.net/{containerName}"), cred);
            const long giga = (1 << 30);

            var blobClient =  new BlockBlobClient(blobUri: new Uri($"https://{accountName}.blob.core.windows.net/{containerName}/{blobName}"), credential: cred);
            //var props = await blobClient.GetPropertiesAsync();


            //var gigs = (int) (props.Value.ContentLength / giga);

            //await Console.Out.WriteLineAsync($"Length {props.Value.ContentLength} bytes, approx {gigs} GB");

            //var tasks = Enumerable
            //    .Range(start: 0, count: gigs)
            //    .Select(i => (long)i)
            //    .Select<long, Azure.HttpRange>(i => new(offset: i * giga, length: giga)) // https://docs.microsoft.com/en-us/rest/api/storageservices/specifying-the-range-header-for-blob-service-operations
            //    .Select(async range =>
            //    {
            //        try {
            //            await Console.Out.WriteLineAsync($"Downloading {range}");
            //            var response = await blobClient.DownloadStreamingAsync(range);
            //            var stream = response.Value.Content;
            //            await stream.CopyToAsync(System.IO.Stream.Null);
            //        }
            //        catch (Exception e)
            //        {
            //            await Console.Error.WriteLineAsync($"Exception {e.Message} {range}");
            //        }
            //    })
            //    .ToArray();

            var blockListResponse = await blobClient.GetBlockListAsync();
            var gigs = (int) (blockListResponse.Value.BlobContentLength / giga);
            await Console.Out.WriteLineAsync($"Length {blockListResponse.Value.BlobContentLength} bytes, approx {gigs} GB");

            int idx = 0;
            foreach (var blocks in blockListResponse.Value.CommittedBlocks.Blocks2().Chunk(batchSize: parallelDownloads))
            {
                var tasks = blocks
                    .Select(async (blockAndRange, _i) =>
                    {
                        (BlobBlock blobBlock, Azure.HttpRange range) = blockAndRange;
                        await Console.Out.WriteLineAsync($"{idx} {blobBlock.Name}: {blobBlock}");

                        try
                        {
                            Stopwatch stopWatch = new ();
                            stopWatch.Start();

                            var response = await blobClient.DownloadStreamingAsync(blockAndRange.Item2);
                            var stream = response.Value.Content;
                            await stream.CopyToAsync(System.IO.Stream.Null);

                            stopWatch.Stop();
                 
                            var ts = stopWatch.Elapsed;

                            const double f = 8.0 / (1024 * 1024); 

                            double megabitPerSecond = f * blobBlock.SizeLong / ts.TotalSeconds;

                            await Console.Out.WriteLineAsync($"Downloaded {blobBlock.Name}: {blobBlock.SizeLong} bytes in {ts} ({megabitPerSecond:F2} Mbit/sec)");
                        }
                        catch (Exception e)
                        {
                            await Console.Error.WriteLineAsync($"Exception {e.Message} {blockAndRange.Item2}");
                        }
                    })
                    .ToArray();

                await Task.WhenAll(tasks);

                idx++;
            }
        }
    }

    internal static class Utilities 
    {
        //public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> items, int maxItems)
        //{
        //    return items
        //        .Select((item, inx) => new { item, inx })
        //        .GroupBy(x => x.inx / maxItems)
        //        .Select(g => g.Select(x => x.item));
        //}

        private static IEnumerable<U> RollingAggregate<T, U, V>(
            this IEnumerable<T> ts, 
            V start, 
            Func<T, V, V> aggregate, 
            Func<T, V, U> map) 
        {
            V agg = start;
            foreach (T t in ts)
            {
                yield return map(t, agg);
                agg = aggregate(t, agg);
            }
        }

        internal static IEnumerable<(BlobBlock, Azure.HttpRange)> Blocks2(this IEnumerable<BlobBlock> collection)
            => collection.RollingAggregate(
                start: 0L, 
                aggregate: (bb,offset) => offset + bb.SizeLong, 
                map: (bb, offset) => (bb, new Azure.HttpRange(offset: offset, length: bb.SizeLong)));

        internal static IEnumerable<(BlobBlock, Azure.HttpRange)> Blocks(this IEnumerable<BlobBlock> collection)
        {
            long offset = 0L;
            foreach (var blockblob in collection)
            {
                yield return (blockblob, new Azure.HttpRange(offset: offset, length: blockblob.SizeLong));
                offset += blockblob.SizeLong;
            }
        }

        internal static IEnumerable<IEnumerable<T>> Chunk<T>(this IEnumerable<T> collection, int batchSize)
        {
            int total = 0;
            while (total < collection.Count())
            {
                yield return collection.Skip(total).Take(batchSize);
                total += batchSize;
            }
        }
    }
}
