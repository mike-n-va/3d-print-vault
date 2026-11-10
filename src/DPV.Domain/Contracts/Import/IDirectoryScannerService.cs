using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DPV.Domain.Contracts.Import;

public sealed record ScanProgressReport(
    int DiscoveredArtists,
    int DiscoveredReleases,
    int DiscoveredModels,
    int DiscoveredFiles,
    string CurrentPath,
    double Percentage = 0);

public sealed record ScanResult(
    int TotalArtists,
    int TotalReleases,
    int TotalModels,
    int TotalFiles,
    TimeSpan Elapsed,
    bool IsIncremental,
    IReadOnlyList<string> Errors,
    int RetiredFiles = 0,
    int RetiredModels = 0,
    int RetiredReleases = 0,
    int RetiredArtists = 0)
{
    public bool Success => Errors.Count == 0;
}

public interface IDirectoryScannerService
{
    Task<ScanResult> BulkScanAsync(
        string rootPath,
        IProgress<ScanProgressReport>? progress = null,
        CancellationToken cancellationToken = default);

    Task<ScanResult> IncrementalScanAsync(
        string rootPath,
        IProgress<ScanProgressReport>? progress = null,
        CancellationToken cancellationToken = default);
}
