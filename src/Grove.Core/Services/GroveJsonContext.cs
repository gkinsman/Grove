using System.Text.Json.Serialization;
using Grove.Core.Models;

namespace Grove.Core.Services;

[JsonSerializable(typeof(GroveConfig))]
[JsonSerializable(typeof(List<RootConfig>))]
[JsonSerializable(typeof(List<CommandPreset>))]
[JsonSerializable(typeof(Dictionary<string, WorktreeConfig>))]
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
public partial class GroveJsonContext : JsonSerializerContext
{
}
