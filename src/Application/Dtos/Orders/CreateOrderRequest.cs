namespace Application.Dtos.Orders;

public class CreateOrderRequest
{
    public Guid CustomerId { get; init; }
    public Guid CreatedByAdminId { get; init; }
    public DateTime? DeliveryWindowStartAt { get; init; }
    public DateTime? DeliveryWindowEndAt { get; init; }
    public IReadOnlyList<CreateOrderItemRequest> Items { get; init; } = [];
}