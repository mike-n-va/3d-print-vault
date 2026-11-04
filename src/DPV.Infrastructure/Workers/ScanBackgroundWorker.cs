using DPV.Domain.Contracts.Catalog;
using DPV.Domain.Contracts.Import;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DPV.Infrastructure.Workers;

public sealed class ScanBackgroundWorker(
    IBackgroundScanQueue taskQueue,
    IModelImportService importService,
    ILogger<ScanBackgroundWorker> logger,
    long maxFileSizeBytes = 2L * 1024 * 1024 * 1024,
    int batchLimit = 500) : IHostedService
{
    private readonly CancellationTokenSource _stop = new();
    private Task? _executing;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _executing = RunAsync(cancellationToken);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        if (_executing is null)
        {
            return;
        }

        await _executing.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RunAsync(CancellationToken hostStoppingToken)
    {
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(hostStoppingToken, _stop.Token);
        var stoppingToken = stopping.Token;
        logger.LogInformation("Background scan worker running.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var workItem = await taskQueue.DequeueAsync(stoppingToken).ConfigureAwait(false);
                logger.LogInformation(
                    "Starting background import for {Path}. ForceRescan={ForceRescan}",
                    workItem.RootPath,
                    workItem.ForceRescan);
                var result = await importService.ImportAsync(
                    new ImportLibraryRequest(workItem.RootPath, maxFileSizeBytes, batchLimit),
                    stoppingToken).ConfigureAwait(false);
                logger.LogInformation(
                    "Completed background import for {Path}. Added={Added}, Updated={Updated}, Errors={Errors}",
                    workItem.RootPath,
                    result.TotalAdded,
                    result.TotalUpdated,
                    result.Errors.Count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Error occurred executing background scan work item.");
            }
        }
    }
}
