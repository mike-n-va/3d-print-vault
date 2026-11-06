using System;
using System.IO;
using DPV.Domain.Enums;
using DPV.Domain.Models;
using DPV.Infrastructure.Import.Discovery;

namespace DPV.Tests.Unit.Infrastructure.Discovery;

public class SidecarReaderTests
{
    [Fact]
    public void TryReadSidecar_ParsesValidManifest()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, """
            {
                "schemaVersion": 1,
                "name": "Ancient Red Dragon",
                "artist": "Artisan Guild",
                "collection": "Dragons of the Red Peak",
                "scale": "32mm",
                "heroThumbnail": "render.png",
                "primaryAsset": "body.stl",
                "tags": ["dragon", "monster", "boss"]
            }
            """);

            var success = SidecarReader.TryReadSidecar(tempFile, out var manifest);

            Assert.True(success);
            Assert.NotNull(manifest);
            Assert.Equal("Ancient Red Dragon", manifest.Name);
            Assert.Equal("Artisan Guild", manifest.Artist);
            Assert.Equal("Dragons of the Red Peak", manifest.Collection);
            Assert.Equal("32mm", manifest.Scale);
            Assert.Equal("render.png", manifest.HeroThumbnail);
            Assert.Equal("body.stl", manifest.PrimaryAsset);
            Assert.Equal(3, manifest.Tags.Count);
            Assert.Contains("dragon", manifest.Tags);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void TryReadSidecar_ReturnsFalseForNonExistentFile()
    {
        var success = SidecarReader.TryReadSidecar(@"C:\nonexistent\path\file.dpv.json", out var manifest);

        Assert.False(success);
        Assert.Null(manifest);
    }

    [Fact]
    public void TryReadSidecar_ReturnsFalseForMalformedJson()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "{ broken json content");

            var success = SidecarReader.TryReadSidecar(tempFile, out var manifest);

            Assert.False(success);
            Assert.Null(manifest);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
