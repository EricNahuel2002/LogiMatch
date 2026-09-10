using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

namespace UnitTests.Domain;

public class ShipmentTests
{
    private readonly Order _order = Order.Create(new Customer(), new Admin(), [OrderItem.Create("Item", 10m, 1, 5m)]);

    [Fact]
    public void Create_LinksSingleOrder()
    {
        var shipment = Shipment.Create(_order);

        Assert.Equal(_order.Id, shipment.OrderId);
        Assert.Equal(shipment.Id, _order.ShipmentId);
    }

    [Fact]
    public void Create_WhenOrderAlreadyAssigned_Throws()
    {
        var first = Shipment.Create(_order);

        Assert.Throws<InvalidOperationException>(() => Shipment.Create(_order));
    }

    [Fact]
    public void Start_SetsInProgress()
    {
        var shipment = Shipment.Create(_order);

        shipment.Start();

        Assert.Equal(ShipmentStatus.InProgress, shipment.Status);
    }

    [Fact]
    public void Start_WhenNotPending_Throws()
    {
        var shipment = Shipment.Create(_order);
        shipment.Start();

        Assert.Throws<InvalidOperationException>(() => shipment.Start());
    }

    [Fact]
    public void Stop_WhenInProgress_SetsStopped()
    {
        var shipment = Shipment.Create(_order);
        shipment.Start();

        shipment.Stop();

        Assert.Equal(ShipmentStatus.Stopped, shipment.Status);
    }

    [Fact]
    public void Stop_WhenNotInProgress_Throws()
    {
        var shipment = Shipment.Create(_order);

        Assert.Throws<InvalidOperationException>(() => shipment.Stop());
    }

    [Fact]
    public void Resume_WhenStopped_SetsInProgress()
    {
        var shipment = Shipment.Create(_order);
        shipment.Start();
        shipment.Stop();

        shipment.Resume();

        Assert.Equal(ShipmentStatus.InProgress, shipment.Status);
    }

    [Fact]
    public void Resume_WhenNotStopped_Throws()
    {
        var shipment = Shipment.Create(_order);
        shipment.Start();

        Assert.Throws<InvalidOperationException>(() => shipment.Resume());
    }

    [Fact]
    public void MarkArrivedAtDestination_WhenInProgress_SetsArrived()
    {
        var shipment = Shipment.Create(_order);
        shipment.Start();

        shipment.MarkArrivedAtDestination();

        Assert.Equal(ShipmentStatus.Arrived, shipment.Status);
        Assert.True(shipment.ArrivedAtDestination);
    }

    [Fact]
    public void MarkArrivedAtDestination_WhenNotInProgress_Throws()
    {
        var shipment = Shipment.Create(_order);
        shipment.Start();
        shipment.Stop();

        Assert.Throws<InvalidOperationException>(() => shipment.MarkArrivedAtDestination());
    }

    [Fact]
    public void Cancel_WhenArrivedAtDestination_Throws()
    {
        var shipment = Shipment.Create(_order);
        shipment.Start();
        shipment.MarkArrivedAtDestination();

        Assert.Throws<InvalidOperationException>(() => shipment.Cancel());
    }

    [Fact]
    public void Cancel_WhenPending_SetsCancelled()
    {
        var shipment = Shipment.Create(_order);

        shipment.Cancel();

        Assert.Equal(ShipmentStatus.Cancelled, shipment.Status);
    }

    [Fact]
    public void Requeue_SetsPendingAndClearsDriver()
    {
        var shipment = Shipment.Create(_order);
        _order.SetAssignedDriver(Guid.NewGuid());
        shipment.Start();

        shipment.Requeue(RouteCancellationReason.VehicleBreakdown);

        Assert.Equal(ShipmentStatus.Pending, shipment.Status);
        Assert.Null(_order.AssignedDriverId);
        Assert.Contains(
            shipment.History,
            h => h.Status == ShipmentStatus.Pending && h.Note == RouteCancellationReason.VehicleBreakdown.ToString());
    }

    [Fact]
    public void Requeue_WithNote_RecordsCombinedNote()
    {
        var shipment = Shipment.Create(_order);

        shipment.Requeue(RouteCancellationReason.RouteAbandonment, "Driver did not show up");

        Assert.Equal(ShipmentStatus.Pending, shipment.Status);
        Assert.Contains(
            shipment.History,
            h => h.Note == "RouteAbandonment - Driver did not show up");
    }

    [Fact]
    public void Requeue_WhenFinalized_Throws()
    {
        var shipment = Shipment.Create(_order);
        shipment.Start();
        shipment.MarkArrivedAtDestination();
        shipment.RegisterDeliveryAttempt(null, true);

        Assert.Throws<InvalidOperationException>(() =>
            shipment.Requeue(RouteCancellationReason.Emergency));
        Assert.Equal(ShipmentStatus.Finalized, shipment.Status);
    }

    [Fact]
    public void Requeue_WhenDeliveryFailed_Throws()
    {
        var shipment = Shipment.Create(_order);
        shipment.Start();
        shipment.MarkArrivedAtDestination();
        var baseTime = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < Shipment.MaxDeliveryAttempts; i++)
        {
            shipment.RegisterDeliveryAttempt(null, false, attemptedAt: baseTime.AddMinutes(10 * i));
        }

        Assert.Equal(ShipmentStatus.DeliveryFailed, shipment.Status);
        Assert.Throws<InvalidOperationException>(() =>
            shipment.Requeue(RouteCancellationReason.Accident));
    }

    [Fact]
    public void Requeue_WhenCancelled_Throws()
    {
        var shipment = Shipment.Create(_order);
        shipment.Cancel();

        Assert.Throws<InvalidOperationException>(() =>
            shipment.Requeue(RouteCancellationReason.InclementWeather));
        Assert.Equal(ShipmentStatus.Cancelled, shipment.Status);
    }
}