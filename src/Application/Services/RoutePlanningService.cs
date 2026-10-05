using Application.Dtos.RoutePlanning;
using Application.Integrations;
using Application.Persistence;
using Application.RoutePlanning;
using Domain.Entities;
using Domain.Services;
using Domain.ValueObjects;
using FluentValidation;

namespace Application.Services;

/// <summary>
    /// Turns an explicit resource selection into a routing proposal over every pending shipment.
    /// The service owns every rule that can be checked before the solver runs: the selection has
    /// to be resolvable, there has to be something plannable to plan, the routing matrix has to
    /// be complete, and at least one of the selected drivers has to be worth planning with.
    /// Anything else is reported as a conflict before a single OR-Tools model is built.
    /// </summary>
    /// <remarks>
    /// A pending shipment that cannot be routed never fails the preview on its own: it comes back
    /// as an excluded shipment with its reason, so one shipment still waiting for an address does
    /// not block the rest of the plan.
    /// </remarks>
    /// <remarks>
    /// The same applies to drivers. A selected driver that is not worth solving for comes back in
    /// <see cref="RoutePlanningProposalResponse.ExcludedDrivers" /> with its reason and its score
    /// instead of silently narrowing the plan, so the caller can see that the fleet it asked for
    /// is not the fleet that got planned.
    /// </remarks>
    /// <remarks>
    /// The service deliberately takes no <c>IUnitOfWork</c>: a preview is a proposal, not a change.
    /// </remarks>
public class RoutePlanningService : IRoutePlanningService
{
    /// <summary>
    /// Longest route a preview may plan. Windows and the horizon are expressed in seconds,
    /// matching the duration matrix.
    /// </summary>
    public const int MaxHorizonHours = 12;

    public const long SecondsPerHour = 3_600;

    /// <summary>
    /// A shipment whose geocoded coordinate is still the (0,0) default has no real delivery
    /// address, so it cannot be routed.
    /// </summary>
    private static readonly Coordinate MissingCoordinate = new(0, 0);

    private const string NoGeocodedCoordinateReason = "no geocoded delivery coordinate";

    private const string ElapsedDeliveryWindowReason = "delivery window already closed";

    private const string BeyondHorizonReason = "delivery window is beyond today's planning horizon";

    private static readonly TimeZoneInfo ArgentinaTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");

    private readonly IShipmentRepository _shipments;
    private readonly IUserRepository _users;
    private readonly IVehicleRepository _vehicles;
    private readonly IDepositRepository _deposits;
    private readonly IDepositAssignmentPolicy _depositAssignment;
    private readonly IRouteMatrixClient _routeMatrixClient;
    private readonly IVehicleRoutingSolver _solver;
    private readonly IDriverEligibilityFilter _driverEligibility;
    private readonly IValidator<PlanRoutesRequest> _validator;
    private readonly TimeProvider _timeProvider;

    public RoutePlanningService(
        IShipmentRepository shipments,
        IUserRepository users,
        IVehicleRepository vehicles,
        IDepositRepository deposits,
        IDepositAssignmentPolicy depositAssignment,
        IRouteMatrixClient routeMatrixClient,
        IVehicleRoutingSolver solver,
        IDriverEligibilityFilter driverEligibility,
        IValidator<PlanRoutesRequest> validator,
        TimeProvider timeProvider)
    {
        _shipments = shipments;
        _users = users;
        _vehicles = vehicles;
        _deposits = deposits;
        _depositAssignment = depositAssignment;
        _routeMatrixClient = routeMatrixClient;
        _solver = solver;
        _driverEligibility = driverEligibility;
        _validator = validator;
        _timeProvider = timeProvider;
    }

    public async Task<RoutePlanningProposalResponse> PreviewAsync(
        PlanRoutesRequest request,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var selections = request.DriverSelections;

        // One bulk query per resource.
        var drivers = await _users.GetDriversByIdsAsync(
            selections.Select(s => s.DriverId).ToList(), cancellationToken);
        var vehicles = await _vehicles.GetManyByIdsAsync(
            selections.Select(s => s.VehicleId).Distinct().ToList(), cancellationToken);
        var deposits = await _deposits.GetAllAsync(cancellationToken);
        var pendingShipments = await _shipments.GetPendingWithPlanningDetailsAsync(cancellationToken);

        if (pendingShipments.Count == 0)
        {
            throw new InvalidOperationException(
                "There are no pending shipments to plan. Create or reprogram an order first.");
        }

        var driversById = drivers.ToDictionary(d => d.Id);
        var vehiclesById = vehicles.ToDictionary(v => v.Id);
        var depositsById = deposits.ToDictionary(d => d.Id);

        EnsureResourcesExist(selections, driversById, vehiclesById);
        EnsureVehiclesAreUsable(selections, vehiclesById);
        EnsureRequestedDepositsAreUsable(selections, depositsById);

        var depositAssignments = BuildDepositAssignments(selections, driversById);
        var depositByDriver = _depositAssignment.Assign(depositAssignments, deposits);
        var planningDrivers = BuildPlanningDrivers(selections, driversById, depositByDriver);

        // Only the deposits that were actually assigned become nodes: every node the solver
        // cannot reach from some vehicle makes the whole problem infeasible.
        var depositNodeOrder = planningDrivers
            .Select(d => d.DepositId)
            .Distinct()
            .ToList();

        var horizonStart = TimeZoneInfo
            .ConvertTime(_timeProvider.GetUtcNow(), ArgentinaTimeZone)
            .DateTime;
        var horizonSeconds = MaxHorizonHours * SecondsPerHour;

        var (planningShipments, excludedShipments) = SplitPlannableShipments(
            pendingShipments, horizonStart, horizonSeconds);

        if (planningShipments.Count == 0)
        {
            throw new InvalidOperationException(
                $"None of the {pendingShipments.Count} pending shipments can be routed: " +
                $"{DescribeExclusions(excludedShipments)}.");
        }

        var planningVehicles = selections
            .Select(s => s.VehicleId)
            .Distinct()
            .Select(id => vehiclesById[id])
            .Select(v => new PlanningVehicleData(v.Id, v.CapacityKg, v.Active))
            .ToList();

        // Node order is drivers, then shipments, then one node per distinct deposit.
        var locations = new List<Coordinate>(planningDrivers.Count + planningShipments.Count + depositNodeOrder.Count);
        locations.AddRange(planningDrivers.Select(d => d.CurrentLocation));
        locations.AddRange(planningShipments.Select(s => s.Destination));
        locations.AddRange(depositNodeOrder.Select(id => depositsById[id].Coordinate));

        var matrix = await _routeMatrixClient.GetMatrixAsync(locations, cancellationToken);

        if (!matrix.HasCompleteData)
        {
            throw new InvalidOperationException(
                "The routing matrix is incomplete, so no proposal can be produced for the " +
                "selected drivers and shipments.");
        }

        // Scoring reads the same matrix the solver gets, and it can drop drivers from the plan,
        // so it has to happen before the problem is assembled rather than after.
        var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(horizonStart, ArgentinaTimeZone);
        var eligibility = await _driverEligibility.SelectAsync(
            planningDrivers,
            vehiclesById,
            planningShipments,
            matrix.Distances,
            matrix.Durations,
            dayStartUtc,
            dayStartUtc.AddDays(1),
            cancellationToken);

        if (!eligibility.HasAnyDriver)
        {
            throw new InvalidOperationException(
                $"None of the {planningDrivers.Count} selected drivers is eligible to plan: " +
                $"{DescribeExclusions(eligibility.ExcludedDrivers)}.");
        }

        var problem = BuildProblem(
            eligibility,
            planningDrivers,
            depositNodeOrder,
            planningVehicles,
            planningShipments,
            horizonSeconds,
            matrix);

        var solution = _solver.Solve(problem, cancellationToken);

        if (solution.Solution is null)
        {
            throw new InvalidOperationException(
                BuildNoSolutionMessage(problem, solution.Failure));
        }

        return new RoutePlanningProposalResponse(
            solution.Solution.Routes
                .Select(route => new RouteAssignmentResponse(
                    route.DriverId,
                    route.VehicleId,
                    route.DepositId,
                    route.LoadKg,
                    route.Stops
                        .Select(stop => new PlannedStopResponse(stop.ShipmentId, stop.StopOrder))
                        .ToList()))
                .ToList(),
            solution.Solution.Routes.Select(r => r.DriverId).Distinct().Count(),
            solution.Solution.Routes.Select(r => r.VehicleId).Distinct().Count(),
            solution.Solution.Routes.Sum(r => r.Stops.Count),
            solution.Solution.TotalDistanceMeters,
            solution.Solution.TotalDurationMinutes,
            excludedShipments,
            eligibility.ExcludedDrivers
                .Select(d => new ExcludedDriverResponse(
                    d.DriverId,
                    d.Score,
                    d.Reason,
                    d.Detail))
                .ToList());
    }

    /// <summary>
    /// Rebuilds the routing problem around the drivers that survived the eligibility filter.
    /// </summary>
    /// <remarks>
    /// The matrices are projected instead of fetched again: they already hold every cell the
    /// surviving nodes need, and a driver left out of the plan must not remain a node, or the
    /// solver would keep solving for a vehicle that is never used. Dropping a driver can also
    /// orphan a deposit that only that driver was assigned to, and an orphan node would make the
    /// whole problem infeasible.
    /// </remarks>
    private static VehicleRoutingProblem BuildProblem(
        DriverEligibilityResult eligibility,
        IReadOnlyList<PlanningDriverData> submittedDrivers,
        IReadOnlyList<Guid> submittedDepositOrder,
        IReadOnlyList<PlanningVehicleData> submittedVehicles,
        IReadOnlyList<PlanningShipmentData> shipments,
        long horizonSeconds,
        RouteMatrixSet matrix)
    {
        var keptDrivers = eligibility.KeptDrivers;
        var keptDriverIds = keptDrivers.Select(d => d.DriverId).ToHashSet();
        var keptDepositIds = keptDrivers.Select(d => d.DepositId).ToHashSet();

        var depositNodeOffset = submittedDrivers.Count + shipments.Count;

        var keptNodeIndices = new List<int>(
            keptDrivers.Count + shipments.Count + keptDepositIds.Count);

        // Node indexes are positions in the submitted layout, which is drivers, shipments, then
        // deposits, and they must stay ascending for the projection to line up with the nodes.
        for (var submitted = 0; submitted < submittedDrivers.Count; submitted++)
        {
            if (keptDriverIds.Contains(submittedDrivers[submitted].DriverId))
            {
                keptNodeIndices.Add(submitted);
            }
        }

        keptNodeIndices.AddRange(Enumerable.Range(submittedDrivers.Count, shipments.Count));

        for (var submitted = 0; submitted < submittedDepositOrder.Count; submitted++)
        {
            if (keptDepositIds.Contains(submittedDepositOrder[submitted]))
            {
                keptNodeIndices.Add(depositNodeOffset + submitted);
            }
        }

        var keptVehicles = keptDrivers
            .Select(d => d.VehicleId)
            .Distinct()
            .Select(id => submittedVehicles.First(v => v.VehicleId == id))
            .ToList();

        return new VehicleRoutingProblem(
            shipments,
            keptDrivers,
            keptVehicles,
            submittedDepositOrder.Where(keptDepositIds.Contains).ToList(),
            horizonSeconds,
            matrix.Distances.Project(keptNodeIndices),
            matrix.Durations.Project(keptNodeIndices));
    }

    private static void EnsureResourcesExist(
        IReadOnlyList<DriverSelectionRequest> selections,
        IReadOnlyDictionary<Guid, Driver> drivers,
        IReadOnlyDictionary<Guid, Vehicle> vehicles)
    {
        var missingDrivers = selections
            .Where(s => !drivers.ContainsKey(s.DriverId))
            .Select(s => s.DriverId)
            .Distinct()
            .ToList();

        if (missingDrivers.Count > 0)
        {
            throw NotFound(nameof(Driver), missingDrivers);
        }

        var missingVehicles = selections
            .Where(s => !vehicles.ContainsKey(s.VehicleId))
            .Select(s => s.VehicleId)
            .Distinct()
            .ToList();

        if (missingVehicles.Count > 0)
        {
            throw NotFound(nameof(Vehicle), missingVehicles);
        }
    }

    /// <summary>
    /// A deposit the caller pinned to a driver has to exist and be active. Letting an inactive
    /// one through would plan a route that ends at a closed site, and the caller would have no
    /// way of knowing the planner quietly ignored the site.
    /// </summary>
    private static void EnsureRequestedDepositsAreUsable(
        IReadOnlyList<DriverSelectionRequest> selections,
        IReadOnlyDictionary<Guid, Deposit> deposits)
    {
        var unknown = selections
            .Where(s => s.DepositId is not null && !deposits.ContainsKey(s.DepositId.Value))
            .Select(s => s.DepositId!.Value)
            .Distinct()
            .ToList();

        if (unknown.Count > 0)
        {
            throw NotFound(nameof(Deposit), unknown);
        }

        var inactive = selections
            .Where(s => s.DepositId is { } id && !deposits[id].Active)
            .Select(s => s.DepositId!.Value)
            .Distinct()
            .ToList();

        if (inactive.Count > 0)
        {
            throw new InvalidOperationException(
                "Deposits are inactive and cannot be used in a route: " +
                string.Join(", ", inactive) + ".");
        }
    }

    /// <summary>
    /// Splits the pending shipments into the ones the solver can take and the ones it cannot. An
    /// excluded shipment keeps a human readable reason so an admin can tell "planned" apart from
    /// "left waiting", instead of one unusable shipment silently sinking the whole proposal.
    /// </summary>
    private static (
        IReadOnlyList<PlanningShipmentData> Plannable,
        IReadOnlyList<ExcludedShipmentResponse> Excluded) SplitPlannableShipments(
            IReadOnlyList<ShipmentPlanningData> pendingShipments,
            DateTime horizonStart,
            long horizonSeconds)
    {
        var plannable = new List<PlanningShipmentData>();
        var excluded = new List<ExcludedShipmentResponse>();

        foreach (var shipment in pendingShipments)
        {
            if (shipment.Destination is null || shipment.Destination == MissingCoordinate)
            {
                excluded.Add(new ExcludedShipmentResponse(
                    shipment.ShipmentId,
                    NoGeocodedCoordinateReason));
                continue;
            }

            if (OutsidePlanningHorizon(shipment, horizonStart, horizonSeconds) is { } reason)
            {
                excluded.Add(new ExcludedShipmentResponse(shipment.ShipmentId, reason));
                continue;
            }

            plannable.Add(new PlanningShipmentData(
                shipment.ShipmentId,
                ShipmentPriorityScoring.UrgencyOf(shipment.Priority),
                shipment.WeightKg,
                shipment.Destination,
                ToTimeWindow(
                    shipment.DeliveryWindowStartAt,
                    shipment.DeliveryWindowEndAt,
                    horizonStart,
                    horizonSeconds)));
        }

        return (plannable, excluded);
    }

    /// <summary>
    /// Why a delivery window leaves no room inside the planning horizon, or null when the
    /// shipment can still be served. The horizon starts at the current local time and lasts
    /// <see cref="MaxHorizonHours" />, while the delivery window is whatever the customer asked
    /// for, so a window can fall on either side of it. Both cases are caught before
    /// <see cref="ToTimeWindow" /> clamps them into a degenerate range that no route can satisfy:
    /// the route still has to be back at the deposit by the end of the horizon.
    /// </summary>
    private static string? OutsidePlanningHorizon(
        ShipmentPlanningData shipment,
        DateTime horizonStart,
        long horizonSeconds)
    {
        if (shipment.DeliveryWindowEndAt is { } end && end <= horizonStart)
        {
            return ElapsedDeliveryWindowReason;
        }

        var horizonEnd = horizonStart.AddSeconds(horizonSeconds);
        if (shipment.DeliveryWindowStartAt is { } start && start >= horizonEnd)
        {
            return BeyondHorizonReason;
        }

        return null;
    }

    /// <summary>
    /// Explains why the solver came back with no plan, in the same
    /// "sentence, then reason: detail" shape the exclusion messages use.
    /// </summary>
    /// <remarks>
    /// The generic sentence stays first so callers can keep keying on it, and the diagnosis only
    /// claims what the failure actually establishes. A search that ran out of time has proven
    /// nothing, so it does not get a capacity verdict, and an invalid model is reported as a
    /// defect rather than as an unplannable set of shipments.
    /// </remarks>
    private static string BuildNoSolutionMessage(
        VehicleRoutingProblem problem,
        RoutingSolveFailure failure)
    {
        const string head =
            "No feasible route covers every plannable pending shipment with the selected " +
            "drivers, vehicles and deposits.";

        var reason = failure switch
        {
            RoutingSolveFailure.NoSolutionWithinTimeLimit =>
                "No solution was found within the search limit, so infeasibility is not proven.",

            RoutingSolveFailure.InvalidModel =>
                "The solver rejected the routing model, which points at a defect rather than at " +
                "the pending shipments.",

            _ => DescribeBindingConstraint(problem)
        };

        return $"{head} {reason}";
    }

    /// <summary>
    /// Names the constraint that leaves the plan short, preferring the one the operator can act on.
    /// </summary>
    /// <remarks>
    /// Fleet capacity is read through the drivers that survived the filter rather than through the
    /// submitted vehicles, because each driver arrives already paired with its own vehicle: a
    /// vehicle nobody was selected with is not capacity the solver could use, and counting it would
    /// hide a shortfall behind a number that cannot be deployed.
    /// </remarks>
    private static string DescribeBindingConstraint(VehicleRoutingProblem problem)
    {
        var fleetCapacityKg = Enumerable
            .Range(0, problem.DriverCount)
            .Select(i => problem.Vehicles[problem.GetVehicleIndex(i)].CapacityKg)
            .Sum();

        var weightKg = problem.Shipments.Sum(s => s.WeightKg);

        if (fleetCapacityKg < weightKg)
        {
            return $"The selected fleet offers {fleetCapacityKg} kg and the {problem.ShipmentCount} " +
                $"plannable pending shipments weigh {weightKg} kg: {weightKg - fleetCapacityKg} kg short, " +
                $"so it needs at least one more vehicle of {weightKg - fleetCapacityKg} kg or more.";
        }

        return $"Capacity is not the limit ({fleetCapacityKg} kg available for {weightKg} kg to be " +
            $"routed); the {problem.HorizonSeconds / SecondsPerHour} hour horizon and the delivery " +
            "windows are.";
    }

    private static string DescribeExclusions(IReadOnlyList<ExcludedShipmentResponse> excluded) =>
        string.Join(", ", excluded
            .GroupBy(e => e.Reason)
            .Select(group => $"{string.Join(", ", group.Select(e => e.ShipmentId))} ({group.Key})"));

    private static string DescribeExclusions(IReadOnlyList<IneligibleDriver> excluded) =>
        string.Join(", ", excluded
            .GroupBy(e => e.Reason)
            .Select(group =>
                $"{string.Join(", ", group.Select(e => e.DriverId))} ({group.Key}: {group.First().Detail})"));

    private static void EnsureVehiclesAreUsable(
        IReadOnlyList<DriverSelectionRequest> selections,
        IReadOnlyDictionary<Guid, Vehicle> vehicles)
    {
        var inactive = selections
            .Where(s => !vehicles[s.VehicleId].Active)
            .Select(s => s.VehicleId)
            .Distinct()
            .ToList();

        if (inactive.Count > 0)
        {
            throw new InvalidOperationException(
                $"Vehicles are inactive and cannot take part in a route: {string.Join(", ", inactive)}.");
        }
    }

    private static IReadOnlyList<DepositAssignment> BuildDepositAssignments(
        IReadOnlyList<DriverSelectionRequest> selections,
        IReadOnlyDictionary<Guid, Driver> drivers)
    {
        var withoutLocation = selections
            .Where(s => drivers[s.DriverId].CurrentLocation is null)
            .Select(s => s.DriverId)
            .Distinct()
            .ToList();

        if (withoutLocation.Count > 0)
        {
            throw new InvalidOperationException(
                "Drivers have no current location, so their route cannot start: " +
                string.Join(", ", withoutLocation) + ".");
        }

        return selections
            .Select(s => new DepositAssignment(
                s.DriverId,
                drivers[s.DriverId].CurrentLocation!,
                s.DepositId))
            .ToList();
    }

    /// <summary>
    /// Builds the pre-scoring driver list, which only exists to lay out the routing matrix and
    /// to give the eligibility filter something to score. The score is set here only as a
    /// placeholder: <see cref="IDriverEligibilityFilter" /> replaces the whole list before the
    /// problem reaches the solver, so a driver that never got a real score cannot reach it.
    /// </summary>
    private static IReadOnlyList<PlanningDriverData> BuildPlanningDrivers(
        IReadOnlyList<DriverSelectionRequest> selections,
        IReadOnlyDictionary<Guid, Driver> drivers,
        IReadOnlyDictionary<Guid, Guid> depositByDriver)
    {
        return selections
            .Select(s => new PlanningDriverData(
                s.DriverId,
                drivers[s.DriverId].CurrentLocation!,
                s.VehicleId,
                depositByDriver[s.DriverId],
                Score: 0m))
            .ToList();
    }

    /// <summary>
    /// Converts an order delivery window into seconds from the horizon start. The stored
    /// window is local wall-clock time without a kind, and the horizon is the local time of the
    /// depot, so the difference is taken directly and then clamped to the planning horizon.
    /// </summary>
    private static TimeWindow? ToTimeWindow(
        DateTime? startAt,
        DateTime? endAt,
        DateTime horizonStart,
        long horizonSeconds)
    {
        if (startAt is null && endAt is null)
        {
            return null;
        }

        var start = startAt.HasValue
            ? Clamp((long)(startAt.Value - horizonStart).TotalSeconds, 0, horizonSeconds)
            : 0;

        var end = endAt.HasValue
            ? Clamp((long)(endAt.Value - horizonStart).TotalSeconds, 0, horizonSeconds)
            : horizonSeconds;

        // A window that closes before it opens is treated as a point in time at the boundary
        // instead of producing an inverted range the solver cannot use.
        if (end < start)
        {
            end = start;
        }

        return new TimeWindow(start, end);
    }

    private static long Clamp(long value, long minimum, long maximum) =>
        value < minimum ? minimum : value > maximum ? maximum : value;

    private static InvalidOperationException NotFound(string entityName, IReadOnlyList<Guid> ids) =>
        new($"'{entityName}' with id '{string.Join(", ", ids)}' was not found.");
}
