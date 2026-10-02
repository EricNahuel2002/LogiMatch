using Application.RoutePlanning;
using Domain.Enums;
using Domain.ValueObjects;
using Infrastructure.RoutePlanning;

namespace UnitTests.Infrastructure;

public class OrToolsVehicleRoutingSolverTests
{
    private static readonly Coordinate Point = new(-34.6037m, -58.3816m);

    private readonly OrToolsVehicleRoutingSolver _solver = new();

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
            ShipmentPriority priority = ShipmentPriority.Normal,
            decimal weightKg = 10m,
            TimeWindow? window = null)
        {
            var shipment = new PlanningShipmentData(
                Guid.NewGuid(), priority, weightKg, Point, window);
            Shipments.Add(shipment);
            return shipment;
        }

        public (Guid DriverId, Guid VehicleId, Guid DepositId) AddDriver(
            Guid? vehicleId = null,
            Guid? depositId = null,
            decimal capacityKg = 100m)
        {
            var driverId = Guid.NewGuid();
            var resolvedVehicleId = vehicleId ?? Guid.NewGuid();
            var resolvedDepositId = depositId ?? Guid.NewGuid();

            if (!DepositOrder.Contains(resolvedDepositId))
            {
                DepositOrder.Add(resolvedDepositId);
            }

            Drivers.Add(new PlanningDriverData(
                driverId, Point, resolvedVehicleId, resolvedDepositId));

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

    [Fact]
    public void Solve_SingleDriver_TwoShipments_AssignsBothWithConsecutiveStopOrder()
    {
        var scenario = new Scenario();
        var first = scenario.AddShipment();
        var second = scenario.AddShipment();
        scenario.AddDriver();

        var solution = _solver.Solve(scenario.Build());

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

        var solution = _solver.Solve(scenario.Build());

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

        Assert.Null(_solver.Solve(scenario.Build()));
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
        var solution = _solver.Solve(scenario.Build());

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
        Assert.Null(_solver.Solve(scenario.Build()));
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
        Assert.Null(_solver.Solve(scenario.Build()));
    }

    [Fact]
    public void Solve_ReachableTimeWindow_AssignsShipment()
    {
        var scenario = new Scenario { HorizonSeconds = 3_600 };
        scenario.AddShipment(window: new TimeWindow(100, 3_000));
        scenario.AddDriver();

        var solution = _solver.Solve(scenario.Build());

        Assert.NotNull(solution);
        Assert.True(solution!.IsComplete);
    }

    [Fact]
    public void Solve_NoShipments_ReturnsNoSolution()
    {
        var scenario = new Scenario();
        scenario.AddDriver();

        Assert.Null(_solver.Solve(scenario.Build()));
    }

    [Fact]
    public void Solve_NoDrivers_ReturnsNoSolution()
    {
        var scenario = new Scenario();
        scenario.AddShipment();

        Assert.Null(_solver.Solve(scenario.Build()));
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

        var solution = _solver.Solve(scenario.Build());

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
        // shipments is identical, so only the priority weighting in the arc cost can decide
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

        var low = scenario.AddShipment(ShipmentPriority.Low);
        var urgent = scenario.AddShipment(ShipmentPriority.Urgent);
        scenario.AddDriver();

        var solution = _solver.Solve(scenario.Build());

        Assert.NotNull(solution);
        var route = Assert.Single(solution!.Routes);
        Assert.Equal(urgent.ShipmentId, route.Stops[0].ShipmentId);
        Assert.Equal(low.ShipmentId, route.Stops[1].ShipmentId);
    }

    [Fact]
    public void Solve_TotalsSumRawDistanceAndDurationOfPlannedArcs()
    {
        var scenario = new Scenario();
        scenario.AddShipment(ShipmentPriority.Urgent);
        scenario.AddShipment(ShipmentPriority.Low);
        scenario.AddDriver();

        var solution = _solver.Solve(scenario.Build());

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

        var solution = _solver.Solve(scenario.Build());

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

        var solution = _solver.Solve(scenario.Build());

        Assert.NotNull(solution);
        Assert.True(solution!.IsComplete);
        Assert.True(solution.TotalDistanceMeters < RouteMatrix.UnreachableValue);
    }
}
