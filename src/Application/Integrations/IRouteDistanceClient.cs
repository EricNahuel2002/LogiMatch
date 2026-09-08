using Domain.ValueObjects;

namespace Application.Integrations;

public sealed record RouteDistanceOrigin(Guid DriverId, Coordinate Location);

public interface IRouteDistanceClient
{
    Task<IReadOnlyDictionary<Guid, int>> GetDrivingDistancesAsync(
        IReadOnlyCollection<RouteDistanceOrigin> origins,
        Coordinate destination,
        CancellationToken cancellationToken = default);
}