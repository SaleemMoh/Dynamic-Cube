using System.Text.Json;
using System.Text.Json.Serialization;

namespace DynamicCube.Modules.AppsControl.Changes;

/// <summary>JSON conventions for change payloads and the Apps Control API: camelCase, enums as UPPER_SNAKE text.</summary>
public static class ChangeJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    public static void Configure(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.PropertyNameCaseInsensitive = true;
        options.NumberHandling = JsonNumberHandling.AllowReadingFromString;
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
    }

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Configure(options);
        return options;
    }
}
