using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConfigurationComponent.Utilities;

/// <summary>
/// Represents Json Defaults.
/// </summary>
public static class JsonDefaults
{
    /// <summary>
    /// Executes Create.
    /// </summary>
    public static JsonSerializerOptions Create(bool indented = true)
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = indented,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
    }
}
