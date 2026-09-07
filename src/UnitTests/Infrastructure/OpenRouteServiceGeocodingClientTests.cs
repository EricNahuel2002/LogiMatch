using Infrastructure.Integrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTests.Infrastructure;

public class OpenRouteServiceGeocodingClientTests
{
    private static MockHttpMessageHandler BuildHandler(string responseJson)
    {
        return new MockHttpMessageHandler(_ => MockHttpMessageHandlerResponses.Json(responseJson));
    }

    private static OpenRouteServiceGeocodingClient CreateClient(
        MockHttpMessageHandler handler,
        Dictionary<string, string?>? config = null)
    {
        var data = new Dictionary<string, string?>
        {
            ["OpenRouteService:BaseUrl"] = "https://api.heigit.org/openrouteservice/",
            ["OpenRouteService:ApiKey"] = "test-key",
            ["OpenRouteService:Country"] = "AR"
        };

        if (config is not null)
        {
            foreach (var (key, value) in config)
            {
                data[key] = value;
            }
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .Build();

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.heigit.org/openrouteservice/")
        };

        return new OpenRouteServiceGeocodingClient(
            httpClient,
            configuration,
            NullLogger<OpenRouteServiceGeocodingClient>.Instance);
    }

    [Fact]
    public async Task GeocodeAsync_SendsFormattedRequestAndReturnsCoordinate()
    {
        var handler = BuildHandler(
            """
            {
              "type": "FeatureCollection",
              "features": [
                {
                  "geometry": { "type": "Point", "coordinates": [-58.3816, -34.6083] }
                }
              ]
            }
            """);
        var client = CreateClient(handler);

        var coordinate = await client.GeocodeAsync("Av. Rivadavia 123");

        Assert.Equal(-34.6083m, coordinate.Latitude);
        Assert.Equal(-58.3816m, coordinate.Longitude);

        var requestUri = handler.LastRequest!.RequestUri!;
        Assert.EndsWith("/geocode/search", requestUri.AbsolutePath);
        Assert.Contains("boundary.country=AR", requestUri.Query);
        Assert.Contains("size=1", requestUri.Query);
        Assert.Contains("api_key=test-key", requestUri.Query);
        Assert.Contains(Uri.EscapeDataString("Av. Rivadavia 123"), requestUri.Query);
    }

    [Fact]
    public async Task GeocodeAsync_WhenNoResults_Throws()
    {
        var client = CreateClient(BuildHandler("""{ "type": "FeatureCollection", "features": [] }"""));

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GeocodeAsync("Nonexistent Place 9999"));
    }

    [Fact]
    public async Task GeocodeAsync_WhenApiKeyNotConfigured_Throws()
    {
        var client = CreateClient(BuildHandler("""{ "features": [] }"""), new Dictionary<string, string?>
        {
            ["OpenRouteService:ApiKey"] = ""
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GeocodeAsync("Av. Rivadavia 123"));
    }

    [Fact]
    public async Task GeocodeAsync_WhenApiFails_Throws()
    {
        var handler = new MockHttpMessageHandler(_ => MockHttpMessageHandlerResponses.Failure(
            System.Net.HttpStatusCode.BadRequest));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GeocodeAsync("Av. Rivadavia 123"));
    }
}