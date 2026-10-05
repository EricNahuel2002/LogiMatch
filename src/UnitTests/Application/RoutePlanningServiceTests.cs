using Application.Dtos.RoutePlanning;
using Application.Integrations;
using Application.Persistence;
using Application.RoutePlanning;
using Application.Services;
using Application.Validators;
using Domain.Entities;
using Domain.Enums;
using Domain.Services;
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

    /// <summary>
    /// The eligibility filter runs for real, on top of the mocked repositories: the point of
    /// these tests is what reaches the solver, and a mocked filter would let any driver list
    /// through untouched.
    /// </summary>
    private RoutePlanningService CreateService(IValidator<PlanRoutesRequest>? validator = null) =>
        new(
            _shipments.Object,
            _users.Object,
            _vehicles.Object,
            _deposits.Object,
            new NearestDepositAssignmentPolicy(),
            _routeMatrixClient.Object,
            _solver.Object,
            new DriverEligibilityFilter(_users.Object, new DriverScoringOptions().ToPolicy()),
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

        ArrangeCandidates(Candidate(_driverId, successes: 10, kilometers: 50m));

        ArrangeShipments(shipment ?? BuildShipment(_shipmentId));
    }

    /// <summary>
    /// Scoring metrics per driver, restricted to the drivers being planned, which is how the
    /// repository contract works: a plan is scored against its own selection, not the fleet.
    /// </summary>
    private void ArrangeCandidates(params DriverAssignmentCandidate[] candidates)
    {
        _users
            .Setup(u => u.GetDriverCandidatesByIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns<IReadOnlyCollection<Guid>, DateTime, DateTime, CancellationToken>(
                (ids, _, _, _) => Task.FromResult<IReadOnlyList<DriverAssignmentCandidate>>(
                    [.. candidates.Where(c => ids.Contains(c.DriverId))]));
    }

    /// <summary>
    /// Neutral defaults with everything zeroed, so a test only has to state the metrics that
    /// are meant to make a driver stand out.
    /// </summary>
    private static DriverAssignmentCandidate Candidate(
        Guid driverId,
        int successes = 0,
        int pending = 0,
        int inProgress = 0,
        decimal inProgressWeightKg = 0m,
        decimal salaryPerHour = 0m,
        decimal kilometers = 0m,
        decimal tolls = 0m) =>
        new(
            driverId,
            DriverLocation,
            MaxActiveVehicleCapacityKg: 500m,
            successes,
            pending,
            inProgress,
            inProgressWeightKg,
            salaryPerHour,
            kilometers,
            VehicleFuelConsumption: 0m,
            VehicleFuelPrice: 0m,
            VehicleMaintenanceCost: 0m,
            tolls);

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
            .Returns(RoutingSolveOutcome.Solved(new VehicleRoutingSolution(
                [new VehicleRoutePlan(_driverId, _vehicleId, _depositId, 10m, stops)],
                [],
                300,
                90)));
    }

    private void ArrangeNoSolution(RoutingSolveFailure failure) =>
        _solver.Setup(s => s.Solve(It.IsAny<VehicleRoutingProblem>(), It.IsAny<CancellationToken>()))
            .Returns(RoutingSolveOutcome.Failed(failure));

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

    private static Vehicle BuildVehicle(Guid id, bool active = true, decimal capacityKg = 500m)
    {
        var vehicle = Vehicle.Create($"plate-{id:N}".Substring(0, 10), capacityKg);
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
        DateTime? windowEnd = null,
        decimal weightKg = 10m) =>
        new(
            id,
            ShipmentPriority.Normal,
            weightKg,
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

    /// <summary>
    /// Two drivers sharing a deposit, both plannable. <paramref name="weakDriverId" /> is the one
    /// expected to score below the floor; it is submitted first so the ring geometry used by the
    /// matrix helper puts it farther from the shipments, which is what turns a mediocre driver
    /// into one that falls under the threshold.
    /// </summary>
    private void ArrangeTwoDrivers(
        Guid secondDriverId,
        Guid secondVehicleId,
        Guid? weakDriverId = null,
        decimal firstVehicleCapacityKg = 500m,
        Coordinate? secondDriverLocation = null)
    {
        var weak = weakDriverId ?? secondDriverId;
        var strongIsFirst = weak != _driverId;

        _users.Setup(u => u.GetDriversByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                BuildDriver(_driverId, DriverLocation),
                BuildDriver(secondDriverId, secondDriverLocation ?? DriverLocation)
            ]);

        _vehicles.Setup(v => v.GetManyByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([BuildVehicle(_vehicleId, capacityKg: firstVehicleCapacityKg), BuildVehicle(secondVehicleId)]);

        _deposits.Setup(d => d.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([BuildDeposit(_depositId, Depot)]);

        // Strong: most deliveries done, nothing queued, short day. Weak: the opposite. Distance
        // and duration are not stated here because they come from the matrix.
        ArrangeCandidates(
            strongIsFirst
                ? Candidate(_driverId, successes: 10, kilometers: 10m)
                : Candidate(_driverId, pending: 5, inProgress: 5, kilometers: 60m),
            strongIsFirst
                ? Candidate(secondDriverId, pending: 5, inProgress: 5, kilometers: 60m)
                : Candidate(secondDriverId, successes: 10, kilometers: 10m));

        ArrangeShipments(BuildShipment(_shipmentId));
    }

    private static PlanRoutesRequest TwoDriverRequest(
        Guid firstDriverId,
        Guid firstVehicleId,
        Guid firstDepositId,
        Guid secondDriverId,
        Guid secondVehicleId,
        Guid secondDepositId) =>
        new()
        {
            DriverSelections =
            [
                new DriverSelectionRequest
                {
                    DriverId = firstDriverId,
                    VehicleId = firstVehicleId,
                    DepositId = firstDepositId
                },
                new DriverSelectionRequest
                {
                    DriverId = secondDriverId,
                    VehicleId = secondVehicleId,
                    DepositId = secondDepositId
                }
            ]
        };

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
    public async Task PreviewAsync_PassesShipmentWeightAndUrgency()
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
        Assert.Equal(ShipmentPriorityScoring.UrgentUrgency, shipment.Urgency);
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
        ArrangeNoSolution(RoutingSolveFailure.Infeasible);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().PreviewAsync(Request));

        Assert.Contains("No feasible route", exception.Message);
    }

    /// <summary>
    /// A fleet that cannot hold the whole plan is the one failure the operator can act on, so the
    /// message has to carry the shortfall rather than just saying no.
    /// </summary>
    [Fact]
    public async Task PreviewAsync_FleetTooSmallForTheLoad_NamesTheShortfall()
    {
        // Neither shipment fits in the vehicle on its own, so the capacity filter lets the driver
        // through and the shortfall is only visible once the two loads are added up.
        ArrangeValidScenario();
        ArrangeShipments(
            BuildShipment(_shipmentId, weightKg: 400m),
            BuildShipment(_otherShipmentId, weightKg: 400m));
        _vehicles.Setup(v => v.GetManyByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([BuildVehicle(_vehicleId, capacityKg: 500m)]);
        ArrangeNoSolution(RoutingSolveFailure.Infeasible);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().PreviewAsync(Request));

        Assert.Contains("No feasible route", exception.Message);
        Assert.Contains("offers 500 kg", exception.Message);
        Assert.Contains("weigh 800 kg", exception.Message);
        Assert.Contains("300 kg short", exception.Message);
    }

    /// <summary>
    /// When capacity is not the problem, saying so is what stops the operator from going shopping
    /// for a truck that would not help.
    /// </summary>
    [Fact]
    public async Task PreviewAsync_CapacityIsEnoughButNoRouteFits_BlamesTheHorizon()
    {
        ArrangeValidScenario(
            BuildShipment(_shipmentId, weightKg: 100m),
            vehicle: BuildVehicle(_vehicleId, capacityKg: 500m));
        ArrangeNoSolution(RoutingSolveFailure.Infeasible);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().PreviewAsync(Request));

        Assert.Contains("Capacity is not the limit", exception.Message);
        Assert.Contains("500 kg available for 100 kg", exception.Message);
        Assert.DoesNotContain("kg short", exception.Message);
    }

    /// <summary>
    /// A search that ran out of time proves nothing, so it must not be reported as an unplannable
    /// plan: the capacity arithmetic is exactly the kind of thing that looks conclusive and is not.
    /// </summary>
    [Fact]
    public async Task PreviewAsync_SearchTimedOut_DoesNotClaimThePlanIsImpossible()
    {
        ArrangeValidScenario();
        ArrangeShipments(
            BuildShipment(_shipmentId, weightKg: 400m),
            BuildShipment(_otherShipmentId, weightKg: 400m));
        _vehicles.Setup(v => v.GetManyByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([BuildVehicle(_vehicleId, capacityKg: 500m)]);
        ArrangeNoSolution(RoutingSolveFailure.NoSolutionWithinTimeLimit);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().PreviewAsync(Request));

        Assert.Contains("No feasible route", exception.Message);
        Assert.Contains("infeasibility is not proven", exception.Message);
        Assert.DoesNotContain("kg short", exception.Message);
    }

    /// <summary>
    /// A rejected model is our defect, not a property of the shipments.
    /// </summary>
    [Fact]
    public async Task PreviewAsync_ModelRejected_ReportsADefect()
    {
        ArrangeValidScenario();
        ArrangeNoSolution(RoutingSolveFailure.InvalidModel);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().PreviewAsync(Request));

        Assert.Contains("rejected the routing model", exception.Message);
    }

    /// <summary>
    /// Each driver arrives already paired with its vehicle, so a vehicle nobody was selected with
    /// is not capacity the solver could have used. Counting it would hide a real shortfall behind
    /// a bigger number than reality.
    /// </summary>
    [Fact]
    public async Task PreviewAsync_UnusedVehicleCapacityDoesNotCountTowardTheShortfall()
    {
        var secondVehicleId = Guid.NewGuid();

        ArrangeValidScenario();
        ArrangeShipments(
            BuildShipment(_shipmentId, weightKg: 400m),
            BuildShipment(_otherShipmentId, weightKg: 400m));
        _vehicles.Setup(v => v.GetManyByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([BuildVehicle(_vehicleId, capacityKg: 500m), BuildVehicle(secondVehicleId, capacityKg: 5000m)]);
        ArrangeNoSolution(RoutingSolveFailure.Infeasible);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().PreviewAsync(Request));

        Assert.Contains("offers 500 kg", exception.Message);
        Assert.Contains("300 kg short", exception.Message);
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
        ArrangeCandidates(
            Candidate(_driverId, successes: 10, kilometers: 50m),
            Candidate(secondDriverId, successes: 10, kilometers: 50m));
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
        ArrangeCandidates(
            Candidate(_driverId, successes: 10, kilometers: 50m),
            Candidate(secondDriverId, successes: 10, kilometers: 50m));
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
        _users.Verify(
            u => u.GetDriverCandidatesByIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PreviewAsync_DriverWithoutCapacityForTheHeaviestShipment_IsExcluded()
    {
        var secondDriverId = Guid.NewGuid();
        var secondVehicleId = Guid.NewGuid();
        ArrangeTwoDrivers(
            secondDriverId,
            secondVehicleId,
            firstVehicleCapacityKg: 5m);

        var result = await CreateService().PreviewAsync(
            TwoDriverRequest(_driverId, _vehicleId, _depositId, secondDriverId, secondVehicleId, _depositId));

        var excluded = Assert.Single(result.ExcludedDrivers);
        Assert.Equal(_driverId, excluded.DriverId);
        Assert.Equal(DriverIneligibilityReason.InsufficientVehicleCapacity, excluded.Reason);
        Assert.Null(excluded.Score);
        Assert.Empty(result.ExcludedShipments);
    }

    [Fact]
    public async Task PreviewAsync_EligibleDriversAreAllServedByTheSolver()
    {
        ArrangeValidScenario();

        var result = await CreateService().PreviewAsync(Request);

        Assert.Empty(result.ExcludedDrivers);
        Assert.Equal(1, CapturedProblem()!.DriverCount);
    }

    /// <summary>
    /// The excluded driver has to leave the problem entirely, matrix included: the solver
    /// validates the matrix size against the node count, so a node left behind would either fail
    /// the contract or keep solving for a vehicle nobody will use.
    /// </summary>
    /// <remarks>
    /// The good driver is submitted second on purpose. The ring geometry used by the matrix
    /// helper puts node 0 farther from the shipments than node 1, so the worst driver has to sit
    /// at node 0 for the distances to work against it, which is what pushes its score under the
    /// floor instead of merely under the average.
    /// </remarks>
    [Fact]
    public async Task PreviewAsync_ExcludedDriverIsRemovedFromTheProblemAndTheMatrix()
    {
        var secondDriverId = Guid.NewGuid();
        var secondVehicleId = Guid.NewGuid();
        ArrangeTwoDrivers(secondDriverId, secondVehicleId, weakDriverId: _driverId);

        var result = await CreateService().PreviewAsync(
            TwoDriverRequest(_driverId, _vehicleId, _depositId, secondDriverId, secondVehicleId, _depositId));

        var problem = CapturedProblem()!;
        Assert.Equal(1, problem.DriverCount);
        Assert.Equal(secondDriverId, problem.Drivers[0].DriverId);
        Assert.Equal(problem.NodeCount, problem.DistanceMatrix.Size);
        Assert.Equal(problem.NodeCount, problem.DurationMatrix.Size);

        var excluded = Assert.Single(result.ExcludedDrivers);
        Assert.Equal(_driverId, excluded.DriverId);
        Assert.Equal(DriverIneligibilityReason.ScoreBelowMinimum, excluded.Reason);

        // Not exactly zero: free capacity and operation cost are equal across the pool, so the
        // weakest driver still scores neutral on those two and lands at 3/21.
        Assert.NotNull(excluded.Score);
        Assert.True(
            excluded.Score < new DriverScoringOptions().MinimumScore,
            $"Expected a score below the floor, got {excluded.Score}.");
    }

    /// <summary>
    /// A deposit only the rejected driver was assigned to must not stay in the problem, or the
    /// solver gets an endpoint no route can reach.
    /// </summary>
    [Fact]
    public async Task PreviewAsync_DepositOrphanedByAnExcludedDriverIsDropped()
    {
        var secondDriverId = Guid.NewGuid();
        var secondVehicleId = Guid.NewGuid();
        var secondDepositId = Guid.NewGuid();
        ArrangeTwoDrivers(secondDriverId, secondVehicleId, weakDriverId: _driverId, secondDriverLocation: new Coordinate(-34.90m, -58.90m));
        _deposits.Setup(d => d.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                BuildDeposit(_depositId, Depot),
                BuildDeposit(secondDepositId, new Coordinate(-34.90m, -58.90m))
            ]);

        await CreateService().PreviewAsync(
            TwoDriverRequest(_driverId, _vehicleId, secondDepositId, secondDriverId, secondVehicleId, _depositId));

        var problem = CapturedProblem()!;
        Assert.Equal([_depositId], problem.DepositNodeOrder);
        Assert.Equal(problem.NodeCount, problem.DistanceMatrix.Size);
    }

    /// <summary>
    /// The score has to reach the solver, otherwise the eligibility filter would rank drivers
    /// without ever letting the routing decision benefit from it.
    /// </summary>
    [Fact]
    public async Task PreviewAsync_PassesTheEligibilityScoreToTheSolver()
    {
        ArrangeValidScenario();

        await CreateService().PreviewAsync(Request);

        Assert.Equal(0.5m, CapturedProblem()!.Drivers[0].Score);
    }

    [Fact]
    public async Task PreviewAsync_EveryDriverIneligible_Throws()
    {
        ArrangeValidScenario(vehicle: BuildVehicle(_vehicleId, capacityKg: 5m));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().PreviewAsync(Request));

        Assert.Contains("is eligible to plan", exception.Message);
        Assert.Contains(_driverId.ToString(), exception.Message);
        _solver.Verify(s => s.Solve(It.IsAny<VehicleRoutingProblem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
