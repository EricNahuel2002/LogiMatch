using Application.RoutePlanning;
using Domain.Enums;
using Google.OrTools.ConstraintSolver;
using Google.Protobuf.WellKnownTypes;

namespace Infrastructure.RoutePlanning;

/// <summary>
/// Solves the CVRPTW with Google OR-Tools.
/// </summary>
/// <remarks>
/// The model is built so that no shipment is ever silently dropped: OR-Tools makes every
/// non-end node mandatory unless a disjunction is declared, and no disjunction is declared
/// here. A shipment that cannot be served therefore makes the whole problem infeasible and
/// <see cref="Solve" /> returns null, which the caller surfaces as an explicit failure.
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
    /// Cost multiplier applied when a route enters a shipment node. Lower makes the solver
    /// prefer serving that shipment earlier. Applies to the arc cost only: it never relaxes
    /// capacity or time windows.
    /// </summary>
    private static decimal CostFactorFor(ShipmentPriority priority) => priority switch
    {
        ShipmentPriority.Urgent => 0.5m,
        ShipmentPriority.High => 0.75m,
        ShipmentPriority.Normal => 1.0m,
        ShipmentPriority.Low => 1.5m,
        _ => 1.0m
    };

    public VehicleRoutingSolution? Solve(
        VehicleRoutingProblem problem,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(problem);

        if (problem.DriverCount == 0 || problem.ShipmentCount == 0)
        {
            return null;
        }

        using var manager = BuildIndexManager(problem);
        using var model = new RoutingModel(manager);

        model.SetArcCostEvaluatorOfAllVehicles(
            model.RegisterTransitMatrix(BuildPriorityWeightedCost(problem)));

        AddCapacityDimension(problem, model);
        AddTimeDimension(problem, model, manager);

        var assignment = model.SolveWithParameters(BuildSearchParameters());

        if (assignment is null)
        {
            return null;
        }

        return ReadSolution(problem, model, manager, assignment);
    }

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
    /// Distance matrix scaled by the priority of the destination node. Entering a driver
    /// start or a deposit keeps factor 1, so only shipment sequencing is affected.
    /// </summary>
    private static long[][] BuildPriorityWeightedCost(VehicleRoutingProblem problem)
    {
        var size = problem.NodeCount;
        var shipmentOffset = problem.ShipmentNodeOffset;
        var shipmentCount = problem.ShipmentCount;
        var distance = problem.DistanceMatrix;

        var factors = new decimal[size];
        Array.Fill(factors, 1m);

        for (var shipment = 0; shipment < shipmentCount; shipment++)
        {
            factors[shipmentOffset + shipment] =
                CostFactorFor(problem.Shipments[shipment].Priority);
        }

        var cost = new long[size][];

        for (var from = 0; from < size; from++)
        {
            cost[from] = new long[size];

            for (var to = 0; to < size; to++)
            {
                var meters = distance[from, to];
                cost[from][to] = meters >= RouteMatrix.UnreachableValue
                    ? RouteMatrix.UnreachableValue
                    : ScaleCost(meters, factors[to]);
            }
        }

        return cost;
    }

    private static long ScaleCost(long meters, decimal factor)
    {
        if (meters <= 0)
        {
            return 0;
        }

        var scaled = decimal.Round(meters * factor, MidpointRounding.AwayFromZero);
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
