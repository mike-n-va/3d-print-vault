using System.Threading.Channels;
using DPV.Domain.Contracts.Catalog;

namespace DPV.Infrastructure.Workers;

public sealed class BackgroundScanQueue : IBackgroundScanQueue
{
    private readonly Channel<ScanWorkItem> _queue;

    public BackgroundScanQueue(int capacity = 100)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _queue = Channel.CreateBounded<ScanWorkItem>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public async ValueTask EnqueueAsync(ScanWorkItem workItem, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        await _queue.Writer.WriteAsync(workItem, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask<ScanWorkItem> DequeueAsync(CancellationToken cancellationToken) =>
        _queue.Reader.ReadAsync(cancellationToken);
}
