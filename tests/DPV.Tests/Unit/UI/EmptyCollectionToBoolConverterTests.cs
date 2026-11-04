using System;
using System.Collections.Generic;
using System.Globalization;
using DPV.UI.Converters;

namespace DPV.Tests.Unit.UI;

public class EmptyCollectionToBoolConverterTests
{
    [Fact]
    public void NullReturnsTrueByDefaultAndFalseWhenInverted()
    {
        var converter = new EmptyCollectionToBoolConverter();

        Assert.Equal(true, converter.Convert(null, typeof(bool), null, CultureInfo.InvariantCulture));
        Assert.Equal(false, converter.Convert(null, typeof(bool), "invert", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void IntZeroReturnsTrueByDefaultAndFalseWhenInverted()
    {
        var converter = new EmptyCollectionToBoolConverter();

        Assert.Equal(true, converter.Convert(0, typeof(bool), null, CultureInfo.InvariantCulture));
        Assert.Equal(false, converter.Convert(0, typeof(bool), "invert", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void PositiveIntReturnsFalseByDefaultAndTrueWhenInverted()
    {
        var converter = new EmptyCollectionToBoolConverter();

        Assert.Equal(false, converter.Convert(4, typeof(bool), null, CultureInfo.InvariantCulture));
        Assert.Equal(true, converter.Convert(4, typeof(bool), "invert", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void CollectionEvaluatesCountCorrectly()
    {
        var converter = new EmptyCollectionToBoolConverter();
        var emptyList = new List<string>();
        var populatedList = new List<string> { "item1" };

        Assert.Equal(true, converter.Convert(emptyList, typeof(bool), null, CultureInfo.InvariantCulture));
        Assert.Equal(false, converter.Convert(emptyList, typeof(bool), "invert", CultureInfo.InvariantCulture));

        Assert.Equal(false, converter.Convert(populatedList, typeof(bool), null, CultureInfo.InvariantCulture));
        Assert.Equal(true, converter.Convert(populatedList, typeof(bool), "invert", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ConvertBackThrowsNotSupportedException()
    {
        var converter = new EmptyCollectionToBoolConverter();

        Assert.Throws<NotSupportedException>(() =>
            converter.ConvertBack(true, typeof(object), null, CultureInfo.InvariantCulture));
    }
}
