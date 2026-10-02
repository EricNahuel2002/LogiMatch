using Domain.Entities;

namespace Application.Persistence;

public interface IVehicleRepository
{
    Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the given vehicles in a single query. Vehicles that do not exist are simply
    /// absent from the result, so the caller can report them.
    /// </summary>
    Task<IReadOnlyList<Vehicle>> GetManyByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);
}