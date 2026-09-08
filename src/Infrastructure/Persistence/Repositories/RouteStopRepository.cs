using Application.Persistence;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class RouteStopRepository : IRouteStopRepository
{
    private readonly LogiMatchDbContext _db;

    public RouteStopRepository(LogiMatchDbContext db)
    {
        _db = db;
    }

    public Task<RouteStop?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _db.RouteStops.FirstOrDefaultAsync(rs => rs.Id == id, cancellationToken);
    }

    public async Task AddAsync(RouteStop routeStop, CancellationToken cancellationToken = default)
    {
        await _db.RouteStops.AddAsync(routeStop, cancellationToken);
    }
}