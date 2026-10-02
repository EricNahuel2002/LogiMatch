using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Integrations;

/// <summary>
/// Shared transport for the OpenRouteService matrix endpoint. Owns the request shape,
/// the bearer authentication and the error translation, so every matrix client in this
/// assembly talks to the API the same way.
/// </summary>
internal sealed class OpenRouteServiceMatrixGateway
{
    private const string MatrixPath = "v2/matrix/driving-car";

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger _logger;

    public OpenRouteServiceMatrixGateway(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger logger)
    {
        _httpClient = httpClient;
        _apiKey = configuration["OpenRouteService:ApiKey"] ?? string.Empty;
        _logger = logger;
    }

    public void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogError("OpenRouteService API key is not configured.");
            throw new InvalidOperationException("Route metrics service is unavailable.");
        }
    }

    /// <summary>
    /// Requests distance and duration for a single block. Indices are offsets inside
    /// <paramref name="locations" />. Each returned block is
    /// <c>rows.Length x columns.Length</c>, or null when the API omitted the metric.
    /// </summary>
    public async Task<MatrixBlock> GetBlockAsync(
        decimal[][] locations,
        int[] rows,
        int[] columns,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var requestBody = new MatrixRequest
        {
            Locations = locations,
            Sources = rows,
            Destinations = columns,
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

        return new MatrixBlock(matrix?.Distances, matrix?.Durations);
    }

    internal sealed record MatrixBlock(double?[][]? Distances, double?[][]? Durations);

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
