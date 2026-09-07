using Domain.Entities;

namespace UnitTests.Domain;

public class OrderItemTests
{
    [Fact]
    public void Create_WithWeight_SetsProperties()
    {
        var item = OrderItem.Create("Item", 10m, 2, 5m);

        Assert.Equal("Item", item.Name);
        Assert.Equal(10m, item.Price);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(5m, item.WeightKg);
    }

    [Fact]
    public void Create_WithNonPositiveWeight_Throws()
    {
        Assert.Throws<ArgumentException>(() => OrderItem.Create("Item", 10m, 1, 0m));
    }
}