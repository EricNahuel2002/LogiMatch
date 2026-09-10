using Application.Dtos.Drivers;
using Application.Exceptions;
using Application.Integrations;
using Application.Persistence;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using FluentValidation;
using Moq;

namespace UnitTests.Application;

public class DriverServiceTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IRouteRepository> _routes = new();
    private readonly Mock<IRouteStopRepository> _routeStops = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IEmailSender> _emailSender = new();
    private readonly Mock<IValidator<UpdateDriverLocationRequest>> _updateLocationValidator =
        FluentValidationMocks.AlwaysValid<UpdateDriverLocationRequest>();
    private readonly Mock<IValidator<CancelRouteRequest>> _cancelRouteValidator =
        FluentValidationMocks.AlwaysValid<CancelRouteRequest>();

    private DriverService CreateService()
    {
        return new DriverService(
            _users.Object,
            _routes.Object,
            _routeStops.Object,
            _unitOfWork.Object,
            _emailSender.Object,
            _updateLocationValidator.Object,
            _cancelRouteValidator.Object);
    }

    [Fact]
    public async Task UpdateLocationAsync_SetsLocationAndSaves()
    {
        var driver = new Driver();
        _users.Setup(u => u.GetDriverByIdAsync(driver.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(driver);

        var service = CreateService();
        await service.UpdateLocationAsync(driver.Id, new UpdateDriverLocationRequest
        {
            Latitude = -34.6m,
            Longitude = -58.4m
        });

        Assert.Equal(new Coordinate(-34.6m, -58.4m), driver.CurrentLocation);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateLocationAsync_WhenDriverNotFound_ThrowsNotFoundException()
    {
        _users.Setup(u => u.GetDriverByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Driver?)null);

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.UpdateLocationAsync(Guid.NewGuid(), new UpdateDriverLocationRequest()));
    }

    [Fact]
    public async Task CancelCurrentRouteAsync_RequeuesShipmentsAndNotifiesAdmins()
    {
        var driver = new Driver { Name = "John", Surname = "Doe", Email = "john@logimatch.com" };
        var route = Route.Create(new Coordinate(10, 10));
        route.AssignDriver(driver);

        var shipment = BuildShipment();
        shipment.Start();
        var stop = RouteStop.Create(shipment, new Coordinate(20, 20), 1, "Cliente A");
        stop.AssignToRoute(route);
        route.AddStop(stop);

        _users.Setup(u => u.GetDriverByIdAsync(driver.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(driver);
        _routes.Setup(r => r.GetActiveByDriverIdAsync(driver.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(route);
        _users.Setup(u => u.GetAdminsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Admin> { new() { Email = "admin@logimatch.com" } });

        var service = CreateService();
        await service.CancelCurrentRouteAsync(
            driver.Id,
            new CancelRouteRequest { Reason = RouteCancellationReason.VehicleBreakdown, Note = "Engine failure" });

        Assert.Equal(ShipmentStatus.Pending, shipment.Status);
        Assert.False(route.IsActive);
        Assert.Equal(RouteCancellationReason.VehicleBreakdown, route.CancellationReason);
        _routeStops.Verify(r => r.Remove(stop), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _emailSender.Verify(
            e => e.SendAsync(
                It.Is<IReadOnlyCollection<string>>(r => r.Count == 1 && r.First() == "admin@logimatch.com"),
                It.IsAny<string>(),
                It.Is<string>(b => b.Contains(shipment.Id.ToString())),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CancelCurrentRouteAsync_WhenNoActiveRoute_ThrowsNotFoundException()
    {
        var driver = new Driver();
        _users.Setup(u => u.GetDriverByIdAsync(driver.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(driver);
        _routes.Setup(r => r.GetActiveByDriverIdAsync(driver.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Route?)null);

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.CancelCurrentRouteAsync(driver.Id, new CancelRouteRequest
            {
                Reason = RouteCancellationReason.RouteAbandonment
            }));

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _emailSender.Verify(
            e => e.SendAsync(
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CancelCurrentRouteAsync_TerminalShipmentsAreNotRequeued()
    {
        var driver = new Driver { Name = "John", Surname = "Doe", Email = "john@logimatch.com" };
        var route = Route.Create(new Coordinate(10, 10));
        route.AssignDriver(driver);

        var delivered = BuildShipment();
        delivered.Start();
        delivered.MarkArrivedAtDestination();
        delivered.RegisterDeliveryAttempt(null, true);

        var stopped = BuildShipment();
        stopped.Start();
        stopped.Stop();

        var stop1 = RouteStop.Create(delivered, new Coordinate(20, 20), 1);
        stop1.AssignToRoute(route);
        route.AddStop(stop1);
        var stop2 = RouteStop.Create(stopped, new Coordinate(30, 30), 2);
        stop2.AssignToRoute(route);
        route.AddStop(stop2);

        _users.Setup(u => u.GetDriverByIdAsync(driver.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(driver);
        _routes.Setup(r => r.GetActiveByDriverIdAsync(driver.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(route);
        _users.Setup(u => u.GetAdminsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Admin> { new() { Email = "admin@logimatch.com" } });

        var service = CreateService();
        await service.CancelCurrentRouteAsync(
            driver.Id,
            new CancelRouteRequest { Reason = RouteCancellationReason.Accident });

        Assert.Equal(ShipmentStatus.Finalized, delivered.Status);
        Assert.Equal(ShipmentStatus.Pending, stopped.Status);
        _routeStops.Verify(r => r.Remove(stop1), Times.Once);
        _routeStops.Verify(r => r.Remove(stop2), Times.Once);
    }

    private static Shipment BuildShipment()
    {
        var order = Order.Create(
            new Customer(),
            new Admin(),
            [OrderItem.Create("Item", 10m, 1, 5m)]);
        return Shipment.Create(order);
    }
}