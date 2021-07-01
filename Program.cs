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

    public record BlockInformation(int BlockNumber, BlobBlock BlobBlock, HttpRange Range);
    public record BlobInformation(string AccountName, string ContainerName, string BlobName);

    class Program
    {
        static async Task Main(string[] _args)
        {
            await BenchNumbers();
        }

        static async Task<BlobInformation> Config()
        {
            // based on the data center region, use a different storage account.
            string location = await PrintMetadataAndReturnLocation();
            var accountName = location switch
            {
                "westeurope" => "chgeuerperf",
                "northeurope" => "chgeuerperfne",
                _ => throw new NotSupportedException("Unsupported location"),

            };

            // dd if=/dev/zero bs=1G count=128 | azcopy copy "https://${storageAccountName}.blob.core.windows.net/${containerName}/${blobName}" --block-size-mb 1024 --from-to PipeBlob 

            return new (accountName, "container1", "1gb.randombin");
        }

        static async Task BenchNumbers()
        {
            var blob = await Config();

            await BenchPlainDownload(blob);

            foreach (var i in new[] { 1, 2, 3, 4, 5, 8, 12, 16, 20, 25, 30, 40, 50 })
            {
                await Bench(i, blob);
            }
        }

        // Just download without any consideration of block boundaries, parallelization, etc.
        static async Task BenchPlainDownload(BlobInformation blob) 
        {
            var blobClient = new BlockBlobClient(
                blobUri: new Uri($"https://{blob.AccountName}.blob.core.windows.net/{blob.ContainerName}/{blob.BlobName}"),
                credential: new DefaultAzureCredential(includeInteractiveCredentials: true));
            Stopwatch outerStopWatch = new();
            outerStopWatch.Start();

            var response = await blobClient.DownloadStreamingAsync();
            Stream stream = response.Value.Content;
            await stream.CopyToAsync(Stream.Null); // We're not really processing the data upon arrival. Just memcopy into the void...

            outerStopWatch.Stop();
            var outerTime = outerStopWatch.Elapsed;

            long lenght = (await blobClient.GetPropertiesAsync()).Value.ContentLength;

            await Console.Out.WriteLineAsync("----------------------------------------------------");
            await Console.Out.WriteLineAsync($"Regular end-to-end download {outerTime.TotalSeconds} seconds ({MegabitPerSecond(lenght, outerStopWatch.Elapsed):F2} Mbit/sec)");
            await Console.Out.WriteLineAsync("----------------------------------------------------");
        }

        static double MegabitPerSecond(long bytes, TimeSpan ts) => (8.0 / (1024 * 1024)) * bytes / ts.TotalSeconds;

        static async Task Bench(int parallelDownloads, BlobInformation blob)
        {
            const long giga = (1 << 30);

            var blobClient = new BlockBlobClient(
                blobUri: new Uri($"https://{blob.AccountName}.blob.core.windows.net/{blob.ContainerName}/{blob.BlobName}"),
                credential: new DefaultAzureCredential(includeInteractiveCredentials: true));

            await Console.Out.WriteLineAsync($"Running {parallelDownloads} downloads in parallel");

            Stopwatch outerStopWatch = new();
            outerStopWatch.Start();

            Response<BlockList> blockListResponse = await blobClient.GetBlockListAsync();
            int gigs = (int)(blockListResponse.Value.BlobContentLength / giga);
            await Console.Out.WriteLineAsync($"Length {blockListResponse.Value.BlobContentLength} bytes, approx {gigs} GB");

            IEnumerable<BlockInformation> listOfBlockLists = blockListResponse.Value.CommittedBlocks.Blocks().ToArray();

            Task<long>[] tasks = Enumerable
                .Range(start: 0, count: parallelDownloads)
                .Select(offset => listOfBlockLists.Where((blockId, i) => i % parallelDownloads == offset))
                .Select(async blocks => {
                    long downloaded = 0L;
                    foreach (var block in blocks) 
                    {
                        Stopwatch innerStopWatch = new();
                        innerStopWatch.Start();

                        var response = await blobClient.DownloadStreamingAsync(block.Range);
                        Stream stream = response.Value.Content;
                        await stream.CopyToAsync(Stream.Null); // We're not really processing the data upon arrival. Just memcopy into the void...
                        downloaded += block.BlobBlock.SizeLong;

                        innerStopWatch.Stop();
                        await Console.Out.WriteLineAsync($"Block {block.BlockNumber} took {innerStopWatch.Elapsed.TotalSeconds} seconds ({MegabitPerSecond(block.BlobBlock.SizeLong, innerStopWatch.Elapsed):F2} Mbit/sec)");
                    }
                    return downloaded;
                }).ToArray();


            long[] downloads = await Task.WhenAll(tasks);

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
        //private static IEnumerable<U> RollingAggregate<T, U, V>(
        //    this IEnumerable<T> ts,
        //    V start,
        //    Func<T, V, V> aggregate,
        //    Func<T, V, U> map)
        //{
        //    V agg = start;
        //    foreach (T t in ts)
        //    {
        //        yield return map(t, agg);
        //        agg = aggregate(t, agg);
        //    }
        //}

        //internal static IEnumerable<(BlobBlock, HttpRange)> GetBlocks(this IEnumerable<BlobBlock> collection)
        //    => collection.RollingAggregate(
        //        start: 0L,
        //        aggregate: (bb, offset) => offset + bb.SizeLong,
        //        map: (bb, offset) => (bb, new HttpRange(offset: offset, length: bb.SizeLong)));

        internal static IEnumerable<BlockInformation> Blocks(this IEnumerable<BlobBlock> blocks)
        {
            long offset = 0L;
            int i = 0;
            foreach (var block in blocks)
            {
                yield return new (i, block, new HttpRange(offset: offset, length: block.SizeLong));
                offset += block.SizeLong;
                i += 1;
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