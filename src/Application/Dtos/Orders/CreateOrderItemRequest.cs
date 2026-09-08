namespace Application.Dtos.Orders;

public class CreateOrderItemRequest
{
    public string Name { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public int Quantity { get; init; }
    public decimal WeightKg { get; init; }
}