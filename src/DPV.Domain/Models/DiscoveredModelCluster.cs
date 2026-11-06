using System;
using System.Collections.Generic;
using DPV.Domain.Enums;

namespace DPV.Domain.Models;

public sealed record DiscoveredModelCluster(
    Guid Id,
    string DisplayName,
    string EnclosingDirectory,
    IReadOnlyList<DiscoveredAsset> Assets,
    double ConfidenceScore,
    ModelConfidenceTier ConfidenceTier,
    string? HeroPreviewPath,
    string? PrimaryMeshPath,
    string? InferredArtist,
    string? InferredCollection,
    IReadOnlyList<string> Tags,
    string? InferredRelease = null,
    string DiscoveryMethod = "Heuristic",
    IReadOnlyList<string>? DiscoveryEvidence = null);
