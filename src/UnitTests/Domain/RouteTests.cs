using Domain.Entities;
using Domain.ValueObjects;

namespace UnitTests.Domain;

public class RouteTests
{
    private readonly Route _route = Route.Create(new Coordinate(10, 10));

    [Fact]
    public void Create_SetsOrigin()
    {
        Assert.Equal(new Coordinate(10, 10), _route.Origin);
    }

    [Fact]
    public void AssignDriver_SetsDriver()
    {
        var driver = new Driver();

        _route.AssignDriver(driver);

        Assert.Equal(driver.Id, _route.DriverId);
    }

    [Fact]
    public void AssignVehicle_SetsVehicle()
    {
        var vehicle = Vehicle.Create("ABC123", 1000m);

        _route.AssignVehicle(vehicle);

        Assert.Equal(vehicle.Id, _route.VehicleId);
    }

    [Fact]
    public void AddStop_AddsRouteStop()
    {
        var shipment = Shipment.Create(
            Order.Create(new Customer(), new Admin(), [OrderItem.Create("Item", 10m, 1)]));
        var stop = RouteStop.Create(shipment, new Coordinate(20, 20), 1, "Cliente A");
        stop.AssignToRoute(_route);

        _route.AddStop(stop);

        Assert.Single(_route.RouteStops);
    }

    [Fact]
    public void AddStop_DuplicateOrder_Throws()
    {
        var stop1 = RouteStop.Create(
            Shipment.Create(Order.Create(new Customer(), new Admin(), [OrderItem.Create("A", 10m, 1)])),
            new Coordinate(20, 20), 1);
        stop1.AssignToRoute(_route);
        _route.AddStop(stop1);

        var stop2 = RouteStop.Create(
            Shipment.Create(Order.Create(new Customer(), new Admin(), [OrderItem.Create("B", 10m, 1)])),
            new Coordinate(20, 20), 1);
        stop2.AssignToRoute(_route);

        Assert.Throws<InvalidOperationException>(() => _route.AddStop(stop2));
    }
}