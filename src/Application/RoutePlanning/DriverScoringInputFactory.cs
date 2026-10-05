using Application.Persistence;
using Domain.Entities;
using Domain.Services;

namespace Application.RoutePlanning;

/// <summary>
/// Turns the planner inputs into the metrics <see cref="DriverScoring" /> ranks on.
/// </summary>
/// <remarks>
/// Distance and duration are read from the same matrix the solver will get, averaged over every
/// plannable shipment instead of over the shipments a driver ends up serving. That decision is
/// made before the solve, so there is no per-driver shipment set to average over, and using the
/// whole plan keeps the drivers comparable: everyone is measured against the same work.
/// </remarks>
public static class DriverScoringInputFactory
{
    /// <summary>
    /// Builds one input per driver, in the order given, which is also driver node order. Only
    /// the driver and vehicle of each entry is read: the score is not known yet at this point.
    /// </summary>
    /// <param name="shipmentNodeOffset">
    /// Matrix index of the first shipment node, that is, the driver count.
    /// </param>
    /// <param name="shipmentCount">Number of shipment nodes to average over.</param>
    public static IReadOnlyList<DriverScoringInput> Build(
        IReadOnlyList<PlanningDriverData> drivers,
        IReadOnlyDictionary<Guid, DriverAssignmentCandidate> candidatesByDriverId,
        IReadOnlyDictionary<Guid, Vehicle> vehiclesById,
        RouteMatrix distanceMatrix,
        RouteMatrix durationMatrix,
        int shipmentNodeOffset,
        int shipmentCount)
    {
        ArgumentNullException.ThrowIfNull(drivers);
        ArgumentNullException.ThrowIfNull(candidatesByDriverId);
        ArgumentNullException.ThrowIfNull(vehiclesById);
        ArgumentNullException.ThrowIfNull(distanceMatrix);
        ArgumentNullException.ThrowIfNull(durationMatrix);

        var inputs = new List<DriverScoringInput>(drivers.Count);

        for (var index = 0; index < drivers.Count; index++)
        {
            var driver = drivers[index];

            if (!vehiclesById.TryGetValue(driver.VehicleId, out var vehicle))
            {
                throw new InvalidOperationException(
                    $"The vehicle {driver.VehicleId} selected for driver {driver.DriverId} " +
                    "is missing from the planning vehicles.");
            }

            // A driver with no candidate row is scored on neutral metrics rather than on zeros,
            // which would rank it as the worst on every criterion.
            candidatesByDriverId.TryGetValue(driver.DriverId, out var candidate);

            inputs.Add(new DriverScoringInput(
                driver.DriverId,
                candidate?.SuccessAttemptsToday ?? 0,
                candidate?.PendingShipmentCount ?? 0,
                candidate?.InProgressShipmentCount ?? 0,
                ToDistanceMeters(Mean(distanceMatrix, index, shipmentNodeOffset, shipmentCount)),
                ToDurationMinutes(Mean(durationMatrix, index, shipmentNodeOffset, shipmentCount)),
                FreeCapacityKg(vehicle, candidate),
                EstimatedOperationCost(candidate, vehicle)));
        }

        return inputs;
    }

    /// <summary>
    /// Load left on the selected vehicle. The candidate exposes the biggest vehicle the driver
    /// currently has active, which is not necessarily the one being planned for, so the capacity
    /// taken from the vehicle itself.
    /// </summary>
    private static decimal FreeCapacityKg(Vehicle vehicle, DriverAssignmentCandidate? candidate) =>
        vehicle.CapacityKg - (candidate?.InProgressShipmentWeightKg ?? 0m);

    /// <summary>
    /// Operation cost of the vehicle being planned for. The candidate already carries the costs
    /// of the vehicle the driver is on right now, which is a different vehicle for any driver
    /// switching vehicles.
    /// </summary>
    /// <remarks>
    /// A driver with no candidate row has nothing to derive a cost from, so it reports
    /// <c>null</c> and scores neutral on this criterion instead of looking free to run.
    /// </remarks>
    private static decimal? EstimatedOperationCost(
        DriverAssignmentCandidate? candidate,
        Vehicle vehicle)
    {
        if (candidate is null)
        {
            return null;
        }

        return RouteOperationCostCalculator.CalculateEstimatedOperationCost(
            candidate.SalaryPerHour,
            candidate.SuccessAttemptsToday,
            candidate.KilometersPerDay,
            vehicle.FuelConsumption,
            vehicle.FuelPrice,
            vehicle.MaintenanceCost,
            candidate.ActiveRouteTollCost);
    }

    /// <summary>
    /// Averages row <paramref name="fromNode" /> over the shipment columns.
    /// </summary>
    /// <remarks>
    /// Unreachable cells are skipped rather than summed. They are large enough that a handful of
    /// them would dominate the average, and the planner refuses incomplete matrices before
    /// getting here, so this is only a guard against a caller that skipped that check.
    /// </remarks>
    private static decimal Mean(
        RouteMatrix matrix,
        int fromNode,
        int shipmentNodeOffset,
        int shipmentCount)
    {
        if (shipmentCount <= 0)
        {
            return 0m;
        }

        decimal total = 0m;
        var counted = 0;

        for (var shipment = 0; shipment < shipmentCount; shipment++)
        {
            var value = matrix[fromNode, shipmentNodeOffset + shipment];

            if (value >= RouteMatrix.UnreachableValue)
            {
                continue;
            }

            total += value;
            counted++;
        }

        return counted == 0 ? 0m : total / counted;
    }

    /// <summary>
    /// The scoring model works with whole meters.
    /// </summary>
    private static int ToDistanceMeters(decimal value) => ToInt32(
        decimal.Round(value, MidpointRounding.AwayFromZero));

    /// <summary>
    /// The scoring model compares durations in minutes, and a route duration is rounded up when
    /// reported, so the average is rounded up too instead of being truncated to the minute.
    /// </summary>
    private static int ToDurationMinutes(decimal seconds) =>
        (int)Math.Ceiling(seconds / 60m);

    private static int ToInt32(decimal value) =>
        value > int.MaxValue ? int.MaxValue : value < int.MinValue ? int.MinValue : (int)value;
}