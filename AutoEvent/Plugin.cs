using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using AutoEvent.API;
using AutoEvent.Loader;
using HarmonyLib;
using LabApi.Events.CustomHandlers;
using LabApi.Features;
using LabApi.Features.Wrappers;
using LabApi.Loader.Features.Paths;
using LabApi.Loader.Features.Plugins;

namespace AutoEvent;

public class AutoEvent : Plugin<Config>
{
    private const bool PreRelease = true;
    public static AutoEvent Singleton;
    private static Harmony _harmonyPatch;
    public static EventManager EventManager;
    private static EventHandler _eventHandler;
    internal static float MusicVolume;
    public override string Name => "AutoEvent";

    public override string Author =>
        "Created by a large community of programmers, map builders and just ordinary people, under the leadership of RisottoMan. MapEditorReborn for 14.1 port by Sakred_. LabApi port by MedveMarci.";

    public override string Description =>
        "A plugin that allows you to play mini-games in SCP:SL. It includes a variety of games such as Spleef, Lava, Hide and Seek, Knives, and more. Each game has its own unique mechanics and rules, providing a fun and engaging experience for players.";

    public override Version Version => new(9, 15, 0);
    public override Version RequiredApiVersion => new(LabApiProperties.CompiledVersion);

    public static string BaseConfigPath { get; private set; }

    public override void Enable()
    {
        CosturaUtility.Initialize();
        BaseConfigPath = Path.Combine(PathManager.Configs.FullName, "AutoEvent");
        try
        {
            Singleton = this;

            if (Config.IgnoredRoles.Contains(Config.LobbyRole))
            {
                LogManager.Error(
                    "The Lobby Role is also in ignored roles. This will break the game if not changed. The plugin will remove the lobby role from ignored roles.");
                Config.IgnoredRoles.Remove(Config.LobbyRole);
            }

            FriendlyFireSystem.IsFriendlyFireEnabledByDefault = Server.FriendlyFire;

            try
            {
                _harmonyPatch = new Harmony("autoevent");
                _harmonyPatch.PatchAll();
            }
            catch (Exception e)
            {
                LogManager.Error($"Could not patch harmony methods.\n{e}");
            }

            try
            {
                LogManager.Debug($"Base Config Path: {BaseConfigPath}");
                LogManager.Debug($"Configs paths: \n" +
                                 $"{Config.SchematicsDirectoryPath}\n" +
                                 $"{Config.MusicDirectoryPath}\n");
                CreateDirectoryIfNotExists(BaseConfigPath);
                CreateDirectoryIfNotExists(Config.SchematicsDirectoryPath);
                CreateDirectoryIfNotExists(Config.MusicDirectoryPath);
            }
            catch (Exception e)
            {
                LogManager.Error($"An error has occured while trying to initialize directories.\n{e}");
            }

            EventManager = new EventManager();
            EventManager.RegisterInternalEvents();
            _eventHandler = new EventHandler();
            CustomHandlersManager.RegisterEventsHandler(_eventHandler);
            ConfigManager.LoadConfigsAndTranslations();
            MusicVolume = Config.Volume;
            LogManager.Info("The mini-games are loaded.");
        }
        catch (Exception e)
        {
            LogManager.Error($"Caught an exception while starting plugin.\n{e}");
        }
    }

    private static void CreateDirectoryIfNotExists(string path)
    {
        try
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        }
        catch (Exception e)
        {
            LogManager.Error($"An error has occured while trying to create a new directory.\nPath: {path}\n{e}");
        }
    }

    public override void Disable()
    {
        _harmonyPatch.UnpatchAll();
        EventManager = null;
        Singleton = null;
        CustomHandlersManager.UnregisterEventsHandler(_eventHandler);
        _eventHandler = null;
    }

    internal static async Task CheckForUpdatesAsync(Version currentVersion)
    {
        try
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"{Singleton.Name}/{currentVersion}");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            var repo = $"MedveMarci/{Singleton.Name}";
            var latestStableJson = await client.GetStringAsync($"https://api.github.com/repos/{repo}/releases/latest")
                .ConfigureAwait(false);
            var allReleasesJson = await client
                .GetStringAsync($"https://api.github.com/repos/{repo}/releases?per_page=20").ConfigureAwait(false);

            using var latestStableDoc = JsonDocument.Parse(latestStableJson);
            using var allReleasesDoc = JsonDocument.Parse(allReleasesJson);

            var latestStableRoot = latestStableDoc.RootElement;
            string stableTag = null;
            if (latestStableRoot.TryGetProperty("tag_name", out var tagProp))
                stableTag = tagProp.GetString();
            var stableVer = ParseVersion(stableTag);

            JsonElement? latestPre = null;
            Version preVer = null;
            string preTag = null;

            if (allReleasesDoc.RootElement.ValueKind == JsonValueKind.Array)
            {
                DateTime? bestPublishedAt = null;
                foreach (var rel in allReleasesDoc.RootElement.EnumerateArray())
                {
                    if (rel.ValueKind != JsonValueKind.Object) continue;

                    var draft = rel.TryGetProperty("draft", out var draftProp) &&
                                draftProp.ValueKind == JsonValueKind.True;
                    if (draft) continue;

                    var prerelease = rel.TryGetProperty("prerelease", out var preProp) &&
                                     preProp.ValueKind == JsonValueKind.True;
                    if (!prerelease) continue;

                    DateTime? publishedAt = null;
                    if (rel.TryGetProperty("published_at", out var pubProp))
                    {
                        var s = pubProp.GetString();
                        if (!string.IsNullOrWhiteSpace(s) && DateTime.TryParse(s, out var dt))
                            publishedAt = dt;
                    }

                    if (latestPre == null || (publishedAt.HasValue &&
                                              (!bestPublishedAt.HasValue || publishedAt.Value > bestPublishedAt.Value)))
                    {
                        latestPre = rel;
                        bestPublishedAt = publishedAt;
                    }
                }
            }

            if (latestPre.HasValue)
            {
                if (latestPre.Value.TryGetProperty("tag_name", out var preTagProp))
                    preTag = preTagProp.GetString();
                preVer = ParseVersion(preTag);
            }

            var outdatedStable = stableVer != null && stableVer > currentVersion;
            var prereleaseNewer = preVer != null && preVer > currentVersion && !outdatedStable;

            if (outdatedStable)
                LogManager.Info(
                    $"A new {Singleton.Name} version is available: {stableTag} (current {currentVersion}). Download: https://github.com/MedveMarci/{Singleton.Name}/releases/latest",
                    ConsoleColor.DarkRed);
            else if (prereleaseNewer)
                LogManager.Info(
                    $"A newer pre-release is available: {preTag} (current {currentVersion}). Download: https://github.com/MedveMarci/{Singleton.Name}/releases/tag/{preTag}",
                    ConsoleColor.DarkYellow);
            else
                LogManager.Info(
                    $"Thanks for using {Singleton.Name} v{currentVersion}. To get support and latest news, join to my Discord Server: https://discord.gg/KmpA8cfaSA",
                    ConsoleColor.Blue);
            if (PreRelease)
                LogManager.Info(
                    "This is a pre-release version. There might be bugs, if you find one, please report it on GitHub or Discord.",
                    ConsoleColor.DarkYellow);
        }
        catch (Exception e)
        {
            LogManager.Error($"Version check failed.\n{e}");
        }
    }

    private static Version ParseVersion(string tag)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(tag)) return null;
            var t = tag.Trim();
            if (t.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                t = t.Substring(1);

            var cut = t.IndexOfAny(new[] { '-', '+' });
            if (cut >= 0)
                t = t.Substring(0, cut);

            return Version.TryParse(t, out var v) ? v : null;
        }
        catch
        {
            return null;
        }
    }
}