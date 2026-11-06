using System.Collections.Generic;
using DPV.Domain.Models;

namespace DPV.Domain.Contracts.Import;

public interface IModelDiscoveryEngine
{
    IReadOnlyList<DiscoveredModelCluster> DiscoverModels(
        string scanRoot,
        IReadOnlyList<DiscoveredAsset> assets);
}
