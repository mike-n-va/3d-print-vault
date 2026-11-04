using System.ComponentModel.DataAnnotations;

namespace DPV.App.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required]
    [MinLength(1)]
    public const string DefaultLocation = "%AppData%/3DPrintVault/dpv.db";

    public string Location { get; set; } = DefaultLocation;
}
