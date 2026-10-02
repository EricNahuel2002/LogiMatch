using Application.Persistence;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class VehicleRepository : IVehicleRepository
{
    private readonly LogiMatchDbContext _db;

    public VehicleRepository(LogiMatchDbContext db)
    {
        _db = db;
    }

    public Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _db.Vehicles.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Vehicle>> GetManyByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        return await _db.Vehicles
            .AsNoTracking()
            .Where(v => ids.Contains(v.Id))
            .ToListAsync(cancellationToken);
    }
}