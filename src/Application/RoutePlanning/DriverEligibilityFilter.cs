using Application.Persistence;
using Domain.Entities;
using Domain.Enums;
using Domain.Services;

namespace Application.RoutePlanning;

/// <summary>
/// Drivers that survived the eligibility filter, plus the ones that did not and why.
/// </summary>
/// <param name="KeptDrivers">
/// Eligible drivers in the order they were submitted, which is driver node order in the routing
/// problem.
/// </param>
/// <param name="ScoreByDriverId">
/// Score of every driver that reached the ranking. Absent for drivers rejected earlier.
/// </param>
public sealed record DriverEligibilityResult(
    IReadOnlyList<PlanningDriverData> KeptDrivers,
    IReadOnlyDictionary<Guid, decimal> ScoreByDriverId,
    IReadOnlyList<IneligibleDriver> ExcludedDrivers)
{
    public bool HasAnyDriver => KeptDrivers.Count > 0;
}

public interface IDriverEligibilityFilter
{
    Task<DriverEligibilityResult> SelectAsync(
        IReadOnlyList<PlanningDriverData> drivers,
        IReadOnlyDictionary<Guid, Vehicle> vehiclesById,
        IReadOnlyList<PlanningShipmentData> shipments,
        RouteMatrix distanceMatrix,
        RouteMatrix durationMatrix,
        DateTime dayStartUtc,
        DateTime dayEndUtc,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Decides which of the submitted drivers are worth solving for.
/// </summary>
/// <remarks>
/// Capacity is decided before the ranking, not after. A driver whose vehicle cannot carry any
/// plannable shipment can never be assigned a stop, so it is not a scoring question at all and
/// would only drag on the min-max normalization <see cref="DriverScoring.Rank" /> does over the
/// pool it is given. A driver that can carry some but not all shipments stays: which shipment
/// rides on which vehicle is the solver's decision to make, and dropping usable capacity here
/// would turn a selection the fleet can serve into a problem the solver cannot solve.
/// </remarks>
public sealed class DriverEligibilityFilter : IDriverEligibilityFilter
{
    private readonly IUserRepository _users;
    private readonly DriverEligibilityPolicy _policy;

    public DriverEligibilityFilter(IUserRepository users, DriverEligibilityPolicy policy)
    {
        _users = users;
        _policy = policy;
    }

    public async Task<DriverEligibilityResult> SelectAsync(
        IReadOnlyList<PlanningDriverData> drivers,
        IReadOnlyDictionary<Guid, Vehicle> vehiclesById,
        IReadOnlyList<PlanningShipmentData> shipments,
        RouteMatrix distanceMatrix,
        RouteMatrix durationMatrix,
        DateTime dayStartUtc,
        DateTime dayEndUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(drivers);
        ArgumentNullException.ThrowIfNull(vehiclesById);
        ArgumentNullException.ThrowIfNull(shipments);
        ArgumentNullException.ThrowIfNull(distanceMatrix);
        ArgumentNullException.ThrowIfNull(durationMatrix);

        var driverIds = drivers.Select(d => d.DriverId).ToList();
        var candidates = await _users.GetDriverCandidatesByIdsAsync(
            driverIds, dayStartUtc, dayEndUtc, cancellationToken);

        var inputs = DriverScoringInputFactory.Build(
            drivers,
            candidates.ToDictionary(c => c.DriverId),
            vehiclesById,
            distanceMatrix,
            durationMatrix,
            shipmentNodeOffset: drivers.Count,
            shipmentCount: shipments.Count);

        var lightestShipmentKg = shipments.Count == 0
            ? 0m
            : shipments.Min(s => s.WeightKg);

        var scoreable = new List<DriverScoringInput>(inputs.Count);
        var excluded = new List<IneligibleDriver>(inputs.Count);

        foreach (var input in inputs)
        {
            if (input.FreeCapacityKg >= lightestShipmentKg)
            {
                scoreable.Add(input);
                continue;
            }

            excluded.Add(new IneligibleDriver(
                input.DriverId,
                Score: null,
                DriverIneligibilityReason.InsufficientVehicleCapacity,
                $"Free capacity of {Describe(input.FreeCapacityKg)} kg cannot carry any " +
                $"plannable shipment, the lightest of which weighs " +
                $"{Describe(lightestShipmentKg)} kg."));
        }

        if (scoreable.Count == 0)
        {
            return new DriverEligibilityResult([], new Dictionary<Guid, decimal>(), excluded);
        }

        var ranked = DriverScoring.Rank(scoreable);

        // The weight the kept drivers have to be able to carry decides how many of them survive
        // the thresholds, so it is what the selection is held to.
        var requiredWeightKg = shipments.Sum(s => s.WeightKg);
        var outcome = DriverEligibility.Select(ranked, _policy, requiredWeightKg);

        excluded.AddRange(outcome.Rejected);

        // Every scoreable driver has a score here, kept or not: the report shows the score of a
        // driver that was rejected for scoring too, not just the ones that made it.
        var scoreByDriverId = ranked.ToDictionary(r => r.DriverId, r => r.Score);

        var keptIds = outcome.Kept.Select(k => k.DriverId).ToHashSet();

        // Keeps the submitted order, which is driver node order in the routing problem.
        var keptDrivers = drivers
            .Where(d => keptIds.Contains(d.DriverId))
            .Select(d => d with { Score = scoreByDriverId[d.DriverId] })
            .ToList();

        return new DriverEligibilityResult(keptDrivers, scoreByDriverId, excluded);
    }

    private static string Describe(decimal kilograms) =>
        kilograms.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}