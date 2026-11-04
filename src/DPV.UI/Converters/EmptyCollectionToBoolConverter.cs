using System;
using System.Collections;
using System.Globalization;
using Avalonia.Data.Converters;

namespace DPV.UI.Converters;

public sealed class EmptyCollectionToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var invert = parameter is string s && s.Equals("invert", StringComparison.OrdinalIgnoreCase);

        var isEmpty = value switch
        {
            null => true,
            int count => count == 0,
            ICollection col => col.Count == 0,
            _ => false
        };

        return invert ? !isEmpty : isEmpty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
