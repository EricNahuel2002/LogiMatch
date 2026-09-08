using Domain.Entities;

namespace Application.Persistence;

public interface IRouteStopRepository
{
    Task<RouteStop?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(RouteStop routeStop, CancellationToken cancellationToken = default);
}