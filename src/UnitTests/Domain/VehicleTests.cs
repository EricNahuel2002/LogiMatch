using Domain.Entities;
using Domain.ValueObjects;

namespace UnitTests.Domain;

public class VehicleTests
{
    [Fact]
    public void Create_SetsDefaults()
    {
        var vehicle = Vehicle.Create("ABC123", 1000m, "Brand", "Model");

        Assert.Equal("ABC123", vehicle.LicensePlate);
        Assert.Equal(1000m, vehicle.CapacityKg);
        Assert.Equal("Brand", vehicle.Brand);
        Assert.Equal("Model", vehicle.Model);
        Assert.True(vehicle.Active);
        Assert.Equal(0m, vehicle.KilometersPerDay);
        Assert.Equal(0m, vehicle.FuelConsumption);
        Assert.Equal(0m, vehicle.FuelPrice);
        Assert.Equal(0m, vehicle.MaintenanceCost);
    }

    [Fact]
    public void Create_WithOperatingCosts_SetsThey()
    {
        var vehicle = Vehicle.Create("ABC123", 1000m, null, null, 0.12m, 1100m, 80m);

        Assert.Equal(0.12m, vehicle.FuelConsumption);
        Assert.Equal(1100m, vehicle.FuelPrice);
        Assert.Equal(80m, vehicle.MaintenanceCost);
    }

    [Fact]
    public void Create_WithInvalidCapacity_Throws()
    {
        Assert.Throws<ArgumentException>(() => Vehicle.Create("ABC123", 0m));
    }

    [Fact]
    public void Create_WithNegativeFuelConsumption_Throws()
    {
        Assert.Throws<ArgumentException>(() => Vehicle.Create("ABC123", 1000m, null, null, -1m));
    }

    [Fact]
    public void SetOperatingCosts_UpdatesCosts()
    {
        var vehicle = Vehicle.Create("ABC123", 1000m);

        vehicle.SetOperatingCosts(0.1m, 1200m, 60m);

        Assert.Equal(0.1m, vehicle.FuelConsumption);
        Assert.Equal(1200m, vehicle.FuelPrice);
        Assert.Equal(60m, vehicle.MaintenanceCost);
    }

    [Fact]
    public void RecalculateKilometersPerDay_SumsOnlyArrivedShipments()
    {
        var vehicle = Vehicle.Create("ABC123", 1000m);
        var route = Route.Create(new Coordinate(10, 10));
        route.AssignVehicle(vehicle);

        var arrived = BuildArrivedShipment();
        var arrivedStop = RouteStop.Create(arrived, new Coordinate(20, 20), 1, null, 4500m);
        arrivedStop.AssignToRoute(route);
        route.AddStop(arrivedStop);

        var pending = Shipment.Create(
            Order.Create(new Customer(), new Admin(), [OrderItem.Create("Item", 10m, 1, 5m)]));
        var pendingStop = RouteStop.Create(pending, new Coordinate(30, 30), 2, null, 9000m);
        pendingStop.AssignToRoute(route);
        route.AddStop(pendingStop);

        vehicle.Routes.Add(route);

        Assert.Equal(4.5m, vehicle.RecalculateKilometersPerDay());
        Assert.Equal(4.5m, vehicle.KilometersPerDay);
    }

    private static Shipment BuildArrivedShipment()
    {
        var shipment = Shipment.Create(
            Order.Create(new Customer(), new Admin(), [OrderItem.Create("Item", 10m, 1, 5m)]));
        shipment.Start();
        shipment.MarkArrivedAtDestination();
        return shipment;
    }

    [Fact]
    public void SetActive_ChangesAvailability()
    {
        var vehicle = Vehicle.Create("ABC123", 1000m);

        vehicle.SetActive(false);
        Assert.False(vehicle.Active);

        vehicle.SetActive(true);
        Assert.True(vehicle.Active);
    }
}