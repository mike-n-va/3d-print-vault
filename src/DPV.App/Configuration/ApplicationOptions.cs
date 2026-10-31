using System.ComponentModel.DataAnnotations;

namespace DPV.App.Configuration;

public sealed class ApplicationOptions
{
    public const string SectionName = "Application";

    [Required]
    [MinLength(1)]
    public string ApplicationName { get; set; } = string.Empty;
}
