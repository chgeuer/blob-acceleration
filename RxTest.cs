namespace ParallelDownload
{
    class RxTest
    {
        //static async Task DemoInterleaving()
        //{
        //    MemoryStream ms = new();
        //    ms.Write(new byte[] { 1, 2, 3, 4, 5, 6, 1 });
        //    ms.Seek(0L, SeekOrigin.Begin);

        //    List<MemoryStream> result = new();

        //    Stream createDestinationStream(uint id)
        //    {
        //        result[(int)id] = new MemoryStream();
        //        return result[(int)id];
        //    }

        //    Func<IEnumerable<uint>, Task> commitDestination = async ids => { await Task.Delay(0); };

        //    await InterleaveBlob(
        //        sourceStream: ms, sourceStreamLength: ms.Length,
        //        createDestinationStream: createDestinationStream,
        //        numberOfBlocksToInterleave: 2,
        //        numberOfBytes: 2,
        //        blockSize: 3,
        //        commitDestination: commitDestination.Invoke);
        //}

        //static async Task InterleaveBlob(
        //    Stream sourceStream, long sourceStreamLength,
        //    Func<uint, Stream> createDestinationStream,
        //    uint numberOfBlocksToInterleave, uint numberOfBytes, uint blockSize,
        //    Func<IEnumerable<uint>, Task> commitDestination,
        //    CancellationToken ct = default)
        //{
        //    var o = sourceStream.ToObservable(blockSize);
        //    o.Subscribe(
        //        onNext: mem => Console.WriteLine("Number of bytes read={0}, buffer should be populated with data now.", mem.Length),
        //        onError: ex => Console.Error.WriteLine(ex.Message),
        //        onCompleted: () => Console.WriteLine("Done"),
        //        token: ct
        //    );
        //    o.Subscribe(
        //        onNext: mem => Console.WriteLine("Number of bytes read={0}, buffer should be populated with data now.", mem.Length),
        //        onError: ex => Console.Error.WriteLine(ex.Message),
        //        onCompleted: () => Console.WriteLine("Done"),
        //        token: ct
        //    );
        //    await Task.Delay(10000);
        //}

        //internal static IObservable<Memory<byte>> ToObservable(this Stream stream, uint length)
        //{
        //    return Observable.Create<Memory<byte>>((observer, cancellationToken) => 
        //    {
        //        return Task.Run(async () =>
        //        {
        //            int count = 0;
        //            do
        //            {
        //                byte[] buffer = new byte[length];
        //                count = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
        //                if (count > 0)
        //                {
        //                    observer.OnNext(new Memory<byte>(buffer, 0, count));
        //                }
        //            } while (count > 0);
        //            observer.OnCompleted();
        //        }, cancellationToken);
        //    });
        //}
    }
}
