using DPV.Domain.Entities;

namespace DPV.Domain.Models;

public static class ModelMetadataAssignment
{
    public static string? NormalizeOptionalName(string? name, string parameterName)
    {
        if (name is null || name.Length == 0 || string.IsNullOrWhiteSpace(name))
        {
            if (name is not null && name.Length > 0 && string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "Name must contain at least one non-whitespace character.",
                    parameterName);
            }

            return null;
        }

        return EntityValidation.ValidateName(name, parameterName);
    }

    public static IReadOnlyList<string> NormalizeTags(IEnumerable<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            if (tag is null || tag.Length == 0)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(tag))
            {
                throw new ArgumentException(
                    "Name must contain at least one non-whitespace character.",
                    nameof(tags));
            }

            var name = EntityValidation.ValidateName(tag, nameof(tags));
            if (seen.Add(name))
            {
                normalized.Add(name);
            }
        }

        return normalized;
    }
}
