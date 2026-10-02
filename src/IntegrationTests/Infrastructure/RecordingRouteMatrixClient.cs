using Application.Integrations;
using Application.RoutePlanning;
using Domain.ValueObjects;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// Stands in for OpenRouteService so a planning preview can be exercised end to end without
/// leaving the machine. It answers with a symmetric ring geometry, which makes every pair
/// reachable and every total predictable.
/// </summary>
public sealed class RecordingRouteMatrixClient : IRouteMatrixClient
{
    private const long BaseDistanceMeters = 100;
    private const long BaseDurationSeconds = 60;

    public IReadOnlyList<Coordinate> RequestedLocations { get; private set; } = [];

    public int CallCount { get; private set; }

    /// <summary>When set, the answer is marked incomplete to exercise the conflict path.</summary>
    public bool Incomplete { get; set; }

    public bool Unavailable { get; set; }

    public Task<RouteMatrixSet> GetMatrixAsync(
        IReadOnlyList<Coordinate> locations,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        RequestedLocations = locations;

        if (Unavailable)
        {
            throw new InvalidOperationException("Route metrics service is unavailable.");
        }

        var size = locations.Count;
        var distances = new long[size * size];
        var durations = new long[size * size];

        for (var from = 0; from < size; from++)
        {
            for (var to = 0; to < size; to++)
            {
                if (from == to)
                {
                    continue;
                }

                var steps = 1 + Math.Abs(from - to);
                distances[(from * size) + to] = BaseDistanceMeters * steps;
                durations[(from * size) + to] = BaseDurationSeconds * steps;
            }
        }

        return Task.FromResult(new RouteMatrixSet(
            RouteMatrix.Create(size, distances, !Incomplete),
            RouteMatrix.Create(size, durations, !Incomplete)));
    }

    public void Reset()
    {
        CallCount = 0;
        RequestedLocations = [];
    }
}
