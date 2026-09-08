using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Integrations;
using Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Integrations;

public sealed class OpenRouteServiceMatrixClient : IRouteClient
{
    private const int MaxOriginsPerRequest = 69;
    private const string MatrixPath = "v2/matrix/driving-car";

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<OpenRouteServiceMatrixClient> _logger;

    public OpenRouteServiceMatrixClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<OpenRouteServiceMatrixClient> logger)
    {
        _httpClient = httpClient;
        _apiKey = configuration["OpenRouteService:ApiKey"] ?? string.Empty;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<Guid, RouteMetrics>> GetDrivingMetricsAsync(
        IReadOnlyCollection<RouteOrigin> origins,
        Coordinate destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(origins);

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogError("OpenRouteService API key is not configured.");
            throw new InvalidOperationException("Route metrics service is unavailable.");
        }

        var result = new Dictionary<Guid, RouteMetrics>();

        foreach (var chunk in origins.Chunk(MaxOriginsPerRequest))
        {
            var originLocations = chunk
                .Select(o => new[] { o.Location.Longitude, o.Location.Latitude })
                .ToArray();

            var locations = originLocations
                .Concat([[destination.Longitude, destination.Latitude]])
                .ToArray();

            var requestBody = new MatrixRequest
            {
                Locations = locations,
                Sources = Enumerable.Range(0, chunk.Length).ToArray(),
                Destinations = [chunk.Length],
                Metrics = ["distance", "duration"],
                Units = "m"
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, MatrixPath);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = JsonContent.Create(requestBody);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "OpenRouteService matrix failed ({StatusCode}): {Body}",
                    response.StatusCode,
                    errorBody);
                throw new InvalidOperationException("Route metrics service is unavailable.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var matrix = await JsonSerializer.DeserializeAsync<MatrixResponse>(
                stream,
                cancellationToken: cancellationToken);

            if (matrix?.Distances is null)
            {
                continue;
            }

            for (var i = 0; i < chunk.Length; i++)
            {
                var distanceRow = matrix.Distances[i];
                var distance = distanceRow is { Length: > 0 } ? distanceRow[0] : null;

                var durationRow = matrix.Durations is null || matrix.Durations.Length <= i
                    ? null
                    : matrix.Durations[i];
                var duration = durationRow is { Length: > 0 } ? durationRow[0] : null;

                if (distance is { } meters && duration is { } seconds)
                {
                    result.TryAdd(
                        chunk[i].DriverId,
                        new RouteMetrics(
                            (int)Math.Round(meters),
                            (int)Math.Ceiling(seconds / 60.0)));
                }
            }
        }

        return result;
    }

    private sealed class MatrixRequest
    {
        public decimal[][] Locations { get; init; } = [];
        public int[] Sources { get; init; } = [];
        public int[] Destinations { get; init; } = [];
        public string[] Metrics { get; init; } = [];
        public string Units { get; init; } = string.Empty;
    }

    private sealed class MatrixResponse
    {
        [JsonPropertyName("distances")]
        public double?[][]? Distances { get; set; }

        [JsonPropertyName("durations")]
        public double?[][]? Durations { get; set; }
    }
}