using Domain.Entities;

namespace UnitTests.Domain;

public class OrderTests
{
    [Fact]
    public void Create_WithAtLeastOneItem_Succeeds()
    {
        var order = Order.Create(new Customer(), new Admin(), [OrderItem.Create("Item", 10m, 1, 5m)]);

        Assert.Single(order.Items);
        Assert.Null(order.ShipmentId);
    }

    [Fact]
    public void Create_WithValidDeliveryWindow_SetsWindow()
    {
        var start = new DateTime(2026, 9, 9, 9, 0, 0);
        var end = start.AddHours(1);

        var order = Order.Create(
            new Customer(),
            new Admin(),
            [OrderItem.Create("Item", 10m, 1, 5m)],
            start,
            end);

        Assert.Equal(start, order.DeliveryWindowStartAt);
        Assert.Equal(end, order.DeliveryWindowEndAt);
    }

    [Fact]
    public void Create_WithOnlyWindowStart_Throws()
    {
        var start = new DateTime(2026, 9, 9, 9, 0, 0);

        Assert.Throws<InvalidOperationException>(() =>
            Order.Create(new Customer(), new Admin(), [OrderItem.Create("Item", 10m, 1, 5m)], start, null));
    }

    [Fact]
    public void Create_WithOnlyWindowEnd_Throws()
    {
        var end = new DateTime(2026, 9, 9, 10, 0, 0);

        Assert.Throws<InvalidOperationException>(() =>
            Order.Create(new Customer(), new Admin(), [OrderItem.Create("Item", 10m, 1, 5m)], null, end));
    }

    [Fact]
    public void Create_WithWindowEndBeforeStart_Throws()
    {
        var start = new DateTime(2026, 9, 9, 10, 0, 0);
        var end = start.AddHours(-1);

        Assert.Throws<InvalidOperationException>(() =>
            Order.Create(new Customer(), new Admin(), [OrderItem.Create("Item", 10m, 1, 5m)], start, end));
    }

    [Fact]
    public void Create_WithoutItems_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Order.Create(new Customer(), new Admin(), []));
    }

    [Fact]
    public void AddItem_WhenNotDispatched_AddsItem()
    {
        var order = Order.Create(new Customer(), new Admin(), [OrderItem.Create("Item", 10m, 1, 5m)]);

        order.AddItem(OrderItem.Create("Item 2", 20m, 2, 5m));

        Assert.Equal(2, order.Items.Count);
    }

    [Fact]
    public void AddItem_WhenDispatched_Throws()
    {
        var order = Order.Create(new Customer(), new Admin(), [OrderItem.Create("Item", 10m, 1, 5m)]);
        Shipment.Create(order);

        Assert.Throws<InvalidOperationException>(
            () => order.AddItem(OrderItem.Create("Item 2", 20m, 2, 5m)));
    }

    [Fact]
    public void SetAssignedDriver_WhenDispatched_SetsDriver()
    {
        var order = Order.Create(new Customer(), new Admin(), [OrderItem.Create("Item", 10m, 1, 5m)]);
        Shipment.Create(order);

        order.SetAssignedDriver(Guid.NewGuid());

        Assert.NotNull(order.AssignedDriverId);
    }

    [Fact]
    public void CreateShipment_WhenAlreadyAssigned_Throws()
    {
        var order = Order.Create(new Customer(), new Admin(), [OrderItem.Create("Item", 10m, 1, 5m)]);
        Shipment.Create(order);

        Assert.Throws<InvalidOperationException>(() => Shipment.Create(order));
    }
}