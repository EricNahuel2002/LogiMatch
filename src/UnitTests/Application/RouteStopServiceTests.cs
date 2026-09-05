using Application.Dtos.RouteStops;
using Application.Exceptions;
using Application.Persistence;
using Application.Services;
using Domain.Entities;
using FluentValidation;
using Moq;

namespace UnitTests.Application;

public class RouteStopServiceTests
{
    private readonly Mock<IRouteStopRepository> _routeStops = new();
    private readonly Mock<IShipmentRepository> _shipments = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IValidator<CreateRouteStopRequest>> _createRouteStopValidator =
        FluentValidationMocks.AlwaysValid<CreateRouteStopRequest>();

    private RouteStopService CreateService()
    {
        return new RouteStopService(
            _routeStops.Object,
            _shipments.Object,
            _unitOfWork.Object,
            _createRouteStopValidator.Object);
    }

    private static Shipment BuildPendingShipment()
    {
        var order = Order.Create(new Customer(), new Admin(), [OrderItem.Create("Item", 1m, 1)]);
        return Shipment.Create(order);
    }

    [Fact]
    public async Task CreateAsync_CreatesRouteStopForShipmentAndSaves()
    {
        var shipment = BuildPendingShipment();
        _shipments.Setup(r => r.GetByIdAsync(shipment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipment);

        RouteStop? created = null;
        _routeStops.Setup(r => r.AddAsync(It.IsAny<RouteStop>(), It.IsAny<CancellationToken>()))
            .Callback<RouteStop, CancellationToken>((stop, _) => created = stop)
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var id = await service.CreateAsync(new CreateRouteStopRequest
        {
            ShipmentId = shipment.Id,
            Latitude = 1m,
            Longitude = 2m,
            StopOrder = 3,
            Name = "Client A"
        });

        Assert.Equal(created!.Id, id);
        Assert.Equal(shipment.Id, created.ShipmentId);
        Assert.Equal(3, created.StopOrder);
        Assert.Equal("Client A", created.Name);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenShipmentNotFound_ThrowsNotFoundException()
    {
        _shipments.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Shipment?)null);

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.CreateAsync(new CreateRouteStopRequest
            {
                ShipmentId = Guid.NewGuid(),
                Latitude = 1m,
                Longitude = 2m,
                StopOrder = 1
            }));
    }
}