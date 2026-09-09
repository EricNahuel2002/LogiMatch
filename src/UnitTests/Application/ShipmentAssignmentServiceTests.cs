using Application.Exceptions;
using Application.Integrations;
using Application.Persistence;
using Application.Services;
using Domain.Entities;
using Domain.ValueObjects;
using Moq;

namespace UnitTests.Application;

public class ShipmentAssignmentServiceTests
{
    private readonly Mock<IShipmentRepository> _shipments = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IRouteClient> _routeClient = new();

    private ShipmentAssignmentService CreateService()
    {
        return new ShipmentAssignmentService(_shipments.Object, _users.Object, _routeClient.Object);
    }

    private static Shipment BuildPendingShipmentWithStop(
        decimal weightKg = 10m,
        DateTime? windowStart = null,
        DateTime? windowEnd = null)
    {
        var order = Order.Create(
            new Customer(),
            new Admin(),
            [OrderItem.Create("Item", 10m, 1, weightKg)],
            windowStart,
            windowEnd);
        var shipment = Shipment.Create(order);
        var stop = RouteStop.Create(shipment, new Coordinate(-34.6m, -58.4m), 1);

        typeof(Shipment).GetProperty(nameof(Shipment.RouteStop))!.SetValue(shipment, stop);

        return shipment;
    }

    private static DriverAssignmentCandidate Candidate(
        Guid driverId,
        decimal capacityKg,
        decimal inProgressWeightKg = 0m,
        int pending = 0,
        int inProgress = 0,
        int attempts = 0)
    {
        return new DriverAssignmentCandidate(
            driverId,
            new Coordinate(-34.6m, -58.4m),
            capacityKg,
            attempts,
            pending,
            inProgress,
            inProgressWeightKg);
    }

    private void SetupDrivingMetrics(params (Guid DriverId, int Meters, int Minutes)[] metrics)
    {
        _ = _routeClient.Setup(r => r.GetDrivingMetricsAsync(
            It.IsAny<IReadOnlyCollection<RouteOrigin>>(),
            It.IsAny<Coordinate>(),
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(metrics.ToDictionary(
            m => m.DriverId,
            m => new RouteMetrics(m.Meters, m.Minutes)));
    }

    private static DateTime GetArgentinaNow()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
    }

    [Fact]
    public async Task SuggestDriverAsync_WhenShipmentNotFound_ThrowsNotFoundException()
    {
        _shipments.Setup(r => r.GetByIdWithAssignmentDetailsAsync(
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Shipment?)null);

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.SuggestDriverAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task SuggestDriverAsync_WhenShipmentNotPending_Throws()
    {
        var shipment = BuildPendingShipmentWithStop();
        shipment.Start();
        _shipments.Setup(r => r.GetByIdWithAssignmentDetailsAsync(
            shipment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipment);

        var service = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SuggestDriverAsync(shipment.Id));
    }

    [Fact]
    public async Task SuggestDriverAsync_WhenNoRouteStop_Throws()
    {
        var order = Order.Create(new Customer(), new Admin(), [OrderItem.Create("Item", 10m, 1, 10m)]);
        var shipment = Shipment.Create(order);
        _shipments.Setup(r => r.GetByIdWithAssignmentDetailsAsync(
            shipment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipment);

        var service = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SuggestDriverAsync(shipment.Id));
    }

    [Fact]
    public async Task SuggestDriverAsync_WhenNoEligibleDrivers_Throws()
    {
        var shipment = BuildPendingShipmentWithStop();
        _shipments.Setup(r => r.GetByIdWithAssignmentDetailsAsync(
            shipment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipment);
        _users.Setup(u => u.GetDriverCandidatesAsync(
            It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Candidate(Guid.NewGuid(), 5m)]);

        var service = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SuggestDriverAsync(shipment.Id));
    }

    [Fact]
    public async Task SuggestDriverAsync_ClosestDriverWithEnoughCapacity_Wins()
    {
        var shipment = BuildPendingShipmentWithStop();
        var driverA = Guid.NewGuid();
        var driverB = Guid.NewGuid();

        _shipments.Setup(r => r.GetByIdWithAssignmentDetailsAsync(
            shipment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipment);
        _users.Setup(u => u.GetDriverCandidatesAsync(
            It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Candidate(driverA, 100m), Candidate(driverB, 100m)]);
        SetupDrivingMetrics((driverA, 5000, 30), (driverB, 1000, 10));

        var service = CreateService();
        var suggestion = await service.SuggestDriverAsync(shipment.Id);

        Assert.Equal(driverB, suggestion.RecommendedDriverId);
        Assert.Equal(2, suggestion.Ranking.Count);
        Assert.Equal(driverB, suggestion.Ranking[0].DriverId);
    }

    [Fact]
    public async Task SuggestDriverAsync_DriverWithInsufficientCapacity_IsExcluded()
    {
        var shipment = BuildPendingShipmentWithStop(10m);
        var smallCapacity = Guid.NewGuid();
        var enoughCapacity = Guid.NewGuid();

        _shipments.Setup(r => r.GetByIdWithAssignmentDetailsAsync(
            shipment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipment);
        _users.Setup(u => u.GetDriverCandidatesAsync(
            It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Candidate(smallCapacity, 5m), Candidate(enoughCapacity, 100m)]);
        SetupDrivingMetrics((smallCapacity, 100, 5), (enoughCapacity, 200, 10));

        var service = CreateService();
        var suggestion = await service.SuggestDriverAsync(shipment.Id);

        Assert.Single(suggestion.Ranking);
        Assert.Equal(enoughCapacity, suggestion.Ranking[0].DriverId);
    }

    [Fact]
    public async Task SuggestDriverAsync_InProgressWeight_ReducesFreeCapacity()
    {
        var shipment = BuildPendingShipmentWithStop(50m);
        var heavyDriver = Guid.NewGuid();
        var freeDriver = Guid.NewGuid();

        _shipments.Setup(r => r.GetByIdWithAssignmentDetailsAsync(
            shipment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipment);
        _users.Setup(u => u.GetDriverCandidatesAsync(
            It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                Candidate(heavyDriver, 100m, inProgressWeightKg: 60m),
                Candidate(freeDriver, 100m)
            ]);
        SetupDrivingMetrics((heavyDriver, 100, 5), (freeDriver, 100, 5));

        var service = CreateService();
        var suggestion = await service.SuggestDriverAsync(shipment.Id);

        Assert.Single(suggestion.Ranking);
        Assert.Equal(freeDriver, suggestion.Ranking[0].DriverId);
    }

    [Fact]
    public async Task SuggestDriverAsync_WhenArrivalOutsideWindow_Throws()
    {
        var nowLocal = GetArgentinaNow();
        var shipment = BuildPendingShipmentWithStop(10m, nowLocal.AddMinutes(-60), nowLocal.AddMinutes(-10));
        var driverA = Guid.NewGuid();
        var driverB = Guid.NewGuid();

        _shipments.Setup(r => r.GetByIdWithAssignmentDetailsAsync(
            shipment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipment);
        _users.Setup(u => u.GetDriverCandidatesAsync(
            It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Candidate(driverA, 100m), Candidate(driverB, 100m)]);
        SetupDrivingMetrics((driverA, 5000, 30), (driverB, 1000, 5));

        var service = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SuggestDriverAsync(shipment.Id));
    }

    [Fact]
    public async Task SuggestDriverAsync_WhenArrivalWithinWindow_SuggestsClosestDriver()
    {
        var nowLocal = GetArgentinaNow();
        var shipment = BuildPendingShipmentWithStop(10m, nowLocal.AddMinutes(-60), nowLocal.AddHours(12));
        var driverA = Guid.NewGuid();
        var driverB = Guid.NewGuid();

        _shipments.Setup(r => r.GetByIdWithAssignmentDetailsAsync(
            shipment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipment);
        _users.Setup(u => u.GetDriverCandidatesAsync(
            It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Candidate(driverA, 100m), Candidate(driverB, 100m)]);
        SetupDrivingMetrics((driverA, 5000, 60), (driverB, 1000, 10));

        var service = CreateService();
        var suggestion = await service.SuggestDriverAsync(shipment.Id);

        Assert.Equal(driverB, suggestion.RecommendedDriverId);
        Assert.Equal(2, suggestion.Ranking.Count);
        Assert.Equal(driverB, suggestion.Ranking[0].DriverId);
    }
}