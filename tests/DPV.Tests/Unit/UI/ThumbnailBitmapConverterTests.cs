using System;
using System.Globalization;
using DPV.UI.Converters;

namespace DPV.Tests.Unit.UI;

public class ThumbnailBitmapConverterTests
{
    [Fact]
    public void NonStringOrNonExistentPathReturnsNull()
    {
        var converter = new ThumbnailBitmapConverter();

        Assert.Null(converter.Convert(null, typeof(object), null, CultureInfo.InvariantCulture));
        Assert.Null(converter.Convert(12345, typeof(object), null, CultureInfo.InvariantCulture));
        Assert.Null(converter.Convert(@"C:\non_existent_folder_xyz\dummy.png", typeof(object), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ConvertBackThrowsNotSupportedException()
    {
        var converter = new ThumbnailBitmapConverter();

        Assert.Throws<NotSupportedException>(() =>
            converter.ConvertBack(null, typeof(object), null, CultureInfo.InvariantCulture));
    }
}
