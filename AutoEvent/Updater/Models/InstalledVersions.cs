using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AutoEvent.ApiFeatures;

namespace AutoEvent.Updater.Models;

internal class InstalledVersions
{
    public Dictionary<string, string> Versions { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public static InstalledVersions Load(string path)
    {
        var result = new InstalledVersions();
        if (!File.Exists(path))
            return result;
        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
            if (dict != null)
                foreach (var kv in dict)
                    result.Versions[kv.Key] = kv.Value;
        }
        catch (Exception ex)
        {
            LogManager.Warn($"[SchematicUpdater] installed.json is corrupt, treating as empty.\n{ex.Message}");
        }
        return result;
    }

    public void Save(string path)
    {
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(Versions,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            LogManager.Warn($"[SchematicUpdater] Failed to save installed.json.\n{ex.Message}");
        }
    }
}
