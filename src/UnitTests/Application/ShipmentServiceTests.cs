using Application.Dtos.Shipments;
using Application.Exceptions;
using Application.Persistence;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Moq;

namespace UnitTests.Application;

public class ShipmentServiceTests
{
    private readonly Mock<IShipmentRepository> _shipments = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IRouteStopRepository> _routeStops = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IValidator<StopShipmentRequest>> _stopValidator =
        FluentValidationMocks.AlwaysValid<StopShipmentRequest>();
    private readonly Mock<IValidator<ResumeShipmentRequest>> _resumeValidator =
        FluentValidationMocks.AlwaysValid<ResumeShipmentRequest>();
    private readonly Mock<IValidator<MarkArrivedRequest>> _markArrivedValidator =
        FluentValidationMocks.AlwaysValid<MarkArrivedRequest>();
    private readonly Mock<IValidator<CancelShipmentRequest>> _cancelValidator =
        FluentValidationMocks.AlwaysValid<CancelShipmentRequest>();
    private readonly Mock<IValidator<RegisterDeliveryAttemptRequest>> _deliveryAttemptValidator =
        FluentValidationMocks.AlwaysValid<RegisterDeliveryAttemptRequest>();

    private ShipmentService CreateService()
    {
        return new ShipmentService(
            _shipments.Object,
            _orders.Object,
            _routeStops.Object,
            _unitOfWork.Object,
            _stopValidator.Object,
            _resumeValidator.Object,
            _markArrivedValidator.Object,
            _cancelValidator.Object,
            _deliveryAttemptValidator.Object);
    }

    private static Shipment BuildPendingShipment()
    {
        var order = Order.Create(new Customer(), new Admin(), [OrderItem.Create("Item", 1m, 1)]);
        return Shipment.Create(order);
    }

    [Fact]
    public async Task CreateAsync_CreatesShipmentForOrderAndSaves()
    {
        var order = Order.Create(new Customer(), new Admin(), [OrderItem.Create("Item", 1m, 1)]);
        _orders.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        Shipment? created = null;
        _shipments.Setup(r => r.AddAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()))
            .Callback<Shipment, CancellationToken>((shipment, _) => created = shipment)
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var id = await service.CreateAsync(order.Id);

        Assert.Equal(created!.Id, id);
        Assert.Equal(order.Id, created.OrderId);
        Assert.Equal(created.Id, order.ShipmentId);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenOrderNotFound_ThrowsNotFoundException()
    {
        _orders.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task StartAsync_StartsShipmentAndSaves()
    {
        var shipment = BuildPendingShipment();
        _shipments.Setup(r => r.GetByIdAsync(shipment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipment);

        var service = CreateService();
        await service.StartAsync(shipment.Id);

        Assert.Equal(ShipmentStatus.InProgress, shipment.Status);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StopAsync_StopsInProgressShipmentAndSaves()
    {
        var shipment = BuildPendingShipment();
        shipment.Start();
        _shipments.Setup(r => r.GetByIdAsync(shipment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipment);

        var service = CreateService();
        await service.StopAsync(shipment.Id, new StopShipmentRequest { Note = "Lunch break" });

        Assert.Equal(ShipmentStatus.Stopped, shipment.Status);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResumeAsync_ResumesStoppedShipmentAndSaves()
    {
        var shipment = BuildPendingShipment();
        shipment.Start();
        shipment.Stop();
        _shipments.Setup(r => r.GetByIdAsync(shipment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipment);

        var service = CreateService();
        await service.ResumeAsync(shipment.Id, new ResumeShipmentRequest());

        Assert.Equal(ShipmentStatus.InProgress, shipment.Status);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkArrivedAsync_MarksArrivedAndSaves()
    {
        var shipment = BuildPendingShipment();
        shipment.Start();
        _shipments.Setup(r => r.GetByIdAsync(shipment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipment);

        var service = CreateService();
        await service.MarkArrivedAsync(shipment.Id, new MarkArrivedRequest());

        Assert.Equal(ShipmentStatus.Arrived, shipment.Status);
        Assert.True(shipment.ArrivedAtDestination);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_CancelsShipmentAndSaves()
    {
        var shipment = BuildPendingShipment();
        _shipments.Setup(r => r.GetByIdAsync(shipment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipment);

        var service = CreateService();
        await service.CancelAsync(shipment.Id, new CancelShipmentRequest { Note = "Client request" });

        Assert.Equal(ShipmentStatus.Cancelled, shipment.Status);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterDeliveryAttemptAsync_WhenSucceeded_FinalizesShipment()
    {
        var shipment = BuildPendingShipment();
        shipment.Start();
        shipment.MarkArrivedAtDestination();
        _shipments.Setup(r => r.GetByIdWithDetailsAsync(shipment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipment);

        var service = CreateService();
        await service.RegisterDeliveryAttemptAsync(
            shipment.Id,
            new RegisterDeliveryAttemptRequest { Succeeded = true });

        Assert.Equal(ShipmentStatus.Finalized, shipment.Status);
        Assert.Single(shipment.DeliveryAttempts);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterDeliveryAttemptAsync_WhenFailed_KeepsShipmentArrived()
    {
        var shipment = BuildPendingShipment();
        shipment.Start();
        shipment.MarkArrivedAtDestination();
        _shipments.Setup(r => r.GetByIdWithDetailsAsync(shipment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipment);

        var service = CreateService();
        await service.RegisterDeliveryAttemptAsync(
            shipment.Id,
            new RegisterDeliveryAttemptRequest { Succeeded = false, Note = "No one home" });

        Assert.Equal(ShipmentStatus.Arrived, shipment.Status);
        Assert.Single(shipment.DeliveryAttempts);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}