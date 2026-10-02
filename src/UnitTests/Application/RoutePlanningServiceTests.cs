using Application.Dtos.RoutePlanning;
using Application.Integrations;
using Application.Persistence;
using Application.RoutePlanning;
using Application.Services;
using Application.Validators;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using FluentValidation;
using FluentValidation.Results;
using Moq;

namespace UnitTests.Application;

public class RoutePlanningServiceTests
{
    private static readonly Coordinate Depot = new(-34.60m, -58.38m);
    private static readonly Coordinate DriverLocation = new(-34.61m, -58.39m);
    private static readonly Coordinate Destination = new(-34.70m, -58.50m);

    private readonly Mock<IShipmentRepository> _shipments = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IVehicleRepository> _vehicles = new();
    private readonly Mock<IDepositRepository> _deposits = new();
    private readonly Mock<IRouteMatrixClient> _routeMatrixClient = new();
    private readonly Mock<IVehicleRoutingSolver> _solver = new();

    private readonly Guid _driverId = Guid.NewGuid();
    private readonly Guid _vehicleId = Guid.NewGuid();
    private readonly Guid _depositId = Guid.NewGuid();
    private readonly Guid _shipmentId = Guid.NewGuid();
    private readonly Guid _otherShipmentId = Guid.NewGuid();

    private static readonly DateTimeOffset Now =
        new(2026, 3, 10, 14, 0, 0, TimeSpan.FromHours(-3));

    private RoutePlanningService CreateService(IValidator<PlanRoutesRequest>? validator = null) =>
        new(
            _shipments.Object,
            _users.Object,
            _vehicles.Object,
            _deposits.Object,
            new NearestDepositAssignmentPolicy(),
            _routeMatrixClient.Object,
            _solver.Object,
            validator ?? FluentValidationMocks.AlwaysValid<PlanRoutesRequest>().Object,
            new FixedTimeProvider(Now));

    private PlanRoutesRequest Request => new()
    {
        DriverSelections =
        [
            new DriverSelectionRequest
            {
                DriverId = _driverId,
                VehicleId = _vehicleId,
                DepositId = _depositId
            }
        ]
    };

    /// <summary>
    /// Same selection without pinning a deposit, which is what lets the planner choose one.
    /// </summary>
    private PlanRoutesRequest PlanRoutesRequestWithoutDeposit => new()
    {
        DriverSelections =
        [
            new DriverSelectionRequest
            {
                DriverId = _driverId,
                VehicleId = _vehicleId
            }
        ]
    };

    /// <summary>
    /// Wires a single driver, vehicle, deposit and plannable shipment, which is the baseline
    /// every test starts from before overriding one specific aspect.
    /// </summary>
    private void ArrangeValidScenario(
        ShipmentPlanningData? shipment = null,
        Driver? driver = null,
        Vehicle? vehicle = null,
        Deposit? deposit = null)
    {
        _users.Setup(u => u.GetDriversByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([driver ?? BuildDriver(_driverId, DriverLocation)]);

        _vehicles.Setup(v => v.GetManyByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([vehicle ?? BuildVehicle(_vehicleId)]);

        _deposits.Setup(d => d.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([deposit ?? BuildDeposit(_depositId, Depot)]);

        ArrangeShipments(shipment ?? BuildShipment(_shipmentId));
    }

    /// <summary>
    /// Answers the pending shipment query, then hands the solver a stop for each one. The
    /// matrix is not sized here: <see cref="ArrangeMatrix" /> adapts to the nodes the service
    /// actually asks for, which is fewer than the shipment count as soon as one is excluded.
    /// </summary>
    private void ArrangeShipments(params ShipmentPlanningData[] shipments)
    {
        _shipments
            .Setup(s => s.GetPendingWithPlanningDetailsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipments);

        ArrangeMatrix();
        ArrangeSolution([.. shipments.Select(s => s.ShipmentId)]);
    }

    /// <summary>
    /// Answers the matrix call with a small ring geometry for exactly the nodes that were
    /// requested, so the service gets a complete and solvable matrix without reaching ORS.
    /// </summary>
    private void ArrangeMatrix()
    {
        _routeMatrixClient
            .Setup(c => c.GetMatrixAsync(It.IsAny<IReadOnlyList<Coordinate>>(), It.IsAny<CancellationToken>()))
            .Returns<IReadOnlyList<Coordinate>, CancellationToken>((locations, _) =>
                Task.FromResult(BuildRingMatrix(locations.Count)));
    }

    private static RouteMatrixSet BuildRingMatrix(int nodeCount)
    {
        var values = Enumerable.Range(0, nodeCount)
            .SelectMany(i => Enumerable.Range(0, nodeCount)
                .Select(j => (long)(i == j ? 0 : 100 * (1 + Math.Abs(i - j)))))
            .ToArray();

        return new RouteMatrixSet(
            RouteMatrix.Create(nodeCount, values),
            RouteMatrix.Create(nodeCount, values));
    }

    private void ArrangeSolution(params Guid[] shipmentIds)
    {
        var stops = shipmentIds
            .Select((id, index) => new VehicleRouteStop(id, index + 1, PlanningStopStatus.Assigned))
            .ToList();

        _solver.Setup(s => s.Solve(It.IsAny<VehicleRoutingProblem>(), It.IsAny<CancellationToken>()))
            .Returns(new VehicleRoutingSolution(
                [new VehicleRoutePlan(_driverId, _vehicleId, _depositId, 10m, stops)],
                [],
                300,
                90));
    }

    private static Driver BuildDriver(Guid id, Coordinate? location)
    {
        var driver = new Driver();
        typeof(Driver).GetProperty(nameof(Driver.Id))!.SetValue(driver, id);

        if (location is not null)
        {
            driver.SetCurrentLocation(location);
        }

        return driver;
    }

    private static Vehicle BuildVehicle(Guid id, bool active = true)
    {
        var vehicle = Vehicle.Create($"plate-{id:N}".Substring(0, 10), 500m);
        typeof(Vehicle).GetProperty(nameof(Vehicle.Id))!.SetValue(vehicle, id);
        vehicle.SetActive(active);
        return vehicle;
    }

    private static Deposit BuildDeposit(Guid id, Coordinate coordinate, bool active = true)
    {
        var deposit = Deposit.Create("Central", coordinate);
        typeof(Deposit).GetProperty(nameof(Deposit.Id))!.SetValue(deposit, id);
        deposit.SetActive(active);
        return deposit;
    }

    private static ShipmentPlanningData BuildShipment(
        Guid id,
        Coordinate? destination = null,
        DateTime? windowStart = null,
        DateTime? windowEnd = null) =>
        new(
            id,
            ShipmentPriority.Normal,
            10m,
            destination ?? Destination,
            windowStart,
            windowEnd);

    private static ShipmentPlanningData BuildShipmentWithoutDestination(Guid id) =>
        new(
            id,
            ShipmentPriority.Normal,
            10m,
            null,
            null,
            null);

    private VehicleRoutingProblem? CapturedProblem() => _solver.Invocations
        .FirstOrDefault()?.Arguments
        .OfType<VehicleRoutingProblem>()
        .FirstOrDefault();

    private static Guid[] PlannedShipmentIds(VehicleRoutingProblem problem) =>
        [.. problem.Shipments.Select(s => s.ShipmentId)];

    [Fact]
    public async Task PreviewAsync_ReturnsProposalFromSolver()
    {
        ArrangeValidScenario();

        var result = await CreateService().PreviewAsync(Request);

        Assert.Single(result.Assignments);
        var assignment = result.Assignments[0];
        Assert.Equal(_driverId, assignment.DriverId);
        Assert.Equal(_vehicleId, assignment.VehicleId);
        Assert.Equal(_depositId, assignment.DepositId);
        Assert.Equal(10m, assignment.LoadKg);
        Assert.Equal([new PlannedStopResponse(_shipmentId, 1)], assignment.Stops);
        Assert.Equal(300, result.TotalDistanceMeters);
        Assert.Equal(2, result.TotalDurationMinutes);
        Assert.Equal(1, result.DriverCount);
        Assert.Equal(1, result.VehicleCount);
        Assert.Equal(1, result.ShipmentCount);
        Assert.Empty(result.ExcludedShipments);
    }

    [Fact]
    public async Task PreviewAsync_PlansEveryPendingShipmentInRepositoryOrder()
    {
        ArrangeValidScenario();
        ArrangeShipments(
            BuildShipment(_shipmentId),
            BuildShipment(_otherShipmentId));

        var result = await CreateService().PreviewAsync(Request);

        Assert.Equal(2, result.ShipmentCount);
        Assert.Equal(
            [
                new PlannedStopResponse(_shipmentId, 1),
                new PlannedStopResponse(_otherShipmentId, 2)
            ],
            Assert.Single(result.Assignments).Stops);
    }

    [Fact]
    public async Task PreviewAsync_RequestsMatrixInDriverShipmentDepositNodeOrder()
    {
        ArrangeValidScenario();
        IReadOnlyList<Coordinate>? requested = null;
        _routeMatrixClient.Setup(c => c.GetMatrixAsync(It.IsAny<IReadOnlyList<Coordinate>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<Coordinate>, CancellationToken>((l, _) => requested = l)
            .ReturnsAsync(new RouteMatrixSet(
                RouteMatrix.Create(3, [0, 100, 200, 100, 0, 100, 200, 100, 0]),
                RouteMatrix.Create(3, [0, 10, 20, 10, 0, 10, 20, 10, 0])));

        await CreateService().PreviewAsync(Request);

        Assert.Equal([DriverLocation, Destination, Depot], requested);
    }

    [Fact]
    public async Task PreviewAsync_UsesTwelveHourHorizon()
    {
        ArrangeValidScenario();

        await CreateService().PreviewAsync(Request);

        Assert.Equal(12 * 3_600, CapturedProblem()!.HorizonSeconds);
    }

    [Fact]
    public async Task PreviewAsync_ConvertsDeliveryWindowToSecondsFromNow()
    {
        var localNow = Now.DateTime;
        ArrangeValidScenario(BuildShipment(
            _shipmentId,
            windowStart: localNow.AddHours(1),
            windowEnd: localNow.AddHours(3)));

        await CreateService().PreviewAsync(Request);

        var window = CapturedProblem()!.Shipments[0].DeliveryWindow;
        Assert.NotNull(window);
        Assert.Equal(3_600, window!.Value.StartSeconds);
        Assert.Equal(10_800, window!.Value.EndSeconds);
    }

    [Fact]
    public async Task PreviewAsync_ClampsDeliveryWindowToHorizon()
    {
        var localNow = Now.DateTime;
        ArrangeValidScenario(BuildShipment(
            _shipmentId,
            windowStart: localNow.AddHours(-2),
            windowEnd: localNow.AddHours(30)));

        await CreateService().PreviewAsync(Request);

        var window = CapturedProblem()!.Shipments[0].DeliveryWindow;
        Assert.NotNull(window);
        Assert.Equal(0, window!.Value.StartSeconds);
        Assert.Equal(12 * 3_600, window!.Value.EndSeconds);
    }

    [Fact]
    public async Task PreviewAsync_NoDeliveryWindow_LeavesShipmentUnwindowed()
    {
        ArrangeValidScenario();

        await CreateService().PreviewAsync(Request);

        Assert.Null(CapturedProblem()!.Shipments[0].DeliveryWindow);
    }

    [Fact]
    public async Task PreviewAsync_PassesShipmentWeightAndPriority()
    {
        ArrangeValidScenario(new ShipmentPlanningData(
            _shipmentId,
            ShipmentPriority.Urgent,
            12.5m,
            Destination,
            null,
            null));

        await CreateService().PreviewAsync(Request);

        var shipment = CapturedProblem()!.Shipments[0];
        Assert.Equal(12.5m, shipment.WeightKg);
        Assert.Equal(ShipmentPriority.Urgent, shipment.Priority);
    }

    [Fact]
    public async Task PreviewAsync_PassesVehicleCapacityAndActivity()
    {
        ArrangeValidScenario(vehicle: BuildVehicle(_vehicleId));

        await CreateService().PreviewAsync(Request);

        var vehicle = CapturedProblem()!.Vehicles[0];
        Assert.Equal(_vehicleId, vehicle.VehicleId);
        Assert.Equal(500m, vehicle.CapacityKg);
        Assert.True(vehicle.Active);
    }

    [Fact]
    public async Task PreviewAsync_NoPendingShipments_Throws()
    {
        ArrangeValidScenario();
        ArrangeShipments();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().PreviewAsync(Request));

        Assert.Contains("no pending shipments", exception.Message);
        _solver.Verify(s => s.Solve(It.IsAny<VehicleRoutingProblem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PreviewAsync_MissingDriver_Throws()
    {
        ArrangeValidScenario();
        _users.Setup(u => u.GetDriversByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().PreviewAsync(Request));

        Assert.Contains(_driverId.ToString(), exception.Message);
    }

    [Fact]
    public async Task PreviewAsync_MissingVehicle_Throws()
    {
        ArrangeValidScenario();
        _vehicles.Setup(v => v.GetManyByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().PreviewAsync(Request));

        Assert.Contains(_vehicleId.ToString(), exception.Message);
    }

    [Fact]
    public async Task PreviewAsync_MissingDeposit_Throws()
    {
        ArrangeValidScenario();
        _deposits.Setup(d => d.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().PreviewAsync(Request));

        Assert.Contains(_depositId.ToString(), exception.Message);
    }

    [Fact]
    public async Task PreviewAsync_InactiveRequestedDeposit_Throws()
    {
        ArrangeValidScenario(deposit: BuildDeposit(_depositId, Depot, active: false));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().PreviewAsync(Request));

        Assert.Contains("inactive", exception.Message);
        Assert.Contains(_depositId.ToString(), exception.Message);
        _solver.Verify(s => s.Solve(It.IsAny<VehicleRoutingProblem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PreviewAsync_WithoutActiveDeposits_Throws()
    {
        ArrangeValidScenario();
        _deposits.Setup(d => d.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([BuildDeposit(_depositId, Depot, active: false)]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().PreviewAsync(PlanRoutesRequestWithoutDeposit));

        Assert.Contains("no active deposits", exception.Message, StringComparison.OrdinalIgnoreCase);
        _solver.Verify(s => s.Solve(It.IsAny<VehicleRoutingProblem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PreviewAsync_WithoutDeposit_UsesTheNearestOne()
    {
        var nearbyDepositId = Guid.NewGuid();
        ArrangeValidScenario();
        _deposits.Setup(d => d.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                BuildDeposit(nearbyDepositId, new Coordinate(-34.605m, -58.381m)),
                BuildDeposit(Guid.NewGuid(), new Coordinate(-34.90m, -58.90m))
            ]);

        await CreateService().PreviewAsync(PlanRoutesRequestWithoutDeposit);

        _solver.Verify(
            s => s.Solve(
                It.Is<VehicleRoutingProblem>(p => p.Drivers[0].DepositId == nearbyDepositId),
                It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task PreviewAsync_OnlyAssignedDepositsBecomeNodes()
    {
        var unusedDepositId = Guid.NewGuid();
        ArrangeValidScenario();
        _deposits.Setup(d => d.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                BuildDeposit(_depositId, Depot),
                BuildDeposit(unusedDepositId, new Coordinate(-34.90m, -58.90m))
            ]);

        await CreateService().PreviewAsync(Request);

        _solver.Verify(
            s => s.Solve(
                It.Is<VehicleRoutingProblem>(p => p.DepositNodeOrder.Count == 1
                    && p.DepositNodeOrder[0] == _depositId),
                It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task PreviewAsync_DriverWithoutCurrentLocation_Throws()
    {
        ArrangeValidScenario(driver: BuildDriver(_driverId, location: null));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().PreviewAsync(Request));

        Assert.Contains("current location", exception.Message);
        _solver.Verify(s => s.Solve(It.IsAny<VehicleRoutingProblem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PreviewAsync_InactiveVehicle_Throws()
    {
        ArrangeValidScenario(vehicle: BuildVehicle(_vehicleId, active: false));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().PreviewAsync(Request));

        Assert.Contains("inactive", exception.Message);
        _solver.Verify(s => s.Solve(It.IsAny<VehicleRoutingProblem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PreviewAsync_ShipmentWithoutRouteStop_AloneFails()
    {
        ArrangeValidScenario(BuildShipmentWithoutDestination(_shipmentId));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().PreviewAsync(Request));

        Assert.Contains("None of the 1 pending shipments can be routed", exception.Message);
        Assert.Contains(_shipmentId.ToString(), exception.Message);
        Assert.Contains("geocoded delivery coordinate", exception.Message);
        _solver.Verify(s => s.Solve(It.IsAny<VehicleRoutingProblem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PreviewAsync_ShipmentWithDefaultCoordinate_IsExcludedFromThePlan()
    {
        ArrangeValidScenario();
        ArrangeShipments(
            BuildShipment(_shipmentId, destination: new Coordinate(0, 0)),
            BuildShipment(_otherShipmentId));
        ArrangeSolution(_otherShipmentId);

        var result = await CreateService().PreviewAsync(Request);

        var excluded = Assert.Single(result.ExcludedShipments);
        Assert.Equal(_shipmentId, excluded.ShipmentId);
        Assert.Contains("geocoded delivery coordinate", excluded.Reason);
        Assert.Equal([_otherShipmentId], PlannedShipmentIds(CapturedProblem()!));
        Assert.Equal(1, result.ShipmentCount);
    }

    [Fact]
    public async Task PreviewAsync_ShipmentWithElapsedDeliveryWindow_IsExcludedFromThePlan()
    {
        var localNow = Now.DateTime;
        ArrangeValidScenario();
        ArrangeShipments(
            BuildShipment(_shipmentId, windowStart: localNow.AddHours(-4), windowEnd: localNow.AddHours(-1)),
            BuildShipment(_otherShipmentId));
        ArrangeSolution(_otherShipmentId);

        var result = await CreateService().PreviewAsync(Request);

        var excluded = Assert.Single(result.ExcludedShipments);
        Assert.Equal(_shipmentId, excluded.ShipmentId);
        Assert.Contains("delivery window already closed", excluded.Reason);
        Assert.Equal([_otherShipmentId], PlannedShipmentIds(CapturedProblem()!));
        Assert.Equal(1, result.ShipmentCount);
    }

    [Fact]
    public async Task PreviewAsync_DeliveryWindowStillOpen_IsPlanned()
    {
        var localNow = Now.DateTime;
        ArrangeValidScenario(BuildShipment(
            _shipmentId,
            windowStart: localNow.AddHours(-4),
            windowEnd: localNow.AddMinutes(1)));

        var result = await CreateService().PreviewAsync(Request);

        Assert.Empty(result.ExcludedShipments);
        Assert.Equal([_shipmentId], PlannedShipmentIds(CapturedProblem()!));
    }

    [Fact]
    public async Task PreviewAsync_DeliveryWindowBeyondTheHorizon_IsExcludedFromThePlan()
    {
        // The horizon ends twelve hours from now, and the route still has to be back at the
        // deposit before it closes, so a window that opens later cannot be served.
        var horizonEnd = Now.DateTime.AddHours(RoutePlanningService.MaxHorizonHours);
        ArrangeValidScenario();
        ArrangeShipments(
            BuildShipment(
                _shipmentId,
                windowStart: horizonEnd,
                windowEnd: horizonEnd.AddHours(4)),
            BuildShipment(_otherShipmentId));
        ArrangeSolution(_otherShipmentId);

        var result = await CreateService().PreviewAsync(Request);

        var excluded = Assert.Single(result.ExcludedShipments);
        Assert.Equal(_shipmentId, excluded.ShipmentId);
        Assert.Contains("beyond today's planning horizon", excluded.Reason);
        Assert.Equal([_otherShipmentId], PlannedShipmentIds(CapturedProblem()!));
        Assert.Equal(1, result.ShipmentCount);
    }

    [Fact]
    public async Task PreviewAsync_DeliveryWindowOpeningInsideTheHorizon_IsPlanned()
    {
        var horizonEnd = Now.DateTime.AddHours(RoutePlanningService.MaxHorizonHours);
        ArrangeValidScenario(BuildShipment(
            _shipmentId,
            windowStart: horizonEnd.AddMinutes(-30),
            windowEnd: horizonEnd.AddHours(4)));

        var result = await CreateService().PreviewAsync(Request);

        Assert.Empty(result.ExcludedShipments);
        Assert.Equal([_shipmentId], PlannedShipmentIds(CapturedProblem()!));
    }

    [Fact]
    public async Task PreviewAsync_UnplannableShipmentDoesNotBlockTheOthers()
    {
        ArrangeValidScenario();
        ArrangeShipments(
            BuildShipmentWithoutDestination(_shipmentId),
            BuildShipment(_otherShipmentId));
        ArrangeSolution(_otherShipmentId);

        var result = await CreateService().PreviewAsync(Request);

        Assert.Equal([_otherShipmentId], PlannedShipmentIds(CapturedProblem()!));
        Assert.Equal(1, result.ShipmentCount);
        Assert.Equal(_shipmentId, Assert.Single(result.ExcludedShipments).ShipmentId);
    }

    [Fact]
    public async Task PreviewAsync_EveryShipmentUnplannable_Throws()
    {
        ArrangeValidScenario();
        ArrangeShipments(
            BuildShipment(_shipmentId, destination: new Coordinate(0, 0)),
            BuildShipmentWithoutDestination(_otherShipmentId));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().PreviewAsync(Request));

        Assert.Contains("None of the 2 pending shipments can be routed", exception.Message);
        Assert.Contains("geocoded delivery coordinate", exception.Message);
        Assert.Contains(_shipmentId.ToString(), exception.Message);
        Assert.Contains(_otherShipmentId.ToString(), exception.Message);
        _solver.Verify(s => s.Solve(It.IsAny<VehicleRoutingProblem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PreviewAsync_IncompleteMatrix_Throws()
    {
        ArrangeValidScenario();
        _routeMatrixClient.Setup(c => c.GetMatrixAsync(It.IsAny<IReadOnlyList<Coordinate>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RouteMatrixSet(
                RouteMatrix.Create(3, [0, 100, 200, 100, 0, 100, 200, 100, RouteMatrix.UnreachableValue], hasCompleteData: false),
                RouteMatrix.Create(3, [0, 10, 20, 10, 0, 10, 20, 10, 0], hasCompleteData: false)));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().PreviewAsync(Request));

        Assert.Contains("matrix is incomplete", exception.Message);
        _solver.Verify(s => s.Solve(It.IsAny<VehicleRoutingProblem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PreviewAsync_NoFeasibleSolution_Throws()
    {
        ArrangeValidScenario();
        _solver.Setup(s => s.Solve(It.IsAny<VehicleRoutingProblem>(), It.IsAny<CancellationToken>()))
            .Returns((VehicleRoutingProblem _, CancellationToken _) => (VehicleRoutingSolution?)null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().PreviewAsync(Request));

        Assert.Contains("No feasible route", exception.Message);
    }

    [Fact]
    public async Task PreviewAsync_SharesOneDepositAcrossDrivers()
    {
        var secondDriverId = Guid.NewGuid();
        var secondVehicleId = Guid.NewGuid();

        _users.Setup(u => u.GetDriversByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([BuildDriver(_driverId, DriverLocation), BuildDriver(secondDriverId, DriverLocation)]);
        _vehicles.Setup(v => v.GetManyByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([BuildVehicle(_vehicleId), BuildVehicle(secondVehicleId)]);
        _deposits.Setup(d => d.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([BuildDeposit(_depositId, Depot)]);
        ArrangeShipments(BuildShipment(_shipmentId));

        var request = new PlanRoutesRequest
        {
            DriverSelections =
            [
                new DriverSelectionRequest { DriverId = _driverId, VehicleId = _vehicleId, DepositId = _depositId },
                new DriverSelectionRequest { DriverId = secondDriverId, VehicleId = secondVehicleId, DepositId = _depositId }
            ]
        };

        await CreateService().PreviewAsync(request);

        var problem = CapturedProblem()!;
        Assert.Equal(2, problem.DriverCount);
        Assert.Equal([_depositId], problem.DepositNodeOrder);
        Assert.Equal(3, problem.GetDepositNodeIndex(0));
        Assert.Equal(3, problem.GetDepositNodeIndex(1));
    }

    [Fact]
    public async Task PreviewAsync_WithoutDeposit_SplitsTheDepositsAcrossDrivers()
    {
        var secondDriverId = Guid.NewGuid();
        var secondVehicleId = Guid.NewGuid();
        var centralDepositId = Guid.NewGuid();
        var southDepositId = Guid.NewGuid();

        _users.Setup(u => u.GetDriversByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                BuildDriver(_driverId, new Coordinate(-34.61m, -58.39m)),
                BuildDriver(secondDriverId, new Coordinate(-34.70m, -58.52m))
            ]);
        _vehicles.Setup(v => v.GetManyByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([BuildVehicle(_vehicleId), BuildVehicle(secondVehicleId)]);
        _deposits.Setup(d => d.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                BuildDeposit(centralDepositId, Depot),
                BuildDeposit(southDepositId, new Coordinate(-34.672m, -58.440m))
            ]);
        ArrangeShipments(BuildShipment(_shipmentId));

        var request = new PlanRoutesRequest
        {
            DriverSelections =
            [
                new DriverSelectionRequest { DriverId = _driverId, VehicleId = _vehicleId },
                new DriverSelectionRequest { DriverId = secondDriverId, VehicleId = secondVehicleId }
            ]
        };

        await CreateService().PreviewAsync(request);

        var problem = CapturedProblem()!;
        Assert.Equal(centralDepositId, problem.Drivers[0].DepositId);
        Assert.Equal(southDepositId, problem.Drivers[1].DepositId);
        Assert.Equal(2, problem.DepositNodeOrder.Count);
    }

    [Fact]
    public async Task PreviewAsync_InvalidRequest_IsRejectedBeforeAnyQuery()
    {
        ArrangeValidScenario();

        // The real validator is used on purpose: ValidateAndThrowAsync reports failures
        // through the failure handler the validator itself invokes, so a mocked validator
        // could never surface a ValidationException.
        var request = new PlanRoutesRequest
        {
            DriverSelections =
            [
                new DriverSelectionRequest
                {
                    DriverId = _driverId,
                    VehicleId = _vehicleId,
                    DepositId = _depositId
                },
                new DriverSelectionRequest
                {
                    DriverId = _driverId,
                    VehicleId = _vehicleId,
                    DepositId = _depositId
                }
            ]
        };

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService(new PlanRoutesRequestValidator()).PreviewAsync(request));

        _routeMatrixClient.Verify(
            c => c.GetMatrixAsync(It.IsAny<IReadOnlyList<Coordinate>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _solver.Verify(
            s => s.Solve(It.IsAny<VehicleRoutingProblem>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PreviewAsync_LoadsEveryResourceInASingleQuery()
    {
        ArrangeValidScenario();

        await CreateService().PreviewAsync(Request);

        _users.Verify(
            u => u.GetDriversByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _vehicles.Verify(
            v => v.GetManyByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _deposits.Verify(
            d => d.GetAllAsync(It.IsAny<CancellationToken>()),
            Times.Once);
        _shipments.Verify(
            s => s.GetPendingWithPlanningDetailsAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
