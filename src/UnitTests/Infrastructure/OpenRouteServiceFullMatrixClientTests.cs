using System.Text.Json;
using Application.RoutePlanning;
using Domain.ValueObjects;
using Infrastructure.Integrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTests.Infrastructure;

public class OpenRouteServiceFullMatrixClientTests
{
    private static OpenRouteServiceFullMatrixClient CreateClient(
        MockHttpMessageHandler handler,
        string apiKey = "test-key")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenRouteService:BaseUrl"] = "https://api.heigit.org/openrouteservice/",
                ["OpenRouteService:ApiKey"] = apiKey
            })
            .Build();

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.heigit.org/openrouteservice/")
        };

        return new OpenRouteServiceFullMatrixClient(
            httpClient,
            configuration,
            NullLogger<OpenRouteServiceFullMatrixClient>.Instance);
    }

    private static List<Coordinate> Locations(int count) =>
        Enumerable.Range(0, count)
            .Select(i => new Coordinate(-34.6m + (i * 0.001m), -58.4m + (i * 0.001m)))
            .ToList();

    /// <summary>
    /// Answers every request with a block whose cell values are derived from the global
    /// source and destination indices, so a symmetric matrix can be verified end to end.
    /// </summary>
    private static MockHttpMessageHandler RespondingWithDerivedValues(
        ICollection<JsonElement>? capturedBodies = null)
    {
        return new MockHttpMessageHandler(request =>
        {
            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            using var parsed = JsonDocument.Parse(body);
            capturedBodies?.Add(parsed.RootElement.Clone());

            var sources = parsed.RootElement.GetProperty("sources")
                .EnumerateArray().Select(e => e.GetInt32()).ToArray();
            var destinations = parsed.RootElement.GetProperty("destinations")
                .EnumerateArray().Select(e => e.GetInt32()).ToArray();

            var distances = sources.Select(s => destinations
                .Select(d => (double?)Cell(s, d, offset: 1, step: 1000))
                .ToArray())
                .ToArray();

            var durations = sources.Select(s => destinations
                .Select(d => (double?)Cell(s, d, offset: 2, step: 7))
                .ToArray())
                .ToArray();

            return MockHttpMessageHandlerResponses.Json(JsonSerializer.Serialize(new
            {
                distances,
                durations
            }));
        });
    }

    private static long Cell(int from, int to, long offset, long step) =>
        from == to ? 0 : (from * step) + (to * step) + offset;

    [Fact]
    public async Task GetMatrixAsync_SingleBlock_RequestsEveryPairOnce()
    {
        var bodies = new List<JsonElement>();
        var client = CreateClient(RespondingWithDerivedValues(bodies));

        var result = await client.GetMatrixAsync(Locations(4));

        // 4 locations fit in a single block, so one request covers the whole matrix.
        var body = Assert.Single(bodies);
        Assert.Equal(
            new[] { 0, 1, 2, 3 },
            body.GetProperty("sources").EnumerateArray().Select(e => e.GetInt32()));
        Assert.Equal(
            new[] { 0, 1, 2, 3 },
            body.GetProperty("destinations").EnumerateArray().Select(e => e.GetInt32()));
        Assert.Equal("distance", body.GetProperty("metrics")[0].GetString());
        Assert.Equal("duration", body.GetProperty("metrics")[1].GetString());
        Assert.Equal("m", body.GetProperty("units").GetString());

        Assert.True(result.HasCompleteData);
        Assert.Equal(4, result.Distances.Size);

        for (var from = 0; from < 4; from++)
        {
            for (var to = 0; to < 4; to++)
            {
                Assert.Equal(Cell(from, to, 1, 1000), result.Distances[from, to]);
                Assert.Equal(Cell(from, to, 2, 7), result.Durations[from, to]);
            }
        }
    }

    [Fact]
    public async Task GetMatrixAsync_UsesLongitudeLatitudeOrder()
    {
        JsonElement? captured = null;
        var handler = new MockHttpMessageHandler(request =>
        {
            captured = JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult()).RootElement.Clone();
            return MockHttpMessageHandlerResponses.Json("""{ "distances": [[0, 1], [1, 0]] }""");
        });
        var client = CreateClient(handler);

        await client.GetMatrixAsync(
        [
            new Coordinate(-34.6037m, -58.3816m),
            new Coordinate(-34.6m, -58.4m)
        ]);

        Assert.NotNull(captured);
        var locations = captured!.Value.GetProperty("locations").EnumerateArray()
            .Select(e => e.EnumerateArray().Select(v => v.GetDecimal()).ToArray())
            .ToArray();

        Assert.Equal(new[] { -58.3816m, -34.6037m }, locations[0]);
        Assert.Equal(new[] { -58.4m, -34.6m }, locations[1]);
    }

    [Fact]
    public async Task GetMatrixAsync_FillsLowerTriangleByTransposition()
    {
        // The client only asks for the upper triangle, so the reverse cells prove the
        // symmetry assumption is applied instead of issuing a request per direction.
        var client = CreateClient(RespondingWithDerivedValues());

        var result = await client.GetMatrixAsync(Locations(3));

        for (var from = 0; from < 3; from++)
        {
            for (var to = 0; to < 3; to++)
            {
                Assert.Equal(result.Distances[from, to], result.Distances[to, from]);
                Assert.Equal(result.Durations[from, to], result.Durations[to, from]);
            }
        }
    }

    [Fact]
    public async Task GetMatrixAsync_MoreLocationsThanBlockSize_SplitsIntoBlockTriangularRequests()
    {
        var bodies = new List<JsonElement>();
        var client = CreateClient(RespondingWithDerivedValues(bodies));
        var locations = Locations(OpenRouteServiceFullMatrixClient.MaxLocationsPerBlock + 1);

        var result = await client.GetMatrixAsync(locations);

        // Two blocks: (0,0), (0,1) and (1,1) cover all 51 locations, so 3 requests instead of
        // one per pair and instead of the full 2 x 2 grid.
        Assert.Equal(3, bodies.Count);

        var requestedPairs = bodies
            .Select(b => (
                Sources: b.GetProperty("sources").EnumerateArray().Select(e => e.GetInt32()).ToArray(),
                Destinations: b.GetProperty("destinations").EnumerateArray().Select(e => e.GetInt32()).ToArray()))
            .ToList();

        Assert.Equal(
            new (int Sources, int Destinations)[] { (50, 50), (50, 1), (1, 1) },
            requestedPairs
                .Select(p => (Sources: p.Sources.Length, Destinations: p.Destinations.Length))
                .ToArray());

        // Every request stays well below the 3500 pair limit.
        Assert.All(
            requestedPairs,
            pair => Assert.True(
                pair.Sources.Length * pair.Destinations.Length <= 3500,
                "A matrix request must respect the OpenRouteService pair limit."));

        // No direction is requested twice: the lower triangle comes from the transpose.
        var sourceDestinationPairs = requestedPairs
            .SelectMany(p => p.Sources.SelectMany(s => p.Destinations.Select(d => (s, d))))
            .ToList();
        Assert.Equal(sourceDestinationPairs.Count, sourceDestinationPairs.Distinct().Count());

        var size = locations.Count;
        Assert.Equal(size, result.Distances.Size);
        Assert.True(result.HasCompleteData);

        for (var from = 0; from < size; from++)
        {
            for (var to = 0; to < size; to++)
            {
                Assert.Equal(Cell(from, to, 1, 1000), result.Distances[from, to]);
            }
        }
    }

    [Fact]
    public async Task GetMatrixAsync_KeepsDiagonalAtZero()
    {
        // The derived mock returns non-zero values everywhere, so this proves the client
        // never lets a self distance reach the solver.
        var client = CreateClient(RespondingWithDerivedValues());

        var result = await client.GetMatrixAsync(Locations(5));

        for (var index = 0; index < 5; index++)
        {
            Assert.Equal(0, result.Distances[index, index]);
            Assert.Equal(0, result.Durations[index, index]);
        }
    }

    [Fact]
    public async Task GetMatrixAsync_NullCell_MarksMatrixIncompleteAndUnreachable()
    {
        var handler = new MockHttpMessageHandler(_ =>
            MockHttpMessageHandlerResponses.Json(
                """{ "distances": [[0, 100], [null, 0]], "durations": [[0, 10], [10, 0]] }"""));
        var client = CreateClient(handler);

        var result = await client.GetMatrixAsync(Locations(2));

        Assert.False(result.HasCompleteData);
        Assert.Equal(RouteMatrix.UnreachableValue, result.Distances[1, 0]);
        Assert.Equal(RouteMatrix.UnreachableValue, result.Distances[0, 1]);
    }

    [Fact]
    public async Task GetMatrixAsync_MissingDuration_MarksMatrixIncomplete()
    {
        var handler = new MockHttpMessageHandler(_ =>
            MockHttpMessageHandlerResponses.Json("""{ "distances": [[0, 100], [100, 0]] }"""));
        var client = CreateClient(handler);

        var result = await client.GetMatrixAsync(Locations(2));

        Assert.False(result.HasCompleteData);
        Assert.Equal(RouteMatrix.UnreachableValue, result.Durations[0, 1]);
    }

    [Fact]
    public async Task GetMatrixAsync_NoLocations_Throws()
    {
        var client = CreateClient(RespondingWithDerivedValues());

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetMatrixAsync([]));
    }

    [Fact]
    public async Task GetMatrixAsync_WhenApiKeyNotConfigured_Throws()
    {
        var client = CreateClient(RespondingWithDerivedValues(), apiKey: string.Empty);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetMatrixAsync(Locations(2)));
    }

    [Fact]
    public async Task GetMatrixAsync_WhenApiFails_Throws()
    {
        var handler = new MockHttpMessageHandler(_ =>
            MockHttpMessageHandlerResponses.Failure(System.Net.HttpStatusCode.TooManyRequests));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetMatrixAsync(Locations(2)));
    }
}
