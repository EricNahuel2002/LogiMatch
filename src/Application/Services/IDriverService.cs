using Application.Dtos.Drivers;

namespace Application.Services;

public interface IDriverService
{
    Task UpdateLocationAsync(
        Guid driverId,
        UpdateDriverLocationRequest request,
        CancellationToken cancellationToken = default);
}