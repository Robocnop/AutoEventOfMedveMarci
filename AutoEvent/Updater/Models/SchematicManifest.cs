using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AutoEvent.Updater.Models;

internal class SchematicManifest
{
    [JsonPropertyName("schematics")] public List<SchematicEntry> Schematics { get; set; } = new();
}