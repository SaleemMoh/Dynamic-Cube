using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DynamicCube.Platform.Persistence;

/// <summary>Stores small typed object graphs as JSON text in a <c>jsonb</c> column.</summary>
public static class JsonColumn
{
    public static ValueConverter<T, string> Converter<T>()
        where T : new() =>
        new(v => JsonSerializer.Serialize(v, JsonSerializerOptions.Web), v => JsonSerializer.Deserialize<T>(v, JsonSerializerOptions.Web) ?? new T());

    public static ValueComparer<T> Comparer<T>()
        where T : new() =>
        new(
            (a, b) => JsonSerializer.Serialize(a, JsonSerializerOptions.Web) == JsonSerializer.Serialize(b, JsonSerializerOptions.Web),
            v => JsonSerializer.Serialize(v, JsonSerializerOptions.Web).GetHashCode(StringComparison.Ordinal),
            v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, JsonSerializerOptions.Web), JsonSerializerOptions.Web) ?? new T());
}
