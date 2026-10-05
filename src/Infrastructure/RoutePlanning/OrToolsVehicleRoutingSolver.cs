using Application.RoutePlanning;
using Google.OrTools.ConstraintSolver;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Options;

namespace Infrastructure.RoutePlanning;

/// <summary>
/// Solves the CVRPTW with Google OR-Tools.
/// </summary>
/// <remarks>
/// The model is built so that no shipment is ever silently dropped: OR-Tools makes every
/// non-end node mandatory unless a disjunction is declared, and no disjunction is declared
/// here. A shipment that cannot be served therefore makes the whole problem infeasible and
/// <see cref="Solve" /> comes back with no solution, which the caller surfaces as an explicit
/// failure. "No solution" is not one thing: <see cref="RoutingSolveOutcome.Failure" /> separates
/// a proven infeasibility from a search that ran into <see cref="SearchTimeLimitSeconds" />,
/// because only the first one is a statement about the data.
/// </remarks>
public sealed class OrToolsVehicleRoutingSolver : IVehicleRoutingSolver
{
    private const string CapacityDimensionName = "Capacity";
    private const string TimeDimensionName = "Time";
    private const int SearchTimeLimitSeconds = 10;

    /// <summary>
    /// Weights and capacities are held in grams so a fractional kilogram is never rounded
    /// down to zero. Decimal scale that OR-Tools has no integer type for.
    /// </summary>
    private const decimal GramsPerKilogram = 1000m;

    /// <summary>
    /// Arc cost multiplier for a node of urgency 0. The solver scales an arc into a shipment
    /// node by <c>BasePriorityFactor - urgency</c>, which reproduces the multipliers the
    /// priority switch used to apply: a Low shipment costs 1.5 times the distance to reach it,
    /// an Urgent one half.
    /// </summary>
    private const decimal BasePriorityFactor = 1.5m;

    private readonly DriverScoringOptions _options;

    public OrToolsVehicleRoutingSolver(IOptions<DriverScoringOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
    }

    public RoutingSolveOutcome Solve(
        VehicleRoutingProblem problem,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(problem);

        if (problem.DriverCount == 0 || problem.ShipmentCount == 0)
        {
            return RoutingSolveOutcome.Failed(RoutingSolveFailure.InvalidModel);
        }

        using var manager = BuildIndexManager(problem);
        using var model = new RoutingModel(manager);

        AddArcCosts(problem, model);

        AddCapacityDimension(problem, model);
        AddTimeDimension(problem, model, manager);

        var assignment = model.SolveWithParameters(BuildSearchParameters());

        if (assignment is null)
        {
            return RoutingSolveOutcome.Failed(TranslateStatus(model.GetStatus()));
        }

        return RoutingSolveOutcome.Solved(ReadSolution(problem, model, manager, assignment));
    }

    /// <summary>
    /// Maps the solver status onto the distinction the caller cares about.
    /// </summary>
    /// <remarks>
    /// OR-Tools reports a search that hit its time limit the same way it reports one that ran to
    /// completion without ever finding a solution, and neither of them is a proof of
    /// infeasibility. Only <c>RoutingInfeasible</c> means the constraints were shown to be
    /// unsatisfiable, so that is the only status allowed to claim the plan cannot be made.
    /// </remarks>
    private static RoutingSolveFailure TranslateStatus(RoutingSearchStatus.Types.Value status) => status switch
    {
        RoutingSearchStatus.Types.Value.RoutingInfeasible => RoutingSolveFailure.Infeasible,
        RoutingSearchStatus.Types.Value.RoutingInvalid => RoutingSolveFailure.InvalidModel,
        RoutingSearchStatus.Types.Value.RoutingFail => RoutingSolveFailure.InvalidModel,
        _ => RoutingSolveFailure.NoSolutionWithinTimeLimit
    };

    private static RoutingIndexManager BuildIndexManager(VehicleRoutingProblem problem)
    {
        var starts = new int[problem.DriverCount];
        var ends = new int[problem.DriverCount];

        for (var driver = 0; driver < problem.DriverCount; driver++)
        {
            starts[driver] = driver;
            ends[driver] = problem.GetDepositNodeIndex(driver);
        }

        return new RoutingIndexManager(problem.NodeCount, problem.DriverCount, starts, ends);
    }

    /// <summary>
    /// Registers one cost matrix per driver.
    /// </summary>
    /// <remarks>
    /// OR-Tools has no per-vehicle parameter on a single matrix, and the score is per driver, so
    /// each driver gets its own matrix: distance to a shipment node scaled by its urgency and by
    /// the urgency weighted driver mismatch penalty. Drivers with the same score produce the
    /// same matrix, which OR-Tools collapses into a single cost class.
    /// </remarks>
    /// <remarks>
    /// Both terms are preferences, not constraints. The solver is free to serve an urgent
    /// shipment with a worse driver when the distance makes it the better plan overall, and
    /// nothing here relaxes capacity or time windows.
    /// </remarks>
    private void AddArcCosts(VehicleRoutingProblem problem, RoutingModel model)
    {
        for (var driver = 0; driver < problem.DriverCount; driver++)
        {
            var score = problem.Drivers[driver].Score;
            var evaluator = model.RegisterTransitMatrix(BuildCostForDriver(problem, score));

            // Argument order is cost first, then vehicle, unlike SetArcCostEvaluatorOfVehicle.
            model.SetFixedCostOfVehicle(FixedCostFor(score), driver);
            model.SetArcCostEvaluatorOfVehicle(evaluator, driver);
        }
    }

    /// <summary>
    /// Distance matrix scaled by the urgency of the destination node, plus the penalty for
    /// serving a shipment with a driver whose score is below the best possible. Entering a
    /// driver start or a deposit keeps factor 1 and no penalty, so only shipment sequencing and
    /// pairing are affected.
    /// </summary>
    private long[][] BuildCostForDriver(VehicleRoutingProblem problem, decimal score)
    {
        var size = problem.NodeCount;
        var shipmentOffset = problem.ShipmentNodeOffset;
        var shipmentCount = problem.ShipmentCount;
        var distance = problem.DistanceMatrix;

        var factors = new decimal[size];
        var penalties = new decimal[size];
        Array.Fill(factors, 1m);

        for (var shipment = 0; shipment < shipmentCount; shipment++)
        {
            var node = shipmentOffset + shipment;
            var urgency = problem.Shipments[shipment].Urgency;

            factors[node] = BasePriorityFactor - urgency;
            penalties[node] = _options.MismatchPenaltyMeters * urgency * (1m - score);
        }

        var cost = new long[size][];

        for (var from = 0; from < size; from++)
        {
            cost[from] = new long[size];

            for (var to = 0; to < size; to++)
            {
                var meters = distance[from, to];

                // An unreachable cell stays exactly as it is. Inflating it by the penalty
                // would still keep it out of reach, but the gap is what makes a summed route
                // cost safe from overflowing, and that guarantee is not worth the risk.
                cost[from][to] = meters >= RouteMatrix.UnreachableValue
                    ? RouteMatrix.UnreachableValue
                    : ScaleCost(meters, factors[to]) + ToCost(penalties[to]);
            }
        }

        return cost;
    }

    /// <summary>
    /// Charge for using a driver at all, in meters of detour, scaled down by their score.
    /// Concentrates work on the best drivers instead of spreading it over many mediocre ones.
    /// </summary>
    private long FixedCostFor(decimal score) =>
        ToCost(_options.FixedCostMeters * (1m - score));

    private static long ScaleCost(long meters, decimal factor)
    {
        if (meters <= 0)
        {
            return 0;
        }

        return ToCost(meters * factor);
    }

    /// <summary>
    /// Moves a decimal cost into the integer domain the solver works in, clamped so a
    /// misconfigured penalty can never produce a cost that overflows a summed route.
    /// </summary>
    private static long ToCost(decimal value)
    {
        if (value <= 0)
        {
            return 0;
        }

        var scaled = decimal.Round(value, MidpointRounding.AwayFromZero);
        return scaled >= RouteMatrix.UnreachableValue ? RouteMatrix.UnreachableValue : (long)scaled;
    }

    private static void AddCapacityDimension(VehicleRoutingProblem problem, RoutingModel model)
    {
        var nodeCount = problem.NodeCount;
        var demands = new long[nodeCount];

        for (var shipment = 0; shipment < problem.ShipmentCount; shipment++)
        {
            demands[problem.ShipmentNodeOffset + shipment] =
                ToGrams(problem.Shipments[shipment].WeightKg);
        }

        var capacities = new long[problem.DriverCount];

        for (var driver = 0; driver < problem.DriverCount; driver++)
        {
            var vehicle = problem.Vehicles[problem.GetVehicleIndex(driver)];
            capacities[driver] = ToGrams(vehicle.CapacityKg);
        }

        var evaluator = model.RegisterUnaryTransitVector(demands);

        model.AddDimensionWithVehicleCapacity(
            evaluator,
            slack_max: 0,
            vehicle_capacities: capacities,
            fix_start_cumul_to_zero: true,
            name: CapacityDimensionName);
    }

    private static void AddTimeDimension(
        VehicleRoutingProblem problem,
        RoutingModel model,
        RoutingIndexManager manager)
    {
        var durations = new long[problem.NodeCount][];

        for (var from = 0; from < problem.NodeCount; from++)
        {
            durations[from] = new long[problem.NodeCount];

            for (var to = 0; to < problem.NodeCount; to++)
            {
                durations[from][to] = problem.DurationMatrix[from, to];
            }
        }

        var evaluator = model.RegisterTransitMatrix(durations);

        // Slack lets a driver wait for an opening time window, bounded by the horizon so a
        // wait can never push a route past the planning limit.
        var slackMax = (long)Math.Ceiling(problem.HorizonSeconds * 0.1d);

        model.AddDimension(
            evaluator_index: evaluator,
            slack_max: slackMax,
            capacity: problem.HorizonSeconds,
            fix_start_cumul_to_zero: false,
            name: TimeDimensionName);

        var time = model.GetDimensionOrDie(TimeDimensionName);

        for (var driver = 0; driver < problem.DriverCount; driver++)
        {
            // A driver may start later than the horizon origin if that is cheaper overall.
            time.CumulVar(model.Start(driver)).SetRange(0, problem.HorizonSeconds);
            time.CumulVar(model.End(driver)).SetRange(0, problem.HorizonSeconds);
            model.AddVariableMinimizedByFinalizer(time.CumulVar(model.Start(driver)));
        }

        for (var shipment = 0; shipment < problem.ShipmentCount; shipment++)
        {
            var index = manager.NodeToIndex(problem.ShipmentNodeOffset + shipment);
            var window = problem.Shipments[shipment].DeliveryWindow;

            time.CumulVar(index).SetRange(
                window?.StartSeconds ?? 0,
                window?.EndSeconds ?? problem.HorizonSeconds);
        }
    }

    private static RoutingSearchParameters BuildSearchParameters()
    {
        var parameters = operations_research_constraint_solver.DefaultRoutingSearchParameters();

        parameters.FirstSolutionStrategy =
            FirstSolutionStrategy.Types.Value.ParallelCheapestInsertion;
        parameters.LocalSearchMetaheuristic =
            LocalSearchMetaheuristic.Types.Value.GuidedLocalSearch;
        parameters.TimeLimit = Duration.FromTimeSpan(TimeSpan.FromSeconds(SearchTimeLimitSeconds));

        return parameters;
    }

    private static VehicleRoutingSolution ReadSolution(
        VehicleRoutingProblem problem,
        RoutingModel model,
        RoutingIndexManager manager,
        Assignment assignment)
    {
        var routes = new List<VehicleRoutePlan>(problem.DriverCount);
        var assigned = new HashSet<Guid>();
        long totalDistance = 0;
        long totalDuration = 0;

        for (var driver = 0; driver < problem.DriverCount; driver++)
        {
            if (!model.IsVehicleUsed(assignment, driver))
            {
                continue;
            }

            var driverData = problem.Drivers[driver];
            var stops = new List<VehicleRouteStop>();
            var loadGrams = 0L;
            var order = 0;

            var index = model.Start(driver);
            var previousNode = manager.IndexToNode(index);

            while (!model.IsEnd(index))
            {
                index = assignment.Value(model.NextVar(index));
                var node = manager.IndexToNode(index);

                totalDistance += problem.DistanceMatrix[previousNode, node];
                totalDuration += problem.DurationMatrix[previousNode, node];
                previousNode = node;

                var shipmentIndex = node - problem.ShipmentNodeOffset;
                if (shipmentIndex < 0 || shipmentIndex >= problem.ShipmentCount)
                {
                    continue;
                }

                var shipment = problem.Shipments[shipmentIndex];
                assigned.Add(shipment.ShipmentId);
                loadGrams += ToGrams(shipment.WeightKg);

                stops.Add(new VehicleRouteStop(
                    shipment.ShipmentId,
                    ++order,
                    PlanningStopStatus.Assigned));
            }

            totalDistance += problem.DistanceMatrix[previousNode, problem.GetDepositNodeIndex(driver)];
            totalDuration += problem.DurationMatrix[previousNode, problem.GetDepositNodeIndex(driver)];

            routes.Add(new VehicleRoutePlan(
                driverData.DriverId,
                driverData.VehicleId,
                driverData.DepositId,
                loadGrams / GramsPerKilogram,
                stops));
        }

        var unassigned = problem.Shipments
            .Where(s => !assigned.Contains(s.ShipmentId))
            .Select(s => new VehicleRouteStop(
                s.ShipmentId,
                StopOrder: 0,
                PlanningStopStatus.Infeasible))
            .ToList();

        return new VehicleRoutingSolution(routes, unassigned, totalDistance, totalDuration);
    }

    /// <summary>
    /// Weights are held in grams so a fractional kilogram is never rounded down to zero.
    /// </summary>
    private static long ToGrams(decimal kilograms) =>
        (long)decimal.Round(kilograms * GramsPerKilogram, MidpointRounding.AwayFromZero);
}
