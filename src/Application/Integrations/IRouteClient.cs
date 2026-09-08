using Domain.ValueObjects;

namespace Application.Integrations;

public sealed record RouteOrigin(Guid DriverId, Coordinate Location);

public sealed record RouteMetrics(int DistanceMeters, int DurationMinutes);

public interface IRouteClient
{
    Task<IReadOnlyDictionary<Guid, RouteMetrics>> GetDrivingMetricsAsync(
        IReadOnlyCollection<RouteOrigin> origins,
        Coordinate destination,
        CancellationToken cancellationToken = default);
}