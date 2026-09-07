using Domain.Entities;
using Domain.ValueObjects;

namespace UnitTests.Domain;

public class RouteStopTests
{
    [Fact]
    public void Create_LinksShipment()
    {
        var shipment = Shipment.Create(
            Order.Create(new Customer(), new Admin(), [OrderItem.Create("Item", 10m, 1, 5m)]));

        var stop = RouteStop.Create(shipment, new Coordinate(20, 20), 2, "Cliente A");

        Assert.Equal(shipment.Id, stop.ShipmentId);
        Assert.Equal(new Coordinate(20, 20), stop.Coordinate);
        Assert.Equal(2, stop.StopOrder);
        Assert.Equal("Cliente A", stop.Name);
    }

    [Fact]
    public void Create_WithInvalidStopOrder_Throws()
    {
        var shipment = Shipment.Create(
            Order.Create(new Customer(), new Admin(), [OrderItem.Create("Item", 10m, 1, 5m)]));

        Assert.Throws<ArgumentException>(
            () => RouteStop.Create(shipment, new Coordinate(20, 20), 0));
    }

    [Fact]
    public void AssignToRoute_SetsRoute()
    {
        var route = Route.Create(new Coordinate(10, 10));
        var shipment = Shipment.Create(
            Order.Create(new Customer(), new Admin(), [OrderItem.Create("Item", 10m, 1, 5m)]));
        var stop = RouteStop.Create(shipment, new Coordinate(20, 20), 1);

        stop.AssignToRoute(route);

        Assert.Equal(route.Id, stop.RouteId);
    }
}