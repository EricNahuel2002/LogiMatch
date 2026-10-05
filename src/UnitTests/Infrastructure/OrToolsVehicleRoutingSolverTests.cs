using Application.RoutePlanning;
using Domain.Enums;
using Domain.Services;
using Domain.ValueObjects;
using Infrastructure.RoutePlanning;
using Microsoft.Extensions.Options;

namespace UnitTests.Infrastructure;

public class OrToolsVehicleRoutingSolverTests
{
    private static readonly Coordinate Point = new(-34.6037m, -58.3816m);

    /// <summary>
    /// A perfect driver costs nothing extra anywhere, so the tests that are not about scoring
    /// keep measuring what they were written to measure. Drivers below are lowered on purpose
    /// where the score is the point.
    /// </summary>
    private const decimal PerfectScore = 1m;

    private readonly OrToolsVehicleRoutingSolver _solver = CreateSolver();

    private static OrToolsVehicleRoutingSolver CreateSolver(
        decimal mismatchPenaltyMeters = 3000m,
        decimal fixedCostMeters = 5000m) =>
        new(Options.Create(new DriverScoringOptions
        {
            MismatchPenaltyMeters = mismatchPenaltyMeters,
            FixedCostMeters = fixedCostMeters
        }));

    /// <summary>
    /// Node layout: drivers, then shipments, then one deposit per distinct deposit id.
    /// </summary>
    private sealed class Scenario
    {
        public List<PlanningShipmentData> Shipments { get; } = [];
        public List<PlanningDriverData> Drivers { get; } = [];
        public List<PlanningVehicleData> Vehicles { get; } = [];
        public List<Guid> DepositOrder { get; } = [];
        public long[][]? Distance { get; set; }
        public long[][]? Duration { get; set; }
        public long HorizonSeconds { get; set; } = 86_400;

        public PlanningShipmentData AddShipment(
            decimal urgency = 0.5m,
            decimal weightKg = 10m,
            TimeWindow? window = null)
        {
            var shipment = new PlanningShipmentData(
                Guid.NewGuid(), urgency, weightKg, Point, window);
            Shipments.Add(shipment);
            return shipment;
        }

        public (Guid DriverId, Guid VehicleId, Guid DepositId) AddDriver(
            Guid? vehicleId = null,
            Guid? depositId = null,
            decimal capacityKg = 100m,
            decimal score = PerfectScore)
        {
            var driverId = Guid.NewGuid();
            var resolvedVehicleId = vehicleId ?? Guid.NewGuid();
            var resolvedDepositId = depositId ?? Guid.NewGuid();

            if (!DepositOrder.Contains(resolvedDepositId))
            {
                DepositOrder.Add(resolvedDepositId);
            }

            Drivers.Add(new PlanningDriverData(
                driverId, Point, resolvedVehicleId, resolvedDepositId, score));

            if (!Vehicles.Any(v => v.VehicleId == resolvedVehicleId))
            {
                Vehicles.Add(new PlanningVehicleData(resolvedVehicleId, capacityKg, Active: true));
            }

            return (driverId, resolvedVehicleId, resolvedDepositId);
        }

        public VehicleRoutingProblem Build()
        {
            var size = Drivers.Count + Shipments.Count + DepositOrder.Count;
            var distance = Distance ?? UnitMatrix(size);
            var duration = Duration ?? distance;

            return new VehicleRoutingProblem(
                Shipments,
                Drivers,
                Vehicles,
                DepositOrder,
                HorizonSeconds,
                RouteMatrix.Create(size, Flatten(distance)),
                RouteMatrix.Create(duration.Length, Flatten(duration)));
        }
    }

    /// <summary>
    /// Symmetric ring geometry: adjacent nodes are 20 apart and every further step adds 10,
    /// with 0 on the diagonal. Every pair is reachable, so a null result always means a real
    /// constraint rather than an unreachable cell.
    /// </summary>
    private static long[][] UnitMatrix(int size) =>
        Enumerable.Range(0, size)
            .Select(i => Enumerable.Range(0, size)
                .Select(j => i == j ? 0L : 10L * (1 + Math.Abs(i - j)))
                .ToArray())
            .ToArray();

    private static long[] Flatten(long[][] matrix) => matrix.SelectMany(r => r).ToArray();

    /// <summary>
    /// Unwraps the outcome for the tests that expect a plan. Failing loudly names the reason the
    /// solver gave instead of letting the assertion complain about a null.
    /// </summary>
    private VehicleRoutingSolution Solved(VehicleRoutingProblem problem)
    {
        var outcome = _solver.Solve(problem);

        return outcome.Solution
            ?? throw new InvalidOperationException(
                $"Expected a solution but the solver reported {outcome.Failure}.");
    }

    private RoutingSolveFailure FailureOf(VehicleRoutingProblem problem) =>
        _solver.Solve(problem).Failure;

    [Fact]
    public void Solve_SingleDriver_TwoShipments_AssignsBothWithConsecutiveStopOrder()
    {
        var scenario = new Scenario();
        var first = scenario.AddShipment();
        var second = scenario.AddShipment();
        scenario.AddDriver();

        var solution = Solved(scenario.Build());

        Assert.NotNull(solution);
        Assert.True(solution!.IsComplete);
        var route = Assert.Single(solution.Routes);
        Assert.Equal([first.ShipmentId, second.ShipmentId], route.Stops.Select(s => s.ShipmentId));
        Assert.Equal([1, 2], route.Stops.Select(s => s.StopOrder));
    }

    [Fact]
    public void Solve_PropagatesDriverVehicleAndDepositFromTheSelection()
    {
        var scenario = new Scenario();
        scenario.AddShipment();
        var (driverId, vehicleId, depositId) = scenario.AddDriver();

        var solution = Solved(scenario.Build());

        Assert.NotNull(solution);
        var route = Assert.Single(solution!.Routes);
        Assert.Equal(driverId, route.DriverId);
        Assert.Equal(vehicleId, route.VehicleId);
        Assert.Equal(depositId, route.DepositId);
    }

    [Fact]
    public void Solve_WeightOverCapacity_ReturnsNoSolution()
    {
        var scenario = new Scenario();
        scenario.AddShipment(weightKg: 80m);
        scenario.AddDriver(capacityKg: 50m);

        Assert.Equal(RoutingSolveFailure.Infeasible, FailureOf(scenario.Build()));
    }

    [Fact]
    public void Solve_SplitLoadAcrossVehicles_IsAssigned()
    {
        var scenario = new Scenario();
        scenario.AddShipment(weightKg: 25m);
        scenario.AddDriver(capacityKg: 50m);
        scenario.AddShipment(weightKg: 25m);
        scenario.AddDriver(capacityKg: 50m);
        scenario.AddShipment(weightKg: 50m);

        // One driver takes the two 25 kg shipments and the other the 50 kg one, so both
        // vehicles have to be used and the load has to be split.
        var solution = Solved(scenario.Build());

        Assert.NotNull(solution);
        Assert.True(solution!.IsComplete);
        Assert.Equal(2, solution.Routes.Count);
        Assert.Equal(100m, solution.Routes.Sum(r => r.LoadKg));
    }

    [Fact]
    public void Solve_ShipmentHeavierThanAnyVehicle_ReturnsNoSolution()
    {
        var scenario = new Scenario();
        scenario.AddShipment(weightKg: 80m);
        scenario.AddDriver(capacityKg: 50m);
        scenario.AddShipment(weightKg: 10m);
        scenario.AddDriver(capacityKg: 50m);

        // 90 kg fits in the combined 100 kg, but the 80 kg shipment fits in neither vehicle.
        Assert.Equal(RoutingSolveFailure.Infeasible, FailureOf(scenario.Build()));
    }

    [Fact]
    public void Solve_WindowOutsideHorizon_IsRejectedBeforeReachingTheSolver()
    {
        var scenario = new Scenario { HorizonSeconds = 30 };
        scenario.AddShipment(window: new TimeWindow(600, 700));
        scenario.AddDriver();

        // A cumul range wider than the horizon makes OR-Tools abort natively, so the problem
        // contract has to reject it first.
        var exception = Assert.Throws<ArgumentException>(() => _solver.Solve(scenario.Build()));

        Assert.Contains("planning horizon", exception.Message);
    }

    [Fact]
    public void Solve_UnreachableTimeWindow_ReturnsNoSolution()
    {
        var scenario = new Scenario { HorizonSeconds = 3_600 };
        scenario.AddShipment(window: new TimeWindow(0, 5));
        scenario.AddDriver();

        // The window is inside the horizon, but reaching the shipment already takes 20
        // seconds, so it closes before any arrival is possible.
        Assert.Equal(RoutingSolveFailure.Infeasible, FailureOf(scenario.Build()));
    }

    /// <summary>
    /// Every failure the solver can report is one the service knows how to explain, so a status
    /// added upstream cannot fall through to a message with no diagnosis attached.
    /// </summary>
    [Fact]
    public void Solve_EmptyModelIsReportedAsADefectRatherThanAnUnplannablePlan()
    {
        // A plan the solver never got to look at is not a plan it could not make, and reporting it
        // as infeasible would send the operator off to reconfigure the fleet for nothing.
        var scenario = new Scenario();
        scenario.AddDriver();

        Assert.NotEqual(RoutingSolveFailure.Infeasible, FailureOf(scenario.Build()));
    }

    [Fact]
    public void Solve_ReachableTimeWindow_AssignsShipment()
    {
        var scenario = new Scenario { HorizonSeconds = 3_600 };
        scenario.AddShipment(window: new TimeWindow(100, 3_000));
        scenario.AddDriver();

        var solution = Solved(scenario.Build());

        Assert.NotNull(solution);
        Assert.True(solution!.IsComplete);
    }

    [Fact]
    public void Solve_NoShipments_IsRejectedAsAnInvalidModel()
    {
        var scenario = new Scenario();
        scenario.AddDriver();

        // Nothing to route is a caller mistake, not a property of the data, so it must not be
        // reported as an unplannable set of shipments.
        Assert.Equal(RoutingSolveFailure.InvalidModel, FailureOf(scenario.Build()));
    }

    [Fact]
    public void Solve_NoDrivers_IsRejectedAsAnInvalidModel()
    {
        var scenario = new Scenario();
        scenario.AddShipment();

        Assert.Equal(RoutingSolveFailure.InvalidModel, FailureOf(scenario.Build()));
    }

    [Fact]
    public void Solve_SharesOneDepositBetweenDrivers()
    {
        var scenario = new Scenario();
        scenario.AddShipment();
        scenario.AddShipment();
        var depositId = Guid.NewGuid();
        scenario.AddDriver(depositId: depositId);
        scenario.AddDriver(depositId: depositId);

        var solution = Solved(scenario.Build());

        Assert.NotNull(solution);
        Assert.True(solution!.IsComplete);
        Assert.NotEmpty(solution.Routes);
        Assert.All(solution.Routes, r => Assert.Equal(depositId, r.DepositId));
        Assert.Equal(
            2,
            solution.Routes.Sum(r => r.Stops.Count));
    }

    [Fact]
    public void Solve_UrgentShipmentIsServedBeforeLowPriorityOne()
    {
        // Layout: driver(0), low(1), urgent(2), deposit(3). Distance from the driver to both
        // shipments is identical, so only the urgency weighting in the arc cost can decide
        // the visiting order.
        var scenario = new Scenario
        {
            Distance =
            [
                [0, 40, 40, 10],
                [40, 0, 20, 20],
                [40, 20, 0, 20],
                [10, 20, 20, 0]
            ]
        };

        var low = scenario.AddShipment(ShipmentPriorityScoring.LowUrgency);
        var urgent = scenario.AddShipment(ShipmentPriorityScoring.UrgentUrgency);
        scenario.AddDriver();

        var solution = Solved(scenario.Build());

        Assert.NotNull(solution);
        var route = Assert.Single(solution!.Routes);
        Assert.Equal(urgent.ShipmentId, route.Stops[0].ShipmentId);
        Assert.Equal(low.ShipmentId, route.Stops[1].ShipmentId);
    }

    [Fact]
    public void Solve_TotalsSumRawDistanceAndDurationOfPlannedArcs()
    {
        var scenario = new Scenario();
        scenario.AddShipment(ShipmentPriorityScoring.UrgentUrgency);
        scenario.AddShipment(ShipmentPriorityScoring.LowUrgency);
        scenario.AddDriver();

        var solution = Solved(scenario.Build());

        Assert.NotNull(solution);
        // Layout: driver(0), s0(1), s1(2), deposit(3). Ring distances are 20 per adjacent step,
        // so 0 -> 1 -> 2 -> 3 costs 60 meters, cheaper than the 80 of the reversed order.
        Assert.Equal(60, solution!.TotalDistanceMeters);
        Assert.Equal(1, solution.TotalDurationMinutes);
    }

    [Fact]
    public void Solve_LoadKgSumsAssignedShipmentWeights()
    {
        var scenario = new Scenario();
        scenario.AddShipment(weightKg: 12.5m);
        scenario.AddShipment(weightKg: 7.25m);
        scenario.AddDriver();

        var solution = Solved(scenario.Build());

        Assert.NotNull(solution);
        Assert.Equal(19.75m, Assert.Single(solution!.Routes).LoadKg);
    }

    [Fact]
    public void Solve_UnreachableCell_IsNeverSelected()
    {
        var scenario = new Scenario
        {
            Distance =
            [
                new long[] { 0, RouteMatrix.UnreachableValue, 10, 10 },
                new long[] { RouteMatrix.UnreachableValue, 0, 20, 20 },
                new long[] { 10, 20, 0, 10 },
                new long[] { 10, 20, 10, 0 }
            ]
        };

        scenario.AddShipment();
        scenario.AddShipment();
        scenario.AddDriver();

        var solution = Solved(scenario.Build());

        Assert.NotNull(solution);
        Assert.True(solution!.IsComplete);
        Assert.True(solution.TotalDistanceMeters < RouteMatrix.UnreachableValue);
    }

    /// <summary>
    /// The far driver is scored well above the near one, and the urgent shipment still goes to
    /// it: the mismatch penalty has to outweigh the extra travel. This is the whole point of
    /// feeding the score into the objective.
    /// </summary>
    [Fact]
    public void Solve_UrgentGoesToTheFarHighScoreDriver()
    {
        var (scenario, _) = ScoreVersusDistanceScenario();

        var solution = Solved(scenario.Build());

        Assert.NotNull(solution);
        var route = Assert.Single(solution!.Routes);
        Assert.Equal(scenario.Drivers[0].DriverId, route.DriverId);
    }

    /// <summary>
    /// Same layout with the penalties switched off: the near driver wins, which proves the
    /// previous assertion came from the score and not from the geometry.
    /// </summary>
    [Fact]
    public void Solve_WithoutPenalties_TheNearestDriverWins()
    {
        var (scenario, urgent) = ScoreVersusDistanceScenario();
        var unpenalized = CreateSolver(mismatchPenaltyMeters: 0m, fixedCostMeters: 0m);

        var outcome = unpenalized.Solve(scenario.Build());

        Assert.NotNull(outcome.Solution);
        var route = Assert.Single(outcome.Solution!.Routes);
        Assert.Equal(scenario.Drivers[1].DriverId, route.DriverId);
        Assert.Equal(urgent.ShipmentId, Assert.Single(route.Stops).ShipmentId);
    }

    /// <summary>
    /// A large fixed cost drops the low score driver even though serving both shipments with it
    /// would be feasible, concentrating the work on the better driver.
    /// </summary>
    [Fact]
    public void Solve_LargeFixedCost_ConcentratesWorkOnTheBestDriver()
    {
        var scenario = TwoIndependentClusters();
        scenario.Drivers[1] = scenario.Drivers[1] with { Score = 0m };

        var solution = Solved(scenario.Build());

        Assert.NotNull(solution);
        var route = Assert.Single(solution!.Routes);
        Assert.Equal(scenario.Drivers[0].DriverId, route.DriverId);
        Assert.Equal(2, route.Stops.Count);
    }

    [Fact]
    public void Solve_WithoutFixedCost_BothDriversAreUsed()
    {
        var scenario = TwoIndependentClusters();
        scenario.Drivers[1] = scenario.Drivers[1] with { Score = 0m };
        var unpenalized = CreateSolver(mismatchPenaltyMeters: 0m, fixedCostMeters: 0m);

        var outcome = unpenalized.Solve(scenario.Build());

        Assert.NotNull(outcome.Solution);
        Assert.Equal(2, outcome.Solution!.Routes.Count);
    }

    /// <summary>
    /// Layout: far driver(0), near driver(1), urgent(2), far deposit(3), near deposit(4). The
    /// far driver scores 1 and the near one 0, so with penalties on the urgent shipment is worth
    /// the detour and without them it is not. Cross distances are 1000 so nothing outside a
    /// driver's own cluster is ever an attractive shortcut.
    /// </summary>
    private static (Scenario Scenario, PlanningShipmentData Urgent) ScoreVersusDistanceScenario()
    {
        var scenario = new Scenario
        {
            Distance =
            [
                new long[] { 0, 1000, 400, 10, 1000 },
                new long[] { 1000, 0, 20, 1000, 10 },
                new long[] { 400, 20, 0, 20, 20 },
                new long[] { 10, 1000, 20, 0, 1000 },
                new long[] { 1000, 10, 20, 1000, 0 }
            ]
        };

        scenario.AddDriver(score: 1m);
        scenario.AddDriver(score: 0m);
        var urgent = scenario.AddShipment(ShipmentPriorityScoring.UrgentUrgency);

        return (scenario, urgent);
    }

    /// <summary>
    /// Two independent clusters, 10 apart within a cluster and 1000 across: driver(0) with its
    /// shipment(2) and deposit(4), driver(1) with its shipment(3) and deposit(5). Serving both
    /// shipments with a single vehicle costs about 2500 against 50 for using both, so only the
    /// fixed cost can make a single vehicle preferable.
    /// </summary>
    private static Scenario TwoIndependentClusters()
    {
        var scenario = new Scenario
        {
            Distance =
            [
                new long[] { 0, 1000, 10, 1000, 10, 1000 },
                new long[] { 1000, 0, 1000, 10, 1000, 10 },
                new long[] { 10, 1000, 0, 1000, 10, 1000 },
                new long[] { 1000, 10, 1000, 0, 1000, 10 },
                new long[] { 10, 1000, 10, 1000, 0, 1000 },
                new long[] { 1000, 10, 1000, 10, 1000, 0 }
            ]
        };

        scenario.AddShipment(ShipmentPriorityScoring.LowUrgency);
        scenario.AddDriver();
        scenario.AddShipment(ShipmentPriorityScoring.LowUrgency);
        scenario.AddDriver();

        return scenario;
    }
}