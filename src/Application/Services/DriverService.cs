using Application.Dtos.Drivers;
using Application.Exceptions;
using Application.Persistence;
using Domain.Entities;
using Domain.ValueObjects;
using FluentValidation;

namespace Application.Services;

public class DriverService : IDriverService
{
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<UpdateDriverLocationRequest> _updateLocationValidator;

    public DriverService(
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IValidator<UpdateDriverLocationRequest> updateLocationValidator)
    {
        _users = users;
        _unitOfWork = unitOfWork;
        _updateLocationValidator = updateLocationValidator;
    }

    public async Task UpdateLocationAsync(
        Guid driverId,
        UpdateDriverLocationRequest request,
        CancellationToken cancellationToken = default)
    {
        await _updateLocationValidator.ValidateAndThrowAsync(request, cancellationToken);

        var driver = await _users.GetDriverByIdAsync(driverId, cancellationToken)
            ?? throw new NotFoundException(nameof(Driver), driverId);

        driver.SetCurrentLocation(new Coordinate(request.Latitude, request.Longitude));

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}