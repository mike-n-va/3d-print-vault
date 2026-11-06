using System;
using DPV.Domain.Enums;

namespace DPV.Domain.Models;

public sealed record DiscoveredAsset(
    string FilePath,
    string NormalizedPath,
    string FileName,
    string FileExtension,
    long FileSizeBytes,
    DateTime ModifiedAtUtc,
    string ContentHash,
    AssetCategory Category,
    FileRole Role,
    bool IsHero = false);
