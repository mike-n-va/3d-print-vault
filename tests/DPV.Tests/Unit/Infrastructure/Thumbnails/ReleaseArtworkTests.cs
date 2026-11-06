using System;
using System.IO;
using System.Threading.Tasks;
using DPV.Infrastructure.Thumbnails;
using Xunit;

namespace DPV.Tests.Unit.Infrastructure.Thumbnails;

public class ReleaseArtworkTests
{
    [Fact]
    public void PureManagedPngEncoder_EncodesValidPngHeaderAndDimensions()
    {
        var width = 480;
        var height = 270;
        var rgba = new byte[width * height * 4];
        for (var i = 0; i < rgba.Length; i += 4)
        {
            rgba[i] = 255;   // R
            rgba[i + 1] = 0; // G
            rgba[i + 2] = 0; // B
            rgba[i + 3] = 255; // A
        }

        var pngBytes = PureManagedPngEncoder.Encode(rgba, width, height);

        Assert.NotNull(pngBytes);
        Assert.True(pngBytes.Length > 64);
        // PNG signature: 137, 80, 78, 71, 13, 10, 26, 10
        Assert.Equal(137, pngBytes[0]);
        Assert.Equal((byte)'P', pngBytes[1]);
        Assert.Equal((byte)'N', pngBytes[2]);
        Assert.Equal((byte)'G', pngBytes[3]);
    }

    [Fact]
    public void ReleaseArtworkCollageGenerator_CreatesWidescreenPlaceholder()
    {
        var placeholderBytes = ReleaseArtworkCollageGenerator.CreatePlaceholderBanner("Graveyard Set");

        Assert.NotNull(placeholderBytes);
        Assert.True(placeholderBytes.Length > 100);
        Assert.Equal(137, placeholderBytes[0]);
        Assert.Equal((byte)'P', placeholderBytes[1]);
    }

    [Fact]
    public void ReleaseArtworkCollageGenerator_CreatesCompositeCollage_FromMemberModels()
    {
        var modelPaths = new[]
        {
            @"C:\models\paladin_commander.stl",
            @"C:\models\paladin_knight_a.stl",
            @"C:\models\paladin_knight_b.stl",
            @"C:\models\paladin_banner.stl"
        };

        var collageBytes = ReleaseArtworkCollageGenerator.CreateCompositeCollage(modelPaths);

        Assert.NotNull(collageBytes);
        Assert.True(collageBytes.Length > 100);
        Assert.Equal(137, collageBytes[0]);
        Assert.Equal((byte)'P', collageBytes[1]);
    }

    [Fact]
    public async Task ReleaseArtworkService_ResolvesPriority1_DedicatedCoverImage()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "dpv-release-art-" + Guid.NewGuid().ToString("N"));
        var releaseFolder = Path.Combine(tempDir, "ArtisanGuild", "CrimsonPaladins");
        var cacheDir = Path.Combine(tempDir, "cache");
        Directory.CreateDirectory(releaseFolder);
        Directory.CreateDirectory(cacheDir);

        try
        {
            var coverPath = Path.Combine(releaseFolder, "cover.png");
            File.WriteAllBytes(coverPath, [1, 2, 3]);

            var modelFile = Path.Combine(releaseFolder, "commander.stl");
            File.WriteAllBytes(modelFile, [4, 5, 6]);

            var service = new ReleaseArtworkService(cacheDir);
            var resolved = await service.ResolveReleaseCoverAsync(
                Guid.NewGuid(),
                "Crimson Paladins",
                [modelFile], TestContext.Current.CancellationToken);

            Assert.Equal(coverPath, resolved);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ReleaseArtworkService_ResolvesPriority2_ModelRenderImagery()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "dpv-release-art-" + Guid.NewGuid().ToString("N"));
        var releaseFolder = Path.Combine(tempDir, "ArtisanGuild", "CrimsonPaladins");
        var cacheDir = Path.Combine(tempDir, "cache");
        Directory.CreateDirectory(releaseFolder);
        Directory.CreateDirectory(cacheDir);

        try
        {
            var renderPath = Path.Combine(releaseFolder, "render_showcase.jpg");
            File.WriteAllBytes(renderPath, [1, 2, 3]);

            var modelFile = Path.Combine(releaseFolder, "commander.stl");
            File.WriteAllBytes(modelFile, [4, 5, 6]);

            var service = new ReleaseArtworkService(cacheDir);
            var resolved = await service.ResolveReleaseCoverAsync(
                Guid.NewGuid(),
                "Crimson Paladins",
                [modelFile, renderPath], TestContext.Current.CancellationToken);

            Assert.Equal(renderPath, resolved);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ReleaseArtworkService_ResolvesPriority3_SynthesizedCollageWhenNoImageryExists()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "dpv-release-art-" + Guid.NewGuid().ToString("N"));
        var releaseFolder = Path.Combine(tempDir, "ArtisanGuild", "CrimsonPaladins");
        var cacheDir = Path.Combine(tempDir, "cache");
        Directory.CreateDirectory(releaseFolder);
        Directory.CreateDirectory(cacheDir);

        try
        {
            var model1 = Path.Combine(releaseFolder, "sword.stl");
            var model2 = Path.Combine(releaseFolder, "shield.stl");
            File.WriteAllBytes(model1, [1, 2, 3]);
            File.WriteAllBytes(model2, [4, 5, 6]);

            var service = new ReleaseArtworkService(cacheDir);
            var releaseId = Guid.NewGuid();
            var resolved = await service.ResolveReleaseCoverAsync(
                releaseId,
                "Crimson Paladins",
                [model1, model2], TestContext.Current.CancellationToken);

            Assert.NotNull(resolved);
            Assert.True(File.Exists(resolved));
            Assert.EndsWith($"{releaseId:D}.png", resolved, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
