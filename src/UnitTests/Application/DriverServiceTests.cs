using Application.Dtos.Drivers;
using Application.Exceptions;
using Application.Persistence;
using Application.Services;
using Domain.Entities;
using Domain.ValueObjects;
using FluentValidation;
using Moq;

namespace UnitTests.Application;

public class DriverServiceTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IValidator<UpdateDriverLocationRequest>> _updateLocationValidator =
        FluentValidationMocks.AlwaysValid<UpdateDriverLocationRequest>();

    private DriverService CreateService()
    {
        return new DriverService(_users.Object, _unitOfWork.Object, _updateLocationValidator.Object);
    }

    [Fact]
    public async Task UpdateLocationAsync_SetsLocationAndSaves()
    {
        var driver = new Driver();
        _users.Setup(u => u.GetDriverByIdAsync(driver.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(driver);

        var service = CreateService();
        await service.UpdateLocationAsync(driver.Id, new UpdateDriverLocationRequest
        {
            Latitude = -34.6m,
            Longitude = -58.4m
        });

        Assert.Equal(new Coordinate(-34.6m, -58.4m), driver.CurrentLocation);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateLocationAsync_WhenDriverNotFound_ThrowsNotFoundException()
    {
        _users.Setup(u => u.GetDriverByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Driver?)null);

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.UpdateLocationAsync(Guid.NewGuid(), new UpdateDriverLocationRequest()));
    }
}