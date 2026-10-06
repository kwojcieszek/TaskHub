using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaskHub.IntegrationTests.Support;

/// <summary>
/// The JSON conventions of the API: camelCase names and enums written as text.
/// </summary>
public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
