#nullable enable
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AutoEvent.ApiFeatures;

namespace AutoEvent;

public abstract class CreditTag
{
    private static readonly HttpClient HttpClient = new();
    private static readonly ConcurrentDictionary<string, TagInfo> Tags = new();
    private static DateTime _lastFetchTime = DateTime.MinValue;
    private static readonly TimeSpan MinFetchInterval = TimeSpan.FromMinutes(10);

    internal static void GetTagsFromGithub()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await RefreshTagsFromGithubAsync().ConfigureAwait(false);
            }
            catch
            {
                LogManager.Error("[CreditTag] Exception occurred while getting tags from GitHub.");
            }
        });
    }

    internal static bool TryGetTag(string steam64, out string tag, out string color)
    {
        tag = string.Empty;
        color = string.Empty;
        if (string.IsNullOrWhiteSpace(steam64))
            return false;
        LogManager.Debug($"[CreditTag] Original Steam64 ID: {steam64}");
        steam64 = steam64.Trim().Replace("@steam", "");
        LogManager.Debug($"[CreditTag] Looking up tag for Steam64 ID: {steam64}");
        if (!steam64.All(char.IsDigit))
            return false;

        var canonical = steam64.ToLowerInvariant();
        var hash = ComputeSha256Hex(canonical);
        LogManager.Debug($"[CreditTag] Computed hash for {canonical}: {hash}");
        LogManager.Debug($"[CreditTag] Available tags: {string.Join(", ", Tags.Keys)}");
        if (!Tags.TryGetValue(hash, out var info))
            return false;

        tag = info.Tag ?? string.Empty;
        color = info.Color ?? string.Empty;
        return true;
    }

    private static async Task RefreshTagsFromGithubAsync()
    {
        if (DateTime.UtcNow - _lastFetchTime < MinFetchInterval)
            return;

        _lastFetchTime = DateTime.UtcNow;

        try
        {
            const string url = "https://raw.githubusercontent.com/MedveMarci/AutoEvent/dev-labapi/AutoEvent/Tags.json";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var response = await HttpClient.GetAsync(url, cts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return;

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("Tags", out var tagsElement) ||
                tagsElement.ValueKind != JsonValueKind.Object)
                return;

            var newTags = new ConcurrentDictionary<string, TagInfo>();
            foreach (var property in tagsElement.EnumerateObject())
            {
                var hash = property.Name.Trim();
                if (string.IsNullOrEmpty(hash) || !IsValidSha256Hash(hash))
                    continue;

                if (property.Value.ValueKind != JsonValueKind.Object)
                    continue;

                var valueObj = property.Value;
                if (!valueObj.TryGetProperty("Tag", out var tagProp) || tagProp.ValueKind != JsonValueKind.String)
                    continue;

                var tagText = tagProp.GetString();
                if (string.IsNullOrWhiteSpace(tagText))
                    continue;

                string? color = null;
                if (valueObj.TryGetProperty("Color", out var colorProp) && colorProp.ValueKind == JsonValueKind.String)
                    color = colorProp.GetString();

                newTags[hash] = new TagInfo
                {
                    Tag = tagText!,
                    Color = color
                };
            }

            if (newTags.Count > 0)
            {
                Tags.Clear();
                foreach (var kvp in newTags)
                    Tags[kvp.Key] = kvp.Value;
            }
        }
        catch
        {
            LogManager.Error("[CreditTag] Failed to refresh tags from GitHub.");
        }
    }

    internal static string ComputeSha256Hex(string input)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = sha.ComputeHash(bytes);
        var sb = new StringBuilder(hashBytes.Length * 2);
        foreach (var b in hashBytes)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    private static bool IsValidSha256Hash(string hash)
    {
        return hash.Length == 64 && hash.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
    }

    private sealed class TagInfo
    {
        public string? Tag { get; set; }
        public string? Color { get; set; }
    }
}