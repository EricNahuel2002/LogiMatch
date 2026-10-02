using Domain.Entities;
using Domain.ValueObjects;

namespace Application.RoutePlanning;

/// <summary>
/// Hands every driver left without an explicit deposit the closest active one, measured as the
/// great-circle distance between the driver's current location and the deposit.
/// </summary>
/// <remarks>
/// Straight-line distance is enough here on purpose. The deposit is a heuristic input to the
/// solver, not the optimisation objective: once chosen, the solver still optimises the real
/// driving route to and from it. A deposit that ranks slightly wrong therefore costs a few
/// kilometres and never makes the plan infeasible, which is the right trade against a second
/// OpenRouteService round trip on every preview.
/// </remarks>
public sealed class NearestDepositAssignmentPolicy : IDepositAssignmentPolicy
{
    private const double EarthRadiusMeters = 6_371_000d;

    public IReadOnlyDictionary<Guid, Guid> Assign(
        IReadOnlyList<DepositAssignment> drivers,
        IReadOnlyList<Deposit> deposits)
    {
        ArgumentNullException.ThrowIfNull(drivers);
        ArgumentNullException.ThrowIfNull(deposits);

        var active = deposits.Where(d => d.Active).ToList();

        if (active.Count == 0)
        {
            throw new InvalidOperationException(
                "There are no active deposits to plan from. Add one or activate an existing " +
                "deposit first.");
        }

        var assignment = new Dictionary<Guid, Guid>(drivers.Count);

        foreach (var driver in drivers)
        {
            if (driver.RequestedDepositId is { } requested)
            {
                assignment[driver.DriverId] = requested;
                continue;
            }

            assignment[driver.DriverId] = Nearest(driver.CurrentLocation, active).Id;
        }

        return assignment;
    }

    private static Deposit Nearest(Coordinate origin, IReadOnlyList<Deposit> candidates)
    {
        var best = candidates[0];
        var bestMeters = double.MaxValue;

        foreach (var candidate in candidates)
        {
            var meters = DistanceInMeters(origin, candidate.Coordinate);

            // Ties break on the id so the same catalogue always produces the same problem:
            // an unstable deposit choice would change the node layout between two previews of
            // the very same data.
            if (meters < bestMeters || (meters == bestMeters && candidate.Id.CompareTo(best.Id) < 0))
            {
                best = candidate;
                bestMeters = meters;
            }
        }

        return best;
    }

    private static double DistanceInMeters(Coordinate from, Coordinate to)
    {
        var fromLatitude = DegreesToRadians((double)from.Latitude);
        var toLatitude = DegreesToRadians((double)to.Latitude);
        var latitudeDelta = toLatitude - fromLatitude;
        var longitudeDelta = DegreesToRadians((double)(to.Longitude - from.Longitude));

        var haversine =
            Math.Pow(Math.Sin(latitudeDelta / 2), 2) +
            Math.Cos(fromLatitude) * Math.Cos(toLatitude) * Math.Pow(Math.Sin(longitudeDelta / 2), 2);

        return 2 * EarthRadiusMeters * Math.Asin(Math.Sqrt(Math.Clamp(haversine, 0d, 1d)));
    }

    private static double DegreesToRadians(double degrees) => degrees * (Math.PI / 180d);
}