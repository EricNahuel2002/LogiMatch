using Application.Integrations;
using Application.Persistence;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Moq;

namespace UnitTests.Application;

public class DelayRiskServiceTests
{
    private readonly Mock<IShipmentRepository> _shipments = new();
    private readonly Mock<IRouteRepository> _routes = new();
    private readonly Mock<IRouteClient> _routeClient = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private DelayRiskService CreateService()
    {
        return new DelayRiskService(
            _shipments.Object,
            _routes.Object,
            _routeClient.Object,
            _unitOfWork.Object);
    }

    [Fact]
    public async Task ExecuteRecalculationAsync_UpdatesDriverAverageTimeBetweenOrders()
    {
        var driver = new Driver { Id = Guid.NewGuid() };
        var first = BuildShipment(
            driver,
            new DateTime(2026, 9, 17, 10, 0, 0),
            new DateTime(2026, 9, 17, 11, 0, 0));
        var second = BuildShipment(
            driver,
            new DateTime(2026, 9, 17, 11, 30, 0),
            new DateTime(2026, 9, 17, 12, 0, 0));

        _shipments.Setup(r => r.GetAssignedWithHistoryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([first, second]);
        _routes.Setup(r => r.GetActiveRoutesForDelayRiskAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = CreateService();

        await service.ExecuteRecalculationAsync();

        Assert.Equal(TimeSpan.FromMinutes(30), driver.AverageTimeBetweenOrders);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteRecalculationAsync_PersistsDelayRiskPercentageOnCurrentShipment()
    {
        var driver = new Driver { Id = Guid.NewGuid() };
        driver.SetCurrentLocation(new Coordinate(-34.6m, -58.4m));

        var nowLocal = GetArgentinaNow();
        var current = BuildPendingShipment(nowLocal.AddMinutes(-60), nowLocal.AddMinutes(10));
        var next = BuildPendingShipment(nowLocal.AddMinutes(-60), nowLocal.AddMinutes(120));

        var route = Route.Create(new Coordinate(0m, 0m));
        route.AssignDriver(driver);
        route.AddStop(RouteStop.Create(current, new Coordinate(-34.6m, -58.45m), 1));
        route.AddStop(RouteStop.Create(next, new Coordinate(-34.55m, -58.45m), 2));

        _shipments.Setup(r => r.GetAssignedWithHistoryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _routes.Setup(r => r.GetActiveRoutesForDelayRiskAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Route> { route });
        _routeClient.Setup(r => r.GetDrivingMetricsAsync(
                It.IsAny<IReadOnlyCollection<RouteOrigin>>(),
                It.IsAny<Coordinate>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, RouteMetrics> { [driver.Id] = new(10000, 25) });

        var service = CreateService();

        await service.ExecuteRecalculationAsync();

        Assert.Equal(45m, current.DelayRiskPercentage);
    }

    [Fact]
    public async Task ExecuteRecalculationAsync_DriverWithoutLocation_IsSkipped()
    {
        var driver = new Driver { Id = Guid.NewGuid() };
        var shipment = BuildPendingShipment(DateTime.UtcNow.AddMinutes(-60), DateTime.UtcNow.AddMinutes(10));

        var route = Route.Create(new Coordinate(0m, 0m));
        route.AssignDriver(driver);
        route.AddStop(RouteStop.Create(shipment, new Coordinate(-34.6m, -58.4m), 1));

        _shipments.Setup(r => r.GetAssignedWithHistoryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _routes.Setup(r => r.GetActiveRoutesForDelayRiskAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Route> { route });

        var service = CreateService();

        await service.ExecuteRecalculationAsync();

        _routeClient.Verify(r => r.GetDrivingMetricsAsync(
            It.IsAny<IReadOnlyCollection<RouteOrigin>>(),
            It.IsAny<Coordinate>(),
            It.IsAny<CancellationToken>()), Times.Never);
        Assert.Null(shipment.DelayRiskPercentage);
    }

    [Fact]
    public async Task ExecuteRecalculationAsync_RouteWithoutActiveShipment_IsSkipped()
    {
        var driver = new Driver { Id = Guid.NewGuid() };
        driver.SetCurrentLocation(new Coordinate(-34.6m, -58.4m));

        var finalized = BuildPendingShipment(DateTime.UtcNow.AddMinutes(-60), DateTime.UtcNow.AddMinutes(10));
        finalized.Start();
        finalized.Cancel("cancelled for test");

        var route = Route.Create(new Coordinate(0m, 0m));
        route.AssignDriver(driver);
        var stop = RouteStop.Create(finalized, new Coordinate(-34.6m, -58.4m), 1);
        route.AddStop(stop);
        typeof(Shipment).GetProperty(nameof(Shipment.RouteStop))!.SetValue(finalized, stop);

        _shipments.Setup(r => r.GetAssignedWithHistoryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _routes.Setup(r => r.GetActiveRoutesForDelayRiskAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Route> { route });

        var service = CreateService();

        await service.ExecuteRecalculationAsync();

        _routeClient.Verify(r => r.GetDrivingMetricsAsync(
            It.IsAny<IReadOnlyCollection<RouteOrigin>>(),
            It.IsAny<Coordinate>(),
            It.IsAny<CancellationToken>()), Times.Never);
        Assert.Null(finalized.DelayRiskPercentage);
    }

    private static Shipment BuildShipment(Driver driver, DateTime? startedAt, DateTime? finalizedAt)
    {
        var order = Order.Create(
            new Customer(),
            new Admin(),
            [OrderItem.Create("Item", 10m, 1, 1m)]);
        order.SetAssignedDriver(driver.Id);
        typeof(Order).GetProperty(nameof(Order.AssignedDriver))!.SetValue(order, driver);

        var shipment = Shipment.Create(order);

        if (startedAt is { } start)
        {
            shipment.History.Add(new ShipmentHistory
            {
                Status = ShipmentStatus.InProgress,
                RecordedAt = start
            });
        }

        if (finalizedAt is { } final)
        {
            shipment.History.Add(new ShipmentHistory
            {
                Status = ShipmentStatus.Finalized,
                RecordedAt = final
            });
        }

        return shipment;
    }

    private static Shipment BuildPendingShipment(DateTime windowStart, DateTime windowEnd)
    {
        var order = Order.Create(
            new Customer(),
            new Admin(),
            [OrderItem.Create("Item", 10m, 1, 1m)],
            windowStart,
            windowEnd);

        return Shipment.Create(order);
    }

    private static DateTime GetArgentinaNow()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
    }
}