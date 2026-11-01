namespace DPV.Domain.Entities;

internal static class EntityValidation
{
    public static string ValidateName(string name, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(name, parameterName);

        var trimmedName = name.Trim();
        if (trimmedName.Length == 0)
        {
            throw new ArgumentException("Name must contain at least one non-whitespace character.", parameterName);
        }

        return trimmedName;
    }
}
