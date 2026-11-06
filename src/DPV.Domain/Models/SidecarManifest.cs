using System;
using System.Collections.Generic;
using DPV.Domain.Enums;

namespace DPV.Domain.Models;

public sealed class SidecarManifest
{
    public int SchemaVersion { get; set; } = 1;
    public Guid? ModelId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Artist { get; set; }
    public string? Collection { get; set; }
    public string? Release { get; set; }
    public string? Scale { get; set; }
    public string? PrimaryAsset { get; set; }
    public string? HeroThumbnail { get; set; }
    public List<SidecarAssetEntry> Assets { get; set; } = [];
    public List<string> Tags { get; set; } = [];
}

public sealed class SidecarAssetEntry
{
    public string Path { get; set; } = string.Empty;
    public FileRole Role { get; set; } = FileRole.PrimaryGeometry;
}
