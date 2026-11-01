using System.ComponentModel.DataAnnotations;

namespace DPV.App.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required]
    [MinLength(1)]
    public string Location { get; set; } = string.Empty;
}
