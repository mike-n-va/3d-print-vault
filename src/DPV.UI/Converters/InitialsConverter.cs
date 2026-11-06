using System;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;

namespace DPV.UI.Converters;

public sealed class InitialsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string text || string.IsNullOrWhiteSpace(text))
        {
            return "?";
        }

        var parts = text.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            var word = parts[0].Trim();
            return word.Length <= 2 ? word.ToUpperInvariant() : word.Substring(0, 2).ToUpperInvariant();
        }

        var first = parts[0].Length > 0 ? parts[0][0].ToString() : "";
        var last = parts[^1].Length > 0 ? parts[^1][0].ToString() : "";
        var initials = (first + last).ToUpperInvariant();
        return string.IsNullOrEmpty(initials) ? "?" : initials;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
