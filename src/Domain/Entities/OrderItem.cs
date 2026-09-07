using Domain.Common;

namespace Domain.Entities;

public class OrderItem : BaseEntity
{
    private OrderItem()
    {
    }

    public Guid OrderId { get; private set; }
    public Order Order { get; private set; } = null!;

    public string Name { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public int Quantity { get; private set; }
    public decimal WeightKg { get; private set; }

    public static OrderItem Create(string name, decimal price, int quantity, decimal weightKg)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (quantity <= 0)
        {
            throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));
        }

        if (price < 0)
        {
            throw new ArgumentException("Price cannot be negative.", nameof(price));
        }

        if (weightKg <= 0)
        {
            throw new ArgumentException("Weight must be greater than zero.", nameof(weightKg));
        }

        return new OrderItem
        {
            Name = name,
            Price = price,
            Quantity = quantity,
            WeightKg = weightKg
        };
    }
}