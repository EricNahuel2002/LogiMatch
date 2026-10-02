using Application.Integrations;
using Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Integrations;

public sealed class OpenRouteServiceMatrixClient : IRouteClient
{
    private const int MaxOriginsPerRequest = 69;

    private readonly OpenRouteServiceMatrixGateway _gateway;

    public OpenRouteServiceMatrixClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<OpenRouteServiceMatrixClient> logger)
    {
        _gateway = new OpenRouteServiceMatrixGateway(httpClient, configuration, logger);
    }

    public async Task<IReadOnlyDictionary<Guid, RouteMetrics>> GetDrivingMetricsAsync(
        IReadOnlyCollection<RouteOrigin> origins,
        Coordinate destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(origins);

        _gateway.EnsureConfigured();

        var result = new Dictionary<Guid, RouteMetrics>();

        foreach (var chunk in origins.Chunk(MaxOriginsPerRequest))
        {
            var locations = chunk
                .Select(o => new[] { o.Location.Longitude, o.Location.Latitude })
                .Concat([[destination.Longitude, destination.Latitude]])
                .ToArray();

            var sources = Enumerable.Range(0, chunk.Length).ToArray();
            var destinations = new[] { chunk.Length };

            var block = await _gateway.GetBlockAsync(locations, sources, destinations, cancellationToken);

            if (block.Distances is null)
            {
                continue;
            }

            for (var i = 0; i < chunk.Length; i++)
            {
                var distanceRow = block.Distances[i];
                var distance = distanceRow is { Length: > 0 } ? distanceRow[0] : null;

                var durationRow = block.Durations is null || block.Durations.Length <= i
                    ? null
                    : block.Durations[i];
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
}
