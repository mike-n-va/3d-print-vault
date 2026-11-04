using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace DPV.UI.Converters;

public sealed class ThumbnailBitmapConverter : IValueConverter
{
    private const int MaxCacheSize = 128;
    private static readonly object SyncRoot = new();
    private static readonly LinkedList<string> LruList = new();
    private static readonly Dictionary<string, (LinkedListNode<string> Node, Bitmap Bitmap)> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || !File.Exists(path))
        {
            return null;
        }

        lock (SyncRoot)
        {
            if (Cache.TryGetValue(path, out var entry))
            {
                LruList.Remove(entry.Node);
                LruList.AddFirst(entry.Node);
                return entry.Bitmap;
            }

            try
            {
                var bitmap = new Bitmap(path);

                if (Cache.Count >= MaxCacheSize)
                {
                    var oldest = LruList.Last;
                    if (oldest is not null)
                    {
                        LruList.RemoveLast();
                        if (Cache.Remove(oldest.Value, out var evicted))
                        {
                            evicted.Bitmap.Dispose();
                        }
                    }
                }

                var node = LruList.AddFirst(path);
                Cache[path] = (node, bitmap);
                return bitmap;
            }
            catch
            {
                return null;
            }
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
