using System.ComponentModel.DataAnnotations;

namespace DPV.App.Configuration;

public sealed class ScanningOptions
{
    public const string SectionName = "Scanning";

    public const long MinimumMaxFileSizeBytes = 84;

    [ActiveRootPath]
    public string RootPath { get; set; } = string.Empty;

    [Range(
        typeof(long),
        "84",
        "9223372036854775807",
        ErrorMessage = "MaxFileSizeBytes must be greater than 0 and at least 84 bytes.")]
    public long MaxFileSizeBytes { get; set; } = 2L * 1024 * 1024 * 1024;

    public List<string> AllowedExtensions { get; set; } = new() { ".stl" };

    public int BatchLimit { get; set; } = 500;
}

/// <summary>
/// Empty <see cref="ScanningOptions.RootPath"/> means scanning is inactive.
/// A null or whitespace path is an active configuration and is rejected.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class ActiveRootPathAttribute : ValidationAttribute
{
    public ActiveRootPathAttribute()
        : base("RootPath must not be null or whitespace when scanning is active.")
    {
    }

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return false;
        }

        if (value is not string path)
        {
            return false;
        }

        return path.Length == 0 || !string.IsNullOrWhiteSpace(path);
    }
}
