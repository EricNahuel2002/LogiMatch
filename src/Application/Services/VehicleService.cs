using Application.Dtos.Vehicles;
using Application.Persistence;

namespace Application.Services;

public interface IVehicleService
{
    Task<IReadOnlyList<VehicleSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);
}

public class VehicleService : IVehicleService
{
    private readonly IVehicleRepository _vehicles;

    public VehicleService(IVehicleRepository vehicles)
    {
        _vehicles = vehicles;
    }

    public async Task<IReadOnlyList<VehicleSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var vehicles = await _vehicles.GetAllAsync(cancellationToken);
        return vehicles.Select(v => new VehicleSummaryDto(v.Id, v.LicensePlate, v.Brand, v.Model, v.CapacityKg, v.Active, v.CreatedAt, v.UpdatedAt)).ToList();
    }
}
