using DPV.Domain.Contracts.Catalog;
using DPV.Domain.Contracts.Import;
using DPV.Infrastructure.Workers;
using Microsoft.Extensions.Logging.Abstractions;

namespace DPV.Tests.Unit.Workers;

public class BackgroundScanQueueTests
{
    [Fact]
    public async Task EnqueueAndDequeue_PassesWorkItemCorrectly()
    {
        var token = TestContext.Current.CancellationToken;
        IBackgroundScanQueue queue = new BackgroundScanQueue(10);
        var workItem = new ScanWorkItem(@"C:\TestModels", ForceRescan: true);

        await queue.EnqueueAsync(workItem, token);
        var dequeuedItem = await queue.DequeueAsync(token);

        Assert.Equal(@"C:\TestModels", dequeuedItem.RootPath);
        Assert.True(dequeuedItem.ForceRescan);
    }

    [Fact]
    public async Task EnqueueAsync_CancelsWhenTheBoundedQueueIsFull()
    {
        var token = TestContext.Current.CancellationToken;
        var queue = new BackgroundScanQueue(1);
        await queue.EnqueueAsync(new ScanWorkItem(@"C:\one", false), token);
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(token);
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            queue.EnqueueAsync(new ScanWorkItem(@"C:\two", true), cancelled.Token).AsTask());
    }

    [Fact]
    public async Task Worker_ImportsTheDequeuedRootAndStops()
    {
        var token = TestContext.Current.CancellationToken;
        var queue = new BackgroundScanQueue(10);
        var import = new RecordingImport();
        var worker = new ScanBackgroundWorker(
            queue,
            import,
            NullLogger<ScanBackgroundWorker>.Instance,
            maxFileSizeBytes: 4096,
            batchLimit: 25);

        await worker.StartAsync(token);
        try
        {
            await queue.EnqueueAsync(new ScanWorkItem(@"C:\models", ForceRescan: true), token);
            var deadline = Environment.TickCount64 + 2000;
            while (import.Requests.Count == 0 && Environment.TickCount64 < deadline)
            {
                await Task.Delay(10, token);
            }
        }
        finally
        {
            await worker.StopAsync(token);
        }

        var request = Assert.Single(import.Requests);
        Assert.Equal(@"C:\models", request.RootPath);
        Assert.Equal(4096, request.MaxFileSizeBytes);
        Assert.Equal(25, request.BatchLimit);
    }

    private sealed class RecordingImport : IModelImportService
    {
        public List<ImportLibraryRequest> Requests { get; } = [];

        public Task<ImportLibraryResult> ImportAsync(
            ImportLibraryRequest request,
            CancellationToken cancellationToken = default,
            IProgress<ImportProgress>? progress = null)
        {
            Requests.Add(request);
            return Task.FromResult(new ImportLibraryResult(0, 0, 0, 0, 0, true, []));
        }
    }
}
