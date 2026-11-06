using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DPV.Infrastructure.Thumbnails;

public static class ReleaseArtworkCollageGenerator
{
    public const int BannerWidth = 480;
    public const int BannerHeight = 270;

    public static byte[] CreatePlaceholderBanner(string? releaseName = null)
    {
        var pixels = new byte[BannerWidth * BannerHeight * 4];

        // Draw deep slate gradient background (#131B24 to #1E2B38)
        for (var y = 0; y < BannerHeight; y++)
        {
            var factor = (float)y / BannerHeight;
            var r = (byte)(19 + (factor * 11));
            var g = (byte)(27 + (factor * 16));
            var b = (byte)(36 + (factor * 20));

            for (var x = 0; x < BannerWidth; x++)
            {
                SetPixel(pixels, x, y, r, g, b, 255);
            }
        }

        // Draw subtle accent frame / header bar
        for (var x = 0; x < BannerWidth; x++)
        {
            for (var y = 0; y < 4; y++)
            {
                SetPixel(pixels, x, y, 79, 70, 229, 255); // Indigo #4F46E5
            }
        }

        // Draw centered geometric 3D cube emblem (32x32)
        var centerX = BannerWidth / 2;
        var centerY = BannerHeight / 2;

        DrawEmblem(pixels, centerX, centerY);

        return PureManagedPngEncoder.Encode(pixels, BannerWidth, BannerHeight);
    }

    public static byte[] CreateCompositeCollage(IEnumerable<string> modelFilePaths)
    {
        var modelList = modelFilePaths.Where(p => !string.IsNullOrWhiteSpace(p)).Take(6).ToList();
        if (modelList.Count == 0)
        {
            return CreatePlaceholderBanner();
        }

        var pixels = new byte[BannerWidth * BannerHeight * 4];

        // Background dark gradient (#111827 to #1F2937)
        for (var y = 0; y < BannerHeight; y++)
        {
            var factor = (float)y / BannerHeight;
            var r = (byte)(17 + (factor * 14));
            var g = (byte)(24 + (factor * 17));
            var b = (byte)(39 + (factor * 16));

            for (var x = 0; x < BannerWidth; x++)
            {
                SetPixel(pixels, x, y, r, g, b, 255);
            }
        }

        // Divide into N columns (3 to 6)
        var slotCount = Math.Clamp(modelList.Count, 3, 6);
        var margin = 16;
        var totalAvailableWidth = BannerWidth - (margin * 2);
        var slotSpacing = 8;
        var totalSpacing = slotSpacing * (slotCount - 1);
        var slotWidth = (totalAvailableWidth - totalSpacing) / slotCount;
        var slotHeight = BannerHeight - (margin * 2);

        for (var i = 0; i < slotCount; i++)
        {
            var slotLeft = margin + i * (slotWidth + slotSpacing);
            var slotTop = margin;

            // Render slot card background (#273546)
            for (var sy = slotTop; sy < slotTop + slotHeight; sy++)
            {
                for (var sx = slotLeft; sx < slotLeft + slotWidth; sx++)
                {
                    // Border check
                    if (sy == slotTop || sy == slotTop + slotHeight - 1 || sx == slotLeft || sx == slotLeft + slotWidth - 1)
                    {
                        SetPixel(pixels, sx, sy, 79, 70, 229, 220); // Accent border
                    }
                    else
                    {
                        var innerFactor = (float)(sy - slotTop) / slotHeight;
                        var br = (byte)(30 + (innerFactor * 10));
                        var bg = (byte)(41 + (innerFactor * 14));
                        var bb = (byte)(59 + (innerFactor * 15));
                        SetPixel(pixels, sx, sy, br, bg, bb, 255);
                    }
                }
            }

            // Draw miniature geometric icon in slot
            var scX = slotLeft + (slotWidth / 2);
            var scY = slotTop + (slotHeight / 2);
            DrawMiniIcon(pixels, scX, scY, (i % 3));
        }

        return PureManagedPngEncoder.Encode(pixels, BannerWidth, BannerHeight);
    }

    private static void DrawEmblem(byte[] pixels, int cx, int cy)
    {
        // Draw 3D cube isometric lines
        for (var d = -20; d <= 20; d++)
        {
            DrawPixelSafe(pixels, cx + d, cy - 20, 129, 140, 248, 255);
            DrawPixelSafe(pixels, cx + d, cy + 20, 129, 140, 248, 255);
            DrawPixelSafe(pixels, cx - 20, cy + d, 129, 140, 248, 255);
            DrawPixelSafe(pixels, cx + 20, cy + d, 129, 140, 248, 255);
        }

        // Inner diagonals
        for (var i = -14; i <= 14; i++)
        {
            DrawPixelSafe(pixels, cx + i, cy + i, 99, 102, 241, 200);
            DrawPixelSafe(pixels, cx - i, cy + i, 99, 102, 241, 200);
        }
    }

    private static void DrawMiniIcon(byte[] pixels, int cx, int cy, int variant)
    {
        var size = 12;
        switch (variant)
        {
            case 0: // Diamond
                for (var r = 0; r <= size; r++)
                {
                    DrawPixelSafe(pixels, cx - r, cy - size + r, 165, 180, 252, 255);
                    DrawPixelSafe(pixels, cx + r, cy - size + r, 165, 180, 252, 255);
                    DrawPixelSafe(pixels, cx - r, cy + size - r, 165, 180, 252, 255);
                    DrawPixelSafe(pixels, cx + r, cy + size - r, 165, 180, 252, 255);
                }
                break;
            case 1: // Hexagon
                for (var d = -size; d <= size; d++)
                {
                    DrawPixelSafe(pixels, cx + d, cy - size, 129, 140, 248, 255);
                    DrawPixelSafe(pixels, cx + d, cy + size, 129, 140, 248, 255);
                    DrawPixelSafe(pixels, cx - size, cy + d, 129, 140, 248, 255);
                    DrawPixelSafe(pixels, cx + size, cy + d, 129, 140, 248, 255);
                }
                break;
            default: // Triangle
                for (var r = 0; r <= size; r++)
                {
                    DrawPixelSafe(pixels, cx - r, cy + size, 199, 210, 254, 255);
                    DrawPixelSafe(pixels, cx + r, cy + size, 199, 210, 254, 255);
                    DrawPixelSafe(pixels, cx - (r / 2), cy - size + r * 2, 199, 210, 254, 255);
                    DrawPixelSafe(pixels, cx + (r / 2), cy - size + r * 2, 199, 210, 254, 255);
                }
                break;
        }
    }

    private static void SetPixel(byte[] pixels, int x, int y, byte r, byte g, byte b, byte a)
    {
        var idx = ((y * BannerWidth) + x) * 4;
        pixels[idx] = r;
        pixels[idx + 1] = g;
        pixels[idx + 2] = b;
        pixels[idx + 3] = a;
    }

    private static void DrawPixelSafe(byte[] pixels, int x, int y, byte r, byte g, byte b, byte a)
    {
        if (x >= 0 && x < BannerWidth && y >= 0 && y < BannerHeight)
        {
            SetPixel(pixels, x, y, r, g, b, a);
        }
    }
}
