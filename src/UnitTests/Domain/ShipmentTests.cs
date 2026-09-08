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
}