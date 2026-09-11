using Application.Integrations;
using Domain.ValueObjects;

namespace IntegrationTests.Infrastructure;

public sealed class RecordingRouteClient : IRouteClient
{
    public int DistanceMeters { get; set; } = 5_000;
    public int DurationMinutes { get; set; } = 30;
    public bool Unavailable { get; set; }

    public Task<IReadOnlyDictionary<Guid, RouteMetrics>> GetDrivingMetricsAsync(
        IReadOnlyCollection<RouteOrigin> origins,
        Coordinate destination,
        CancellationToken cancellationToken = default)
    {
        if (Unavailable)
        {
            throw new InvalidOperationException("Route metrics service is unavailable.");
        }

        var result = origins.ToDictionary(
            o => o.DriverId,
            _ => new RouteMetrics(DistanceMeters, DurationMinutes));

        return Task.FromResult<IReadOnlyDictionary<Guid, RouteMetrics>>(result);
    }
}