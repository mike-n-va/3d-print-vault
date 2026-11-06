using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace DPV.UI.Converters;

public sealed class FavoriteIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isFavorite = value is true;
        return isFavorite ? "♥" : "♡";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
