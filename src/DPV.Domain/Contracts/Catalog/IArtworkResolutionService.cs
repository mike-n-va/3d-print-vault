using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DPV.Domain.Contracts.Catalog;

public enum ArtworkTier
{
    Tier1ExplicitCover = 1,
    Tier2Render = 2,
    Tier3Collage = 3,
    Tier4GeometricFallback = 4
}

public sealed record ArtworkResolutionResult(
    string? ResolvedPath,
    ArtworkTier Tier,
    bool IsSynthesized = false);

public interface IArtworkResolutionService
{
    Task<ArtworkResolutionResult> ResolveModelArtworkAsync(
        Guid modelId,
        string modelDirectoryPath,
        IEnumerable<string>? candidateFilePaths = null,
        CancellationToken cancellationToken = default);

    Task<ArtworkResolutionResult> ResolveReleaseArtworkAsync(
        Guid releaseId,
        string releaseDirectoryPath,
        IEnumerable<string>? candidateFilePaths = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, string>> ResolveAllUnresolvedModelsAsync(
        string? scanRoot = null,
        CancellationToken cancellationToken = default);
}

public interface IProceduralCollageSynthesizer
{
    Task<string?> SynthesizeCollageAsync(
        IEnumerable<string> sourceImagePaths,
        string outputCacheFilePath,
        int canvasSize = 1000,
        int separatorThickness = 2,
        CancellationToken cancellationToken = default);
}
