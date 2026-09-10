using Application.Persistence;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class RouteRepository : IRouteRepository
{
    private readonly LogiMatchDbContext _db;

    public RouteRepository(LogiMatchDbContext db)
    {
        _db = db;
    }

    public Task<Route?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _db.Routes
            .Include(r => r.RouteStops)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public Task<Route?> GetActiveByDriverIdAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        return _db.Routes
            .Where(r => r.DriverId == driverId && r.IsActive)
            .Include(r => r.RouteStops)
                .ThenInclude(rs => rs.Shipment)
                    .ThenInclude(s => s.Order)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(Route route, CancellationToken cancellationToken = default)
    {
        await _db.Routes.AddAsync(route, cancellationToken);
    }
}