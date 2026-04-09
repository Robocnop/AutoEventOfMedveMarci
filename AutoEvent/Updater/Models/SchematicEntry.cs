using System.Text.Json.Serialization;

namespace AutoEvent.Updater.Models;

internal class SchematicEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonPropertyName("file")]
    public string File { get; set; }

    [JsonPropertyName("changelog")]
    public string Changelog { get; set; }
}
