using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

namespace UnitTests.Domain;

public class DeliveryAttemptTests
{
    private readonly Vehicle _vehicle = Vehicle.Create("ABC123", 1000m);
    private readonly Driver _driver = new();
    private readonly Route _route = new Route
    {
        Origin = new Coordinate(10, 10),
        Destination = new Coordinate(20, 20)
    };
    private readonly Order _order = Order.Create(new Customer(), new Admin(), [OrderItem.Create("Item", 10m, 1)]);

    private Shipment BuildArrivedShipment()
    {
        var shipment = Shipment.Create();
        shipment.AssignRoute(_route);
        shipment.AssignDriver(_driver);
        shipment.AssignVehicle(_vehicle);
        shipment.AddOrder(_order);
        shipment.Start();
        shipment.MarkArrivedAtDestination();
        return shipment;
    }

    [Fact]
    public void RegisterDeliveryAttempt_WhenNotArrived_Throws()
    {
        var shipment = Shipment.Create();
        shipment.AssignRoute(_route);
        shipment.AssignDriver(_driver);
        shipment.AssignVehicle(_vehicle);
        shipment.AddOrder(_order);
        shipment.Start();

        Assert.Throws<InvalidOperationException>(
            () => shipment.RegisterDeliveryAttempt(_order, null, false));
    }

    [Fact]
    public void RegisterDeliveryAttempt_ForOrderNotInShipment_Throws()
    {
        var shipment = BuildArrivedShipment();
        var foreignOrder = Order.Create(new Customer(), new Admin(), [OrderItem.Create("Other", 5m, 1)]);

        Assert.Throws<InvalidOperationException>(
            () => shipment.RegisterDeliveryAttempt(foreignOrder, null, false));
    }

    [Fact]
    public void RegisterDeliveryAttempt_WhenSucceeded_FinalizesShipment()
    {
        var shipment = BuildArrivedShipment();

        shipment.RegisterDeliveryAttempt(_order, null, true);

        Assert.Equal(ShipmentStatus.Finalized, shipment.Status);
        Assert.True(shipment.DeliveryAttempts.Single().Succeeded);
    }

    [Fact]
    public void RegisterDeliveryAttempt_MaximumOfThreeAttempts()
    {
        var shipment = BuildArrivedShipment();
        var t0 = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        shipment.RegisterDeliveryAttempt(_order, null, false, attemptedAt: t0);
        shipment.RegisterDeliveryAttempt(_order, null, false, attemptedAt: t0.AddMinutes(5));
        shipment.RegisterDeliveryAttempt(_order, null, false, attemptedAt: t0.AddMinutes(10));

        Assert.Equal(ShipmentStatus.DeliveryFailed, shipment.Status);
        Assert.Equal(3, shipment.DeliveryAttempts.Count);

        Assert.Throws<InvalidOperationException>(
            () => shipment.RegisterDeliveryAttempt(_order, null, false, attemptedAt: t0.AddMinutes(15)));
    }

    [Fact]
    public void RegisterDeliveryAttempt_TooSoon_Throws()
    {
        var shipment = BuildArrivedShipment();
        var t0 = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        shipment.RegisterDeliveryAttempt(_order, null, false, attemptedAt: t0);

        Assert.Throws<InvalidOperationException>(
            () => shipment.RegisterDeliveryAttempt(_order, null, false, attemptedAt: t0.AddMinutes(1)));
    }
}