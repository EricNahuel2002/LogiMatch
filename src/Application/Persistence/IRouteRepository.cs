using Domain.Entities;

namespace Application.Persistence;

public interface IRouteRepository
{
    Task<Route?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Route?> GetActiveByDriverIdAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Route>> GetActiveRoutesForDelayRiskAsync(
        CancellationToken cancellationToken = default);

    Task AddAsync(Route route, CancellationToken cancellationToken = default);
}