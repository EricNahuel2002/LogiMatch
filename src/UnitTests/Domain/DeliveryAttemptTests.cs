using Domain.Entities;
using Domain.Enums;

namespace UnitTests.Domain;

public class DeliveryAttemptTests
{
    private readonly Order _order = Order.Create(new Customer(), new Admin(), [OrderItem.Create("Item", 10m, 1)]);

    private Shipment BuildArrivedShipment()
    {
        var shipment = Shipment.Create(_order);
        shipment.Start();
        shipment.MarkArrivedAtDestination();
        return shipment;
    }

    [Fact]
    public void RegisterDeliveryAttempt_WhenNotArrived_Throws()
    {
        var shipment = Shipment.Create(_order);
        shipment.Start();

        Assert.Throws<InvalidOperationException>(
            () => shipment.RegisterDeliveryAttempt(null, false));
    }

    [Fact]
    public void RegisterDeliveryAttempt_WhenSucceeded_FinalizesShipment()
    {
        var shipment = BuildArrivedShipment();

        shipment.RegisterDeliveryAttempt(null, true);

        Assert.Equal(ShipmentStatus.Finalized, shipment.Status);
        Assert.True(shipment.DeliveryAttempts.Single().Succeeded);
    }

    [Fact]
    public void RegisterDeliveryAttempt_MaximumOfThreeAttempts()
    {
        var shipment = BuildArrivedShipment();
        var t0 = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        shipment.RegisterDeliveryAttempt(null, false, attemptedAt: t0);
        shipment.RegisterDeliveryAttempt(null, false, attemptedAt: t0.AddMinutes(5));
        shipment.RegisterDeliveryAttempt(null, false, attemptedAt: t0.AddMinutes(10));

        Assert.Equal(ShipmentStatus.DeliveryFailed, shipment.Status);
        Assert.Equal(3, shipment.DeliveryAttempts.Count);

        Assert.Throws<InvalidOperationException>(
            () => shipment.RegisterDeliveryAttempt(null, false, attemptedAt: t0.AddMinutes(15)));
    }

    [Fact]
    public void RegisterDeliveryAttempt_TooSoon_Throws()
    {
        var shipment = BuildArrivedShipment();
        var t0 = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        shipment.RegisterDeliveryAttempt(null, false, attemptedAt: t0);

        Assert.Throws<InvalidOperationException>(
            () => shipment.RegisterDeliveryAttempt(null, false, attemptedAt: t0.AddMinutes(1)));
    }
}