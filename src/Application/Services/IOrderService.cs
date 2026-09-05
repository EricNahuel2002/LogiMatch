using Application.Dtos.Orders;

namespace Application.Services;

public interface IOrderService
{
    Task<Guid> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken = default);

    Task AddItemAsync(Guid orderId, CreateOrderItemRequest request, CancellationToken cancellationToken = default);

    Task AssignDriverAsync(Guid orderId, AssignOrderDriverRequest request, CancellationToken cancellationToken = default);
}