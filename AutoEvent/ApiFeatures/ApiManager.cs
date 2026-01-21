using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using LabApi.Features;

namespace AutoEvent.ApiFeatures;

public static class ApiManager
{
    private const string ApiBase = "https://bearmanapi.hu";
    private static readonly Dictionary<string, CreditTag> SavedCreditTags = new();

    internal static void CheckForUpdates()
    {
        var name = AutoEvent.Singleton.Name;
        var currentVersion = AutoEvent.Singleton.Version;

        var resp = HttpQuery.Get($"{ApiBase}/api/v1/plugin/{Uri.EscapeDataString(name)}/latest");
        var (statusCode, message) = ParseApiResponse(resp);

        if (statusCode != HttpStatusCode.OK)
        {
            LogManager.Error($"Version check failed: {statusCode} - {message ?? "(no message)"}");
            return;
        }

        var root = JsonDocument.Parse(resp).RootElement;

        if (!root.TryGetProperty("version", out var versionProp) || versionProp.ValueKind != JsonValueKind.String)
        {
            LogManager.Error("Version check failed: 'version' field missing or invalid.");
            return;
        }

        var version = versionProp.GetString();

        if (version == null || !Version.TryParse(version, out var latestRemoteVersion))
        {
            LogManager.Error("Version check failed: Invalid version format.");
            return;
        }

        var outdated = latestRemoteVersion > currentVersion;
        var currentIsNewerThanRemote = currentVersion > latestRemoteVersion;

        var currentVersionResp =
            HttpQuery.Get(
                $"{ApiBase}/api/v1/plugin/{Uri.EscapeDataString(name)}/version/{Uri.EscapeDataString(currentVersion.ToString())}");
        var (currentStatusCode, currentMessage) = ParseApiResponse(currentVersionResp);
        if (currentStatusCode != HttpStatusCode.OK)
            LogManager.Debug($"Recall check failed: {currentStatusCode} - {currentMessage}");


        var recallRoot = JsonDocument.Parse(currentVersionResp).RootElement;

        if (recallRoot.TryGetProperty("is_recalled", out var isRecalledProp) &&
            isRecalledProp.ValueKind == JsonValueKind.True)
        {
            var recallReason = recallRoot.TryGetProperty("recall_reason", out var reasonProp) &&
                               reasonProp.ValueKind == JsonValueKind.String
                ? reasonProp.GetString()
                : "No reason provided.";
            LogManager.Error(
                $"This version of {name} has been recalled.\nPlease update to {latestRemoteVersion} version as soon as possible.\nReason: {recallReason}",
                ConsoleColor.DarkRed);
            return;
        }

        if (outdated)
            LogManager.Info(
                $"A new of {name} version is available: {version} (current {currentVersion}). {GetDownloadUrl(root)}",
                ConsoleColor.DarkRed);
        else
            LogManager.Info(
                $"Thanks for using {name} v{currentVersion}. To get support and latest news, join to my Discord Server: https://discord.gg/KmpA8cfaSA",
                ConsoleColor.Blue);


        if (!currentIsNewerThanRemote) return;
        LogManager.Info(
            $"You are running a newer version of {name} ({currentVersion}) than {latestRemoteVersion}. This is a development/pre-release build and it can contain errors or bugs.",
            ConsoleColor.DarkMagenta);
    }

    private static string GetDownloadUrl(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return "";
        if (root.TryGetProperty("download_url", out var d) && d.ValueKind == JsonValueKind.String)
            return string.IsNullOrEmpty(d.GetString()) ? "" : $"Download: {d.GetString()}";

        return "";
    }

    internal static string SendLogsAsync(string content)
    {
        try
        {
            var url = $"{ApiBase}/api/v1/plugin/{Uri.EscapeDataString(AutoEvent.Singleton.Name)}/log";

            LogManager.Info("Sending logs to BearmanAPI...", ConsoleColor.Green);

            var payload = new
            {
                content,
                plugin_version = AutoEvent.Singleton.Version.ToString(),
                labapi_version = LabApiProperties.CurrentVersion
            };
            var json = JsonSerializer.Serialize(payload);
            var resp = HttpQuery.Post(url, json, "application/json");
            var data = ParseApiResponse(resp);
            if (data.StatusCode != HttpStatusCode.Created)
            {
                LogManager.Error($"Failed to send logs: {data.StatusCode} - {data.Message ?? "(no message)"}");
                return null;
            }

            if (JsonDocument.Parse(resp).RootElement.TryGetProperty("log_id", out var logIdProp) &&
                logIdProp.ValueKind == JsonValueKind.String)
                return logIdProp.GetString();

            LogManager.Warn("Logs sent but no log_id returned.");
            return null;
        }
        catch (Exception e)
        {
            LogManager.Error($"Sending logs failed.\n{e}");
            return null;
        }
    }

    internal static bool TryGetCreditTag(string steam64, out string tag, out string color)
    {
        tag = null;
        color = null;
        if (string.IsNullOrWhiteSpace(steam64))
            return false;
        LogManager.Debug($"[CreditTag] Original Steam64 ID: {steam64}");
        steam64 = steam64.Trim().Replace("@steam", "");
        LogManager.Debug($"[CreditTag] Looking up tag for Steam64 ID: {steam64}");
        if (!steam64.All(char.IsDigit) || !SavedCreditTags.TryGetValue(steam64, out var savedTag))
            return false;
        tag = savedTag.BadgeName;
        color = savedTag.Color;
        LogManager.Debug($"[CreditTag] Found saved tag: {tag} with color: {color}");
        return true;
    }

    internal static void LoadCreditTags()
    {
        try
        {
            string resp;
            try
            {
                resp = HttpQuery.Get($"{ApiBase}/api/v1/credittags/");
            }
            catch (Exception ex)
            {
                LogManager.Error($"[CreditTag] HTTP request failed: {ex}");
                return;
            }

            var (statusCode, message) = ParseApiResponse(resp);

            if (statusCode != HttpStatusCode.OK)
                switch (statusCode)
                {
                    case HttpStatusCode.InternalServerError:
                        LogManager.Error("[CreditTag] Server error (500) while getting CreditTags.");
                        return;
                    default:
                        LogManager.Debug(
                            $"[CreditTag] Unexpected status code: {statusCode} - {message ?? "(no message)"}");
                        return;
                }

            using var doc = JsonDocument.Parse(resp);
            var root = doc.RootElement;

            if (!root.TryGetProperty("tags", out var tagsProp) || tagsProp.ValueKind != JsonValueKind.Array)
            {
                LogManager.Debug("[CreditTag] No tags array in response.");
                return;
            }

            foreach (var item in tagsProp.EnumerateArray())
            {
                if (!item.TryGetProperty("steam_id", out var hashProp) ||
                    hashProp.ValueKind != JsonValueKind.String ||
                    !item.TryGetProperty("badge_name", out var badgeProp) ||
                    badgeProp.ValueKind != JsonValueKind.String ||
                    !item.TryGetProperty("color", out var colorProp) || colorProp.ValueKind != JsonValueKind.String)
                    continue;
                var steamIdHash = hashProp.GetString();
                var badgeName = badgeProp.GetString();
                var color = colorProp.GetString();

                if (string.IsNullOrEmpty(steamIdHash) || string.IsNullOrEmpty(badgeName) || string.IsNullOrEmpty(color))
                    continue;

                SavedCreditTags[steamIdHash] = new CreditTag
                {
                    BadgeName = badgeName,
                    Color = color
                };

                LogManager.Debug($"[CreditTag] Loaded tag for Tag: {badgeName}, Color: {color}");
            }
        }
        catch (Exception e)
        {
            LogManager.Error($"[CreditTag] Failed to load credit tag.\n{e}");
        }
    }

    internal static bool TryGetLanguages(out Dictionary<string, string> languages)
    {
        languages = GetLanguages();
        return languages is { Count: > 0 };
    }

    private static Dictionary<string, string> GetLanguages()
    {
        var resp = HttpQuery.Get($"{ApiBase}/api/v1/languages");
        var (statusCode, message) = ParseApiResponse(resp);

        if (statusCode != HttpStatusCode.OK)
        {
            LogManager.Error($"Failed to get languages: {statusCode} - {message ?? "(no message)"}");
            return new Dictionary<string, string>();
        }

        var result = new Dictionary<string, string>();
        var root = JsonDocument.Parse(resp).RootElement;
        if (!root.TryGetProperty("languages", out var languagesProp) ||
            languagesProp.ValueKind != JsonValueKind.Array) return result;
        foreach (var lang in languagesProp.EnumerateArray())
            if (lang.TryGetProperty("code", out var codeProp) && codeProp.ValueKind == JsonValueKind.String &&
                lang.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
            {
                var code = codeProp.GetString();
                var name = nameProp.GetString();
                if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(name)) continue;
                result[code] = nameProp.GetString();
            }

        return result;
    }

    private static (HttpStatusCode StatusCode, string Message) ParseApiResponse(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var statusCode = HttpStatusCode.InternalServerError;
            string message = null;

            if (root.TryGetProperty("status", out var statusProp) && statusProp.ValueKind == JsonValueKind.Number)
                statusCode = (HttpStatusCode)statusProp.GetInt32();

            if (root.TryGetProperty("message", out var messageProp) && messageProp.ValueKind == JsonValueKind.String)
                message = messageProp.GetString();

            return (statusCode, message);
        }
        catch (Exception e)
        {
            LogManager.Error("Failed to parse API response.");
            LogManager.Debug($"ParseApiResponse failed.\n{e}");
            return (HttpStatusCode.InternalServerError, null);
        }
    }

    internal static bool TryGetPluginTranslations(string language, out object translations)
    {
        translations = null;
        var name = AutoEvent.Singleton.Name;
        if (string.IsNullOrWhiteSpace(language))
            return false;

        try
        {
            LogManager.Debug($"[Translations] Fetching translations for {name}/{language}");
            var url =
                $"{ApiBase}/api/v1/plugin/{Uri.EscapeDataString(name)}/translations/{Uri.EscapeDataString(language)}";
            var resp = HttpQuery.Get(url);

            var (statusCode, message) = ParseApiResponse(resp);
            if (statusCode != HttpStatusCode.OK)
                switch (statusCode)
                {
                    case HttpStatusCode.NotFound:
                        LogManager.Debug($"[Translations] Plugin or language not found: {name}/{language}");
                        return false;
                    case HttpStatusCode.InternalServerError:
                        LogManager.Error(
                            $"[Translations] Server error while fetching translations for {name}/{language}");
                        return false;
                    default:
                        LogManager.Debug(
                            $"[Translations] Unexpected status: {statusCode} - {message ?? "(no message)"}");
                        return false;
                }

            using var doc = JsonDocument.Parse(resp);
            var root = doc.RootElement;
            if (!root.TryGetProperty("translations", out var translationsProp) ||
                translationsProp.ValueKind != JsonValueKind.Object)
            {
                LogManager.Debug($"[Translations] No translations object in response for {name}/{language}");
                return false;
            }

            translations = ConvertJsonElement(translationsProp);

            return translations is not null;
        }
        catch (Exception ex)
        {
            LogManager.Error($"[Translations] Exception while fetching translations for {name}/{language}: {ex}");
            translations = null;
            return false;
        }
    }

    private static object ConvertJsonElement(JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                var dict = new Dictionary<string, object>();
                foreach (var prop in el.EnumerateObject()) dict[prop.Name] = ConvertJsonElement(prop.Value);
                return dict;
            case JsonValueKind.Array:
                var list = new List<object>();
                foreach (var item in el.EnumerateArray()) list.Add(ConvertJsonElement(item));
                return list;
            case JsonValueKind.String:
                return el.GetString();
            case JsonValueKind.Number:
                if (el.TryGetInt64(out var l)) return l;
                if (el.TryGetDouble(out var d)) return d;
                return el.GetDecimal();
            case JsonValueKind.True:
            case JsonValueKind.False:
                return el.GetBoolean();
            case JsonValueKind.Null:
                return null;
            default:
                return el.ToString();
        }
    }

    private class CreditTag
    {
        public string BadgeName { get; init; }
        public string Color { get; init; }
    }
}