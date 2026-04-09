using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using AutoEvent.ApiFeatures;
using AutoEvent.Integrations.MapEditor;
using AutoEvent.Updater.Models;

namespace AutoEvent.Updater;

internal static class SchematicUpdater
{
    private const string ManifestUrl =
        "https://raw.githubusercontent.com/MedveMarci/AutoEvent/schematics/updater/manifest.json";

    private const string RawBaseUrl =
        "https://raw.githubusercontent.com/MedveMarci/AutoEvent/schematics/";

    private static string BaseSchematicsPath =>
        AutoEvent.Singleton.Config.SchematicsDirectoryPath;

    private static string ProjectMerFolder =>
        Path.Combine(BaseSchematicsPath, "ProjectMER");

    private static string TmeFolder => 
        Path.Combine(BaseSchematicsPath, "TME");

    private static string InstalledPath =>
        Path.Combine(BaseSchematicsPath, "installed.json");

    private static string GetActiveEditorFilePath(SchematicEntry entry)
    {
        if (MapSystemIntegration.UseProjectMer)
        {
            Directory.CreateDirectory(ProjectMerFolder);
            return Path.Combine(ProjectMerFolder, Path.GetFileName(entry.ProjectMerFile));
        }

        if (!MapSystemIntegration.UseTme)
            return null;
        Directory.CreateDirectory(TmeFolder);
        return Path.Combine(TmeFolder, Path.GetFileName(entry.TmeFile));
    }

    private static string GetActiveEditorFileUrl(SchematicEntry entry)
    {
        if (MapSystemIntegration.UseProjectMer)
            return RawBaseUrl + entry.ProjectMerFile;
        if (MapSystemIntegration.UseTme)
            return RawBaseUrl + entry.TmeFile;
        return null;
    }

    private static string BuildProgressBar(int current, int total, int barWidth = 20)
    {
        var filled = total == 0 ? barWidth : (int)((double)current / total * barWidth);
        var percent = total == 0 ? 100 : (int)((double)current / total * 100);
        return $"[{new string('█', filled)}{new string('░', barWidth - filled)}] {percent,3}%";
    }

    public static void Check()
    {
        try
        {
            if (!MapSystemIntegration.AnyLoaded)
            {
                LogManager.Info("[SchematicUpdater] No map editor detected — skipping schematic check.");
                return;
            }

            var manifestJson = HttpQuery.Get(ManifestUrl);
            if (string.IsNullOrWhiteSpace(manifestJson))
            {
                LogManager.Warn("[SchematicUpdater] Could not fetch schematic manifest.");
                return;
            }

            var manifest = JsonSerializer.Deserialize<SchematicManifest>(manifestJson);
            if (manifest?.Schematics == null)
            {
                LogManager.Warn("[SchematicUpdater] Manifest is empty or malformed.");
                return;
            }

            if (!File.Exists(InstalledPath))
            {
                LogManager.Info(
                    "[SchematicUpdater] Schematics are not yet registered. Run 'ev update' to migrate and update.");
                return;
            }

            var installed = InstalledVersions.Load(InstalledPath);
            var outdated = new List<(SchematicEntry entry, string localVersion)>();
            var missing = new List<SchematicEntry>();

            foreach (var entry in manifest.Schematics)
            {
                if (!installed.Versions.TryGetValue(entry.Name, out var localVersion))
                {
                    var filePath = GetActiveEditorFilePath(entry);
                    if (filePath != null && !File.Exists(filePath))
                        missing.Add(entry);
                    continue;
                }

                if (Version.TryParse(localVersion, out var local) &&
                    Version.TryParse(entry.Version, out var remote) &&
                    remote > local)
                    outdated.Add((entry, localVersion));
            }

            if (outdated.Count == 0 && missing.Count == 0)
            {
                LogManager.Info("[SchematicUpdater] All schematics are up to date.");
                return;
            }

            LogManager.Info(
                $"[SchematicUpdater] {outdated.Count + missing.Count} schematic(s) outdated or missing. Run 'ev update' to update.");

            foreach (var (entry, localVersion) in outdated)
            {
                var changelogPart = string.IsNullOrWhiteSpace(entry.Changelog)
                    ? string.Empty
                    : $"  |  {entry.Changelog}";
                LogManager.Info(
                    $"[SchematicUpdater]   {entry.Name,-20} {localVersion} → {entry.Version,-10}{changelogPart}");
            }

            foreach (var entry in missing)
                LogManager.Info($"[SchematicUpdater]   {entry.Name,-20} (missing)");
        }
        catch (Exception ex)
        {
            LogManager.Warn($"[SchematicUpdater] Version check failed: {ex.Message}");
        }
    }


    public static (int updated, int failed, int total) Update()
    {
        if (!MapSystemIntegration.AnyLoaded)
            throw new Exception(
                "No map editor detected. Cannot update schematics without ProjectMER or ThaumielMapEditor.");

        var manifestJson = HttpQuery.Get(ManifestUrl);
        if (string.IsNullOrWhiteSpace(manifestJson))
            throw new Exception("Could not fetch schematic manifest. Check your internet connection.");

        var manifest = JsonSerializer.Deserialize<SchematicManifest>(manifestJson);
        if (manifest?.Schematics == null)
            throw new Exception("Manifest is empty or malformed.");

        var isLegacy = !File.Exists(InstalledPath);
        var installed = InstalledVersions.Load(InstalledPath);

        if (isLegacy)
        {
            LogManager.Info(
                "[SchematicUpdater] No installed.json found — migrating legacy schematics to new structure...");
            Directory.CreateDirectory(ProjectMerFolder);
            var legacyCount = 0;

            foreach (var entry in manifest.Schematics)
            {
                var legacyPath = Path.Combine(BaseSchematicsPath, Path.GetFileName(entry.ProjectMerFile));
                if (!File.Exists(legacyPath)) 
                    continue;
                var newPath = Path.Combine(ProjectMerFolder,
                    Path.GetFileName(entry.ProjectMerFile) ?? string.Empty);
                try
                {
                    if (File.Exists(newPath))
                        File.Delete(newPath);
                    File.Move(legacyPath, newPath);
                    installed.Versions[entry.Name] = "0.0.0";
                    legacyCount++;
                }
                catch (Exception ex)
                {
                    LogManager.Warn($"[SchematicUpdater] Failed to migrate {entry.Name}: {ex.Message}");
                }
            }

            LogManager.Info(
                $"[SchematicUpdater] Found {legacyCount} existing schematics, migrated to ProjectMER folder (will be updated).");
        }

        var toUpdate = new List<(SchematicEntry entry, string label)>();
        foreach (var entry in manifest.Schematics)
            if (installed.Versions.TryGetValue(entry.Name, out var localVersion))
            {
                if (Version.TryParse(localVersion, out var local) &&
                    Version.TryParse(entry.Version, out var remote) &&
                    remote > local)
                    toUpdate.Add((entry, $"{localVersion} → {entry.Version}"));
            }
            else
            {
                var filePath = GetActiveEditorFilePath(entry);
                if (filePath != null && !File.Exists(filePath))
                    toUpdate.Add((entry, "new"));
            }

        if (toUpdate.Count == 0)
        {
            LogManager.Info("[SchematicUpdater] All schematics are already up to date.");
            installed.Save(InstalledPath);
            return (0, 0, 0);
        }

        LogManager.Info($"[SchematicUpdater] Starting update... ({toUpdate.Count} schematics to update)");

        int updated = 0, failed = 0;
        for (var i = 0; i < toUpdate.Count; i++)
        {
            var (entry, label) = toUpdate[i];
            try
            {
                var fileUrl = GetActiveEditorFileUrl(entry);
                var destPath = GetActiveEditorFilePath(entry);

                if (fileUrl == null || destPath == null)
                    throw new Exception("No active map editor or invalid file path.");

                var content = HttpQuery.Get(fileUrl);
                if (string.IsNullOrEmpty(content))
                    throw new Exception("Empty response from server.");

                File.WriteAllText(destPath, content, Encoding.UTF8);

                installed.Versions[entry.Name] = entry.Version;
                updated++;
            }
            catch (Exception ex)
            {
                LogManager.Warn($"[SchematicUpdater] Failed to download {entry.Name}: {ex.Message}");
                failed++;
            }

            var showChangelog = label != "new" && !string.IsNullOrWhiteSpace(entry.Changelog);
            var changelogSuffix = showChangelog ? $"  {entry.Changelog}" : string.Empty;
            var fileName = MapSystemIntegration.UseProjectMer
                ? Path.GetFileName(entry.ProjectMerFile)
                : Path.GetFileName(entry.TmeFile);
            LogManager.Info(
                $"[SchematicUpdater] {BuildProgressBar(i + 1, toUpdate.Count)} - {fileName,-30} ({label}){changelogSuffix}");
        }

        installed.Save(InstalledPath);

        var summary = failed > 0
            ? $"Updated {updated}/{toUpdate.Count} schematics. {failed} failed (see console)."
            : $"Update complete! Updated {updated}/{toUpdate.Count} schematics.";
        LogManager.Info($"[SchematicUpdater] {summary}");

        return (updated, failed, toUpdate.Count);
    }
}