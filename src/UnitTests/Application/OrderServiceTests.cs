using Application.Dtos.Orders;
using Application.Exceptions;
using Application.Persistence;
using Application.Services;
using Domain.Entities;
using FluentValidation;
using Moq;

namespace UnitTests.Application;

public class OrderServiceTests
{
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IValidator<CreateOrderRequest>> _createOrderValidator =
        FluentValidationMocks.AlwaysValid<CreateOrderRequest>();
    private readonly Mock<IValidator<CreateOrderItemRequest>> _orderItemValidator =
        FluentValidationMocks.AlwaysValid<CreateOrderItemRequest>();
    private readonly Mock<IValidator<AssignOrderDriverRequest>> _assignDriverValidator =
        FluentValidationMocks.AlwaysValid<AssignOrderDriverRequest>();

    private OrderService CreateService()
    {
        return new OrderService(
            _orders.Object,
            _users.Object,
            _unitOfWork.Object,
            _createOrderValidator.Object,
            _orderItemValidator.Object,
            _assignDriverValidator.Object);
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_CreatesOrderAndSaves()
    {
        var customer = new Customer();
        var admin = new Admin();
        _users.Setup(u => u.GetCustomerByIdAsync(customer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);
        _users.Setup(u => u.GetAdminByIdAsync(admin.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(admin);

        Order? created = null;
        _orders.Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Callback<Order, CancellationToken>((order, _) => created = order)
            .Returns(Task.CompletedTask);

        var request = new CreateOrderRequest
        {
            CustomerId = customer.Id,
            CreatedByAdminId = admin.Id,
            Items =
            [
                new CreateOrderItemRequest { Name = "Item A", Price = 10m, Quantity = 2, WeightKg = 3m },
                new CreateOrderItemRequest { Name = "Item B", Price = 5m, Quantity = 1, WeightKg = 4m }
            ]
        };

        var service = CreateService();
        var id = await service.CreateAsync(request);

        Assert.Equal(created!.Id, id);
        Assert.Same(customer, created.Customer);
        Assert.Same(admin, created.CreatedBy);
        Assert.Equal(2, created.Items.Count);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenCustomerNotFound_ThrowsNotFoundException()
    {
        _users.Setup(u => u.GetCustomerByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer?)null);

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.CreateAsync(new CreateOrderRequest
            {
                CustomerId = Guid.NewGuid(),
                CreatedByAdminId = Guid.NewGuid(),
                Items = [new CreateOrderItemRequest { Name = "Item", Price = 1m, Quantity = 1, WeightKg = 2m }]
            }));
    }

    [Fact]
    public async Task CreateAsync_WhenAdminNotFound_ThrowsNotFoundException()
    {
        var customer = new Customer();
        _users.Setup(u => u.GetCustomerByIdAsync(customer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);
        _users.Setup(u => u.GetAdminByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Admin?)null);

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.CreateAsync(new CreateOrderRequest
            {
                CustomerId = customer.Id,
                CreatedByAdminId = Guid.NewGuid(),
                Items = [new CreateOrderItemRequest { Name = "Item", Price = 1m, Quantity = 1, WeightKg = 2m }]
            }));
    }

    [Fact]
    public async Task AddItemAsync_AddsItemToOrderAndSaves()
    {
        var order = Order.Create(new Customer(), new Admin(), [OrderItem.Create("Existing", 1m, 1, 5m)]);
        _orders.Setup(r => r.GetByIdWithItemsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var service = CreateService();
        await service.AddItemAsync(
            order.Id,
            new CreateOrderItemRequest { Name = "New", Price = 2m, Quantity = 3, WeightKg = 4m });

        Assert.Equal(2, order.Items.Count);
        Assert.Contains(order.Items, i => i.Name == "New" && i.Quantity == 3);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddItemAsync_WhenOrderNotFound_ThrowsNotFoundException()
    {
        _orders.Setup(r => r.GetByIdWithItemsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.AddItemAsync(
                Guid.NewGuid(),
                new CreateOrderItemRequest { Name = "Item", Price = 1m, Quantity = 1, WeightKg = 2m }));
    }

    [Fact]
    public async Task AssignDriverAsync_SetsDriverAndSaves()
    {
        var order = Order.Create(new Customer(), new Admin(), [OrderItem.Create("Item", 1m, 1, 5m)]);
        var driver = new Driver();
        _orders.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _users.Setup(u => u.GetDriverByIdAsync(driver.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(driver);

        var service = CreateService();
        await service.AssignDriverAsync(order.Id, new AssignOrderDriverRequest { DriverId = driver.Id });

        Assert.Equal(driver.Id, order.AssignedDriverId);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssignDriverAsync_WhenNoDriverProvided_UnassignsWithoutLoadingDriver()
    {
        var order = Order.Create(new Customer(), new Admin(), [OrderItem.Create("Item", 1m, 1, 5m)]);
        _orders.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var service = CreateService();
        await service.AssignDriverAsync(order.Id, new AssignOrderDriverRequest { DriverId = null });

        Assert.Null(order.AssignedDriverId);
        _users.Verify(u => u.GetDriverByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AssignDriverAsync_WhenDriverNotFound_ThrowsNotFoundException()
    {
        var order = Order.Create(new Customer(), new Admin(), [OrderItem.Create("Item", 1m, 1, 5m)]);
        _orders.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _users.Setup(u => u.GetDriverByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Driver?)null);

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.AssignDriverAsync(order.Id, new AssignOrderDriverRequest { DriverId = Guid.NewGuid() }));
    }
}