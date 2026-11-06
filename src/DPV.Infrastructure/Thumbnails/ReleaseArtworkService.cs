using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DPV.Domain.Contracts.Catalog;

namespace DPV.Infrastructure.Thumbnails;

public sealed class ReleaseArtworkService : IReleaseArtworkService
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp"
    };

    private static readonly string[] DedicatedNames = ["cover", "banner", "hero", "promo"];
    private static readonly string[] ModelImageryTokens = ["render", "thumbnail", "preview", "showcase", "painted"];

    private readonly string _cacheDirectory;

    public ReleaseArtworkService(string? cacheDirectory = null)
    {
        _cacheDirectory = string.IsNullOrWhiteSpace(cacheDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DPV",
                "Cache",
                "Releases")
            : cacheDirectory;

        Directory.CreateDirectory(_cacheDirectory);
    }

    public async Task<string?> ResolveReleaseCoverAsync(
        Guid releaseId,
        string releaseName,
        IEnumerable<string> candidateFilePaths,
        CancellationToken cancellationToken = default)
    {
        var fileList = candidateFilePaths.Where(f => !string.IsNullOrWhiteSpace(f)).ToList();

        // Priority 1: Dedicated release imagery (cover.*, banner.*, hero.*, promo.*)
        var directories = fileList
            .Select(f =>
            {
                try { return Path.GetDirectoryName(f); } catch { return null; }
            })
            .Where(d => !string.IsNullOrWhiteSpace(d) && Directory.Exists(d))
            .Distinct()
            .ToList();

        // Check release directory and parent directory (if release folder has subfolders)
        var searchDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in directories)
        {
            if (dir is not null)
            {
                searchDirs.Add(dir);
                var parent = Directory.GetParent(dir)?.FullName;
                if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent))
                {
                    searchDirs.Add(parent);
                }
            }
        }

        foreach (var dir in searchDirs)
        {
            try
            {
                var files = Directory.GetFiles(dir);
                foreach (var file in files)
                {
                    var ext = Path.GetExtension(file);
                    if (!ImageExtensions.Contains(ext))
                    {
                        continue;
                    }

                    var nameWithoutExt = Path.GetFileNameWithoutExtension(file);
                    foreach (var dedicated in DedicatedNames)
                    {
                        if (nameWithoutExt.Equals(dedicated, StringComparison.OrdinalIgnoreCase)
                            || nameWithoutExt.StartsWith(dedicated + "_", StringComparison.OrdinalIgnoreCase)
                            || nameWithoutExt.StartsWith(dedicated + "-", StringComparison.OrdinalIgnoreCase))
                        {
                            return file;
                        }
                    }
                }
            }
            catch
            {
                // Ignore IO errors when scanning candidate directories
            }
        }

        // Also check if any candidate path itself matches Priority 1
        foreach (var file in fileList)
        {
            var ext = Path.GetExtension(file);
            if (!ImageExtensions.Contains(ext))
            {
                continue;
            }

            var nameWithoutExt = Path.GetFileNameWithoutExtension(file);
            foreach (var dedicated in DedicatedNames)
            {
                if (nameWithoutExt.Contains(dedicated, StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(file))
                    {
                        return file;
                    }
                }
            }
        }

        // Priority 2: Model imagery (render.*, thumbnail.*, preview.*)
        foreach (var file in fileList)
        {
            var ext = Path.GetExtension(file);
            if (!ImageExtensions.Contains(ext))
            {
                continue;
            }

            var nameWithoutExt = Path.GetFileNameWithoutExtension(file);
            foreach (var token in ModelImageryTokens)
            {
                if (nameWithoutExt.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(file))
                    {
                        return file;
                    }
                }
            }
        }

        // Any image in candidate files
        var firstImage = fileList.FirstOrDefault(f => ImageExtensions.Contains(Path.GetExtension(f)) && File.Exists(f));
        if (firstImage is not null)
        {
            return firstImage;
        }

        // Priority 3: Generated Collage
        var cacheFilePath = Path.Combine(_cacheDirectory, $"{releaseId:D}.png");
        if (File.Exists(cacheFilePath))
        {
            return cacheFilePath;
        }

        try
        {
            byte[] bytes;
            if (fileList.Count > 0)
            {
                bytes = ReleaseArtworkCollageGenerator.CreateCompositeCollage(fileList);
            }
            else
            {
                // Priority 4: Default placeholder
                bytes = ReleaseArtworkCollageGenerator.CreatePlaceholderBanner(releaseName);
            }

            await File.WriteAllBytesAsync(cacheFilePath, bytes, cancellationToken).ConfigureAwait(false);
            return cacheFilePath;
        }
        catch
        {
            // Priority 4: Default placeholder fallback
            try
            {
                var placeholderBytes = ReleaseArtworkCollageGenerator.CreatePlaceholderBanner(releaseName);
                await File.WriteAllBytesAsync(cacheFilePath, placeholderBytes, cancellationToken).ConfigureAwait(false);
                return cacheFilePath;
            }
            catch
            {
                return null;
            }
        }
    }
}
