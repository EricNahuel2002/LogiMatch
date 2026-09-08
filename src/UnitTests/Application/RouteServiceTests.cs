using Application.Dtos.Routes;
using Application.Persistence;
using Application.Services;
using Domain.Entities;
using Domain.ValueObjects;
using FluentValidation;
using Moq;

namespace UnitTests.Application;

public class RouteServiceTests
{
    private readonly Mock<IRouteRepository> _routes = new();
    private readonly Mock<IRouteStopRepository> _routeStops = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IVehicleRepository> _vehicles = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IValidator<CreateRouteRequest>> _createRouteValidator =
        FluentValidationMocks.AlwaysValid<CreateRouteRequest>();

    private RouteService CreateService()
    {
        return new RouteService(
            _routes.Object,
            _routeStops.Object,
            _users.Object,
            _vehicles.Object,
            _unitOfWork.Object,
            _createRouteValidator.Object);
    }

    private static Shipment BuildPendingShipment()
    {
        var order = Order.Create(new Customer(), new Admin(), [OrderItem.Create("Item", 1m, 1, 5m)]);
        return Shipment.Create(order);
    }

    [Fact]
    public async Task CreateAsync_CreatesRouteWithOriginAndSaves()
    {
        Route? created = null;
        _routes.Setup(r => r.AddAsync(It.IsAny<Route>(), It.IsAny<CancellationToken>()))
            .Callback<Route, CancellationToken>((route, _) => created = route)
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var id = await service.CreateAsync(new CreateRouteRequest { Latitude = -34.6m, Longitude = -58.4m });

        Assert.Equal(created!.Id, id);
        Assert.Equal(-34.6m, created.Origin.Latitude);
        Assert.Equal(-58.4m, created.Origin.Longitude);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssignDriverAsync_AssignsDriverAndSaves()
    {
        var route = Route.Create(new Coordinate(0, 0));
        var driver = new Driver();
        _routes.Setup(r => r.GetByIdAsync(route.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(route);
        _users.Setup(u => u.GetDriverByIdAsync(driver.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(driver);

        var service = CreateService();
        await service.AssignDriverAsync(route.Id, driver.Id);

        Assert.Equal(driver.Id, route.DriverId);
        Assert.Same(driver, route.Driver);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssignVehicleAsync_AssignsVehicleAndSaves()
    {
        var route = Route.Create(new Coordinate(0, 0));
        var vehicle = Vehicle.Create("ABC123", 1000m);
        _routes.Setup(r => r.GetByIdAsync(route.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(route);
        _vehicles.Setup(v => v.GetByIdAsync(vehicle.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vehicle);

        var service = CreateService();
        await service.AssignVehicleAsync(route.Id, vehicle.Id);

        Assert.Equal(vehicle.Id, route.VehicleId);
        Assert.Same(vehicle, route.Vehicle);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddStopAsync_AddsStopToRouteAndSaves()
    {
        var route = Route.Create(new Coordinate(0, 0));
        var stop = RouteStop.Create(BuildPendingShipment(), new Coordinate(1, 1), 1, "Client A");
        _routes.Setup(r => r.GetByIdAsync(route.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(route);
        _routeStops.Setup(r => r.GetByIdAsync(stop.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stop);

        var service = CreateService();
        await service.AddStopAsync(route.Id, stop.Id);

        Assert.Contains(stop, route.RouteStops);
        Assert.Equal(route.Id, stop.RouteId);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}