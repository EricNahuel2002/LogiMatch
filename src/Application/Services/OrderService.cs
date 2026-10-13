using Application.Dtos.Orders;
using Application.Exceptions;
using Application.Persistence;
using Domain.Entities;
using FluentValidation;

namespace Application.Services;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orders;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateOrderRequest> _createOrderValidator;
    private readonly IValidator<CreateOrderItemRequest> _orderItemValidator;
    private readonly IValidator<AssignOrderDriverRequest> _assignDriverValidator;

    public OrderService(
        IOrderRepository orders,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IValidator<CreateOrderRequest> createOrderValidator,
        IValidator<CreateOrderItemRequest> orderItemValidator,
        IValidator<AssignOrderDriverRequest> assignDriverValidator)
    {
        _orders = orders;
        _users = users;
        _unitOfWork = unitOfWork;
        _createOrderValidator = createOrderValidator;
        _orderItemValidator = orderItemValidator;
        _assignDriverValidator = assignDriverValidator;
    }

    public async Task<Guid> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        await _createOrderValidator.ValidateAndThrowAsync(request, cancellationToken);

        var customer = await _users.GetCustomerByIdAsync(request.CustomerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.CustomerId);

        var admin = await _users.GetAdminByIdAsync(request.CreatedByAdminId, cancellationToken)
            ?? throw new NotFoundException(nameof(Admin), request.CreatedByAdminId);

        var items = request.Items
            .Select(i => OrderItem.Create(i.Name, i.Price, i.Quantity, i.WeightKg))
            .ToList();

        var order = Order.Create(
            customer,
            admin,
            items,
            request.DeliveryWindowStartAt,
            request.DeliveryWindowEndAt);

        await _orders.AddAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return order.Id;
    }

    public async Task AddItemAsync(Guid orderId, CreateOrderItemRequest request, CancellationToken cancellationToken = default)
    {
        await _orderItemValidator.ValidateAndThrowAsync(request, cancellationToken);

        var order = await _orders.GetByIdWithItemsAsync(orderId, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), orderId);

        order.AddItem(OrderItem.Create(request.Name, request.Price, request.Quantity, request.WeightKg));

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task AssignDriverAsync(Guid orderId, AssignOrderDriverRequest request, CancellationToken cancellationToken = default)
    {
        await _assignDriverValidator.ValidateAndThrowAsync(request, cancellationToken);

        var order = await _orders.GetByIdAsync(orderId, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), orderId);

        if (request.DriverId is { } driverId)
        {
            _ = await _users.GetDriverByIdAsync(driverId, cancellationToken)
                ?? throw new NotFoundException(nameof(Driver), driverId);
        }

        order.SetAssignedDriver(request.DriverId);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OrderSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var orders = await _orders.GetAllAsync(cancellationToken);

        return orders.Select(o => new OrderSummaryDto(
            o.Id,
            o.CustomerId,
            o.AssignedDriverId,
            o.DeliveryWindowStartAt,
            o.DeliveryWindowEndAt,
            o.CreatedAt,
            o.UpdatedAt)).ToList();
    }

    public async Task<OrderDetailsDto?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _orders.GetByIdWithFullDetailsAsync(orderId, cancellationToken);

        if (order is null)
        {
            return null;
        }

        return new OrderDetailsDto(
            order.Id,
            order.CustomerId,
            order.AssignedDriverId,
            order.DeliveryWindowStartAt,
            order.DeliveryWindowEndAt,
            order.Items.Select(i => new OrderItemDto(i.Id, i.Name, i.Price, i.Quantity, i.WeightKg)).ToArray(),
            order.CreatedAt,
            order.UpdatedAt);
    }
}