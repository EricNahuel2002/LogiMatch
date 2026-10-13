using Application.Dtos.Drivers;
using Application.Persistence;

namespace Application.Services;

public interface IDriverService
{
    Task UpdateLocationAsync(
        Guid driverId,
        UpdateDriverLocationRequest request,
        CancellationToken cancellationToken = default);

    Task CancelCurrentRouteAsync(
        Guid driverId,
        CancelRouteRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DriverSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<DriverRouteCurrentDto?> GetCurrentRouteAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DriverShipmentDto>> GetShipmentsAsync(Guid driverId, CancellationToken cancellationToken = default);
}
