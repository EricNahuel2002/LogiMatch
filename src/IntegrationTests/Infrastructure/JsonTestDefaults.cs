using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IntegrationTests.Infrastructure;

internal static class JsonTestDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}

public static class TestHttpContentExtensions
{
    public static Task<T?> ReadJsonAsync<T>(
        this HttpContent content,
        CancellationToken cancellationToken = default) =>
        content.ReadFromJsonAsync<T>(JsonTestDefaults.Options, cancellationToken);
}
