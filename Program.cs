using System;

namespace ParallelDownload
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;

    using Azure;
    using Azure.Identity;
    // using Azure.Storage.Blobs; // https://github.com/Azure/azure-sdk-for-net/tree/main/sdk/storage/Azure.Storage.Blobs/src
    using Azure.Storage.Blobs.Models;
    using Azure.Storage.Blobs.Specialized;
    using Newtonsoft.Json;

    class Program
    {
        static async Task Main(string[] _args)
        {
           
            await BenchNumbers();
        }

        static async Task BenchNumbers()
        {
            string location = await PrintMetadataAndReturnLocation();
            foreach (var i in new[] { 1, 2, 3, 4, 5, 8, 12, 16, 20, 25, 30, 40, 50 })
            {
                await Bench(i, location);
            }
        }

        static async Task Bench(int parallelDownloads, string location)
        {
            // based on the data center region, use a different storage account.
            var accountName = location switch
            {
                "westeurope" => "chgeuerperf",
                "northeurope" => "chgeuerperfne",
                _ => throw new NotSupportedException("Unsupported location"),
            };

            // storageAccountName="chgeuerperfne"
            // containerName = "container1"
            // blobName="1gb.randombin"
            //
            // dd if=/dev/zero bs=1G count=128 | azcopy copy "https://${storageAccountName}.blob.core.windows.net/${containerName}/${blobName}" --block-size-mb 1024 --from-to PipeBlob 
            // 
            var (containerName, blobName) = ("container1", "1gb.randombin");

            const long giga = (1 << 30);
            static double MegabitPerSecond(long bytes, TimeSpan ts) => (8.0 / (1024 * 1024)) * bytes / ts.TotalSeconds;

            var blobClient = new BlockBlobClient(
                blobUri: new Uri($"https://{accountName}.blob.core.windows.net/{containerName}/{blobName}"),
                credential: new DefaultAzureCredential(includeInteractiveCredentials: true));

            await Console.Out.WriteLineAsync($"Running {parallelDownloads} downloads in parallel");

            Stopwatch outerStopWatch = new();
            outerStopWatch.Start();

            var blockListResponse = await blobClient.GetBlockListAsync();
            var gigs = (int)(blockListResponse.Value.BlobContentLength / giga);
            await Console.Out.WriteLineAsync($"Length {blockListResponse.Value.BlobContentLength} bytes, approx {gigs} GB");

            foreach (var blocks in blockListResponse.Value.CommittedBlocks.GetBlocks().Chunk(batchSize: parallelDownloads))
            {
                Stopwatch stopWatch = new();
                stopWatch.Start();

                var tasks = blocks
                    .Select(async (blockAndRange, _i) =>
                    {
                        (BlobBlock blobBlock, HttpRange range) = blockAndRange;

                        // https://docs.microsoft.com/en-us/rest/api/storageservices/specifying-the-range-header-for-blob-service-operations
                        var response = await blobClient.DownloadStreamingAsync(range);
                        var stream = response.Value.Content;
                        await stream.CopyToAsync(Stream.Null); // We're not really processing the data upon arrival. Just memcopy into the void...

                        return blobBlock.SizeLong;
                    })
                    .ToArray();

                var downloads = await Task.WhenAll(tasks);

                stopWatch.Stop();
                var ts = stopWatch.Elapsed;

                await Console.Out.WriteLineAsync($"In {parallelDownloads} downloads, fetch {downloads.Sum()} bytes ({MegabitPerSecond(downloads.Sum(), ts):F2} Mbit/sec)");
            }

            outerStopWatch.Stop();
            var outerTime = outerStopWatch.Elapsed;

            await Console.Out.WriteLineAsync($"Overall time with {parallelDownloads} {outerTime.TotalSeconds} seconds ({MegabitPerSecond(blockListResponse.Value.BlobContentLength, outerTime):F2} Mbit/sec)");

            await Console.Out.WriteLineAsync($"-----------------------------------");
        }

        static async Task<string> PrintMetadataAndReturnLocation()
        {
            static async Task<dynamic> GetIMDS()
            {
                var request = new HttpRequestMessage(HttpMethod.Get,
                    requestUri: "http://169.254.169.254/metadata/instance?api-version=2021-02-01");
                request.Headers.Add("Metadata", "true");
                var response = await new HttpClient().SendAsync(request);
                var responseString = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<dynamic>(responseString);
            }

            dynamic s = await GetIMDS();
            Console.Out.WriteLine($"--------------------------------------------------------------");
            await Console.Out.WriteLineAsync($".compute.vmSize     = {s.compute.vmSize}");
            await Console.Out.WriteLineAsync($".compute.location   = {s.compute.location}");
            await Console.Out.WriteLineAsync($".compute.zone       = {s.compute.zone}");
            await Console.Out.WriteLineAsync($".compute.resourceId = {s.compute.resourceId}");
            await Console.Out.WriteLineAsync($"--------------------------------------------------------------");
            return s.compute.location;
        }
    }

    internal static class Utilities
    {
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

        internal static IEnumerable<(BlobBlock, HttpRange)> GetBlocks(this IEnumerable<BlobBlock> collection)
            => collection.RollingAggregate(
                start: 0L,
                aggregate: (bb, offset) => offset + bb.SizeLong,
                map: (bb, offset) => (bb, new HttpRange(offset: offset, length: bb.SizeLong)));

        internal static IEnumerable<(BlobBlock, HttpRange)> Blocks(this IEnumerable<BlobBlock> collection)
        {
            long offset = 0L;
            foreach (var blockblob in collection)
            {
                yield return (blockblob, new HttpRange(offset: offset, length: blockblob.SizeLong));
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