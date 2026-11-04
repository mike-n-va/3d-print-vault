namespace DPV.Domain.Contracts.Catalog;

public sealed record ScanWorkItem(string RootPath, bool ForceRescan);

public interface IBackgroundScanQueue
{
    ValueTask EnqueueAsync(ScanWorkItem workItem, CancellationToken cancellationToken = default);

    ValueTask<ScanWorkItem> DequeueAsync(CancellationToken cancellationToken);
}
