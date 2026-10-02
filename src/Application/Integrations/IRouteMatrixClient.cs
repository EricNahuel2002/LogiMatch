using Application.RoutePlanning;
using Domain.ValueObjects;

namespace Application.Integrations;

public interface IRouteMatrixClient
{
    /// <summary>
    /// Resolves the distance (meters) and duration (seconds) between every pair of
    /// <paramref name="locations" />. Row and column order of the returned matrices match
    /// the order of <paramref name="locations" />.
    /// </summary>
    Task<RouteMatrixSet> GetMatrixAsync(
        IReadOnlyList<Coordinate> locations,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Distance and duration matrices over the same set of locations.
/// </summary>
public sealed record RouteMatrixSet(RouteMatrix Distances, RouteMatrix Durations)
{
    public bool HasCompleteData => Distances.HasCompleteData && Durations.HasCompleteData;
}
