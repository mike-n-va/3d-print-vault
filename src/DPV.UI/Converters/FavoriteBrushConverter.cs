using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace DPV.UI.Converters;

public sealed class FavoriteBrushConverter : IValueConverter
{
    private static readonly IBrush ActiveBrush = new SolidColorBrush(Color.Parse("#FF4757"));
    private static readonly IBrush InactiveBrush = new SolidColorBrush(Color.FromArgb(180, 200, 210, 225));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isFavorite = value is true;
        return isFavorite ? ActiveBrush : InactiveBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
