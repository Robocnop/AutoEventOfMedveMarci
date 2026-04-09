using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using AutoEvent.ApiFeatures;
using AutoEvent.Updater.Models;
using LabApi.Features;

namespace AutoEvent.Updater;

internal static class SchematicUpdater
{
    private const string ManifestUrl =
        "https://raw.githubusercontent.com/MedveMarci/AutoEvent/main/updater/manifest.json";
    private const string RawBaseUrl =
        "https://raw.githubusercontent.com/MedveMarci/AutoEvent/main/";

    private static string InstalledPath =>
        Path.Combine(AutoEvent.Singleton.Config.SchematicsDirectoryPath, "installed.json");

    // ── Progress bar ────────────────────────────────────────────────────────
    private static string BuildProgressBar(int current, int total, int barWidth = 20)
    {
        int filled  = total == 0 ? barWidth : (int)((double)current / total * barWidth);
        int percent = total == 0 ? 100      : (int)((double)current / total * 100);
        return $"[{new string('█', filled)}{new string('░', barWidth - filled)}] {percent,3}%";
    }

    /// <summary>Called on plugin startup. Logs outdated/missing schematics — never downloads.</summary>
    public static void Check()
    {
        try
        {
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
                LogManager.Info("[SchematicUpdater] Schematics are not yet registered. Run 'ev update' to migrate and update.");
                return;
            }

            var installed = InstalledVersions.Load(InstalledPath);
            var outdated  = new List<(SchematicEntry entry, string localVersion)>();
            var missing   = new List<SchematicEntry>();

            foreach (var entry in manifest.Schematics)
            {
                if (!installed.Versions.TryGetValue(entry.Name, out var localVersion))
                {
                    var filePath = Path.Combine(
                        AutoEvent.Singleton.Config.SchematicsDirectoryPath,
                        Path.GetFileName(entry.File));
                    if (!File.Exists(filePath))
                        missing.Add(entry);
                    // file exists but not in installed.json → custom/unmanaged, skip
                    continue;
                }

                if (Version.TryParse(localVersion, out var local) &&
                    Version.TryParse(entry.Version, out var remote) &&
                    remote > local)
                {
                    outdated.Add((entry, localVersion));
                }
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

    /// <summary>
    /// Fetches manifest, performs legacy migration if needed, downloads outdated/missing schematics.
    /// Returns (updated, failed, total). Throws on manifest fetch/parse failure.
    /// </summary>
    public static (int updated, int failed, int total) Update()
    {
        var manifestJson = HttpQuery.Get(ManifestUrl);
        if (string.IsNullOrWhiteSpace(manifestJson))
            throw new Exception("Could not fetch schematic manifest. Check your internet connection.");

        var manifest = JsonSerializer.Deserialize<SchematicManifest>(manifestJson);
        if (manifest?.Schematics == null)
            throw new Exception("Manifest is empty or malformed.");

        bool isLegacy = !File.Exists(InstalledPath);
        var installed = InstalledVersions.Load(InstalledPath);

        // ── Legacy migration ─────────────────────────────────────────────
        if (isLegacy)
        {
            LogManager.Info(
                "[SchematicUpdater] No installed.json found — migrating legacy schematics to new update system...");
            int legacyCount = 0;
            foreach (var entry in manifest.Schematics)
            {
                var filePath = Path.Combine(
                    AutoEvent.Singleton.Config.SchematicsDirectoryPath,
                    Path.GetFileName(entry.File));
                if (File.Exists(filePath))
                {
                    installed.Versions[entry.Name] = "0.0.0";
                    legacyCount++;
                }
            }
            LogManager.Info(
                $"[SchematicUpdater] Found {legacyCount} existing schematics, registering as legacy (will be updated).");
        }

        // ── Build work list ──────────────────────────────────────────────
        var toUpdate = new List<(SchematicEntry entry, string label)>();
        foreach (var entry in manifest.Schematics)
        {
            if (installed.Versions.TryGetValue(entry.Name, out var localVersion))
            {
                if (Version.TryParse(localVersion, out var local) &&
                    Version.TryParse(entry.Version, out var remote) &&
                    remote > local)
                {
                    toUpdate.Add((entry, $"{localVersion} → {entry.Version}"));
                }
            }
            else
            {
                var filePath = Path.Combine(
                    AutoEvent.Singleton.Config.SchematicsDirectoryPath,
                    Path.GetFileName(entry.File));
                if (!File.Exists(filePath))
                    toUpdate.Add((entry, "new"));
                // file exists but not in installed.json → custom/unmanaged, skip
            }
        }

        if (toUpdate.Count == 0)
        {
            LogManager.Info("[SchematicUpdater] All schematics are already up to date.");
            installed.Save(InstalledPath); // persist legacy migration if it ran
            return (0, 0, 0);
        }

        // ── Download loop ────────────────────────────────────────────────
        LogManager.Info($"[SchematicUpdater] Starting update... ({toUpdate.Count} schematics to update)");

        int updated = 0, failed = 0;
        for (int i = 0; i < toUpdate.Count; i++)
        {
            var (entry, label) = toUpdate[i];
            try
            {
                var fileUrl  = RawBaseUrl + entry.File;
                var content  = HttpQuery.Get(fileUrl);
                if (string.IsNullOrEmpty(content))
                    throw new Exception("Empty response from server.");

                var destPath = Path.Combine(
                    AutoEvent.Singleton.Config.SchematicsDirectoryPath,
                    Path.GetFileName(entry.File));
                File.WriteAllText(destPath, content, Encoding.UTF8);

                installed.Versions[entry.Name] = entry.Version;
                updated++;
            }
            catch (Exception ex)
            {
                LogManager.Warn($"[SchematicUpdater] Failed to download {entry.Name}: {ex.Message}");
                failed++;
            }

            // Progress shown after each file (success or fail)
            // changelog shown for all updates (label != "new"), including legacy 0.0.0 → x.y.z
            bool showChangelog  = label != "new" && !string.IsNullOrWhiteSpace(entry.Changelog);
            var changelogSuffix = showChangelog ? $"  {entry.Changelog}" : string.Empty;
            var fileName        = Path.GetFileName(entry.File);
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
