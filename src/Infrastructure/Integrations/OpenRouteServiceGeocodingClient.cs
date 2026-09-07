using System.Text.Json;
using Application.Integrations;
using Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Integrations;

public sealed class OpenRouteServiceGeocodingClient : IGeocodingClient
{
    private const string GeocodeSearchPath = "geocode/search";

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _country;
    private readonly ILogger<OpenRouteServiceGeocodingClient> _logger;

    public OpenRouteServiceGeocodingClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<OpenRouteServiceGeocodingClient> logger)
    {
        _httpClient = httpClient;
        _apiKey = configuration["OpenRouteService:ApiKey"] ?? string.Empty;
        _country = configuration["OpenRouteService:Country"] ?? "AR";
        _logger = logger;
    }

    public async Task<Coordinate> GeocodeAsync(string address, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogError("OpenRouteService API key is not configured.");
            throw new InvalidOperationException("Geocoding service is unavailable.");
        }

        var query = QueryStringBuilder.Build(
            ("text", address),
            ("boundary.country", _country),
            ("size", "1"),
            ("api_key", _apiKey));

        using var response = await _httpClient.GetAsync(
            $"{GeocodeSearchPath}?{query}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "OpenRouteService geocoding failed ({StatusCode}): {Body}",
                response.StatusCode,
                errorBody);
            throw new InvalidOperationException("Geocoding service is unavailable.");
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var document = JsonDocument.Parse(content);
        var root = document.RootElement;

        var features = root.TryGetProperty("features", out var featuresElement)
            ? featuresElement
            : default;

        if (features.ValueKind != JsonValueKind.Array || features.GetArrayLength() == 0)
        {
            _logger.LogError("OpenRouteService geocoding returned no results for address '{Address}'.", address);
            throw new InvalidOperationException("Geocoding service is unavailable.");
        }

        var coordinates = features[0].GetProperty("geometry").GetProperty("coordinates");

        var longitude = coordinates[0].GetDecimal();
        var latitude = coordinates[1].GetDecimal();

        return new Coordinate(latitude, longitude);
    }
}

internal static class QueryStringBuilder
{
    public static string Build(params (string Key, string Value)[] parameters)
    {
        return string.Join(
            "&",
            parameters.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
    }
}