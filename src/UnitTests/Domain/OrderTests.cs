using Domain.Entities;

namespace UnitTests.Domain;

public class OrderTests
{
    [Fact]
    public void Create_WithAtLeastOneItem_Succeeds()
    {
        var order = Order.Create(new Customer(), new Admin(), [OrderItem.Create("Item", 10m, 1)]);

        Assert.Single(order.Items);
        Assert.Null(order.ShipmentId);
    }

    [Fact]
    public void Create_WithoutItems_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Order.Create(new Customer(), new Admin(), []));
    }

    [Fact]
    public void AddItem_WhenNotDispatched_AddsItem()
    {
        var order = Order.Create(new Customer(), new Admin(), [OrderItem.Create("Item", 10m, 1)]);

        order.AddItem(OrderItem.Create("Item 2", 20m, 2));

        Assert.Equal(2, order.Items.Count);
    }

    [Fact]
    public void AddItem_WhenDispatched_Throws()
    {
        var order = Order.Create(new Customer(), new Admin(), [OrderItem.Create("Item", 10m, 1)]);
        Shipment.Create(order);

        Assert.Throws<InvalidOperationException>(
            () => order.AddItem(OrderItem.Create("Item 2", 20m, 2)));
    }

    [Fact]
    public void SetAssignedDriver_WhenDispatched_Throws()
    {
        var order = Order.Create(new Customer(), new Admin(), [OrderItem.Create("Item", 10m, 1)]);
        Shipment.Create(order);

        Assert.Throws<InvalidOperationException>(() => order.SetAssignedDriver(Guid.NewGuid()));
    }

    [Fact]
    public void CreateShipment_WhenAlreadyAssigned_Throws()
    {
        var order = Order.Create(new Customer(), new Admin(), [OrderItem.Create("Item", 10m, 1)]);
        Shipment.Create(order);

        Assert.Throws<InvalidOperationException>(() => Shipment.Create(order));
    }
}