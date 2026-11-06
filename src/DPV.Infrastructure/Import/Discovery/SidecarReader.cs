using System;
using System.IO;
using System.Text.Json;
using DPV.Domain.Models;

namespace DPV.Infrastructure.Import.Discovery;

public static class SidecarReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static bool TryReadSidecar(string filePath, out SidecarManifest? manifest)
    {
        manifest = null;
        try
        {
            if (!File.Exists(filePath))
            {
                return false;
            }

            var text = File.ReadAllText(filePath);
            var parsed = JsonSerializer.Deserialize<SidecarManifest>(text, JsonOptions);
            if (parsed != null && !string.IsNullOrWhiteSpace(parsed.Name))
            {
                manifest = parsed;
                return true;
            }
        }
        catch (Exception)
        {
            // Invalid or unparseable JSON sidecar
        }

        return false;
    }
}
