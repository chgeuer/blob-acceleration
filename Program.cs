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

            foreach (var parallelDownloads in new[] { 1, 2, 3, 4, 5, 8, 12, 16, 20, 25, 30, 40, 50 }) 
            {
                await Bench(parallelDownloads);
            }
        }

        static async Task Bench(int parallelDownloads)
        {
            await Console.Out.WriteLineAsync($"Running {parallelDownloads} downloads in parallel");

            var (accountName, containerName) = ("chgeuerperf", "container1");
            var blobName = "1gb.randombin";

            Stopwatch outerStopWatch = new();
            outerStopWatch.Start();

            var cred = new DefaultAzureCredential(includeInteractiveCredentials: true);
            BlobContainerClient containerClient = new(new Uri($"https://{accountName}.blob.core.windows.net/{containerName}"), cred);
            const long giga = (1 << 30);

            var blobClient = new BlockBlobClient(blobUri: new Uri($"https://{accountName}.blob.core.windows.net/{containerName}/{blobName}"), credential: cred);
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
            var gigs = (int)(blockListResponse.Value.BlobContentLength / giga);
            await Console.Out.WriteLineAsync($"Length {blockListResponse.Value.BlobContentLength} bytes, approx {gigs} GB");

            static double MegabitPerSecond(long bytes, TimeSpan ts) => (8.0 / (1024 * 1024)) * bytes / ts.TotalSeconds;

            int idx = 0;
            foreach (var blocks in blockListResponse.Value.CommittedBlocks.Blocks2().Chunk(batchSize: parallelDownloads))
            {

                Stopwatch stopWatch = new();
                stopWatch.Start();

                var tasks = blocks
                    .Select(async (blockAndRange, _i) =>
                    {
                        (BlobBlock blobBlock, Azure.HttpRange range) = blockAndRange;

                        Stopwatch innerStopWatch = new();
                        innerStopWatch.Start();

                        var response = await blobClient.DownloadStreamingAsync(blockAndRange.Item2);
                        var stream = response.Value.Content;
                        await stream.CopyToAsync(System.IO.Stream.Null);

                        innerStopWatch.Stop();
                        var ts = innerStopWatch.Elapsed;

                        // await Console.Out.WriteLineAsync($"Downloaded {range} {blobBlock.SizeLong} bytes ({MegabitPerSecond(blobBlock.SizeLong, ts):F2} Mbit/sec)");

                        return blobBlock.SizeLong;
                    })
                    .ToArray();

                var downloads = await Task.WhenAll(tasks);

                stopWatch.Stop();
                var ts = stopWatch.Elapsed;

                await Console.Out.WriteLineAsync($"In {parallelDownloads} downloads, fetch {downloads.Sum()} bytes ({MegabitPerSecond(downloads.Sum(), ts):F2} Mbit/sec)");

                idx++;
            }

            outerStopWatch.Stop();
            var outerTime = outerStopWatch.Elapsed;

            await Console.Out.WriteLineAsync($"Overall time with {parallelDownloads} {outerTime.TotalSeconds} seconds ({MegabitPerSecond(blockListResponse.Value.BlobContentLength, outerTime):F2} Mbit/sec)");

            await Console.Out.WriteLineAsync($"-----------------------------------");
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
