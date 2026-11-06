using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DPV.Domain.Contracts.Catalog;

public interface IReleaseArtworkService
{
    Task<string?> ResolveReleaseCoverAsync(
        Guid releaseId,
        string releaseName,
        IEnumerable<string> candidateFilePaths,
        CancellationToken cancellationToken = default);
}
