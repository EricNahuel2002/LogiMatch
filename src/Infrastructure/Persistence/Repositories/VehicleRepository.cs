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
}