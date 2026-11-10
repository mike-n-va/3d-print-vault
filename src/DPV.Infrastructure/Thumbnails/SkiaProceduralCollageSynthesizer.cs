using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DPV.Domain.Contracts.Catalog;
using SkiaSharp;

namespace DPV.Infrastructure.Thumbnails;

public sealed class SkiaProceduralCollageSynthesizer : IProceduralCollageSynthesizer
{
    public Task<string?> SynthesizeCollageAsync(
        IEnumerable<string> sourceImagePaths,
        string outputCacheFilePath,
        int canvasSize = 1000,
        int separatorThickness = 2,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceImagePaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputCacheFilePath);

        var validImages = sourceImagePaths
            .Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p))
            .Take(4)
            .ToList();

        if (validImages.Count == 0)
        {
            return Task.FromResult<string?>(null);
        }

        // Return cached output if already present and non-empty
        if (File.Exists(outputCacheFilePath) && new FileInfo(outputCacheFilePath).Length > 0)
        {
            return Task.FromResult<string?>(outputCacheFilePath);
        }

        try
        {
            var imageInfo = new SKImageInfo(canvasSize, canvasSize, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(imageInfo);
            if (surface is null)
            {
                return Task.FromResult<string?>(null);
            }

            var canvas = surface.Canvas;
            canvas.Clear(new SKColor(0x18, 0x18, 0x1B)); // Dark charcoal slate #18181B

            var tileRects = ComputeTileRectangles(validImages.Count, canvasSize, separatorThickness);

            for (var i = 0; i < Math.Min(validImages.Count, tileRects.Count); i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var imagePath = validImages[i];
                var destRect = tileRects[i];

                using var sourceBmp = SKBitmap.Decode(imagePath);
                if (sourceBmp is null)
                {
                    continue;
                }

                // Center-crop to 1:1 square
                var minDim = Math.Min(sourceBmp.Width, sourceBmp.Height);
                var srcX = (sourceBmp.Width - minDim) / 2;
                var srcY = (sourceBmp.Height - minDim) / 2;
                var srcRect = new SKRect(srcX, srcY, srcX + minDim, srcY + minDim);

                using var paint = new SKPaint
                {
                    IsAntialias = true
                };

                using var img = SKImage.FromBitmap(sourceBmp);
                canvas.DrawImage(img, srcRect, destRect, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), paint);
            }

            // Draw grid separator lines
            if (validImages.Count > 1)
            {
                DrawSeparators(canvas, validImages.Count, canvasSize, separatorThickness);
            }

            // If 3 images in a 2x2 grid, draw a styled monogram accent in tile 4
            if (validImages.Count == 3 && tileRects.Count >= 4)
            {
                DrawAccentedEmblemTile(canvas, tileRects[3]);
            }

            var outDir = Path.GetDirectoryName(outputCacheFilePath);
            if (!string.IsNullOrWhiteSpace(outDir))
            {
                Directory.CreateDirectory(outDir);
            }

            using var snapshot = surface.Snapshot();
            using var encodedData = snapshot.Encode(SKEncodedImageFormat.Png, 92);

            var tempPath = outputCacheFilePath + ".tmp." + Guid.NewGuid().ToString("N");
            using (var stream = File.Create(tempPath))
            {
                encodedData.SaveTo(stream);
            }

            File.Move(tempPath, outputCacheFilePath, overwrite: true);
            return Task.FromResult<string?>(outputCacheFilePath);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return Task.FromResult<string?>(null);
        }
    }

    private static List<SKRect> ComputeTileRectangles(int imageCount, int canvasSize, int separatorThickness)
    {
        var rects = new List<SKRect>();

        if (imageCount == 1)
        {
            rects.Add(new SKRect(0, 0, canvasSize, canvasSize));
            return rects;
        }

        if (imageCount == 2)
        {
            var halfW = (canvasSize - separatorThickness) / 2f;
            rects.Add(new SKRect(0, 0, halfW, canvasSize));
            rects.Add(new SKRect(halfW + separatorThickness, 0, canvasSize, canvasSize));
            return rects;
        }

        // 3 or 4 images: 2x2 Mosaic Layout
        var tileW = (canvasSize - separatorThickness) / 2f;
        var tileH = (canvasSize - separatorThickness) / 2f;

        // Tile 0: Top-Left
        rects.Add(new SKRect(0, 0, tileW, tileH));
        // Tile 1: Top-Right
        rects.Add(new SKRect(tileW + separatorThickness, 0, canvasSize, tileH));
        // Tile 2: Bottom-Left
        rects.Add(new SKRect(0, tileH + separatorThickness, tileW, canvasSize));
        // Tile 3: Bottom-Right
        rects.Add(new SKRect(tileW + separatorThickness, tileH + separatorThickness, canvasSize, canvasSize));

        return rects;
    }

    private static void DrawSeparators(SKCanvas canvas, int imageCount, int canvasSize, int separatorThickness)
    {
        using var linePaint = new SKPaint
        {
            Color = new SKColor(0x0F, 0x17, 0x2A), // #0F172A dark slate
            StrokeWidth = separatorThickness,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        var halfW = (canvasSize - separatorThickness) / 2f + (separatorThickness / 2f);
        var halfH = (canvasSize - separatorThickness) / 2f + (separatorThickness / 2f);

        if (imageCount == 2)
        {
            canvas.DrawLine(halfW, 0, halfW, canvasSize, linePaint);
        }
        else
        {
            // Vertical split
            canvas.DrawLine(halfW, 0, halfW, canvasSize, linePaint);
            // Horizontal split
            canvas.DrawLine(0, halfH, canvasSize, halfH, linePaint);
        }
    }

    private static void DrawAccentedEmblemTile(SKCanvas canvas, SKRect dest)
    {
        using var bgPaint = new SKPaint
        {
            Color = new SKColor(0x1E, 0x29, 0x3B), // #1E293B slate-800
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(dest, bgPaint);

        // Center a minimalist 3D cube emblem
        var cx = dest.MidX;
        var cy = dest.MidY;
        var size = Math.Min(dest.Width, dest.Height) * 0.25f;

        using var strokePaint = new SKPaint
        {
            Color = new SKColor(0x64, 0x74, 0x8B), // #64748B
            StrokeWidth = 3,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        canvas.DrawCircle(cx, cy, size, strokePaint);
    }
}
