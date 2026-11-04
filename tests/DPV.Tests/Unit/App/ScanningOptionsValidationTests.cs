using System.ComponentModel.DataAnnotations;
using DPV.App.Configuration;

namespace DPV.Tests.Unit.App;

public class ScanningOptionsValidationTests
{
    [Fact]
    public void DefaultOptions_HaveExpectedValues()
    {
        var options = new ScanningOptions();
        Assert.Equal(2L * 1024 * 1024 * 1024, options.MaxFileSizeBytes);
        Assert.Single(options.AllowedExtensions);
        Assert.Equal(".stl", options.AllowedExtensions[0]);
        Assert.Equal(500, options.BatchLimit);
        Assert.Equal(string.Empty, options.RootPath);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    [InlineData(50)] // Below the 84-byte minimum STL threshold
    public void MaxFileSizeBytes_RejectsInvalidSizes(long invalidSize)
    {
        var options = new ScanningOptions { MaxFileSizeBytes = invalidSize };
        Assert.True(options.MaxFileSizeBytes < 84);

        var results = Validate(options);

        Assert.Contains(results, result => result.ErrorMessage!.Contains("MaxFileSizeBytes", StringComparison.Ordinal));
    }

    [Fact]
    public void MaxFileSizeBytes_AcceptsMinimumStlThreshold()
    {
        var options = new ScanningOptions { MaxFileSizeBytes = 84 };

        Assert.Empty(Validate(options));
    }

    [Fact]
    public void RootPath_RejectsNullWhenActive()
    {
        var options = new ScanningOptions { RootPath = null! };

        var results = Validate(options);

        Assert.Contains(results, result => result.ErrorMessage!.Contains("RootPath", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("\t")]
    public void RootPath_RejectsWhitespaceWhenActive(string rootPath)
    {
        var options = new ScanningOptions { RootPath = rootPath };

        var results = Validate(options);

        Assert.Contains(results, result => result.ErrorMessage!.Contains("RootPath", StringComparison.Ordinal));
    }

    [Fact]
    public void RootPath_AllowsEmptyWhenInactive()
    {
        var options = new ScanningOptions { RootPath = string.Empty };

        Assert.Empty(Validate(options));
    }

    private static List<ValidationResult> Validate(ScanningOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }
}
