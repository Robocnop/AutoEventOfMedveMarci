using System.Text.Json.Serialization;

namespace AutoEvent.Updater.Models;

internal class SchematicEntry
{
    [JsonPropertyName("name")] public string Name { get; set; }

    [JsonPropertyName("version")] public string Version { get; set; }

    [JsonPropertyName("projectmer_file")] public string ProjectMerFile { get; set; }

    [JsonPropertyName("tme_file")] public string TmeFile { get; set; }

    [JsonPropertyName("changelog")] public string Changelog { get; set; }
}