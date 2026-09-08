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

        var order = Order.Create(customer, admin, items);

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
}