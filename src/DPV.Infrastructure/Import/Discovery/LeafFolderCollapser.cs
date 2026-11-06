using System;
using System.Collections.Generic;
using System.IO;

namespace DPV.Infrastructure.Import.Discovery;

public static class LeafFolderCollapser
{
    private static readonly HashSet<string> ReservedVariantSubfolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "supported",
        "unsupported",
        "pre-supported",
        "presupported",
        "pre_supported",
        "parts",
        "pieces",
        "mesh",
        "meshes",
        "renders",
        "render",
        "images",
        "image",
        "previews",
        "preview",
        "docs",
        "documentation",
        "instructions",
        "stl",
        "stls",
        "3mf",
        "plates"
    };

    public static string ResolveLogicalDirectory(string filePath, string scanRoot)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(dir))
        {
            return scanRoot;
        }

        var current = new DirectoryInfo(dir);
        var root = new DirectoryInfo(scanRoot);

        while (current != null && !string.Equals(current.FullName, root.FullName, StringComparison.OrdinalIgnoreCase))
        {
            if (ReservedVariantSubfolders.Contains(current.Name))
            {
                if (current.Parent != null && !string.Equals(current.FullName, root.FullName, StringComparison.OrdinalIgnoreCase))
                {
                    current = current.Parent;
                    continue;
                }
            }
            break;
        }

        return current?.FullName ?? scanRoot;
    }

    public static bool IsVirtualVariantSubfolder(string folderName) =>
        ReservedVariantSubfolders.Contains(folderName);
}
