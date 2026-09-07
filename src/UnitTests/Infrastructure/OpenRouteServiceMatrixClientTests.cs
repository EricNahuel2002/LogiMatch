using System.Text.Json;
using Application.Integrations;
using Domain.ValueObjects;
using Infrastructure.Integrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTests.Infrastructure;

public class OpenRouteServiceMatrixClientTests
{
    private static OpenRouteServiceMatrixClient CreateClient(
        MockHttpMessageHandler handler,
        Dictionary<string, string?>? config = null)
    {
        var data = new Dictionary<string, string?>
        {
            ["OpenRouteService:BaseUrl"] = "https://api.heigit.org/openrouteservice/",
            ["OpenRouteService:ApiKey"] = "test-key"
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

        return new OpenRouteServiceMatrixClient(
            httpClient,
            configuration,
            NullLogger<OpenRouteServiceMatrixClient>.Instance);
    }

    private string ReadRequestBody(MockHttpMessageHandler handler)
    {
        var body = handler.LastRequest!.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
        return body;
    }

    private static RouteDistanceOrigin Origin(Guid driverId, decimal lat, decimal lon)
    {
        return new RouteDistanceOrigin(driverId, new Coordinate(lat, lon));
    }

    [Fact]
    public async Task GetDrivingDistancesAsync_ReturnsDistancesPerDriver()
    {
        var driverA = Guid.NewGuid();
        var driverB = Guid.NewGuid();
        string? capturedBody = null;

        var handler = new MockHttpMessageHandler(request =>
        {
            capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return MockHttpMessageHandlerResponses.Json(
                """{ "distances": [[5000], [1000]], "durations": [[420], [90]] }""");
        });
        var client = CreateClient(handler);

        var result = await client.GetDrivingDistancesAsync(
            [Origin(driverA, -34.5m, -58.3m), Origin(driverB, -34.7m, -58.5m)],
            new Coordinate(-34.6m, -58.4m));

        Assert.Equal(5000, result[driverA]);
        Assert.Equal(1000, result[driverB]);

        Assert.EndsWith("/v2/matrix/driving-car", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer test-key", handler.LastRequest.Headers.Authorization?.ToString());

        using var body = JsonDocument.Parse(capturedBody!);
        var root = body.RootElement;

        var locations = root.GetProperty("locations").EnumerateArray().Select(e => e.EnumerateArray().Select(v => v.GetDecimal()).ToArray()).ToArray();
        Assert.Equal(3, locations.Length);
        Assert.Equal(new[] { -58.3m, -34.5m }, locations[0]);
        Assert.Equal(new[] { -58.5m, -34.7m }, locations[1]);
        Assert.Equal(new[] { -58.4m, -34.6m }, locations[2]);
        Assert.Equal(new[] { 0, 1 }, root.GetProperty("sources").EnumerateArray().Select(e => e.GetInt32()));
        Assert.Equal(new[] { 2 }, root.GetProperty("destinations").EnumerateArray().Select(e => e.GetInt32()));
        Assert.Equal("distance", root.GetProperty("metrics")[0].GetString());
        Assert.Equal("m", root.GetProperty("units").GetString());
    }

    [Fact]
    public async Task GetDrivingDistancesAsync_NullDistance_OmitsDriver()
    {
        var driverWithoutRoute = Guid.NewGuid();
        var driverWithRoute = Guid.NewGuid();

        var handler = new MockHttpMessageHandler(_ =>
            MockHttpMessageHandlerResponses.Json("""{ "distances": [[null], [1000]] }"""));
        var client = CreateClient(handler);

        var result = await client.GetDrivingDistancesAsync(
            [Origin(driverWithoutRoute, -34.5m, -58.3m), Origin(driverWithRoute, -34.7m, -58.5m)],
            new Coordinate(-34.6m, -58.4m));

        Assert.False(result.ContainsKey(driverWithoutRoute));
        Assert.Equal(1000, result[driverWithRoute]);
    }

    [Fact]
    public async Task GetDrivingDistancesAsync_WhenApiKeyNotConfigured_Throws()
    {
        var handler = new MockHttpMessageHandler(_ => MockHttpMessageHandlerResponses.Json("{}"));
        var client = CreateClient(handler, new Dictionary<string, string?>
        {
            ["OpenRouteService:ApiKey"] = ""
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetDrivingDistancesAsync([], new Coordinate(-34.6m, -58.4m)));
    }

    [Fact]
    public async Task GetDrivingDistancesAsync_WhenApiFails_Throws()
    {
        var handler = new MockHttpMessageHandler(_ =>
            MockHttpMessageHandlerResponses.Failure(System.Net.HttpStatusCode.TooManyRequests));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetDrivingDistancesAsync(
                [Origin(Guid.NewGuid(), -34.5m, -58.3m)],
                new Coordinate(-34.6m, -58.4m)));
    }
}